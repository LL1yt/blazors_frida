import uuid
import logging
from typing import Dict
import asyncio
import time
import grpc
from grpc import aio
from opentelemetry import trace
from opentelemetry.trace.status import Status, StatusCode

import sys
sys.path.append('../Proto')
sys.path.append('../Native')
sys.path.append('../Operations')

from MemoryScanner.Native import memory_scanner_pb2, memory_scanner_pb2_grpc, health_pb2
from MemoryScanner.Native.frida_module import FridaMemoryScanner
from MemoryScanner.Native.process_list import get_process_list
from MemoryScanner.Native.scanner import MemoryScanner
from MemoryScanner.Native.reader import MemoryReader
from MemoryScanner.Native.writer import MemoryWriter
from MemoryScanner.Server.metrics import active_sessions_counter, operation_counter, operation_duration, error_counter

logger = logging.getLogger(__name__)
tracer = trace.get_tracer(__name__)

class MemoryScannerService(memory_scanner_pb2_grpc.MemoryScannerServicer):
    def __init__(self):
        from MemoryScanner.Server import metrics
        metrics.init_metrics()  # Initialize metrics

        self.sessions: Dict[str, FridaMemoryScanner] = {}
        self.scanners: Dict[str, MemoryScanner] = {}
        self.readers: Dict[str, MemoryReader] = {}
        self.writers: Dict[str, MemoryWriter] = {}
        self.freezer_tasks: Dict[str, tuple] = {}
        self.state_versions: Dict[str, str] = {}
        self.operation_counter = metrics.operation_counter
        self.operation_duration = metrics.operation_duration
        self.error_counter = metrics.error_counter
        self.active_sessions_counter = metrics.active_sessions_counter
        logger.info("MemoryScannerService initialized")

    async def ListProcesses(self, request: memory_scanner_pb2.Empty, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.ProcessList:
        with tracer.start_as_current_span("list_processes") as span:
            try:
                logger.info("Starting process list enumeration...")
                processes = get_process_list()
                logger.info(f"Found {len(processes)} processes")
                return memory_scanner_pb2.ProcessList(
                    processes=[
                        memory_scanner_pb2.ProcessInfo(pid=p.pid, name=p.name, path=p.path)
                        for p in processes
                    ]
                )
            except Exception as e:
                logger.error("Failed to list processes", exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.ProcessList()

    async def AttachToProcess(self, request: memory_scanner_pb2.ProcessRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.AttachResponse:
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

                self.active_sessions_counter.add(1)
                self.operation_counter.add(1, {"operation": "attach"})

                logger.info(f"Successfully attached to process {request.pid} with session {session_id}")
                return memory_scanner_pb2.AttachResponse(success=True, session_id=session_id)
            except Exception as e:
                self.error_counter.add(1, {"operation": "attach", "error": str(e)})
                logger.error(f"Failed to attach to process {request.pid}", exc_info=e)
                return memory_scanner_pb2.AttachResponse(success=False, error_message=str(e))

    async def DetachFromProcess(self, request: memory_scanner_pb2.DetachRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.DetachResponse:
        with tracer.start_as_current_span("detach_from_process") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                # Cancel any existing freeze tasks
                if session_id in self.freezer_tasks:
                    task, _ = self.freezer_tasks[session_id]
                    task.cancel()
                    del self.freezer_tasks[session_id]

                # Clean up session resources
                await self.sessions[session_id].detach()
                del self.sessions[session_id]
                del self.scanners[session_id]
                del self.readers[session_id]
                del self.writers[session_id]
                del self.state_versions[session_id]

                self.active_sessions_counter.add(-1)
                self.operation_counter.add(1, {"operation": "detach"})

                logger.info(f"Successfully detached from session {session_id}")
                return memory_scanner_pb2.DetachResponse(success=True)
            except Exception as e:
                self.error_counter.add(1, {"operation": "detach", "error": str(e)})
                logger.error(f"Failed to detach from session {session_id}", exc_info=e)
                return memory_scanner_pb2.DetachResponse(success=False, error_message=str(e))

    async def ScanMemory(self, request: memory_scanner_pb2.ScanRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.ScanResponse:
        with tracer.start_as_current_span("scan_memory") as span:
            try:
                start_time = time.time()
                
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")
                scanner = self.scanners[session_id]
                span.set_attribute("scan.type", request.scan_type)
                span.set_attribute("value.type", request.value_type)
                
                # Record pattern scan metric before the scan
                if request.value_type == "pattern":
                    logger.debug("Recording pattern scan metric")
                    await self.operation_counter.add(1, {"operation": "pattern_scan"})
                
                results = await scanner.scan(
                    request.value,
                    request.value_type,
                    request.comparison_type
                )
                
                duration = time.time() - start_time
                operation_type = "pattern_scan" if request.value_type == "pattern" else "scan"
                
                if operation_type == "scan":  # Only record normal scan metric if not pattern scan
                    await self.operation_counter.add(1, {"operation": operation_type})
                await self.operation_duration.record(duration, {"operation": operation_type})
                
                self.state_versions[session_id] = str(uuid.uuid4())
                logger.info(f"{operation_type.title()} completed for session {session_id}, found {len(results)} results")
                
                return memory_scanner_pb2.ScanResponse(
                    results=[
                        memory_scanner_pb2.ScanResult(
                            address=addr,
                            value=value if isinstance(value, bytes) else str(value).encode('utf-8')
                        )
                        for addr, value in results
                    ],
                    checkpoint_id=str(uuid.uuid4())
                )
            except Exception as e:
                operation_type = "pattern_scan" if request.value_type == "pattern" else "scan"
                await self.error_counter.add(1, {"operation": operation_type, "error": str(e)})
                logger.error(f"Failed to perform {operation_type} for session {session_id}", exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.ScanResponse()

    async def ReadMemory(self, request: memory_scanner_pb2.ReadRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.ReadResponse:
        with tracer.start_as_current_span("read_memory") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                reader = self.readers[session_id]
                span.set_attribute("address", request.address)
                span.set_attribute("value.type", request.value_type)

                with self.operation_duration.time({"operation": "read"}):
                    value = await reader.read_memory(
                        request.address,
                        request.value_type
                    )

                self.operation_counter.add(1, {"operation": "read"})
                logger.info(f"Memory read completed for session {session_id} at address {request.address}")
                return memory_scanner_pb2.ReadResponse(value=value, success=True)
            except Exception as e:
                self.error_counter.add(1, {"operation": "read", "error": str(e)})
                logger.error(f"Failed to read memory for session {session_id}", exc_info=e)
                return memory_scanner_pb2.ReadResponse(success=False, error_message=str(e))

    async def WriteMemory(self, request: memory_scanner_pb2.WriteRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.WriteResponse:
        with tracer.start_as_current_span("write_memory") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                writer = self.writers[session_id]
                span.set_attribute("address", request.address)
                span.set_attribute("value.type", request.value_type)

                with self.operation_duration.time({"operation": "write"}):
                    success = await writer.write_memory(
                        request.address,
                        request.value,
                        request.value_type
                    )

                self.operation_counter.add(1, {"operation": "write"})
                self.state_versions[session_id] = str(uuid.uuid4())

                logger.info(f"Memory write completed for session {session_id} at address {request.address}")
                return memory_scanner_pb2.WriteResponse(success=success)
            except Exception as e:
                self.error_counter.add(1, {"operation": "write", "error": str(e)})
                logger.error(f"Failed to write memory for session {session_id}", exc_info=e)
                return memory_scanner_pb2.WriteResponse(success=False, error_message=str(e))

    async def _freeze_value_task(self, session_id: str, address: int, value: bytes, value_type: str, context: grpc.aio.ServicerContext):
        writer = self.writers[session_id]
        try:
            while True:
                await writer.write_memory(address, value, value_type)
                await asyncio.sleep(0.1)  # Small delay to prevent excessive CPU usage
        except asyncio.CancelledError:
            logger.info(f"Freeze task cancelled for session {session_id} at address {address}")
        except Exception as e:
            logger.error(f"Error in freeze task for session {session_id}", exc_info=e)
            raise

    async def FreezeValue(self, request: memory_scanner_pb2.FreezeRequest, context: grpc.aio.ServicerContext):
        """Stream status updates while freezing a value at the specified address."""
        with tracer.start_as_current_span("freeze_value") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                span.set_attribute("address", request.address)
                span.set_attribute("value.type", request.value_type)

                writer = self.writers[session_id]
                reader = self.readers[session_id]

                await self.operation_counter.add(1, {"operation": "freeze"})

                while True:
                    try:
                        # Write the value
                        success = await writer.write_memory(
                            request.address,
                            request.value,
                            request.value_type
                        )

                        if not success:
                            logger.error(f"Failed to write memory in freeze task for session {session_id}")
                            yield memory_scanner_pb2.FreezeStatus(
                                active=False,
                                error_message="Failed to write memory"
                            )
                            break

                        # Read back the current value to verify
                        current_value = await reader.read_memory(
                            request.address,
                            request.value_type
                        )

                        # Ensure current_value is bytes
                        if not isinstance(current_value, bytes):
                            if isinstance(current_value, str):
                                current_value = current_value.encode('utf-8')
                            else:
                                current_value = str(current_value).encode('utf-8')

                        # Yield status update
                        yield memory_scanner_pb2.FreezeStatus(
                            active=True,
                            current_value=current_value
                        )

                        await asyncio.sleep(0.1)  # Small delay to prevent excessive CPU usage
                    except asyncio.CancelledError:
                        logger.info(f"Freeze task cancelled for session {session_id} at address {request.address}")
                        break
                    except Exception as e:
                        logger.error(f"Error in freeze task for session {session_id}", exc_info=e)
                        yield memory_scanner_pb2.FreezeStatus(
                            active=False,
                            error_message=str(e)
                        )
                        break

            except Exception as e:
                await self.error_counter.add(1, {"operation": "freeze", "error": str(e)})
                logger.error(f"Failed to start freeze task for session {session_id}", exc_info=e)
                yield memory_scanner_pb2.FreezeStatus(
                    active=False,
                    error_message=str(e)
                )

    async def UnfreezeValue(self, request: memory_scanner_pb2.UnfreezeRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.Empty:
        with tracer.start_as_current_span("unfreeze_value") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                if session_id in self.freezer_tasks:
                    task, address = self.freezer_tasks[session_id]
                    task.cancel()
                    del self.freezer_tasks[session_id]
                    
                self.operation_counter.add(1, {"operation": "unfreeze"})
                logger.info(f"Cancelled freeze task for session {session_id}")
                return memory_scanner_pb2.Empty()
            except Exception as e:
                self.error_counter.add(1, {"operation": "unfreeze", "error": str(e)})
                logger.error(f"Failed to cancel freeze task for session {session_id}", exc_info=e)
                return memory_scanner_pb2.Empty()

    async def GetState(self, request: memory_scanner_pb2.StateRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.StateResponse:
        with tracer.start_as_current_span("get_state") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                scanner = self.scanners[session_id]
                version = self.state_versions[session_id]
                
                state = {
                    "scan_results": scanner.get_results(),
                    "frozen_addresses": [addr for _, (_, addr) in self.freezer_tasks.items() if _ == session_id]
                }

                return memory_scanner_pb2.StateResponse(
                    state=str(state),
                    version=version
                )
            except Exception as e:
                logger.error(f"Failed to get state for session {session_id}", exc_info=e)
                return memory_scanner_pb2.StateResponse(error_message=str(e))

    async def SyncState(self, request: memory_scanner_pb2.SyncRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.SyncResponse:
        with tracer.start_as_current_span("sync_state") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                current_version = self.state_versions[session_id]
                needs_sync = current_version != request.version

                return memory_scanner_pb2.SyncResponse(
                    needs_sync=needs_sync,
                    current_version=current_version
                )
            except Exception as e:
                logger.error(f"Failed to check sync state for session {session_id}", exc_info=e)
                return memory_scanner_pb2.SyncResponse(
                    needs_sync=True,
                    error_message=str(e)
                )

    async def ScanPattern(self, request: memory_scanner_pb2.PatternScanRequest, context: grpc.aio.ServicerContext) -> memory_scanner_pb2.ScanResponse:
        with tracer.start_as_current_span("scan_pattern") as span:
            try:
                session_id = request.session_id
                if session_id not in self.sessions:
                    raise ValueError(f"Invalid session ID: {session_id}")

                scanner = self.scanners[session_id]
                span.set_attribute("pattern", request.pattern)

                with self.operation_duration.time({"operation": "pattern_scan"}):
                    results = await scanner.pattern_scan(request.pattern)

                self.operation_counter.add(1, {"operation": "pattern_scan"})
                self.state_versions[session_id] = str(uuid.uuid4())

                logger.info(f"Pattern scan completed for session {session_id}, found {len(results)} results")
                return memory_scanner_pb2.ScanResponse(
                    results=[
                        memory_scanner_pb2.ScanResult(
                            address=addr,
                            value=bytes([])  # Pattern scan doesn't return values, just addresses
                        )
                        for addr, _ in results
                    ],
                    checkpoint_id=str(uuid.uuid4())
                )
            except Exception as e:
                self.error_counter.add(1, {"operation": "pattern_scan", "error": str(e)})
                logger.error(f"Failed to perform pattern scan for session {session_id}", exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.ScanResponse()