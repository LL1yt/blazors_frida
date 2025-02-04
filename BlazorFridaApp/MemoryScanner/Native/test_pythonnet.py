import sys
import time
import threading
from threading import Event
import frida
import signal
import ctypes
import os

__all__ = [
    "set_dotnet_callback",
    "cleanup",
    "run_long_running_test",
    "run_gil_test",
    "run_frida_test",
    "force_stop",
]

# Global state to track resources
_resources = {
    "threads": [],
    "frida_sessions": [],
    "stop_event": Event(),
    "dotnet_callback": None,  # Callback to .NET for cleanup
}


def run_long_running_test():
    """Run long running function test"""
    try:
        print("\nRunning long running function test...")
        result = long_running_function()
        print(f"Test result: {result}")
        cleanup()
        return True
    except Exception as e:
        print(f"Error during test: {e}")
        cleanup()
        return False


def run_gil_test():
    """Run GIL test"""
    try:
        print("\nRunning GIL test...")
        result = test_gil()
        cleanup()
        return result
    except Exception as e:
        print(f"Error during test: {e}")
        cleanup()
        return False


def run_frida_test():
    """Run Frida test"""
    try:
        print("\nRunning Frida test...")
        result = test_frida()
        cleanup()
        return result
    except Exception as e:
        print(f"Error during test: {e}")
        cleanup()
        return False


def set_dotnet_callback(callback):
    """Set the callback function to be called for .NET cleanup"""
    global _resources
    _resources["dotnet_callback"] = callback
    print("Callback from .NET registered")
    return True


def setup_signal_handlers():
    """Set up signal handlers for graceful shutdown"""

    def signal_handler(signum, frame):
        print("\nReceived signal, initiating cleanup...")
        _resources["stop_event"].set()

        # Call .NET cleanup if callback is set
        if _resources["dotnet_callback"] is not None:
            try:
                print("Calling .NET cleanup callback...")
                _resources["dotnet_callback"]()
                print(".NET cleanup callback completed")
            except Exception as e:
                print(f"Error calling .NET cleanup: {e}")

        cleanup()
        print("Exiting Python process gracefully...")

    print("Setting up signal handlers...")
    # Set up handlers for both SIGINT (Ctrl+C) and SIGTERM
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)
    print("Signal handlers set up successfully")


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

    # Call .NET cleanup if callback is set
    if _resources["dotnet_callback"] is not None:
        try:
            print("Calling .NET cleanup callback...")
            _resources["dotnet_callback"]()
            print(".NET cleanup callback completed")
        except Exception as e:
            print(f"Error calling .NET cleanup: {e}")

    print("Cleanup completed")
    # Instead of sys.exit(), just return normally
    return True


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


def force_stop():
    """Force stop all Python processes and cleanup"""
    print("Force stopping all Python processes...")

    # Signal all operations to stop immediately
    _resources["stop_event"].set()

    # Aggressively cleanup threads
    for thread in _resources["threads"]:
        if thread.is_alive():
            print(f"Force stopping thread {thread.name}...")
            try:
                # Try to join with a very short timeout
                thread.join(timeout=0.5)
            except:
                pass  # Ignore any errors during force stop

    # Clear thread list
    _resources["threads"].clear()

    # Aggressively cleanup Frida sessions
    for session in _resources["frida_sessions"]:
        try:
            session.detach()
        except:
            pass  # Ignore any errors during force stop

    # Clear session list
    _resources["frida_sessions"].clear()

    print("Force stop completed")
    return True


# Initialize module
print("Initializing Python test module...")
setup_signal_handlers()

# Make functions available at module level
sys.modules[__name__].set_dotnet_callback = set_dotnet_callback
sys.modules[__name__].run_long_running_test = run_long_running_test
sys.modules[__name__].run_gil_test = run_gil_test
sys.modules[__name__].run_frida_test = run_frida_test
sys.modules[__name__].force_stop = force_stop

print("Python test module initialized")


def main():
    print(f"Python version: {sys.version}")
    print(f"Python executable: {sys.executable}")
    print(f"Python path: {sys.path}")

    if not run_long_running_test():
        return 1

    print("\nAll tests completed successfully!")
    return 0


if __name__ == "__main__":
    sys.exit(main())
