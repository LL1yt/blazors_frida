from attacher import FridaAttacher
from reader import read_memory
from writer import write_memory
from scanner import scan_memory
from process_list import get_process_list


class FridaMemoryScanner:
    def __init__(self):
        self.attacher = FridaAttacher()

    async def attach_to_process(self, process_id):
        """Attach to a process by its PID."""
        try:
            return await self.attacher.attach_to_process(process_id)
        except Exception as e:
            raise Exception(f"Failed to attach to process {process_id}: {str(e)}")

    async def detach_from_process(self):
        """Detach from the currently attached process."""
        try:
            if self.attacher.session:
                return await self.attacher.detach()
            return True
        except Exception as e:
            raise Exception(f"Failed to detach from process: {str(e)}")

    async def read_memory(self, address, size):
        """Read memory at the specified address."""
        if not self.attacher.session:
            raise Exception("Not attached to any process")
        try:
            return await read_memory(self.attacher.session, address, size)
        except Exception as e:
            raise Exception(f"Failed to read memory at {address}: {str(e)}")

    async def write_memory(self, address, value, value_type="bytes"):
        """Write memory at the specified address.

        Args:
            address: Memory address to write to
            value: Value to write
            value_type: Type of value ('int8', 'uint8', etc.)
        """
        if not self.attacher.session:
            raise Exception("Not attached to any process")
        try:
            return await write_memory(self.attacher.session, address, value, value_type)
        except Exception as e:
            raise Exception(f"Failed to write memory at {address}: {str(e)}")

    async def scan_memory(self, value_type, value):
        """Scan memory for the specified value."""
        if not self.attacher.session:
            raise Exception("Not attached to any process")
        try:
            return await scan_memory(self.attacher.session, value_type, value)
        except Exception as e:
            raise Exception(f"Failed to scan memory: {str(e)}")

    def get_process_list(self):
        """Get list of running processes."""
        try:
            return get_process_list()
        except Exception as e:
            raise Exception(f"Failed to get process list: {str(e)}")
