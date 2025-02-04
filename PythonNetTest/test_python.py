import sys
import os
import site


def print_separator():
    print("\n" + "=" * 50 + "\n")


print("Python Diagnostic Information:")
print_separator()

print("Python Version:")
print(sys.version)
print_separator()

print("Python Executable:")
print(sys.executable)
print_separator()

print("Site Packages:")
print("\n".join(site.getsitepackages()))
print_separator()

print("Current Working Directory:")
print(os.getcwd())
print_separator()

print("Module Search Paths:")
for path in sys.path:
    print(path)
print_separator()

print("Attempting to import frida:")
try:
    import frida

    print("Successfully imported frida")
    print(f"Frida version: {frida.__version__}")
except ImportError as e:
    print(f"Failed to import frida: {e}")
print_separator()

module_path = os.path.join(os.getcwd(), "BlazorFridaApp", "MemoryScanner", "Native")
print(f"Frida Memory Module Path:")
print(f"Checking: {module_path}")
if os.path.exists(module_path):
    print(f"Directory exists")
    print("Contents:")
    for file in os.listdir(module_path):
        print(f"  {file}")
else:
    print("Directory does not exist")
