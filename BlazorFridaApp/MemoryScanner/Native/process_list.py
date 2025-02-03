import frida
import json
import sys
import threading
from threading import Event


def get_process_list():
    try:
        # Create an event for synchronization
        done = Event()
        result = []
        error = None

        def worker():
            nonlocal result, error
            try:
                # Get local device directly
                device = frida.get_local_device()

                # Get process list
                processes = []
                for process in device.enumerate_processes():
                    processes.append({"name": process.name, "pid": process.pid})
                result = sorted(processes, key=lambda x: x["name"].lower())
            except Exception as e:
                error = str(e)
            finally:
                done.set()

        # Start worker thread
        thread = threading.Thread(target=worker)
        thread.daemon = True
        thread.start()

        # Wait for completion with timeout
        if not done.wait(timeout=4.0):  # 4 second timeout
            raise TimeoutError("Operation timed out")

        if error:
            raise Exception(error)

        return json.dumps(result)
    except Exception as e:
        print(f"Error in get_process_list: {str(e)}", file=sys.stderr)
        return "[]"
