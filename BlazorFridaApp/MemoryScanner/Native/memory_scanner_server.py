#!/usr/bin/env python3
import asyncio
import logging
import uuid
from concurrent import futures
from typing import Dict, Optional
import signal
import sys

import grpc
from grpc import aio
from opentelemetry import trace, metrics
from opentelemetry.trace.status import Status, StatusCode
from opentelemetry.instrumentation.grpc import (
    GrpcInstrumentorClient,
    GrpcInstrumentorServer,
)
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import ConsoleSpanExporter, SimpleSpanProcessor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import (
    ConsoleMetricExporter,
    PeriodicExportingMetricReader,
)
from pythonjsonlogger import jsonlogger

import memory_scanner_pb2
import memory_scanner_pb2_grpc
import health_pb2
import health_pb2_grpc
from health_service import HealthServicer
from frida_module import FridaMemoryScanner
from process_list import get_process_list
from scanner import MemoryScanner
from reader import MemoryReader
from writer import MemoryWriter

# Configure logging
logger = logging.getLogger(__name__)
logHandler = logging.StreamHandler()
formatter = jsonlogger.JsonFormatter(
    "%(timestamp)s %(level)s %(name)s %(message)s %(trace_id)s %(span_id)s"
)
logHandler.setFormatter(formatter)
logger.addHandler(logHandler)
logger.setLevel(logging.INFO)

# Configure OpenTelemetry tracing
trace.set_tracer_provider(TracerProvider())
tracer = trace.get_tracer(__name__)
trace.get_tracer_provider().add_span_processor(
    SimpleSpanProcessor(ConsoleSpanExporter())
)

# Configure OpenTelemetry metrics
metrics.set_meter_provider(
    MeterProvider(
        metric_readers=[
            PeriodicExportingMetricReader(
                ConsoleMetricExporter(), export_interval_millis=5000
            )
        ]
    )
)
meter = metrics.get_meter(__name__)

# Create metrics
active_sessions_counter = meter.create_up_down_counter(
    "memory_scanner_active_sessions", description="Number of active scanning sessions"
)

operation_counter = meter.create_counter(
    "memory_scanner_operations", description="Number of memory operations performed"
)

operation_duration = meter.create_histogram(
    "memory_scanner_operation_duration",
    description="Duration of memory operations",
    unit="ms",
)

error_counter = meter.create_counter(
    "memory_scanner_errors", description="Number of errors encountered"
)

# Initialize gRPC instrumentation
grpc_server_instrumentor = GrpcInstrumentorServer()
grpc_server_instrumentor.instrument()

grpc_client_instrumentor = GrpcInstrumentorClient()
grpc_client_instrumentor.instrument()

# Global server reference
server = None
health_service = None


def signal_handler(signum, frame):
    logger.info(f"Received signal {signum}, initiating graceful shutdown...")
    if server:
        logger.info("Stopping gRPC server...")
        if health_service:
            health_service.set_status(
                "", health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING
            )
            health_service.set_status(
                "memory_scanner.MemoryScanner",
                health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING,
            )
        asyncio.create_task(server.stop(5))
    sys.exit(0)


signal.signal(signal.SIGINT, signal_handler)
signal.signal(signal.SIGTERM, signal_handler)


