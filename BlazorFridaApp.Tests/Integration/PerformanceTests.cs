using BlazorFridaApp.MemoryScanner.Models;
using BlazorFridaApp.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;

namespace BlazorFridaApp.Tests.Integration;

[Collection("Integration Tests")]
public class PerformanceTests : ServiceTestBase
{
    private readonly ILogger<PerformanceTests> _logger;
    private readonly Stopwatch _stopwatch;

    public PerformanceTests() : base()
    {
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = factory.CreateLogger<PerformanceTests>();
        _stopwatch = new Stopwatch();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task ShouldMaintainScanPerformance(int numOperations)
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        var times = new List<long>();
        const int batchSize = 100;
        var numBatches = (int)Math.Ceiling(numOperations / (double)batchSize);

        _logger.LogInformation("Starting performance test with {Operations} operations in {Batches} batches", 
            numOperations, numBatches);

        for (int batch = 0; batch < numBatches; batch++)
        {
            var batchTasks = new List<Task>();
            _stopwatch.Restart();

            // Run a batch of operations
            for (int i = 0; i < batchSize && (batch * batchSize + i) < numOperations; i++)
            {
                var value = BitConverter.GetBytes(i);
                batchTasks.Add(ScannerService.ScanMemoryAsync(
                    sessionId,
                    "int32",
                    value,
                    "exact",
                    new[] { ((ulong)0x1000, (ulong)0x10000) }));
            }

            await Task.WhenAll(batchTasks);
            _stopwatch.Stop();

            times.Add(_stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Batch {Batch} completed in {Time}ms", batch + 1, _stopwatch.ElapsedMilliseconds);
        }

        // Calculate statistics
        var avgTime = times.Average();
        var stdDev = Math.Sqrt(times.Average(t => Math.Pow(t - avgTime, 2)));
        var maxTime = times.Max();

        _logger.LogInformation("Performance stats - Avg: {Avg}ms, StdDev: {StdDev}ms, Max: {Max}ms",
            avgTime, stdDev, maxTime);

        // Assert performance characteristics
        Assert.True(stdDev / avgTime < 0.5, "Performance variance too high"); // Less than 50% variance
        Assert.True(maxTime / avgTime < 3.0, "Maximum time too high"); // No single operation should take 3x average
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public async Task ShouldHandleConcurrentSessions(int numSessions)
    {
        _logger.LogInformation("Starting concurrent session test with {Sessions} sessions", numSessions);

        // Start multiple notepads
        var processes = new List<System.Diagnostics.Process>();
        var sessions = new List<string>();

        try
        {
            // Start processes
            for (int i = 0; i < numSessions; i++)
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo("notepad.exe");
                var process = System.Diagnostics.Process.Start(startInfo);
                Assert.NotNull(process);
                processes.Add(process);
            }

            // Wait for processes to start
            await Task.Delay(1000 * Math.Min(5, numSessions / 10));

            _stopwatch.Restart();

            // Attach to all processes
            foreach (var process in processes)
            {
                var (success, sessionId) = await ProcessService.AttachToProcessAsync(process.Id);
                Assert.True(success);
                Assert.NotNull(sessionId);
                sessions.Add(sessionId);
            }

            var attachTime = _stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("Attached to {Sessions} processes in {Time}ms", 
                sessions.Count, attachTime);

            // Run concurrent operations on all sessions
            _stopwatch.Restart();
            
            var tasks = sessions.SelectMany(sid => new Task[]
            {
                ScannerService.ScanMemoryAsync(
                    sid,
                    "int32",
                    BitConverter.GetBytes(12345),
                    "exact",
                    new[] { ((ulong)0x1000, (ulong)0x10000) }),
                MemoryService.ReadMemoryAsync(sid, 0x1000, 4),
                MemoryService.WriteMemoryAsync(sid, 0x1000, new byte[] { 1, 2, 3, 4 })
            }).ToList();

            await Task.WhenAll(tasks);
            var operationTime = _stopwatch.ElapsedMilliseconds;
            
            _logger.LogInformation("Completed {Operations} operations across {Sessions} sessions in {Time}ms",
                tasks.Count, sessions.Count, operationTime);

            // Assert reasonable timing
            var avgTimePerSession = operationTime / (double)numSessions;
            Assert.True(avgTimePerSession < 1000, "Too slow per session"); // Less than 1 second per session
        }
        finally
        {
            // Clean up
            foreach (var sid in sessions)
            {
                await ProcessService.DetachFromProcessAsync(sid);
            }
            foreach (var proc in processes)
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                }
                proc.Dispose();
            }
        }
    }

    [Fact]
    public async Task ShouldHandleResourceRecovery()
    {
        // Find and attach to notepad
        var notepad = await TestHelper.FindTestProcessAsync(this, "notepad.exe", _logger);
        var sessionId = await TestHelper.AttachToProcessAsync(this, notepad.Id, _logger);

        const int iterations = 10;
        var times = new List<long>();

        for (int i = 0; i < iterations; i++)
        {
            _stopwatch.Restart();

            // Run a memory-intensive operation
            var scanResult = await ScannerService.ScanMemoryAsync(
                sessionId,
                "int32",
                BitConverter.GetBytes(12345),
                "exact",
                new[] { ((ulong)0, (ulong)0x7FFFFFFF) });

            _stopwatch.Stop();
            times.Add(_stopwatch.ElapsedMilliseconds);

            // Force GC between iterations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(100);

            _logger.LogInformation("Iteration {Iteration} completed in {Time}ms", i + 1, times[i]);
        }

        // Verify no significant degradation
        var correlation = CalculateCorrelation(Enumerable.Range(1, times.Count).Select(x => (double)x), times.Select(x => (double)x));
        Assert.True(correlation < 0.7, "Performance degrading over time");
    }

    private static double CalculateCorrelation(IEnumerable<double> xs, IEnumerable<double> ys)
    {
        var xArray = xs.ToArray();
        var yArray = ys.ToArray();
        var xMean = xArray.Average();
        var yMean = yArray.Average();
        
        var num = xArray.Zip(yArray, (x, y) => (x - xMean) * (y - yMean)).Sum();
        var den = Math.Sqrt(xArray.Sum(x => Math.Pow(x - xMean, 2)) * yArray.Sum(y => Math.Pow(y - yMean, 2)));
        
        return num / den;
    }
}