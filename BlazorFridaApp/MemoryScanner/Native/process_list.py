import frida
import json
import sys


def get_process_list():
    try:
        print("Getting device manager...", file=sys.stderr)
        device_manager = frida.get_device_manager()

        print("Getting local device...", file=sys.stderr)
        local_device = device_manager.get_local_device()

        print("Enumerating processes...", file=sys.stderr)
        processes = []
        for process in local_device.enumerate_processes():
            processes.append({"name": process.name, "pid": process.pid})

        print(f"Found {len(processes)} processes", file=sys.stderr)
        return json.dumps(processes)
    except Exception as e:
        print(f"Error getting process list: {str(e)}", file=sys.stderr)
        return "[]"
