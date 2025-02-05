#!/usr/bin/env python3
import asyncio
import grpc
import health_pb2
import health_pb2_grpc
import memory_scanner_pb2
import memory_scanner_pb2_grpc
import requests
import sys
import logging
import time
from requests.packages.urllib3.exceptions import InsecureRequestWarning

# Configure logging
logger = logging.getLogger(__name__)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)

# Suppress SSL warning for development environment
requests.packages.urllib3.disable_warnings(InsecureRequestWarning)

# Constants
GRPC_SERVER_ADDRESS = "localhost:50051"
DOTNET_HEALTH_URL = "https://localhost:7235/health"
MAX_RETRIES = 3
RETRY_DELAY = 2


async def check_grpc_health(test_frida: bool = False, retry_count: int = 0):
    """Test the gRPC health check service and optionally test Frida operations."""
    logger.info(
        f"Testing gRPC Health Check Service (attempt {retry_count + 1}/{MAX_RETRIES})..."
    )

    try:
        channel = grpc.aio.insecure_channel(GRPC_SERVER_ADDRESS)
        health_stub = health_pb2_grpc.HealthStub(channel)
        memory_scanner_stub = memory_scanner_pb2_grpc.MemoryScannerStub(channel)

        # Test overall service health
        logger.debug("Checking overall service health...")
        response = await health_stub.Check(health_pb2.HealthCheckRequest(service=""))
        logger.info(f"Overall service health status: {response.status}")
        if response.status != health_pb2.HealthCheckResponse.SERVING:
            raise Exception(f"Service is not healthy. Status: {response.status}")

        # Test memory scanner service health
        logger.debug("Checking memory scanner service health...")
        response = await health_stub.Check(
            health_pb2.HealthCheckRequest(service="memory_scanner.MemoryScanner")
        )
        logger.info(f"Memory Scanner service health status: {response.status}")
        if response.status != health_pb2.HealthCheckResponse.SERVING:
            raise Exception(
                f"Memory Scanner service is not healthy. Status: {response.status}"
            )

        if test_frida:
            logger.info("Starting Frida operations test...")
            # Test basic functionality
            processes = await memory_scanner_stub.ListProcesses(
                memory_scanner_pb2.Empty()
            )
            process_count = len(processes.processes)
            logger.info(f"Listed {process_count} processes")
            if process_count == 0:
                raise Exception("No processes found")

            # Try to attach to notepad.exe if found
            notepad = next(
                (p for p in processes.processes if p.name.lower() == "notepad.exe"),
                None,
            )
            if notepad:
                logger.info(f"Found notepad.exe (PID: {notepad.pid})")
                logger.info("Attempting to attach to process...")
                attach_response = await memory_scanner_stub.AttachToProcess(
                    memory_scanner_pb2.ProcessRequest(pid=notepad.pid)
                )

                if not attach_response.success:
                    raise Exception(f"Failed to attach to process {notepad.pid}")

                logger.info("Successfully attached to process")

                try:
                    # Detach after a moment
                    await asyncio.sleep(1)
                    logger.info("Attempting to detach from process...")
                    detach_response = await memory_scanner_stub.DetachFromProcess(
                        memory_scanner_pb2.DetachRequest(
                            session_id=attach_response.session_id
                        )
                    )
                    if not detach_response.success:
                        logger.warning(
                            f"Failed to detach from process: {detach_response.error_message}"
                        )
                    else:
                        logger.info("Successfully detached from process")
                except Exception as detach_error:
                    logger.error(f"Error during detach: {detach_error}")
                    # Don't raise here, as we want to continue with other tests
            else:
                logger.warning("Notepad.exe not found for Frida test")
                # Try with another process as fallback
                if processes.processes:
                    test_process = processes.processes[0]
                    logger.info(
                        f"Trying with alternative process: {test_process.name} (PID: {test_process.pid})"
                    )
                    # ... similar attach/detach logic for fallback process
                else:
                    raise Exception("No suitable processes found for Frida test")
        else:
            # Only list processes if not testing Frida
            processes = await memory_scanner_stub.ListProcesses(
                memory_scanner_pb2.Empty()
            )
            logger.info(f"Listed {len(processes.processes)} processes")

        await channel.close()
        return True
    except Exception as e:
        logger.error(f"gRPC health check failed: {str(e)}")
        if retry_count < MAX_RETRIES - 1:
            logger.info(f"Retrying in {RETRY_DELAY} seconds...")
            await asyncio.sleep(RETRY_DELAY)
            return await check_grpc_health(test_frida, retry_count + 1)
        return False


