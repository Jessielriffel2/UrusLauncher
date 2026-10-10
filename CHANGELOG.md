# Histórico de versões

As mudanças relevantes do Urus Launcher são registradas aqui. As versões públicas seguem versionamento semântico e são publicadas em `vMAJOR.MINOR.PATCH`.

## 1.1.19 — 10/10/2026

### Corrigido

- **Botões "coladinhos" nas pontas**: os templates de `PrimaryButtonStyle` e `SecondaryButtonStyle` ignoravam o `Padding` (o conteúdo ia direto na borda). Agora repassam `Padding` ao conteúdo (`Themes/Controls.xaml`); botões de ícone mantêm `Padding="0"`.
- **Cartão de atualização**: só aparece quando há atualização para agir (baixando, pronta, instalando, ou falha de uma atualização conhecida); "launcher atualizado" e a verificação silenciosa não ocupam mais espaço (`IsUpdateCardVisible`). Título e texto ganham a largura toda e os botões INSTALAR e VER NOVIDADES ficam numa linha própria, maiores e com folga.
- **Modal de Novidades**: botão FECHAR mais baixo, com respiro nas laterais.
- **Perfil nunca fica desmarcado**: com perfis na lista, o ViewModel recusa a desseleção empurrada pela lista (Ctrl+clique, lista reconstruída) e a lista volta a marcar o perfil; só "novo perfil", "excluir" e "criar conta" limpam a seleção de propósito (`ClearSelectedProfile`).
- **Macro Assistant embutido no launcher**: a moldura, o painel e a mira (`MacroSetupWindow`, `MacroOverlayWindow`) deixaram de ser `Topmost`; passam a ser **de propriedade** da janela que mostra o jogo (o launcher ou a janela destacada, via `SetWindowOwner`/`GetRootWindow`), então ficam acima dela, escondem e minimizam junto e nunca flutuam sobre outros programas. A checagem de disponibilidade agora também exige que a janela anfitriã esteja visível e não minimizada (uma superfície dentro de janela minimizada continuava "visível"). `Stop()` esconde tudo primeiro, antes de salvar. A entrada continua só por `PostMessage` na HWND do jogo (não usa o mouse do jogador).

### Alterado

- **Painel do Macro Assistant** redesenhado: mais largo, campos numéricos com setas dentro de uma moldura arredondada (não são mais cortados), botões Play/Parar arredondados e iguais, divisor e texto maior.
- **Seletor de provedores**: caixa de busca no topo do dropdown (filtra ignorando maiúsculas e acentos; "Nenhum provedor encontrado" quando vazio; fechar o dropdown limpa a busca) e o provedor em destaque (SwordOnline) fica maior, em negrito e com faixa lateral.
- **Só o SwordOnline é oferecido**: o launcher público lista apenas o SwordOnline. Elarionis, OAS e 7wan saíram de `LauncherComposition` porque não funcionavam (catálogo OAS responde 403 da Cloudflare, login OAS recusado com 403, todos os servidores do 7wan encerrados). Os projetos e os testes continuam no repositório, desligados; o App deixou de referenciar Elarionis e 7wan.
- **Versão visível na tela**: a versão em execução aparece embaixo de "Dados protegidos" (`AppVersionText`, chave `App_VersionLabel`), para nunca haver dúvida sobre qual launcher está aberto.
- **Perfis de provedores que não existem mais** (ou do provedor de desenvolvimento) deixam de ser listados, mas continuam no disco sem alteração (`LoadProfilesAsync`).

## 1.1.18 — 10/10/2026

### Corrigido

- **Criar o perfil e jogar ficava complicado**: depois de salvar um perfil novo, o ENTRAR E JOGAR só liberava se a opção de salvar a senha no Cofre estivesse marcada, porque salvar zerava a senha digitada. Agora "Manter/salvar senha no Cofre do Windows" já vem marcado em conta nova (`NewProfile`, e ao abrir sem perfis) e, se o jogador desmarcar ou o Cofre falhar, a senha digitada é mantida em memória para a sessão (`SaveProfileAsync`) e espelhada no campo de senha do cartão (`MainWindow.xaml.cs`).
- **Botão do SwordOnline desativado em PC lento ou com internet ruim**: o catálogo sondava `config.xml` por até 5 s e, se a sondagem falhava, marcava o servidor como inválido, desativando o botão sem explicação. O servidor do SwordOnline agora é sempre jogável e o catálogo não usa mais a rede (`SwordOnlineServerDirectory`); se o portal estiver fora do ar, o login mostra a mensagem de rede.

## 1.1.16 — 08/10/2026

### Adicionado

