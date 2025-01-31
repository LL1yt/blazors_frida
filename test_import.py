import sys
import os

# Add the path where frida_memory.py is located
module_path = os.path.join(os.getcwd(), "BlazorFridaApp", "MemoryScanner", "Native")
print(f"Adding to Python path: {module_path}")
sys.path.append(module_path)

# Print Python version and sys.path
print(f"\nPython version: {sys.version}")
print("\nPython path:")
for p in sys.path:
    print(p)

# Try to import frida_memory
try:
    import frida_memory

    print("\nSuccessfully imported frida_memory")
except ImportError as e:
    print(f"\nFailed to import frida_memory: {e}")
