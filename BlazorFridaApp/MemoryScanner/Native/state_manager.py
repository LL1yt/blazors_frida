import json
import os
import asyncio
import logging
import aiofiles
import sqlite3
import hashlib
from typing import Dict, Any, Optional, List, Tuple
from dataclasses import dataclass, asdict
from datetime import datetime
import uuid
import aiosqlite

logger = logging.getLogger(__name__)


@dataclass
class ModuleInfo:
    name: str
    base_address: int
    size: int
    signature: str  # Hash of the first few bytes of the module


@dataclass
class CachedOffset:
    module_name: str
    relative_offset: int
    signature: str
    value_type: str
    last_validated: datetime


@dataclass
class StateSnapshot:
    version: str
    timestamp: str
    checkpoint_id: Optional[str]
    scan_results: list
    metadata: Dict[str, Any]
    module_info: Optional[ModuleInfo] = None
    cached_offsets: List[CachedOffset] = None


class StateManager:
    def __init__(self, session_id: str, state_dir: str = "states"):
        self.session_id = session_id
        self.state_dir = state_dir
        self.current_version = str(uuid.uuid4())
        self.state_lock = asyncio.Lock()
        self.db_path = os.path.join(state_dir, "scan_cache.db")
        self._ensure_state_directory()
        self._init_database()

    def _ensure_state_directory(self):
        """Ensure the state directory exists"""
        session_dir = os.path.join(self.state_dir, self.session_id)
        os.makedirs(session_dir, exist_ok=True)

    def _init_database(self):
        """Initialize SQLite database for scan caching"""
        try:
            with sqlite3.connect(self.db_path) as conn:
                conn.execute(
                    """
                    CREATE TABLE IF NOT EXISTS modules (
                        id INTEGER PRIMARY KEY,
                        name TEXT NOT NULL,
                        base_address INTEGER NOT NULL,
                        size INTEGER NOT NULL,
                        signature TEXT NOT NULL,
                        last_updated TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    )
                """
                )

                conn.execute(
                    """
                    CREATE TABLE IF NOT EXISTS cached_offsets (
                        id INTEGER PRIMARY KEY,
                        module_name TEXT NOT NULL,
                        relative_offset INTEGER NOT NULL,
                        signature TEXT NOT NULL,
                        value_type TEXT NOT NULL,
                        last_validated TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (module_name) REFERENCES modules(name)
                    )
                """
                )
                conn.commit()
        except Exception as e:
            logger.error(f"Failed to initialize database: {e}")
            raise

    def _get_state_path(self, version: str) -> str:
        """Get the file path for a state version"""
        return os.path.join(self.state_dir, self.session_id, f"{version}.json")

    async def calculate_module_signature(
        self, session, module_name: str, base_address: int
    ) -> str:
        """Calculate a signature for a module based on its first few bytes"""
        try:
            # Read first 1KB of the module for signature
            script = """
            rpc.exports = {
                readModuleBytes: function(baseAddress, size) {
                    try {
                        return Memory.readByteArray(ptr(baseAddress), size);
                    } catch(e) {
                        return null;
                    }
                }
            };
            """
            bytes_data = await session.execute_script(
                script, "readModuleBytes", base_address, 1024
            )
            if bytes_data:
                return hashlib.sha256(bytes_data).hexdigest()
            return ""
        except Exception as e:
            logger.error(f"Failed to calculate module signature: {e}")
            return ""

    async def cache_offset(
        self,
        module_name: str,
        absolute_address: int,
        base_address: int,
        value_type: str,
        signature: str,
    ):
        """Cache a memory offset relative to module base"""
        try:
            relative_offset = absolute_address - base_address
            async with aiosqlite.connect(self.db_path) as db:
                await db.execute(
                    """
                    INSERT INTO cached_offsets 
                    (module_name, relative_offset, signature, value_type, last_validated)
                    VALUES (?, ?, ?, ?, CURRENT_TIMESTAMP)
                """,
                    (module_name, relative_offset, signature, value_type),
                )
                await db.commit()
            logger.info(f"Cached offset for {module_name}: {hex(relative_offset)}")
        except Exception as e:
            logger.error(f"Failed to cache offset: {e}")

    async def validate_cached_offset(
        self, session, module_name: str, cached_offset: CachedOffset
    ) -> bool:
        """Validate a cached offset by checking module signature"""
        try:
            current_signature = await self.calculate_module_signature(
                session, module_name, cached_offset.relative_offset
            )
            return current_signature == cached_offset.signature
        except Exception as e:
            logger.error(f"Failed to validate cached offset: {e}")
            return False

    async def get_cached_offsets(self, module_name: str) -> List[CachedOffset]:
        """Get all cached offsets for a module"""
        try:
            async with aiosqlite.connect(self.db_path) as db:
                async with db.execute(
                    """
                    SELECT relative_offset, signature, value_type, last_validated
                    FROM cached_offsets
                    WHERE module_name = ?
                """,
                    (module_name,),
                ) as cursor:
                    rows = await cursor.fetchall()
                    return [
                        CachedOffset(
                            module_name=module_name,
                            relative_offset=row[0],
                            signature=row[1],
                            value_type=row[2],
                            last_validated=datetime.fromisoformat(row[3]),
                        )
                        for row in rows
                    ]
        except Exception as e:
            logger.error(f"Failed to get cached offsets: {e}")
            return []

    async def invalidate_cache(self, module_name: Optional[str] = None):
        """Invalidate cache entries for a module or all modules"""
        try:
            async with aiosqlite.connect(self.db_path) as db:
                if module_name:
                    await db.execute(
                        "DELETE FROM cached_offsets WHERE module_name = ?",
                        (module_name,),
                    )
                else:
                    await db.execute("DELETE FROM cached_offsets")
                await db.commit()
            logger.info(f"Invalidated cache for {module_name or 'all modules'}")
        except Exception as e:
            logger.error(f"Failed to invalidate cache: {e}")

    async def save_state(
        self, checkpoint_id: Optional[str], scan_results: list, metadata: Dict[str, Any]
    ) -> str:
        """Save the current state to disk with versioning"""
        async with self.state_lock:
            new_version = str(uuid.uuid4())
            snapshot = StateSnapshot(
                version=new_version,
                timestamp=datetime.utcnow().isoformat(),
                checkpoint_id=checkpoint_id,
                scan_results=scan_results,
                metadata=metadata,
            )

            try:
                state_path = self._get_state_path(new_version)
                async with aiofiles.open(state_path, "w") as f:
                    await f.write(json.dumps(asdict(snapshot), indent=2))
                logger.info(f"State saved: version={new_version}")
                self.current_version = new_version
                return new_version
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
        await self.save_state(checkpoint_id, scan_results, metadata)
        return checkpoint_id

    async def restore_checkpoint(self, checkpoint_id: str) -> Optional[StateSnapshot]:
        """Restore state from a checkpoint"""
        async with self.state_lock:
            try:
                session_dir = os.path.join(self.state_dir, self.session_id)
                if not os.path.exists(session_dir):
                    return None

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
