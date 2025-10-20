"""
Lightweight Python adapter/mimic for the AIAssist ViewModel.
This is a pure-Python class intended to be tested by pytest without requiring
any .NET runtime. It models the minimal behavior needed by tests:
- hold messages (user/assistant)
- validate input
- send_message uses an injected ai_service mock to "get" a reply
- calculate_service_charge uses an injected charge_service mock

Design contract (inputs/outputs):
- send_message(text: str) -> bool (returns True if message sent)
- messages: list of {'role': 'user'|'assistant', 'text': str}
- can_send: property (bool) - True when last input is non-empty and non-whitespace
- calculate_service_charge(context: dict) -> dict (returns result from charge_service)

Error modes: raises ValueError for invalid inputs.

This adapter is intentionally simple and test-friendly.
"""
from typing import List, Dict, Any, Optional


class AIAssistViewModel:
    def __init__(self, ai_service, charge_service=None, logger=None):
        # ai_service: an object with async-like 'get_reply' or sync 'get_reply' method
        self.ai_service = ai_service
        self.charge_service = charge_service
        self.logger = logger
        self.messages: List[Dict[str, str]] = []
        self._current_input: str = ""

    @property
    def can_send(self) -> bool:
        return bool(self._current_input and self._current_input.strip())

    def set_input(self, text: Optional[str]):
        self._current_input = text or ""

    def send_message(self) -> bool:
        """Send current input to the ai_service and append both user and assistant messages.
        Returns True on success.
        Raises ValueError when input is invalid (empty/whitespace).
        """
        if not self.can_send:
            raise ValueError("Cannot send empty message")

        user_text = self._current_input.strip()
        self.messages.append({"role": "user", "text": user_text})

        # ai_service may be a callable or object with get_reply method
        reply = None
        if callable(self.ai_service):
            reply = self.ai_service(user_text)
        elif hasattr(self.ai_service, "get_reply"):
            reply = self.ai_service.get_reply(user_text)
        else:
            raise RuntimeError("ai_service is not callable or does not implement get_reply")

        if reply is None:
            reply = ""

        self.messages.append({"role": "assistant", "text": str(reply)})
        # clear input after sending
        self._current_input = ""
        return True

    def calculate_service_charge(self, context: Dict[str, Any]) -> Dict[str, Any]:
        if not self.charge_service:
            raise RuntimeError("No charge_service provided")
        if not isinstance(context, dict):
            raise ValueError("context must be a dict")
        # delegate to the charge service
        if callable(self.charge_service):
            return self.charge_service(context)
        elif hasattr(self.charge_service, "calculate"):
            return self.charge_service.calculate(context)
        else:
            raise RuntimeError("charge_service does not expose calculate behavior")
