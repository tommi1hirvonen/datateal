import logging
from contextlib import asynccontextmanager

import uvicorn
from fastapi import FastAPI

from datateal_runtime.api import health, kernels
from datateal_runtime.kernels.manager import registry

logging.basicConfig(level=logging.INFO)


@asynccontextmanager
async def lifespan(_: FastAPI):
    yield
    await registry.shutdown_all()


app = FastAPI(title="Datateal Runtime", version="2026.0.1", lifespan=lifespan)

app.include_router(health.router)
app.include_router(kernels.router)


def run():
    uvicorn.run("datateal_runtime.main:app", host="0.0.0.0", port=8000, reload=False)
