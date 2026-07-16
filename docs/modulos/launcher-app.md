# Módulo Launcher App

## Objetivo do módulo

`LegendLauncher.App` é o projeto-fonte do executável WPF x64 distribuído como **Urus Launcher** / `UrusLauncher.App.exe`. Ele apresenta perfis, plataformas e servidores, mantém a interface responsiva durante I/O, coordena autenticação/persistência e alterna entre duas superfícies: o launcher de três colunas inspirado no mock 1584×992 e o workspace multissessão descrito em [game-session-workspace.md](game-session-workspace.md). Toda a interface própria pode alternar dinamicamente entre português brasileiro, inglês e espanhol conforme [localizacao.md](localizacao.md). A mesma janela hospeda o [pedido de doação](donation-prompt.md) e o cartão de [atualização](atualizacao.md), que consulta e prepara releases em segundo plano sem bloquear catálogo, login ou jogo e nunca executa o setup sem **INSTALAR**.

O processo WPF nunca carrega o Adobe Flash ActiveX. Cada conta aberta permanece em seu próprio `LegendLauncher.GameHost.Legacy`; a App recebe PID/HWND validados e incorpora somente a janela externa sob um proxy Win32 pertencente ao launcher. Isso preserva o isolamento de processo, mas não constitui uma sandbox. O fluxo é direto e não inicia `H2Proxy.exe`.

## Arquivos, classes e funções principais

