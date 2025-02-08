from typing import Dict, List, Optional
import logging
from datetime import datetime, timedelta


class SessionManager:
    def __init__(self):
        self._sessions: Dict[str, Dict] = {}
        self._logger = logging.getLogger(__name__)

    def create_session(self, process_id: int) -> str:
        """Creates a new scan session for the given process ID"""
        session_id = f"{process_id}_{datetime.now().timestamp()}"
        self._sessions[session_id] = {
            "process_id": process_id,
            "created_at": datetime.now(),
            "last_accessed": datetime.now(),
            "results": [],
            "previous_results": None,
        }
        return session_id

    def get_session(self, session_id: str) -> Optional[Dict]:
        """Gets a session by ID"""
        session = self._sessions.get(session_id)
        if session:
            session["last_accessed"] = datetime.now()
        return session

    def update_session_results(
        self, session_id: str, results: List[int], store_previous: bool = True
    ):
        """Updates the results for a given session"""
        if session_id not in self._sessions:
            raise KeyError(f"Session {session_id} not found")

        session = self._sessions[session_id]
        if store_previous:
            session["previous_results"] = session["results"]
        session["results"] = results
        session["last_accessed"] = datetime.now()

    def cleanup_old_sessions(self, max_age_minutes: int = 30):
        """Removes sessions older than max_age_minutes"""
        now = datetime.now()
        to_remove = []

        for session_id, session in self._sessions.items():
            if now - session["last_accessed"] > timedelta(minutes=max_age_minutes):
                to_remove.append(session_id)

        for session_id in to_remove:
            del self._sessions[session_id]
            self._logger.debug(f"Cleaned up session {session_id}")

    def get_session_results(self, session_id: str) -> List[int]:
        """Gets the current results for a session"""
        session = self.get_session(session_id)
        if not session:
            raise KeyError(f"Session {session_id} not found")
        return session["results"]

    def get_previous_results(self, session_id: str) -> Optional[List[int]]:
        """Gets the previous results for a session"""
        session = self.get_session(session_id)
        if not session:
            raise KeyError(f"Session {session_id} not found")
        return session.get("previous_results")
