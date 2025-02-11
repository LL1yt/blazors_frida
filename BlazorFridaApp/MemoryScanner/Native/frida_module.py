from attacher import FridaAttacher
from reader import read_memory
from writer import write_memory
from scanner import scan_memory, MemoryScanner, SCAN_SCRIPT
from process_list import get_process_list
import logging
from typing import List, Tuple, Dict, Any
from base64 import b64encode
import traceback
import datetime


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
        """Scan a specific memory range for a value."""
        try:
            timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S,%f")[:-3]
            self._logger.debug(f"[{timestamp}] Starting scan_memory_range")
            self._logger.debug(f"[{timestamp}] Input parameters:")
            self._logger.debug(
                f"[{timestamp}] - value_type: {value_type} (type: {type(value_type)})"
            )
            self._logger.debug(f"[{timestamp}] - value: {value} (type: {type(value)})")
            self._logger.debug(f"[{timestamp}] - comparison_type: {comparison_type}")
            self._logger.debug(f"[{timestamp}] - ranges: {ranges}")

            if not self.is_initialized:
                self._logger.error(f"[{timestamp}] Scanner not initialized")
                raise RuntimeError("Scanner not initialized")

            valid_types = [
                "int8",
                "uint8",
                "int16",
                "uint16",
                "int32",
                "uint32",
                "int64",
                "uint64",
                "float",
                "double",
                "bytes",
                "any",
                "*",  # Wildcard type
            ]

            self._logger.debug(f"[{timestamp}] Valid types: {valid_types}")
            self._logger.debug(
                f"[{timestamp}] Checking if {value_type!r} in valid_types"
            )
            self._logger.debug(
                f"[{timestamp}] Type comparison result: {value_type in valid_types}"
            )

            if value_type not in valid_types:
                self._logger.error(f"[{timestamp}] Invalid value_type: {value_type!r}")
                self._logger.error(f"[{timestamp}] Value type validation failed")
                self._logger.error(
                    f"[{timestamp}] Stack trace:\n{traceback.format_stack()}"
                )
                raise ValueError(
                    f"Invalid value type: {value_type}. Supported types: {valid_types}"
                )

            def make_serializable(v):
                """Convert value to JSON serializable format"""
                try:
                    self._logger.debug(
                        f"make_serializable input: type={type(v)}, value={v}"
                    )

                    if isinstance(v, bytes):
                        # For pattern type, convert bytes to hex string
                        if value_type == "pattern":
                            hex_str = "".join(f"{b:02x}" for b in v)
                            self._logger.debug(
                                f"Converting pattern bytes to hex: {hex_str}"
                            )
                            return hex_str
                        # For other types, use base64
                        encoded_value = b64encode(v).decode("utf-8")
                        self._logger.debug(
                            f"Encoding bytes value to base64: {encoded_value[:50]}..."
                        )
                        return {
                            "type": "bytes",
                            "encoding": "base64",
                            "data": encoded_value,
                        }

                    if isinstance(v, (list, tuple)):
                        result = [make_serializable(x) for x in v]
                        self._logger.debug(f"Converted sequence: {result}")
                        return result

                    if isinstance(v, dict):
                        result = {
                            str(k): make_serializable(val) for k, val in v.items()
                        }
                        self._logger.debug(f"Converted dict: {result}")
                        return result

                    if isinstance(v, (int, float, str, bool, type(None))):
                        return v

                    # Handle any other types by converting to string
                    self._logger.warning(f"Converting unknown type {type(v)} to string")
                    return str(v)

                except Exception as e:
                    self._logger.error(
                        f"Serialization error for value type {type(v)}: {str(e)}"
                    )
                    self._logger.debug(f"Value content: {v}")
                    raise

            scan_value = make_serializable(value)

            script = self._attacher.session.create_script(SCAN_SCRIPT)
            self._logger.debug("Created Frida script")

            script.on(
                "message",
                lambda message, data: self._logger.debug(
                    f"Script message: {message}, data: {data}"
                ),
            )
            self._logger.debug("Registered message handler")

            script.load()
            self._logger.debug("Script loaded successfully")

            # Log available exports
            self._logger.debug(f"Available script exports: {dir(script.exports)}")

            results = []
            for start, end in ranges:
                try:
                    self._logger.debug(f"Processing range {hex(start)}-{hex(end)}")

                    # Prepare scan value based on type
                    if isinstance(value, bytes):
                        scan_value = make_serializable(value)
                    elif isinstance(value, (int, float, str, bool)):
                        scan_value = value
                    else:
                        scan_value = str(value)

                    self._logger.debug(
                        f"Prepared scan value: {scan_value} (type: {type(scan_value)})"
                    )

                    # Call scan_memory with positional parameters in the order defined in the script
                    if not hasattr(script.exports, "scanMemory"):
                        available_methods = dir(script.exports)
                        self._logger.error(
                            f"scanMemory method not found. Available methods: {available_methods}"
                        )
                        raise RuntimeError(
                            f"Frida script missing required method: scanMemory. Available methods: {available_methods}"
                        )

                    self._logger.debug("Calling scanMemory with parameters:")
                    self._logger.debug(f"  valueType: {value_type}")
                    self._logger.debug(f"  value: {scan_value}")
                    self._logger.debug(f"  startAddress: 0x{start:x}")
                    self._logger.debug(f"  endAddress: 0x{end:x}")
                    self._logger.debug(f"  comparisonType: {comparison_type}")

                    matches = script.exports.scanMemory(
                        value_type, scan_value, start, end, comparison_type
                    )

                    self._logger.debug(
                        f"Scan completed, found {len(matches) if matches else 0} matches"
                    )

                    # Process results
                    if matches:
                        results.extend(
                            [
                                {"address": int(match), "value": scan_value}
                                for match in matches
                            ]
                        )

                except TypeError as e:
                    self._logger.error(
                        f"Type error during scan: {str(e)}\n"
                        f"Value type: {value_type} (type: {type(value_type)})\n"
                        f"Value: {value} (type: {type(value)})\n"
                        f"Scan value: {scan_value if 'scan_value' in locals() else 'not prepared'}"
                    )
                    raise
                except Exception as e:
                    self._logger.error(
                        f"Failed to scan range {hex(start)}-{hex(end)}: {str(e)}"
                    )
                    self._logger.debug(
                        f"Failed value details - Type: {type(value)}, Content: {value}"
                    )
                    raise

            return results
        except Exception as e:
            self._logger.error(f"Error scanning memory range: {str(e)}", exc_info=True)
            raise
