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
│   │   ├── EcoStream.AI/              # Semantic Kernel agents, MCP client, RAG/Qdrant, guardrails
│   │   └── EcoStream.Notifications/   # Telegram, Twilio, Slack/Teams (Phase 4)
│   ├── Presentation/
│   │   ├── EcoStream.Api/             # ASP.NET Core Minimal API
│   │   └── EcoStream.McpServer/       # MCP server exposing PostGIS tools
│   ├── Tools/
│   │   ├── EcoStream.DbMigrator/      # applies migrations + imports GeoJSON from infra/seed/
│   │   └── EcoStream.KnowledgeIngestor/ # embeds docs/protocols into Qdrant (RAG)
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
├── docs/protocols/                # RAG knowledge base (illustrative)
└── .github/workflows/ci.yml
```

**Dependency rule:** `Domain` ← `Application` ← `Infrastructure` ← hosts (`Api`, `McpServer`, `Workers`). The domain knows nothing about EF, NetTopologySuite or MassTransit.

### Getting started

Prerequisites: .NET SDK 9.0.300+, Docker, Node 20+.

```bash
cp .env.example .env
docker compose up -d --build                           # PostGIS, RabbitMQ, Qdrant, Jaeger
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

#### Messaging (Phase 2)
```
API / Ingestion ──(outbox)──► ecostream.anomaly-detected (fanout)
                                   └─► anomaly-detected queue ─► Data Analyst (PostGIS) ──► ecostream.affected-areas (fanout)
                                                                   └─► Risk Decider (rules-v1) ──► ecostream.risk-assessed (fanout)
                                                                        └─► Communicator ──► ecostream.alerts (topic, alerts.{severity}.{channel})
                                                                             ├─ alerts.*.slack    ─► notifications-slack
                                                                             ├─ alerts.*.telegram ─► notifications-telegram
                                                                             └─ alerts.*.sms      ─► notifications-sms
```
- **Transactional outbox** (MassTransit + EF Core): events are committed with the business data and delivered afterwards — no lost or phantom events.
- **Inbox / idempotency:** each consumer processes a `MessageId` once, so a redelivery never sends the same SMS twice.
- **Failure handling:** exponential retry → delayed redelivery (1/5/15 min, RabbitMQ delayed-exchange plugin) → `<queue>_error`. Validation/domain errors skip retries.
- **Tracing:** trace context flows API → outbox → RabbitMQ → every consumer; see the whole incident as one trace in Jaeger.
- Steps are interfaces (`IAffectedAreaAnalyzer`, `IRiskDecider`, `IAlertComposer`); Phase 3 plugs AI agents in, rules stay as fallback.

#### AI agents, MCP & RAG (Phase 3)
| Agent | LLM does | Stays deterministic |
|---|---|---|
| Data Analyst | calls MCP tools (rivers, risk zones) and writes analyst notes | neighborhood list comes from PostGIS — never generated |
| RAG & Protocols | — (embeddings only) | vector search over Civil Defense protocols in Qdrant |
| Risk Decider | severity, summary, actions as JSON with protocol citations | **guardrail**: if AI and rules differ by >1 level, the more severe wins; invented citations are dropped |
| Communicator | writes each channel's text | which channels fire (by severity) and the 160-char SMS limit |

Every AI step has a time budget and **falls back to the rules** on timeout, provider error or invalid output. Default is `Ai:Provider = Rules` (AI off).

```bash
# OpenAI
dotnet user-secrets --id ecostream-ai set "Ai:Provider" "OpenAI"
dotnet user-secrets --id ecostream-ai set "Ai:ChatModel" "gpt-4.1-mini"            # any tool-calling model
dotnet user-secrets --id ecostream-ai set "Ai:EmbeddingModel" "text-embedding-3-small"
dotnet user-secrets --id ecostream-ai set "Ai:ApiKey" "sk-..."

# or local & free with Ollama (brew install ollama)
ollama pull llama3.1:8b && ollama pull nomic-embed-text
dotnet user-secrets --id ecostream-ai set "Ai:Provider" "Ollama"
dotnet user-secrets --id ecostream-ai set "Ai:ChatModel" "llama3.1:8b"
dotnet user-secrets --id ecostream-ai set "Ai:EmbeddingModel" "nomic-embed-text"

dotnet run --project src/Tools/EcoStream.KnowledgeIngestor      # embed docs/protocols into Qdrant
dotnet run --project src/Presentation/EcoStream.McpServer       # MCP tools at http://localhost:5090/mcp
dotnet run --project src/Workers/EcoStream.Agents.Worker
```
- Inspect the MCP server with any client: `npx @modelcontextprotocol/inspector` → `http://localhost:5090/mcp`.
- Model evals (real provider, cost tokens): `ECOSTREAM_AI_EVALS=1 Ai__Provider=... dotnet test tests/EcoStream.AI.Tests`.
- `docs/protocols/` holds **illustrative, unofficial** protocol texts written for this project.

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
│   │   ├── EcoStream.AI/              # agentes Semantic Kernel, cliente MCP, RAG/Qdrant, guardrails
│   │   └── EcoStream.Notifications/   # Telegram, Twilio, Slack/Teams (Fase 4)
│   ├── Presentation/
│   │   ├── EcoStream.Api/             # ASP.NET Core Minimal API
│   │   └── EcoStream.McpServer/       # servidor MCP expondo tools PostGIS
│   ├── Tools/
│   │   ├── EcoStream.DbMigrator/      # aplica migrations + importa GeoJSON de infra/seed/
│   │   └── EcoStream.KnowledgeIngestor/ # embeddings de docs/protocols no Qdrant (RAG)
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
├── docs/protocols/                # base de conhecimento do RAG (didática)
└── .github/workflows/ci.yml
```

**Regra de dependência:** `Domain` ← `Application` ← `Infrastructure` ← hosts (`Api`, `McpServer`, `Workers`). O domínio não conhece EF, NetTopologySuite nem MassTransit.

### Como rodar

Pré-requisitos: .NET SDK 9.0.300+, Docker, Node 20+.

```bash
cp .env.example .env
docker compose up -d --build                           # PostGIS, RabbitMQ, Qdrant, Jaeger
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