| Referência aproximada | Tipo/função | Responsabilidade, entrada e saída |
| --- | --- | --- |
| `src/LegendLauncher.App/App.xaml:1` | `App` e recursos globais | Define `MainWindow` e incorpora `Themes/Colors.xaml`, `Controls.xaml` e `WindowStyles.xaml`. |
| `src/LegendLauncher.App/App.xaml.cs:16` | `OnStartup(...)` / `OnExit(...)` | Mantém um mutex local à sessão; a primeira instância lê/aplica idioma e cria a janela, enquanto outra tentativa localiza o mesmo executável na sessão, restaura apenas se minimizado, ativa a janela existente e encerra. Na saída, libera o mutex. |
| `src/LegendLauncher.App/LegendLauncher.App.csproj:13` | Marca e artefato | Empacota o PNG Urus, define `AssemblyName=UrusLauncher.App`, mantém `RootNamespace=LegendLauncher.App`, aplica metadados “Urus Launcher” e usa `urus-launcher.ico`. |
| `src/LegendLauncher.App/Assets/Branding/urus-logo.png` | Logo público | Monograma “U” transparente original usado no cabeçalho e janela desacoplada; o contrato completo está em [branding.md](branding.md). |
| `src/LegendLauncher.App/MainWindow.xaml:1` | `MainWindow` | Chrome próprio em 1420×820, mínimo 1180×700 e caption row de 44 px. Contém launcher e `GameWorkspaceView` na mesma janela. |
| `src/LegendLauncher.App/MainWindow.xaml:51` | `LauncherHeader` | Cabeçalho de 96 px visível apenas no launcher; exibe logo, “U R U S LAUNCHER” e `Brand_Subtitle`, e colapsa no workspace. O botão PayPal compacto começa por volta da linha 72 e o seletor de idioma por volta da linha 110. |
| `src/LegendLauncher.App/MainWindow.xaml:104` | Superfície de três colunas | Grade responsiva do launcher; painel de contas começa por volta da linha 114 e lista vários perfis, criação, seleção, edição, exclusão e servidores recentes. |
| `src/LegendLauncher.App/MainWindow.xaml:284` | `UpdateStatusView` | Insere o cartão de atualização na base da coluna de perfis, sem deslocar catálogo ou sessão. |
| `src/LegendLauncher.App/MainWindow.xaml:299` | Catálogo/versões | Seletor de plataformas OAS/7wan, busca, refresh e status. A lista por volta da linha 407 usa o template compartilhado com selos por função e divisor localizado. |
| `src/LegendLauncher.App/MainWindow.xaml:441` | Sessão e ação | Perfil, login, senha transitória/cofre e servidor. O setup dentro de `SessionSetupScrollViewer` rola quando necessário; compatibilidade, CTA e legenda permanecem fixos. |
| `src/LegendLauncher.App/MainWindow.xaml:684` | Workspace e caption buttons | Liga o workspace na segunda linha e sobrepõe os três botões compartilhados da janela, com 44 px, às duas superfícies. |
| `src/LegendLauncher.App/MainWindow.xaml:713` | `DonationPromptOverlay` | Mantém o pedido de apoio como último filho visual, sobreposto por Z-index alto e ligado ao estado do view model. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml:1` | `DonationPromptView` | Modal arredondado e localizado com QR PayPal original, chave PIX CNPJ copiável, foco cíclico, Escape e ações de fechamento. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml.cs:9` | PIX e foco do modal | Define o CNPJ público, copia exatamente a chave com feedback de sucesso/falha (linha 23) e restaura foco/feedback ao exibir (linha 42). |
| `src/LegendLauncher.App/MainWindow.xaml.cs:21` | `MainWindow()` | Cria cliente HTTP, compõe dependências, liga `DataContext`, registra eventos de workspace/janelas desacopladas e aplica a maximização limitada à área útil do monitor. |
| `src/LegendLauncher.App/MainWindow.xaml.cs:39` | `OnLoaded(...)` | Inicializa settings, perfis e catálogo uma única vez. |
| `src/LegendLauncher.App/MainWindow.xaml.cs:50` | Senha e ação de jogo | Mantém a senha no estado transitório e sincroniza o `PasswordBox` imediatamente antes do comando. |
| `src/LegendLauncher.App/MainWindow.xaml.cs:63` | Abertura dos seletores | Aplica ao idioma e às versões o mesmo fluxo explícito que abre a combobox por clique, Enter, Espaço, F4 ou Alt+Seta para baixo. |
| `src/LegendLauncher.App/MainWindow.xaml.cs:95` | Menu do perfil | Oferece jogar, editar, excluir e selecionar um servidor recente sem marcar uma simples seleção como partida. |
| `src/LegendLauncher.App/MainWindow.xaml.cs:220` | Caption commands | Encaminha minimizar, maximizar/restaurar e atualização do glifo para `BorderlessWindowCommands`. |
| `src/LegendLauncher.App/MainWindow.xaml.cs:244` | Shutdown | Fecha todas as janelas desacopladas sem reattach, mesmo se uma delas falhar, desliga eventos, descarta view model/workspace e cliente HTTP. |
| `src/LegendLauncher.App/MainWindow.xaml.cs:272` | Detach/reattach | Mantém uma janela desacoplada por sessão; criação/exibição têm rollback, e remoção, reattach e shutdown são idempotentes. |
| `src/LegendLauncher.App/BorderlessWindowCommands.cs:5` | `BorderlessWindowCommands` | Centraliza minimizar (linha 10), maximizar/restaurar (linha 16), decisão de estado (linha 28) e glifo (linha 33) para a janela principal e as desacopladas. |
| `src/LegendLauncher.App/BorderlessWindowWorkArea.cs:12` | `BorderlessWindowWorkArea` | Limita a maximização à work area e calcula o tamanho normal/restaurado em DIPs (linha 32). `RequiresNormalLimitsRefresh` (linha 58) reconhece mudanças de settings/display/DPI e a atualização coalescida começa na linha 122, inclusive com a janela parada. |
| `src/LegendLauncher.App/LauncherComposition.cs:27` | `CreateMainWindowViewModel(...)` | Compõe paths, cache, perfis, cofre, providers, runtime, settings, localização, áudio, workspace e `LauncherUpdateService`. |
| `src/LegendLauncher.App/LauncherComposition.cs:79` | `CreateHttpClient()` | Cliente compartilhado com redirects automáticos desativados, descompressão, timeout de conexão e User-Agent versionado. |
| `src/LegendLauncher.App/LauncherComposition.cs:106` | `FindLegacyRuntimeCandidate(...)` | Prioriza `runtime\` ao lado do executável; depois aceita configuração explícita e nomes Brov conhecidos em Program Files/Program Files (x86). Entrada pura existe para testes. |
| `src/LegendLauncher.App/Services/PlatformAdapterRegistry.cs:9` | `PlatformAdapterRegistry` | Resolve a combinação canônica de plataforma, diretório e autenticador; impede definições alteradas/duplicadas. |
| `src/LegendLauncher.App/Services/ProfilePlatformCompatibility.cs:7` | `ShareAccountIdentity(...)` | Considera variantes `oas-*` parte da mesma identidade de conta para reutilizar a credencial; IDs exatos continuam compatíveis e nenhuma credencial OAS atravessa para `sevenwan-*`. |
| `src/LegendLauncher.App/Services/SessionLaunchCoordinator.cs:41` | `LaunchAsync(...)` | Resolve senha digitada ou salva, autentica, inicia `IGameRuntime` e persiste perfil/UID/histórico/cofre. Saída: `SessionLaunchOutcome` com `GameSession` e o `EffectiveProfile` realmente persistido; se a etapa pós-runtime falhar, encerra o processo ainda não adotado. |
| `src/LegendLauncher.App/Services/SessionLaunchCoordinator.cs:98` | `ResolveCredentialAsync(...)` | Aceita a credencial do mesmo login entre variantes OAS, mas exige família compatível e nunca compartilha com SevenWan. |
| `src/LegendLauncher.App/Services/SessionLaunchCoordinator.cs:133` | Persistência pós-abertura | Atualiza UID e até cinco servidores recentes somente para a plataforma lançada, preserva os estados das demais variantes e devolve o mesmo perfil efetivo. |
| `src/LegendLauncher.App/Services/ProfileStorageCoordinator.cs:27` | `SaveAsync(...)` | Separa metadados não secretos do cofre; editar o mesmo login entre variantes OAS preserva ID/chave e materializa o estado por plataforma, enquanto mudar login ou família rotaciona a chave opaca. A seleção atual nunca vira “último jogado”. |
| `src/LegendLauncher.App/Services/ServerCatalogPresentation.cs:9` | `ResolveLastPlayedServerId(...)` | Resolve o histórico específico da plataforma, incluindo fallback legado apenas para a plataforma espelhada. Saída: ID normalizado ou nulo. |
| `src/LegendLauncher.App/Services/ServerCatalogPresentation.cs:25` | `BuildRows(...)` | Fixa o último realmente usado no topo, calcula entre servidores válidos já abertos o lançamento mais recente por `StartTimeUtc`/`NumericId`, ordena o restante e devolve linhas prontas para exibição. |
| `src/LegendLauncher.App/Services/ServerCatalogPresentation.cs:60` | `Filter(...)` / `Choose(...)` | Filtra código/nome/ID e recalcula o divisor para o resultado visível; a escolha prioriza ID desejado, último servidor jogável, lançamento mais recente jogável e primeiro servidor jogável. |
| `src/LegendLauncher.App/ViewModels/ServerRowViewModel.cs:6` | `ServerRowViewModel` | Expõe os papéis independentes `IsCurrent` e `IsLatestReleased`, selos/tooltips localizados, rótulo de seção e disponibilidade. Um servidor pode mostrar os dois selos ao mesmo tempo. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.cs:12` | `MainWindowViewModel` | Estado e comandos centrais, cancelamento de catálogo/login, seleção, mensagens saneadas e alternância launcher/workspace. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.cs:72` | `Workspace` e comandos de navegação | Compõe/recebe o workspace; comandos para adicionar conta, voltar ao launcher e abrir o workspace começam por volta da linha 92. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.cs:349` | `IsWorkspaceVisible` | Seleciona qual das duas superfícies WPF está visível sem encerrar sessões. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.cs:392` | Estado da sessão ativa | Exibe sessão ativa e troca a ação principal somente quando perfil, plataforma e servidor selecionados coincidem com uma aba em execução. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.cs:471` | `CanStartGame` | Reutiliza o alvo exato em execução ou exige runtime, servidor, login e senha digitada/salva para abrir outro alvo. `RuntimeStatusBrush` usa verde/vermelho conforme o probe real. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.cs:486` | `InitializeAsync()` | Restaura settings/último perfil, perfis e catálogo; usa defaults se settings falharem. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.cs:550` | `StartGameAsync()` | Reutiliza somente perfil+plataforma+servidor idênticos; outro servidor ou variante autentica, usa o `EffectiveProfile`, registra nova sessão e limpa a senha transitória. Se o workspace não adotar o PID/HWND retornado, encerra o GameHost órfão. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Catalog.cs:8` | `LoadServersAsync(...)` | Consulta por plataforma e pelo UID específico daquela variante, resolve somente seu histórico, cancela a operação anterior e rejeita respostas obsoletas. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Catalog.cs:103` | `ApplyServerFilter()` | Recria a lista visível pela busca, o que também remove ou reposiciona o divisor conforme o último servidor continue ou não no resultado. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Profiles.cs:9` | `LoadProfilesAsync(...)` | Ordena perfis por atualização e restaura o ID selecionado em settings. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Profiles.cs:168` | `PersistSelectedProfileAsync(...)` | Salva apenas o GUID do último perfil; falha não invalida a seleção em memória. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Localization.cs:6` | Localização do view model | Expõe o seletor, guarda status por chave/argumentos e reapresenta propriedades calculadas e linhas do catálogo quando o idioma muda. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Localization.cs:111` | `PersistLanguageAsync(...)` | Persiste a cultura canônica sem bloquear nem interromper a sessão em falha de I/O. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Donation.cs:8` | Cadência e comandos de apoio | Avalia o intervalo de cinco horas somente na abertura (linha 23), oferece abertura manual (linha 42), fechamento e persistência tolerante a falhas. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:135` | Inicialização e comandos do updater | Cria consulta, instalação e popup de notas; reage a sessões ativas e expõe estados localizados. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:167` | `CheckForUpdatesAsync()` | Consulta cedo uma vez por abertura, baixa e valida automaticamente um release superior e chega a `ReadyToInstall` sem executar o setup. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:223` | `InstallUpdateAsync()` | Exige setup preparado, nenhuma sessão e clique explícito em **INSTALAR**; então sinaliza à janela para encerrar somente após iniciar o instalador. |
| `src/LegendLauncher.App/Views/Updates/UpdateStatusView.xaml:19` | Cartão e popup de atualização | Status compacto, progresso, retry e novidades; o popup localizado começa na linha 100. |
| `src/LegendLauncher.App/Services/LauncherSettingsService.cs:96` | `SaveDonationPromptShownAsync(...)` | Atualiza atomicamente o instante UTC da última exibição sem apagar idioma, perfil, mudo ou layout. |
| `src/LegendLauncher.App/Localization/LocalizeExtension.cs:7` | `LocalizeExtension` | Liga textos XAML ao indexador observável do serviço global para atualização sem recriar janelas. |

