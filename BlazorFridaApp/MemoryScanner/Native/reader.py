from .executor import execute_script

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


def read_memory(session, address, size):
    if not session:
        return None
    return execute_script(session, READ_SCRIPT, "read_memory", address, size)
