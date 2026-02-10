# Analise BPM Detalhada e Recomendacoes de Melhoria

## 1. Contexto da Analise

**Objetivo**: Avaliar o processo operacional AS-IS do sistema Botijas sob a otica de Business Process Modeling, identificando ineficiencias, gaps informativos e oportunidades de melhoria em UI/UX para maximizar a eficiencia operacional.

**Metodo**: Analise cruzada entre o documento AS-IS (`08_processo_operacional_as_is.md`) e a implementacao real do frontend (Next.js/React) e backend (.NET/PostgreSQL).

---

## 2. Avaliacao do Processo Atual (Scorecard)

| Dimensao                   | Nota (1-5) | Observacao                                                     |
|----------------------------|:----------:|----------------------------------------------------------------|
| Clareza do fluxo           | 3          | Fluxo linear claro, mas falta visibilidade entre etapas        |
| Eficiencia de cliques      | 2          | Muitos passos manuais, especialmente na etapa In               |
| Feedback ao utilizador     | 2          | Mensagens temporarias, sem indicadores persistentes de estado   |
| Tratamento de excecoes     | 2          | Excecoes interrompem fluxo principal, modal obrigatorio         |
| Visibilidade de progresso  | 3          | Existe parcialmente (progress na Filling), mas fragmentada      |
| Rastreabilidade            | 4          | Historico por botija e implementado e funcional                 |
| Automacao                  | 2          | Poucos gatilhos automaticos, muitas accoes manuais repetitivas  |
| Consistencia de UI         | 3          | Padroes visuais razoaveis, mas sem design system unificado      |

**Score Global: 2.6/5** - Processo funcional mas com margem significativa de otimizacao.

---

## 3. Analise Detalhada por Etapa

### 3.1 Etapa IN (Delivery) - Recepcao de Botijas

**Fluxo atual (7-10 cliques por botija nova)**:
```
Abrir In -> Pesquisar cliente -> Selecionar cliente -> [Criar pedido automatico]
-> Escolher: Scan QR OU Imprimir nova etiqueta
-> [Se imprimir]: Abrir modal -> Definir qtd -> Confirmar -> Preview -> Imprimir -> Fechar
-> Repetir para cada botija -> Finalizar entrada
```

#### Problemas identificados:

**P0 - Identificacao do cliente ineficiente (problema critico)**
- O `CustomerSearch.tsx` usa pattern de search-and-submit: o utilizador escreve, clica "Pesquisar", e so depois ve resultados. Sem debounce, sem typeahead, sem dropdown automatico.
- Nao existe opcao de escanear uma botija para identificar o cliente. O endpoint `GET /api/cylinders/scan/{qrToken}` ja retorna `customerName` e `customerPhone`, mas so e usado no Dashboard.
- **Para clientes recorrentes (caso mais frequente)**, o atendente precisa: digitar nome -> clicar pesquisar -> esperar -> selecionar. Quando poderia simplesmente escanear uma botija que o cliente ja traz.

**P0b - Sistema nao apresenta botijas conhecidas do cliente**
- Quando o cliente e selecionado, o codigo (`delivery/page.tsx:99-111`) chama `customersApi.getCylinders()` que retorna **todas as botijas historicas**, mas filtra e mostra **apenas as do pedido actual** (vazio):
  ```
  const existingOrderCylinders = customerData.cylinders
    .filter(c => c.orderId === newOrder.orderId)  // descarta historico
  ```
- O atendente nao ve que o cliente tem 3 botijas ja cadastradas com QR. Precisa escanear cada uma manualmente.
- Como o cliente normalmente traz as mesmas botijas, o sistema deveria mostrar as botijas conhecidas e permitir adiciona-las com um clique.

