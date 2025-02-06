from executor import execute_script
from typing import List, Tuple, Optional, Dict, Any, Union
from state_manager import StateManager
from dataclasses import asdict
import logging
import re
from enum import Enum

logger = logging.getLogger(__name__)


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
        if value_type == "pattern":
            if isinstance(value, bytes):
                value = value.decode('utf-8')  # Convert bytes to string for pattern optimization
            value = optimize_pattern(value)
        elif isinstance(value, bytes) and value_type != "bytes":
            # If we got bytes but it's not meant to be raw bytes, decode it
            value = value.decode('utf-8')
            
        return await execute_script(
            session, SCAN_SCRIPT, "scanMemory", value_type, value
        )
    except Exception as e:
        logger.error(f"Memory scan failed: {e}")
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
        
        Process.enumerateRanges('r--').forEach(range => {
            if (!shouldScanRange(range)) return;
            
            try {
                if (valueType === 'pattern') {
                    // Pattern scanning
                    const patternBytes = patternToBytes(value);
                    const data = Memory.readByteArray(range.base, range.size);
                    
                    for (let offset = 0; offset < data.byteLength - patternBytes.length; offset++) {
                        const slice = new Uint8Array(data, offset, patternBytes.length);
                        if (matchPattern(slice, patternBytes)) {
                            matches.push(range.base.add(offset).toString());
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
            } catch(e) {
                // Skip invalid memory regions
                console.log('Error scanning range:', e.message);
            }
        });
        return matches;
    }
};
"""


class MemoryScanner:
    def __init__(self, frida_scanner, session_id: str):
        self.frida_scanner = frida_scanner
        self.session_id = session_id
        self.state_manager = StateManager(session_id)
        logger.info(f"MemoryScanner initialized for session {session_id}")

    async def scan(
        self,
        value_type: str,
        value: Any,
        comparison_type: str,
        ranges: List[Tuple[int, int]],
    ) -> List[Dict[str, Any]]:
        """
        Perform a memory scan with the given parameters
        """
        if not self.frida_scanner:
            return []

        try:
            # Ensure value is in correct format for scanning
            scan_value = value
            if isinstance(value, bytes):
                if value_type == "pattern":
                    scan_value = value.decode('utf-8')
                elif value_type != "bytes":
                    scan_value = value.decode('utf-8')
            
            # Execute the Frida script to scan memory
            addresses = await execute_script(
                self.frida_scanner.session, SCAN_SCRIPT, "scanMemory", value_type, scan_value
            )

            # Convert addresses to scan results
            results = []
            for addr in addresses:
                if isinstance(addr, str):
                    addr = int(addr, 16)  # Convert hex string to int
                # Ensure value is in bytes format for protobuf
                if not isinstance(value, bytes):
                    if isinstance(value, str):
                        value_bytes = value.encode('utf-8')
                    else:
                        value_bytes = str(value).encode('utf-8')
                else:
                    value_bytes = value
                results.append({"address": addr, "value": value_bytes})

            # Create checkpoint and save state
            metadata = {
                "value_type": value_type,
                "comparison_type": comparison_type,
                "ranges": ranges,
            }
            checkpoint_id = await self.state_manager.create_checkpoint(
                results, metadata
            )
            logger.info(f"Created checkpoint {checkpoint_id} for scan results")

            return results
        except Exception as e:
            logger.error(f"Scan failed: {e}")
            raise

    async def get_state(self, checkpoint_id: Optional[str] = None) -> Dict[str, Any]:
        """Get the current scanner state or state at a specific checkpoint"""
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

    def cleanup(self):
        """Cleanup old state files"""
        try:
            self.state_manager.cleanup_old_states()
            logger.info("Old states cleaned up")
        except Exception as e:
            logger.error(f"Failed to cleanup states: {e}")
