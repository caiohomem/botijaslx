# Botijas CO₂ - Frontend

Frontend Next.js com suporte a internacionalização (PT-PT e EN) e tema claro/escuro.

## Funcionalidades

- 🌍 Internacionalização: Português (PT-PT) e Inglês
- 🌓 Tema claro e escuro com persistência
- 📱 Interface responsiva
- ⚡ Next.js App Router

## Desenvolvimento

```bash
npm install
npm run dev
```

Acesse [http://localhost:3000](http://localhost:3000)

### Configuração da API

Crie um arquivo `.env.local` na pasta `web/`:

```bash
NEXT_PUBLIC_API_URL=http://localhost:5001
```

Certifique-se de que o backend está rodando na porta configurada.

## Estrutura

- `/app/[locale]` - Rotas com suporte a locale
- `/components` - Componentes reutilizáveis
- `/messages` - Traduções (pt-PT.json e en.json)
- `/i18n.ts` - Configuração de i18n
