import frida
import json
import sys


def main():
    print("Starting Frida test...")
    try:
        print("Getting device manager...")
        device_manager = frida.get_device_manager()
        print("Getting local device...")
        local_device = device_manager.get_local_device()
        print(f"Local device type: {local_device.type}")

        print("Getting process list...")
        processes = local_device.enumerate_processes()
        print(f"Found {len(processes)} processes")

        # Convert to JSON-friendly format
        process_list = [
            {"pid": process.pid, "name": process.name} for process in processes
        ]
        print(json.dumps(process_list[:5], indent=2))  # Print first 5 processes

        print("Test completed successfully")
        return 0
    except Exception as e:
        print(f"Error: {str(e)}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
