from datetime import datetime
from typing import Any

from pydantic import BaseModel


class KernelInfo(BaseModel):
    id: str
    status: str
    created_at: datetime
    last_activity: datetime


class ExecutionHandle(BaseModel):
    execution_id: str


class ExecuteRequest(BaseModel):
    code: str
    timeout: float | None = None  # None means no timeout


class Output(BaseModel):
    type: str
    name: str | None = None
    text: str | None = None
    data: dict[str, Any] | None = None
    execution_count: int | None = None


class ErrorInfo(BaseModel):
    ename: str
    evalue: str
    traceback: list[str]


class ExecutionResult(BaseModel):
    status: str
    execution_count: int | None = None
    outputs: list[Output]
    error: ErrorInfo | None = None
    duration_ms: float


class PollExecutionResponse(BaseModel):
    is_complete: bool
    result: ExecutionResult | None = None


class CompleteRequest(BaseModel):
    code: str
    line: int  # 1-based (Jedi convention)
    column: int  # 0-based (Jedi convention)
    context: str = ""  # code from all prior cells joined by newlines


class CompletionItem(BaseModel):
    label: str
    kind: str  # jedi type: function, module, class, instance, keyword, etc.
    detail: str | None = None
    documentation: str | None = None
    insert_text: str


class CompleteResponse(BaseModel):
    items: list[CompletionItem]


class DiagnoseRequest(BaseModel):
    code: str
    context: str = ""  # code from all prior cells joined by newlines


class Diagnostic(BaseModel):
    row: int  # 1-based
    col: int  # 0-based
    message: str
    severity: str  # error, warning


class DiagnoseResponse(BaseModel):
    diagnostics: list[Diagnostic]


class SemanticTokenRequest(BaseModel):
    code: str
    context: str = ""  # code from all prior cells joined by newlines


class SemanticToken(BaseModel):
    line: int  # 1-based
    start_char: int  # 0-based
    length: int
    # Expected values: function, class, parameter, variable, builtin,
    # selfParameter, property, decorator, namespace
    token_type: str


class SemanticTokenResponse(BaseModel):
    tokens: list[SemanticToken]


class HoverRequest(BaseModel):
    code: str
    line: int  # 1-based (Jedi convention)
    column: int  # 0-based (Jedi convention)
    context: str = ""  # code from all prior cells joined by newlines


class HoverResponse(BaseModel):
    contents: list[str]  # markdown strings; empty means nothing to show
