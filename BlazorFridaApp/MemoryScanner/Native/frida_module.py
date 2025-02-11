from attacher import FridaAttacher
from reader import read_memory
from writer import write_memory
from scanner import scan_memory, MemoryScanner, SCAN_SCRIPT
from process_list import get_process_list
import logging
from typing import List, Tuple, Dict, Any


class FridaMemoryScanner:
    def __init__(self):
        self._attacher = FridaAttacher()
        self.scanner = None
        self._initialized = False
        self._logger = logging.getLogger(__name__)

    async def attach_to_process(self, pid: int) -> bool:
        """Attach to a process and initialize scanner."""
        try:
            success = await self._attacher.attach_to_process(pid)
            if not success:
                self._logger.error(f"Failed to attach to process {pid}")
                return False

            self._logger.info(f"Successfully attached to process {pid}")
            self.scanner = MemoryScanner(self, str(pid))
            self._initialized = True
            return True
        except Exception as e:
            self._logger.error(f"Error attaching to process {pid}: {e}", exc_info=e)
            return False

    @property
    def is_initialized(self) -> bool:
        """Check if scanner is properly initialized."""
        return self._initialized and self.scanner is not None

    async def detach_from_process(self) -> bool:
        """Detach from the currently attached process."""
        try:
            if self._attacher.session:
                success = await self._attacher.detach()
                if success:
                    self.scanner = None
                    self._initialized = False
                return success
            return True
        except Exception as e:
            self._logger.error(f"Failed to detach from process: {e}", exc_info=e)
            return False

    async def read_memory(self, address, size):
        """Read memory at the specified address."""
        if not self._attacher.session:
            raise Exception("Not attached to any process")
        try:
            return await read_memory(self._attacher.session, address, size)
        except Exception as e:
            raise Exception(f"Failed to read memory at {address}: {str(e)}")

    async def write_memory(self, address, value, value_type="bytes"):
        """Write memory at the specified address.

        Args:
            address: Memory address to write to
            value: Value to write
            value_type: Type of value ('int8', 'uint8', etc.)
        """
        if not self._attacher.session:
            raise Exception("Not attached to any process")
        try:
            return await write_memory(
                self._attacher.session, address, value, value_type
            )
        except Exception as e:
            raise Exception(f"Failed to write memory at {address}: {str(e)}")

    async def scan_memory(self, value_type, value):
        """Scan memory for the specified value."""
        if not self._attacher.session:
            raise Exception("Not attached to any process")
        try:
            return await scan_memory(self._attacher.session, value_type, value)
        except Exception as e:
            raise Exception(f"Failed to scan memory: {str(e)}")

    def get_process_list(self):
        """Get list of running processes."""
        try:
            return get_process_list()
        except Exception as e:
            raise Exception(f"Failed to get process list: {str(e)}")

    async def scan(self, value, value_type: str, comparison_type: str):
        """
        Scan memory with the given parameters.
        Args:
            value: The value to search for
            value_type: The type of value being searched
            comparison_type: The type of comparison to perform
        Returns:
            List of results
        """
        if not self.is_initialized:
            raise RuntimeError("Scanner not initialized")
        return await self.scanner.scan(value, value_type, comparison_type)

    async def scan_pattern(self, pattern: bytes, mask: str) -> List[int]:
        """Scan memory for a byte pattern with mask.

        Args:
            pattern: Bytes to search for
            mask: Mask string where 'x' means match exact byte and '?' means wildcard

        Returns:
            List of memory addresses where the pattern was found
        """
        if not self.scanner:
            raise RuntimeError("Scanner not initialized. Call attach_to_process first.")
        return await self.scanner.scan_pattern(pattern, mask)

    async def scan_memory_range(
        self,
        value_type: str,
        value: Any,
        comparison_type: str,
        ranges: List[Tuple[int, int]],
    ) -> List[Dict[str, Any]]:
        """
        Scan a specific memory range for a value
        Args:
            value_type: Type of value to scan for
            value: The value to search for
            comparison_type: Type of comparison to perform
            ranges: List of (start, end) address tuples to scan
        Returns:
            List of results containing addresses and values
        """
        if not self._attacher.session:
            raise RuntimeError("Not attached to any process")

        def make_serializable(v):
            """Convert value to JSON serializable format"""
            if isinstance(v, bytes):
                return v.hex()
            if isinstance(v, (list, tuple)):
                return [make_serializable(x) for x in v]
            if isinstance(v, dict):
                return {k: make_serializable(v) for k, v in v.items()}
            return v

        try:
            script = self._attacher.session.create_script(SCAN_SCRIPT)
            script.load()

            results = []
            for start, end in ranges:
                # Convert values to JSON serializable format
                json_value = make_serializable(value)

                matches = script.exports.scan_memory(
                    value_type, json_value, start, end, comparison_type
                )
                results.extend(
                    [{"address": match, "value": json_value} for match in matches]
                )

            return results
        except Exception as e:
            self._logger.error(f"Error scanning memory range: {e}")
            raise
