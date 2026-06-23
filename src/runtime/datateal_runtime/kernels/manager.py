import ast as _ast
import asyncio
import contextlib
import logging
import os
import pathlib
import sys
import time
import uuid
from datetime import UTC, datetime

import jedi
import pyflakes.checker
from jedi.api.environment import Environment as JediEnvironment
from jupyter_client.asynchronous.client import AsyncKernelClient
from jupyter_client.kernelspec import KernelSpec
from jupyter_client.manager import AsyncKernelManager

logger = logging.getLogger(__name__)


class _DatatealKernelManager(AsyncKernelManager):
    """AsyncKernelManager that launches kernels in the configured Python environment.

    The kernel Python executable is resolved from the DATATEAL_KERNEL_PYTHON
    environment variable, falling back to the API server's own executable.
    This allows the kernel environment to be fully separated from the API
    environment (e.g. two distinct venvs in a Docker image).

    The kernel environment must have ipykernel installed.
    """

    @property
    def kernel_spec(self) -> KernelSpec:  # type: ignore[override]
        kernel_python = os.environ.get("DATATEAL_KERNEL_PYTHON", sys.executable)
        return KernelSpec(
            argv=[kernel_python, "-m", "ipykernel_launcher", "-f", "{connection_file}"],
            display_name="Datateal Python Kernel",
            language="python",
        )