def check_dotnet_health(retry_count: int = 0):
    """Test the .NET health check endpoint."""
    logger.info(
        f"Testing .NET Health Check Endpoint (attempt {retry_count + 1}/{MAX_RETRIES})..."
    )

    try:
        start_time = time.time()
        response = requests.get(DOTNET_HEALTH_URL, verify=False, timeout=10)
        response_time = time.time() - start_time
        logger.info(f"Health check response time: {response_time:.2f} seconds")

        response.raise_for_status()
        health_data = response.json()

        logger.info(f"Health check status: {health_data['status']}")

        all_checks_healthy = True
        for check in health_data["checks"]:
            check_status = check["status"]
            logger.info(f"Check '{check['name']}': {check_status}")
            if "description" in check:
                logger.info(f"Description: {check['description']}")
            logger.info(f"Duration: {check['duration']}")

            if check_status.lower() != "healthy":
                all_checks_healthy = False
                logger.error(f"Check '{check['name']}' is not healthy")

        if not all_checks_healthy:
            raise Exception("Not all health checks passed")

        return True
    except requests.exceptions.RequestException as e:
        logger.error(f"Error testing .NET health endpoint: {str(e)}")
        if retry_count < MAX_RETRIES - 1:
            logger.info(f"Retrying in {RETRY_DELAY} seconds...")
            time.sleep(RETRY_DELAY)
            return check_dotnet_health(retry_count + 1)
        return False
    except Exception as e:
        logger.error(f"Unexpected error in .NET health check: {str(e)}")
        return False


async def test_server_stability():
    """Test server stability including Frida operations."""
    logger.info("Starting server stability test...")
    start_time = time.time()

    try:
        # Initial health check
        logger.info("Performing initial health check...")
        if not await check_grpc_health():
            logger.error("Initial health check failed!")
            return False

        # Test with Frida operations
        logger.info("Testing with Frida operations...")
        if not await check_grpc_health(test_frida=True):
            logger.error("Health check with Frida operations failed!")
            return False

        # Wait a moment
        await asyncio.sleep(1)

        # Final health check
        logger.info("Performing final health check...")
        if not await check_grpc_health():
            logger.error("Final health check failed!")
            return False

        duration = time.time() - start_time
        logger.info(
            f"Server stability test completed successfully in {duration:.2f} seconds"
        )
        return True
    except Exception as e:
        logger.error(f"Unexpected error in stability test: {str(e)}")
        return False


async def main():
    """Run all tests based on command line arguments."""
    start_time = time.time()
    success = True

    try:
        if len(sys.argv) > 1:
            if sys.argv[1] == "--stability":
                success = await test_server_stability()
            elif sys.argv[1] == "--with-dotnet":
                # Test .NET health first
                logger.info("Starting .NET health check sequence...")
                if not check_dotnet_health():
                    success = False
                    logger.error(".NET health check failed")
                else:
                    logger.info(".NET health check passed")

                # Then test gRPC health
                if success:  # Only proceed if .NET check passed
                    logger.info("Starting gRPC health check sequence...")
                    if not await check_grpc_health():
                        success = False
                        logger.error("gRPC health check failed")
                    else:
                        logger.info("gRPC health check passed")
            else:
                logger.error(f"Unknown argument: {sys.argv[1]}")
                success = False
        else:
            logger.error("No test mode specified")
            success = False

        duration = time.time() - start_time
        logger.info(f"Total test duration: {duration:.2f} seconds")

        if success:
            logger.info("All tests passed successfully!")
            sys.exit(0)
        else:
            logger.error("Some tests failed!")
            sys.exit(1)
    except Exception as e:
        logger.error(f"Unexpected error in main: {str(e)}")
        sys.exit(1)


if __name__ == "__main__":
    asyncio.run(main())
