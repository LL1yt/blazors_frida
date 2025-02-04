#!/usr/bin/env python3
import asyncio
import logging
import uuid
from concurrent import futures
from typing import Dict, Optional

import grpc
from grpc import aio
from opentelemetry import trace
from opentelemetry.instrumentation.grpc import GrpcInstrumentatorServer
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import ConsoleSpanExporter
from pythonjsonlogger import jsonlogger

import memory_scanner_pb2
import memory_scanner_pb2_grpc
from frida_module import FridaModule
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

# Configure OpenTelemetry
trace.set_tracer_provider(TracerProvider())
tracer = trace.get_tracer(__name__)
trace.get_tracer_provider().add_span_processor(
    trace.sdk.trace.export.SimpleSpanProcessor(ConsoleSpanExporter())
)

# Initialize gRPC instrumentation
grpc_instrumentor = GrpcInstrumentatorServer()
grpc_instrumentor.instrument()


class MemoryScannerService(memory_scanner_pb2_grpc.MemoryScannerServicer):
    def __init__(self):
        self.sessions: Dict[str, FridaModule] = {}
        self.scanners: Dict[str, MemoryScanner] = {}
        self.freezer_tasks: Dict[str, asyncio.Task] = {}
        self.state_versions: Dict[str, str] = {}

    async def ListProcesses(
        self, request: memory_scanner_pb2.Empty, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.ProcessList:
        with tracer.start_as_current_span("list_processes") as span:
            try:
                processes = get_process_list()
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
                frida_module = FridaModule()
                await frida_module.attach(request.pid)

                self.sessions[session_id] = frida_module
                self.scanners[session_id] = MemoryScanner(frida_module)
                self.state_versions[session_id] = str(uuid.uuid4())

                return memory_scanner_pb2.AttachResponse(
                    success=True, session_id=session_id
                )
            except Exception as e:
                logger.error(f"Failed to attach to process {request.pid}", exc_info=e)
                return memory_scanner_pb2.AttachResponse(
                    success=False, error_message=str(e)
                )

    async def DetachFromProcess(
        self,
        request: memory_scanner_pb2.ProcessRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.Empty:
        with tracer.start_as_current_span("detach_from_process") as span:
            try:
                if request.session_id in self.sessions:
                    await self.sessions[request.session_id].detach()
                    del self.sessions[request.session_id]
                    del self.scanners[request.session_id]
                    del self.state_versions[request.session_id]
                return memory_scanner_pb2.Empty()
            except Exception as e:
                logger.error(f"Failed to detach from process", exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.Empty()

    async def ScanMemory(
        self, request: memory_scanner_pb2.ScanRequest, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.ScanResponse:
        with tracer.start_as_current_span("scan_memory") as span:
            try:
                scanner = self.scanners.get(request.session_id)
                if not scanner:
                    raise ValueError("Invalid session ID")

                ranges = [(r.start, r.end) for r in request.ranges]
                results = await scanner.scan(
                    value_type=request.value_type,
                    value=request.value,
                    comparison_type=request.comparison_type,
                    ranges=ranges,
                )

                checkpoint_id = str(uuid.uuid4())
                return memory_scanner_pb2.ScanResponse(
                    results=[
                        memory_scanner_pb2.ScanResult(address=r.address, value=r.value)
                        for r in results
                    ],
                    checkpoint_id=checkpoint_id,
                )
            except Exception as e:
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

                reader = MemoryReader(session)
                value = await reader.read(
                    address=request.address,
                    size=request.size,
                    value_type=request.value_type,
                )

                return memory_scanner_pb2.ReadResponse(value=value, success=True)
            except Exception as e:
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

                writer = MemoryWriter(session)
                await writer.write(
                    address=request.address,
                    value=request.value,
                    value_type=request.value_type,
                )

                return memory_scanner_pb2.WriteResponse(success=True)
            except Exception as e:
                logger.error("Failed to write memory", exc_info=e)
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
        writer = MemoryWriter(self.sessions[session_id])
        reader = MemoryReader(self.sessions[session_id])

        while not context.done():
            try:
                current_value = await reader.read(address, len(value), value_type)
                if current_value != value:
                    await writer.write(address, value, value_type)

                yield memory_scanner_pb2.FreezeStatus(
                    active=True,
                    current_value=current_value,
                    correlation_id=context.get_active_span()
                    .get_span_context()
                    .trace_id,
                )
                await asyncio.sleep(0.1)
            except Exception as e:
                logger.error("Error in freeze task", exc_info=e)
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
            freeze_key = f"{request.session_id}_{request.address}"

            if freeze_key in self.freezer_tasks:
                # Cancel existing freeze task
                self.freezer_tasks[freeze_key].cancel()

            async for status in self._freeze_value_task(
                request.session_id,
                request.address,
                request.value,
                request.value_type,
                context,
            ):
                yield status

    async def UnfreezeValue(
        self,
        request: memory_scanner_pb2.UnfreezeRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.Empty:
        with tracer.start_as_current_span("unfreeze_value") as span:
            freeze_key = f"{request.session_id}_{request.address}"
            if freeze_key in self.freezer_tasks:
                self.freezer_tasks[freeze_key].cancel()
                del self.freezer_tasks[freeze_key]
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
    server = aio.server(
        futures.ThreadPoolExecutor(max_workers=10),
        options=[
            ("grpc.max_send_message_length", 512 * 1024 * 1024),
            ("grpc.max_receive_message_length", 512 * 1024 * 1024),
        ],
    )

    memory_scanner_pb2_grpc.add_MemoryScannerServicer_to_server(
        MemoryScannerService(), server
    )

    listen_addr = f"[::]:{port}"
    server.add_insecure_port(listen_addr)

    logger.info(f"Starting server on {listen_addr}")
    await server.start()
    await server.wait_for_termination()


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    asyncio.run(serve())
