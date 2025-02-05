using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Grpc.Core;

namespace BlazorFridaApp.MemoryScanner.Services;

public interface IPythonProcessManager : IDisposable
{
    Task EnsureServerRunning();
    Task StopServer();
    bool IsRunning { get; }
    int Port { get; }
}

public class PythonProcessManager : IPythonProcessManager
{
    private readonly ILogger<PythonProcessManager> _logger;
    private Process? _pythonProcess;
    private readonly string _pythonPath;
    private readonly string _serverScript;
    private readonly int _port;
    private bool _isRunning;
    private int? _serverProcessId;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CancellationTokenSource _processTokenSource = new();

    public bool IsRunning => _isRunning && _pythonProcess?.HasExited == false;
    public int Port => _port;

    public PythonProcessManager(ILogger<PythonProcessManager> logger)
    {
        _logger = logger;
        _port = 50051; // Default gRPC port
        _pythonPath = "python"; // Use system Python
        _serverScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "MemoryScanner", "Native", "memory_scanner_server.py");
        _logger.LogInformation("Initialized PythonProcessManager with port {Port} and script path {ScriptPath}", _port, _serverScript);
    }

    public async Task EnsureServerRunning()
    {
        if (_isRunning)
        {
            _logger.LogDebug("Server already running on port {Port}, checking process state...", _port);
            if (_pythonProcess?.HasExited ?? true)
            {
                _logger.LogWarning("Process has exited unexpectedly, will restart");
                _isRunning = false;
            }
            else
            {
                return;
            }
        }

        await _lock.WaitAsync();
        try
        {
            if (_isRunning)
            {
                _logger.LogDebug("Server already running on port {Port} (after lock)", _port);
                return;
            }

            _logger.LogInformation("Starting Python gRPC server on port {Port}...", _port);

            // Kill existing server process if we have its ID
            if (_serverProcessId.HasValue)
            {
                try
                {
                    var existingProcess = Process.GetProcessById(_serverProcessId.Value);
                    _logger.LogWarning("Found existing server process (PID: {Pid}), attempting to kill it", _serverProcessId.Value);
                    existingProcess.Kill(true);
                    await existingProcess.WaitForExitAsync();
                }
                catch (ArgumentException)
                {
                    _logger.LogInformation("No process found with PID {Pid}", _serverProcessId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while trying to kill existing server process");
                }
                _serverProcessId = null;
            }

            // Install requirements if needed
            await InstallRequirements();

            // Start the Python process with unique identifier in arguments
            var processId = Process.GetCurrentProcess().Id;
            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{_serverScript}\" --port {_port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_serverScript) ?? string.Empty
            };

            _logger.LogDebug("Starting process with command: {Command} {Args}", startInfo.FileName, startInfo.Arguments);

            _pythonProcess = new Process { StartInfo = startInfo };

            // Handle output asynchronously
            _pythonProcess.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogInformation("Python Server: {Output}", e.Data);
                }
            };

            _pythonProcess.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogError("Python Server Error: {Error}", e.Data);
                }
            };

            _pythonProcess.Exited += (sender, e) =>
            {
                var exitCode = _pythonProcess?.ExitCode ?? -1;
                _logger.LogWarning("Python server process exited unexpectedly with code {ExitCode}", exitCode);
                _isRunning = false;
            };

            // Start the process and store its ID
            _pythonProcess.Start();
            _serverProcessId = _pythonProcess.Id;
            _pythonProcess.BeginOutputReadLine();
            _pythonProcess.BeginErrorReadLine();

            // Wait for the server to be ready
            _logger.LogDebug("Waiting for server to be ready on port {Port} (PID: {Pid})...", _port, _serverProcessId);
            await WaitForServerReady();

            _isRunning = true;
            _logger.LogInformation("Python gRPC server started successfully on port {Port} (PID: {Pid})", _port, _serverProcessId);
        }
        catch (Exception ex)
        {
            _isRunning = false;
            _logger.LogError(ex, "Failed to start Python gRPC server on port {Port}", _port);
            throw new PythonServerException($"Failed to start Python gRPC server on port {_port}", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task InstallRequirements()
    {
        var requirementsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "MemoryScanner", "Native", "requirements.txt");

        if (!File.Exists(requirementsPath))
        {
            _logger.LogWarning("Requirements file not found at {Path}", requirementsPath);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"-m pip install -r \"{requirementsPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new PythonServerException("Failed to start pip install process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Pip install failed: {Error}", error);
                throw new PythonServerException($"Pip install failed with exit code {process.ExitCode}");
            }

            _logger.LogInformation("Python requirements installed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install Python requirements");
            throw new PythonServerException("Failed to install Python requirements", ex);
        }
    }

    private async Task WaitForServerReady()
    {
        var retryCount = 0;
        const int maxRetries = 50;  // Increased max retries
        const int retryDelayMs = 200; // Increased delay between retries
        const int timeoutMs = 1000;   // Increased timeout for each attempt

        while (retryCount < maxRetries)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                using var channel = Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{_port}");
                var client = new Proto.Health.Health.HealthClient(channel);
                
                // Use empty string to check overall service health
                var request = new Proto.Health.HealthCheckRequest { Service = "" };
                var response = await client.CheckAsync(request, cancellationToken: cts.Token);
                
                if (response.Status == Proto.Health.HealthCheckResponse.Types.ServingStatus.Serving)
                {
                    _logger.LogInformation("Server health check passed after {Attempts} attempts", retryCount + 1);
                    return;
                }
                
                _logger.LogWarning("Server reported non-serving status: {Status}", response.Status);
                throw new PythonServerException($"Server reported non-serving status: {response.Status}");
            }
            catch (Exception ex) when (ex is RpcException or TimeoutException)
            {
                _logger.LogDebug("Server not ready yet (attempt {RetryCount} of {MaxRetries})", retryCount + 1, maxRetries);
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    _logger.LogError(ex, "Server failed to start after {MaxRetries} attempts", maxRetries);
                    throw new PythonServerException("Server failed to start within the expected timeframe", ex);
                }
                await Task.Delay(retryDelayMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while waiting for server");
                throw;
            }
        }
    }

    public async Task StopServer()
    {
        if (!_isRunning && !_serverProcessId.HasValue) return;

        await _lock.WaitAsync();
        try
        {
            if (!_isRunning && !_serverProcessId.HasValue) return;

            _logger.LogInformation("Stopping Python gRPC server (PID: {Pid})...", _serverProcessId);

            if (_serverProcessId.HasValue)
            {
                try
                {
                    var process = Process.GetProcessById(_serverProcessId.Value);
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        await process.WaitForExitAsync();
                        _logger.LogInformation("Successfully killed process {Pid}", _serverProcessId.Value);
                    }
                }
                catch (ArgumentException)
                {
                    _logger.LogInformation("Process {Pid} no longer exists", _serverProcessId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error killing process {Pid}", _serverProcessId.Value);
                }
                _serverProcessId = null;
            }

            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                try
                {
                    _pythonProcess.Kill(true);
                    await _pythonProcess.WaitForExitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping Python process");
                }
            }

            _isRunning = false;
            _logger.LogInformation("Python gRPC server stopped");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _processTokenSource.Cancel();
        StopServer().Wait();
        _processTokenSource.Dispose();
        _lock.Dispose();
        _pythonProcess?.Dispose();
    }
}

public class PythonServerException : Exception
{
    public PythonServerException(string message) : base(message) { }
    public PythonServerException(string message, Exception innerException) 
        : base(message, innerException) { }
}