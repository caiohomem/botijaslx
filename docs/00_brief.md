# Botijas CO₂ — Brief do Produto

## Objetivo
Sistema simples e confiável para gerir enchimento de botijas CO₂ (ex: SodaStream),
com foco em operação de loja, baixa fricção e rastreabilidade mínima.

## Problema que resolve
- Controle manual gera erros (botijas esquecidas, entregas parciais).
- Falta de rastreabilidade clara por botija.
- Impressão lenta ou inconsistente de etiquetas.

## Princípios
- Menos estados, menos cliques.
- Sistema reflete o mundo real (eventos concluídos).
- Operação não deve parar por falha temporária de internet.
- Conhecimento do projeto persiste em arquivos `.md`.

## Glossário
- **Cliente (Customer)**: pessoa que deixa e recolhe botijas.
- **Botija (Cylinder)**: recipiente físico de CO₂ identificado por QR.
- **Etiqueta (Label)**: QR reutilizável colado na botija.
- **Pedido (RefillOrder)**: agrupamento de botijas entregues juntas.
- **Entrega**: cliente deixa botijas na loja.
- **Recolha**: cliente busca botijas cheias.

## Regras Fixas
1. QR identifica a botija, nunca o pedido.
2. Etiquetas são reutilizáveis.
3. Pedido só é entregue quando todas as botijas estão prontas.

## Internacionalização
- Suporte a Português (PT-PT) e Inglês (EN)
- Seletor de idioma disponível no header
- Todas as strings traduzidas via next-intl

## Tema
- Suporte a tema claro e escuro
- Preferência salva no localStorage
- Respeita preferência do sistema operacional por padrão
