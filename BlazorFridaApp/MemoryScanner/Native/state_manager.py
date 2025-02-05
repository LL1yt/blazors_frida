import json
import os
import asyncio
import logging
import aiofiles
from typing import Dict, Any, Optional
from dataclasses import dataclass, asdict
from datetime import datetime
import uuid

logger = logging.getLogger(__name__)


@dataclass
class StateSnapshot:
    version: str
    timestamp: str
    checkpoint_id: Optional[str]
    scan_results: list
    metadata: Dict[str, Any]


class StateManager:
    def __init__(self, session_id: str, state_dir: str = "states"):
        self.session_id = session_id
        self.state_dir = state_dir
        self.current_version = str(uuid.uuid4())
        self.state_lock = asyncio.Lock()
        self._ensure_state_directory()

    def _ensure_state_directory(self):
        """Ensure the state directory exists"""
        session_dir = os.path.join(self.state_dir, self.session_id)
        os.makedirs(session_dir, exist_ok=True)

    def _get_state_path(self, version: str) -> str:
        """Get the file path for a state version"""
        return os.path.join(self.state_dir, self.session_id, f"{version}.json")

    async def save_state(
        self, checkpoint_id: Optional[str], scan_results: list, metadata: Dict[str, Any]
    ) -> str:
        """Save the current state to disk with versioning"""
        async with self.state_lock:
            snapshot = StateSnapshot(
                version=self.current_version,
                timestamp=datetime.utcnow().isoformat(),
                checkpoint_id=checkpoint_id,
                scan_results=scan_results,
                metadata=metadata,
            )

            try:
                state_path = self._get_state_path(self.current_version)
                async with aiofiles.open(state_path, "w") as f:
                    await f.write(json.dumps(asdict(snapshot), indent=2))
                logger.info(f"State saved: version={self.current_version}")
                return self.current_version
            except Exception as e:
                logger.error(f"Failed to save state: {e}")
                raise

    async def load_state(
        self, version: Optional[str] = None
    ) -> Optional[StateSnapshot]:
        """Load state from disk"""
        async with self.state_lock:
            try:
                if version is None:
                    # Get the most recent version
                    session_dir = os.path.join(self.state_dir, self.session_id)
                    if not os.path.exists(session_dir):
                        return None
                    versions = [
                        f.replace(".json", "")
                        for f in os.listdir(session_dir)
                        if f.endswith(".json")
                    ]
                    if not versions:
                        return None
                    version = sorted(versions)[-1]

                state_path = self._get_state_path(version)
                if not os.path.exists(state_path):
                    return None

                async with aiofiles.open(state_path, "r") as f:
                    content = await f.read()
                    state_dict = json.loads(content)
                    return StateSnapshot(**state_dict)
            except Exception as e:
                logger.error(f"Failed to load state: {e}")
                return None

    async def create_checkpoint(
        self, scan_results: list, metadata: Dict[str, Any]
    ) -> str:
        """Create a new checkpoint"""
        checkpoint_id = str(uuid.uuid4())
        new_version = str(uuid.uuid4())

        await self.save_state(checkpoint_id, scan_results, metadata)
        self.current_version = new_version

        return checkpoint_id

    async def restore_checkpoint(self, checkpoint_id: str) -> Optional[StateSnapshot]:
        """Restore state from a checkpoint"""
        async with self.state_lock:
            try:
                session_dir = os.path.join(self.state_dir, self.session_id)
                if not os.path.exists(session_dir):
                    return None

                # Find state file with matching checkpoint_id
                for filename in os.listdir(session_dir):
                    if not filename.endswith(".json"):
                        continue

                    state_path = os.path.join(session_dir, filename)
                    async with aiofiles.open(state_path, "r") as f:
                        content = await f.read()
                        state_dict = json.loads(content)
                        if state_dict.get("checkpoint_id") == checkpoint_id:
                            return StateSnapshot(**state_dict)
                return None
            except Exception as e:
                logger.error(f"Failed to restore checkpoint: {e}")
                return None

    def cleanup_old_states(self, max_states: int = 10):
        """Remove old state files keeping only the most recent ones"""
        try:
            session_dir = os.path.join(self.state_dir, self.session_id)
            if not os.path.exists(session_dir):
                return

            state_files = []
            for f in os.listdir(session_dir):
                if f.endswith(".json"):
                    path = os.path.join(session_dir, f)
                    state_files.append((path, os.path.getmtime(path)))

            # Sort by modification time, newest first
            state_files.sort(key=lambda x: x[1], reverse=True)

            # Remove old files
            for path, _ in state_files[max_states:]:
                os.remove(path)
                logger.info(f"Removed old state file: {path}")
        except Exception as e:
            logger.error(f"Failed to cleanup old states: {e}")
