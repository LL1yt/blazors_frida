from executor import execute_script

READ_SCRIPT = """
rpc.exports = {
    readMemory: function(address, size) {
        try {
            const buf = Memory.readByteArray(ptr(address), size);
            return Array.from(new Uint8Array(buf));
        } catch(e) {
            return null;
        }
    }
};
"""


class MemoryReader:
    def __init__(self, session):
        self.session = session

    async def read(self, address, size, value_type=None):
        """Read memory at the specified address.

        Args:
            address: Memory address to read from
            size: Number of bytes to read
            value_type: Optional type hint for value interpretation

        Returns:
            Bytes read from memory
        """
        return read_memory(self.session, address, size)


def read_memory(session, address, size):
    if not session:
        return None
    return execute_script(session, READ_SCRIPT, "read_memory", address, size)
