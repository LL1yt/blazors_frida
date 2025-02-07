from setuptools import setup, find_packages
from setuptools.command.build_py import build_py
import subprocess

class BuildPyCommand(build_py):
    def run(self):
        proto_dir = 'MemoryScanner/proto'
        output_dir = 'MemoryScanner/Proto'
        subprocess.check_call([
            'python', '-m', 'grpc_tools.protoc',
            f'-I{proto_dir}',
            f'--python_out={output_dir}',
            f'--grpc_python_out={output_dir}',
            f'{proto_dir}/health.proto',
            f'{proto_dir}/memory_scanner.proto'
        ])
        super().run()

setup(
    name='MemoryScanner',
    version='1.0',
    packages=find_packages(),
    package_data={
        'MemoryScanner.Proto': ['*.pyi', '*.proto'],
    },
    install_requires=[
        'grpcio',
        'grpcio-tools',
        'opentelemetry-sdk'
    ],
    cmdclass={
        'build_py': BuildPyCommand,
    },
)
