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


def write_memory(session, address, data):
    if not session:
        return False
    return execute_script(session, WRITE_SCRIPT, "write_memory", address, data)
