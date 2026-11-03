# Roadmap — EcoStream AI

Cada fase termina com algo **demonstrável** (bom para post no LinkedIn) e com testes passando no CI.

## Fase 0 — Fundação ✅
- Solução .NET 9 em Clean Architecture (`EcoStream.slnx`), Central Package Management, analyzers.
- `docker-compose.yml`: PostGIS, RabbitMQ (management), Qdrant, Jaeger (+ Ollama opcional).
- Domínio inicial: `TelemetryReading`, `GeoPoint`, `SeverityLevel`, evento `SevereAnomalyDetected`.
- CQRS: `IngestTelemetryCommand` + validação via pipeline MediatR.
- Frontend React/TS/Tailwind com mapa (Leaflet) e gráfico (Recharts) sobre dados mock.
- GitHub Actions: build + test do backend e build do frontend.

## Fase 1 — Infra de dados & Ingestão ✅
- Agregado `Station`; leituras com FK por código e índice único `(station_code, measured_at)` → ingestão idempotente.
- Camadas geo no schema `geo` (`neighborhoods`, `rivers`, `risk_zones`) em `geography(…,4326)` com índices GIST — read models, fora do domínio.
- Migration `Initial` + `EcoStream.DbMigrator` (migrate + seed GeoJSON idempotente). Seed de Porto Alegre **aproximado**.
- `Ingestion.Worker`: cliente tipado Open-Meteo com `AddStandardResilienceHandler` (retry c/ jitter, circuit breaker, timeouts), escopo DI por estação, falhas isoladas, span OTel por ciclo.
- Queries CQRS: estações, última leitura por estação, série temporal, bairros num raio com flag de zona de inundação (base das tools MCP da Fase 3).
- **Pendências:** adaptador INMET (exige token), dados oficiais IBGE/ANA/SGB, testes de integração com Testcontainers (Fase 5).
- **Demo:** worker ingerindo dados reais; `GET /api/geo/neighborhoods/near` respondendo via PostGIS; traces no Jaeger.

## Fase 2 — Event-Driven com RabbitMQ ✅
- **Transactional Outbox** do MassTransit (EF Core, Postgres) — `Publish` antes do `SaveChanges`, entrega assíncrona; tabelas `outbox_message`, `outbox_state`, `inbox_state` (migration `AddMessagingOutbox`).
- **Inbox** em todos os consumers via `ResilientConsumerDefinition<T>` → idempotência por `MessageId`.
- Topologia: fanout `ecostream.anomaly-detected` / `affected-areas` / `risk-assessed`; topic `ecostream.alerts` com `alerts.{severity}.{channel}` e uma fila por canal.
- Falhas: retry exponencial → delayed redelivery (imagem RabbitMQ 4.1 com plugin delayed-exchange) → `_error`; erros permanentes pulam retry.
- Pipeline completo **baseado em regras** (`RiskAssessmentPolicy` no domínio) — baseline/fallback dos agentes da Fase 3. Canais de notificação são stubs que logam.
- Testes: pipeline ponta a ponta no test harness in-memory do MassTransit.
- **Pendente (precisa de Docker):** validar contra RabbitMQ/Postgres reais e capturar o trace no Jaeger.
- **Demo:** `POST` de leitura severa → mensagem atravessa filas → trace completo → alertas por canal no log.

## Fase 3 — Agentes de IA, MCP & RAG
- `EcoStream.McpServer` com SDK oficial de MCP para C#: tools `find_neighborhoods_within_radius`, `find_rivers_near`, `get_risk_zones_intersecting` (PostGIS `ST_DWithin`, `ST_Intersects`).
- Ingestão de manuais da Defesa Civil → chunking → embeddings → **Qdrant**.
- Semantic Kernel: 4 agentes (Analista de Dados → RAG & Protocolos → Decisor de Risco → Comunicador), cada passo encadeado por mensagem RabbitMQ.
- Provedor de LLM plugável por configuração (OpenAI / Azure OpenAI / Ollama).
- Saída estruturada (JSON schema) no Decisor; testes de avaliação com cenários fixos.
- **Demo:** anomalia gera relatório de contingência com severidade 1–5 e protocolos citados.

## Fase 4 — Notificações & Frontend real
- `Notifications`: Telegram.Bot, Twilio (SMS/voz para severidade ≥ 4), webhooks Slack/Teams; cada canal com Polly e rate limit.
- API: endpoints de leitura + **SignalR** para alertas em tempo real.
- Frontend: mapa com polígonos de risco/bairros afetados, timeline de alertas, relatório do agente.
- **Demo:** vídeo do alerta chegando no Telegram enquanto o mapa atualiza.

## Fase 5 — Produção & Portfólio
- Dockerfiles multi-stage para API/Workers/MCP; compose "full".
- Testes de integração com **Testcontainers** (PostGIS, RabbitMQ, Qdrant).
- CD (GHCR + deploy em Azure Container Apps ou similar), métricas OTel + dashboards.
- README com diagrama de arquitetura, GIF e decisões (ADRs).
