# Quick Start - Botijas CO₂

## Pré-requisitos

- Node.js 18+ (para frontend)
- .NET SDK 10.0+ (para backend)
- SQLite (vem com .NET)

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
curl -X POST http://localhost:5000/api/customers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "João Silva",
    "phone": "912345678",
    "email": "joao@example.com"
  }'
```

### Buscar Clientes
```bash
curl "http://localhost:5000/api/customers?query=joao"
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
