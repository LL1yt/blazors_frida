import asyncio
import time
from collections import defaultdict, deque
from typing import DefaultDict, Deque, Optional
import logging
from .config import RATE_LIMITS

logger = logging.getLogger(__name__)


class RateLimiter:
    def __init__(self):
        self.requests_per_minute = RATE_LIMITS["max_requests_per_minute"]
        self.max_concurrent = RATE_LIMITS["max_concurrent_operations"]

        # Track request timestamps per client
        self.request_history: DefaultDict[str, Deque[float]] = defaultdict(
            lambda: deque(maxlen=self.requests_per_minute)
        )

        # Track concurrent operations per client
        self.concurrent_ops: DefaultDict[str, int] = defaultdict(int)

        # Lock for thread safety
        self._lock = asyncio.Lock()

    async def acquire(self, client_id: str) -> bool:
        """
        Attempt to acquire a rate limit slot for the client.
        Returns True if acquired, False if rate limit exceeded.
        """
        async with self._lock:
            now = time.time()

            # Clean up old request history
            while (
                self.request_history[client_id]
                and self.request_history[client_id][0] < now - 60
            ):
                self.request_history[client_id].popleft()

            # Check rate limits
            if len(self.request_history[client_id]) >= self.requests_per_minute:
                logger.warning(
                    f"Rate limit exceeded for client {client_id}: "
                    f"{len(self.request_history[client_id])} requests/minute"
                )
                return False

            if self.concurrent_ops[client_id] >= self.max_concurrent:
                logger.warning(
                    f"Concurrent operation limit exceeded for client {client_id}: "
                    f"{self.concurrent_ops[client_id]} operations"
                )
                return False

            # Acquire slot
            self.request_history[client_id].append(now)
            self.concurrent_ops[client_id] += 1
            return True

    async def release(self, client_id: str):
        """Release a concurrent operation slot for the client."""
        async with self._lock:
            self.concurrent_ops[client_id] = max(0, self.concurrent_ops[client_id] - 1)


class RateLimitDecorator:
    """Decorator for rate-limiting gRPC methods."""

    def __init__(self, rate_limiter: RateLimiter):
        self.rate_limiter = rate_limiter

    def __call__(self, func):
        async def wrapper(service_instance, request, context, *args, **kwargs):
            # Extract client ID from metadata or use peer address
            client_id = dict(context.invocation_metadata()).get(
                "client-id", context.peer()
            )

            if not await self.rate_limiter.acquire(client_id):
                context.set_code(grpc.StatusCode.RESOURCE_EXHAUSTED)
                context.set_details("Rate limit exceeded")
                return None

            try:
                return await func(service_instance, request, context, *args, **kwargs)
            finally:
                await self.rate_limiter.release(client_id)

        return wrapper
