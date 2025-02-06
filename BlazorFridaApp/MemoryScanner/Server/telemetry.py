import logging
from opentelemetry import trace, metrics
from opentelemetry.trace.status import Status, StatusCode
from opentelemetry.instrumentation.grpc import (
    GrpcInstrumentorClient,
    GrpcInstrumentorServer,
)
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import ConsoleSpanExporter, SimpleSpanProcessor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import (
    ConsoleMetricExporter,
    PeriodicExportingMetricReader,
)
from pythonjsonlogger import jsonlogger

# Configure logging
logger = logging.getLogger(__name__)
logHandler = logging.StreamHandler()
formatter = jsonlogger.JsonFormatter(
    "%(timestamp)s %(level)s %(name)s %(message)s %(trace_id)s %(span_id)s"
)
logHandler.setFormatter(formatter)
logger.addHandler(logHandler)
logger.setLevel(logging.INFO)

# Configure OpenTelemetry tracing
trace.set_tracer_provider(TracerProvider())
tracer = trace.get_tracer(__name__)
trace.get_tracer_provider().add_span_processor(
    SimpleSpanProcessor(ConsoleSpanExporter())
)

# Configure OpenTelemetry metrics
metrics.set_meter_provider(
    MeterProvider(
        metric_readers=[
            PeriodicExportingMetricReader(
                ConsoleMetricExporter(), export_interval_millis=5000
            )
        ]
    )
)

# Initialize gRPC instrumentation
grpc_server_instrumentor = GrpcInstrumentorServer()
grpc_server_instrumentor.instrument()

grpc_client_instrumentor = GrpcInstrumentorClient()
grpc_client_instrumentor.instrument()