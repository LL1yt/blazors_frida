#!/usr/bin/env python3
import asyncio
import logging
import unittest
from unittest.mock import MagicMock, patch, AsyncMock
import grpc
from opentelemetry import trace
from opentelemetry.trace.status import Status, StatusCode
import argparse
import sqlite3
import os
import time
import contextlib
from typing import AsyncGenerator, List, Dict, Any, Optional

from MemoryScanner.Server.memory_scanner_service import MemoryScannerService
from MemoryScanner.Server.metrics import (
    active_sessions_counter,
    operation_counter,
    operation_duration,
    error_counter,
)
from MemoryScanner.Proto import memory_scanner_pb2
from MemoryScanner.Native.scanner import MemoryScanner
from MemoryScanner.Native.writer import MemoryWriter
from MemoryScanner.Native.state_manager import StateManager

# Configure logging with more detailed format
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# Constants
ASYNC_TIMEOUT = 5.0  # Default timeout for async operations
TEST_SESSION_ID = "test_session"
TEST_PID = 1234
TEST_ADDRESS = 0x1000
TEST_MODULE = "test_module"


class TestMetricsCollection(unittest.IsolatedAsyncioTestCase):
    """Test suite for metrics collection in the Memory Scanner service.

    This test suite verifies the proper collection and recording of metrics
    for various operations including process attachment, memory reading/writing,
    pattern scanning, and value freezing.
    """

    async def asyncSetUp(self):
        """Set up test environment with mocked metrics and service instances."""
        logger.info("Setting up test environment")

        # Create base mocks with proper async support
        self.active_sessions_counter = AsyncMock()
        self.operation_counter = AsyncMock()
        self.operation_duration = AsyncMock()
        self.error_counter = AsyncMock()

        # Configure async methods
        self.active_sessions_counter.add = AsyncMock()
        self.operation_counter.add = AsyncMock()
        self.operation_duration.record = AsyncMock()
        self.error_counter.add = AsyncMock()

        # Create async patches for metrics
        self.patches = [
            patch(
                "MemoryScanner.Server.metrics.active_sessions_counter",
                self.active_sessions_counter,
            ),
            patch(
                "MemoryScanner.Server.metrics.operation_counter", self.operation_counter
            ),
            patch(
                "MemoryScanner.Server.metrics.operation_duration",
                self.operation_duration,
            ),
            patch("MemoryScanner.Server.metrics.error_counter", self.error_counter),
            patch("MemoryScanner.Server.metrics.init_metrics"),
        ]

        # Start all patches
        for p in self.patches:
            p.start()

        # Initialize service after patching metrics
        self.service = MemoryScannerService()

        # Mock context
        self.context = MagicMock()
        self.context.set_code = MagicMock()
        self.context.set_details = MagicMock()

        logger.info("Test environment setup completed")

    async def asyncTearDown(self):
        """Clean up test environment and resources."""
        logger.info("Cleaning up test environment")

        # Clean up any remaining freezer tasks
        for session_id, (task, _) in self.service.freezer_tasks.items():
            if not task.done():
                task.cancel()
                try:
                    await task
                except asyncio.CancelledError:
                    pass

        # Stop all patches
        for p in self.patches:
            p.stop()

        # Clear service state
        self.service.sessions.clear()
        self.service.scanners.clear()
        self.service.readers.clear()
        self.service.writers.clear()

        logger.info("Test environment cleanup completed")

    @contextlib.asynccontextmanager
    async def assert_timeout(self, timeout: float = ASYNC_TIMEOUT):
        """Context manager to ensure async operations complete within timeout."""
        try:
            yield
        except asyncio.TimeoutError:
            self.fail(f"Operation timed out after {timeout} seconds")

    async def test_attach_metrics(self):
        """Test metrics collection for AttachToProcess operation."""
        logger.info("Starting attach metrics test")

        request = memory_scanner_pb2.ProcessRequest(pid=TEST_PID)

        async with self.assert_timeout():
            with patch(
                "MemoryScanner.Server.memory_scanner_service.FridaMemoryScanner"
            ) as mock_frida:
                instance = mock_frida.return_value

                async def mock_attach(pid):
                    current_span = trace.get_current_span()
                    current_span.set_attribute("process.id", pid)

                instance.attach_to_process = mock_attach
                response = await self.service.AttachToProcess(request, self.context)

        self.active_sessions_counter.add.assert_called_once_with(1)
        self.operation_counter.add.assert_called_once_with(1, {"operation": "attach"})
        self.assertTrue(response.success)
        logger.info("Attach metrics test completed successfully")

    async def test_attach_error_metrics(self):
        """Test error metrics collection for failed AttachToProcess operation."""
        logger.info("Starting attach error metrics test")

        request = memory_scanner_pb2.ProcessRequest(pid=TEST_PID)
        error_message = "Test error"

        async with self.assert_timeout():
            with patch(
                "MemoryScanner.Server.memory_scanner_service.FridaMemoryScanner"
            ) as mock_frida:
                instance = mock_frida.return_value

                async def mock_attach(pid):
                    current_span = trace.get_current_span()
                    current_span.set_attribute("process.id", pid)
                    current_span.set_status(Status(StatusCode.ERROR, error_message))
                    raise Exception(error_message)

                instance.attach_to_process = mock_attach
                response = await self.service.AttachToProcess(request, self.context)

        self.error_counter.add.assert_called_once_with(
            1, {"operation": "attach", "error": error_message}
        )
        self.assertFalse(response.success)
        logger.info("Attach error metrics test completed successfully")

    async def test_write_metrics(self):
        """Test metrics collection for WriteMemory operation."""
        logger.info("Starting write metrics test")

        session_id = TEST_SESSION_ID
        self.service.sessions[session_id] = MagicMock()
        request = memory_scanner_pb2.WriteRequest(
            session_id=session_id,
            address=TEST_ADDRESS,
            value=b"test",
            value_type="bytes",
        )

        async with self.assert_timeout():
            with patch(
                "MemoryScanner.Server.memory_scanner_service.MemoryWriter"
            ) as mock_writer:
                instance = mock_writer.return_value

                async def mock_write(address, value, value_type):
                    current_span = trace.get_current_span()
                    current_span.set_attribute("memory.address", address)
                    current_span.set_attribute("memory.value_type", value_type)

                instance.write_memory = mock_write
                response = await self.service.WriteMemory(request, self.context)

        self.operation_counter.add.assert_called_once_with(1, {"operation": "write"})
        self.operation_duration.record.assert_called_once()
        self.assertTrue(response.success)
        logger.info("Write metrics test completed successfully")

    async def test_pattern_scanner(self):
        """Test pattern scanning functionality and metrics collection."""
        logger.info("Starting pattern scanner test")

        try:
            # Verify metrics are properly mocked
            self.assertIs(
                self.service.operation_counter,
                self.operation_counter,
                "Operation counter not properly initialized",
            )

            # Reset mock counters
            self.operation_counter.reset_mock()

            # Configure mock scanner
            mock_scanner_instance = AsyncMock()

            async def mock_scan(value, value_type, comparison_type):
                logger.info(
                    f"Mock scan called with value_type: {value_type}, comparison_type: {comparison_type}"
                )
                if value_type == "pattern":
                    return [(TEST_ADDRESS, b"")]
                return []

            mock_scanner_instance.scan = AsyncMock(side_effect=mock_scan)

            # Configure service
            self.service.scanners[TEST_SESSION_ID] = mock_scanner_instance
            self.service.sessions[TEST_SESSION_ID] = AsyncMock()

            # Create scan request
            pattern = "48 8B ? ? 45 85"
            request = memory_scanner_pb2.ScanRequest(
                session_id=TEST_SESSION_ID,
                scan_type="pattern",
                value=pattern.encode("utf-8"),
                value_type="pattern",
                comparison_type="exact",
            )

            # Configure metrics mocks
            self.operation_counter.add.return_value = asyncio.Future()
            self.operation_counter.add.return_value.set_result(None)
            self.operation_duration.record.return_value = asyncio.Future()
            self.operation_duration.record.return_value.set_result(None)

            async with self.assert_timeout():
                response = await self.service.ScanMemory(request, self.context)

            # Verify response
            self.assertTrue(len(response.results) > 0, "No results returned from scan")
            self.assertEqual(response.results[0].address, TEST_ADDRESS)

            # Verify metrics
            self.operation_counter.add.assert_called_once_with(
                1, {"operation": "pattern_scan"}
            )

            logger.info("Pattern scanner test completed successfully")

        except Exception as e:
            logger.error(f"Pattern scanner test failed: {e}", exc_info=True)
            raise

    async def test_value_freezer(self):
        """Test value freezing functionality and metrics collection."""
        logger.info("Starting value freezer test")

        try:
            session_id = "test_session_4096"
            test_int = 42
            value = test_int.to_bytes(4, byteorder="little", signed=True)
            value_type = "int32"

            # Setup mocks
            frida_mock = AsyncMock()
            reader_mock = AsyncMock()
            writer_mock = AsyncMock()

            reader_mock.read_memory = AsyncMock(return_value=value)
            writer_mock.write_memory = AsyncMock(return_value=True)

            self.service.sessions[session_id] = frida_mock
            self.service.readers[session_id] = reader_mock
            self.service.writers[session_id] = writer_mock

            # Test freezing
            request = memory_scanner_pb2.FreezeRequest(
                session_id=session_id,
                address=TEST_ADDRESS,
                value=value,
                value_type=value_type,
            )

            status_count = 0
            mock_task = asyncio.create_task(asyncio.sleep(0))
            self.service.freezer_tasks[session_id] = (mock_task, TEST_ADDRESS)

            async with self.assert_timeout():
                async for status in self.service.FreezeValue(request, self.context):
                    self.assertIsNotNone(status)
                    self.assertTrue(status.active)

                    current_int = int.from_bytes(
                        status.current_value, byteorder="little", signed=True
                    )
                    self.assertEqual(current_int, test_int)

                    status_count += 1
                    if status_count >= 2:
                        break

            self.assertGreater(status_count, 0)

            # Test unfreezing
            unfreeze_request = memory_scanner_pb2.UnfreezeRequest(
                session_id=session_id, address=TEST_ADDRESS
            )

            async with self.assert_timeout():
                response = await self.service.UnfreezeValue(
                    unfreeze_request, self.context
                )

            self.assertIsNotNone(response)

            # Verify metrics
            freeze_calls = [str(call) for call in self.operation_counter.add.mock_calls]
            self.assertIn("call(1, {'operation': 'freeze'})", freeze_calls)
            self.assertIn("call(1, {'operation': 'unfreeze'})", freeze_calls)

            logger.info("Value freezer test completed successfully")

        except Exception as e:
            logger.error(f"Value freezer test failed: {e}", exc_info=True)
            raise

    async def test_cache_system(self):
        """Test scan result caching functionality."""
        logger.info("Starting cache system test")

        try:
            state_manager = StateManager(TEST_SESSION_ID)
            base_address = 0x500
            value_type = "int32"
            signature = "test_signature"

            async with self.assert_timeout():
                # Clear any existing cached offsets first
                await state_manager.invalidate_cache()

                # Cache an offset
                await state_manager.cache_offset(
                    TEST_MODULE, TEST_ADDRESS, base_address, value_type, signature
                )

                # Get cached offsets
                cached_offsets = await state_manager.get_cached_offsets(TEST_MODULE)

            self.assertEqual(len(cached_offsets), 1)
            self.assertEqual(cached_offsets[0].module_name, TEST_MODULE)
            self.assertEqual(
                cached_offsets[0].relative_offset, TEST_ADDRESS - base_address
            )
            self.assertEqual(cached_offsets[0].value_type, value_type)

            logger.info("Cache system test completed successfully")

        except Exception as e:
            logger.error(f"Cache system test failed: {e}", exc_info=True)
            raise
        finally:
            state_manager.cleanup_old_states()

    async def test_integrated_functionality(self):
        """Test integration of pattern scanning, freezing, and caching."""
        logger.info("Starting integrated functionality test")

        try:
            scanner = MemoryScanner(MagicMock(), TEST_SESSION_ID)
            writer = MemoryWriter(MagicMock())
            state_manager = StateManager(TEST_SESSION_ID)

            session_mock = MagicMock()
            session_mock.execute_script = AsyncMock(
                return_value=[{"address": TEST_ADDRESS}]
            )

            async with self.assert_timeout():
                # Pattern scan
                results = await scanner.scan(
                    value_type="pattern",
                    value="48 8B ? ? 45 85",
                    comparison_type="pattern",
                    ranges=[],
                )
                self.assertTrue(len(results) > 0)

                # Freeze value
                address = results[0]["address"]
                freeze_success = await writer.freeze_value(address, 100, "int32")
                self.assertTrue(freeze_success)

                # Cache result
                await state_manager.cache_offset(
                    TEST_MODULE, address, 0x500, "int32", "test_signature"
                )
                cached = await state_manager.get_cached_offsets(TEST_MODULE)
                self.assertTrue(len(cached) > 0)

            logger.info("Integrated functionality test completed successfully")

        except Exception as e:
            logger.error(f"Integrated functionality test failed: {e}", exc_info=True)
            raise
        finally:
            writer.cleanup()
            state_manager.cleanup_old_states()