**P1 - Criacao sequencial de botijas (N+1 API calls)**
- No codigo (`delivery/page.tsx:174-181`), cada botija e criada individualmente num loop `for`:
  ```
  for (let i = 0; i < printQuantity; i++) {
    await ordersApi.addCylinder(order.orderId);
    await cylindersApi.assignLabel(cylinder.cylinderId, qrToken);
  }
  ```
- Para 5 botijas: 10 chamadas API sequenciais. Impacto direto no tempo de espera.

**P2 - Dupla interaccao modal para impressao**
- O utilizador precisa: (1) abrir modal de quantidade, (2) confirmar, (3) ver preview, (4) clicar imprimir, (5) fechar preview. Sao 5 interaccoes para uma unica accao logica.

**P3 - Sem atalho para volumes frequentes**
- Nao existem presets de quantidade (ex: "1 botija", "3 botijas", "5 botijas"). Cada atendimento comeca sempre do zero.

**P4 - Troca de contexto scan vs. impressao**
- O atendente precisa decidir entre escanear QR existente ou imprimir novo. Nao ha orientacao visual sobre qual caminho seguir.

**P5 - Finalizacao sem confirmacao de completude**
- O botao "Finalizar" nao valida se todas as botijas tem etiqueta atribuida. Botijas sem etiqueta passam para o Filling sem rastreabilidade.

**P6 - Loading overlay bloqueia toda a tela**
- O loading (`delivery/page.tsx:548-554`) usa overlay full-screen. Uma unica operacao congela toda a interface.

---

### 3.2 Etapa FILLING (Enchimento)

**Fluxo atual (2-3 cliques por botija)**:
```
Abrir Filling -> Ver fila -> Scan QR OU clicar "Pronto" -> Botija sai da fila
-> [Se problema]: Abrir modal -> Selecionar tipo -> Escrever notas -> Confirmar
```

#### Problemas identificados:

**P7 - Sem accao em lote (batch)**
- O operador marca uma botija de cada vez. Se um pedido tem 10 botijas e todas estao prontas, sao 10 cliques individuais em "Pronto".
- Nao existe "Marcar todas como prontas" ao nivel do pedido.

**P8 - Fila sem priorizacao visual**
- Todos os pedidos aparecem com o mesmo peso visual. Nao ha destaque para:
  - Pedidos mais antigos (FIFO prioritario)
  - Pedidos quase completos (falta 1 botija)
  - Pedidos com botijas com problema

**P9 - Refresh manual**
- A fila so atualiza com clique no botao "Atualizar" (`filling/page.tsx:319-327`). Nao ha polling nem WebSocket real para a fila (o SignalR so e usado para print jobs).

**P10 - Scan sem feedback sonoro/visual de sucesso**
- Quando o operador escaneia um QR, o feedback e apenas uma mensagem de texto verde temporaria. Em ambiente de producao ruidoso, feedback sonoro ou haptico seria mais eficaz.

**P11 - Sem contagem total visivel**
- O operador nao ve "Total na fila: X botijas" nem "Enchidas hoje: Y". Falta contexto de volume.

---

### 3.3 Etapa OUT (Pickup/Recolha)

**Fluxo atual (3-4 cliques por pedido)**:
```
Abrir Out -> Pesquisar (opcional) -> Expandir pedido -> Conferir botijas -> "Entregar Todas"
```

#### Problemas identificados:

**P12 - Expand obrigatorio antes de entregar**
- O utilizador precisa expandir o pedido para ver as botijas antes de poder entregar. Para pedidos simples (1-2 botijas), isto e um passo desnecessario.

**P13 - Entrega sequencial via API**
- No codigo (`pickup/page.tsx:56-58`), cada botija e entregue numa chamada separada:
  ```
  for (const cylinder of undelivered) {
    await pickupApi.deliverCylinder(order.orderId, cylinder.cylinderId);
  }
  ```
- Para 5 botijas: 5 chamadas sequenciais.

**P14 - Sem confirmacao antes da entrega**
- Nao existe dialogo de confirmacao. Um clique acidental em "Entregar Todas" e irreversivel.

