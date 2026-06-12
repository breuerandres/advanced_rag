"""Incremental extractor for the streamed `answer` field of the model's JSON payload.

The chat model returns `{"answer": "...", "cited_chunk_ids": [...]}` (json_object
mode). Deltas can split anywhere, including inside escape sequences, so this is a
character state machine: it scans for the `"answer"` key, then enters the string
value and emits unescaped characters as they complete. Everything fed is also kept
in `raw` so the caller can json-parse the full payload at the end (citations) or
fall back to raw text when the payload was never JSON.
"""

from __future__ import annotations


class AnswerStreamParser:
    _SEEK_KEY = 0      # scanning for "answer" key
    _SEEK_COLON = 1    # key found, scanning for the value start quote
    _IN_VALUE = 2      # inside the answer string value
    _DONE = 3          # value closed; ignore the rest

    def __init__(self) -> None:
        self.raw: str = ""
        self._state = self._SEEK_KEY
        self._escape = False
        self._unicode_buffer: str | None = None  # collects 4 hex digits after \u
        self._key_window = ""

    def feed(self, delta: str) -> str:
        """Consume a provider delta; return the answer characters it completes."""
        self.raw += delta
        out: list[str] = []
        for ch in delta:
            if self._state == self._SEEK_KEY:
                self._key_window = (self._key_window + ch)[-12:]
                if self._key_window.endswith('"answer"'):
                    self._state = self._SEEK_COLON
            elif self._state == self._SEEK_COLON:
                if ch == '"':
                    self._state = self._IN_VALUE
                # ':' and whitespace are skipped silently
            elif self._state == self._IN_VALUE:
                if self._unicode_buffer is not None:
                    self._unicode_buffer += ch
                    if len(self._unicode_buffer) == 4:
                        out.append(chr(int(self._unicode_buffer, 16)))
                        self._unicode_buffer = None
                elif self._escape:
                    self._escape = False
                    if ch == "u":
                        self._unicode_buffer = ""
                    else:
                        out.append(_UNESCAPE.get(ch, ch))
                elif ch == "\\":
                    self._escape = True
                elif ch == '"':
                    self._state = self._DONE
                else:
                    out.append(ch)
        return "".join(out)

    def finalize_raw(self) -> str:
        """Full raw content; used for citation parsing or non-JSON fallback."""
        return self.raw


_UNESCAPE = {"n": "\n", "t": "\t", "r": "\r", "b": "\b", "f": "\f", '"': '"', "\\": "\\", "/": "/"}