## Fluxos e dados

### Perfis e credenciais

- A lista aceita vários `AccountProfile` de plataformas/usuários diferentes.
- Cada perfil guarda apenas metadados, uma chave opaca do cofre e, separadamente por plataforma, UID opcional e até cinco IDs recentes. Os campos antigos espelham a última variante usada para migração compatível.
- A senha fica no `WindowsCredentialVault` somente quando a opção de lembrar está marcada; o `PasswordBox` e os inputs de sessão são transitórios.
- Trocar entre Classic, Brasil, Reborn ou outra variante `oas-*` mantém a credencial do mesmo login, mas não mistura UID nem histórico de servidores. Trocar usuário ou família de provider invalida a identidade e rotaciona a chave antiga.
- Uma credencial salva recusada exige redigitação e só é substituída após login bem-sucedido.
- O histórico é atualizado somente depois de autenticação e abertura aceitas.

### Ordenação do catálogo por perfil

1. `RecentServerIdsByPlatform[plataforma][0]` define o servidor fixado no topo. Esse histórico é atualizado somente após uma abertura aceita; os campos legados servem como fallback apenas para sua plataforma espelhada e `catalog.Current` cobre a ausência de ambos.
2. O item fixado recebe **RECOMENDADO** porque representa o último servidor realmente usado por aquele perfil, e não uma recomendação genérica fornecida pela OAS.
3. Entre os servidores válidos que já abriram, o lançamento mais recente é escolhido pelo maior `StartTimeUtc`; empate ou datas ausentes usam o maior `NumericId`. Ele recebe **MAIS RECENTE** e vem antes dos demais servidores não fixados, mesmo quando a plataforma ainda é apenas catálogo.
4. Se houver mais itens depois do servidor fixado, a segunda linha abre a seção **OUTROS SERVIDORES** com um divisor visual. Se o mesmo servidor também for o lançamento mais novo, os selos **RECOMENDADO** e **MAIS RECENTE** coexistem na primeira linha.
5. A busca preserva a ordenação dos itens encontrados e recalcula o divisor: se o servidor fixado não estiver visível ou for o único resultado, não aparece uma separação órfã.

