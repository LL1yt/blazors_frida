#!/usr/bin/env python3
import os
import sys
from grpc_tools import protoc


def generate_protos():
    """Generate Python gRPC code from proto files."""
    proto_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "Proto")
    output_dir = os.path.dirname(__file__)

    # Ensure output directory exists
    os.makedirs(output_dir, exist_ok=True)

    protos = ["memory_scanner.proto", "health.proto"]

    for proto in protos:
        proto_path = os.path.join(proto_dir, proto)
        if not os.path.exists(proto_path):
            print(f"Error: Proto file not found: {proto_path}")
            sys.exit(1)

        print(f"Generating Python code for {proto}...")

        # Generate Python code
        protoc.main(
            [
                "grpc_tools.protoc",
                f"--proto_path={proto_dir}",
                f"--python_out={output_dir}",
                f"--grpc_python_out={output_dir}",
                proto_path,
            ]
        )

        print(f"Successfully generated Python code for {proto}")


if __name__ == "__main__":
    generate_protos()
