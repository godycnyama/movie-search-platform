"""Structured JSON logging with request tracing (spec §3.2).

Every log line is one JSON object on stdout. Tool handlers bind a request id
into a contextvar at the start of each call; all log records emitted within
that call (at any depth) carry the same ``request_id``, so a single tool
invocation can be traced across the log stream.
"""

import json
import logging
import sys
from contextvars import ContextVar
from datetime import UTC, datetime

from server.constants import LOG_EXTRA_FIELDS as _EXTRA_FIELDS

request_id_var: ContextVar[str | None] = ContextVar("request_id", default=None)


class JsonFormatter(logging.Formatter):
    def format(self, record: logging.LogRecord) -> str:
        payload: dict = {
            "timestamp": datetime.now(UTC).isoformat(),
            "level": record.levelname,
            "logger": record.name,
            "message": record.getMessage(),
        }
        if (request_id := request_id_var.get()) is not None:
            payload["request_id"] = request_id
        for field in _EXTRA_FIELDS:
            if (value := record.__dict__.get(field)) is not None:
                payload[field] = value
        if record.exc_info and record.exc_info[0] is not None:
            payload["exception"] = self.formatException(record.exc_info)
        return json.dumps(payload, default=str)


def configure_logging(level: str) -> None:
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(JsonFormatter())
    logging.basicConfig(level=level.upper(), handlers=[handler], force=True)

    # The uvicorn CLI installs its own (non-JSON) handlers before the app
    # factory runs; strip them so uvicorn logs flow through the JSON root
    # handler and the process emits a single structured stream.
    for name in ("uvicorn", "uvicorn.error", "uvicorn.access"):
        uvicorn_logger = logging.getLogger(name)
        uvicorn_logger.handlers.clear()
        uvicorn_logger.propagate = True
