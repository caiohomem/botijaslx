# Análise Técnica do Projeto — Estado Atual

> Documento gerado em 2026-02-25. Reflecte o estado real do código (verificado via leitura directa dos ficheiros).

---

## Stack Real (corrige docs anteriores)

| Camada | Tecnologia |
|--------|-----------|
| Frontend | Next.js 14.1, App Router, TypeScript, TailwindCSS, next-intl |
| Backend | .NET 10, ASP.NET Core, C# |
| Banco | PostgreSQL via EF Core (Npgsql) |
| Real-time | SignalR (`/hubs/print`) |
| Print Gateway | Worker .NET separado (`Botijas.PrintGateway`) |

> Os docs antigos mencionavam SQLite e .NET 8/9 — **estão desactualizados**. O banco é PostgreSQL.

---

## Estrutura de Projectos Backend

```
src/
├── Botijas.Domain/           # Entidades, Value Objects, Interfaces de Repositório, Domain Events
├── Botijas.Application/      # Handlers CQRS (Commands + Queries), DTOs, Result<T>
├── Botijas.Infrastructure/   # EF Core DbContext, Repositórios, Migrations, SignalR Hub, Print Dispatchers
├── Botijas.Api/              # Controllers, Program.cs (DI, CORS, Migrations auto-run)
└── Botijas.PrintGateway/     # Serviço de impressão separado (Worker)
```

---

## Entidades do Domínio

### Customer (`src/Botijas.Domain/Entities/Customer.cs`)
```
CustomerId: Guid (PK)
Name: string (max 200, required)
Phone: PhoneNumber (value object, unique index, stored as VARCHAR(20))
CreatedAt: DateTime (UTC)
```
- Factory: `Customer.Create(name, phone)` — emite `CustomerCreated` domain event
- Sem método `UpdatePhone` ainda (a adicionar)

### Cylinder (`src/Botijas.Domain/Entities/Cylinder.cs`)
```
CylinderId: Guid (PK)
SequentialNumber: long (auto-increment, para exibição no UI)
LabelToken: LabelToken? (value object, unique index, QR code)
State: CylinderState enum (Received | Ready | Delivered | Problem)
OccurrenceNotes: string?
CreatedAt: DateTime
```

### RefillOrder (`src/Botijas.Domain/Entities/RefillOrder.cs`)
```
OrderId: Guid (PK)
CustomerId: Guid (FK)
Status: RefillOrderStatus (Open | ReadyForPickup | Completed)
CreatedAt, CompletedAt?, NotifiedAt?: DateTime
Cylinders: List<CylinderRef> (owned collection, CASCADE delete configurado)
```

### Value Objects
- `PhoneNumber`: strip não-dígitos, validar >= 9 dígitos, stored as string
- `LabelToken`: QR code identifier

### Outras Entidades (sem domínio rico)
- `PrintJob`: gerido por `PrintJobsController`, estados: Pending → Dispatched → Printed/Failed
- `CylinderHistoryEntry`: log de eventos (Received, LabelAssigned, MarkedReady, Delivered, ProblemReported)
- `CylinderRef`: join entity entre RefillOrder e Cylinder

---

## Controllers e Endpoints (estado actual)

### `CustomersController` — `GET/POST /api/customers`
| Método | Rota | Handler |
|--------|------|---------|
| POST | `/api/customers` | `CreateCustomerCommandHandler` |
| GET | `/api/customers?query=` | `SearchCustomersQueryHandler` |
| GET | `/api/customers/{id}` | TODO (retorna 404) |
| GET | `/api/customers/{id}/cylinders` | `GetCustomerCylindersQueryHandler` |

### `CylindersController`
| Método | Rota |
|--------|------|
| GET | `/api/cylinders/filling-queue` |
| POST | `/api/cylinders/{id}/mark-ready` |
| POST | `/api/cylinders/batch/mark-ready` |
| POST | `/api/cylinders/{id}/report-problem` |
| POST | `/api/cylinders/{id}/assign-label` |
| GET | `/api/cylinders/{id}/history` |
| GET | `/api/cylinders/scan/{qrToken}` |

