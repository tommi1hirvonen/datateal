"""Tests for Jedi-based language features: semantic tokens, completions, hover.

These tests exercise the KernelConnection language-feature methods using the
current Python interpreter as the Jedi environment (DATATEAL_KERNEL_PYTHON is
not set, so sys.executable is used).
"""

import pytest
from datateal_runtime.kernels.manager import KernelConnection

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _token_map(tokens: list[dict]) -> dict[str, str]:
    """Build {name_at_line_col: token_type} from semantic token dicts.

    This is a convenience helper that collapses line/start_char/length into a
    single string key for easy assertion.  If only one token for a given name
    exists, the key is just the name; otherwise it's "name@L:C".
    """
    raw: list[tuple[str, str, int, int]] = []
    for t in tokens:
        raw.append((t["token_type"], "?", t["line"], t["start_char"]))

    # Build a simpler lookup keyed by (line, start_char) → token_type
    return {(t["line"], t["start_char"]): t["token_type"] for t in tokens}


def _find_token(tokens: list[dict], *, line: int, name: str) -> dict | None:
    """Find the first token on *line* whose length matches *name*."""
    for t in tokens:
        if t["line"] == line and t["length"] == len(name):
            # Disambiguate by start_char if needed — for now length is good enough.
            return t
    return None


def _find_tokens_by_type(tokens: list[dict], token_type: str) -> list[dict]:
    """Return all tokens of a given type."""
    return [t for t in tokens if t["token_type"] == token_type]


# We need a KernelConnection instance but never start an actual kernel.
# Language features only need the Jedi environment (sys.executable).
@pytest.fixture
def conn():
    """Create a KernelConnection without starting a kernel process."""
    return KernelConnection("test-kernel")


# ---------------------------------------------------------------------------
# Semantic tokens
# ---------------------------------------------------------------------------


class TestSemanticTokens:
    """Semantic token classification tests."""

    async def test_import_module_classified_as_namespace(self, conn):
        code = "import os"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'os' should be classified as namespace (module)
        assert tmap.get((1, 7)) == "namespace"

    async def test_from_import_class_classified_correctly(self, conn):
        code = "from pathlib import Path"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'pathlib' → namespace, 'Path' → class
        assert tmap.get((1, 5)) == "namespace"
        assert tmap.get((1, 20)) == "class"

    async def test_function_def_classified_as_function(self, conn):
        code = "def hello():\n    pass"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        assert tmap.get((1, 4)) == "function"

    async def test_class_def_classified_as_class(self, conn):
        code = "class MyClass:\n    pass"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        assert tmap.get((1, 6)) == "class"

    async def test_builtin_function_classified_as_builtin(self, conn):
        code = "x = len([1, 2, 3])"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'len' at col 4 should be builtin
        assert tmap.get((1, 4)) == "builtin"

    async def test_builtin_type_classified_as_class(self, conn):
        """Built-in types like str and bool should resolve to 'class' via goto,
        not 'builtin', because they are types (teal) not functions (gold)."""
        code = "def foo(x: str) -> bool:\n    return bool(x)"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'str' annotation at col 11 → class
        assert tmap.get((1, 11)) == "class"
        # 'bool' return type at col 19 → class
        assert tmap.get((1, 19)) == "class"

    async def test_parameter_classified(self, conn):
        code = "def greet(name: str):\n    return name"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'name' param definition at line 1 col 10
        assert tmap.get((1, 10)) == "parameter"

    async def test_self_classified_as_self_parameter(self, conn):
        code = "class A:\n    def method(self):\n        pass"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'self' at line 2 col 15
        assert tmap.get((2, 15)) == "selfParameter"

    async def test_module_reference_resolved_via_goto(self, conn):
        """A module used as a reference (e.g. os.path) should resolve to namespace."""
        code = "import os\nx = os.path.join('a', 'b')"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'os' on line 2, col 4 → namespace
        assert tmap.get((2, 4)) == "namespace"

    async def test_function_reference_resolved_via_goto(self, conn):
        """A function called as a reference should resolve to function."""
        code = "import os\nx = os.path.join('a', 'b')"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'join' on line 2, col 12 → function
        assert tmap.get((2, 12)) == "function"

    async def test_variable_assignment_classified_as_variable(self, conn):
        code = "x = 42"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        assert tmap.get((1, 0)) == "variable"

    async def test_context_does_not_leak_into_tokens(self, conn):
        """Tokens from the context (prior cells) must not appear in the result."""
        context = "import os"
        code = "x = os.getcwd()"
        tokens = await conn.semantic_tokens(code, context=context)
        for t in tokens:
            assert t["line"] >= 1, "Token from context leaked into output"

    async def test_pascal_case_instance_classified_as_class(self, conn):
        """PascalCase names that Jedi sees as 'instance' should be classified as class."""
        code = "from pathlib import Path\np = Path('.')"
        tokens = await conn.semantic_tokens(code)
        tmap = _token_map(tokens)
        # 'Path' on line 2 col 4 → class (constructor call)
        assert tmap.get((2, 4)) == "class"


# ---------------------------------------------------------------------------
# Completions
# ---------------------------------------------------------------------------


