import asyncio
import ast as _ast
import os
import sys
import time
import uuid
from datetime import datetime, timezone
from typing import Optional
import pyflakes.checker
import jedi
from jedi.api.environment import Environment as JediEnvironment

from jupyter_client.manager import AsyncKernelManager
from jupyter_client.asynchronous.client import AsyncKernelClient
from jupyter_client.kernelspec import KernelSpec


class _DuckhouseKernelManager(AsyncKernelManager):
    """AsyncKernelManager that launches kernels in the configured Python environment.

    The kernel Python executable is resolved from the DUCKHOUSE_KERNEL_PYTHON
    environment variable, falling back to the API server's own executable.
    This allows the kernel environment to be fully separated from the API
    environment (e.g. two distinct venvs in a Docker image).

    The kernel environment must have ipykernel installed.
    """

    @property
    def kernel_spec(self) -> KernelSpec:  # type: ignore[override]
        kernel_python = os.environ.get("DUCKHOUSE_KERNEL_PYTHON", sys.executable)
        return KernelSpec(
            argv=[kernel_python, "-m", "ipykernel_launcher", "-f", "{connection_file}"],
            display_name="DuckHouse Python Kernel",
            language="python",
        )


class KernelConnection:
    def __init__(self, kernel_id: str) -> None:
        self.kernel_id = kernel_id
        self.km: _DuckhouseKernelManager = _DuckhouseKernelManager()
        self.kc: Optional[AsyncKernelClient] = None
        self.status = "starting"
        self.created_at = datetime.now(timezone.utc)
        self.last_activity = datetime.now(timezone.utc)
        self._lock = asyncio.Lock()

    async def start(self) -> None:
        await self.km.start_kernel()
        self.kc = self.km.client()
        self.kc.start_channels()
        await self.kc.wait_for_ready(timeout=30)
        self.status = "idle"

    async def execute(self, code: str, timeout: float = 60.0) -> dict:
        async with self._lock:
            if self.kc is None:
                raise RuntimeError("Kernel client is not initialized")
            self.status = "busy"
            self.last_activity = datetime.now(timezone.utc)
            try:
                start = time.monotonic()
                msg_id = self.kc.execute(code)
                result = await self._collect_output(msg_id, timeout)
                result["duration_ms"] = (time.monotonic() - start) * 1000
                return result
            finally:
                self.status = "idle"
                self.last_activity = datetime.now(timezone.utc)

    async def _collect_output(self, msg_id: str, timeout: float) -> dict:
        if self.kc is None:
            raise RuntimeError("Kernel client is not initialized")
        outputs = []
        error = None
        execution_count = None
        loop = asyncio.get_running_loop()
        deadline = loop.time() + timeout

        while True:
            remaining = deadline - loop.time()
            if remaining <= 0:
                raise TimeoutError("Execution timed out")
            try:
                msg = await asyncio.wait_for(self.kc.get_iopub_msg(), timeout=remaining)
            except asyncio.TimeoutError:
                raise TimeoutError("Execution timed out")

            if msg.get("parent_header", {}).get("msg_id") != msg_id:
                continue

            msg_type = msg["msg_type"]
            content = msg["content"]

            if msg_type == "stream":
                outputs.append({"type": "stream", "name": content["name"], "text": content["text"]})
            elif msg_type == "execute_result":
                execution_count = content.get("execution_count")
                outputs.append({"type": "execute_result", "data": content["data"], "execution_count": execution_count})
            elif msg_type == "display_data":
                outputs.append({"type": "display_data", "data": content["data"]})
            elif msg_type == "error":
                error = {"ename": content["ename"], "evalue": content["evalue"], "traceback": content["traceback"]}
            elif msg_type == "status" and content.get("execution_state") == "idle":
                break

        return {
            "status": "error" if error else "ok",
            "execution_count": execution_count,
            "outputs": outputs,
            "error": error,
        }

    async def interrupt(self) -> None:
        await self.km.interrupt_kernel()

    async def restart(self) -> None:
        self.status = "restarting"
        await self.km.restart_kernel()
        if self.kc is None:
            raise RuntimeError("Kernel client is not initialized")
        await self.kc.wait_for_ready(timeout=30)
        self.status = "idle"
        self.last_activity = datetime.now(timezone.utc)

    async def shutdown(self) -> None:
        if self.kc is None:
            raise RuntimeError("Kernel client is not initialized")
        self.kc.stop_channels()
        await self.km.shutdown_kernel(now=True)
        self.status = "dead"

    async def complete(self, code: str, line: int, column: int, context: str = "") -> list[dict]:
        """Return Jedi completions for code at the given 1-based line / 0-based column.

        If *context* is provided (code from prior cells joined by newlines) it is
        prepended so Jedi can resolve names/imports defined in earlier cells.
        """
        kernel_python = os.environ.get("DUCKHOUSE_KERNEL_PYTHON", sys.executable)

        def _run() -> list[dict]:
            if context:
                context_line_count = context.count("\n") + 1
                full_code = context + "\n" + code
                adjusted_line = line + context_line_count
            else:
                full_code = code
                adjusted_line = line

            env = JediEnvironment(kernel_python)
            script = jedi.Script(full_code, environment=env)
            completions = script.complete(adjusted_line, column)

            def _visibility(name: str) -> int:
                if name.startswith("__"):
                    return 2  # dunder / name-mangled
                if name.startswith("_"):
                    return 1  # private / internal
                return 0      # public

            completions = sorted(completions, key=lambda c: _visibility(c.name))
            return [
                {
                    "label": c.name,
                    "kind": c.type,
                    "detail": c.description or None,
                    "documentation": None,
                    "insert_text": c.name,
                }
                for c in completions
            ]

        return await asyncio.to_thread(_run)

    async def diagnose(self, code: str, context: str = "") -> list[dict]:
        """Return pyflakes diagnostics for the given code string.

        If *context* is provided (code from prior cells joined by newlines) it is
        prepended so that names/imports defined in earlier cells are recognised.
        Only diagnostics that fall within *code* (not the context) are returned,
        with row numbers relative to *code*.
        """
        def _run() -> list[dict]:
            context_line_count = context.count("\n") + 1 if context else 0
            full_code = (context + "\n" + code) if context else code

            diagnostics = []
            try:
                tree = compile(full_code, "<string>", "exec", _ast.PyCF_ONLY_AST)
            except SyntaxError as exc:
                row = (exc.lineno or 1) - context_line_count
                if row >= 1:
                    diagnostics.append({
                        "row": row,
                        "col": max(0, (exc.offset or 1) - 1),
                        "message": exc.msg,
                        "severity": "error",
                    })
                return diagnostics

            try:
                checker = pyflakes.checker.Checker(tree, "<string>")
                for msg in checker.messages:
                    row = msg.lineno - context_line_count
                    if row >= 1:
                        diagnostics.append({
                            "row": row,
                            "col": getattr(msg, "col", 0),
                            "message": msg.message % msg.message_args,
                            "severity": "warning",
                        })
            except ImportError:
                pass

            return diagnostics

        return await asyncio.to_thread(_run)


class KernelRegistry:
    def __init__(self) -> None:
        self._kernels: dict[str, KernelConnection] = {}

    async def create(self) -> KernelConnection:
        kernel_id = str(uuid.uuid4())
        conn = KernelConnection(kernel_id)
        await conn.start()
        self._kernels[kernel_id] = conn
        return conn

    def get(self, kernel_id: str) -> Optional[KernelConnection]:
        return self._kernels.get(kernel_id)

    def list(self) -> list[KernelConnection]:
        return list(self._kernels.values())

    async def delete(self, kernel_id: str) -> bool:
        conn = self._kernels.pop(kernel_id, None)
        if conn is None:
            return False
        await conn.shutdown()
        return True

    async def shutdown_all(self) -> None:
        for conn in list(self._kernels.values()):
            try:
                await conn.shutdown()
            except Exception:
                pass
        self._kernels.clear()


registry = KernelRegistry()
