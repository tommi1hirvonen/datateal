# pipelines/ingestion/ingest_orders
# Notebook source file — stored in src/notebooks/ within the bundle.
# The workspace path (without extension) is the notebook's logical address.

import os
from datetime import date, timedelta

import duckdb

# ── Parameters ────────────────────────────────────────────────────────────────
# This cell is tagged `parameters`. When the job runs with a `run_date` parameter,
# Datateal injects an override cell immediately after this one.

run_date: str = ""  # YYYY-MM-DD; defaults to yesterday when empty

# ── Resolve run date ──────────────────────────────────────────────────────────

effective_date = date.fromisoformat(run_date) if run_date else date.today() - timedelta(days=1)
print(f"Ingesting orders for {effective_date}")

# ── Read source data ──────────────────────────────────────────────────────────

data_path = os.environ["DATA_PATH"]
source_path = f"{data_path}/raw/orders/{effective_date.strftime('%Y/%m/%d')}/*.parquet"

orders_raw = duckdb.read_parquet(source_path)
print(f"Loaded {orders_raw.count('order_id').fetchone()[0]:,} rows from {source_path}")

# ── Write to DuckLake catalog ──────────────────────────────────────────────────

# The sales_prod catalog is attached via the node pool configuration.