- Novo provedor **SwordOnline** (`LegendLauncher.Providers.SwordOnline`): servidor próprio em `https://swordonline.tech`. A conta é criada no site; o launcher troca login e senha por uma ficha de jogo (`POST /api/launcher`) e abre o `Loading.swf` no Flash interno com `http=https:`. Detalhes em [providers-swordonline.md](docs/modulos/providers-swordonline.md).
- O SwordOnline é o **primeiro** da lista de plataformas e leva um selo **HOT** vermelho, com brilho pulsante, ao lado do nome (`PlatformDefinition.IsHot`; animação por `Storyboard` no item do combo, sem texto a traduzir).
- Abertura na plataforma em destaque: ao abrir, o launcher seleciona a plataforma HOT e um perfil dela (ou o último perfil usado, se nenhum for dessa plataforma), **sem abrir nenhum formulário sozinho**. Sem plataforma HOT o comportamento é o de sempre (`LoadProfilesAsync(preferHotPlatform)`).
- Campo de **senha embutido** no cartão da conta (painel direito): aparece quando o perfil exige senha e não há senha salva no Cofre do Windows, com a opção "Manter/salvar senha"; Enter joga (`ShowInlinePassword`).
- `swordonline.tech` entrou nas listas de permissão de host (`BridgeSecurityPolicy`, `ServerRowViewModel`), só em HTTPS/443.
- **Criar conta SwordOnline dentro do app**: o botão "Criar conta no SwordOnline" fica na coluna de contas da tela principal (sempre visível quando a plataforma selecionada é o SwordOnline) e também como link no editor de conta de um perfil novo. Ele abre um painel com login, senha, confirmação e e-mail opcional. O launcher envia `POST /api/conta/criar` (mesmo endpoint da página do site) e, se der certo, salva o perfil com a senha no Cofre do Windows, pronto para jogar. As regras do site (login 4–16 com letras, números e `_`; senha 8–64 com letra e número) são conferidas antes de ir à rede. Contrato novo `IAccountRegistrationService` no Core, `SwordOnlineAccountRegistrationService` no provedor, `PlatformDefinition.SupportsAccountCreation` e `MainWindowViewModel.Registration.cs` no app. Só o SwordOnline oferece o botão. Detalhes em [providers-swordonline.md](docs/modulos/providers-swordonline.md).

### Corrigido

- **Janela preta "Legend Online" no Alt+Tab e na visão de tarefas**: a janela de sessão do `GameHost` (`LegacyGameHostForm`) passa a ter `WS_EX_TOOLWINDOW` enquanto é de nível superior, então não aparece mais nos seletores de janela do Windows. A âncora do Cheat Engine não muda.
- **ENTRAR E JOGAR ficava desabilitado** para conta sem senha salva: o único campo de senha ficava dentro de "Editar conta" e salvar o perfil zerava a senha digitada (`MainWindowViewModel.Profiles.cs`, `PendingPassword = string.Empty`), então não havia como habilitar o botão sem marcar "salvar senha". Agora a senha pode ser digitada no próprio cartão.
- **O clique em ENTRAR E JOGAR apagava a senha digitada no cartão**: `PlayButton_OnClick` copiava o `PasswordBox` do editor (vazio) para `PendingPassword` antes de o comando rodar. Agora só copia senha realmente digitada (`PasswordSelection.FromBoxes`).

### Alterado

- `scripts/build-urus-distribution.ps1`: o ZIP portátil passa a usar `[ZipFile]::CreateFromDirectory` (o `Compress-Archive` do PowerShell 5.1 falhava de forma intermitente com "arquivo em uso" em `clretwrc.dll`/OCX) e há uma espera até o payload ficar destravado antes de zipar.
- `scripts/build-urus-distribution.ps1` valida que o payload do release contém somente provedores públicos e aborta caso contrário.
- `FeatureCatalogTests`: a última release esperada passou de v1.1.15 para v1.1.16 (`docs/releases/v1.1.16.json`).

## 1.1.15 — 26/09/2026

### Adicionado

- Quando a lista oficial de servidores está fora do ar, o launcher passa a oferecer o servidor que a conta já usou, em vez de desabilitar **ENTRAR E JOGAR** e travar a tela.
- Na mesma situação, um campo opcional permite digitar o número do servidor e jogar direto, inclusive em contas que nunca entraram em um servidor.

### Corrigido

- O launcher parava de oferecer a mesma versão para sempre depois de instalar: o binário era gravado com a versão anterior, então a atualização era detectada de novo a cada abertura. A 1.1.14 foi publicada com esse defeito; a 1.1.15 corrige.
- O empacotador passa a reprovar qualquer build cujo binário reporte uma versão diferente da release, e a gravar todas as propriedades de versão a partir do argumento `-Version`, para que o defeito não se repita.

## 1.1.14 — 26/09/2026

### Adicionado

- Falhas de plataforma que não são de credencial (HTTP 403, timeout, servidor fora do ar) exibem um banner de erro vermelho acima de **ENTRAR E JOGAR** em vez de abrir a edição de perfil; a senha salva é mantida para nova tentativa. Credencial rejeitada continua abrindo a edição para re-digitação.
- Cada conta logada recebe uma cor própria na barra lateral, permitindo distinguir de relance o avatar e os botões de relogar e fechar mesmo com várias contas abertas.

### Corrigido

