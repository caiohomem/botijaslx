# Quick Start - Botijas CO₂

## Docker (mais fácil)

```bash
docker compose up --build
```

| Serviço | URL |
|---------|-----|
| Web | http://localhost:3000 |
| API | http://localhost:8080 |
| Swagger | http://localhost:8080/swagger |
| Health | http://localhost:8080/health |

Header obrigatório nas rotas `/api/*`: `X-Api-Key: oficina`

```bash
curl http://localhost:8080/health
curl -H "X-Api-Key: oficina" "http://localhost:8080/api/customers?query=joao"
```

## Pré-requisitos (sem Docker)

- Node.js 18+ (para frontend)
- .NET SDK 10.0+ (para backend)
- PostgreSQL (ou use só o serviço `postgres` do compose)

## Iniciar Frontend

```bash
cd web
npm install
npm run dev
```

Acesse: http://localhost:3000

## Iniciar Backend

```bash
cd src/Botijas.Api
dotnet restore
dotnet run
```

Acesse: http://localhost:5000/swagger

## Testar API (UC01)

### Criar Cliente
```bash
curl -X POST http://localhost:8080/api/customers \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: oficina" \
  -d '{
    "name": "João Silva",
    "phone": "912345678",
    "email": "joao@example.com"
  }'
```

### Buscar Clientes
```bash
curl -H "X-Api-Key: oficina" "http://localhost:8080/api/customers?query=joao"
```

## Estrutura do Projeto

```
botijaslx/
├── docs/              # Documentação completa
├── src/               # Backend .NET
│   ├── Botijas.Domain
│   ├── Botijas.Application
│   ├── Botijas.Infrastructure
│   └── Botijas.Api
└── web/               # Frontend Next.js
```

## Status

✅ UC01 - Identificar/Registrar Cliente (implementado)
⏳ UC02-UC07 - Em desenvolvimento
