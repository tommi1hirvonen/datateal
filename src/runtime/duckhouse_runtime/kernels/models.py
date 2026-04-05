from datetime import datetime
from typing import Any, Optional

from pydantic import BaseModel


class KernelInfo(BaseModel):
    id: str
    status: str
    created_at: datetime
    last_activity: datetime


class ExecuteRequest(BaseModel):
    code: str
    timeout: float = 60.0


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