class KernelConnection:
    def __init__(self, kernel_id: str) -> None:
        self.kernel_id = kernel_id
        self.km: _DatatealKernelManager = _DatatealKernelManager()
        self.kc: AsyncKernelClient | None = None
        self.status = "starting"
        self.created_at = datetime.now(UTC)
        self.last_activity = datetime.now(UTC)
        self._lock = asyncio.Lock()
        # Async execution registry: execution_id → Task
        self._execution_tasks: dict[str, asyncio.Task] = {}
        # Completed results: execution_id → raw result dict or Exception
        self._execution_results: dict[str, dict | Exception] = {}

    async def start(self) -> None:
        await self.km.start_kernel()
        self.kc = self.km.client()
        self.kc.start_channels()
        await self.kc.wait_for_ready(timeout=30)
        await self._setup_formatters()
        self.status = "idle"

    async def _wait_for_idle(self, msg_id: str, timeout: float = 10.0) -> None:
        """Drain iopub messages until status:idle is received for *msg_id*."""
        if self.kc is None:
            return
        loop = asyncio.get_running_loop()
        deadline = loop.time() + timeout
        while True:
            remaining = deadline - loop.time()
            if remaining <= 0:
                return
            try:
                msg = await asyncio.wait_for(self.kc.get_iopub_msg(), timeout=remaining)
            except TimeoutError:
                return
            if (
                msg.get("parent_header", {}).get("msg_id") == msg_id
                and msg["msg_type"] == "status"
                and msg["content"]["execution_state"] == "idle"
            ):
                return

    async def _setup_formatters(self) -> None:
        """Execute the Datateal MIME formatter registration in the kernel.

        Reads dataframe_formatter.py from the API venv and sends its source to
        the kernel process as a silent execute.  This avoids importing from the
        API venv package (which is not installed in the kernel venv).
        """
        if self.kc is None:
            return
        formatter_src = (pathlib.Path(__file__).parent / "dataframe_formatter.py").read_text()
        setup_code = formatter_src + "\nregister_formatters()\n"
        msg_id = self.kc.execute(setup_code, silent=True, store_history=False)
        await self._wait_for_idle(msg_id)

    def start_execute(self, code: str, timeout: float | None = None) -> str:
        """Queue a kernel execution and return an execution_id immediately.

        The actual execution runs in a background asyncio Task.  The kernel
        may be busy with a prior execution; the task will wait for the lock.
        Use get_execution() to poll for the result.
        """
        execution_id = str(uuid.uuid4())

        async def _run() -> None:
            async with self._lock:
                if self.kc is None:
                    self._execution_results[execution_id] = RuntimeError(
                        "Kernel client is not initialized"
                    )
                    return
                self.status = "busy"
                self.last_activity = datetime.now(UTC)
                try:
                    start = time.monotonic()
                    msg_id = self.kc.execute(code)
                    result = await self._collect_output(msg_id, timeout)
                    result["duration_ms"] = (time.monotonic() - start) * 1000
                    self._execution_results[execution_id] = result
                except Exception as exc:
                    self._execution_results[execution_id] = exc
                finally:
                    self.status = "idle"
                    self.last_activity = datetime.now(UTC)

        task = asyncio.create_task(_run())
        self._execution_tasks[execution_id] = task
        return execution_id

    def get_execution(self, execution_id: str) -> dict:
        """Return poll status for a previously started execution.

        Returns a dict suitable for serialisation as PollExecutionResponse.
        Raises KeyError if the execution_id is not found (404).
        Raises the stored exception if the execution failed.
        On first successful retrieval the result is removed from memory.
        """
        if execution_id not in self._execution_tasks:
            raise KeyError(execution_id)

        if execution_id not in self._execution_results:
            # Task is still running (or waiting for the lock).
            return {"is_complete": False}

        # Task is done — retrieve and remove from memory.
        result = self._execution_results.pop(execution_id)
        self._execution_tasks.pop(execution_id, None)

        if isinstance(result, Exception):
            raise result

        return {"is_complete": True, "result": result}

    def _cancel_all_executions(self) -> None:
        """Cancel all pending/running execution tasks and clear the registry."""
        for task in self._execution_tasks.values():
            task.cancel()
        self._execution_tasks.clear()
        self._execution_results.clear()

    async def _collect_output(self, msg_id: str, timeout: float | None) -> dict:
        if self.kc is None:
            raise RuntimeError("Kernel client is not initialized")
        outputs = []
        error = None
        execution_count = None
        loop = asyncio.get_running_loop()
        deadline = (loop.time() + timeout) if timeout is not None else None

        while True:
            if deadline is not None:
                remaining = deadline - loop.time()
                if remaining <= 0:
                    raise TimeoutError("Execution timed out")
                try:
                    msg = await asyncio.wait_for(self.kc.get_iopub_msg(), timeout=remaining)
                except TimeoutError as err:
                    raise TimeoutError("Execution timed out") from err
            else:
                msg = await self.kc.get_iopub_msg()

            if msg.get("parent_header", {}).get("msg_id") != msg_id:
                continue

            msg_type = msg["msg_type"]
            content = msg["content"]

            if msg_type == "stream":
                outputs.append({"type": "stream", "name": content["name"], "text": content["text"]})
            elif msg_type == "execute_result":
                execution_count = content.get("execution_count")
                outputs.append(
                    {
                        "type": "execute_result",
                        "data": content["data"],
                        "execution_count": execution_count,
                    }
                )
            elif msg_type == "display_data":
                outputs.append({"type": "display_data", "data": content["data"]})
            elif msg_type == "error":
                error = {
                    "ename": content["ename"],
                    "evalue": content["evalue"],
                    "traceback": content["traceback"],
                }
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
        self._cancel_all_executions()
        self.status = "restarting"
        await self.km.restart_kernel()
        if self.kc is None:
            raise RuntimeError("Kernel client is not initialized")
        await self.kc.wait_for_ready(timeout=30)
        await self._setup_formatters()
        self.status = "idle"
        self.last_activity = datetime.now(UTC)

    async def shutdown(self) -> None:
        self._cancel_all_executions()
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
        kernel_python = os.environ.get("DATATEAL_KERNEL_PYTHON", sys.executable)

        def _run() -> list[dict]:
            full_code, context_line_count = KernelConnection._build_full_code(context, code)
            adjusted_line = line + context_line_count

            total_lines = full_code.count("\n") + 1
            logger.debug(
                "complete: line=%d col=%d adj_line=%d total_lines=%d code_len=%d context_len=%d",
                line,
                column,
                adjusted_line,
                total_lines,
                len(code),
                len(context),
            )

            env = JediEnvironment(kernel_python)
            script = jedi.Script(full_code, environment=env)
            try:
                completions = script.complete(adjusted_line, column)
            except Exception:
                logger.warning("complete: script.complete() failed", exc_info=True)
                return []

            logger.debug("complete: %d raw completions", len(completions))

            def _visibility(name: str) -> int:
                if name.startswith("__"):
                    return 2  # dunder / name-mangled
                if name.startswith("_"):
                    return 1  # private / internal
                return 0  # public

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
            full_code, context_line_count = KernelConnection._build_full_code(context, code)

            diagnostics = []
            try:
                tree = compile(full_code, "<string>", "exec", _ast.PyCF_ONLY_AST)
            except SyntaxError as exc:
                row = (exc.lineno or 1) - context_line_count
                if row >= 1:
                    diagnostics.append(
                        {
                            "row": row,
                            "col": max(0, (exc.offset or 1) - 1),
                            "message": exc.msg,
                            "severity": "error",
                        }
                    )
                return diagnostics

            try:
                checker = pyflakes.checker.Checker(tree, "<string>")
                for msg in checker.messages:
                    row = msg.lineno - context_line_count
                    if row >= 1:
                        diagnostics.append(
                            {
                                "row": row,
                                "col": getattr(msg, "col", 0),
                                "message": msg.message % msg.message_args,
                                "severity": "warning",
                            }
                        )
            except ImportError:
                pass

            return diagnostics

        return await asyncio.to_thread(_run)

    # Python builtins that should get a distinct "builtin" token type.
    _PYTHON_BUILTINS = frozenset(
        [
            "abs",
            "aiter",
            "all",
            "anext",
            "any",
            "ascii",
            "bin",
            "bool",
            "breakpoint",
            "bytearray",
            "bytes",
            "callable",
            "chr",
            "classmethod",
            "compile",
            "complex",
            "copyright",
            "credits",
            "delattr",
            "dict",
            "dir",
            "display",
            "divmod",
            "enumerate",
            "eval",
            "exec",
            "exit",
            "filter",
            "float",
            "format",
            "frozenset",
            "getattr",
            "globals",
            "hasattr",
            "hash",
            "help",
            "hex",
            "id",
            "input",
            "int",
            "isinstance",
            "issubclass",
            "iter",
            "len",
            "license",
            "list",
            "locals",
            "map",
            "max",
            "memoryview",
            "min",
            "next",
            "object",
            "oct",
            "open",
            "ord",
            "pow",
            "print",
            "property",
            "quit",
            "range",
            "repr",
            "reversed",
            "round",
            "set",
            "setattr",
            "slice",
            "sorted",
            "staticmethod",
            "str",
            "sum",
            "super",
            "tuple",
            "type",
            "vars",
            "zip",
            "__import__",
            # Common constants
            "True",
            "False",
            "None",
            "NotImplemented",
            "Ellipsis",
            "__name__",
            "__doc__",
            "__file__",
            "__spec__",
        ]
    )

    # Preamble silently prepended to every analysis request (diagnostics,
    # completions, hover, semantic tokens).  It declares names that ipykernel
    # injects into the user namespace at startup so that pyflakes/Jedi do not
    # flag them as undefined and can resolve their types/signatures.
    _KERNEL_PREAMBLE = "from IPython.display import display\n"

    @classmethod
    def _build_full_code(cls, context: str, code: str) -> tuple[str, int]:
        """Prepend the kernel preamble and optional prior-cell context to *code*.

        Returns ``(full_code, context_line_count)`` where *context_line_count*
        is the total number of lines that precede the user's *code* inside
        *full_code* (preamble lines + context lines).  Callers use this offset
        to translate absolute pyflakes/Jedi line numbers back to code-relative
        ones.
        """
        preamble = cls._KERNEL_PREAMBLE
        preamble_lines = preamble.count("\n")
        if context:
            full_code = preamble + context + "\n" + code
            context_line_count = preamble_lines + context.count("\n") + 1
        else:
            full_code = preamble + code
            context_line_count = preamble_lines
        return full_code, context_line_count

    async def semantic_tokens(self, code: str, context: str = "") -> list[dict]:
        """Return semantic token classifications using Jedi (primary) with AST fallback.

        If *context* is provided it is prepended so Jedi can resolve names from
        prior cells.  Only tokens that fall within *code* are returned, with
        line numbers relative to *code*.
        """
        kernel_python = os.environ.get("DATATEAL_KERNEL_PYTHON", sys.executable)

        def _run_jedi() -> list[dict]:
            full_code, context_line_count = KernelConnection._build_full_code(context, code)

            env = JediEnvironment(kernel_python)
            script = jedi.Script(full_code, environment=env)

            tokens = []
            try:
                names = script.get_names(all_scopes=True, references=True)
            except Exception:
                logger.info(
                    "semantic_tokens: get_names() failed, falling back to AST", exc_info=True
                )
                return _run_ast()

            # Cache for resolved definition types: name_string → resolved_type
            # Used so we don't call goto() multiple times for the same name.
            _goto_cache: dict[str, str] = {}

            for name in names:
                line = name.line - context_line_count
                if line < 1:
                    continue
                col = name.column
                length = len(name.name)

                token_type = _classify_jedi_name(name, _goto_cache)
                if token_type is None:
                    continue

                tokens.append(
                    {
                        "line": line,
                        "start_char": col,
                        "length": length,
                        "token_type": token_type,
                    }
                )

            logger.debug("semantic_tokens: %d names → %d tokens", len(names), len(tokens))
            return tokens

        def _is_pascal_case(n: str) -> bool:
            """Return True if name follows PascalCase (class naming convention)."""
            return len(n) > 1 and n[0].isupper() and not n.isupper()

        # Sentinel indicating goto() was attempted but failed entirely.
        _goto_failed = "_failed_"

        def _resolve_statement_type(name, cache: dict[str, str]) -> str:
            """Resolve the actual definition type of a 'statement' name via goto().

            Jedi's get_names(references=True) returns type='statement' for
            references.  This helper calls name.goto() to find the definition
            and returns the resolved type (e.g. 'module', 'function', 'class',
            or 'statement' when the definition is a plain variable).
            Results are cached by name string to avoid redundant resolutions.
            Returns _goto_failed if resolution fails entirely.
            """
            n = name.name
            if n in cache:
                return cache[n]
            try:
                defs = name.goto()
                if defs:
                    resolved = defs[0].type
                    cache[n] = resolved
                    return resolved
            except Exception:
                pass
            cache[n] = _goto_failed
            return _goto_failed

        def _classify_jedi_name(name, goto_cache: dict[str, str]) -> str | None:
            """Map a Jedi Name object to a semantic token type."""
            n = name.name
            ntype = name.type  # function, class, module, instance, keyword, param, statement, etc.

            if ntype == "keyword":
                return None  # already handled by Monarch grammar
            if ntype == "param":
                if n in ("self", "cls"):
                    return "selfParameter"
                return "parameter"
            if ntype == "function":
                if n in KernelConnection._PYTHON_BUILTINS:
                    return "builtin"
                # PascalCase "function" calls are class constructors (e.g. MyClass())
                if _is_pascal_case(n):
                    return "class"
                return "function"
            if ntype == "class":
                return "class"
            if ntype == "module":
                return "namespace"
            if ntype == "instance":
                if n in KernelConnection._PYTHON_BUILTINS:
                    return "builtin"
                # PascalCase instances are likely class/type references that Jedi
                # couldn't fully resolve (e.g. unresolved imports).
                if _is_pascal_case(n):
                    return "class"
                return "variable"
            if ntype == "property":
                return "property"
            if ntype == "statement":
                # Decorators appear as statements sometimes
                if n.startswith("@"):
                    return "decorator"
                # Try to resolve the actual definition type via goto() FIRST.
                # This correctly differentiates builtin types (str, bool → class/teal)
                # from builtin functions (print, len → function/gold), and also
                # resolves references to modules, functions, and classes that Jedi
                # reports as 'statement' in get_names(references=True).
                resolved = _resolve_statement_type(name, goto_cache)
                if resolved == "function":
                    if n in KernelConnection._PYTHON_BUILTINS:
                        return "builtin"
                    if _is_pascal_case(n):
                        return "class"
                    return "function"
                if resolved == "class":
                    return "class"
                if resolved == "module":
                    return "namespace"
                if resolved == "param":
                    return "parameter"
                if resolved == "statement":
                    # goto() confirmed this is a plain variable assignment.
                    # Return "variable" so Monarch's 'namespace' heuristic
                    # (identifier before .method()) is correctly overridden.
                    if _is_pascal_case(n):
                        return "class"
                    return "variable"
                if resolved == "instance":
                    if _is_pascal_case(n):
                        return "class"
                    return "variable"
                if resolved == "property":
                    return "property"
                # goto() failed entirely — we don't know what this name is.
                # Return None so Monarch grammar heuristics are preserved
                # (e.g. identifier before '(' → function.call).
                if _is_pascal_case(n):
                    return "class"
                return None
            # Fallback: skip unknown types
            return None

        def _run_ast() -> list[dict]:
            """Fallback: use Python's ast module for basic classification."""
            full_code, context_line_count = KernelConnection._build_full_code(context, code)

            try:
                tree = _ast.parse(full_code)
            except SyntaxError:
                return []

            tokens = []

            for node in _ast.walk(tree):
                if isinstance(node, _ast.FunctionDef | _ast.AsyncFunctionDef):
                    line = node.lineno - context_line_count
                    if line >= 1:
                        tokens.append(
                            {
                                "line": line,
                                "start_char": node.col_offset,
                                "length": len(node.name),
                                "token_type": "function",
                            }
                        )
                    # Classify parameters
                    for arg in node.args.args + node.args.posonlyargs + node.args.kwonlyargs:
                        arg_line = arg.lineno - context_line_count
                        if arg_line >= 1:
                            tt = "selfParameter" if arg.arg in ("self", "cls") else "parameter"
                            tokens.append(
                                {
                                    "line": arg_line,
                                    "start_char": arg.col_offset,
                                    "length": len(arg.arg),
                                    "token_type": tt,
                                }
                            )
                    # Decorators
                    for dec in node.decorator_list:
                        dec_line = dec.lineno - context_line_count
                        if dec_line >= 1 and isinstance(dec, _ast.Name):
                            tokens.append(
                                {
                                    "line": dec_line,
                                    "start_char": dec.col_offset,
                                    "length": len(dec.id),
                                    "token_type": "decorator",
                                }
                            )
                elif isinstance(node, _ast.ClassDef):
                    line = node.lineno - context_line_count
                    if line >= 1:
                        tokens.append(
                            {
                                "line": line,
                                "start_char": node.col_offset,
                                "length": len(node.name),
                                "token_type": "class",
                            }
                        )
                elif isinstance(node, _ast.Name):
                    line = node.lineno - context_line_count
                    if line >= 1:
                        if node.id in KernelConnection._PYTHON_BUILTINS:
                            tokens.append(
                                {
                                    "line": line,
                                    "start_char": node.col_offset,
                                    "length": len(node.id),
                                    "token_type": "builtin",
                                }
                            )
                        elif _is_pascal_case(node.id):
                            tokens.append(
                                {
                                    "line": line,
                                    "start_char": node.col_offset,
                                    "length": len(node.id),
                                    "token_type": "class",
                                }
                            )

            return tokens

        try:
            return await asyncio.to_thread(_run_jedi)
        except Exception:
            logger.info("semantic_tokens: _run_jedi() failed, falling back to AST", exc_info=True)
            return await asyncio.to_thread(_run_ast)

    async def hover(self, code: str, line: int, column: int, context: str = "") -> list[str]:
        """Return markdown hover content for the symbol at (line, column).

        Strategy (first non-empty result wins):
        1. ``get_signatures`` — when the cursor is inside a call's parentheses.
        2. ``goto(follow_imports=True)`` — resolves the definition; best for
           compiled extensions (e.g. duckdb) where ``help()`` often returns nothing.
        3. ``infer`` — type inference; useful for variables and literals.
        4. ``help`` — last resort; works well for pure-Python identifiers.

        Returns an empty list when nothing useful can be shown.
        """
        kernel_python = os.environ.get("DATATEAL_KERNEL_PYTHON", sys.executable)

        def _format_doc(full_name: str, doc: str) -> list[str]:
            """Build a markdown hover from a fully-qualified name and docstring."""
            doc = doc.strip()
            if not doc:
                return [f"**{full_name}**"]

            lines = doc.split("\n")
            first_line = lines[0]

            # If the first line looks like a function signature (contains '('),
            # format it as a Python code block for better readability.
            if (
                "(" in first_line
                and first_line.split("(")[0].replace(".", "").replace("_", "").isalnum()
            ):
                result: list[str] = [f"```python\n{first_line}\n```"]
                rest = "\n".join(lines[1:]).strip()
            else:
                result = [f"**{full_name}**"]
                rest = doc

            if rest:
                paragraphs = rest.split("\n\n")
                snippet = "\n\n".join(paragraphs[:3])
                if len(snippet) > 1000:
                    snippet = snippet[:1000] + "…"
                result.append(snippet)
            return result

        def _run() -> list[str]:
            full_code, context_line_count = KernelConnection._build_full_code(context, code)
            adj_line = line + context_line_count
            total_lines = full_code.count("\n") + 1

            logger.debug(
                "hover: line=%d col=%d adj_line=%d total_lines=%d",
                line,
                column,
                adj_line,
                total_lines,
            )

            if adj_line < 1 or adj_line > total_lines:
                logger.warning(
                    "hover: adj_line %d out of range [1, %d] — code=%r context_tail=%r",
                    adj_line,
                    total_lines,
                    code[:100],
                    context[-100:] if context else "",
                )
                return []

            env = JediEnvironment(kernel_python)
            script = jedi.Script(full_code, environment=env)

            # ── 1. Try signatures (cursor inside call parentheses) ──
            try:
                sigs = script.get_signatures(adj_line, column)
                logger.debug("get_signatures(%d, %d) → %d", adj_line, column, len(sigs))
                if sigs:
                    sig = sigs[0]
                    params = [p.description for p in sig.params]
                    if sig.index is not None and 0 <= sig.index < len(params):
                        params[sig.index] = f"**{params[sig.index]}**"
                    sig_str = f"{sig.name}({', '.join(params)})"
                    contents: list[str] = [f"```python\n{sig_str}\n```"]
                    doc = sig.docstring(raw=True).strip()
                    if doc:
                        paragraphs = doc.split("\n\n")
                        snippet = "\n\n".join(paragraphs[:3])
                        if len(snippet) > 1000:
                            snippet = snippet[:1000] + "…"
                        contents.append(snippet)
                    return contents
            except Exception:
                logger.warning("get_signatures failed at (%d, %d)", adj_line, column, exc_info=True)

            # ── 2. Try goto (resolves to definition — best for compiled modules) ──
            try:
                defs = script.goto(
                    adj_line, column, follow_imports=True, follow_builtin_imports=True
                )
                logger.debug("goto(%d, %d) → %d defs", adj_line, column, len(defs))
                for d in defs:
                    full_name = d.full_name or d.name
                    doc = d.docstring(raw=True)
                    desc = d.description

                    if doc:
                        return _format_doc(full_name, doc)

                    # For functions/classes without a docstring, try get_type_hint()
                    # which returns the full signature including parameters and return
                    # type — even for pybind11 C extensions and local functions.
                    if d.type in ("function", "class"):
                        try:
                            sig_str = d.get_type_hint()
                        except Exception:
                            sig_str = None
                        if sig_str:
                            result = [f"```python\n{sig_str}\n```"]
                            # Also check infer for a docstring to pair with the signature
                            try:
                                for n in script.infer(adj_line, column):
                                    ndoc = n.docstring(raw=True).strip()
                                    if ndoc:
                                        # Skip the first line if it repeats the signature
                                        lines = ndoc.split("\n")
                                        if "(" in lines[0]:
                                            ndoc = "\n".join(lines[1:]).strip()
                                        if ndoc:
                                            paragraphs = ndoc.split("\n\n")
                                            snippet = "\n\n".join(paragraphs[:3])
                                            if len(snippet) > 1000:
                                                snippet = snippet[:1000] + "…"
                                            result.append(snippet)
                                        break
                            except Exception:
                                pass
                            return result
                        # Fall through if get_type_hint() returned nothing
                    elif desc and desc != full_name:
                        return [f"**{full_name}**", f"```python\n{desc}\n```"]
            except Exception:
                logger.warning("goto failed at (%d, %d)", adj_line, column, exc_info=True)

            # ── 3. Try infer (type inference) ──
            try:
                names = script.infer(adj_line, column)
                logger.debug("infer(%d, %d) → %d names", adj_line, column, len(names))
                for n in names:
                    full_name = n.full_name or n.name
                    doc = n.docstring(raw=True)
                    if doc:
                        return _format_doc(full_name, doc)
                    elif full_name:
                        return [f"**{full_name}**"]
            except Exception:
                logger.warning("infer failed at (%d, %d)", adj_line, column, exc_info=True)

            # ── 4. Try help (last resort) ──
            try:
                names = script.help(adj_line, column)
                logger.debug("help(%d, %d) → %d names", adj_line, column, len(names))
                for n in names:
                    full_name = n.full_name or n.name
                    doc = n.docstring(raw=True)
                    if doc:
                        return _format_doc(full_name, doc)
                    elif full_name:
                        return [f"**{full_name}**"]
            except Exception:
                logger.warning("help failed at (%d, %d)", adj_line, column, exc_info=True)

            return []

        try:
            return await asyncio.wait_for(asyncio.to_thread(_run), timeout=10.0)
        except TimeoutError:
            logger.warning("hover() timed out for (line=%d, col=%d)", line, column)
            return []
        except Exception:
            logger.warning("hover() failed for (line=%d, col=%d)", line, column, exc_info=True)
            return []


class KernelRegistry:
    def __init__(self) -> None:
        self._kernels: dict[str, KernelConnection] = {}

    async def create(self) -> KernelConnection:
        kernel_id = str(uuid.uuid4())
        conn = KernelConnection(kernel_id)
        await conn.start()
        self._kernels[kernel_id] = conn
        return conn

    def get(self, kernel_id: str) -> KernelConnection | None:
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
            with contextlib.suppress(Exception):
                await conn.shutdown()
        self._kernels.clear()


registry = KernelRegistry()
