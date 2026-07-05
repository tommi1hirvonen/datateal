"""Secret redaction for kernel cell outputs.

At module load, reads ``DATATEAL_SECRET_KEYS`` (a comma-separated list of
environment-variable names that hold secret values) and resolves each name to
its value.  Any secret value longer than two characters is added to the
redaction set; values that are empty or trivially short are skipped to avoid
over-redaction of common substrings.

The replacement token ``[REDACTED]`` is used, matching the behaviour of
Microsoft Fabric and Azure Databricks notebook outputs.
"""

import os

_REDACTED = "[REDACTED]"

# Minimum length a secret value must have to be included in the redaction set.
# This avoids replacing ubiquitous substrings when a secret is, say, a single
# letter or digit.
_MIN_SECRET_LENGTH = 3


def _build_redaction_set() -> frozenset[str]:
    raw = os.environ.get("DATATEAL_SECRET_KEYS", "")
    if not raw:
        return frozenset()

    values: set[str] = set()
    for key in raw.split(","):
        key = key.strip()
        if not key:
            continue
        value = os.environ.get(key)
        if value and len(value) >= _MIN_SECRET_LENGTH:
            values.add(value)

    return frozenset(values)


# Resolved once at import time so each execution pays no per-call overhead.
_SECRET_VALUES: frozenset[str] = _build_redaction_set()


def redact(text: str) -> str:
    """Replace all secret values in *text* with ``[REDACTED]``."""
    for secret in _SECRET_VALUES:
        text = text.replace(secret, _REDACTED)
    return text


def redact_output(output: dict) -> dict:
    """Return a copy of *output* with secret values replaced.

    Handles the three output types produced by ``_collect_output``:

    * ``stream``         — ``text`` field
    * ``execute_result`` — ``data["text/plain"]`` (if present)
    * ``display_data``   — ``data["text/plain"]`` (if present)

    The structured DataFrame MIME type
    (``application/vnd.datateal.dataframe+json``) is intentionally left
    untouched because it is structured tabular data, not free-form text.
    """
    if not _SECRET_VALUES:
        return output

    output_type = output.get("type")

    if output_type == "stream":
        text = output.get("text", "")
        redacted = redact(text)
        if redacted is not text:
            return {**output, "text": redacted}
        return output

    if output_type in ("execute_result", "display_data"):
        data = output.get("data")
        if not isinstance(data, dict):
            return output
        plain = data.get("text/plain")
        if plain is None:
            return output
        redacted_plain = redact(plain)
        if redacted_plain is plain:
            return output
        return {**output, "data": {**data, "text/plain": redacted_plain}}

    return output


def redact_error(error: dict) -> dict:
    """Return a copy of *error* with secret values replaced.

    Covers ``evalue`` (the error message) and each line of ``traceback``.
    """
    if not _SECRET_VALUES:
        return error

    evalue = error.get("evalue", "")
    redacted_evalue = redact(evalue)

    traceback: list[str] = error.get("traceback", [])
    redacted_traceback = [redact(line) for line in traceback]

    if redacted_evalue == evalue and redacted_traceback == traceback:
        return error

    return {**error, "evalue": redacted_evalue, "traceback": redacted_traceback}
