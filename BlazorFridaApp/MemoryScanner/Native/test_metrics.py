#!/usr/bin/env python3
import asyncio
import logging
import unittest
from unittest.mock import MagicMock, patch
import grpc
from opentelemetry import trace
from opentelemetry.trace.status import Status, StatusCode
import memory_scanner_pb2
from memory_scanner_server import MemoryScannerService

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class TestMetricsCollection(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        # Mock metrics collectors
        self.active_sessions_counter = MagicMock()
        self.operation_counter = MagicMock()
        self.operation_duration = MagicMock()
        self.error_counter = MagicMock()

        # Create patches for metrics
        self.patches = [
            patch(
                "memory_scanner_server.active_sessions_counter",
                self.active_sessions_counter,
            ),
            patch("memory_scanner_server.operation_counter", self.operation_counter),
            patch("memory_scanner_server.operation_duration", self.operation_duration),
            patch("memory_scanner_server.error_counter", self.error_counter),
        ]

        # Start all patches
        for p in self.patches:
            p.start()

        # Initialize service
        self.service = MemoryScannerService()

        # Mock context
        self.context = MagicMock()
        self.context.set_code = MagicMock()
        self.context.set_details = MagicMock()

    async def asyncTearDown(self):
        # Stop all patches
        for p in self.patches:
            p.stop()

    async def test_attach_metrics(self):
        """Test metrics collection for AttachToProcess"""
        # Arrange
        request = memory_scanner_pb2.ProcessRequest(pid=1234)

        # Act
        with patch("memory_scanner_server.FridaMemoryScanner") as mock_frida:
            instance = mock_frida.return_value

            async def mock_attach(pid):
                # Set span attributes
                current_span = trace.get_current_span()
                current_span.set_attribute("process.id", pid)
                pass

            instance.attach_to_process = mock_attach
            response = await self.service.AttachToProcess(request, self.context)

        # Assert
        self.active_sessions_counter.add.assert_called_once_with(1)
        self.operation_counter.add.assert_called_once_with(1, {"operation": "attach"})
        self.assertTrue(response.success)

    async def test_attach_error_metrics(self):
        """Test error metrics collection for AttachToProcess"""
        # Arrange
        request = memory_scanner_pb2.ProcessRequest(pid=1234)

        # Act
        with patch("memory_scanner_server.FridaMemoryScanner") as mock_frida:
            instance = mock_frida.return_value

            async def mock_attach(pid):
                current_span = trace.get_current_span()
                current_span.set_attribute("process.id", pid)
                current_span.set_status(Status(StatusCode.ERROR, "Test error"))
                raise Exception("Test error")

            instance.attach_to_process = mock_attach
            response = await self.service.AttachToProcess(request, self.context)

        # Assert
        self.error_counter.add.assert_called_once_with(
            1, {"operation": "attach", "error": "Test error"}
        )
        self.assertFalse(response.success)

    async def test_write_metrics(self):
        """Test metrics collection for WriteMemory"""
        # Arrange
        session_id = "test_session"
        self.service.sessions[session_id] = MagicMock()
        request = memory_scanner_pb2.WriteRequest(
            session_id=session_id, address=1000, value=b"test", value_type="bytes"
        )

        # Act
        with patch("memory_scanner_server.MemoryWriter") as mock_writer:
            instance = mock_writer.return_value

            async def mock_write(address, value, value_type):
                current_span = trace.get_current_span()
                current_span.set_attribute("memory.address", address)
                current_span.set_attribute("memory.value_type", value_type)
                pass

            instance.write = mock_write
            response = await self.service.WriteMemory(request, self.context)

        # Assert
        self.operation_counter.add.assert_called_once_with(1, {"operation": "write"})
        self.operation_duration.record.assert_called_once()
        self.assertTrue(response.success)

    async def test_write_error_metrics(self):
        """Test error metrics collection for WriteMemory"""
        # Arrange
        session_id = "test_session"
        self.service.sessions[session_id] = MagicMock()
        request = memory_scanner_pb2.WriteRequest(
            session_id=session_id, address=1000, value=b"test", value_type="bytes"
        )

        # Act
        with patch("memory_scanner_server.MemoryWriter") as mock_writer:
            instance = mock_writer.return_value

            async def mock_write(address, value, value_type):
                current_span = trace.get_current_span()
                current_span.set_attribute("memory.address", address)
                current_span.set_attribute("memory.value_type", value_type)
                current_span.set_status(Status(StatusCode.ERROR, "Write error"))
                raise Exception("Write error")

            instance.write = mock_write
            response = await self.service.WriteMemory(request, self.context)

        # Assert
        self.error_counter.add.assert_called_once_with(
            1, {"operation": "write", "error": "Write error"}
        )
        self.assertFalse(response.success)

    async def test_read_metrics(self):
        """Test metrics collection for ReadMemory"""
        # Arrange
        session_id = "test_session"
        self.service.sessions[session_id] = MagicMock()
        request = memory_scanner_pb2.ReadRequest(
            session_id=session_id, address=1000, size=4, value_type="int32"
        )

        # Act
        with patch("memory_scanner_server.MemoryReader") as mock_reader:
            instance = mock_reader.return_value

            async def mock_read(address, size, value_type):
                current_span = trace.get_current_span()
                current_span.set_attribute("memory.address", address)
                current_span.set_attribute("memory.size", size)
                current_span.set_attribute("memory.value_type", value_type)
                return b"test"

            instance.read = mock_read
            response = await self.service.ReadMemory(request, self.context)

        # Assert
        self.operation_counter.add.assert_called_once_with(1, {"operation": "read"})
        self.operation_duration.record.assert_called_once()
        self.assertTrue(response.success)

    async def test_scan_metrics(self):
        """Test metrics collection for ScanMemory"""
        # Arrange
        session_id = "test_session"
        scanner_mock = MagicMock()

        async def mock_scan(*args, **kwargs):
            current_span = trace.get_current_span()
            current_span.set_attribute("memory.value_type", kwargs.get("value_type"))
            current_span.set_attribute(
                "memory.comparison_type", kwargs.get("comparison_type")
            )
            current_span.set_attribute(
                "memory.ranges_count", len(kwargs.get("ranges", []))
            )
            return []

        scanner_mock.scan = mock_scan
        self.service.scanners[session_id] = scanner_mock
        request = memory_scanner_pb2.ScanRequest(
            session_id=session_id,
            value_type="int32",
            value=b"test",
            comparison_type="exact",
            ranges=[],
        )

        # Act
        response = await self.service.ScanMemory(request, self.context)

        # Assert
        self.operation_counter.add.assert_called_once_with(1, {"operation": "scan"})
        self.operation_duration.record.assert_called_once()
        self.assertIsNotNone(response.checkpoint_id)


if __name__ == "__main__":
    asyncio.run(unittest.main())
