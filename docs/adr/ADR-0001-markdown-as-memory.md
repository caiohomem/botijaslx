# ADR-0001 — Markdown como Memória Persistente

## Contexto
Conversas e LLMs perdem contexto com o tempo. Projetos complexos precisam de documentação persistente para manter consistência e evitar drift de regras.

## Decisão
Persistir decisões, regras e progresso em arquivos `.md` no repositório dentro da pasta `/docs`.

## Estrutura
- `00_brief.md` - Visão geral e regras fixas
- `01_domain_model.md` - Modelo de domínio
- `02_use_cases.md` - Casos de uso
- `03_architecture.md` - Decisões arquiteturais
- `04_api_contracts.md` - Contratos de API
- `05_ui_flows.md` - Fluxos de UI
- `06_progress_log.md` - Log de progresso
- `07_runbook.md` - Como executar o sistema
- `adr/` - Architecture Decision Records

## Consequências
- ✅ Fácil retomada de contexto
- ✅ Documentação sempre viva (atualizada junto com código)
- ✅ Menos dependência de histórico de chat
- ✅ Onboarding mais rápido para novos desenvolvedores
- ⚠️ Requer disciplina para manter atualizado
- ⚠️ Pode haver duplicação se não gerenciado

## Status
Aceito e em uso desde o início do projeto.
