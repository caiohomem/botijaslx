# ADR-0002 — Internacionalização desde o Início

## Contexto
Sistema precisa suportar múltiplos idiomas (PT-PT e EN). Decisão deve ser tomada no início para evitar refatoração posterior.

## Decisão
Implementar internacionalização usando `next-intl` desde o início do projeto, com suporte a PT-PT e EN.

## Implementação
- Usar `next-intl` para gerenciamento de traduções
- Estrutura de rotas: `/app/[locale]/*`
- Arquivos de tradução: `/messages/pt-PT.json` e `/messages/en.json`
- Middleware para redirecionamento automático baseado em locale
- Seletor de idioma no header

## Consequências
- ✅ Suporte nativo a múltiplos idiomas
- ✅ Fácil adicionar novos idiomas no futuro
- ✅ URLs incluem locale (`/pt-PT/...` ou `/en/...`)
- ✅ Traduções centralizadas em arquivos JSON
- ⚠️ Requer tradução de todas as strings da UI
- ⚠️ URLs ficam mais longas

## Alternativas Consideradas
1. **react-i18next**: Mais complexo, não integrado ao Next.js
2. **next-i18next**: Baseado em Pages Router, não App Router
3. **Sem i18n**: Refatoração posterior seria custosa

## Status
Implementado e em uso.
