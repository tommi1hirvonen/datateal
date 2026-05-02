import logging

from fastapi import APIRouter, Depends, HTTPException

from duckhouse_runtime.api.auth import verify_api_key
from duckhouse_runtime.kernels.manager import KernelConnection, registry
from duckhouse_runtime.kernels.models import (
    CompleteRequest,
    CompleteResponse,
    CompletionItem,
    DiagnoseRequest,
    DiagnoseResponse,
    Diagnostic,
    ErrorInfo,
    ExecuteRequest,
    ExecutionHandle,
    ExecutionResult,
    HoverRequest,
    HoverResponse,
    KernelInfo,
    Output,
    PollExecutionResponse,
    SemanticToken,
    SemanticTokenRequest,
    SemanticTokenResponse,
)

router = APIRouter(prefix="/kernels", tags=["kernels"], dependencies=[Depends(verify_api_key)])
logger = logging.getLogger(__name__)


def _to_info(conn: KernelConnection) -> KernelInfo:
    return KernelInfo(
        id=conn.kernel_id,
        status=conn.status,
        created_at=conn.created_at,
        last_activity=conn.last_activity,
    )


@router.post("", response_model=KernelInfo, status_code=201)
async def create_kernel():
    conn = await registry.create()
    return _to_info(conn)


@router.get("", response_model=list[KernelInfo])
def list_kernels():
    return [_to_info(c) for c in registry.list()]


@router.get("/{kernel_id}", response_model=KernelInfo)
def get_kernel(kernel_id: str):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    return _to_info(conn)


@router.delete("/{kernel_id}", status_code=204)
async def delete_kernel(kernel_id: str):
    if not await registry.delete(kernel_id):
        raise HTTPException(status_code=404, detail="Kernel not found")


@router.post("/{kernel_id}/execute", response_model=ExecutionHandle, status_code=202)
async def execute(kernel_id: str, body: ExecuteRequest):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    if conn.status == "dead":
        raise HTTPException(status_code=409, detail="Kernel is dead")
    execution_id = conn.start_execute(body.code, body.timeout)
    return ExecutionHandle(execution_id=execution_id)


@router.get("/{kernel_id}/executions/{execution_id}", response_model=PollExecutionResponse)
def poll_execution(kernel_id: str, execution_id: str):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    try:
        poll = conn.get_execution(execution_id)
    except KeyError:
        raise HTTPException(status_code=404, detail="Execution not found")
    except TimeoutError:
        raise HTTPException(status_code=408, detail="Execution timed out")
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc))

    if not poll["is_complete"]:
        return PollExecutionResponse(is_complete=False)

    raw = poll["result"]
    outputs = [Output(**o) for o in raw["outputs"]]
    error = ErrorInfo(**raw["error"]) if raw["error"] else None
    result = ExecutionResult(
        status=raw["status"],
        execution_count=raw["execution_count"],
        outputs=outputs,
        error=error,
        duration_ms=raw["duration_ms"],
    )
    return PollExecutionResponse(is_complete=True, result=result)


@router.post("/{kernel_id}/restart", response_model=KernelInfo)
async def restart_kernel(kernel_id: str):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    await conn.restart()
    return _to_info(conn)


@router.post("/{kernel_id}/interrupt", status_code=204)
async def interrupt_kernel(kernel_id: str):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    await conn.interrupt()


@router.post("/{kernel_id}/completions", response_model=CompleteResponse)
async def complete(kernel_id: str, body: CompleteRequest):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    items = await conn.complete(body.code, body.line, body.column, body.context)
    return CompleteResponse(items=[CompletionItem(**i) for i in items])


@router.post("/{kernel_id}/diagnostics", response_model=DiagnoseResponse)
async def diagnose(kernel_id: str, body: DiagnoseRequest):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    diagnostics = await conn.diagnose(body.code, body.context)
    return DiagnoseResponse(diagnostics=[Diagnostic(**d) for d in diagnostics])


@router.post("/{kernel_id}/semantic-tokens", response_model=SemanticTokenResponse)
async def semantic_tokens(kernel_id: str, body: SemanticTokenRequest):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    tokens = await conn.semantic_tokens(body.code, body.context)
    return SemanticTokenResponse(tokens=[SemanticToken(**t) for t in tokens])


@router.post("/{kernel_id}/hover", response_model=HoverResponse)
async def hover(kernel_id: str, body: HoverRequest):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    contents = await conn.hover(body.code, body.line, body.column, body.context)
    logger.info("hover kernel=%s line=%d col=%d → %d content items", kernel_id, body.line, body.column, len(contents))
    return HoverResponse(contents=contents)