### `OrdersController`
| Método | Rota |
|--------|------|
| POST | `/api/orders` |
| POST | `/api/orders/{id}/cylinders` |
| POST | `/api/orders/{id}/cylinders/batch` |
| POST | `/api/orders/{id}/cylinders/scan` |
| GET | `/api/orders/ready-for-pickup?search=` |
| POST | `/api/orders/{id}/cylinders/{cid}/deliver` |
| POST | `/api/orders/{id}/mark-notified` |

> **Importante**: Não existem endpoints DELETE em nenhum controller.

---

## Padrão CQRS — Como Criar um Novo Handler

1. **Command/Query Record** em `src/Botijas.Application/{Domain}/Commands/` ou `Queries/`
2. **Handler class** no mesmo directório, injectando repositórios e `BotijasDbContext` se precisar
3. **Retornar `Result<T>` ou `Result`** (non-generic para operações void)
4. **Registar no DI** em `src/Botijas.Api/Program.cs`: `builder.Services.AddScoped<THandler>()`
5. **Injectar no Controller** via constructor, chamar `.Handle(command, ct)`

### Padrão de Response do Controller
```csharp
var result = await _handler.Handle(command, cancellationToken);
if (!result.IsSuccess) return BadRequest(new { error = result.Error });
return Ok(result.Value);  // ou NoContent() para deletes
```

---

## Repositórios — Interfaces Actuais

### `ICustomerRepository`
- `FindByIdAsync(Guid)`
- `FindByPhoneAsync(PhoneNumber)`
- `SearchAsync(string?)` — busca por nome/telefone, top 50
- `AddAsync(Customer)`
- `SaveChangesAsync()`

### `ICylinderRepository`
- `FindByIdAsync(Guid)`
- `FindByLabelTokenAsync(LabelToken)`
- `FindBySequentialNumberAsync(long)`
- `FindInOpenOrderAsync(Guid)`
- `FindByOrderIdAsync(Guid)`
- `GetFillingQueueAsync()` — retorna `List<FillingQueueItem>` (DTO complexo com join)
- `AddAsync(Cylinder)`
- `SaveChangesAsync()`

### `IRefillOrderRepository`
- `FindByIdAsync(Guid)`
- `FindByCylinderIdAsync(Guid)`
- `FindOpenOrdersByCustomerAsync(Guid)`
- **`FindAllByCustomerAsync(Guid)`** — útil para verificar histórico antes de excluir cliente
- `FindReadyForPickupAsync(Guid?)`
- `AddAsync(RefillOrder)`
- `SaveChangesAsync()`

---

## Banco de Dados — DbContext

**Ficheiro**: `src/Botijas.Infrastructure/Data/BotijasDbContext.cs`

DbSets disponíveis:
- `Customers`
- `Orders`
- `Cylinders`
- `CylinderRefs` — join entity, **CASCADE delete configurado do Order para CylinderRef**
- `PrintJobs`
- `CylinderHistory` — **SEM cascade configurado de Cylinder → CylinderHistory**

**Cascade que existe**: Order → CylinderRefs (ao apagar Order, os CylinderRefs são apagados)
**Cascade que NÃO existe**: Cylinder → CylinderHistory, Cylinder → CylinderRefs

**Migrations**: única migration em `src/Botijas.Infrastructure/Migrations/20260211010845_InitialCreate.cs`

**Auto-run**: `Program.cs` chama `dbContext.Database.Migrate()` no startup (com retry loop de 20 tentativas).

---

## Frontend — Estrutura