class MemoryScannerService(memory_scanner_pb2_grpc.MemoryScannerServicer):
    def __init__(self):
        self.sessions: Dict[str, FridaMemoryScanner] = {}
        self.scanners: Dict[str, MemoryScanner] = {}
        self.readers: Dict[str, MemoryReader] = {}
        self.writers: Dict[str, MemoryWriter] = {}
        self.freezer_tasks: Dict[str, asyncio.Task] = {}
        self.state_versions: Dict[str, str] = {}
        logger.info("MemoryScannerService initialized")

    async def ListProcesses(
        self, request: memory_scanner_pb2.Empty, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.ProcessList:
        with tracer.start_as_current_span("list_processes") as span:
            try:
                logger.info("Starting process list enumeration...")
                processes = get_process_list()
                logger.info(f"Found {len(processes)} processes")
                return memory_scanner_pb2.ProcessList(
                    processes=[
                        memory_scanner_pb2.ProcessInfo(
                            pid=p.pid, name=p.name, path=p.path
                        )
                        for p in processes
                    ]
                )
            except Exception as e:
                logger.error("Failed to list processes", exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.ProcessList()

    async def AttachToProcess(
        self,
        request: memory_scanner_pb2.ProcessRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.AttachResponse:
        with tracer.start_as_current_span("attach_to_process") as span:
            try:
                session_id = str(uuid.uuid4())
                frida_scanner = FridaMemoryScanner()
                span.set_attribute("process.id", request.pid)
                await frida_scanner.attach_to_process(request.pid)

                self.sessions[session_id] = frida_scanner
                self.scanners[session_id] = MemoryScanner(frida_scanner, session_id)
                self.readers[session_id] = MemoryReader(frida_scanner)
                self.writers[session_id] = MemoryWriter(frida_scanner)
                self.state_versions[session_id] = str(uuid.uuid4())

                # Update metrics
                active_sessions_counter.add(1)
                operation_counter.add(1, {"operation": "attach"})

                logger.info(
                    f"Successfully attached to process {request.pid} with session {session_id}"
                )
                return memory_scanner_pb2.AttachResponse(
                    success=True, session_id=session_id
                )
            except Exception as e:
                error_counter.add(1, {"operation": "attach", "error": str(e)})
                logger.error(f"Failed to attach to process {request.pid}", exc_info=e)
                return memory_scanner_pb2.AttachResponse(
                    success=False, error_message=str(e)
                )

    async def DetachFromProcess(
        self,
        request: memory_scanner_pb2.DetachRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.DetachResponse:
        with tracer.start_as_current_span("detach_from_process") as span:
            try:
                session_id = request.session_id
                if session_id in self.sessions:
                    # Cleanup state before detaching
                    scanner = self.scanners[session_id]
                    scanner.cleanup()  # Clean up state files

                    # Cancel any active freeze tasks
                    for freeze_key in list(self.freezer_tasks.keys()):
                        if freeze_key.startswith(f"{session_id}_"):
                            self.freezer_tasks[freeze_key].cancel()
                            del self.freezer_tasks[freeze_key]

                    await self.sessions[session_id].detach_from_process()
                    del self.sessions[session_id]
                    del self.scanners[session_id]
                    del self.readers[session_id]
                    del self.writers[session_id]
                    del self.state_versions[session_id]

                    # Update metrics
                    active_sessions_counter.add(-1)
                    operation_counter.add(1, {"operation": "detach"})

                    logger.info(
                        f"Successfully detached and cleaned up session {session_id}"
                    )
                    return memory_scanner_pb2.DetachResponse(success=True)
                else:
                    error_counter.add(
                        1, {"operation": "detach", "error": "session_not_found"}
                    )
                    logger.warning(f"Session {session_id} not found")
                    return memory_scanner_pb2.DetachResponse(
                        success=False, error_message=f"Session {session_id} not found"
                    )
            except Exception as e:
                error_counter.add(1, {"operation": "detach", "error": str(e)})
                logger.error(f"Failed to detach from process", exc_info=e)
                return memory_scanner_pb2.DetachResponse(
                    success=False, error_message=str(e)
                )

    async def ScanMemory(
        self, request: memory_scanner_pb2.ScanRequest, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.ScanResponse:
        with tracer.start_as_current_span("scan_memory") as span:
            try:
                scanner = self.scanners.get(request.session_id)
                if not scanner:
                    raise ValueError("Invalid session ID")

                start_time = asyncio.get_event_loop().time()
                ranges = [(r.start, r.end) for r in request.ranges]
                results = await scanner.scan(
                    value_type=request.value_type,
                    value=request.value,
                    comparison_type=request.comparison_type,
                    ranges=ranges,
                )
                duration_ms = (asyncio.get_event_loop().time() - start_time) * 1000

                # Update metrics
                operation_type = (
                    "pattern_scan" if request.value_type == "pattern" else "scan"
                )
                operation_counter.add(1, {"operation": operation_type})
                operation_duration.record(duration_ms, {"operation": operation_type})

                checkpoint_id = str(uuid.uuid4())

                # Ensure each result value is in bytes format
                scan_results = []
                for r in results:
                    value = r["value"]
                    if not isinstance(value, bytes):
                        if isinstance(value, str):
                            value = value.encode("utf-8")
                        else:
                            value = str(value).encode("utf-8")
                    scan_results.append(
                        memory_scanner_pb2.ScanResult(address=r["address"], value=value)
                    )

                return memory_scanner_pb2.ScanResponse(
                    results=scan_results,
                    checkpoint_id=checkpoint_id,
                )
            except Exception as e:
                error_counter.add(1, {"operation": "scan", "error": str(e)})
                logger.error("Failed to scan memory", exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.ScanResponse()

    async def ReadMemory(
        self, request: memory_scanner_pb2.ReadRequest, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.ReadResponse:
        with tracer.start_as_current_span("read_memory") as span:
            try:
                session = self.sessions.get(request.session_id)
                if not session:
                    raise ValueError("Invalid session ID")

                start_time = asyncio.get_event_loop().time()
                reader = MemoryReader(session)
                value = await reader.read(
                    address=request.address,
                    size=request.size,
                    value_type=request.value_type,
                )
                duration_ms = (asyncio.get_event_loop().time() - start_time) * 1000

                # Update metrics
                operation_counter.add(1, {"operation": "read"})
                operation_duration.record(duration_ms, {"operation": "read"})

                return memory_scanner_pb2.ReadResponse(value=value, success=True)
            except Exception as e:
                error_counter.add(1, {"operation": "read", "error": str(e)})
                logger.error("Failed to read memory", exc_info=e)
                return memory_scanner_pb2.ReadResponse(
                    success=False, error_message=str(e)
                )

    async def WriteMemory(
        self,
        request: memory_scanner_pb2.WriteRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.WriteResponse:
        with tracer.start_as_current_span("write_memory") as span:
            try:
                session = self.sessions.get(request.session_id)
                if not session:
                    raise ValueError("Invalid session ID")

                # Set span attributes
                span.set_attribute("memory.address", request.address)
                span.set_attribute("memory.value_type", request.value_type)
                span.set_attribute("memory.value_size", len(request.value))

                start_time = asyncio.get_event_loop().time()
                writer = MemoryWriter(session)
                await writer.write(
                    address=request.address,
                    value=request.value,
                    value_type=request.value_type,
                )
                duration_ms = (asyncio.get_event_loop().time() - start_time) * 1000

                # Update metrics
                operation_counter.add(1, {"operation": "write"})
                operation_duration.record(duration_ms, {"operation": "write"})

                return memory_scanner_pb2.WriteResponse(success=True)
            except Exception as e:
                error_counter.add(1, {"operation": "write", "error": str(e)})
                logger.error("Failed to write memory", exc_info=e)
                # Set error status on span
                span.set_status(Status(StatusCode.ERROR, str(e)))
                return memory_scanner_pb2.WriteResponse(
                    success=False, error_message=str(e)
                )

    async def _freeze_value_task(
        self,
        session_id: str,
        address: int,
        value: bytes,
        value_type: str,
        context: grpc.aio.ServicerContext,
    ):
        if session_id not in self.writers or session_id not in self.readers:
            logger.error(f"No reader/writer found for session {session_id}")
            yield memory_scanner_pb2.FreezeStatus(
                active=False, error_message="Session not found"
            )
            return

        writer = self.writers[session_id]
        reader = self.readers[session_id]

        while not context.done():
            try:
                current_value = await reader.read(address, len(value), value_type)
                if current_value != value:
                    await writer.write(address, value, value_type)
                    logger.debug(f"Updated frozen value at address {address}")

                yield memory_scanner_pb2.FreezeStatus(
                    active=True,
                    current_value=current_value,
                    correlation_id=context.get_active_span()
                    .get_span_context()
                    .trace_id,
                )
                await asyncio.sleep(0.1)
            except Exception as e:
                logger.error(f"Error in freeze task: {e}", exc_info=e)
                yield memory_scanner_pb2.FreezeStatus(
                    active=False, error_message=str(e)
                )
                break

    async def FreezeValue(
        self,
        request: memory_scanner_pb2.FreezeRequest,
        context: grpc.aio.ServicerContext,
    ):
        with tracer.start_as_current_span("freeze_value") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                freeze_key = f"{session_id}_{request.address}"
                logger.info(f"Starting value freeze for address {request.address}")

                if freeze_key in self.freezer_tasks:
                    logger.info(f"Cancelling existing freeze task for {freeze_key}")
                    self.freezer_tasks[freeze_key].cancel()

                # Create and store the freeze task
                freeze_task = asyncio.create_task(
                    self._freeze_value_task(
                        session_id,
                        request.address,
                        request.value,
                        request.value_type,
                        context,
                    )
                )
                self.freezer_tasks[freeze_key] = freeze_task

                try:
                    async for status in freeze_task:
                        yield status
                except asyncio.CancelledError:
                    logger.info(f"Freeze task cancelled for {freeze_key}")
                    yield memory_scanner_pb2.FreezeStatus(
                        active=False, error_message="Task cancelled"
                    )
                except Exception as e:
                    logger.error(f"Error in freeze task: {e}", exc_info=e)
                    yield memory_scanner_pb2.FreezeStatus(
                        active=False, error_message=str(e)
                    )
                finally:
                    if freeze_key in self.freezer_tasks:
                        del self.freezer_tasks[freeze_key]
            except Exception as e:
                logger.error(f"Error setting up freeze task: {e}", exc_info=e)
                yield memory_scanner_pb2.FreezeStatus(
                    active=False, error_message=str(e)
                )

    async def UnfreezeValue(
        self,
        request: memory_scanner_pb2.UnfreezeRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.Empty:
        with tracer.start_as_current_span("unfreeze_value") as span:
            try:
                session_id = request.session_id
                freeze_key = f"{session_id}_{request.address}"

                if freeze_key in self.freezer_tasks:
                    logger.info(f"Cancelling freeze task for {freeze_key}")
                    self.freezer_tasks[freeze_key].cancel()
                    del self.freezer_tasks[freeze_key]
                else:
                    logger.warning(f"No active freeze task found for {freeze_key}")

                return memory_scanner_pb2.Empty()
            except Exception as e:
                logger.error(f"Error unfreezing value: {e}", exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.Empty()

    async def GetState(
        self,
        request: memory_scanner_pb2.StateRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.StateResponse:
        with tracer.start_as_current_span("get_state") as span:
            try:
                if request.session_id not in self.sessions:
                    raise ValueError("Invalid session ID")

                scanner = self.scanners[request.session_id]
                state = await scanner.get_state(request.checkpoint_id)

                return memory_scanner_pb2.StateResponse(
                    state=state, version=self.state_versions[request.session_id]
                )
            except Exception as e:
                logger.error("Failed to get state", exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.StateResponse()

    async def SyncState(
        self, request: memory_scanner_pb2.SyncRequest, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.SyncResponse:
        with tracer.start_as_current_span("sync_state") as span:
            try:
                if request.session_id not in self.sessions:
                    raise ValueError("Invalid session ID")

                if request.version != self.state_versions[request.session_id]:
                    return memory_scanner_pb2.SyncResponse(
                        success=False, error_message="Version mismatch"
                    )

                scanner = self.scanners[request.session_id]
                await scanner.update_state(request.state_updates)

                new_version = str(uuid.uuid4())
                self.state_versions[request.session_id] = new_version

                return memory_scanner_pb2.SyncResponse(
                    success=True, new_version=new_version
                )
            except Exception as e:
                logger.error("Failed to sync state", exc_info=e)
                return memory_scanner_pb2.SyncResponse(
                    success=False, error_message=str(e)
                )


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
        memory_scanner_pb2_grpc.add_MemoryScannerServicer_to_server(
            memory_scanner_service, server
        )
        health_pb2_grpc.add_HealthServicer_to_server(health_service, server)

        listen_addr = f"[::]:{port}"
        server.add_insecure_port(listen_addr)

        logger.info(f"Starting server on {listen_addr}")
        await server.start()

        # Set initial health status
        health_service.set_status(
            "", health_pb2.HealthCheckResponse.ServingStatus.SERVING
        )
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
            health_service.set_status(
                "", health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING
            )
            health_service.set_status(
                "memory_scanner.MemoryScanner",
                health_pb2.HealthCheckResponse.ServingStatus.NOT_SERVING,
            )
        raise
    finally:
        logger.info("Server shutdown complete")


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    asyncio.run(serve())
