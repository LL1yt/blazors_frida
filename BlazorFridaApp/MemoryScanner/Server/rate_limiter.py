import asyncio
import time
from collections import defaultdict, deque
from typing import DefaultDict, Deque, Optional, Dict, Callable, Any
import logging
from .config import RATE_LIMITS
from functools import wraps
from datetime import datetime, timedelta

logger = logging.getLogger(__name__)


class RateLimiter:
    def __init__(self, max_requests: int = 10, time_window: int = 60):
        self.max_requests = max_requests
        self.time_window = time_window  # in seconds
        self.requests: Dict[str, list] = {}

    def is_allowed(self, client_id: str) -> bool:
        now = time.time()

        # Initialize if this is a new client
        if client_id not in self.requests:
            self.requests[client_id] = []

        # Remove old requests outside the time window
        self.requests[client_id] = [
            req_time
            for req_time in self.requests[client_id]
            if now - req_time <= self.time_window
        ]

        # Check if we're under the limit
        if len(self.requests[client_id]) < self.max_requests:
            self.requests[client_id].append(now)
            return True

        return False

    def time_until_reset(self, client_id: str) -> float:
        if client_id not in self.requests or not self.requests[client_id]:
            return 0

        oldest_request = min(self.requests[client_id])
        return max(0, self.time_window - (time.time() - oldest_request))


class RateLimitDecorator:
    def __init__(self, limiter: RateLimiter):
        self.limiter = limiter

    def __call__(self, func: Callable) -> Callable:
        @wraps(func)
        async def wrapper(instance: Any, request: Any, context: Any) -> Any:
            client_id = context.peer() if context else "default"

            if not self.limiter.is_allowed(client_id):
                wait_time = self.limiter.time_until_reset(client_id)
                logger.warning(
                    f"Rate limit exceeded for {client_id}. Must wait {wait_time:.2f} seconds"
                )
                context.abort(
                    code=429,
                    details=f"Too many requests. Please wait {wait_time:.2f} seconds",
                )

            return await func(instance, request, context)

        return wrapper


# Global rate limiter instance
_rate_limiter = RateLimiter()

# Global decorator instance
rate_limit = RateLimitDecorator(_rate_limiter)