```
web/
├── app/
│   ├── page.tsx               # Hub de navegação (5 cards → rotas)
│   ├── layout.tsx             # Root layout com i18n e ThemeProvider
│   ├── delivery/page.tsx      # Receber botijas do cliente (multi-step)
│   ├── filling/page.tsx       # Marcar botijas como cheias
│   ├── pickup/page.tsx        # Entregar botijas ao cliente
│   ├── dashboard/page.tsx     # Estatísticas e pesquisa
│   └── settings/page.tsx      # Configurações
├── components/
│   ├── CreateCustomerForm.tsx  # Form de criação (name + phone)
│   ├── CustomerSearch.tsx      # Typeahead com debounce (props: onSelect, onCreateNew?, disabled?)
│   ├── QrScanner.tsx
│   ├── LabelPreview.tsx
│   ├── Header.tsx
│   ├── ThemeProvider.tsx
│   └── LocaleProvider.tsx
├── lib/
│   └── api.ts                 # Cliente HTTP centralizado
└── messages/
    ├── pt-PT.json
    └── en.json
```

### `CustomerSearch` — Props
```typescript
onSelect: (customer: { customerId, name, phone }) => void
onCreateNew?: (query: string) => void   // opcional — se omitido, não mostra botão "Criar"
disabled?: boolean
```

### `apiRequest<T>` — Padrão Base
```typescript
// lib/api.ts
export async function apiRequest<T>(endpoint: string, options: RequestInit = {}): Promise<T>
// Lança Error com message do backend em caso de resposta não-ok
// ATENÇÃO: actualmente não trata 204 No Content — precisa de fix para endpoints DELETE
```

### i18n — `useTranslations()`
```typescript
const t = useTranslations();
t('navigation.delivery')   // chaves aninhadas
t('clientes.title')        // nova secção a adicionar
```

---

## Padrão de Estado nas Páginas

```typescript
// Padrão consistente em todas as páginas
const [loading, setLoading] = useState(false);
const [error, setError] = useState<string | null>(null);

const handleAction = async () => {
  setLoading(true);
  setError(null);
  try {
    const result = await someApi.method(params);
    // update state with result
  } catch (err) {
    setError(err instanceof Error ? err.message : 'Erro desconhecido');
  } finally {
    setLoading(false);
  }
};
```

---

## Configuração DI — Program.cs

```csharp
// Repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IRefillOrderRepository, RefillOrderRepository>();
builder.Services.AddScoped<ICylinderRepository, CylinderRepository>();
// ...

// Handlers (pattern: fully-qualified class name)
builder.Services.AddScoped<Botijas.Application.Customers.Commands.CreateCustomerCommandHandler>();
builder.Services.AddScoped<Botijas.Application.Customers.Queries.SearchCustomersQueryHandler>();
// ... (ver Program.cs para lista completa)
```

---

## Notas de Implementação Importantes

1. **PhoneNumber.Create é chamado no EF Core** na conversão de leitura — NÃO adicionar validação de max ao Create para não quebrar dados existentes com >9 dígitos. Validar max apenas no handler da aplicação.

2. **SaveChangesAsync vs múltiplos repositórios**: Todos partilham o mesmo `BotijasDbContext` no scope DI, então o `SaveChangesAsync` de qualquer repositório persiste tudo pendente no DbContext.

3. **`BotijasDbContext` pode ser injectado directamente** nos handlers da Application layer (padrão já usado em `GetFillingQueueQueryHandler` e repositórios). Necessário quando se precisa de acesso a múltiplos DbSets não expostos pelos repositórios (ex: `CylinderHistory`, `CylinderRefs` directamente).

4. **`IRefillOrderRepository.FindAllByCustomerAsync`** já existe — reutilizar para verificar se cliente tem histórico antes de excluir.

5. **Grid de navegação** em `page.tsx` é `grid-cols-1 md:grid-cols-2 lg:grid-cols-4` — ao adicionar 5º card, considerar `lg:grid-cols-3 xl:grid-cols-5`.
