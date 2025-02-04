from executor import execute_script
from typing import List, Tuple, Optional, Dict, Any

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
    def __init__(self, frida_scanner):
        self.frida_scanner = frida_scanner
        self.scan_history = {}
        self.current_state = {}

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
            self.frida_scanner.session, SCAN_SCRIPT, "scan_memory", value_type, value
        )

        # Convert addresses to scan results
        results = []
        for addr in addresses:
            if isinstance(addr, str):
                addr = int(addr, 16)  # Convert hex string to int
            results.append({"address": addr, "value": value})

        # Store scan results in history
        checkpoint_id = len(self.scan_history)
        self.scan_history[checkpoint_id] = results

        return results

    async def get_state(self, checkpoint_id: Optional[str] = None) -> Dict[str, Any]:
        """
        Get the current scanner state or state at a specific checkpoint
        """
        if checkpoint_id and checkpoint_id in self.scan_history:
            return {
                "checkpoint_id": checkpoint_id,
                "results": self.scan_history[checkpoint_id],
                **self.current_state,
            }
        return self.current_state

    async def update_state(self, state_updates: Dict[str, Any]) -> None:
        """
        Update the scanner state with the provided updates
        """
        self.current_state.update(state_updates)


def scan_memory(session, value_type, value):
    """
    Legacy function maintained for compatibility
    """
    if not session:
        return []
    return execute_script(session, SCAN_SCRIPT, "scan_memory", value_type, value)
