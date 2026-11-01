# EcoStream AI / ClimateGuard

**[English](#-english)** · **[Português](#-português)**

---

## 🇺🇸 English

Real-time climate telemetry monitoring and emergency response platform. It detects severe anomalies (heavy rain, wind gusts, pressure drops), runs geospatial analysis on PostGIS and triggers a multi-agent system (Semantic Kernel + MCP + RAG) that produces contingency reports and dispatches multichannel alerts.

### Architecture

```
 INMET / Open-Meteo
        │  (Polly)
        ▼
 Ingestion.Worker ──► Application (CQRS/MediatR) ──► PostgreSQL + PostGIS
        │                       │
        │                       └── AnomalyDetected ──► RabbitMQ (MassTransit)
        ▼                                                   │
     Api (REST) ◄── React + Leaflet + Recharts              ▼
                                                    Agents.Worker (Semantic Kernel)
                                         ┌──────────────┼──────────────┬──────────────┐
                                    Data Analyst    RAG & Protocols    Risk Decider  Multichannel
                                    (MCP ─►         (Qdrant)           (1–5)         Communicator
                                    McpServer/PostGIS)                               Telegram/Twilio/Slack
```
Everything is instrumented with OpenTelemetry and exported to Jaeger.

### Solution structure

```
clima/
├── EcoStream.slnx                 # solution (.slnx — XML format, .NET 9.0.200+)
├── Directory.Build.props          # net9.0, nullable, analyzers
├── Directory.Packages.props       # Central Package Management (versions in one place)
├── docker-compose.yml             # PostGIS, RabbitMQ, Qdrant, Jaeger, (Ollama)
├── infra/postgres/init/           # PostGIS extensions on first start
├── infra/seed/porto-alegre/       # (approximate) GeoJSON for stations, neighborhoods, rivers, risk zones
├── src/
│   ├── Core/
│   │   ├── EcoStream.Domain/          # entities, value objects, domain events (zero dependencies)
│   │   └── EcoStream.Application/     # MediatR commands/queries, validation, ports (interfaces)
│   ├── Contracts/
│   │   └── EcoStream.Contracts/       # integration events carried over RabbitMQ
│   ├── Infrastructure/
│   │   ├── EcoStream.Infrastructure/  # EF Core + PostGIS, MassTransit, Polly, OpenTelemetry
│   │   ├── EcoStream.AI/              # Semantic Kernel agents, RAG/Qdrant (Phase 3)
│   │   └── EcoStream.Notifications/   # Telegram, Twilio, Slack/Teams (Phase 4)
│   ├── Presentation/
│   │   ├── EcoStream.Api/             # ASP.NET Core Minimal API
│   │   └── EcoStream.McpServer/       # MCP server exposing PostGIS tools (Phase 3)
│   ├── Tools/
│   │   └── EcoStream.DbMigrator/      # applies migrations + imports GeoJSON from infra/seed/
│   └── Workers/
│       ├── EcoStream.Ingestion.Worker/  # polls telemetry providers
│       └── EcoStream.Agents.Worker/     # RabbitMQ consumers → multi-agent pipeline
├── tests/
│   ├── EcoStream.Domain.Tests/
│   ├── EcoStream.Application.Tests/
│   └── EcoStream.Infrastructure.Tests/
├── web/                           # React + TS + Vite + Tailwind v4
│   └── src/
│       ├── components/            # generic UI
│       ├── features/map/          # risk map (Leaflet)
│       ├── features/telemetry/    # charts (Recharts)
│       └── lib/                   # types, API client
├── docs/ROADMAP.md
└── .github/workflows/ci.yml
```

**Dependency rule:** `Domain` ← `Application` ← `Infrastructure` ← hosts (`Api`, `McpServer`, `Workers`). The domain knows nothing about EF, NetTopologySuite or MassTransit.

### Getting started

Prerequisites: .NET SDK 9.0.300+, Docker, Node 20+.

```bash
cp .env.example .env
docker compose up -d                                   # PostGIS, RabbitMQ, Qdrant, Jaeger
dotnet tool restore                                    # dotnet-ef (local manifest)

dotnet run --project src/Tools/EcoStream.DbMigrator    # migrations + geospatial seed (Porto Alegre)
dotnet run --project src/Presentation/EcoStream.Api    # http://localhost:5080
dotnet run --project src/Workers/EcoStream.Ingestion.Worker   # polls Open-Meteo every 5 min
dotnet run --project src/Workers/EcoStream.Agents.Worker      # consumes AnomalyDetected

cd web && npm install && npm run dev                   # http://localhost:5173
```

Tests: `dotnet test EcoStream.slnx`. Sample requests: `src/Presentation/EcoStream.Api/EcoStream.Api.http`.

#### Endpoints (Phase 1)
| Method | Route | Description |
|---|---|---|
| GET | `/api/stations` | Registered stations |
| GET | `/api/telemetry/latest` | Latest reading per station |
| GET | `/api/stations/{code}/readings?from&to` | Time series (default: last 24h, max 31 days) |
| GET | `/api/geo/neighborhoods/near?lat&lon&radiusKm` | Neighborhoods within radius (`ST_DWithin`) + flood-zone flag |
| POST | `/api/telemetry` | Manual reading (idempotent per station + timestamp) |

#### Migrations
```bash
dotnet ef migrations add <Name> -p src/Infrastructure/EcoStream.Infrastructure -o Persistence/Migrations
dotnet ef migrations script -p src/Infrastructure/EcoStream.Infrastructure   # review SQL
```

| Service | URL |
|---|---|
| RabbitMQ Management | http://localhost:15672 (ecostream / ecostream) |
| Qdrant Dashboard | http://localhost:6333/dashboard |
| Jaeger UI | http://localhost:16686 |

### Licensing decisions
MediatR ≥ 13 and MassTransit ≥ 9 moved to commercial licenses. This project is pinned to **MediatR 12.x** and **MassTransit 8.x** (Apache-2.0).

See the full plan in [docs/ROADMAP.md](docs/ROADMAP.md) (Portuguese).

---

## 🇧🇷 Português

Plataforma de monitoramento de telemetria climática e resposta a emergências em tempo real. Detecta anomalias severas (chuva, rajadas, queda de pressão), faz análise geoespacial no PostGIS e aciona um sistema multiagente (Semantic Kernel + MCP + RAG) que gera relatórios de contingência e dispara alertas multicanal.

### Arquitetura

```
 INMET / Open-Meteo
        │  (Polly)
        ▼
 Ingestion.Worker ──► Application (CQRS/MediatR) ──► PostgreSQL + PostGIS
        │                       │
        │                       └── AnomalyDetected ──► RabbitMQ (MassTransit)
        ▼                                                   │
     Api (REST) ◄── React + Leaflet + Recharts              ▼
                                                    Agents.Worker (Semantic Kernel)
                                         ┌──────────────┼──────────────┬──────────────┐
                                    Analista de     RAG & Protocolos   Decisor de    Comunicador
                                    Dados (MCP ─►   (Qdrant)           Risco (1–5)   Multicanal
                                    McpServer/PostGIS)                               Telegram/Twilio/Slack
```
Tudo instrumentado com OpenTelemetry e enviado ao Jaeger.

### Estrutura da solução

```
clima/
├── EcoStream.slnx                 # solução (.slnx — formato XML do .NET 9.0.200+)
├── Directory.Build.props          # net9.0, nullable, analyzers
├── Directory.Packages.props       # Central Package Management (versões num só lugar)
├── docker-compose.yml             # PostGIS, RabbitMQ, Qdrant, Jaeger, (Ollama)
├── infra/postgres/init/           # extensões PostGIS na primeira subida
├── infra/seed/porto-alegre/       # GeoJSON (aproximado) de estações, bairros, rios, zonas de risco
├── src/
│   ├── Core/
│   │   ├── EcoStream.Domain/          # entidades, value objects, eventos de domínio (zero dependências)
│   │   └── EcoStream.Application/     # comandos/queries MediatR, validação, portas (interfaces)
│   ├── Contracts/
│   │   └── EcoStream.Contracts/       # eventos de integração trafegados no RabbitMQ
│   ├── Infrastructure/
│   │   ├── EcoStream.Infrastructure/  # EF Core + PostGIS, MassTransit, Polly, OpenTelemetry
│   │   ├── EcoStream.AI/              # agentes Semantic Kernel, RAG/Qdrant (Fase 3)
│   │   └── EcoStream.Notifications/   # Telegram, Twilio, Slack/Teams (Fase 4)
│   ├── Presentation/
│   │   ├── EcoStream.Api/             # ASP.NET Core Minimal API
│   │   └── EcoStream.McpServer/       # servidor MCP expondo tools PostGIS (Fase 3)
│   ├── Tools/
│   │   └── EcoStream.DbMigrator/      # aplica migrations + importa GeoJSON de infra/seed/
│   └── Workers/
│       ├── EcoStream.Ingestion.Worker/  # polling das fontes de telemetria
│       └── EcoStream.Agents.Worker/     # consumers RabbitMQ → pipeline multiagente
├── tests/
│   ├── EcoStream.Domain.Tests/
│   ├── EcoStream.Application.Tests/
│   └── EcoStream.Infrastructure.Tests/
├── web/                           # React + TS + Vite + Tailwind v4
│   └── src/
│       ├── components/            # UI genérica
│       ├── features/map/          # mapa de risco (Leaflet)
│       ├── features/telemetry/    # gráficos (Recharts)
│       └── lib/                   # tipos, cliente da API
├── docs/ROADMAP.md
└── .github/workflows/ci.yml
```

**Regra de dependência:** `Domain` ← `Application` ← `Infrastructure` ← hosts (`Api`, `McpServer`, `Workers`). O domínio não conhece EF, NetTopologySuite nem MassTransit.

### Como rodar

Pré-requisitos: .NET SDK 9.0.300+, Docker, Node 20+.

```bash
cp .env.example .env
docker compose up -d                                   # PostGIS, RabbitMQ, Qdrant, Jaeger
dotnet tool restore                                    # dotnet-ef (manifesto local)

dotnet run --project src/Tools/EcoStream.DbMigrator    # migrations + seed geoespacial (Porto Alegre)
dotnet run --project src/Presentation/EcoStream.Api    # http://localhost:5080
dotnet run --project src/Workers/EcoStream.Ingestion.Worker   # polling Open-Meteo a cada 5 min
dotnet run --project src/Workers/EcoStream.Agents.Worker      # consome AnomalyDetected

cd web && npm install && npm run dev                   # http://localhost:5173
```

Testes: `dotnet test EcoStream.slnx`. Exemplos de requisição: `src/Presentation/EcoStream.Api/EcoStream.Api.http`.

#### Endpoints (Fase 1)
| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/stations` | Estações cadastradas |
| GET | `/api/telemetry/latest` | Última leitura de cada estação |
| GET | `/api/stations/{code}/readings?from&to` | Série temporal (padrão: últimas 24h, máx. 31 dias) |
| GET | `/api/geo/neighborhoods/near?lat&lon&radiusKm` | Bairros no raio (`ST_DWithin`) + flag de zona de inundação |
| POST | `/api/telemetry` | Leitura manual (idempotente por estação + horário) |

#### Migrations
```bash
dotnet ef migrations add <Nome> -p src/Infrastructure/EcoStream.Infrastructure -o Persistence/Migrations
dotnet ef migrations script -p src/Infrastructure/EcoStream.Infrastructure   # revisar SQL
```

| Serviço | URL |
|---|---|
| RabbitMQ Management | http://localhost:15672 (ecostream / ecostream) |
| Qdrant Dashboard | http://localhost:6333/dashboard |
| Jaeger UI | http://localhost:16686 |

### Decisões de licença
MediatR ≥ 13 e MassTransit ≥ 9 passaram a ter licença comercial. O projeto fica fixado em **MediatR 12.x** e **MassTransit 8.x** (Apache-2.0).

Veja o plano completo em [docs/ROADMAP.md](docs/ROADMAP.md).
