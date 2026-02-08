# Casos de Uso

## Casos de Uso (Núcleo do Negócio)

### UC01 — Identificar Cliente (Criar/Editar)
**Ator:** Atendente  
**Gatilho:** Cliente chega para entregar botijas  
**Pré-condições:** Nenhuma  
**Pós-condições:** Cliente existe no sistema (nome + telefone)

**Fluxo Principal:**
1. Atendente pesquisa cliente por telefone/nome
2. Se não existir, cria cliente (nome, telefone)
3. Se existir, confirma dados (corrige se necessário)

**Critérios de Aceite:**
- Criar cliente com telefone válido
- Buscar por nome ou telefone
- Telefone duplicado: sistema sugere possível duplicidade

**Status:** ✅ Implementado

---

### UC02 — Receber Botijas do Cliente (Criar Pedido e Associar Botijas)
**Ator:** Atendente  
**Pré-condições:** Cliente identificado (UC01)  
**Pós-condições:** Pedido aberto e botijas associadas; botijas ficam Received (vazia)

**Fluxo Principal:**
1. Atendente inicia "Nova Entrega" para o cliente
2. Para cada botija:
   - Se já tem etiqueta: escaneia QR → sistema associa ao pedido
   - Se não tem etiqueta: marca como "sem etiqueta"
3. Sistema pergunta: "Quantas etiquetas imprimir agora?"
4. Atendente imprime etiquetas necessárias (UC03)
5. Atendente cola etiquetas e escaneia para registrar (UC04)
6. Finaliza entrega → pedido fica Open com N botijas

**Critérios de Aceite:**
- Pedido pertence a um único cliente
- Botija não pode estar em dois pedidos abertos
- QR inválido/rasgado: reimprimir etiqueta (UC04)

**Status:** ✅ Implementado

---

### UC03 — Imprimir Etiquetas (QR) para Botijas Novas
**Ator:** Atendente  
**Pré-condições:** Impressora local configurada  
**Pós-condições:** Etiquetas geradas e marcadas como "disponíveis para atribuição"

**Fluxo Principal:**
1. Sistema pergunta quantidade e confirma modelo de etiqueta
2. Sistema manda job para impressora local via SignalR
3. Sistema registra lote de etiquetas impressas

**Regras Importantes:**
- Etiqueta não contém dados do pedido (reutilizável)
- QR aponta para token/UUID da botija, não dados pessoais

**Exceções:**
- Impressora offline: modo pendente e tentar depois

**Status:** ✅ Implementado

---

### UC04 — Substituir/Reimprimir Etiqueta Danificada
**Ator:** Atendente  
**Pré-condições:** Botija presente na loja  
**Pós-condições:** Botija continua com mesmo vínculo lógico, ou migra para novo QR com trilha de auditoria

**Fluxo Principal:**
1. Atendente busca botija pelo cliente ou status
2. Marca "etiqueta danificada"
3. Imprime nova etiqueta e atribui à botija
4. Sistema registra histórico (QR antigo → QR novo)

**Implementação:**
- Endpoint POST /api/cylinders/{id}/assign-label
- UI na página de Entrega para atribuir etiqueta
- Guarda etiqueta anterior para auditoria

**Status:** ✅ Implementado

---

### UC05 — Listar Fila de Enchimento (Pendências)
**Ator:** Operador de Enchimento  
**Pré-condições:** Existir botijas Received (vazia)  
**Pós-condições:** Operador seleciona qual botija vai encher

**Fluxo Principal:**
1. Operador abre "Fila de enchimento"
2. Sistema mostra:
   - Botijas pendentes
   - Outras botijas do mesmo pedido que ainda faltam
   - Prioridade (ordem de chegada)

**Status:** ✅ Implementado

---

### UC06 — Encher Botija (Marcar como Cheia)
**Ator:** Operador de Enchimento  
**Pré-condições:** Botija está Received (vazia)  
**Pós-condições:** Botija passa a Ready (cheia); pode disparar notificação se pedido completo

**Fluxo Principal:**
1. Operador escaneia QR da botija
2. Sistema abre detalhe: botija + pedido + lista do que falta
3. Operador marca "Concluído" → estado Ready (cheia)

**Regra Crítica:**
- Só notificar cliente quando TODAS as botijas do pedido estiverem Ready

