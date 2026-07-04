"""Prometheus metrics for the MCP server.

Exposed on ``GET /metrics`` (see ``main.py``) and scraped by Prometheus
(``monitoring/prometheus.yml``, job ``mcp-server``). Tool metrics are recorded
by the ``_done`` helper in ``tools.py`` so every tool is instrumented the same
way without per-tool boilerplate.
"""

from prometheus_client import Counter, Histogram

TOOL_CALLS = Counter(
    "mcp_tool_calls_total",
    "Completed MCP tool calls.",
    ["tool"],
)

TOOL_DURATION_SECONDS = Histogram(
    "mcp_tool_duration_seconds",
    "Wall-clock duration of completed MCP tool calls.",
    ["tool"],
)
