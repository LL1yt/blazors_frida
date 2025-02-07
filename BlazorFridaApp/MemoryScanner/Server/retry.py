import asyncio
import logging
import time
from functools import wraps
from typing import Type, Tuple, Optional, Callable, Any
from .config import RETRY_CONFIG

logger = logging.getLogger(__name__)


def with_retry(
    exceptions: Tuple[Type[Exception], ...] = (Exception,),
    max_retries: Optional[int] = None,
    retry_delay: Optional[float] = None,
    max_retry_delay: Optional[float] = None,
    exponential_backoff: bool = True,
):
    """
    Decorator for retrying async functions with exponential backoff.

    Args:
        exceptions: Tuple of exception types to catch and retry
        max_retries: Maximum number of retry attempts
        retry_delay: Initial delay between retries in seconds
        max_retry_delay: Maximum delay between retries in seconds
        exponential_backoff: Whether to use exponential backoff
    """
    max_retries = max_retries or RETRY_CONFIG["max_retries"]
    retry_delay = retry_delay or RETRY_CONFIG["retry_delay"]
    max_retry_delay = max_retry_delay or RETRY_CONFIG["max_retry_delay"]

    def decorator(func: Callable) -> Callable:
        @wraps(func)
        async def wrapper(*args: Any, **kwargs: Any) -> Any:
            last_exception = None
            current_delay = retry_delay

            for attempt in range(max_retries + 1):
                try:
                    return await func(*args, **kwargs)
                except exceptions as e:
                    last_exception = e

                    if attempt == max_retries:
                        logger.error(
                            f"Failed after {max_retries} retries",
                            exc_info=last_exception,
                        )
                        raise

                    # Calculate next delay
                    if exponential_backoff:
                        current_delay = min(current_delay * 2, max_retry_delay)

                    logger.warning(
                        f"Attempt {attempt + 1}/{max_retries + 1} failed, "
                        f"retrying in {current_delay:.1f}s: {str(e)}"
                    )

                    await asyncio.sleep(current_delay)
                except Exception as e:
                    # Don't retry other exceptions
                    logger.error("Unhandled exception", exc_info=e)
                    raise

        return wrapper

    return decorator