**P15 - Pesquisa requer submissao manual**
- A pesquisa usa form submit (`pickup/page.tsx:87-91`). Nao ha debounce/live search como existe no CustomerSearch da etapa In.

**P16 - Sem indicador de tempo de espera**
- Nao e visivel ha quanto tempo o pedido esta pronto para recolha. Um pedido pronto ha 3 dias deveria ter destaque diferente de um pronto ha 30 minutos.

---

### 3.4 Processos de Suporte

**P17 - Impressao sem estado real da impressora**
- O PrintJob tem estados (`Pending`, `Dispatched`, `Printed`, `Failed`), mas o ACK e manual. Nao ha validacao real de que a impressora imprimiu.

**P18 - Reimpressao nao guarda historico do motivo**
- Quando o atendente reimprime, nao e registado o motivo (danificada, ilegivel, perdida). Dados uteis para KPIs de qualidade.

**P19 - Ocorrencias sem workflow de resolucao**
- Uma botija marcada como `Problem` sai do fluxo e nao tem caminho de retorno. Nao ha processo para resolver o problema e reintegrar a botija.

---

## 4. Recomendacoes de Melhoria (TO-BE)

### 4.1 Melhorias de Alta Prioridade (Impacto direto na eficiencia)

#### M0 - Identificacao rapida do cliente (scan botija + typeahead)
**Problema**: P0, P0b
**Proposta - 3 melhorias combinadas**:

**M0a - Typeahead com debounce no CustomerSearch**:
- Substituir pattern search-and-submit por typeahead/autocomplete.
- Resultados aparecem automaticamente ao digitar (debounce 300ms, minimo 2 caracteres).
- Dropdown flutuante com nome, telefone e numero de botijas do cliente.
- Sem botao "Pesquisar" (desnecessario com live search).
- Highlight do texto que faz match.

**M0b - Scan de botija para identificar cliente na tela In**:
- Adicionar scanner QR na etapa inicial (antes de selecionar cliente).
- Usa o endpoint existente `GET /api/cylinders/scan/{qrToken}` que ja retorna `customerName`, `customerPhone` e `customerId`.
- Ao escanear, sistema identifica o cliente automaticamente e avanca directo para o pedido.
- Fluxo: scan botija -> cliente identificado -> pedido aberto -> botija ja adicionada. Zero cliques alem do scan.

**M0c - Mostrar botijas conhecidas do cliente e permitir re-adicao rapida**:
- Apos selecionar cliente, mostrar seccao "Botijas conhecidas deste cliente" com botijas de pedidos anteriores (estado `Delivered`).
- Cada botija mostra: QR token, ultima visita, estado.
- Botao "Adicionar todas (N)" para re-adicionar todas as botijas conhecidas ao pedido actual com um clique.
- Botoes individuais "+" para adicionar selectivamente.
- Usar endpoint existente `GET /api/customers/{id}/cylinders` (ja chamado mas dados descartados).

**Impacto combinado**:
- Cliente recorrente com botija: scan -> pedido pronto. **~3 segundos, 0-1 cliques** (vs 15+ segundos, 5-6 cliques).
- Cliente recorrente sem botija: digita 2-3 letras -> seleciona -> ve botijas conhecidas -> "Adicionar todas". **~5 segundos, 2 cliques**.
- Cliente novo: sem alteracao ao fluxo actual (pesquisa + criar).

**Mudancas necessarias**:
| Componente | Mudanca |
|---|---|
| `CustomerSearch.tsx` | Reescrever: debounce + dropdown typeahead + mostrar nr botijas por cliente |
| `delivery/page.tsx` | Adicionar scan QR na etapa inicial via `historyApi.scanCylinder()` para resolver cliente |
| `delivery/page.tsx` | Apos selecionar cliente, mostrar botijas conhecidas (filtrar `Delivered` de pedidos `Completed`) |
| `delivery/page.tsx` | Botao "Adicionar todas" que chama `ordersApi.scanCylinder()` para cada botija conhecida |
| `delivery/page.tsx` | Unificar tabs "Pesquisar/Criar" num fluxo unico com fallback "Criar novo cliente" |
| Backend | **Nenhuma mudanca** - todos os endpoints ja existem |

