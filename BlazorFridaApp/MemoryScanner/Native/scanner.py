from executor import execute_script
from typing import List, Tuple, Optional, Dict, Any, Union
from state_manager import StateManager
from dataclasses import asdict
import logging
import re
from enum import Enum
import gc
import weakref
import psutil
from threading import Lock

logger = logging.getLogger(__name__)

# Global scanner instance cache
_scanner_instances = {}
_scanner_lock = Lock()


def get_or_create_scanner(session_id: str, frida_scanner) -> "MemoryScanner":
    with _scanner_lock:
        if (
            session_id not in _scanner_instances
            or _scanner_instances[session_id]() is None
        ):
            scanner = MemoryScanner(frida_scanner, session_id)
            _scanner_instances[session_id] = weakref.ref(
                scanner, lambda _: _scanner_instances.pop(session_id, None)
            )
            return scanner
        return _scanner_instances[session_id]()


class PatternType(Enum):
    EXACT = "exact"  # Exact value match
    PATTERN = "pattern"  # Byte pattern with wildcards
    REGEX = "regex"  # Regular expression pattern


def optimize_pattern(pattern: str) -> str:
    """
    Optimize byte pattern for faster scanning
    Args:
        pattern: Byte pattern string (e.g. "48 8B ? ? 45 85")
    Returns:
        Optimized pattern string
    """
    # Remove whitespace and validate
    pattern = re.sub(r"\s+", "", pattern)
    if not re.match(r"^[0-9A-Fa-f\?]+$", pattern):
        raise ValueError("Invalid pattern format")

    # Convert to bytes with wildcards
    return pattern


async def scan_memory(session, value_type: str, value: Any) -> List[str]:
    """
    Scan process memory for a specific value
    Args:
        session: Frida session object
        value_type: Type of value to scan for ('string', 'number', 'pattern')
        value: The actual value to search for (can be bytes, str, or any other type)
    Returns:
        List of memory addresses where the value was found
    """
    try:
        scanner = get_or_create_scanner(session.session_id, session)
        # Get all readable memory ranges
        ranges = await scanner.frida_scanner.enumerate_ranges("r--")

        # Convert ranges to list of tuples
        range_tuples = [
            (int(r.base_address, 16), int(r.base_address, 16) + r.size) for r in ranges
        ]

        # Perform the scan
        results = await scanner.scan(value_type, value, "exact", range_tuples)
        return [str(result["address"]) for result in results]

    except Exception as e:
        logger.error(f"Error during memory scan: {str(e)}")
        raise


SCAN_SCRIPT = """
rpc.exports = {
    scanMemory: function(valueType, value) {
        const matches = [];
        
        // Helper to check if memory range should be scanned
        function shouldScanRange(range) {
            // Skip if not readable
            if (!(range.protection.indexOf('r') !== -1)) return false;
            
            // Skip certain module ranges
            const skipModules = ['kernel32.dll', 'ntdll.dll'];
            if (range.file && skipModules.some(m => range.file.name.includes(m))) {
                return false;
            }
            
            return true;
        }
        
        // Convert pattern to byte array with wildcards
        function patternToBytes(pattern) {
            const bytes = [];
            for (let i = 0; i < pattern.length; i += 2) {
                const byte = pattern.substr(i, 2);
                bytes.push(byte === '??' ? null : parseInt(byte, 16));
            }
            return bytes;
        }
        
        // Pattern matching function
        function matchPattern(data, pattern) {
            for (let i = 0; i < pattern.length; i++) {
                if (pattern[i] !== null && data[i] !== pattern[i]) {
                    return false;
                }
            }
            return true;
        }
        
        // Scan memory ranges
        Process.enumerateRanges('r--').forEach(range => {
            if (!shouldScanRange(range)) return;
            
            try {
                if (valueType === 'pattern') {
                    const pattern = patternToBytes(value);
                    const data = range.readByteArray(range.size);
                    
                    for (let offset = 0; offset < data.byteLength - pattern.length; offset++) {
                        const slice = new Uint8Array(data, offset, pattern.length);
                        if (matchPattern(slice, pattern)) {
                            matches.push(range.base.add(offset));
                        }
                    }
                } else {
                    // Standard value scanning
                    const pattern = valueType === 'string' ? 
                        Memory.scanSync(range.base, range.size, value) :
                        Memory.scanSync(range.base, range.size, Array.from(new Uint8Array(new Float64Array([value]).buffer)));
                    pattern.forEach(match => {
                        matches.push(match.address.toString());
                    });
                }
            } catch (e) {
                // Ignore read errors and continue
                console.log('Error scanning range:', e);
            }
        });
        
        return matches.map(ptr => ptr.toString());
    }
};
"""


