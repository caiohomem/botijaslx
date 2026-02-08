# Quick Start

## Instalação

```bash
cd web
npm install
```

## Executar em Desenvolvimento

```bash
npm run dev
```

Acesse:
- Português: [http://localhost:3000/pt-PT](http://localhost:3000/pt-PT)
- Inglês: [http://localhost:3000/en](http://localhost:3000/en)

O sistema redireciona automaticamente para `/pt-PT` se acessar a raiz.

## Usar Traduções

```tsx
'use client';

import { useTranslations } from 'next-intl';

export default function MyComponent() {
  const t = useTranslations('common');
  
  return <button>{t('save')}</button>;
}
```

## Usar Tema

```tsx
'use client';

import { useTheme } from '@/components/ThemeProvider';

export default function MyComponent() {
  const { theme, toggleTheme, setTheme } = useTheme();
  
  return (
    <div>
      <p>Tema atual: {theme}</p>
      <button onClick={toggleTheme}>Alternar</button>
      <button onClick={() => setTheme('dark')}>Escuro</button>
    </div>
  );
}
```

## Adicionar Nova Tradução

1. Edite `messages/pt-PT.json`:
```json
{
  "novaSecao": {
    "chave": "Valor em português"
  }
}
```

2. Edite `messages/en.json`:
```json
{
  "novaSecao": {
    "chave": "Value in English"
  }
}
```

3. Use no componente:
```tsx
const t = useTranslations('novaSecao');
<p>{t('chave')}</p>
```

## Estrutura de Rotas

- `/pt-PT` ou `/en` - Página inicial
- `/pt-PT/delivery` - Entrega
- `/pt-PT/filling` - Enchimento
- `/pt-PT/pickup` - Recolha
- `/pt-PT/dashboard` - Painel

Todas as rotas funcionam com ambos os locales.
