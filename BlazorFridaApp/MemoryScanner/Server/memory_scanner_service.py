import uuid
import logging
import asyncio
import time
from typing import Dict, Optional
import grpc
from grpc import aio
from opentelemetry import trace
from opentelemetry.trace.status import Status, StatusCode

import sys

sys.path.append("../Proto")
sys.path.append("../Native")
sys.path.append("../Operations")

from MemoryScanner.Native import memory_scanner_pb2, memory_scanner_pb2_grpc
from MemoryScanner.Native.frida_module import FridaMemoryScanner
from MemoryScanner.Native.process_list import get_process_list
from MemoryScanner.Server.session_manager import SessionManager
from MemoryScanner.Server.rate_limiter import (
    RateLimiter,
    RateLimitDecorator,
    rate_limit,
)
from MemoryScanner.Server.retry import with_retry
from MemoryScanner.Server.metrics import (
    active_sessions_counter,
    operation_counter,
    operation_duration,
    error_counter,
)

logger = logging.getLogger(__name__)
tracer = trace.get_tracer(__name__)


def log_client_info(context: grpc.aio.ServicerContext, method_name: str) -> str:
    """Log client information from metadata and return client ID."""
    metadata = dict(context.invocation_metadata())
    client_id = metadata.get("client-id", "unknown")
    test_name = metadata.get("test-name", "unknown")
    logger.info(f"[{method_name}] Request from client {client_id} (test: {test_name})")
    return client_id


