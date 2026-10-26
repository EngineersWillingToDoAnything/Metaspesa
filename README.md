# Metaspesa

Metaspesa manages shopping lists and tracks supermarket product prices.

- `client/`: Next.js/React frontend.
- `server/`: .NET REST API and PostgreSQL persistence.
- `scraper/`: Python worker for supermarket catalogue ingestion.

Feature requests and planned work are tracked in [GitHub issues](https://github.com/gianluisdiana/Metaspesa/issues).

## Observability

The local observability stack collects OpenTelemetry logs, metrics, and traces with
Alloy and stores them in Loki, Mimir, and Tempo. Grafana is provisioned with the
matching data sources, dashboards, alert, and trace-to-log correlation.

Telemetry uses authenticated TLS by default, with two Alloy collectors behind
a health-checked endpoint. Observability now runs as its own Compose project;
the default application stack does not start LGTM or require telemetry secrets.
Metaspesa dashboards, alerts, PostgreSQL datasource and rules live in `telemetry/`.
See [the application telemetry guide](telemetry/README.md) to opt into telemetry.
The observability platform is maintained in a separate repository; use that
checkout's README for standalone deployment and certificate management.

## Run locally

Prerequisites: Docker with Compose 2.24 or newer.

Copy `.env.example` to `.env` and replace its password placeholders. Create
`secrets/jwt_secret_key` (a random signing key of at least 32 bytes),
`secrets/scraper_username`, and `secrets/scraper_password` with your local values.
Keep these files private; they are ignored by Git. Preserve existing values.

```sh
docker compose up -d db
```

No collector endpoint is configured in this default mode: client and scraper
exporters stay off, and the server/migration runner register no OTLP exporter.
Application JWT and scraper credentials remain in root `secrets/`. The read-only
Grafana database role remains application-owned; `GRAFANA_DB_PASSWORD` is its
password, not the Grafana administrator password.
Run migrations explicitly for a fresh database or a release with schema changes:

```sh
docker compose run --rm --build migrations
```

Start the API and client:

```sh
docker compose up --build -d server client
```

Before starting catalogue ingestion, create any missing Markets and Brands;
ingestion does not create them automatically. Register a dedicated account and
assign it the `CatalogAdmin` role in PostgreSQL (`identity.users.role_id`, using
the role ID from `identity.roles`).

After assigning the role, request a fresh token with
`POST /api/v1/auth/tokens` and `{"username":"...","password":"..."}`.
Send the returned `accessToken` as `Authorization: Bearer <accessToken>` to:

- `POST /api/v1/markets` with `{"name":"Mercadona","logoUrl":null}`.
- `POST /api/v1/brands` with `{"name":"Hacendado"}`.

Then start the scraper:

```sh
docker compose up --build -d scraper
```
The API waits for a healthy database, not the migration runner. Open the client at
http://localhost:3000 and the API at http://localhost:4001.

```sh
docker compose down
```

## Optional telemetry

Prepare the separate observability checkout's `.env` and certificates according
to its README. Set `OBSERVABILITY_SECRETS_DIR` in this project's `.env` to that
checkout's secrets directory. Follow the
[telemetry setup instructions](telemetry/README.md#enable-telemetry) to select
the checkout and set the absolute Metaspesa asset path before starting both
projects. Then run from this repository's root:

```sh
docker compose --env-file .env --env-file "${observabilityDir}/.env" -f "${observabilityDir}/compose.yaml" -f telemetry/compose.observability.yaml up -d
docker compose -f compose.yaml -f telemetry/compose.yaml up --build -d
```

Observability owns the shared network and starts first. The application adapter
attaches services and database without starting or owning any LGTM services.
Grafana is at http://localhost:3001 (`admin`, password from the observability
checkout's `.env`, unless its deployment settings customize these defaults).
Both projects stop independently; see the telemetry guide for matching shutdown
commands and switching back to application-only mode.

See the separate observability checkout's README for label policy, batching
and memory limits, trace retention, Mimir recording rules, secure telemetry
transport, certificate renewal, HA behavior, and validation procedures.
