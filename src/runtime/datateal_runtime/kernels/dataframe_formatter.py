"""IPython MIME formatter for structured DataFrame output.

Registers a custom formatter for ``application/vnd.datateal.dataframe+json``
that fires whenever a pandas DataFrame or DuckDB relation is displayed as a
kernel output The formatter produces a compact JSON-serialisable dict:

    {
        "columns": [{"name": "col", "type": "number"}, ...],
        "rows":    [[1.0, "a"], [2.0, "b"], ...],
        "total_rows": 10000,
        "displayed_rows": 500
    }

The cap is hardcoded to _ROW_CAP (10 000) When ``displayed_rows`` equals
_ROW_CAP the UI treats the count as "10 000+" because the DataFrame may have
been larger.
"""

from __future__ import annotations

import duckdb
import pandas as pd
from IPython.core.formatters import BaseFormatter
from IPython.core.getipython import get_ipython

_ROW_CAP = 10_000
MIME_TYPE = "application/vnd.datateal.dataframe+json"


def _dtype_to_type(dtype) -> str:
    name = str(dtype)
    if name.startswith(("int", "uint")):
        return "integer"
    if name.startswith("float"):
        return "number"
    if name.startswith("datetime"):
        return "datetime"
    if name == "bool":
        return "boolean"
    return "string"


def _format_df(df) -> dict:
    total_rows = len(df)
    display_df = df.iloc[:_ROW_CAP] if total_rows > _ROW_CAP else df

    columns = [
        {"name": str(col), "type": _dtype_to_type(display_df[col].dtype)}
        for col in display_df.columns
    ]

    # Convert to Python-native types so the IPython JSON encoder can handle them
    rows = [
        [None if pd.isna(v) else v.item() if hasattr(v, "item") else v for v in row]
        for row in display_df.itertuples(index=False, name=None)
    ]

    return {
        "columns": columns,
        "rows": rows,
        "total_rows": total_rows,
        "displayed_rows": len(display_df),
    }


def register_formatters() -> None:
    """Register the Datateal DataFrame MIME formatter with the running IPython kernel."""

    ip = get_ipython()
    if ip is None:
        return

    class _DatatealFormatter(BaseFormatter):
        format_type = MIME_TYPE
        print_method = "_repr_datateal_"

        def __call__(self, obj):
            try:
                if isinstance(obj, pd.DataFrame):
                    return _format_df(obj)
            except ImportError:
                pass

            try:
                if isinstance(obj, duckdb.DuckDBPyRelation):
                    return _format_df(obj.df())
            except ImportError:
                pass

            return None

    formatter = _DatatealFormatter()
    ip.display_formatter.formatters[MIME_TYPE] = formatter
