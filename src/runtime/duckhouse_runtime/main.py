from contextlib import asynccontextmanager
import logging
from fastapi import FastAPI
import uvicorn
from duckhouse_runtime.api import health, kernels
from duckhouse_runtime.kernels.manager import registry

logging.basicConfig(level=logging.INFO)

@asynccontextmanager
async def lifespan(_: FastAPI):
    yield
    await registry.shutdown_all()


app = FastAPI(title="DuckHouse Runtime", version="2026.0.1", lifespan=lifespan)

app.include_router(health.router)
app.include_router(kernels.router)


def run():
    uvicorn.run("duckhouse_runtime.main:app", host="0.0.0.0", port=8000, reload=False)
