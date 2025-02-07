#!/usr/bin/env python3
import asyncio
import logging
import signal
import sys
import os
from concurrent import futures
from grpc import aio
import psutil

# Change to the script's directory and add parent paths
os.chdir(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.append("../Native")

import health_pb2
import health_pb2_grpc
import memory_scanner_pb2_grpc
from MemoryScanner.Server.memory_scanner_service import MemoryScannerService
from MemoryScanner.Server.health_service import HealthServicer
from MemoryScanner.Server.telemetry import setup_telemetry
from MemoryScanner.Server.config import SERVER_CONFIG, MEMORY_THRESHOLDS

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler(os.path.join("logs", "server.log")),
    ],
)
logger = logging.getLogger(__name__)

# Global references
server = None
memory_scanner_service = None
health_service = None
shutdown_event = asyncio.Event()


async def monitor_memory():
    """Monitor memory usage and log warnings."""
    while not shutdown_event.is_set():
        try:
            process = psutil.Process()
            memory_mb = process.memory_info().rss / (1024 * 1024)

            if memory_mb > MEMORY_THRESHOLDS["critical"]:
                logger.error(f"Memory usage critical: {memory_mb:.1f}MB")
            elif memory_mb > MEMORY_THRESHOLDS["warning"]:
                logger.warning(f"Memory usage high: {memory_mb:.1f}MB")

            await asyncio.sleep(60)  # Check every minute
        except Exception as e:
            logger.error("Error monitoring memory", exc_info=e)
            await asyncio.sleep(60)


async def cleanup():
    """Perform cleanup tasks during shutdown."""
    logger.info("Starting cleanup...")

    if health_service:
        health_service.set_status(
            "", health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING
        )
        health_service.set_status(
            "memory_scanner.MemoryScanner",
            health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING,
        )

    if memory_scanner_service:
        try:
            await memory_scanner_service.stop()
        except Exception as e:
            logger.error("Error stopping memory scanner service", exc_info=e)

    if server:
        try:
            # Wait for pending RPCs to complete
            logger.info("Waiting for pending RPCs to complete...")
            await asyncio.wait_for(
                server.stop(grace=SERVER_CONFIG["graceful_shutdown_timeout"]),
                timeout=SERVER_CONFIG["graceful_shutdown_timeout"] + 5,
            )
        except asyncio.TimeoutError:
            logger.warning("Graceful shutdown timed out, forcing stop")
        except Exception as e:
            logger.error("Error during server shutdown", exc_info=e)

    logger.info("Cleanup completed")


def signal_handler(signum, frame):
    """Handle shutdown signals."""
    logger.info(f"Received signal {signum}, initiating graceful shutdown...")
    shutdown_event.set()


async def serve():
    """Start and run the gRPC server."""
    global server, memory_scanner_service, health_service

    try:
        # Create server with configured settings
        server = aio.server(
            futures.ThreadPoolExecutor(max_workers=SERVER_CONFIG["max_workers"]),
            options=[
                (
                    "grpc.max_send_message_length",
                    SERVER_CONFIG["max_message_size_mb"] * 1024 * 1024,
                ),
                (
                    "grpc.max_receive_message_length",
                    SERVER_CONFIG["max_message_size_mb"] * 1024 * 1024,
                ),
            ],
        )

        # Create and initialize services
        memory_scanner_service = MemoryScannerService()
        health_service = HealthServicer()

        # Add services to server
        memory_scanner_pb2_grpc.add_MemoryScannerServicer_to_server(
            memory_scanner_service, server
        )
        health_pb2_grpc.add_HealthServicer_to_server(health_service, server)

        # Start listening
        listen_addr = f"[::]:{SERVER_CONFIG['port']}"
        server.add_insecure_port(listen_addr)

        logger.info(f"Starting server on {listen_addr}")
        await server.start()

        # Start the memory scanner service
        await memory_scanner_service.start()

        # Set initial health status
        health_service.set_status(
            "", health_pb2.HealthCheckResponse.ServingStatus.SERVING
        )
        health_service.set_status(
            "memory_scanner.MemoryScanner",
            health_pb2.HealthCheckResponse.ServingStatus.SERVING,
        )

        logger.info("Server is ready and serving")

        # Start memory monitoring
        monitor_task = asyncio.create_task(monitor_memory())

        # Wait for shutdown signal
        await shutdown_event.wait()

        # Cancel monitoring and cleanup
        monitor_task.cancel()
        await cleanup()

    except Exception as e:
        logger.error("Failed to start server", exc_info=e)
        if health_service:
            health_service.set_status(
                "", health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING
            )
            health_service.set_status(
                "memory_scanner.MemoryScanner",
                health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING,
            )
        await cleanup()
        raise
    finally:
        logger.info("Server shutdown complete")


if __name__ == "__main__":
    # Register signal handlers
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    # Ensure logs directory exists
    os.makedirs("logs", exist_ok=True)

    # Setup telemetry
    setup_telemetry()

    # Run the server
    asyncio.run(serve())
