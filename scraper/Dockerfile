FROM python:3.14.10-slim AS builder

COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/

WORKDIR /app

COPY pyproject.toml uv.lock ./

RUN uv sync --frozen --no-dev --no-install-project

FROM python:3.14.10-slim

WORKDIR /app

COPY --from=builder /app/.venv /app/.venv

ENV PATH="/app/.venv/bin:$PATH"

RUN playwright install --with-deps firefox \
    && rm -rf /var/lib/apt/lists/*

COPY . .

ENTRYPOINT [ "python", "./src/main.py" ]