#!/bin/bash
set -e

KERNEL_PIP=/opt/venvs/kernel/bin/pip
REQUIREMENTS_FILE=/etc/duckhouse/kernel-requirements.txt

if [ -f "$REQUIREMENTS_FILE" ]; then
    echo "Installing kernel packages from $REQUIREMENTS_FILE"
    "$KERNEL_PIP" install --no-cache-dir -r "$REQUIREMENTS_FILE"
fi

if [ -n "$KERNEL_PACKAGES" ]; then
    echo "Installing kernel packages from KERNEL_PACKAGES env var"
    "$KERNEL_PIP" install --no-cache-dir $KERNEL_PACKAGES
fi

if find /etc/wheels -name "*.whl" -type f 2>/dev/null | grep -q .; then
    echo "Installing custom wheel packages from /etc/wheels/"
    find /etc/wheels -name "*.whl" -type f -exec "$KERNEL_PIP" install --no-cache-dir {} +
fi

exec /opt/venvs/api/bin/duckhouse-runtime