#### M1 - API Batch para criacao de botijas
**Problema**: P1, P13
**Proposta**: Criar endpoint `POST /api/orders/{orderId}/cylinders/batch` que aceita `{ quantity: N }` e retorna array de botijas criadas com etiquetas ja atribuidas.
**Impacto**: Reduz N*2 chamadas para 1 chamada. Tempo de espera cai proporcionalmente ao volume.

#### M2 - Quick-actions na etapa In (presets de quantidade)
**Problema**: P2, P3
**Proposta**: Substituir o fluxo modal por botoes de accao rapida inline:
```
[1 Botija] [3 Botijas] [5 Botijas] [Personalizar...]
```
- Clique unico cria botijas + gera etiquetas + abre print preview.
- Apenas "Personalizar" abre modal para quantidades fora dos presets.
**Impacto**: Reduz de 5 interaccoes para 1 clique nos cenarios mais frequentes (80% dos casos).

#### M3 - "Marcar Todas Prontas" no Filling
**Problema**: P7
**Proposta**: Adicionar botao ao nivel do grupo/pedido: "Marcar X botijas como prontas".
- Com confirmacao rapida (swipe ou double-click, nao modal).
- Operador pode continuar a marcar individualmente para casos seletivos.
**Impacto**: Reduz de N cliques para 1 clique por pedido completo.

#### M4 - Entrega direta sem expand obrigatorio
**Problema**: P12
**Proposta**: Mostrar botao "Entregar X botijas" diretamente no header do pedido (sem precisar expandir).
- Expand continua disponivel para conferencia detalhada quando o atendente quiser.
- Adicionar confirmacao leve (1 clique de confirmacao) para evitar accao acidental (P14).
**Impacto**: Reduz de 3-4 cliques para 2 cliques por entrega.

#### M5 - Auto-refresh com polling/WebSocket no Filling e Out
**Problema**: P9
**Proposta**: Implementar polling a cada 15-30 segundos ou usar o SignalR ja existente para push updates.
- Indicador visual discreto "Ultima atualizacao: ha X min".
- Manter botao de refresh manual como fallback.
**Impacto**: Elimina necessidade de refresh manual, dados sempre atualizados.

---

### 4.2 Melhorias de Media Prioridade (Informativos e UX)

#### M6 - Priorizacao visual na fila de Filling
**Problema**: P8
**Proposta**: Aplicar indicadores visuais de prioridade:
- Borda amarela/laranja para pedidos com > 24h na fila
- Badge "Quase completo" para pedidos com >= 80% das botijas prontas
- Ordenar por: (1) quase completos, (2) mais antigos primeiro
**Impacto**: Operador identifica prioridades sem pensar, FIFO natural.

#### M7 - Contadores de contexto (KPIs operacionais inline)
**Problema**: P11
**Proposta**: Header fixo em cada tela com metricas operacionais:
- **In**: "Hoje: X entradas | Y botijas recebidas"
- **Filling**: "Na fila: X botijas | Enchidas hoje: Y | Com problema: Z"
- **Out**: "Prontos para recolha: X pedidos | Entregues hoje: Y"
**Impacto**: Contexto imediato sem navegar ao Dashboard.

#### M8 - Indicador de tempo de espera na Out
**Problema**: P16
**Proposta**: Mostrar tempo desde que o pedido ficou `ReadyForPickup`:
- "Pronto ha 30 min" (cinza)
- "Pronto ha 4h" (amarelo)
- "Pronto ha 2 dias" (vermelho com destaque)
**Impacto**: Priorizacao natural de atendimento por urgencia.

