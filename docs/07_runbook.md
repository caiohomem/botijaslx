# Runbook

## Requisitos
- Node.js 18+ (para frontend)
- .NET SDK 10.0+ (para backend)
- Docker + Docker Compose (recomendado)
- PostgreSQL (via Docker Compose)

## Docker Compose

```bash
docker compose up --build
```

| Serviço | URL |
|---------|-----|
| Web | http://localhost:3000 |
| API | http://localhost:8080 |
| Swagger | http://localhost:8080/swagger |
| Health | http://localhost:8080/health |
| Postgres | localhost:5432 |

Credenciais da API: header `X-Api-Key: oficina` (não necessário em `/health` nem `/swagger`).

Compose de desenvolvimento (mesma exposição de portas):

```bash
docker compose -f docker-compose.dev.yml up --build
```

## Frontend (Next.js)

### Instalação
```bash
cd web
npm install
```

### Desenvolvimento
```bash
npm run dev
```
Acesse [http://localhost:3000](http://localhost:3000)

O sistema redireciona automaticamente para `/pt-PT` ou `/en` conforme configuração.

### Build de Produção
```bash
npm run build
npm start
```

## Backend (ASP.NET Core)

### Requisitos
- .NET SDK 10.0+

### Instalação e Execução
```bash
cd src/Botijas.Api
dotnet restore
dotnet run
```

A API estará disponível em:
- HTTP: `http://localhost:5000` ou `http://localhost:5001`
- Swagger UI: `http://localhost:5000/swagger` (em desenvolvimento)

### Endpoints Disponíveis (UC01)
- `POST /api/customers` - Criar cliente
- `GET /api/customers?query=...` - Buscar clientes

### Banco de Dados
- SQLite: arquivo `botijas.db` criado automaticamente na primeira execução
- Usa `EnsureCreated()` no startup (para produção, usar migrations)

## Print Gateway
*Será implementado nos próximos passos*

```bash
cd src/Botijas.PrintGateway
dotnet run
```

## Observações
- Gateway deve estar online para impressão imediata.
- Sistema tolera queda temporária de internet.
- Tema e idioma são salvos no localStorage do navegador.
- Idioma é persistido na URL (`/pt-PT` ou `/en`).

## Estrutura de Locales
- Português (PT-PT): `/pt-PT/*`
- Inglês (EN): `/en/*`
- Locale padrão: PT-PT

## Tema
- Claro: tema padrão
- Escuro: ativado via toggle no header
- Preferência salva em `localStorage.theme`