- O botão **ENTRAR E JOGAR** não é mais cortado quando o banner de erro aparece: a linha passou de altura fixa para automática e o bloco de contas acima se ajusta.

## 1.1.11 — 24/09/2026

### Corrigido

- A âncora de compatibilidade do GameHost agora é realmente invisível: não cria mais uma janela “Urus GameHost”, título, borda ou entrada na barra de tarefas.
- O processo separado continua disponível para ferramentas de memória, mas o launcher permanece como a única janela visível e controla o encerramento das sessões filhas.
- O botão **Novidades** pulsa suavemente enquanto a release atual não foi vista; ao abrir o modal, o conteúdo é marcado como visto e o pulso desaparece.

## 1.1.10 — 24/09/2026

### Adicionado

- Macro Assistant com modos Gemas/Cristais, Cosmo e Cliques, configuração de moldura e controle único para iniciar ou parar todas as sessões.
- Catálogo de recursos e histórico de releases disponível pelo botão **Novidades**, com instruções de uso dos macros e fechamento por botão, Escape ou clique fora.
- Elarionis Online integrado ao catálogo e ao fluxo de autenticação.
- Logo oficial do PayPal no cabeçalho, mantendo o modal de apoio e o caminho PIX.

### Corrigido

- O rodapé não exibe mais dois controles “Parar macro”; qualquer macro ativa é interrompida pelo único botão de Macro Assistant.

## 1.1.9 — 23/08/2026

### Corrigido

- A âncora do GameHost deixa de abrir a janela visível com texto do Cheat Engine ao entrar na conta. O HWND top-level permanece oculto para ferramentas de memória x64.

## 1.1.8 — 23/08/2026

### Adicionado

- Falhas que não sejam login aceito e jogo aberto são gravadas em `Documentos/uruslauncher/logs`, com data/hora, operação, detalhes técnicos e texto da exceção, sem senha.

### Corrigido

- O `update-manifest.json` da 1.1.8 foi republicado em JSON válido. A consulta da 1.1.7 deixa de falhar na preparação da atualização.

## 1.1.7 — 21/08/2026

### Interface

- A lista de perfis filtra por nome e login enquanto a pessoa digita e passa a rolar com a barra personalizada.
- Os cartões de atualização e de dados protegidos viram faixas compactas, liberando altura para as contas.
- No workspace, a conta ativa fica destacada. No layout 2, as duas contas visíveis aparecem como bolinhas fundidas; clicar no agrupamento abre as duas em tela dividida.
- Clicar na busca de servidor ou de perfil esconde o placeholder e coloca o cursor no início.

### Corrigido

- Sessões que saem da grade deixam de reaparecer como janela de jogo por cima do launcher; o HWND é estacionado oculto e o encaixe é recalculado após o layout.

## 1.1.6 — 21/08/2026

### Corrigido

- A consulta de atualização deixa de recusar o `update-manifest.json` publicado com BOM UTF-8 pelo Windows PowerShell 5.1; o pipeline grava UTF-8 sem BOM e o parser ignora um BOM residual.
- Máquinas na 1.1.4/1.1.5 voltam a descobrir a versão nova pelo GitHub Releases.

### Compatibilidade

- Com o jogo aberto, o GameHost mantém uma janela top-level **Urus GameHost** na barra de tarefas para o Cheat Engine x64 anexar `LegendLauncher.GameHost.Legacy.exe` (não o processo WPF do launcher).
- Os executáveis nativos são publicados sem CET shadow stack, para o attach funcionar no Windows 11. Cheat Engine 32 bits continua incompatível com estes processos x64.

## 1.1.5 — 20/08/2026

### Removido

- O tradutor de tela (captura, OCR e overlay) saiu do launcher, da localização e da documentação.

### Adicionado

- Botão de relogar em cada aba de conta: encerra a sessão e entra de novo no mesmo servidor.

### Compatibilidade

- O curl nativo do Windows passa a atender todos os Passports OAS (Creaction e OAS Games).

## 1.1.4 — 16/07/2026

### Compatibilidade em instalação limpa

- O pipeline passa a exigir uma origem de runtime fornecida pelo mantenedor e copia somente `Adobe.Flash.Control.manifest` e o OCX x64 referenciado para `runtime\` no payload.
- Instalador e ZIP gerados a partir desse payload deixam de depender do cliente Brov estar previamente instalado no computador de destino.
- O launcher procura primeiro seu runtime empacotado e preserva `LEGEND_LEGACY_ROOT` e instalações Brov conhecidas como fallback.
- O build rejeita manifesto sem OCX confinado, ActiveX pequeno ou assinatura Authenticode inválida e registra bytes/SHA-256 no manifesto de distribuição.

### Interface

- O indicador de compatibilidade passa a refletir o estado real em verde/vermelho.
- O botão **Entrar e jogar** desabilitado agora possui aparência realmente inativa, em vez de parecer clicável sem responder ou animar.

### Distribuição

- A origem do runtime não entra no Git; cada distribuição deve comprovar autorização de redistribuição antes de publicação pública. O build não baixa Flash de terceiros.

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
