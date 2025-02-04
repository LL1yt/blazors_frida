import asyncio
from typing import Dict
import grpc
from grpc import aio
import health_pb2
import health_pb2_grpc


class HealthServicer(health_pb2_grpc.HealthServicer):
    def __init__(self):
        self._server_status: Dict[str, health_pb2.HealthCheckResponse.ServingStatus] = (
            {}
        )
        self._server_status[""] = health_pb2.HealthCheckResponse.ServingStatus.SERVING
        self._server_status["memory_scanner.MemoryScanner"] = (
            health_pb2.HealthCheckResponse.ServingStatus.SERVING
        )
        self._watchers = set()

    def set_status(
        self, service: str, status: health_pb2.HealthCheckResponse.ServingStatus
    ) -> None:
        self._server_status[service] = status
        self._notify_watchers()

    def _notify_watchers(self) -> None:
        for watcher in self._watchers:
            if not watcher.done():
                watcher.set_result(None)

    async def Check(
        self, request: health_pb2.HealthCheckRequest, context: grpc.aio.ServicerContext
    ) -> health_pb2.HealthCheckResponse:
        if request.service in self._server_status:
            return health_pb2.HealthCheckResponse(
                status=self._server_status[request.service]
            )
        else:
            context.set_code(grpc.StatusCode.NOT_FOUND)
            context.set_details(f"Service {request.service} not found")
            return health_pb2.HealthCheckResponse(
                status=health_pb2.HealthCheckResponse.ServingStatus.SERVICE_UNKNOWN
            )

    async def Watch(
        self, request: health_pb2.HealthCheckRequest, context: grpc.aio.ServicerContext
    ) -> health_pb2.HealthCheckResponse:
        last_status = None
        while True:
            if request.service in self._server_status:
                current_status = self._server_status[request.service]
                if current_status != last_status:
                    yield health_pb2.HealthCheckResponse(status=current_status)
                    last_status = current_status
            else:
                yield health_pb2.HealthCheckResponse(
                    status=health_pb2.HealthCheckResponse.ServingStatus.SERVICE_UNKNOWN
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
