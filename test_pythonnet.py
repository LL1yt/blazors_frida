import sys
import time
import threading
from threading import Event
import frida

# Global state to track resources
_resources = {"threads": [], "frida_sessions": [], "stop_event": Event()}


def cleanup():
    """Clean up all resources used by the module"""
    print("Starting cleanup...")

    # Signal all operations to stop
    _resources["stop_event"].set()

    # Wait for all threads to complete
    for thread in _resources["threads"]:
        if thread.is_alive():
            print(f"Waiting for thread {thread.name} to complete...")
            thread.join(timeout=2.0)

    # Clear thread list
    _resources["threads"].clear()

    # Clean up Frida sessions
    for session in _resources["frida_sessions"]:
        try:
            session.detach()
        except Exception as e:
            print(f"Error cleaning up Frida session: {e}")

    # Clear session list
    _resources["frida_sessions"].clear()

    # Reset stop event
    _resources["stop_event"].clear()
    print("Cleanup completed")


def long_running_function():
    """Function that simulates long-running operation"""
    print("Starting long operation...")
    start_time = time.time()
    while not _resources["stop_event"].is_set() and (time.time() - start_time) < 2:
        time.sleep(0.1)
    print("Long operation completed")
    return "Success!"


def test_frida():
    """Test Frida functionality"""
    print("\nTesting Frida...")
    done = Event()
    result = None
    error = None

    def worker():
        nonlocal result, error
        try:
            if _resources["stop_event"].is_set():
                return

            print("Getting device manager...")
            device_manager = frida.get_device_manager()
            print("Getting local device...")
            local_device = device_manager.get_local_device()
            print(f"Local device type: {local_device.type}")
            print("Getting processes...")
            processes = local_device.enumerate_processes()
            result = f"Found {len(processes)} processes"
            print(result)
            if processes:
                first = processes[0]
                print(f"First process: {first.name} (PID: {first.pid})")
        except Exception as e:
            error = str(e)
        finally:
            done.set()

    thread = threading.Thread(target=worker, name="FridaWorker")
    thread.daemon = True
    _resources["threads"].append(thread)
    print("Starting Frida worker thread...")
    thread.start()

    if not done.wait(timeout=5.0):
        print("Frida operation timed out!")
        return False

    if error:
        print(f"Frida error occurred: {error}")
        return False

    print(f"Frida test result: {result}")
    return True


def test_gil():
    """Test GIL behavior with threading"""
    print("\nTesting GIL with threading...")
    done = Event()
    result = None
    error = None

    def worker():
        nonlocal result, error
        try:
            if _resources["stop_event"].is_set():
                return
            result = long_running_function()
        except Exception as e:
            error = str(e)
        finally:
            done.set()

    thread = threading.Thread(target=worker, name="GILWorker")
    thread.daemon = True
    _resources["threads"].append(thread)
    print("Starting worker thread...")
    thread.start()

    if not done.wait(timeout=5.0):
        print("Operation timed out!")
        return False

    if error:
        print(f"Error occurred: {error}")
        return False

    print(f"Thread result: {result}")
    return True


def main():
    print(f"Python version: {sys.version}")
    print(f"Python executable: {sys.executable}")
    print(f"Python path: {sys.path}")

    print("\nTesting basic function call...")
    try:
        result = long_running_function()
        print(f"Basic function result: {result}")
    except Exception as e:
        print(f"Error in basic function: {e}")
        return 1

    if not test_gil():
        print("GIL test failed")
        return 1

    if not test_frida():
        print("Frida test failed")
        return 1

    print("\nAll tests completed successfully!")
    return 0


if __name__ == "__main__":
    sys.exit(main())
