# Botijas CO₂

Sistema de gestão de enchimento de botijas CO₂ com foco em operação de loja.

## Stack

- **Frontend**: Next.js (App Router), TypeScript, Tailwind CSS
- **Backend**: ASP.NET Core (.NET 10)
- **DB**: SQLite com EF Core
- **Real-time**: SignalR - *em desenvolvimento*

## Funcionalidades

- 🌍 **Internacionalização**: Português (PT-PT) e Inglês
- 🌓 **Tema claro/escuro** com persistência
- 📱 Interface responsiva

## Desenvolvimento

### Frontend

```bash
cd web
npm install
npm run dev
```

Acesse [http://localhost:3000](http://localhost:3000)

### Backend

```bash
cd src/Botijas.Api
dotnet restore
dotnet run
```

Acesse `http://localhost:5000/swagger` para ver a documentação da API.

**Endpoints disponíveis:**
- `POST /api/customers` - Criar cliente
- `GET /api/customers?query=...` - Buscar clientes

## Documentação

Toda a documentação do projeto está em `/docs`:

- `00_brief.md` - Visão geral e regras
- `01_domain_model.md` - Modelo de domínio
- `02_use_cases.md` - Casos de uso
- `03_architecture.md` - Arquitetura
- `04_api_contracts.md` - Contratos de API
- `05_ui_flows.md` - Fluxos de UI
- `06_progress_log.md` - Log de progresso
- `07_runbook.md` - Como executar
- `adr/` - Architecture Decision Records

## Estrutura

```
botijaslx/
├── docs/           # Documentação
├── src/            # Backend .NET (a criar)
│   ├── Botijas.Domain
│   ├── Botijas.Application
│   ├── Botijas.Infrastructure
│   └── Botijas.Api
├── web/            # Frontend Next.js
│   ├── app/
│   ├── components/
│   └── messages/
└── README.md
```

## Licença

*A definir*