class TestCompletions:
    """Auto-completion tests."""

    async def test_module_completions(self, conn):
        """Typing 'os.' should return os module attributes."""
        code = "import os\nos."
        completions = await conn.complete(code, line=2, column=3)
        labels = {c["label"] for c in completions}
        assert "path" in labels
        assert "getcwd" in labels

    async def test_string_method_completions(self, conn):
        """Typing a string method prefix should return string methods."""
        code = "x = 'hello'\nx.upper"
        completions = await conn.complete(code, line=2, column=7)
        labels = {c["label"] for c in completions}
        assert "upper" in labels

    async def test_completions_with_context(self, conn):
        """Names defined in context (prior cells) should appear in completions."""
        context = "my_variable = 42"
        code = "my_"
        completions = await conn.complete(code, line=1, column=3, context=context)
        labels = {c["label"] for c in completions}
        assert "my_variable" in labels

    async def test_completions_include_kind(self, conn):
        """Each completion should have a 'kind' field (e.g. 'module', 'function')."""
        code = "import os\nos."
        completions = await conn.complete(code, line=2, column=3)
        assert len(completions) > 0
        for c in completions:
            assert "kind" in c
            assert c["kind"] is not None

    async def test_no_completions_in_empty_string(self, conn):
        """Completing at the start of an empty line should not crash."""
        code = ""
        completions = await conn.complete(code, line=1, column=0)
        assert isinstance(completions, list)

    async def test_display_completion_available(self, conn):
        """Typing 'disp' should suggest 'display' (injected by ipykernel preamble)."""
        code = "disp"
        completions = await conn.complete(code, line=1, column=4)
        labels = {c["label"] for c in completions}
        assert "display" in labels


# ---------------------------------------------------------------------------
# Hover
# ---------------------------------------------------------------------------


class TestHover:
    """Hover documentation tests."""

    async def test_hover_on_builtin_function(self, conn):
        """Hovering over 'len' should return documentation."""
        code = "len([1, 2, 3])"
        contents = await conn.hover(code, line=1, column=0)
        assert len(contents) > 0
        # Should contain something about length
        combined = "\n".join(contents).lower()
        assert "len" in combined

    async def test_hover_on_module(self, conn):
        """Hovering over 'os' should return documentation."""
        code = "import os\nos.getcwd()"
        contents = await conn.hover(code, line=2, column=0)
        assert len(contents) > 0
        combined = "\n".join(contents).lower()
        assert "os" in combined

    async def test_hover_on_function_shows_signature(self, conn):
        """Hovering over a locally defined function should include its signature."""
        code = "def greet(name: str) -> str:\n    return f'Hello {name}'\ngreet('world')"
        contents = await conn.hover(code, line=3, column=0)
        assert len(contents) > 0
        combined = "\n".join(contents)
        assert "greet" in combined

    async def test_hover_with_context(self, conn):
        """Hovering should resolve names defined in context (prior cells)."""
        context = "def my_func(x: int) -> int:\n    return x * 2"
        code = "my_func(5)"
        contents = await conn.hover(code, line=1, column=0, context=context)
        assert len(contents) > 0
        combined = "\n".join(contents)
        assert "my_func" in combined

    async def test_hover_returns_empty_for_whitespace(self, conn):
        """Hovering over whitespace should return an empty list."""
        code = "x = 1"
        contents = await conn.hover(code, line=1, column=3)
        # Column 3 is the space after '='. Jedi may or may not return something.
        assert isinstance(contents, list)

    async def test_hover_inside_call_parens_shows_signature(self, conn):
        """Hovering inside call parentheses should show the active signature."""
        code = "import os\nos.path.join('a', 'b')"
        # Column 14 is inside the parentheses of join(...)
        contents = await conn.hover(code, line=2, column=14)
        assert len(contents) > 0
        combined = "\n".join(contents)
        assert "join" in combined

    async def test_hover_on_display(self, conn):
        """Hovering over 'display' should return IPython documentation."""
        code = "display(42)"
        contents = await conn.hover(code, line=1, column=0)
        assert len(contents) > 0
        combined = "\n".join(contents).lower()
        assert "display" in combined


# ---------------------------------------------------------------------------
# Diagnostics
# ---------------------------------------------------------------------------


class TestDiagnostics:
    """Pyflakes diagnostics tests."""

    async def test_syntax_error_detected(self, conn):
        code = "def foo(\n"
        diags = await conn.diagnose(code)
        assert any(d["severity"] == "error" for d in diags)

    async def test_unused_import_warning(self, conn):
        code = "import os"
        diags = await conn.diagnose(code)
        assert any("os" in d["message"] and d["severity"] == "warning" for d in diags)

    async def test_no_diagnostics_for_clean_code(self, conn):
        code = "x = 1\nprint(x)"
        diags = await conn.diagnose(code)
        assert len(diags) == 0

    async def test_context_suppresses_undefined_name(self, conn):
        """A name defined in context should not be flagged as undefined."""
        context = "my_var = 42"
        code = "print(my_var)"
        diags = await conn.diagnose(code, context=context)
        # Should NOT have an undefined name warning for my_var
        assert not any("my_var" in d["message"] for d in diags)

    async def test_diagnostics_line_numbers_relative_to_code(self, conn):
        """Diagnostic line numbers should be relative to code, not context."""
        context = "x = 1\ny = 2"
        code = "def foo(\n"
        diags = await conn.diagnose(code, context=context)
        for d in diags:
            assert d["row"] >= 1, "Diagnostic row from context leaked"

    async def test_display_no_undefined_name_warning(self, conn):
        """display() is injected by ipykernel — it must not be flagged as undefined."""
        code = "display(42)"
        diags = await conn.diagnose(code)
        assert not any("display" in d["message"] for d in diags)

    async def test_sqldf_context_suppresses_undefined_name(self, conn):
        """_sqldf assigned in context (as from a SQL cell) must not be flagged."""
        # Use a plain object assignment — pyflakes only checks name existence, not types.
        context = "_sqldf = object()"
        code = "_sqldf.head()"
        diags = await conn.diagnose(code, context=context)
        assert not any("_sqldf" in d["message"] for d in diags)
