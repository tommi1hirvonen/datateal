from fastapi import APIRouter, HTTPException

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
    ExecutionResult,
    KernelInfo,
    Output,
)

router = APIRouter(prefix="/kernels", tags=["kernels"])


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


@router.post("/{kernel_id}/execute", response_model=ExecutionResult)
async def execute(kernel_id: str, body: ExecuteRequest):
    conn = registry.get(kernel_id)
    if not conn:
        raise HTTPException(status_code=404, detail="Kernel not found")
    if conn.status == "dead":
        raise HTTPException(status_code=409, detail="Kernel is dead")
    try:
        result = await conn.execute(body.code, body.timeout)
        outputs = [Output(**o) for o in result["outputs"]]
        error = ErrorInfo(**result["error"]) if result["error"] else None
        return ExecutionResult(
            status=result["status"],
            execution_count=result["execution_count"],
            outputs=outputs,
            error=error,
            duration_ms=result["duration_ms"],
        )
    except TimeoutError:
        raise HTTPException(status_code=408, detail="Execution timed out")


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
