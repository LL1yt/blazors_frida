from executor import execute_script

WRITE_SCRIPT = """
rpc.exports = {
    writeMemory: function(address, data) {
        try {
            Memory.writeByteArray(ptr(address), data);
            return true;
        } catch(e) {
            return false;
        }
    }
};
"""


class MemoryWriter:
    def __init__(self, session):
        self.session = session

    async def write(self, address, value, value_type=None):
        """Write value to memory at the specified address.

        Args:
            address: Memory address to write to
            value: Value to write
            value_type: Optional type hint for value interpretation

        Returns:
            Boolean indicating success
        """
        return write_memory(self.session, address, value)


def write_memory(session, address, data):
    if not session:
        return False
    return execute_script(session, WRITE_SCRIPT, "write_memory", address, data)
