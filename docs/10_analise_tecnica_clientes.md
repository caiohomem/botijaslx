# Análise Técnica: Aba de Manutenção de Clientes

## Data: 2026-02-25

### Objetivo
Adicionar funcionalidade de manutenção de clientes com possibilidade de:
- Pesquisar e selecionar cliente
- Editar telefone (máx 9 dígitos)
- Listar botijas do cliente
- Excluir botija individual
- Excluir cliente (apenas sem histórico de pedidos)

---

## Stack Atual

### Backend
- **Framework:** ASP.NET Core (.NET 10)
- **Arquitetura:** Domain-Driven Design (DDD) com CQRS
- **Padrão:** Clean Architecture com 4 camadas:
  - Domain: Entities, Value Objects, Repository Interfaces, Domain Events
  - Application: Commands/Queries, Handlers
  - Infrastructure: EF Core, Repositories, Database
  - API: Controllers, REST endpoints

- **Database:** PostgreSQL com EF Core migrations
- **Padrão Data Access:** Repository pattern + Unit of Work (SaveChangesAsync)

### Frontend
- **Framework:** Next.js 14.1 (App Router)
- **Language:** TypeScript
- **Styling:** TailwindCSS
- **i18n:** next-intl (PT-PT, EN)
- **State:** useState/useEffect (React hooks)
- **API Client:** Fetch wrapper em `lib/api.ts`

---

## Modelos de Dados Existentes

### Customer (Cliente)
```
CustomerId: Guid (PK)
Name: string (200 chars max)
Phone: PhoneNumber (value object, digits only, >9 chars min, unique)
CreatedAt: DateTime (UTC)
```

### Cylinder (Botija)
```
CylinderId: Guid (PK)
SequentialNumber: long (auto-increment, display ID)
LabelToken: string (QR code, unique, nullable)
State: enum (Received, Ready, Delivered, Problem)
OccurrenceNotes: string (nullable, problem notes)
CreatedAt: DateTime (UTC)
```

### RefillOrder (Pedido)
```
OrderId: Guid (PK)
CustomerId: Guid (FK)
Status: enum (Open, ReadyForPickup, Completed)
CreatedAt, CompletedAt, NotifiedAt: DateTime
CylinderRefs: List<CylinderRef>
```

### Relationships
```
Customer (1) ----> (Many) RefillOrder
RefillOrder (1) ----> (Many) CylinderRef
CylinderRef (N:N junction, with cascade delete from RefillOrder)
Cylinder (1) <---- (N) CylinderRef (no reverse nav, no cascade from Cylinder)
CylinderHistoryEntry (N) --FK--> Cylinder (no cascade configured)
```

---

## Padrões Arquiteturais Existentes

### 1. Value Objects
- `PhoneNumber.Create(string)` → normaliza (remove não-dígitos), valida min 9 dígitos
- Imutável, stored as string em DB via `HasConversion`

### 2. Commands & Handlers
Exemplo: `CreateCustomerCommandHandler`
```csharp
public class CreateCustomerCommandHandler
{
    public async Task<Result<CustomerDto>> Handle(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        // Validação
        var phone = PhoneNumber.Create(command.Phone);
        var existing = await _customerRepository.FindByPhoneAsync(phone);
        if (existing != null) return Result.Failure("...");

        // Domain
        var customer = Customer.Create(command.Name, phone);
        await _customerRepository.AddAsync(customer);

        // Persistência
        await _customerRepository.SaveChangesAsync();

        return Result.Success(new CustomerDto { ... });
    }
}
```

### 3. Result Pattern
```csharp
Result<T> { bool IsSuccess, T? Value, string? Error }
Result { bool IsSuccess, string? Error }
```
Usado em todos os handlers para erro handling uniforme.

### 4. Repositories
Padrão: Interface em Domain, implementação em Infrastructure
```csharp
// Domain/Repositories
public interface ICustomerRepository
{
    Task<Customer?> FindByIdAsync(...);
    Task<Customer?> FindByPhoneAsync(...);
    Task<List<Customer>> SearchAsync(...);
    Task AddAsync(Customer customer);
    Task SaveChangesAsync();
}

// Infrastructure/Repositories
public class CustomerRepository : ICustomerRepository
{
    private readonly BotijasDbContext _context;
    // implementations
}
```

### 5. DI Registration
Em `Program.cs`:
```csharp
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<CreateCustomerCommandHandler>();
```

### 6. Controllers
RESTful, injeta handlers no construtor:
```csharp
[HttpPost]
public async Task<IActionResult> Create(
    [FromBody] CreateCustomerCommand command,
    CancellationToken cancellationToken)
{
    var result = await _createHandler.Handle(command, cancellationToken);
    if (!result.IsSuccess) return BadRequest(new { error = result.Error });
    return CreatedAtAction(..., result.Value);
}
```

---

## Frontend Patterns

### 1. Pages
Localização: `/web/app/{pageName}/page.tsx`
- `'use client'` directive
- `useTranslations()` do next-intl
- useState para state local
- Estrutura: search → select → details