**Exceções:**
- Falha no enchimento: marcar Problem e criar ocorrência (UC07)

**Status:** ✅ Implementado

---

### UC07 — Registrar Ocorrência (Botija com Problema)
**Ator:** Operador / Atendente  
**Pós-condições:** Botija sai do fluxo normal

**Fluxo Principal:**
1. Escaneia botija
2. Seleciona tipo de problema (válvula, dano físico, etc.)
3. Sistema marca botija como Problem

**Status:** ✅ Implementado

---

### UC08 — Verificar se Pedido Está Completo e Disparar WhatsApp
**Ator:** Sistema (automático) / Atendente  
**Gatilho:** UC06 muda uma botija para Ready  
**Pré-condições:** Cliente tem telefone válido  
**Pós-condições:** Pedido vira ReadyForPickup e WhatsApp enviado

**Fluxo Principal:**
1. Sistema avalia: todas as botijas do pedido estão Ready?
2. Se sim:
   - Pedido → ReadyForPickup
   - Na página de Pickup, mostra botão "WhatsApp"
   - Atendente clica → abre wa.me com mensagem pré-definida
   - Sistema marca pedido como notificado

**Implementação:**
- Link wa.me com mensagem personalizada
- Campo `NotifiedAt` no RefillOrder
- UI mostra pedidos pendentes de notificação

**Status:** ✅ Implementado

---

### UC09 — Reenviar Notificação WhatsApp
**Ator:** Atendente  
**Pré-condições:** Pedido ReadyForPickup e notificação falhou  
**Pós-condições:** Novo envio registrado

**Implementação:**
- Usar wa.me permite reenvio natural (basta clicar novamente)
- Botão WhatsApp sempre disponível na página de Pickup

**Status:** ✅ Implementado

---

### UC10 — Entregar Botijas ao Cliente (Recolha e Baixa)
**Ator:** Atendente  
**Pré-condições:** Pedido ReadyForPickup  
**Pós-condições:** Botijas Delivered ao cliente; pedido Completed

**Fluxo Principal:**
1. Cliente chega para recolha e informa nome/telefone
2. Atendente abre pedido "ReadyForPickup"
3. Atendente escaneia o QR de cada botija entregue
4. Sistema valida: pertence ao pedido e está Ready
5. Ao escanear a última botija: pedido → Completed

**Exceções:**
- Cliente tenta levar só parte: bloquear (só entregar quando tiver todas)
- Botija escaneada não pertence ao pedido: alerta e bloqueio

**Status:** ✅ Implementado

---

## Casos de Uso de Suporte (Admin/Operacional)

### UC11 — Consultar Histórico de uma Botija
**Ator:** Atendente / Operador  
Mostra: quando foi recebida, enchida, entregue, ocorrências, trocas de etiqueta

**Implementação:**
- Entidade CylinderHistoryEntry para registrar eventos
- Endpoints: GET /api/cylinders/{id}/history, GET /api/cylinders/scan/{qrToken}
- Página Dashboard com busca por QR
- Timeline visual de eventos

**Status:** ✅ Implementado

---

### UC12 — Relatórios Operacionais
**Ator:** Gestor  
Exemplos: quantidade enchida por dia, tempo médio, pendências, ocorrências

**Implementação:**
- Endpoint GET /api/reports/stats
- Dashboard com cards de estatísticas
- Contagens: pedidos abertos, prontos, concluídos; botijas por estado
- Métricas diárias e semanais

**Status:** ✅ Implementado

---

### UC13 — Gestão de Configurações
**Ator:** Admin  
Modelo de etiqueta, mensagens WhatsApp, regras de prioridade, permissões

**Implementação:**
- Página /settings com configurações básicas
- Configurações armazenadas em localStorage
- Modelo de mensagem WhatsApp editável
- Seleção de modelo de etiqueta

**Status:** ✅ Implementado (versão básica)

---

## Regras de Negócio (Consolidadas)

1. Etiqueta é reutilizável → não leva dados do pedido
2. QR identifica a botija, não o pedido
3. Pedido pode ter várias botijas e todas vinculadas ao mesmo cliente
4. Notifica apenas quando todas estiverem cheias
5. Entrega (recolha) só acontece quando todas estiverem prontas (salvo override)
6. No enchimento, operador deve ver a botija atual + as restantes do mesmo pedido
