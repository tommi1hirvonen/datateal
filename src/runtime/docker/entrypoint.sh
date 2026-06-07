#!/bin/bash
set -e

KERNEL_PYTHON=/opt/venvs/kernel/bin/python
REQUIREMENTS_FILE=/etc/datateal/kernel-requirements.txt

if [ -f "$REQUIREMENTS_FILE" ]; then
    echo "Installing kernel packages from $REQUIREMENTS_FILE"
    uv pip install --no-cache --only-binary :all: --python "$KERNEL_PYTHON" -r "$REQUIREMENTS_FILE"
fi

if [ -n "$KERNEL_PACKAGES" ]; then
    echo "Installing kernel packages from KERNEL_PACKAGES env var"
    set -f
    uv pip install --no-cache --only-binary :all: --python "$KERNEL_PYTHON" $KERNEL_PACKAGES
    set +f
fi

if find /etc/wheels -name "*.whl" -type f 2>/dev/null | grep -q .; then
    echo "Installing custom wheel packages from /etc/wheels/"
    find /etc/wheels -name "*.whl" -type f -exec uv pip install --no-cache --only-binary :all: --python "$KERNEL_PYTHON" {} +
fi

exec /opt/venvs/api/bin/datateal-runtime
