#!/usr/bin/env python3
import asyncio
import logging
import signal
import sys
from concurrent import futures
from grpc import aio

sys.path.append('../Proto')
sys.path.append('../Native')

import health_pb2
import health_pb2_grpc
import memory_scanner_pb2_grpc
from MemoryScanner.Server.memory_scanner_service import MemoryScannerService
from health_service import HealthServicer
from telemetry import setup_telemetry

# Global server reference
server = None
health_service = None

def signal_handler(signum, frame):
    logger.info(f"Received signal {signum}, initiating graceful shutdown...")
    if server:
        logger.info("Stopping gRPC server...")
        if health_service:
            health_service.set_status("", health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING)
            health_service.set_status(
                "memory_scanner.MemoryScanner",
                health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING,
            )
        asyncio.create_task(server.stop(5))
    sys.exit(0)

signal.signal(signal.SIGINT, signal_handler)
signal.signal(signal.SIGTERM, signal_handler)

async def serve(port: int = 50051):
    global server, health_service

    try:
        server = aio.server(
            futures.ThreadPoolExecutor(max_workers=10),
            options=[
                ("grpc.max_send_message_length", 512 * 1024 * 1024),
                ("grpc.max_receive_message_length", 512 * 1024 * 1024),
            ],
        )

        # Create services
        memory_scanner_service = MemoryScannerService()
        health_service = HealthServicer()

        # Add services to server
        memory_scanner_pb2_grpc.add_MemoryScannerServicer_to_server(memory_scanner_service, server)
        health_pb2_grpc.add_HealthServicer_to_server(health_service, server)

        listen_addr = f"[::]:{port}"
        server.add_insecure_port(listen_addr)

        logger.info(f"Starting server on {listen_addr}")
        await server.start()

        # Set initial health status
        health_service.set_status("", health_pb2.HealthCheckResponse.ServingStatus.SERVING)
        health_service.set_status(
            "memory_scanner.MemoryScanner",
            health_pb2.HealthCheckResponse.ServingStatus.SERVING,
        )
        logger.info("Server is ready and serving")

        try:
            await server.wait_for_termination()
        except Exception as e:
            logger.error("Server error during operation", exc_info=e)
            raise
    except Exception as e:
        logger.error("Failed to start server", exc_info=e)
        if health_service:
            health_service.set_status("", health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING)
            health_service.set_status(
                "memory_scanner.MemoryScanner",
                health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING,
            )
        raise
    finally:
        logger.info("Server shutdown complete")

if __name__ == "__main__":
    logger = setup_telemetry()
    asyncio.run(serve())