# Histórico de versões

As mudanças relevantes do Urus Launcher são registradas aqui. As versões públicas seguem versionamento semântico e são publicadas em `vMAJOR.MINOR.PATCH`.

## 1.1.0 — 15/07/2026

### Adicionado

- Verificação opcional de atualizações pelo GitHub Releases em toda abertura do launcher.
- Cartão de atualização no canto inferior esquerdo, com estado de busca, versão encontrada, progresso e notas da versão.
- Notas de versão em português brasileiro, inglês e espanhol.
- Download do instalador somente depois da confirmação explícita do usuário.
- Manifesto de atualização com versão, nome, tamanho e SHA-256 do instalador.
- Workflow público por tag para gerar e publicar os artefatos de cada release.

### Segurança e correções

- O instalador baixado é validado por nome, tamanho e SHA-256 antes de poder ser executado.
- URLs, repositório e redirecionamentos do download ficam restritos às origens HTTPS esperadas do GitHub.
- Falhas de rede ou de validação não bloqueiam login, catálogo ou jogo.
- Novas sessões ficam bloqueadas durante a instalação, com uma segunda conferência antes de executar o instalador.
- Downloads parciais abandonados e instaladores antigos passam por limpeza segura no próximo início.
- Corrigida a inicialização do cartão de progresso em builds portáteis e instaláveis.
- O workflow separa compilação e publicação, usa dependências fixadas e não persiste a credencial do GitHub no checkout.
- O pipeline de distribuição calcula SHA-256 por stream, inclusive no Windows PowerShell.

## 1.0.1 — 15/07/2026

### Adicionado

- O último servidor realmente jogado por cada perfil passou a aparecer primeiro como **RECOMENDADO**.
- O lançamento válido mais novo passou a receber o selo **MAIS RECENTE**.
- A lista passou a separar visualmente o histórico do perfil dos demais servidores.

### Corrigido

- Selecionar ou salvar um perfil não altera mais, indevidamente, o histórico de jogo.
- O empacotamento passou a funcionar de forma consistente no Windows PowerShell.

## 1.0.0 — 15/07/2026

- Primeira distribuição instalável do Urus Launcher para Windows x64.
- Perfis múltiplos, senha no Cofre do Windows, seleção de plataformas/servidores e workspace multissessão.
- Interface em português brasileiro, inglês e espanhol.
