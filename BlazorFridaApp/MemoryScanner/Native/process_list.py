import frida
import json
import sys
import threading
from threading import Event
import traceback
from dataclasses import dataclass
import os


@dataclass
class ProcessInfo:
    pid: int
    name: str
    path: str = ""  # Default empty string for path if not available


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
                device = None

                # Always try local device first as it's most reliable
                try:
                    device = frida.get_local_device()
                except Exception as e:
                    print(f"Error getting local device: {str(e)}", file=sys.stderr)

                    # Only try USB device if we're not in test mode and local device failed
                    if os.environ.get("BLAZOR_FRIDA_USB", "").lower() == "true":
                        try:
                            print("Getting USB device...", file=sys.stderr)
                            device = frida.get_usb_device(1000)  # 1 second timeout
                        except (frida.InvalidArgumentError, frida.TimedOutError) as e:
                            print(f"USB device error: {str(e)}", file=sys.stderr)
                            raise  # Re-raise if both local and USB failed

                if device is None:
                    raise Exception("Failed to initialize any Frida device")

                print(f"Got device: {device.name} ({device.type})", file=sys.stderr)

                # Get process list with error handling for each process
                print("Enumerating processes...", file=sys.stderr)
                processes = []
                try:
                    for process in device.enumerate_processes():
                        try:
                            processes.append(
                                ProcessInfo(
                                    pid=process.pid,
                                    name=process.name,
                                    path=getattr(process, "path", "") or "",
                                )
                            )
                        except Exception as proc_err:
                            print(
                                f"Error processing process {process.pid}: {str(proc_err)}",
                                file=sys.stderr,
                            )
                            continue
                    result = sorted(processes, key=lambda x: x.name.lower())
                    print(f"Found {len(processes)} processes", file=sys.stderr)
                except frida.InvalidArgumentError:
                    error = "Failed to enumerate processes. Please ensure Frida server is running with sufficient privileges."
                    print(error, file=sys.stderr)
                except Exception as enum_err:
                    error = f"Error enumerating processes: {str(enum_err)}"
                    print(error, file=sys.stderr)
            except Exception as e:
                error = f"Error in worker thread: {str(e)}\n{traceback.format_exc()}"
                print(error, file=sys.stderr)
            finally:
                done.set()

        # Start worker thread
        print("Starting worker thread...", file=sys.stderr)
        thread = threading.Thread(target=worker)
        thread.daemon = True
        thread.start()

        # Wait for completion with timeout
        print("Waiting for worker thread...", file=sys.stderr)
        if not done.wait(timeout=10.0):
            error_msg = "Operation timed out waiting for process list"
            print(error_msg, file=sys.stderr)
            raise TimeoutError(error_msg)

        if error:
            print(f"Worker thread reported error: {error}", file=sys.stderr)
            raise Exception(error)

        print("Successfully got process list", file=sys.stderr)
        return result
    except Exception as e:
        error_msg = f"Error in get_process_list: {str(e)}\n{traceback.format_exc()}"
        print(error_msg, file=sys.stderr)
        return []