class MemoryScannerService(memory_scanner_pb2_grpc.MemoryScannerServicer):
    def __init__(self):
        from MemoryScanner.Server import metrics

        metrics.init_metrics()

        self.session_manager = SessionManager()
        self.rate_limiter = RateLimiter()
        self.rate_limit = RateLimitDecorator(self.rate_limiter)

    async def start(self):
        """Start the service and initialize components."""
        await self.session_manager.start()
        logger.info("MemoryScannerService started")

    async def stop(self):
        """Stop the service and cleanup resources."""
        await self.session_manager.stop()
        logger.info("MemoryScannerService stopped")

    @with_retry(exceptions=(grpc.RpcError,))
    async def ListProcesses(
        self, request: memory_scanner_pb2.Empty, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.ProcessList:
        client_id = log_client_info(context, "ListProcesses")
        logger.info(f"Starting process list enumeration for client {client_id}...")
        with tracer.start_as_current_span("list_processes") as span:
            try:
                processes = get_process_list()
                logger.info(
                    f"Process list enumeration completed for client {client_id}"
                )
                return memory_scanner_pb2.ProcessList(
                    processes=[
                        memory_scanner_pb2.ProcessInfo(
                            pid=p.pid, name=p.name, path=p.path
                        )
                        for p in processes
                    ]
                )
            except Exception as e:
                logger.error(
                    f"Error in process list enumeration for client {client_id}: {str(e)}"
                )
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(str(e))
                return memory_scanner_pb2.ProcessList()

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def AttachToProcess(
        self,
        request: memory_scanner_pb2.ProcessRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.AttachResponse:
        client_id = log_client_info(context, "AttachToProcess")
        with tracer.start_as_current_span("attach_to_process") as span:
            try:
                frida_scanner = FridaMemoryScanner()
                span.set_attribute("process.id", request.pid)

                # Attach to process and wait for initialization
                success = await frida_scanner.attach_to_process(request.pid)
                if not success:
                    return memory_scanner_pb2.AttachResponse(
                        success=False,
                        error_message=f"Failed to attach to process {request.pid}",
                    )

                # Give the scanner time to fully initialize
                retry_count = 0
                max_retries = 1  # Changed from 10 to 1
                while retry_count < max_retries:
                    if frida_scanner.is_initialized:
                        break
                    await asyncio.sleep(0.5)  # Increased from 0.2
                    retry_count += 1
                    logger.debug(
                        f"Waiting for scanner to initialize (attempt {retry_count}/{max_retries})"
                    )

                if not frida_scanner.is_initialized:
                    error_msg = f"Scanner failed to initialize for process {request.pid} after {max_retries} attempts"
                    logger.error(error_msg)
                    return memory_scanner_pb2.AttachResponse(
                        success=False, error_message=error_msg
                    )

                # Create session only after scanner is ready
                session = self.session_manager.create_session(
                    request.pid, frida_scanner
                )
                logger.info(
                    f"Successfully attached to process {request.pid} with session {session.session_id}"
                )
                return memory_scanner_pb2.AttachResponse(
                    success=True, session_id=session.session_id
                )
            except Exception as e:
                logger.error(f"Failed to attach to process {request.pid}", exc_info=e)
                return memory_scanner_pb2.AttachResponse(
                    success=False, error_message=str(e)
                )

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def DetachFromProcess(
        self,
        request: memory_scanner_pb2.DetachRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.DetachResponse:
        client_id = log_client_info(context, "DetachFromProcess")
        with tracer.start_as_current_span("detach_from_process") as span:
            try:
                await self.session_manager.remove_session(request.session_id)
                logger.info(f"Successfully detached from session {request.session_id}")
                return memory_scanner_pb2.DetachResponse(success=True)
            except Exception as e:
                logger.error(
                    f"Failed to detach from session {request.session_id}", exc_info=e
                )
                return memory_scanner_pb2.DetachResponse(
                    success=False, error_message=str(e)
                )

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def ScanMemory(
        self, request: memory_scanner_pb2.ScanRequest, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.ScanResponse:
        client_id = log_client_info(context, "ScanMemory")
        logger.info(f"Starting memory scan for client {client_id}...")
        with tracer.start_as_current_span("scan_memory") as span:
            try:
                session = self.session_manager.get_session(request.session_id)
                if not session:
                    error_msg = f"Session {request.session_id} not found"
                    logger.error(error_msg)
                    context.set_code(grpc.StatusCode.NOT_FOUND)
                    context.set_details(error_msg)
                    return memory_scanner_pb2.ScanResponse()

                if not session.scanner or not session.scanner.is_initialized:
                    error_msg = f"Scanner not properly initialized for session {request.session_id}"
                    logger.error(error_msg)
                    context.set_code(grpc.StatusCode.FAILED_PRECONDITION)
                    context.set_details(error_msg)
                    return memory_scanner_pb2.ScanResponse()

                span.set_attribute("scan.type", request.scan_type)
                span.set_attribute("value.type", request.value_type)

                results = await session.scanner.scan(
                    request.value, request.value_type, request.comparison_type
                )

                checkpoint_id = str(uuid.uuid4())
                logger.info(
                    f"Scan completed for session {request.session_id}, found {len(results)} results"
                )

                return memory_scanner_pb2.ScanResponse(
                    results=[
                        memory_scanner_pb2.ScanResult(
                            address=addr,
                            value=(
                                value
                                if isinstance(value, bytes)
                                else str(value).encode("utf-8")
                            ),
                        )
                        for addr, value in results
                    ],
                    checkpoint_id=checkpoint_id,
                )
            except Exception as e:
                error_msg = (
                    f"Failed to perform scan for session {request.session_id}: {str(e)}"
                )
                logger.error(error_msg, exc_info=e)
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(error_msg)
                return memory_scanner_pb2.ScanResponse()

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def ReadMemory(
        self, request: memory_scanner_pb2.ReadRequest, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.ReadResponse:
        client_id = log_client_info(context, "ReadMemory")
        with tracer.start_as_current_span("read_memory") as span:
            try:
                session = self.session_manager.get_session(request.session_id)
                span.set_attribute("address", request.address)
                span.set_attribute("value.type", request.value_type)

                value = await session.reader.read_memory(
                    request.address, request.value_type
                )

                logger.info(
                    f"Memory read completed for session {request.session_id} at address {request.address}"
                )
                return memory_scanner_pb2.ReadResponse(value=value, success=True)
            except Exception as e:
                logger.error(
                    f"Failed to read memory for session {request.session_id}",
                    exc_info=e,
                )
                return memory_scanner_pb2.ReadResponse(
                    success=False, error_message=str(e)
                )

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def WriteMemory(
        self,
        request: memory_scanner_pb2.WriteRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.WriteResponse:
        client_id = log_client_info(context, "WriteMemory")
        with tracer.start_as_current_span("write_memory") as span:
            try:
                session = self.session_manager.get_session(request.session_id)
                span.set_attribute("address", request.address)
                span.set_attribute("value.type", request.value_type)

                success = await session.writer.write_memory(
                    request.address, request.value, request.value_type
                )

                logger.info(
                    f"Memory write completed for session {request.session_id} at address {request.address}"
                )
                return memory_scanner_pb2.WriteResponse(success=success)
            except Exception as e:
                logger.error(
                    f"Failed to write memory for session {request.session_id}",
                    exc_info=e,
                )
                return memory_scanner_pb2.WriteResponse(
                    success=False, error_message=str(e)
                )

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def FreezeValue(
        self,
        request: memory_scanner_pb2.FreezeRequest,
        context: grpc.aio.ServicerContext,
    ):
        """Stream status updates while freezing a value at the specified address."""
        client_id = log_client_info(context, "FreezeValue")
        with tracer.start_as_current_span("freeze_value") as span:
            try:
                session = self.session_manager.get_session(request.session_id)
                span.set_attribute("address", request.address)
                span.set_attribute("value.type", request.value_type)

                # Convert request value to int for comparison if it's an integer type
                expected_value = None
                if request.value_type in ["int32", "int64", "int16", "int8"]:
                    expected_value = int.from_bytes(
                        request.value, byteorder="little", signed=True
                    )

                while True:
                    try:
                        # Write the value
                        success = await session.writer.write_memory(
                            request.address, request.value, request.value_type
                        )

                        if not success:
                            logger.error(
                                f"Failed to write memory in freeze task for session {request.session_id}"
                            )
                            yield memory_scanner_pb2.FreezeStatus(
                                active=False,
                                current_value=request.value,
                                error_message="Failed to write memory",
                            )
                            break

                        # Read back the current value to verify
                        current_value = await session.reader.read_memory(
                            request.address, request.value_type
                        )

                        # Verify the value based on type
                        if request.value_type in ["int32", "int64", "int16", "int8"]:
                            current_int = int.from_bytes(
                                current_value, byteorder="little", signed=True
                            )
                            current_value = current_int.to_bytes(
                                len(request.value), byteorder="little", signed=True
                            )
                            if current_int != expected_value:
                                logger.error(
                                    f"Value mismatch: got {current_int}, expected {expected_value}"
                                )
                                yield memory_scanner_pb2.FreezeStatus(
                                    active=False,
                                    current_value=current_value,
                                    error_message=f"Value mismatch: got {current_int}, expected {expected_value}",
                                )
                                break
                        else:
                            if not isinstance(current_value, bytes):
                                current_value = (
                                    current_value
                                    if isinstance(current_value, bytes)
                                    else str(current_value).encode("utf-8")
                                )
                            if current_value != request.value:
                                yield memory_scanner_pb2.FreezeStatus(
                                    active=False,
                                    current_value=current_value,
                                    error_message="Value mismatch",
                                )
                                break

                        yield memory_scanner_pb2.FreezeStatus(
                            active=True, current_value=current_value
                        )

                        await asyncio.sleep(
                            0.1
                        )  # Small delay to prevent excessive CPU usage

                    except asyncio.CancelledError:
                        logger.info(
                            f"Freeze task cancelled for session {request.session_id} at address {request.address}"
                        )
                        break
                    except Exception as e:
                        logger.error(
                            f"Error in freeze task for session {request.session_id}",
                            exc_info=e,
                        )
                        yield memory_scanner_pb2.FreezeStatus(
                            active=False,
                            current_value=request.value,
                            error_message=str(e),
                        )
                        break

            except Exception as e:
                logger.error(
                    f"Failed to start freeze task for session {request.session_id}",
                    exc_info=e,
                )
                yield memory_scanner_pb2.FreezeStatus(
                    active=False, current_value=request.value, error_message=str(e)
                )

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def UnfreezeValue(
        self,
        request: memory_scanner_pb2.UnfreezeRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.Empty:
        client_id = log_client_info(context, "UnfreezeValue")
        with tracer.start_as_current_span("unfreeze_value") as span:
            try:
                session = self.session_manager.get_session(request.session_id)
                if request.address in session.freezer_tasks:
                    task, _ = session.freezer_tasks[request.address]
                    task.cancel()
                    del session.freezer_tasks[request.address]

                logger.info(f"Cancelled freeze task for session {request.session_id}")
                return memory_scanner_pb2.Empty()
            except Exception as e:
                logger.error(
                    f"Failed to cancel freeze task for session {request.session_id}",
                    exc_info=e,
                )
                return memory_scanner_pb2.Empty()

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def GetState(
        self,
        request: memory_scanner_pb2.StateRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.StateResponse:
        client_id = log_client_info(context, "GetState")
        with tracer.start_as_current_span("get_state") as span:
            try:
                session = self.session_manager.get_session(request.session_id)
                state = {
                    "scan_results": session.scanner.get_results(),
                    "frozen_addresses": list(session.freezer_tasks.keys()),
                }

                return memory_scanner_pb2.StateResponse(
                    state=str(state), version=session.state_version
                )
            except Exception as e:
                logger.error(
                    f"Failed to get state for session {request.session_id}", exc_info=e
                )
                return memory_scanner_pb2.StateResponse(error_message=str(e))

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def SyncState(
        self, request: memory_scanner_pb2.SyncRequest, context: grpc.aio.ServicerContext
    ) -> memory_scanner_pb2.SyncResponse:
        client_id = log_client_info(context, "SyncState")
        with tracer.start_as_current_span("sync_state") as span:
            try:
                session = self.session_manager.get_session(request.session_id)
                needs_sync = session.state_version != request.version

                return memory_scanner_pb2.SyncResponse(
                    needs_sync=needs_sync, current_version=session.state_version
                )
            except Exception as e:
                logger.error(
                    f"Failed to check sync state for session {request.session_id}",
                    exc_info=e,
                )
                return memory_scanner_pb2.SyncResponse(
                    needs_sync=True, error_message=str(e)
                )

    @with_retry(exceptions=(grpc.RpcError,))
    @rate_limit
    async def ScanPattern(
        self,
        request: memory_scanner_pb2.PatternScanRequest,
        context: grpc.aio.ServicerContext,
    ) -> memory_scanner_pb2.ScanResponse:
        """Scan memory for a specific pattern."""
        client_id = log_client_info(context, "ScanPattern")
        with tracer.start_as_current_span("scan_pattern") as span:
            try:
                session = self.session_manager.get_session(request.session_id)
                if not session:
                    error_msg = f"Session {request.session_id} not found"
                    logger.error(error_msg)
                    context.set_code(grpc.StatusCode.NOT_FOUND)
                    context.set_details(error_msg)
                    return memory_scanner_pb2.ScanResponse()

                if not session.scanner or not session.scanner.is_initialized:
                    error_msg = f"Scanner not properly initialized for session {request.session_id}"
                    logger.error(error_msg)
                    context.set_code(grpc.StatusCode.FAILED_PRECONDITION)
                    context.set_details(error_msg)
                    return memory_scanner_pb2.ScanResponse()

                # Convert pattern string to bytes and mask
                pattern_parts = request.pattern.split()
                pattern_bytes = []
                mask = ""

                for part in pattern_parts:
                    if part == "??":
                        pattern_bytes.append(0)
                        mask += "?"
                    else:
                        pattern_bytes.append(int(part, 16))
                        mask += "x"

                pattern = bytes(pattern_bytes)

                # Perform the pattern scan using the session's scanner
                results = await session.scanner.scan_pattern(pattern, mask)

                # Convert results to response format
                response_results = []
                for address in results:
                    response_results.append(
                        memory_scanner_pb2.ScanResult(address=address)
                    )

                logger.info(
                    f"Pattern scan completed for session {request.session_id}, found {len(results)} results"
                )
                return memory_scanner_pb2.ScanResponse(results=response_results)

            except Exception as e:
                error_msg = f"Pattern scan failed: {str(e)}"
                logger.error(error_msg, exc_info=e)
                span.set_status(Status(StatusCode.ERROR, str(e)))
                context.set_code(grpc.StatusCode.INTERNAL)
                context.set_details(error_msg)
                return memory_scanner_pb2.ScanResponse()