#### M9 - Pesquisa live com debounce na Out
**Problema**: P15
**Proposta**: Substituir form submit por pesquisa com debounce (300ms) como ja existe no `CustomerSearch`.
**Impacto**: Resultado instantaneo, menos um clique.

#### M10 - Feedback multimodal (som + visual)
**Problema**: P10
**Proposta**: Adicionar feedback sonoro para accoes criticas:
- Beep curto de sucesso ao marcar botija como pronta
- Som de erro ao escanear QR nao encontrado
- Som de celebracao ao completar pedido
- Opcao de desativar nas Settings
**Impacto**: Operador nao precisa olhar para a tela apos cada scan em ambiente de producao.

#### M11 - Loading granular (nao full-screen)
**Problema**: P6
**Proposta**: Substituir overlay full-screen por loading inline no botao/seccao que esta a processar.
- Botao mostra spinner interno (ja usado parcialmente no Filling).
- Resto da interface continua interativa.
**Impacto**: Interface nao congela, operador pode preparar proximo passo.

---

### 4.3 Melhorias de Menor Prioridade (Robustez e Rastreabilidade)

#### M12 - Workflow de resolucao de ocorrencias
**Problema**: P19
**Proposta**: Adicionar estados ao fluxo de problema:
```
Problem -> UnderReview -> Resolved (reintegrada) | Scrapped (descartada)
```
- Tela dedicada ou seccao no Dashboard para gestao de problemas.
- Botija resolvida pode reentrar na fila de Filling.
**Impacto**: Ciclo completo de gestao, menos botijas "perdidas" no sistema.

#### M13 - Motivo de reimpressao
**Problema**: P18
**Proposta**: Ao reimprimir, pedir motivo via select inline (nao modal):
- Danificada | Ilegivel | Perdida | Outro
- Registar no historico da botija.
**Impacto**: Dados para KPI de qualidade de etiquetas.

#### M14 - Validacao de completude antes de finalizar In
**Problema**: P5
**Proposta**: Antes de "Finalizar entrada":
- Verificar se todas as botijas tem etiqueta.
- Se houver botijas sem etiqueta, mostrar aviso inline (nao bloqueante): "X botijas sem etiqueta - deseja continuar?"
**Impacto**: Reduz botijas sem rastreabilidade no fluxo.

#### M15 - Guia visual de caminho (scan vs. impressao)
**Problema**: P4
**Proposta**: Na etapa In, apos selecionar cliente, mostrar decisao visual clara:
```
+---------------------------+    +---------------------------+
| Botija com QR existente   |    | Botija nova (sem QR)      |
| [Escanear codigo]         |    | [Criar + Imprimir etiq.]  |
+---------------------------+    +---------------------------+
```
- Cards lado a lado com icones distintos.
- Cada caminho leva ao fluxo correto sem ambiguidade.
**Impacto**: Atendentes novos aprendem o processo mais rapido.

---

## 5. Proposta de Fluxo TO-BE (Resumo Visual)

### Etapa In (otimizada):
```
Abrir In -> Identificar cliente:
            CAMINHO A (mais rapido - cliente recorrente com botija):
              Scan botija -> Cliente identificado automaticamente
              -> Pedido criado -> Botijas conhecidas listadas
              -> "Adicionar todas" (1 clique) -> Finalizar

            CAMINHO B (cliente recorrente sem botija):
              Digita 2-3 letras -> Typeahead mostra resultados
              -> Seleciona cliente (1 clique) -> Botijas conhecidas listadas
              -> "Adicionar todas" + scan novas -> Finalizar

            CAMINHO C (cliente novo):
              Pesquisa sem resultados -> "Criar novo cliente"
              -> Preenche dados -> Scan/Imprimir botijas -> Finalizar
         -> Escolher caminho visual: Scan QR | Nova botija
            -> Scan: escaneia e adiciona (0 cliques extra)
            -> Nova: Quick-preset [1] [3] [5] -> preview+print (1-2 cliques)
         -> Validacao de completude
         -> Finalizar (1 clique)
```
**Caminho A**: scan -> 1 clique -> pronto (~3 seg)
**Caminho B**: 3 letras + 2 cliques -> pronto (~5 seg)
**Caminho C**: Fluxo actual (sem alteracao)
**Antes**: 7-10 cliques/botija nova -> **Depois**: 0-3 cliques (recorrente) | 2-3 cliques (novo)