class MemoryScanner:
    def __init__(self, frida_scanner, session_id: str):
        self.frida_scanner = frida_scanner
        self._session_id = session_id
        self._logger = logging.getLogger(__name__)
        self._scan_results = weakref.WeakValueDictionary()
        self._memory_lock = Lock()
        self._max_chunk_size = 1024 * 1024  # 1MB chunks
        self._process = psutil.Process()
        self._mem_threshold = 0.8  # 80% memory threshold
        self.state_manager = StateManager(session_id)
        logger.info(f"MemoryScanner initialized for session {session_id}")

    def _check_memory_usage(self):
        """Monitor memory usage and trigger cleanup if needed"""
        with self._memory_lock:
            mem_percent = self._process.memory_percent()
            if mem_percent > self._mem_threshold:
                self._logger.warning(
                    f"Memory usage high ({mem_percent:.1f}%), triggering cleanup"
                )
                self.cleanup(force=True)
                gc.collect()

    async def scan(
        self,
        value_type: str,
        value: Any,
        comparison_type: str,
        ranges: List[Tuple[int, int]],
    ) -> List[Dict[str, Any]]:
        """Perform memory scan with memory usage monitoring"""
        self._check_memory_usage()

        # Break large ranges into chunks
        chunked_ranges = []
        for start, end in ranges:
            size = end - start
            if size > self._max_chunk_size:
                chunks = range(start, end, self._max_chunk_size)
                chunked_ranges.extend(
                    (chunk, min(chunk + self._max_chunk_size, end)) for chunk in chunks
                )
            else:
                chunked_ranges.append((start, end))

        results = []
        for chunk_start, chunk_end in chunked_ranges:
            chunk_results = await self.frida_scanner.scan_memory_range(
                value_type, value, comparison_type, [(chunk_start, chunk_end)]
            )
            results.extend(chunk_results)
            self._check_memory_usage()

        # Store results with weak reference
        result_key = f"{value_type}_{hash(str(value))}_{comparison_type}"
        self._scan_results[result_key] = results

        return results

    async def get_state(self, checkpoint_id: Optional[str] = None) -> Dict[str, Any]:
        """Get the current scanner state or state at a specific checkpoint"""
        self._check_memory_usage()
        try:
            state = await self.state_manager.load_state()
            if not state:
                logger.warning("No state found")
                return {}

            if checkpoint_id:
                checkpoint_state = await self.state_manager.restore_checkpoint(
                    checkpoint_id
                )
                if checkpoint_state:
                    return asdict(checkpoint_state)
                logger.warning(f"Checkpoint {checkpoint_id} not found")
                return {}

            return asdict(state)
        except Exception as e:
            logger.error(f"Failed to get state: {e}")
            return {}

    async def update_state(self, state_updates: Dict[str, Any]) -> None:
        """Update the scanner state with the provided updates"""
        self._check_memory_usage()
        try:
            current_state = await self.state_manager.load_state()
            if current_state:
                metadata = current_state.metadata
                metadata.update(state_updates)
                await self.state_manager.save_state(
                    current_state.checkpoint_id, current_state.scan_results, metadata
                )
                logger.info("State updated successfully")
            else:
                logger.warning("No state to update")
        except Exception as e:
            logger.error(f"Failed to update state: {e}")
            raise

    def cleanup(self, force: bool = False):
        """Cleanup old state files and memory"""
        with self._memory_lock:
            try:
                # Clear scan results cache
                self._scan_results.clear()

                if force:
                    # Force garbage collection
                    gc.collect(2)

                    # Release memory back to OS if possible
                    import ctypes

                    if hasattr(ctypes, "windll"):
                        ctypes.windll.psapi.EmptyWorkingSet(-1)

                self._logger.info(
                    f"Cleanup completed. Current memory usage: {self._process.memory_percent():.1f}%"
                )
            except Exception as e:
                self._logger.error(f"Error during cleanup: {e}")

    def __del__(self):
        """Cleanup when object is destroyed"""
        self.cleanup(force=True)

    async def scan_pattern(self, pattern: bytes, mask: str) -> List[int]:
        """
        Scan memory for a byte pattern with mask
        Args:
            pattern: Bytes to search for
            mask: Mask string where 'x' means match exact byte, '?' means wildcard
        Returns:
            List of memory addresses where pattern was found
        """
        try:
            # Convert pattern and mask to the format expected by scan_memory
            pattern_str = ""
            for b, m in zip(pattern, mask):
                if m == "x":
                    pattern_str += f"{b:02X}"
                else:
                    pattern_str += "??"

            # Use the existing scan_memory function with pattern type
            results = await scan_memory(
                self.frida_scanner.session, "pattern", pattern_str
            )

            # Convert results to integers
            return [
                int(addr, 16) if isinstance(addr, str) else int(addr)
                for addr in results
            ]
        except Exception as e:
            self._logger.error(f"Pattern scan failed: {e}")
            raise
