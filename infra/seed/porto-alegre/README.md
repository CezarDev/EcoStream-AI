# Seed — Porto Alegre (demo)

> ⚠️ **Dados aproximados, só para desenvolvimento.** Os polígonos são retângulos simplificados e as
> populações são arredondadas. **Não use para decisões reais.**

| Arquivo | Conteúdo | Substituir por (fonte oficial) |
|---|---|---|
| `stations.geojson` | Pontos de monitoramento (`code`, `name`, `provider`) | Estações automáticas do INMET |
| `neighborhoods.geojson` | Bairros (`name`, `city`, `population`) | Malha de bairros IBGE/Censo 2022 ou ObservaPOA |
| `rivers.geojson` | Rios e arroios (`name`) | Base Hidrográfica Ottocodificada — ANA |
| `risk-zones.geojson` | Zonas de risco (`name`, `riskType`: Flood/Landslide, `level` 1–5) | Áreas de risco do SGB/CPRM e Defesa Civil |

O seeder é idempotente: compara pela chave natural (code; city+name; name) e só insere o que falta.
Para outra cidade, crie `infra/seed/<cidade>/` com os mesmos arquivos e rode:
`dotnet run --project src/Tools/EcoStream.DbMigrator -- --seed infra/seed/<cidade>`
