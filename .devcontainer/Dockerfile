FROM mcr.microsoft.com/devcontainers/dotnet

RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql postgresql-client \
    && rm -rf /var/lib/apt/lists/*
