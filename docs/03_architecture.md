# Arquitetura

## Stack
- Frontend: Next.js (App Router), TypeScript, Tailwind CSS
- Backend: ASP.NET Core (.NET 8/9)
- DB: SQLite com EF Core
- Real-time: SignalR
- Gateway local: .NET Worker

## Clean Architecture
- Domain: regras, invariantes, eventos
- Application: casos de uso, handlers
- Infrastructure: EF Core, SQLite, WhatsApp, SignalR
- API: endpoints, auth futura

## Frontend
- Next.js App Router com internacionalização (next-intl)
- Suporte a PT-PT e EN
- Tema claro/escuro com persistência
- Componentes reutilizáveis em `/components`
- Rotas com locale: `/app/[locale]/*`

## Impressão
- PrintGateway conecta via SignalR (outbound).
- Recebe jobs via push.
- Confirma via API (idempotente).

## Decisões
- Sem polling contínuo.
- Estados mínimos no domínio.
- Markdown como memória persistente do projeto.
- Internacionalização desde o início.
- Tema claro/escuro nativo.
