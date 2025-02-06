from opentelemetry import metrics

# Configure metrics
meter = metrics.get_meter(__name__)

active_sessions_counter = meter.create_counter(
    "active_sessions",
    description="Number of active scanning sessions",
)

operation_counter = meter.create_counter(
    "operations",
    description="Number of operations performed",
)

operation_duration = meter.create_histogram(
    "operation_duration",
    description="Duration of operations",
    unit="ms",
)

error_counter = meter.create_counter(
    "errors",
    description="Number of errors encountered",
)