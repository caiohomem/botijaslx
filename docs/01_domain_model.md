# Domain Model

## Agregados

### Customer
**Responsabilidade**
- Identificar o cliente.
- Normalizar e validar telefone.

**Atributos**
- CustomerId (GUID)
- Name
- Phone (VO)

**Invariantes**
- Telefone é obrigatório e válido.

---

### Cylinder
**Responsabilidade**
- Representar uma botija física.
- Controlar estado e etiqueta.

**Estados**
- Received (vazia)
- Ready (cheia)
- Delivered
- Problem

**Atributos**
- CylinderId (GUID)
- LabelToken (VO, opcional)
- State
- Occurrence (opcional)

**Invariantes**
- Não marcar Ready se não estiver Received.
- Não marcar Delivered se não estiver Ready.

---

### RefillOrder
**Responsabilidade**
- Agrupar botijas entregues juntas.
- Determinar quando está pronto para recolha.

**Estados**
- Open
- ReadyForPickup
- Completed

**Atributos**
- OrderId
- CustomerId
- Cylinders[]
- CreatedAt
- CompletedAt (opcional)

**Invariantes**
- Todas as botijas devem pertencer ao mesmo cliente.
- Pedido só fica ReadyForPickup quando todas as botijas estiverem Ready.

---

### PrintJob
**Responsabilidade**
- Representar uma solicitação de impressão.

**Estados**
- Pending
- Dispatched
- Printed
- Failed

**Invariantes**
- PrintJob é idempotente (não imprimir duas vezes).

---

## Value Objects
- PhoneNumber
- LabelToken

---

## Domain Events
- CustomerCreated
- OrderCreated
- CylinderReceived
- LabelAssigned
- CylinderMarkedReady
- OrderBecameReadyForPickup
- CylinderDelivered
- OrderCompleted
- PrintJobCreated
- PrintJobPrinted
- PrintJobFailed