### Etapa Filling (otimizada):
```
Abrir Filling -> Fila priorizada auto-atualizada
             -> Scan QR (marca pronta automaticamente, com feedback sonoro)
             -> OU "Marcar Todas Prontas" no grupo (1 clique + confirmacao)
             -> Problemas: Quick-report inline (sem modal para tipos comuns)
             -> WhatsApp automatico quando pedido completo
```
**Antes**: 2-3 cliques/botija -> **Depois**: 1 clique/botija ou 1 clique/pedido

### Etapa Out (otimizada):
```
Abrir Out -> Lista com live-search e tempo de espera
          -> "Entregar X botijas" direto no header (1 clique)
          -> Confirmacao leve (1 clique)
          -> WhatsApp automatico
```
**Antes**: 3-4 cliques/pedido -> **Depois**: 2 cliques/pedido

---

## 6. Matriz de Priorizacao (Esforco vs. Impacto)

```
IMPACTO
  Alto  | M0 (ID cliente)     | M3 (Marcar todas)  | M2 (Quick-actions)
        | M1 (Batch API)      | M4 (Entrega direta) |
        | M5 (Auto-refresh)   |                     |
  ------+---------------------+---------------------+--------------------
  Medio | M11 (Loading inline)| M6 (Priorizacao)    | M7 (KPIs inline)
        | M9 (Live search)    | M10 (Feedback som)  | M8 (Tempo espera)
  ------+---------------------+---------------------+--------------------
  Baixo | M14 (Validacao)     | M15 (Guia visual)   | M12 (Workflow prob.)
        |                     | M13 (Motivo reimpr.) |
  ------+---------------------+---------------------+--------------------
        |     Baixo           |      Medio          |      Alto
                              ESFORCO
```

**Ordem recomendada de implementacao**:
1. **M0 (scan botija + typeahead + botijas conhecidas)** - maior impacto, zero mudancas backend
2. M1 + M3 + M4 (batch + accoes em lote - ganho imediato)
3. M2 + M11 + M9 (quick-actions + UX responsiva)
4. M5 + M6 + M7 (auto-refresh + visibilidade)
5. M8 + M10 + M14 (indicadores + feedback)
6. M12 + M13 + M15 (robustez + onboarding)

---

## 7. Boas Praticas de UI Recomendadas

### 7.1 Principios de Design para Operacoes Repetitivas
- **Lei de Fitts**: Botoes de accao principal devem ser grandes e proximos da area de atencao (ja parcialmente aplicado).
- **Reducao de carga cognitiva**: Maximo 3 opcoes visiveis por decisao. Esconder complexidade atras de "Mais opcoes".
- **Feedback imediato**: Toda accao deve ter resposta visual em < 100ms (skeleton/optimistic UI).
- **Tolerancia ao erro**: Accoes destrutivas (entregar, reportar problema) devem ter undo ou confirmacao. Accoes construtivas (adicionar botija) nao devem pedir confirmacao.

### 7.2 Padroes Especificos Recomendados
- **Optimistic UI**: Marcar botija como pronta na UI antes do retorno da API. Reverter se falhar.
- **Skeleton loading**: Em vez de texto "Carregando...", mostrar esqueleto da lista.
- **Toast notifications**: Substituir alertas inline por toasts nao-bloqueantes (ja parcial).
- **Stepper visual**: Na etapa In, mostrar barra de progresso dos passos (Cliente -> Botijas -> Etiquetas -> Concluido).
- **Empty states informativos**: Em vez de apenas "Fila vazia", mostrar "Todas as botijas foram enchidas. Proximo lote esperado as X:XXh" (se dados disponiveis).