### Autenticação, abertura e multissessão

1. `MainWindowViewModel` valida perfil/plataforma/servidor/runtime.
2. `SessionLaunchCoordinator` obtém a credencial transitória e chama o autenticador do adapter selecionado.
3. A `LaunchSession` segue por Named Pipe ao GameHost; perfil, usuário e senha não atravessam essa fronteira.
4. O runtime devolve `GameSession` com PID/HWND e o coordenador devolve o `EffectiveProfile` já atualizado. A App cria o attachment, registra áudio e abre o workspace.
5. O launcher só volta a uma aba quando perfil, plataforma e servidor são idênticos. O mesmo perfil pode abrir outro servidor ou outra variante OAS em processo/sessão separado; criar personagem ou carregar um já existente continua sendo decisão do jogo.
6. Qualquer GameHost iniciado e não adotado é encerrado; fechar aba/processo atualiza os comandos e, quando não resta sessão, a UI retorna ao launcher.

Detalhes de layouts 1/2/4, abas, áudio, detach e HWND estão em [game-session-workspace.md](game-session-workspace.md).

### Idioma e mensagens

- `App.OnStartup` restaura `languageCode` antes de a primeira janela ser construída. O seletor oferece `pt-BR`, `en-US` e `es-ES` por nomes nativos.
- Textos estáticos usam `LocalizeExtension`; status de catálogo/login usam `LocalizedMessage`, evitando que uma mensagem antiga permaneça no idioma anterior.
- Ao trocar a cultura, o view model notifica campos calculados e atualiza explicitamente as linhas já criadas, inclusive **RECOMENDADO**, **MAIS RECENTE**, tooltips e **OUTROS SERVIDORES**, sem manter uma assinatura global por servidor.
- Menus de perfil construídos no code-behind resolvem suas chaves quando são abertos. Tooltips e nomes de acessibilidade usam o mesmo catálogo.
- Novos GameHosts herdam a cultura pelo ambiente do processo filho. A escolha e os limites de tradução estão documentados em [localizacao.md](localizacao.md) e [ADR-005](../decisoes/ADR-005-localizacao-dinamica.md).

