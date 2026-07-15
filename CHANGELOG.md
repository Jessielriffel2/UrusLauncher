# Histórico de versões

As mudanças relevantes do Urus Launcher são registradas aqui. As versões públicas seguem versionamento semântico e são publicadas em `vMAJOR.MINOR.PATCH`.

## 1.1.3 — 15/07/2026

### Atualização preparada em segundo plano

- A consulta ao GitHub começa no início da abertura e uma versão superior é baixada e validada automaticamente no diretório privado do usuário.
- O cartão separa claramente **Baixando**, **Pronta para instalar** e **Abrindo instalador**.
- O setup nunca é executado automaticamente: somente o clique explícito em **Instalar** inicia a instalação.
- Download e verificação não bloqueiam catálogo, login ou jogo; somente a instalação exige que não haja sessão ativa nem login em andamento.
- O estado **Launcher atualizado** informa a versão instalada e oferece **Verificar novamente** sem reiniciar o aplicativo.

### Portabilidade e eficiência

- Um setup já preparado é reaproveitado apenas depois de conferir novamente tamanho e SHA-256; cache inválido é removido e baixado outra vez.
- O download aceita até uma hora em redes lentas, sem depender da velocidade observada na máquina de desenvolvimento.
- O launcher passa a manter uma única instância por sessão do Windows e traz a janela existente para frente, evitando disputa de arquivos e instalação enquanto outra instância mantém contas abertas.
- Instalador, App e updater continuam por usuário, sem caminhos do computador de desenvolvimento, contas, perfis ou senhas incorporados.
- A execução permanece limitada ao release público, repositório, hosts, tag, nome, bytes e SHA-256 esperados.

### Corrigido

- Mudanças de mudo solicitadas durante uma aplicação de áudio em andamento deixam de ser perdidas e são reaplicadas imediatamente após o callback atual.

## 1.1.2 — 15/07/2026

### Corrigido

- Perfis OAS salvos deixam de perder a credencial ao trocar entre Reborn, Brasil, Classic Português e as demais variantes OAS.
- O botão **Entrar e jogar** passa a habilitar e autenticar exatamente a versão e o servidor escolhidos, incluindo Classic Português S100.
- UID, último servidor e histórico recente deixam de vazar de uma variante OAS para outra.
- Uma sessão aberta só é reutilizada quando perfil, plataforma e servidor coincidem; outro destino abre outra sessão.

### Compatibilidade e segurança

- Perfis 1.1.1 são migrados em memória e persistidos com estado separado por plataforma, mantendo o mesmo ID e a mesma chave do Cofre do Windows.
- Credenciais continuam isoladas entre OAS e SevenWan.
- O launcher não decide se a conta possui personagem: depois do login, cada servidor abre o fluxo normal do jogo, inclusive criação de personagem.

## 1.1.1 — 15/07/2026

### Corrigido

- Quando a API do GitHub responde `403` ou `429` por limite compartilhado, a consulta tenta a rota pública `releases/latest/download` do manifesto.
- Redes corporativas, provedores e outros ambientes que compartilham IP deixam de depender exclusivamente da cota da API.

### Segurança

- O fallback não baixa nem instala sem clique; ele apenas localiza o mesmo manifesto público por outra rota.
- Repositório, versão, origem, nome, tamanho e SHA-256 continuam sujeitos às mesmas validações da 1.1.0.

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
