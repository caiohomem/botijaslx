# Processo Operacional Completo (AS-IS)

## 1. Objetivo
Descrever o funcionamento atual do sistema apenas pelo ponto de vista de processo operacional, para identificar melhorias que reduzam tempo e cliques.

## 2. Escopo do processo
- Entrada (`In`): receção de botijas e abertura/continuidade de pedido.
- Enchimento (`Filling`): execução operacional do enchimento.
- Saída (`Out`): entrega ao cliente e encerramento do ciclo.
- Processos de suporte: impressão de etiquetas, reimpressão, ocorrências, notificações e consulta.

## 3. Papéis operacionais
- Atendente de balcão:
  - Identifica cliente.
  - Regista entrada de botijas.
  - Imprime/reimprime etiquetas.
  - Faz atendimento de recolha.
- Operador de enchimento:
  - Consulta fila.
  - Marca botijas como prontas.
  - Regista problemas.
- Supervisor/gestão:
  - Acompanha dashboard e histórico.
  - Ajusta configurações operacionais.

## 4. Fluxo ponta a ponta (estado atual)

## 4.1 Início no balcão (In)
1. Atendente abre a tela `In`.
2. Pesquisa cliente existente ou cria novo cliente.
3. Sistema cria ou reutiliza pedido aberto do cliente.
4. Sistema carrega botijas já associadas ao pedido atual.
5. Atendente adiciona botijas:
   - Com QR já existente: escaneia.
   - Sem QR: cria botija nova e segue para impressão.
6. Se necessário, atendente abre modal de impressão, define quantidade e confirma.
7. Sistema cria `PrintJob` e cria botijas no pedido.
8. Etiquetas são geradas para impressão e associadas às botijas.
9. Atendente pode reimprimir última leva quando necessário.
10. Atendente finaliza a entrada.

### Resultado da etapa
- Pedido permanece aberto com botijas em estado inicial (`Received`).
- Botijas ficam associadas ao cliente/pedido.
- Etiquetas ficam vinculadas (quando aplicável).

## 4.2 Operação de enchimento (Filling)
1. Operador abre tela `Filling`.
2. Sistema lista botijas pendentes de enchimento.
3. Operador identifica botija por lista/QR.
4. Operador marca botija como pronta (`Ready`) após enchimento.
5. Em caso de problema, operador regista ocorrência (`Problem`) com tipo e observações.
6. Sistema recalcula progresso do pedido.
7. Quando todas as botijas do pedido ficam `Ready`, pedido muda para `ReadyForPickup`.

### Resultado da etapa
- Botijas prontas saem da fila de enchimento.
- Pedido pronto para recolha quando 100% das botijas estão prontas.

## 4.3 Entrega ao cliente (Out)
1. Atendente abre tela `Out`.
2. Sistema lista pedidos `ReadyForPickup`.
3. Atendente expande pedido e confere botijas.
4. Atendente executa entrega das botijas pendentes do pedido.
5. Sistema marca cada botija como `Delivered`.
6. Quando todas são entregues, pedido é concluído (`Completed`) e sai da lista de pendentes.

### Resultado da etapa
- Ciclo do pedido encerrado.
- Botijas entregues e rastreáveis no histórico.

## 5. Processos de suporte

## 5.1 Impressão de etiquetas
- `PrintJob` é criado para registrar solicitação de impressão.
- Job segue estados de controle (`Pending`, `Dispatched`, `Printed`, `Failed`).
- A confirmação de impressão (ACK) fecha o ciclo operacional da impressão.

## 5.2 Reimpressão
- Reimpressão é acionada quando etiqueta danifica/perde legibilidade.
- Processo mantém rastreabilidade pela associação de etiqueta à botija.

## 5.3 Ocorrências
- Problemas de botija são retirados do fluxo normal e marcados como `Problem`.
- Registro de observações suporta tratativa e auditoria.

## 5.4 Histórico e consulta
- Histórico por botija permite verificar eventos do ciclo:
  - Entrada.
  - Atribuição/troca de etiqueta.
  - Pronto.
  - Entrega.
  - Problema.

## 6. Regras operacionais observadas
- Um cliente deve operar com um pedido aberto por vez.
- Botija não deve estar em dois pedidos abertos simultaneamente.
- Pedido só fica `ReadyForPickup` quando todas as botijas estão `Ready`.
- Pedido só conclui quando todas as botijas foram entregues.

## 7. Pontos de atrito atuais (foco em eficiência)
- Sequência de ações na `In` ainda exige vários passos manuais para alta volumetria.
- Dependência de confirmações em telas/modais para cada bloco de ação.
- Troca entre contexto de impressão e contexto de operação pode gerar retrabalho.
- Casos de exceção (reimpressão/problema) aumentam cliques quando frequentes.
- Conferência final na `Out` pode manter passos redundantes se pedido já estiver integralmente entregue.

## 8. Indicadores de processo para otimização
- Tempo de atendimento por pedido (In -> Out).
- Cliques médios por pedido em cada etapa.
- Tempo de espera entre `In` e `ReadyForPickup`.
- Taxa de reimpressão por 100 botijas.
- Taxa de ocorrências por 100 botijas.
- Tempo médio de recolha por pedido pronto.

## 9. Fronteiras de melhoria (sem definir solução ainda)
- Redução de cliques na criação/continuidade de pedido.
- Agrupamento de ações repetitivas em lote.
- Menos mudança de contexto entre imprimir, associar e confirmar.
- Melhoria da visibilidade de estado para evitar ações desnecessárias.
- Tratamento mais rápido de exceções sem interromper fluxo principal.

---
Documento AS-IS: descreve o processo atual para servir de base ao desenho TO-BE (processo futuro otimizado).
