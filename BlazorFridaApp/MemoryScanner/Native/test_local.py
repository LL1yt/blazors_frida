import frida
import json
import sys
from process_list import get_process_list


def main():
    try:
        print("Python version:", sys.version)
        print("Frida version:", frida.__version__)

        print("\nGetting process list...")
        result = get_process_list()
        processes = json.loads(result)
        print(f"Found {len(processes)} processes")
        if processes:
            print("First 5 processes:")
            for p in processes[:5]:
                print(f"  {p['name']} (PID: {p['pid']})")

        return 0
    except Exception as e:
        print("Error:", str(e), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
