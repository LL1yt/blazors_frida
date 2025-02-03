import frida
import json
import sys
import threading
from threading import Event
import traceback


def get_process_list():
    try:
        print("Starting get_process_list...", file=sys.stderr)
        # Create an event for synchronization
        done = Event()
        result = []
        error = None

        def worker():
            nonlocal result, error
            try:
                print("Worker thread starting...", file=sys.stderr)
                # Get local device directly
                print("Getting local device...", file=sys.stderr)
                device = frida.get_local_device()
                print(
                    f"Got local device: {device.name} ({device.type})", file=sys.stderr
                )

                # Get process list
                print("Enumerating processes...", file=sys.stderr)
                processes = []
                for process in device.enumerate_processes():
                    processes.append({"name": process.name, "pid": process.pid})
                result = sorted(processes, key=lambda x: x["name"].lower())
                print(f"Found {len(processes)} processes", file=sys.stderr)
            except Exception as e:
                error = f"Error in worker thread: {str(e)}\n{traceback.format_exc()}"
                print(error, file=sys.stderr)
            finally:
                print("Worker thread finished", file=sys.stderr)
                done.set()

        # Start worker thread
        print("Starting worker thread...", file=sys.stderr)
        thread = threading.Thread(target=worker)
        thread.daemon = True
        thread.start()

        # Wait for completion with timeout
        print("Waiting for worker thread...", file=sys.stderr)
        if not done.wait(timeout=10.0):  # Увеличиваем таймаут до 10 секунд
            error_msg = "Operation timed out waiting for process list"
            print(error_msg, file=sys.stderr)
            raise TimeoutError(error_msg)

        if error:
            print(f"Worker thread reported error: {error}", file=sys.stderr)
            raise Exception(error)

        print("Successfully got process list", file=sys.stderr)
        return json.dumps(result)
    except Exception as e:
        error_msg = f"Error in get_process_list: {str(e)}\n{traceback.format_exc()}"
        print(error_msg, file=sys.stderr)
        return "[]"
