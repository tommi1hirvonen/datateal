FROM python:3.14-slim

# ── uv ────────────────────────────────────────────────────────────────────────
COPY --from=ghcr.io/astral-sh/uv:latest /uv /usr/local/bin/uv

# ── API environment ──────────────────────────────────────────────────────────
RUN python -m venv /opt/venvs/api

COPY dist/*.whl /tmp/wheels/
RUN uv pip install --no-cache --python /opt/venvs/api/bin/python /tmp/wheels/*.whl \
    && rm -rf /tmp/wheels

# ── Kernel environment ───────────────────────────────────────────────────────
# A separate Python environment for user kernels, isolated from the API.
# Only ipykernel is required here as a base. Additional packages can be
# supplied at build time (KERNEL_PACKAGES build arg), at runtime via the
# KERNEL_PACKAGES environment variable, or via a requirements.txt file
# mounted at /etc/datateal/kernel-requirements.txt.
RUN python -m venv /opt/venvs/kernel \
    && uv pip install --no-cache --python /opt/venvs/kernel/bin/python ipykernel==7.2.0 duckdb==1.5.2 numpy==2.4.4 pandas==3.0.2

# ── Entrypoint ───────────────────────────────────────────────────────────────
COPY docker/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN sed -i 's/\r$//' /usr/local/bin/entrypoint.sh \
    && chmod +x /usr/local/bin/entrypoint.sh

# ── Runtime configuration ────────────────────────────────────────────────────
ENV DATATEAL_KERNEL_PYTHON=/opt/venvs/kernel/bin/python

RUN useradd -r -m -s /bin/false datateal \
    && chown -R datateal:datateal /opt/venvs
USER datateal

EXPOSE 8000

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
