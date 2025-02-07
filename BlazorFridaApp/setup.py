from setuptools import setup, find_packages
from setuptools.command.build_py import build_py
import subprocess
import os

class BuildPyCommand(build_py):
    def run(self):
        proto_dir = os.path.join('MemoryScanner', 'proto')
        output_dir = os.path.join('MemoryScanner', 'Proto')
        
        os.makedirs(output_dir, exist_ok=True)
        
        subprocess.check_call([
            'python', '-m', 'grpc_tools.protoc',
            f'-I={proto_dir}',
            f'--python_out={output_dir}',
            f'--grpc_python_out={output_dir}',
            os.path.join(proto_dir, 'health.proto'),
            os.path.join(proto_dir, 'memory_scanner.proto')
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
