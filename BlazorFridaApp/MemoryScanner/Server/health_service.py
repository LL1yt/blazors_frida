import asyncio
from typing import Dict
import grpc
from grpc import aio
import health_pb2
import health_pb2_grpc
from health_pb2 import HealthCheckRequest, HealthCheckResponse


class HealthServicer(health_pb2_grpc.HealthServicer):
    def __init__(self):
        self._server_status: Dict[str, HealthCheckResponse.ServingStatus] = {}
        self._server_status[""] = HealthCheckResponse.ServingStatus.SERVING
        self._server_status["memory_scanner.MemoryScanner"] = (
            HealthCheckResponse.ServingStatus.SERVING
        )
        self._watchers = set()

    def set_status(
        self, service: str, status: HealthCheckResponse.ServingStatus
    ) -> None:
        self._server_status[service] = status
        self._notify_watchers()

    def _notify_watchers(self) -> None:
        for watcher in self._watchers:
            if not watcher.done():
                watcher.set_result(None)

    async def Check(
        self, request: HealthCheckRequest, context: grpc.aio.ServicerContext
    ) -> HealthCheckResponse:
        if request.service in self._server_status:
            return HealthCheckResponse(
                status=self._server_status[request.service]
            )
        else:
            context.set_code(grpc.StatusCode.NOT_FOUND)
            context.set_details(f"Service {request.service} not found")
            return HealthCheckResponse(
                status=HealthCheckResponse.ServingStatus.SERVICE_UNKNOWN
            )

    async def Watch(
        self, request: HealthCheckRequest, context: grpc.aio.ServicerContext
    ) -> HealthCheckResponse:
        last_status = None
        while True:
            if request.service in self._server_status:
                current_status = self._server_status[request.service]
                if current_status != last_status:
                    yield HealthCheckResponse(status=current_status)
                    last_status = current_status
            else:
                yield HealthCheckResponse(
                    status=HealthCheckResponse.ServingStatus.SERVICE_UNKNOWN
                )
                break

            try:
                watcher = asyncio.Future()
                self._watchers.add(watcher)
                await watcher
            except asyncio.CancelledError:
                break
            finally:
                self._watchers.discard(watcher)

    def clear_status(self, service: str) -> None:
        if service in self._server_status:
            del self._server_status[service]
            self._notify_watchers()