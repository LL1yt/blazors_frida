from typing import Dict, List, Optional, Any
import logging
from datetime import datetime, timedelta
import asyncio


class Session:
    def __init__(self, process_id: int, scanner: Any):
        if not scanner:
            raise ValueError("Scanner cannot be None")
        if not hasattr(scanner, "is_initialized"):
            raise ValueError("Scanner must have is_initialized property")
        if not scanner.is_initialized:
            raise ValueError("Scanner must be initialized before creating session")

        self.session_id = f"{process_id}_{datetime.now().timestamp()}"
        self.process_id = process_id
        self.created_at = datetime.now()
        self.last_accessed = datetime.now()
        self.scanner = scanner
        self.results = []
        self.previous_results = None
        self.freezer_tasks: Dict[int, tuple] = {}
        self.state_version = 0


class SessionManager:
    def __init__(self):
        self._sessions: Dict[str, Session] = {}
        self._logger = logging.getLogger(__name__)
        self._cleanup_task: Optional[asyncio.Task] = None

    async def start(self):
        """Start the session manager and its cleanup task"""
        self._cleanup_task = asyncio.create_task(self._cleanup_loop())
        self._logger.info("SessionManager started")

    async def stop(self):
        """Stop the session manager and cleanup"""
        if self._cleanup_task:
            self._cleanup_task.cancel()
            try:
                await self._cleanup_task
            except asyncio.CancelledError:
                pass
        self._logger.info("SessionManager stopped")

    def create_session(self, process_id: int, scanner: Any) -> Session:
        """Creates a new scan session for the given process ID with a scanner"""
        session = Session(process_id, scanner)
        self._sessions[session.session_id] = session
        self._logger.info(
            f"Created new session {session.session_id} for process {process_id}"
        )
        return session

    def get_session(self, session_id: str) -> Optional[Session]:
        """Gets a session by ID"""
        session = self._sessions.get(session_id)
        if session:
            session.last_accessed = datetime.now()
        return session

    async def remove_session(self, session_id: str):
        """Remove a session and cleanup its resources"""
        if session_id in self._sessions:
            session = self._sessions[session_id]
            # Cancel any active freezer tasks
            for task, _ in session.freezer_tasks.values():
                task.cancel()
            del self._sessions[session_id]
            self._logger.info(f"Removed session {session_id}")

    def update_session_results(
        self, session_id: str, results: List[int], store_previous: bool = True
    ):
        """Updates the results for a given session"""
        session = self.get_session(session_id)
        if not session:
            raise KeyError(f"Session {session_id} not found")

        if store_previous:
            session.previous_results = session.results
        session.results = results
        session.last_accessed = datetime.now()
        session.state_version += 1

    async def _cleanup_loop(self, cleanup_interval: int = 300):
        """Background task to cleanup old sessions"""
        while True:
            try:
                await asyncio.sleep(cleanup_interval)
                await self.cleanup_old_sessions()
            except asyncio.CancelledError:
                break
            except Exception as e:
                self._logger.error("Error in cleanup loop", exc_info=e)
                await asyncio.sleep(60)  # Wait a bit before retrying

    async def cleanup_old_sessions(self, max_age_minutes: int = 30):
        """Removes sessions older than max_age_minutes"""
        now = datetime.now()
        to_remove = []

        for session_id, session in self._sessions.items():
            if now - session.last_accessed > timedelta(minutes=max_age_minutes):
                to_remove.append(session_id)

        for session_id in to_remove:
            await self.remove_session(session_id)
            self._logger.debug(f"Cleaned up session {session_id}")

    def get_session_results(self, session_id: str) -> List[int]:
        """Gets the current results for a session"""
        session = self.get_session(session_id)
        if not session:
            raise KeyError(f"Session {session_id} not found")
        return session.results

    def get_previous_results(self, session_id: str) -> Optional[List[int]]:
        """Gets the previous results for a session"""
        session = self.get_session(session_id)
        if not session:
            raise KeyError(f"Session {session_id} not found")
        return session.previous_results
