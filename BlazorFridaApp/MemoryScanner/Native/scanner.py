from executor import execute_script
from typing import List, Tuple, Optional, Dict, Any, Union
from state_manager import StateManager
from dataclasses import asdict
import logging

logger = logging.getLogger(__name__)


async def scan_memory(session, value_type: str, value: Any) -> List[str]:
    """
    Scan process memory for a specific value
    Args:
        session: Frida session object
        value_type: Type of value to scan for ('string', 'number', etc)
        value: The actual value to search for
    Returns:
        List of memory addresses where the value was found
    """
    try:
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
        Process.enumerateRanges('r--').forEach(range => {
            try {
                const pattern = valueType === 'string' ? 
                    Memory.scanSync(range.base, range.size, value) :
                    Memory.scanSync(range.base, range.size, Array.from(new Uint8Array(new Float64Array([value]).buffer)));
                pattern.forEach(match => {
                    matches.push(match.address.toString());
                });
            } catch(e) {
                // Skip invalid memory regions
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

        # Execute the Frida script to scan memory
        addresses = await execute_script(
            self.frida_scanner.session, SCAN_SCRIPT, "scanMemory", value_type, value
        )

        # Convert addresses to scan results
        results = []
        for addr in addresses:
            if isinstance(addr, str):
                addr = int(addr, 16)  # Convert hex string to int
            results.append({"address": addr, "value": value})

        # Create checkpoint and save state
        metadata = {
            "value_type": value_type,
            "comparison_type": comparison_type,
            "ranges": ranges,
        }
        checkpoint_id = await self.state_manager.create_checkpoint(results, metadata)
        logger.info(f"Created checkpoint {checkpoint_id} for scan results")

        return results

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
