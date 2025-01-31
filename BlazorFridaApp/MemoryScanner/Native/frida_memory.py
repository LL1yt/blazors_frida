import frida
import json


class FridaMemoryScanner:
    def __init__(self):
        self.session = None
        self.script = None

    def attach_to_process(self, process_name):
        try:
            self.session = frida.attach(process_name)
            return True
        except frida.ProcessNotFoundError:
            return False
        except Exception as e:
            print(f"Error attaching to process: {str(e)}")
            return False

    def read_memory(self, address, size):
        if not self.session:
            return None

        script_code = """
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

        try:
            script = self.session.create_script(script_code)
            script.load()
            api = script.exports
            result = api.read_memory(address, size)
            script.unload()
            return result
        except Exception as e:
            print(f"Error reading memory: {str(e)}")
            return None

    def write_memory(self, address, data):
        if not self.session:
            return False

        script_code = """
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

        try:
            script = self.session.create_script(script_code)
            script.load()
            api = script.exports
            result = api.write_memory(address, data)
            script.unload()
            return result
        except Exception as e:
            print(f"Error writing memory: {str(e)}")
            return False

    def scan_memory(self, value_type, value):
        if not self.session:
            return []

        script_code = """
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

        try:
            script = self.session.create_script(script_code)
            script.load()
            api = script.exports
            result = api.scan_memory(value_type, value)
            script.unload()
            return result
        except Exception as e:
            print(f"Error scanning memory: {str(e)}")
            return []

    def get_process_list(self):
        try:
            processes = []
            for process in frida.enumerate_processes():
                processes.append({"name": process.name, "pid": process.pid})
            return json.dumps(processes)
        except Exception as e:
            print(f"Error getting process list: {str(e)}")
            return "[]"

    def detach(self):
        if self.session:
            self.session.detach()
            self.session = None