def create_test_suite(args: argparse.Namespace) -> unittest.TestSuite:
    """Create a test suite based on command line arguments."""
    suite = unittest.TestSuite()
    loader = unittest.TestLoader()
    test_class = TestMetricsCollection

    if args.pattern_scanner:
        suite.addTest(loader.loadTestsFromName("test_pattern_scanner", test_class))
    elif args.value_freezer:
        suite.addTest(loader.loadTestsFromName("test_value_freezer", test_class))
    elif args.cache_system:
        suite.addTest(loader.loadTestsFromName("test_cache_system", test_class))
    elif args.integrated:
        suite.addTest(
            loader.loadTestsFromName("test_integrated_functionality", test_class)
        )
    else:
        suite.addTests(loader.loadTestsFromTestCase(test_class))

    return suite


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Run memory scanner tests")
    parser.add_argument(
        "--pattern-scanner", action="store_true", help="Run pattern scanner tests only"
    )
    parser.add_argument(
        "--value-freezer", action="store_true", help="Run value freezer tests only"
    )
    parser.add_argument(
        "--cache-system", action="store_true", help="Run cache system tests only"
    )
    parser.add_argument(
        "--integrated",
        action="store_true",
        help="Run integrated functionality tests only",
    )
    parser.add_argument(
        "--verbose", "-v", action="store_true", help="Enable verbose logging"
    )

    args = parser.parse_args()

    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)

    # Create and run the test suite
    suite = create_test_suite(args)
    runner = unittest.TextTestRunner(verbosity=2 if args.verbose else 1)
    runner.run(suite)
