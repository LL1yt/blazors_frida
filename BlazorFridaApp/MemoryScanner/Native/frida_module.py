from attacher import FridaAttacher
from reader import read_memory
from writer import write_memory
from scanner import scan_memory
from process_list import get_process_list


class FridaMemoryScanner:
    def __init__(self):
        self.attacher = FridaAttacher()

    def attach_to_process(self, process_name):
        return self.attacher.attach_to_process(process_name)

    def detach(self):
        return self.attacher.detach()

    def read_memory(self, address, size):
        if not self.attacher.session:
            return None
        return read_memory(self.attacher.session, address, size)

    def write_memory(self, address, data):
        if not self.attacher.session:
            return False
        return write_memory(self.attacher.session, address, data)

    def scan_memory(self, value_type, value):
        if not self.attacher.session:
            return []
        return scan_memory(self.attacher.session, value_type, value)

    def get_process_list(self):
        return get_process_list()
