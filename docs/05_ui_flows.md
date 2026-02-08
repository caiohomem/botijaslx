# UI Flows

## Entrega
- Buscar cliente (pesquisa por nome/telefone)
- Criar pedido (se cliente novo, criar primeiro)
- Escanear botijas (QR code)
- Adicionar botijas sem etiqueta (cria novo cylinder)
- Imprimir etiquetas (para botijas sem etiqueta)
- Atribuir etiquetas (colar e escanear)

## Enchimento
- Scan QR da botija
- Ver contexto: cliente, pedido, progresso (ex: 2/5 prontas)
- Botão "Marcar como cheia"
- Quando todas prontas: notificação automática ao cliente

## Recolha
- Listar pedidos prontos (ReadyForPickup)
- Filtrar por cliente (opcional)
- Scan de cada botija do pedido
- Checklist visual (ex: 3/5 entregues)
- Concluir pedido quando todas entregues

## Painel
- Pendentes (pedidos abertos)
- Prontos (ReadyForPickup)
- Ocorrências (botijas com problema)
- Status do Gateway (conectado/desconectado)

## Internacionalização
- Seletor de idioma no header (PT-PT / EN)
- Todas as telas traduzidas
- Persistência da preferência de idioma

## Tema
- Toggle de tema no header (claro/escuro)
- Preferência salva automaticamente
- Respeita preferência do sistema por padrão
