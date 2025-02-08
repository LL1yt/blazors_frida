from typing import Dict, Any
import os

# Server configuration
SERVER_CONFIG = {
    "address": os.getenv(
        "MEMORY_SCANNER_ADDRESS", "[::]"
    ),  # Use IPv6 wildcard address to bind to all interfaces
    "port": int(os.getenv("MEMORY_SCANNER_PORT", "50051")),
    "max_workers": int(os.getenv("MEMORY_SCANNER_MAX_WORKERS", "10")),
    "max_message_size_mb": int(os.getenv("MEMORY_SCANNER_MAX_MESSAGE_SIZE_MB", "512")),
    "graceful_shutdown_timeout": int(
        os.getenv("MEMORY_SCANNER_SHUTDOWN_TIMEOUT", "30")
    ),
}

# Session configuration
SESSION_CONFIG = {
    "cleanup_interval": int(
        os.getenv("MEMORY_SCANNER_CLEANUP_INTERVAL", "300")
    ),  # 5 minutes
    "session_timeout": int(
        os.getenv("MEMORY_SCANNER_SESSION_TIMEOUT", "3600")
    ),  # 1 hour
    "max_sessions_per_client": int(os.getenv("MEMORY_SCANNER_MAX_SESSIONS", "5")),
}

# Operation timeouts (in seconds)
OPERATION_TIMEOUTS = {
    "attach": int(os.getenv("MEMORY_SCANNER_ATTACH_TIMEOUT", "30")),
    "detach": int(os.getenv("MEMORY_SCANNER_DETACH_TIMEOUT", "10")),
    "scan": int(os.getenv("MEMORY_SCANNER_SCAN_TIMEOUT", "60")),
    "read": int(os.getenv("MEMORY_SCANNER_READ_TIMEOUT", "5")),
    "write": int(os.getenv("MEMORY_SCANNER_WRITE_TIMEOUT", "5")),
    "pattern_scan": int(os.getenv("MEMORY_SCANNER_PATTERN_SCAN_TIMEOUT", "120")),
}

# Rate limiting configuration
RATE_LIMITS = {
    "max_requests_per_minute": int(
        os.getenv("MEMORY_SCANNER_MAX_REQUESTS_PER_MINUTE", "300")
    ),
    "max_concurrent_operations": int(
        os.getenv("MEMORY_SCANNER_MAX_CONCURRENT_OPS", "50")
    ),
}

# Retry configuration
RETRY_CONFIG = {
    "max_retries": int(os.getenv("MEMORY_SCANNER_MAX_RETRIES", "3")),
    "retry_delay": float(os.getenv("MEMORY_SCANNER_RETRY_DELAY", "0.5")),
    "max_retry_delay": float(os.getenv("MEMORY_SCANNER_MAX_RETRY_DELAY", "5.0")),
}

# Memory monitoring thresholds (in MB)
MEMORY_THRESHOLDS = {
    "warning": int(os.getenv("MEMORY_SCANNER_MEMORY_WARNING_MB", "1024")),  # 1GB
    "critical": int(os.getenv("MEMORY_SCANNER_MEMORY_CRITICAL_MB", "2048")),  # 2GB
}


def get_config() -> Dict[str, Any]:
    """Returns the complete configuration dictionary."""
    return {
        "server": SERVER_CONFIG,
        "session": SESSION_CONFIG,
        "timeouts": OPERATION_TIMEOUTS,
        "rate_limits": RATE_LIMITS,
        "retry": RETRY_CONFIG,
        "memory": MEMORY_THRESHOLDS,
    }
