import frida
import json


def get_process_list():
    try:
        processes = []
        for process in frida.get_local_device().enumerate_processes():
            processes.append({"name": process.name, "pid": process.pid})
        return json.dumps(processes)
    except Exception as e:
        print(f"Error getting process list: {e}")
        return "[]"
