using Grpc.Net.Client;
using System;

namespace BlazorFridaApp.Tests.Infrastructure;

public class TestGrpcConfiguration
{
    public ServerSettings Server { get; set; } = new();
    public ClientSettings Client { get; set; } = new();
    public RetrySettings Retry { get; set; } = new();

    public class ServerSettings
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 50051;
        public string Endpoint => $"http://{Host}:{Port}";
    }

    public class ClientSettings
    {
        public int MaxReceiveMessageSize { get; set; } = int.MaxValue;
        public int MaxSendMessageSize { get; set; } = int.MaxValue;
        public bool EnableMultipleHttp2Connections { get; set; } = true;
        public TimeSpan KeepAlivePingDelay { get; set; } = TimeSpan.FromSeconds(60);
        public TimeSpan KeepAlivePingTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public int ConnectionTimeoutMs { get; set; } = 5000;
    }

    public class RetrySettings
    {
        public int MaxAttempts { get; set; } = 3;
        public int DelayMs { get; set; } = 200;
    }

    public GrpcChannelOptions CreateChannelOptions()
    {
        return new GrpcChannelOptions
        {
            MaxReceiveMessageSize = Client.MaxReceiveMessageSize,
            MaxSendMessageSize = Client.MaxSendMessageSize,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = Client.EnableMultipleHttp2Connections,
                KeepAlivePingDelay = Client.KeepAlivePingDelay,
                KeepAlivePingTimeout = Client.KeepAlivePingTimeout,
                PooledConnectionIdleTimeout = Client.PooledConnectionIdleTimeout
            }
        };
    }
}