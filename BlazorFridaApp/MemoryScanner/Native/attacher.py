import frida


class FridaAttacher:
    def __init__(self):
        self.session = None

    def attach_to_process(self, process_name):
        try:
            self.session = frida.attach(process_name)
            return True
        except frida.ProcessNotFoundError:
            return False
        except Exception as e:
            print(f"Error attaching to process: {e}")
            return False

    def detach(self):
        if self.session:
            self.session.detach()
            self.session = None
