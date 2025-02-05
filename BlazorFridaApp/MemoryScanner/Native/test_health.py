#!/usr/bin/env python3
import asyncio
import grpc
import health_pb2
import health_pb2_grpc
import memory_scanner_pb2
import memory_scanner_pb2_grpc
import requests
import sys
from requests.packages.urllib3.exceptions import InsecureRequestWarning

# Suppress only the single warning from urllib3 needed.
requests.packages.urllib3.disable_warnings(InsecureRequestWarning)


async def test_health_check():
    """Test the gRPC health check service."""
    print("\nTesting gRPC Health Check Service...")

    try:
        # Create channel
        channel = grpc.aio.insecure_channel("localhost:50051")

        # Test health service
        health_stub = health_pb2_grpc.HealthStub(channel)
        memory_scanner_stub = memory_scanner_pb2_grpc.MemoryScannerStub(channel)

        # Test overall service health
        response = await health_stub.Check(health_pb2.HealthCheckRequest(service=""))
        print(f"Overall service health status: {response.status}")

        # Test memory scanner service health
        response = await health_stub.Check(
            health_pb2.HealthCheckRequest(service="memory_scanner.MemoryScanner")
        )
        print(f"Memory Scanner service health status: {response.status}")

        # Test basic memory scanner functionality
        try:
            response = await memory_scanner_stub.ListProcesses(
                memory_scanner_pb2.Empty()
            )
            print("Memory Scanner ListProcesses test: Success")
        except grpc.RpcError as e:
            print(f"Memory Scanner ListProcesses test failed: {e.details()}")

        await channel.close()

    except Exception as e:
        print(f"Error testing gRPC health check: {str(e)}")
        return False

    return True


def test_dotnet_health_endpoint():
    """Test the .NET health check endpoint."""
    print("\nTesting .NET Health Check Endpoint...")

    try:
        # Disable SSL verification for development environment
        response = requests.get("https://localhost:7235/health", verify=False)
        response.raise_for_status()
        health_data = response.json()

        print(f"Health check status: {health_data['status']}")
        for check in health_data["checks"]:
            print(f"Check '{check['name']}': {check['status']}")
            if "description" in check:
                print(f"Description: {check['description']}")
            print(f"Duration: {check['duration']}")

        return True
    except Exception as e:
        print(f"Error testing .NET health endpoint: {str(e)}")
        return False


async def main():
    """Run all tests."""
    success = True

    # Test gRPC health check
    if not await test_health_check():
        success = False

    # Test .NET health endpoint
    if not test_dotnet_health_endpoint():
        success = False

    if success:
        print("\nAll tests passed successfully!")
        sys.exit(0)
    else:
        print("\nSome tests failed!")
        sys.exit(1)


if __name__ == "__main__":
    asyncio.run(main())
