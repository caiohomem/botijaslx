# Progress Log

## 2024-12-XX — Setup Inicial e Internacionalização
- Implementado setup do projeto Next.js com App Router
- Configurado internacionalização (next-intl) com suporte a PT-PT e EN
- Implementado sistema de tema claro/escuro com persistência
- Criada estrutura de componentes reutilizáveis
- Arquivos principais:
  - `web/app/[locale]/layout.tsx` - Layout com i18n e tema
  - `web/components/ThemeProvider.tsx` - Provider de tema
  - `web/components/ThemeToggle.tsx` - Botão de alternar tema
  - `web/components/LanguageSelector.tsx` - Seletor de idioma
  - `web/components/Header.tsx` - Header com controles
  - `web/messages/pt-PT.json` - Traduções em português
  - `web/messages/en.json` - Traduções em inglês
  - `web/i18n.ts` - Configuração de i18n
  - `web/middleware.ts` - Middleware de roteamento por locale
- Docs atualizados:
  - `00_brief.md` - Adicionada seção de i18n e tema
  - `03_architecture.md` - Documentado frontend com i18n
  - `05_ui_flows.md` - Documentado fluxos de i18n e tema
  - `06_progress_log.md` - Este arquivo

## Notas
- Frontend pronto para desenvolvimento dos casos de uso
- Backend ainda não iniciado
- Testes ainda não implementados

---

## 2024-12-XX — UC01 Implementado: Identificar/Registrar Cliente
- Criada estrutura backend .NET 10 com Clean Architecture
- Implementado Domain: Customer, PhoneNumber (VO), CustomerCreated (Domain Event)
- Implementado Application: CreateCustomerCommand/Handler, SearchCustomersQuery/Handler
- Implementado Infrastructure: BotijasDbContext, CustomerRepository com EF Core SQLite
- Implementado API: POST /api/customers, GET /api/customers?query=
- Arquivos principais:
  - Domain/Entities/Customer.cs
  - Domain/ValueObjects/PhoneNumber.cs
  - Domain/DomainEvents/CustomerCreated.cs
  - Application/Customers/Commands/CreateCustomerCommandHandler.cs
  - Application/Customers/Queries/SearchCustomersQueryHandler.cs
  - Infrastructure/Data/BotijasDbContext.cs
  - Infrastructure/Repositories/CustomerRepository.cs
  - Api/Controllers/CustomersController.cs
- Docs atualizados:
  - 06_progress_log.md (este arquivo)
- Pendências:
  - Testes unitários (Domain e Application)
  - Migrations do EF Core (atualmente usando EnsureCreated)

---

