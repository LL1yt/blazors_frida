from executor import execute_script
from typing import Dict, Any, List, Optional, Union
import asyncio
import logging
from dataclasses import dataclass
from datetime import datetime
import threading
from concurrent.futures import ThreadPoolExecutor
import time

logger = logging.getLogger(__name__)


@dataclass
class FrozenValue:
    address: int
    value: Any
    value_type: str
    last_update: datetime
    update_interval: float  # seconds
    enabled: bool = True


WRITE_SCRIPT = """
rpc.exports = {
    writeMemory: function(address, value, valueType) {
        try {
            const ptr = new NativePointer(address);
            
            switch(valueType) {
                case 'int8':
                    Memory.writeS8(ptr, value);
                    break;
                case 'uint8':
                    Memory.writeU8(ptr, value);
                    break;
                case 'int16':
                    Memory.writeS16(ptr, value);
                    break;
                case 'uint16':
                    Memory.writeU16(ptr, value);
                    break;
                case 'int32':
                    Memory.writeS32(ptr, value);
                    break;
                case 'uint32':
                    Memory.writeU32(ptr, value);
                    break;
                case 'int64':
                    Memory.writeS64(ptr, value);
                    break;
                case 'uint64':
                    Memory.writeU64(ptr, value);
                    break;
                case 'float':
                    Memory.writeFloat(ptr, value);
                    break;
                case 'double':
                    Memory.writeDouble(ptr, value);
                    break;
                case 'string':
                    Memory.writeUtf8String(ptr, value);
                    break;
                case 'bytes':
                    Memory.writeByteArray(ptr, value);
                    break;
                default:
                    throw new Error('Unsupported value type: ' + valueType);
            }
            return true;
        } catch(e) {
            console.log('Write error:', e);
            return false;
        }
    },
    
    writeBatch: function(operations) {
        const results = [];
        for (const op of operations) {
            try {
                const success = this.writeMemory(op.address, op.value, op.valueType);
                results.push({
                    address: op.address,
                    success: success,
                    error: success ? null : 'Write failed'
                });
            } catch(e) {
                results.push({
                    address: op.address,
                    success: false,
                    error: e.message
                });
            }
        }
        return results;
    }
};
"""


class MemoryWriter:
    def __init__(self, session):
        self.session = session
        self.frozen_values: Dict[int, FrozenValue] = {}
        self._freeze_thread = None
        self._stop_event = threading.Event()
        self._executor = ThreadPoolExecutor(max_workers=1)
        logger.info("MemoryWriter initialized")

    async def write(self, address: int, value: Any, value_type: str = "bytes") -> bool:
        """Write value to memory at the specified address.

        Args:
            address: Memory address to write to
            value: Value to write
            value_type: Type of value ('int8', 'uint8', 'int16', 'uint16', 'int32',
                       'uint32', 'int64', 'uint64', 'float', 'double', 'string', 'bytes')

        Returns:
            Boolean indicating success
        """
        try:
            result = await execute_script(
                self.session, WRITE_SCRIPT, "writeMemory", address, value, value_type
            )
            if result:
                logger.debug(f"Successfully wrote {value} to {hex(address)}")
            else:
                logger.error(f"Failed to write to {hex(address)}")
            return result
        except Exception as e:
            logger.error(f"Write error: {e}")
            return False

    async def write_batch(
        self, operations: List[Dict[str, Any]]
    ) -> List[Dict[str, Any]]:
        """Write multiple values to memory in a batch.

        Args:
            operations: List of dicts with keys: address, value, value_type

        Returns:
            List of operation results with success/error info
        """
        try:
            return await execute_script(
                self.session, WRITE_SCRIPT, "writeBatch", operations
            )
        except Exception as e:
            logger.error(f"Batch write error: {e}")
            return [{"success": False, "error": str(e)} for _ in operations]

    def freeze_value(
        self,
        address: int,
        value: Any,
        value_type: str = "bytes",
        update_interval: float = 0.1,
    ) -> bool:
        """Freeze a memory value by continuously writing it.

        Args:
            address: Memory address to freeze
            value: Value to maintain
            value_type: Type of value
            update_interval: How often to update the value (seconds)

        Returns:
            Boolean indicating if freeze was initiated
        """
        try:
            self.frozen_values[address] = FrozenValue(
                address=address,
                value=value,
                value_type=value_type,
                last_update=datetime.now(),
                update_interval=update_interval,
            )

            # Start freeze thread if not running
            if not self._freeze_thread or not self._freeze_thread.is_alive():
                self._stop_event.clear()
                self._freeze_thread = threading.Thread(
                    target=self._freeze_loop, daemon=True
                )
                self._freeze_thread.start()
                logger.info(f"Started freeze thread for {hex(address)}")

            return True
        except Exception as e:
            logger.error(f"Failed to freeze value: {e}")
            return False

    def unfreeze_value(self, address: int) -> bool:
        """Stop freezing a memory value.

        Args:
            address: Memory address to unfreeze

        Returns:
            Boolean indicating if unfreeze was successful
        """
        try:
            if address in self.frozen_values:
                del self.frozen_values[address]
                logger.info(f"Unfroze value at {hex(address)}")

                # Stop freeze thread if no more frozen values
                if not self.frozen_values:
                    self._stop_event.set()

                return True
            return False
        except Exception as e:
            logger.error(f"Failed to unfreeze value: {e}")
            return False

    def _freeze_loop(self):
        """Background thread that maintains frozen values."""
        while not self._stop_event.is_set():
            try:
                current_time = datetime.now()
                operations = []

                # Collect operations for values that need updating
                for frozen in self.frozen_values.values():
                    if not frozen.enabled:
                        continue

                    time_diff = (current_time - frozen.last_update).total_seconds()
                    if time_diff >= frozen.update_interval:
                        operations.append(
                            {
                                "address": frozen.address,
                                "value": frozen.value,
                                "value_type": frozen.value_type,
                            }
                        )
                        frozen.last_update = current_time

                # Execute batch write if needed
                if operations:
                    asyncio.run(self.write_batch(operations))

                time.sleep(0.01)  # Small sleep to prevent CPU overuse

            except Exception as e:
                logger.error(f"Error in freeze loop: {e}")
                time.sleep(1)  # Longer sleep on error

    def cleanup(self):
        """Clean up resources used by the writer."""
        try:
            self._stop_event.set()
            if self._freeze_thread and self._freeze_thread.is_alive():
                self._freeze_thread.join(timeout=1.0)
            self._executor.shutdown(wait=False)
            self.frozen_values.clear()
            logger.info("MemoryWriter cleanup completed")
        except Exception as e:
            logger.error(f"Cleanup error: {e}")
