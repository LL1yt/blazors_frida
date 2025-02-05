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
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CancellationTokenSource _processTokenSource = new();

    public bool IsRunning => _isRunning;
    public int Port => _port;

    public PythonProcessManager(ILogger<PythonProcessManager> logger)
    {
        _logger = logger;
        _port = 50051; // Default gRPC port
        _pythonPath = "python"; // Use system Python
        _serverScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "MemoryScanner", "Native", "memory_scanner_server.py");
    }

    public async Task EnsureServerRunning()
    {
        if (_isRunning) return;

        await _lock.WaitAsync();
        try
        {
            if (_isRunning) return;

            _logger.LogInformation("Starting Python gRPC server...");

            // Install requirements if needed
            await InstallRequirements();

            // Start the Python process
            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{_serverScript}\" --port {_port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

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
                _logger.LogWarning("Python server process exited unexpectedly");
                _isRunning = false;
            };

            // Start the process
            _pythonProcess.Start();
            _pythonProcess.BeginOutputReadLine();
            _pythonProcess.BeginErrorReadLine();

            // Wait for the server to be ready
            await WaitForServerReady();

            _isRunning = true;
            _logger.LogInformation("Python gRPC server started successfully on port {Port}", _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Python gRPC server");
            throw new PythonServerException("Failed to start Python gRPC server", ex);
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
        const int maxRetries = 30;
        const int retryDelayMs = 100;

        while (retryCount < maxRetries)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                using var channel = Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{_port}");
                var client = new Proto.Health.Health.HealthClient(channel);
                
                var request = new Proto.Health.HealthCheckRequest { Service = "memory_scanner.MemoryScanner" };
                var response = await client.CheckAsync(request, cancellationToken: cts.Token);
                
                if (response.Status == Proto.Health.HealthCheckResponse.Types.ServingStatus.Serving)
                {
                    return;
                }
                
                throw new PythonServerException($"Server reported non-serving status: {response.Status}");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Server not ready yet (attempt {RetryCount} of {MaxRetries})", retryCount + 1, maxRetries);
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    throw new PythonServerException("Server failed to start within the expected timeframe", ex);
                }
                await Task.Delay(retryDelayMs);
            }
        }
    }

    public async Task StopServer()
    {
        if (!_isRunning) return;

        await _lock.WaitAsync();
        try
        {
            if (!_isRunning) return;

            _logger.LogInformation("Stopping Python gRPC server...");

            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                _pythonProcess.Kill(true);
                await _pythonProcess.WaitForExitAsync();
            }

            _isRunning = false;
            _logger.LogInformation("Python gRPC server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Python gRPC server");
            throw;
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