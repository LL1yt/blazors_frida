import frida
import asyncio
import logging

logger = logging.getLogger(__name__)


class FridaAttacher:
    def __init__(self):
        self.session = None

    async def attach_to_process(self, process_id):
        """Attach to a process by its PID."""
        try:
            # Run frida.attach in a thread pool since it's blocking
            loop = asyncio.get_event_loop()
            self.session = await loop.run_in_executor(None, frida.attach, process_id)
            logger.info(f"Successfully attached to process {process_id}")
            return True
        except frida.ProcessNotFoundError:
            logger.error(f"Process {process_id} not found")
            return False
        except Exception as e:
            logger.error(f"Error attaching to process {process_id}: {e}")
            return False

    async def detach(self):
        """Detach from the currently attached process."""
        if self.session:
            try:
                loop = asyncio.get_event_loop()
                await loop.run_in_executor(None, self.session.detach)
                self.session = None
                logger.info("Successfully detached from process")
                return True
            except Exception as e:
                logger.error(f"Error detaching from process: {e}")
                return False
        return True
