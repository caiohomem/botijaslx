# ADR-0003 — Tema Claro/Escuro Nativo

## Contexto
Usuários têm preferências diferentes para tema (claro/escuro). Sistema deve suportar ambos desde o início.

## Decisão
Implementar sistema de tema claro/escuro usando CSS variables e Tailwind CSS com classe `dark:`.

## Implementação
- Provider React (`ThemeProvider`) gerencia estado do tema
- Preferência salva em `localStorage`
- Respeita preferência do sistema (`prefers-color-scheme`) por padrão
- CSS variables para cores (fácil customização)
- Toggle no header para alternar manualmente

## Consequências
- ✅ Melhor experiência do usuário
- ✅ Acessibilidade (reduz fadiga visual)
- ✅ Implementação simples com Tailwind
- ✅ Persistência automática da preferência
- ⚠️ Requer definir paleta de cores para ambos os temas
- ⚠️ Testes devem considerar ambos os temas

## Alternativas Consideradas
1. **CSS puro**: Mais verboso, menos flexível
2. **Biblioteca externa**: Dependência desnecessária
3. **Sem tema escuro**: Pior experiência para usuários que preferem escuro

## Status
Implementado e em uso.
