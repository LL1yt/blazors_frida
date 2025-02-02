from executor import execute_script

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


def scan_memory(session, value_type, value):
    if not session:
        return []
    return execute_script(session, SCAN_SCRIPT, "scan_memory", value_type, value)