### 2. API Client
```typescript
// lib/api.ts
export async function apiRequest<T>(endpoint: string, options?: RequestInit): Promise<T>

export const customersApi = {
  create: (data) => apiRequest<CustomerDto>('/api/customers', { ... }),
  search: (query) => apiRequest<{ customers: CustomerDto[] }>('/api/customers?query=...'),
  getCylinders: (customerId) => apiRequest<CustomerCylindersResult>(`/api/customers/${customerId}/cylinders`),
}
```

### 3. Components
Exemplo: `CustomerSearch`
- Props: `onSelect`, `onCreateNew?`, `disabled?`
- Debounce search
- Typeahead dropdown
- Reutilizável em múltiplas páginas

### 4. i18n
Chaves em `messages/{locale}.json`:
```json
{
  "navigation": { "delivery": "...", "filling": "..." },
  "common": { "cancel": "...", "delete": "..." }
}
```

---

## Fluxo de Deleção (Novo)

### Excluir Botija
1. Frontend: Modal de confirmação → `cylindersApi.delete(cylinderId)`
2. Backend: `DELETE /api/cylinders/{cylinderId}`
3. Handler: `DeleteCylinderCommandHandler`
   - Busca `Cylinder` por ID
   - Deleta `CylinderHistoryEntry` (sem cascade)
   - Deleta `CylinderRef` (FK sem cascade)
   - Deleta `Cylinder`
   - `SaveChangesAsync`
4. Frontend: Remove da lista local, mostra sucesso

### Excluir Cliente
1. Frontend: Modal de confirmação → `customersApi.delete(customerId)`
2. Backend: `DELETE /api/customers/{customerId}`
3. Handler: `DeleteCustomerCommandHandler`
   - Verifica se cliente existe
   - **Regra de Negócio:** Se tem pedidos → retorna erro "Não é possível eliminar clientes com histórico de pedidos"
   - Se sem pedidos → deleta cliente diretamente
   - `SaveChangesAsync`
4. Frontend: Se erro, exibe em modal sem fechar; se sucesso, limpa seleção

---

## Validação de Telefone (Max 9 dígitos)

**Locais onde validar:**

1. **Value Object (NÃO fazer aqui)**
   - ❌ NÃO adicionar `if (normalized.Length > 9) throw` em `PhoneNumber.Create`
   - Razão: EF Core chama `PhoneNumber.Create` durante materialização (leitura de BD)
   - Qualquer registo com >9 dígitos quebraria ao ler
   - Risco de crash em app existente

2. **Application Command Handler (✅ SIM)**
   ```csharp
   var digits = new string(command.Phone.Where(char.IsDigit).ToArray());
   if (digits.Length > 9)
       return Result.Failure("Phone number must have at most 9 digits");
   ```
   - Valida antes de criar value object
   - Seguro para registos existentes

3. **Frontend Input (✅ SIM)**
   ```tsx
   <input type="tel" maxLength={9} />
   ```
   - UX: previne entrada de >9 caracteres
   - Não é garantia de segurança (fácil contornar), mas boa prática

---

## Endpoints Novos

### PUT /api/customers/{id}/phone
```
Request:  { "phone": "123456789" }
Response (200): { "customerId": "...", "name": "...", "phone": "123456789" }
Response (400): { "error": "..." }
```

### DELETE /api/customers/{id}
```
Response (204): No Content (sucesso, sem body)
Response (400): { "error": "Não é possível eliminar clientes com histórico de pedidos" }
```

### DELETE /api/cylinders/{cylinderId}
```
Response (204): No Content
Response (400): { "error": "..." }
```

---

## Arquivos Modificados (Resumo)

### Backend (11 arquivos)
1. `Customer.cs` — add `UpdatePhone()`
2. `ICustomerRepository.cs` — add `DeleteAsync`
3. `ICylinderRepository.cs` — add `DeleteAsync`, `FindByCustomerIdAsync`
4. `CustomerRepository.cs` — implement novos métodos
5. `CylinderRepository.cs` — implement novos métodos
6. 6x Application Commands (6 novos arquivos)
7. `CustomersController.cs` — 2 novos endpoints
8. `CylindersController.cs` — 1 novo endpoint
9. `Program.cs` — registrar 3 handlers

### Frontend (6 arquivos)
1. `lib/api.ts` — fix 204, novos métodos
2. `app/page.tsx` — card Clientes
3. `components/CreateCustomerForm.tsx` — maxLength=9
4. `messages/pt-PT.json` — chaves i18n
5. `messages/en.json` — chaves i18n
6. `app/clientes/page.tsx` — NOVO, página principal

---

## Notas Importantes

1. **Sem migrations** — nenhuma mudança no schema BD
2. **Cascade delete complexo** — `DeleteCustomerCommandHandler` orquestra remoção manual de relacionamentos
3. **DI registration crítico** — sem registar handlers em `Program.cs`, controllers falham ao resolver
4. **i18n chaves** — novas chaves em ambos pt-PT e en
5. **204 fix** — `apiRequest` deve retornar `undefined` para 204 No Content
6. **Regra de negócio** — cliente só pode ser deletado se sem pedidos (proteção de integridade)

---

## Próximos Passos

1. Implementar backend em ordem: Domain → Application → Infrastructure → API
2. Registrar handlers em DI
3. Testar endpoints com Swagger/curl
4. Implementar frontend: api.ts → page.tsx
5. Adicionar i18n
6. Testar fluxo completo: pesquisar → editar → excluir
