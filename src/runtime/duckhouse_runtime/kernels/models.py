from datetime import datetime
from typing import Any, Optional

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
    timeout: Optional[float] = None  # None means no timeout


class Output(BaseModel):
    type: str
    name: Optional[str] = None
    text: Optional[str] = None
    data: Optional[dict[str, Any]] = None
    execution_count: Optional[int] = None


class ErrorInfo(BaseModel):
    ename: str
    evalue: str
    traceback: list[str]


class ExecutionResult(BaseModel):
    status: str
    execution_count: Optional[int] = None
    outputs: list[Output]
    error: Optional[ErrorInfo] = None
    duration_ms: float


class PollExecutionResponse(BaseModel):
    is_complete: bool
    result: Optional[ExecutionResult] = None


class CompleteRequest(BaseModel):
    code: str
    line: int   # 1-based (Jedi convention)
    column: int  # 0-based (Jedi convention)
    context: str = ""  # code from all prior cells joined by newlines


class CompletionItem(BaseModel):
    label: str
    kind: str  # jedi type: function, module, class, instance, keyword, etc.
    detail: Optional[str] = None
    documentation: Optional[str] = None
    insert_text: str


class CompleteResponse(BaseModel):
    items: list[CompletionItem]


class DiagnoseRequest(BaseModel):
    code: str
    context: str = ""  # code from all prior cells joined by newlines


class Diagnostic(BaseModel):
    row: int     # 1-based
    col: int     # 0-based
    message: str
    severity: str  # error, warning


class DiagnoseResponse(BaseModel):
    diagnostics: list[Diagnostic]