### Identidade e distribuição

- A marca pública é Urus Launcher. Título, cabeçalho, doação, GameHost, metadados do Windows, ícone, atalhos e instalador não exibem mais os marcadores de Next/prévia/teste do protótipo.
- `Brand_Subtitle` apresenta “Jogue do seu jeito”, “Play your way” ou “Juega a tu manera”; Urus Launcher permanece invariável nos três idiomas.
- O PNG transparente foi gerado como identidade original pelo imagegen incorporado e teve somente o cromakey removido localmente. O desenho abstrato não usa touro, escudo ou trade dress de terceiros.
- O assembly/arquivo público é `UrusLauncher.App.exe`; namespaces, projetos e diretório de dados permanecem `LegendLauncher.*`/`LegendLauncherNext` como detalhes internos compatíveis.
- `scripts/build-urus-distribution.ps1` gera payload self-contained, exige/copia um runtime fornecido pelo mantenedor para `runtime\`, e produz instalador Inno e ZIP portátil. Veja [branding.md](branding.md), [distribuicao-windows.md](distribuicao-windows.md), [ADR-007](../decisoes/ADR-007-identidade-e-distribuicao-urus.md) e [ADR-009](../decisoes/ADR-009-provisionamento-runtime-legado.md).

### Atualização preparada automaticamente e instalação opcional

- Logo depois de restaurar settings/idioma em cada abertura, antes de perfis e catálogo, a App consulta uma vez o último release público esperado. A consulta é assíncrona; falha de rede ou release ausente não bloqueia as demais funções.
- O cartão inferior esquerdo informa busca, versão instalada, download, `ReadyToInstall`, erro ou instalação. Em `Current` e `Failed`, oferece **VERIFICAR NOVAMENTE**/**TENTAR DE NOVO**. Estado, ações e notas acompanham imediatamente o idioma selecionado.
- Encontrar uma versão superior inicia download e validação automáticos em `%LocalAppData%\LegendLauncherNext\updates`. O jogo permanece disponível durante consulta/download, inclusive para abrir novas sessões.
- O download possui janela total de uma hora para máquinas em conexões lentas. Uma única instância por sessão evita que dois processos disputem o mesmo `.part` ou que um instalador ignore contas abertas em outra App.
- Um setup com nome, bytes e SHA-256 exatamente iguais é reutilizado; cache divergente é removido e baixado novamente como `.part`. O arquivo final só chega a `ReadyToInstall` depois das validações estritas.
- **INSTALAR** é o único consentimento para executar o setup e fica desabilitado enquanto houver conta em jogo ou login/abertura em andamento. Quando a abertura termina e as sessões são fechadas, a pessoa pode instalar o arquivo já preparado sem novo download; caminho, nome, bytes e SHA-256 são revalidados antes da execução.
- O instalador fecha e relança o launcher. Perfis, settings e credenciais permanecem fora da pasta instalada e não são substituídos.
- A preparação automática entra na 1.1.3; versões 1.1.1/1.1.2 ainda usam o antigo clique **Atualizar** para chegar a ela uma vez. O contrato completo está em [atualizacao.md](atualizacao.md) e [ADR-008](../decisoes/ADR-008-atualizacoes-github-releases.md).

### Apoio voluntário

- `InitializeAsync()` avalia `LastDonationPromptUtc` uma única vez. O modal aparece na primeira execução, a partir de cinco horas ou diante de timestamp futuro; menos de cinco horas não exibe.
- Não existe timer recorrente. Permanecer com o launcher aberto, inclusive por 12 horas, não mostra um pedido sobre o jogo; a próxima avaliação ocorre somente ao reabrir.
- O botão PayPal do cabeçalho abre manualmente em qualquer momento e registra a exibição. Fechar por Escape, botão superior ou **Agora não** apenas oculta a superfície.
- O QR PayPal permanece byte a byte igual ao arquivo fornecido e a chave PIX CNPJ `57.646.942/0001-69` pode ser selecionada ou copiada localmente com feedback nos três idiomas.
- Nenhum dado de perfil, credencial ou sessão entra nesse fluxo. O contrato completo está em [donation-prompt.md](donation-prompt.md) e a decisão em [ADR-006](../decisoes/ADR-006-lembrete-doacao-nao-intrusivo.md).

### Janela responsiva e chrome

- A janela principal usa 1420×820 como tamanho inicial e 1180×700 como mínimo. No modo workspace, o cabeçalho de 96 px colapsa e a barra de jogo ocupa a caption row de 44 px.
- Os três caption buttons pertencem à `MainWindow`, atravessam launcher/workspace e usam os mesmos comandos borderless das janelas desacopladas.
- Na coluna de sessão, somente campos e seleção ficam no scroll. O status de compatibilidade, a ação `ENTRAR E JOGAR`/`VOLTAR AO JOGO` e a legenda continuam ancorados na base. O indicador usa o brush real do probe e o CTA desabilitado tem aparência inativa, evitando simular um botão clicável sem hover.
- A maximização usa a work area do monitor. O tamanho normal/restaurado é convertido para DIPs e limitado novamente quando monitor, estado, DPI, display ou work area mudam, mesmo sem movimentar a janela.

## Dependências e consumidores

- Usa modelos/contratos de [Core](core.md).
- Usa paths, JSON, cofre e probe de [Infrastructure](infrastructure.md).
- Usa catálogo/autenticação de [Providers OAS](providers-oas.md) e catálogo reconhecido de [Providers SevenWan](providers-sevenwan.md).
- Chama [GameHost Legacy](game-host-legacy.md), que aplica a política de [Network Bridge](network-bridge.md).
- Compõe o módulo de [Localização](localizacao.md), cuja cultura também é persistida por Infrastructure e propagada ao GameHost.
- Hospeda o [Pedido de Doação](donation-prompt.md), que usa somente UI, localização, settings não sensíveis e área de transferência local.
- Compõe a [Atualização pelo GitHub Releases](atualizacao.md), que usa rede pública, diretório local próprio e artefatos da distribuição sem acessar conta ou senha do jogo.
- Consome [Branding Urus](branding.md) e é a entrada principal da [Distribuição Windows](distribuicao-windows.md).
- É o executável principal; nenhum projeto de produção depende do WPF App.

## Testes e validação

`tests/LegendLauncher.Tests/App/` cobre view model principal, registry, perfis, histórico, catálogo, coordenação de abertura, cleanup de processo não adotado, settings, áudio, workspace, work area de janelas borderless e validação do attachment HWND/PID. `LauncherCompositionTests.cs` fixa a prioridade do runtime embutido e os fallbacks configurado/Brov. `MainWindowViewModelTests.cs` fixa Reborn salvo → Classic Português S100, botão habilitado, alvo exato enviado à autenticação e sessões distintas por plataforma/servidor. `ServerCatalogPresentationTests.cs` fixa o servidor do perfil no topo, o critério `StartTimeUtc` com desempate por `NumericId`, a coexistência dos papéis, a prioridade de seleção e o divisor recalculado pela busca. `MainWindowLayoutXamlTests.cs` fixa o template com os dois selos, a seção dos demais servidores e a apresentação honesta de prontidão. Os testes do workspace fixam capacidades 1/2/4, grade adaptativa, seleção visível, ativação por alvo exato, detach/reattach idempotente com rollback, fechamento e mudo global.

A localização acrescenta contratos de paridade/referências em `LocalizationCatalogTests.cs`, normalização e troca observável em `LocalizationServiceTests.cs`, default/migração/persistência em `LauncherSettingsServiceTests.cs` e reapresentação das fronteiras em `MainWindowViewModelLocalizationTests.cs`, `GameWorkspaceLocalizationTests.cs` e `ServerRowLocalizationTests.cs`. Este último cobre os dois selos e o divisor em `pt-BR`, `en-US` e `es-ES`. `GameHostLocalizationTests.cs` cobre a propagação normalizada ao processo separado.

`DonationPromptTests.cs` cobre primeira abertura, limites antes/exatamente/depois de cinco horas, relógio futuro, comando manual, fechamento e avaliação única. `DonationPromptAssetTests.cs` fixa os 62.216 bytes/hash do QR, seu painel sem clip, overlay/topologia, teclado, automação, presença do PIX e cópia exata do CNPJ. Na etapa específica do PIX, a suíte possuía 345 testes aprovados. O modal PayPal teve QA visual real aprovado; a inspeção manual específica após a inclusão do PIX foi interrompida por Escape e permanece registrada como pendente.

`BrandingAssetTests.cs`, os contratos de layout/localização e `GameHostLocalizationTests.cs` fixam PNG RGBA/ICO, remoção dos marks antigos, nome/slogan, `UrusLauncher.App.exe`, metadados, Pack URIs e distinção entre a marca Urus e o jogo Legend Online. `WindowsDistributionContractTests.cs` fixa o pipeline self-contained, nomes versionados, configuração Inno e hashing compatível com Windows PowerShell. A suíte funcional completa passou com **364/364 testes antes da pequena correção de hashing** e não foi repetida depois; os contratos específicos alterados passaram em saída isolada (**2/2**). A entrega atual é a 1.0.1, com smoke portátil de sete segundos, instalação silenciosa sem dados de perfil/conta/senha e desinstalação completa validados. Por pedido do usuário, esta alteração não recebeu QA visual manual pelo agente; a conferência visual será feita diretamente pelo usuário. O histórico de validação está em [`design-qa.md`](../../design-qa.md).

`LauncherUpdateViewModelTests.cs:10` cobre consulta/download automáticos na abertura, jogo disponível durante a preparação, `ReadyToInstall`, consentimento exclusivo por **INSTALAR**, nova verificação, troca dinâmica de idioma, falha e bloqueio da instalação durante sessão ativa ou login ainda em abertura. `LauncherUpdateLayoutTests.cs:3` fixa a posição inferior esquerda, popup, ações e bindings. Os contratos de rede, manifesto, cache exato, download, validação e execução ficam no subdiretório `tests/LegendLauncher.Tests/App/Updates/`; `WindowsDistributionContractTests.cs` fixa a guarda de instância única; o workflow e as notas fonte, incluindo a 1.1.3, são cobertos por `GitHubReleaseContractTests.cs:5`. O conjunto focado concluiu **82/82** e a suíte completa desta revisão concluiu **461/461** em Debug e **461/461** em Release.

- `GameWorkspaceXamlTests.cs:13` fixa a barra única de 44 px, controles/abas de 34 px, a reserva de 150 px, abas roláveis e `+ CONTA` fora do scroller.
- `BorderlessWindowCommandsTests.cs:12` e `:26` fixam a ação maximize/restore e o glifo para cada `WindowState`.
- `MainWindowLayoutXamlTests.cs:15` fixa tamanho, caption row, cabeçalho colapsável e botões compartilhados; também exige os handlers explícitos de abertura no seletor de idioma. O teste de setup fixa status/CTA/legenda fora da rolagem.
- `BorderlessWindowWorkAreaTests.cs:8` cobre work areas em várias origens; os testes a partir da linha 49 cobrem conversão DPI, clamp do tamanho normal/restaurado e restauração dos mínimos em monitor maior, e o teste da linha 90 fixa as mensagens que exigem refresh dinâmico.

A execução anterior confirmou duas sessões Reborn turco S115 simultâneas; a build compacta final revalidou uma sessão jogável, minimização/restauração, detach/maximize/reattach e a maximização em 3440×1392 dentro do monitor de 3440×1440. A comparação da barra compacta e da janela desacoplada está aprovada em [`design-qa.md`](../../design-qa.md).
