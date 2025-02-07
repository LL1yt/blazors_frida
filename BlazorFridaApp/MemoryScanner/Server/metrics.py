from opentelemetry import metrics

# Initialize metrics as None
active_sessions_counter = None
operation_counter = None
operation_duration = None
error_counter = None

def init_metrics():
    global active_sessions_counter, operation_counter, operation_duration, error_counter
    
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