### 7.3 Acessibilidade Operacional
- **Contraste**: Status badges devem funcionar para daltonicos (usar icones alem de cores).
- **Touch targets**: Minimo 44x44px para botoes em tablets (dispositivo provavel no balcao).
- **Keyboard navigation**: Tab order logico para uso com leitor de QR (que simula teclado).

---

## 8. KPIs de Processo Recomendados (Complemento ao AS-IS)

Alem dos indicadores ja documentados, recomendo:

| KPI                                | Formula                                        | Meta sugerida |
|------------------------------------|-------------------------------------------------|---------------|
| Cliques por entrada (In)           | Total cliques / Total entradas                  | < 5           |
| Tempo de scan-to-ready (Filling)   | Timestamp Ready - Timestamp Received            | < 4h          |
| Taxa de entrega no mesmo dia       | Pedidos entregues D+0 / Total pedidos           | > 70%         |
| Pedidos envelhecidos (> 48h)       | Count(ReadyForPickup > 48h) / Total Ready       | < 10%         |
| Erros de scan (QR nao encontrado)  | Scans falhados / Total scans                    | < 5%          |
| Utilizacao de presets (pos-M2)     | Entradas via preset / Total entradas             | > 60%         |

---

## 9. Plano de Implementacao - M0 (Primeira Iteracao)

### Fase 1: CustomerSearch com typeahead (M0a)
**Ficheiro**: `web/components/CustomerSearch.tsx`
**Mudancas**:
- Remover botao "Pesquisar"
- Adicionar debounce de 300ms no onChange
- Dropdown flutuante de resultados (max 5 resultados)
- Mostrar nome, telefone e contagem de botijas por cliente
- Highlight do texto que faz match
- Fechar dropdown ao selecionar ou clicar fora
- Estado vazio: "Nao encontrado? Criar novo cliente"

### Fase 2: Scan de botija para identificar cliente (M0b)
**Ficheiro**: `web/app/delivery/page.tsx`
**Mudancas**:
- Adicionar QrScanner na etapa inicial (step === 'search')
- Ao escanear, chamar `historyApi.scanCylinder(qrToken)`
- Se retornar cliente: construir objecto Customer a partir do resultado e chamar `handleCustomerSelect()`
- Se nao encontrar: mostrar erro "Botija nao encontrada" com fallback para pesquisa normal
- Remover tabs "Pesquisar / Criar" e unificar: pesquisa + scan em cima, link "Criar novo" em baixo

### Fase 3: Botijas conhecidas do cliente (M0c)
**Ficheiro**: `web/app/delivery/page.tsx`
**Mudancas**:
- Guardar resultado completo de `customersApi.getCylinders()` (nao descartar)
- Filtrar botijas de pedidos `Completed` com estado `Delivered` e que tenham `labelToken` (botijas rastreadas)
- Mostrar seccao "Botijas conhecidas" no step 'order', antes da area de scan
- Cada botija: token QR (primeiros 8 chars), data da ultima entrega
- Botao "Adicionar todas (N)" que adiciona ao pedido via `ordersApi.scanCylinder()` para cada uma
- Botao individual "+" por botija para adicao selectiva
- Apos adicionar, botija move da lista "conhecidas" para a lista "neste pedido"

### Dependencias
- Fase 1 e independente
- Fase 2 depende de Fase 1 (unificacao da tela inicial)
- Fase 3 e independente (pode ser feita em paralelo com Fase 2)

---

*Documento gerado como complemento ao AS-IS (doc 08). Serve de base para priorizacao e implementacao do processo TO-BE.*