## 2024-12-XX — UC03 Implementado: Imprimir Etiquetas (PrintJob)
- Implementado Domain: PrintJob (agregado) com estados Pending/Dispatched/Printed/Failed
- Implementado Domain Events: PrintJobCreated, PrintJobPrinted, PrintJobFailed
- Implementado Application: CreatePrintJobCommand/Handler, AckPrintJobPrintedCommand/Handler, AckPrintJobFailedCommand/Handler
- Implementado Infrastructure: PrintJobRepository, SignalRPrintJobDispatcher, PrintHub
- Implementado API: POST /api/print-jobs, POST /api/print-jobs/{id}/ack-printed, POST /api/print-jobs/{id}/ack-failed
- Criado PrintGateway: Worker Service que conecta via SignalR e simula impressão
- Arquivos principais:
  - Domain/Entities/PrintJob.cs
  - Domain/DomainEvents/PrintJob*.cs (3 eventos)
  - Application/PrintJobs/Commands/*.cs (3 handlers)
  - Application/PrintJobs/IPrintJobDispatcher.cs (abstração)
  - Infrastructure/PrintJobs/SignalRPrintJobDispatcher.cs
  - Infrastructure/Hubs/PrintHub.cs
  - Infrastructure/Repositories/PrintJobRepository.cs
  - Api/Controllers/PrintJobsController.cs
  - PrintGateway/Program.cs (worker)
- Docs atualizados:
  - 02_use_cases.md (casos de uso detalhados)
  - 06_progress_log.md (este arquivo)

---

## 2024-12-XX — UC02 Implementado: Receber Botijas (Criar Pedido e Associar Botijas)
- Implementado Domain: RefillOrder (agregado), Cylinder (agregado), LabelToken (VO)
- Implementado Domain Events: OrderCreated, CylinderReceived, LabelAssigned, CylinderMarkedReady, CylinderDelivered, OrderBecameReadyForPickup, OrderCompleted
- Implementado Application: CreateOrderCommand/Handler, AddCylinderToOrderCommand/Handler, ScanCylinderToOrderCommand/Handler
- Implementado Infrastructure: RefillOrderRepository, CylinderRepository, EF Core mappings
- Implementado API: POST /api/orders, POST /api/orders/{id}/cylinders, POST /api/orders/{id}/cylinders/scan
- Arquivos principais:
  - Domain/Entities/RefillOrder.cs
  - Domain/Entities/Cylinder.cs
  - Domain/ValueObjects/LabelToken.cs
  - Domain/DomainEvents/*.cs (7 eventos)
  - Application/Orders/Commands/*.cs (3 handlers)
  - Infrastructure/Repositories/RefillOrderRepository.cs
  - Infrastructure/Repositories/CylinderRepository.cs
  - Api/Controllers/OrdersController.cs
- Invariantes implementadas:
  - Pedido pertence a um único cliente
  - Botija não pode estar em dois pedidos abertos
  - Validação de estados (Received → Ready → Delivered)
- Docs atualizados:
  - 06_progress_log.md (este arquivo)

---

## 2025-01-XX — UC05/UC06 Implementado: Fila de Enchimento e Marcar Botija como Cheia
- Implementado Application: GetFillingQueueQuery/Handler, MarkCylinderReadyCommand/Handler
- Implementado novos métodos nos repositórios:
  - ICylinderRepository: GetFillingQueueAsync, FindByOrderIdAsync
  - IRefillOrderRepository: FindByCylinderIdAsync
- Implementado API: GET /api/cylinders/filling-queue, POST /api/cylinders/{id}/mark-ready
- Implementado Frontend: Página /filling com UI completa
- Arquivos principais:
  - Application/Filling/FillingQueueItemDto.cs
  - Application/Filling/Queries/GetFillingQueueQuery.cs
  - Application/Filling/Queries/GetFillingQueueQueryHandler.cs
  - Application/Filling/Commands/MarkCylinderReadyCommand.cs
  - Application/Filling/Commands/MarkCylinderReadyCommandHandler.cs
  - Api/Controllers/CylindersController.cs
  - web/app/filling/page.tsx
  - web/lib/api.ts (adicionado cylindersApi)
  - web/messages/*.json (adicionadas traduções de filling)
- Funcionalidades implementadas:
  - Listagem de botijas pendentes agrupadas por pedido
  - Visualização do progresso do pedido (X de Y prontas)
  - Marcar botija como cheia via clique ou scan
  - Atualização automática do status do pedido quando todas prontas
  - Campo de scan para marcação rápida
- Docs atualizados:
  - 06_progress_log.md (este arquivo)

---

## 2025-01-XX — UC10 Implementado: Entregar Botijas ao Cliente (Recolha/Pickup)
- Implementado Application: GetReadyForPickupQuery/Handler, DeliverCylinderCommand/Handler
- Implementado API:
  - GET /api/orders/ready-for-pickup - Lista pedidos prontos para recolha
  - POST /api/orders/{orderId}/cylinders/{cylinderId}/deliver - Entrega botija
- Implementado Frontend: Página /pickup com UI completa
- Arquivos principais:
  - Application/Pickup/PickupOrderDto.cs
  - Application/Pickup/Queries/GetReadyForPickupQuery.cs
  - Application/Pickup/Queries/GetReadyForPickupQueryHandler.cs
  - Application/Pickup/Commands/DeliverCylinderCommand.cs
  - Application/Pickup/Commands/DeliverCylinderCommandHandler.cs
  - Api/Controllers/OrdersController.cs (adicionados endpoints)
  - web/app/pickup/page.tsx
  - web/lib/api.ts (adicionado pickupApi)
  - web/messages/*.json (adicionadas traduções de pickup)
- Funcionalidades implementadas:
  - Listagem de pedidos prontos para recolha
  - Filtro por cliente (nome/telefone)
  - Visualização expandível de botijas por pedido
  - Entrega de botija via clique ou scan
  - Progresso visual (X de Y entregues)
  - Conclusão automática do pedido quando todas entregues
  - Campo de scan para entrega rápida
- Regras de negócio implementadas:
  - Só permite entregar botijas de pedidos ReadyForPickup
  - Valida se botija pertence ao pedido
  - Transição Ready → Delivered
  - Pedido automaticamente Completed quando todas entregues
- Docs atualizados:
  - 02_use_cases.md (atualizar status)
  - 06_progress_log.md (este arquivo)

---

## 2025-01-XX — UC02/UC03/UC04/UC07/UC08/UC09 Implementados

### UC02 - Página de Entrega Completa
- Frontend completo com fluxo de 3 passos: busca/criar cliente → adicionar botijas → resumo
- Scan de QR ou adicionar sem etiqueta
- UI para atribuir etiquetas às botijas sem etiqueta
- Integração com impressão de etiquetas

### UC03 - Imprimir Etiquetas
- Botão na página de entrega que cria PrintJob via API
- Integrado com PrintGateway existente via SignalR

### UC04 - Substituir/Reimprimir Etiqueta
- Endpoint POST /api/cylinders/{id}/assign-label
- UI na página de entrega para escanear e atribuir etiqueta
- Guarda etiqueta anterior para auditoria

### UC07 - Registrar Ocorrência
- Endpoint POST /api/cylinders/{id}/report-problem
- Modal na página de enchimento com tipos de problema
- Botija marcada como Problem e removida da fila

### UC08 - WhatsApp via wa.me
- Campo NotifiedAt no RefillOrder
- Botão "WhatsApp" na página de Pickup para pedidos pendentes
- Gera link wa.me com mensagem personalizada
- Marca pedido como notificado após clicar

### UC09 - Reenviar WhatsApp
- Implementado naturalmente via UC08 (wa.me permite múltiplos envios)

### Arquivos principais criados/modificados:
- Application/Filling/Commands/ReportCylinderProblemCommand*.cs
- Application/Filling/Commands/AssignLabelCommand*.cs
- Application/Pickup/Commands/MarkOrderNotifiedCommand*.cs
- Domain/Entities/RefillOrder.cs (NotifiedAt, MarkAsNotified)
- Api/Controllers/CylindersController.cs (report-problem, assign-label)
- Api/Controllers/OrdersController.cs (mark-notified)
- web/app/delivery/page.tsx (fluxo completo)
- web/app/filling/page.tsx (modal de problema)
- web/app/pickup/page.tsx (botão WhatsApp)
- web/lib/api.ts (novos endpoints)
- web/messages/*.json (novas traduções)

---

## 2025-01-XX — UC11/UC12/UC13 Implementados: Histórico, Relatórios, Configurações

### UC11 - Histórico de Botija
- Entidade CylinderHistoryEntry para registrar eventos
- Repositório ICylinderHistoryRepository
- Handlers atualizados para registrar histórico (MarkReady, ReportProblem, Deliver, AssignLabel)
- Endpoints: GET /api/cylinders/{id}/history, GET /api/cylinders/scan/{qrToken}
- Página Dashboard com busca por QR e timeline visual

### UC12 - Relatórios Operacionais
- Interface IDashboardStatsQuery com implementação em Infrastructure
- Endpoint GET /api/reports/stats
- Dashboard com cards de estatísticas:
  - Pedidos abertos, prontos, concluídos
  - Botijas por estado
  - Métricas diárias e semanais

### UC13 - Gestão de Configurações
- Página /settings com configurações básicas
- Armazenamento em localStorage
- Configuráveis: nome da loja, modelo de mensagem WhatsApp, modelo de etiqueta

### Melhorias Gerais
- Header com navegação por abas
- Link para configurações no header

### Arquivos principais:
- Domain/Entities/CylinderHistoryEntry.cs
- Domain/Repositories/ICylinderHistoryRepository.cs
- Infrastructure/Repositories/CylinderHistoryRepository.cs
- Infrastructure/Queries/DashboardStatsQuery.cs
- Application/Cylinders/Queries/GetCylinderHistoryQuery*.cs
- Application/Cylinders/Queries/GetCylinderByTokenQuery*.cs
- Application/Reports/Queries/IDashboardStatsQuery.cs
- Api/Controllers/ReportsController.cs
- web/app/dashboard/page.tsx (completo com stats e histórico)
- web/app/settings/page.tsx (configurações)
- web/components/Header.tsx (navegação)

---

## 2025-01-XX — Impressão Real: Zebra GK420d + Logo LxBrewery

### Geração de Etiquetas ZPL
- LabelGenerator com suporte a ImageSharp para conversão de logo
- Layout: Logo LxBrewery (topo) → QR Code (centro) → Nome/Telefone cliente
- Tamanho: 75x50mm (600x400 dots @ 203 DPI)

### PrintGateway Atualizado
- Carrega logo PNG e converte para GRF (formato Zebra)
- Envia ZPL via TCP/IP para impressora (porta 9100)
- Modo simulação quando IP não configurado

### Configuração (appsettings.json)
```json
{
  "Printer": {
    "IpAddress": "192.168.x.x",
    "Port": 9100
  }
}
```

### API Atualizada
- PrintJob agora aceita customerName e customerPhone
- Dados do cliente incluídos na etiqueta

### Arquivos principais:
- PrintGateway/LabelGenerator.cs (geração ZPL + conversão imagem)
- PrintGateway/Program.cs (worker atualizado)
- PrintGateway/Assets/lxbrewery.png (logo)
- Application/PrintJobs/Commands/CreatePrintJobCommand.cs (+ customer data)
- web/lib/api.ts (printJobsApi atualizado)

---

## Estado Atual do Sistema (Janeiro 2025)

### Casos de Uso Implementados

| UC | Descrição | Status |
|----|-----------|--------|
| UC01 | Identificar Cliente | ✅ |
| UC02 | Receber Botijas (Criar Pedido) | ✅ |
| UC03 | Imprimir Etiquetas (Zebra GK420d) | ✅ |
| UC04 | Substituir/Reimprimir Etiqueta | ✅ |
| UC05 | Fila de Enchimento | ✅ |
| UC06 | Marcar Botija Cheia | ✅ |
| UC07 | Registrar Ocorrência | ✅ |
| UC08 | Notificação WhatsApp (wa.me) | ✅ |
| UC09 | Reenviar Notificação | ✅ |
| UC10 | Entregar Botijas (Recolha) | ✅ |
| UC11 | Histórico de Botija | ✅ |
| UC12 | Relatórios Operacionais | ✅ |
| UC13 | Gestão de Configurações | ✅ |

### Stack Tecnológica

**Backend:**
- .NET 10 (C#)
- Clean Architecture (Domain, Application, Infrastructure, API)
- Entity Framework Core + SQLite
- SignalR (comunicação com PrintGateway)

**Frontend:**
- Next.js 14 (App Router)
- TypeScript
- Tailwind CSS
- next-intl (i18n: PT-PT, EN)

**PrintGateway:**
- .NET Worker Service
- SignalR Client
- ImageSharp (conversão de imagens)
- ZPL para Zebra GK420d

### Estrutura de Pastas

```
botijaslx/
├── docs/                    # Documentação
├── src/
│   ├── Botijas.Domain/      # Entidades, VOs, Eventos
│   ├── Botijas.Application/ # Commands, Queries, Handlers
│   ├── Botijas.Infrastructure/ # EF Core, Repos, SignalR
│   ├── Botijas.Api/         # Controllers, Program.cs
│   └── Botijas.PrintGateway/ # Worker de impressão
└── web/                     # Frontend Next.js
    ├── app/                 # Páginas (App Router)
    ├── components/          # Componentes React
    ├── lib/                 # API client, utils
    └── messages/            # Traduções i18n
```

### Fluxo Principal de Uso

1. **Entrega** (`/delivery`):
   - Pesquisar/criar cliente
   - Adicionar botijas (scan QR ou sem etiqueta)
   - Finalizar → Imprimir etiquetas (se necessário)

2. **Enchimento** (`/filling`):
   - Ver fila de botijas pendentes
   - Marcar como cheia (scan ou clique)
   - Reportar problema se necessário

3. **Recolha** (`/pickup`):
   - Ver pedidos prontos
   - Notificar cliente via WhatsApp
   - Entregar botijas (scan ou clique)

4. **Dashboard** (`/dashboard`):
   - Estatísticas operacionais
   - Consultar histórico de botija por QR

### Como Executar

```bash
# Terminal 1 - Backend API
cd src/Botijas.Api && dotnet run

# Terminal 2 - PrintGateway (opcional)
cd src/Botijas.PrintGateway && dotnet run

# Terminal 3 - Frontend
cd web && npm run dev
```

### URLs

- Frontend: http://localhost:3000
- API: http://localhost:5001
- Swagger: http://localhost:5001/swagger

### Configuração da Impressora Zebra

1. Descobrir IP da impressora (ver página de configuração)
2. Editar `src/Botijas.PrintGateway/appsettings.json`:
   ```json
   {
     "Printer": {
       "IpAddress": "192.168.1.XXX",
       "Port": 9100
     }
   }
   ```
3. Reiniciar PrintGateway

### Pendências Futuras

- [ ] Autenticação/Login de utilizadores
- [ ] Testes unitários e de integração
- [ ] Migrations do EF Core (em vez de EnsureCreated)
- [ ] Docker/containerização
- [ ] Deploy para produção