#### Mensageria (Fase 2)
```
API / Ingestion ──(outbox)──► ecostream.anomaly-detected (fanout)
                                   └─► fila anomaly-detected ─► Analista de Dados (PostGIS) ──► ecostream.affected-areas (fanout)
                                                                  └─► Decisor de Risco (rules-v1) ──► ecostream.risk-assessed (fanout)
                                                                       └─► Comunicador ──► ecostream.alerts (topic, alerts.{severidade}.{canal})
                                                                            ├─ alerts.*.slack    ─► notifications-slack
                                                                            ├─ alerts.*.telegram ─► notifications-telegram
                                                                            └─ alerts.*.sms      ─► notifications-sms
```
- **Transactional outbox** (MassTransit + EF Core): o evento é gravado na mesma transação dos dados e entregue depois — sem evento perdido ou fantasma.
- **Inbox / idempotência:** cada consumer processa um `MessageId` uma única vez; reentrega nunca manda o mesmo SMS duas vezes.
- **Falhas:** retry exponencial → redelivery atrasado (1/5/15 min, plugin delayed-exchange) → `<fila>_error`. Erros de validação/domínio não fazem retry.
- **Tracing:** o contexto do trace atravessa API → outbox → RabbitMQ → cada consumer; o incidente inteiro aparece como um trace no Jaeger.
- Cada etapa é uma interface (`IAffectedAreaAnalyzer`, `IRiskDecider`, `IAlertComposer`); na Fase 3 entram os agentes de IA e as regras ficam como fallback.

#### Agentes de IA, MCP & RAG (Fase 3)
| Agente | O LLM faz | Continua determinístico |
|---|---|---|
| Analista de Dados | chama tools MCP (rios, zonas de risco) e escreve notas | lista de bairros vem do PostGIS — nunca gerada |
| RAG & Protocolos | — (só embeddings) | busca vetorial nos protocolos da Defesa Civil no Qdrant |
| Decisor de Risco | severidade, resumo e ações em JSON, citando protocolos | **guardrail**: se IA e regras divergem >1 nível, vence o mais severo; citações inventadas são descartadas |
| Comunicador | escreve o texto de cada canal | quais canais disparam (pela severidade) e o limite de 160 caracteres do SMS |

Cada etapa de IA tem orçamento de tempo e **cai para as regras** em timeout, erro do provedor ou saída inválida. Padrão: `Ai:Provider = Rules` (IA desligada).

```bash
# OpenAI
dotnet user-secrets --id ecostream-ai set "Ai:Provider" "OpenAI"
dotnet user-secrets --id ecostream-ai set "Ai:ChatModel" "gpt-4.1-mini"            # qualquer modelo com tool calling
dotnet user-secrets --id ecostream-ai set "Ai:EmbeddingModel" "text-embedding-3-small"
dotnet user-secrets --id ecostream-ai set "Ai:ApiKey" "sk-..."

# ou local e gratuito com Ollama (brew install ollama)
ollama pull llama3.1:8b && ollama pull nomic-embed-text
dotnet user-secrets --id ecostream-ai set "Ai:Provider" "Ollama"
dotnet user-secrets --id ecostream-ai set "Ai:ChatModel" "llama3.1:8b"
dotnet user-secrets --id ecostream-ai set "Ai:EmbeddingModel" "nomic-embed-text"

dotnet run --project src/Tools/EcoStream.KnowledgeIngestor      # embeddings de docs/protocols no Qdrant
dotnet run --project src/Presentation/EcoStream.McpServer       # tools MCP em http://localhost:5090/mcp
dotnet run --project src/Workers/EcoStream.Agents.Worker
```
- Inspecione o servidor MCP com qualquer cliente: `npx @modelcontextprotocol/inspector` → `http://localhost:5090/mcp`.
- Evals do modelo (provedor real, consome tokens): `ECOSTREAM_AI_EVALS=1 Ai__Provider=... dotnet test tests/EcoStream.AI.Tests`.
- `docs/protocols/` contém textos de protocolo **didáticos, não oficiais**, escritos para este projeto.

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
