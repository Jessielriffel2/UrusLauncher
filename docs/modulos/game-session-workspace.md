# Módulo Game Session Workspace

## Objetivo do módulo

O workspace multissessão pertence ao `LegendLauncher.App` e reúne, numa única superfície WPF, várias contas já autenticadas. Cada sessão continua executando o Adobe Flash ActiveX em seu próprio processo `LegendLauncher.GameHost.Legacy`; o launcher recebe apenas PID, HWND e instante de início, valida essa identidade e incorpora visualmente a janela externa por meio de um HWND-proxy pertencente ao processo WPF.

O módulo oferece abas por conta, seleção rápida, layouts de uma, duas ou quatro superfícies, janela desacoplada, encerramento individual e mudo global. Sua barra ocupa uma única linha de 44 px; controles e abas têm 34 px, a lista de abas rola horizontalmente, `+ CONTA` permanece visível fora dessa rolagem e 150 px são reservados para os caption buttons da janela principal. Na grade de quatro, a geometria ocupa a área disponível de forma adaptativa: uma sessão usa 1×1, duas usam 1×2 e três ou quatro usam 2×2. Não existe limite lógico de quatro sessões: o layout define quantas ficam visíveis simultaneamente, enquanto as demais continuam nas abas e na barra lateral. Um mesmo perfil não é aberto duas vezes; tentar iniciá-lo novamente seleciona a sessão existente. Todos os rótulos próprios do workspace e das janelas desacopladas acompanham dinamicamente o módulo de [Localização](localizacao.md).

## Arquivos, classes e funções principais

| Referência aproximada | Tipo/função | Responsabilidade, entrada e saída |
| --- | --- | --- |
| `src/LegendLauncher.App/Services/GameLayoutMode.cs:3` | `GameLayoutMode` | Define as capacidades `Single = 1`, `SplitTwo = 2` e `GridFour = 4` usadas pela seleção visual e pela persistência. |
| `src/LegendLauncher.App/Services/LauncherSettingsService.cs:7` | `LauncherSettingsSnapshot` | Snapshot não sensível de mudo global, layout, último perfil e idioma. O padrão é jogo mudo, grade de quatro, nenhum perfil fixado e `pt-BR`. |
| `src/LegendLauncher.App/Services/LauncherSettingsService.cs:17` | `LauncherSettingsService` | Lê e atualiza `settings.json` por `AtomicJsonFileStore`; documento ausente/corrompido usa defaults, layout desconhecido volta para quatro superfícies e cultura desconhecida volta para `pt-BR`. |
| `src/LegendLauncher.App/Services/LauncherSettingsService.cs:60` | Métodos de atualização | `SaveLastSelectedProfileAsync`, `SaveGamePreferencesAsync` e `SaveLanguageAsync` (linha 79) alteram campos independentes sem apagar as demais preferências. |
| `src/LegendLauncher.App/Services/GameAudioService.cs:3` | `GameAudioService` | Mantém o conjunto de PIDs dos GameHosts e reaplica o estado global de mudo em intervalo de 750 ms, cobrindo sessões de áudio que aparecem depois do processo. Falhas recuperáveis de Core Audio são capturadas sem derrubar o launcher. |
| `src/LegendLauncher.App/Services/GameAudioService.cs:47` | `SetMuted` / `RegisterProcess` / `UnregisterProcess` | Altera o estado global e controla quais processos pertencem ao workspace; não afeta áudio de outros aplicativos. |
| `src/LegendLauncher.App/Services/GameAudioService.cs:86` | `RefreshNow()` / `Dispose()` | Serializa aplicações concorrentes; `Dispose` (linha 123) para o timer e aguarda callbacks já iniciados antes de liberar o serviço. |
| `src/LegendLauncher.App/Services/CoreAudioInterop.cs:5` | `CoreAudioSessionController.TrySetMute` | Enumera as sessões do endpoint de renderização padrão via Core Audio e chama `ISimpleAudioVolume.SetMute` somente quando o PID pertence ao conjunto registrado. Mudanças/dispositivos ausentes são tratados como falha recuperável. |
| `src/LegendLauncher.App/ViewModels/GameSessionViewModel.cs:8` | `GameSessionViewModel` | Associa perfil, plataforma e servidor ao `GameSession`, observa a saída do processo, expõe títulos de aba/superfície e controla os estados selecionado, desacoplado e em execução. A notificação de saída volta à `Dispatcher` WPF antes de alterar propriedades. |
| `src/LegendLauncher.App/ViewModels/GameSessionViewModel.cs:110` | `Terminate()` | Encerra a árvore do GameHost daquela sessão; falha do Windows não impede a remoção do estado local. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:12` | `GameWorkspaceViewModel` | Fonte da verdade em memória para sessões, seleção, layout, mudo, comandos, superfícies visíveis e rótulos localizados. Compartilha o serviço global e remove seu handler no descarte. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:111` | `LayoutMode` | Recalcula linhas/colunas e a lista visível, mantendo a sessão selecionada dentro da capacidade 1/2/4, e persiste a preferência. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:131` | `IsMuted` | Aplica o mudo a todos os PIDs registrados e persiste a preferência. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:149` | `LayoutRows` / `LayoutColumns` | No modo de grade, produz 1×1 para uma sessão, 1×2 para duas e 2×2 para três ou quatro; os modos simples continuam respeitando suas capacidades. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:165` | `MuteLabel` / `FooterStatus` | Resolve mudo, singular/plural de sessões e estado do áudio na cultura atual; `LanguageChanged` os renotifica por volta da linha 324. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:182` | `ApplySettings(...)` | Restaura layout e áudio durante a inicialização sem criar sessões. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:200` | `AddSession(...)` | Valida/cria o vínculo HWND↔PID, registra o processo no áudio, adiciona a aba e seleciona a nova sessão. Se o attachment falhar, encerra o processo recém-iniciado. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:233` | `TryActivateProfile(...)` | Localiza uma sessão em execução pelo `ProfileId`; evita duplicar a mesma conta e devolve se encontrou. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:246` | `Reattach(...)` | Traz uma sessão desacoplada de volta ao conjunto de superfícies incorporadas sem reiniciar o GameHost. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:282` | Fechamento e detach | Encerrar termina/remove a sessão; desacoplar apenas a marca fora do grid e emite `DetachRequested`. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:309` | Saída espontânea do processo | Redireciona o callback para a `Dispatcher` WPF, remove a sessão, desregistra seu áudio e seleciona outra sessão válida. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:341` | `RefreshVisibleSessions()` | Filtra sessões em execução/não desacopladas até a capacidade do layout e substitui a última posição quando necessário para manter a seleção visível. |
| `src/LegendLauncher.App/GameHosting/NativeWindowMethods.cs:9` | `NativeWindowMethods` | Superfície Win32 estreita para validar handles/PIDs, criar o proxy local, alterar estilos, aplicar `SetParent`, redimensionar, mostrar, ocultar e focar. |
| `src/LegendLauncher.App/GameHosting/GameWindowAttachment.cs:7` | `GameWindowAttachment` | Vínculo reversível entre um HWND externo e proxies do launcher. Confere `IsWindow` e PID real, preserva o estilo standalone e só desanexa se o proxy informado ainda for o pai efetivo. |
| `src/LegendLauncher.App/GameHosting/GameWindowAttachment.cs:34` | `AttachTo` / `ResizeTo` / `DetachIfParent` | Move a mesma janela GameHost entre superfícies, ajusta seu client rect e restaura o estilo original no detach; rollback protege falhas parciais. |
| `src/LegendLauncher.App/GameHosting/EmbeddedGameSurfaceHost.cs:11` | `EmbeddedGameSurfaceHost` | `HwndHost` que cria um HWND-proxy do launcher, anexa a janela externa abaixo dele, acompanha tamanho/foco e destrói somente o proxy local ao desmontar. |
| `src/LegendLauncher.App/GameHosting/GameSurfacePresenter.cs:6` | `GameSurfacePresenter` | Converte a propriedade WPF `Attachment` em um novo `EmbeddedGameSurfaceHost`, permitindo mover a sessão entre grid e janela desacoplada. |
| `src/LegendLauncher.App/Views/Game/GameWorkspaceView.xaml:9` | Estilos compactos | `WorkspaceToolbarButtonStyle`, `LayoutButtonStyle` e `SessionTabBorderStyle` fixam controles/abas de 34 px. |
| `src/LegendLauncher.App/Views/Game/GameWorkspaceView.xaml:91` | `CompactGameToolbar` | Barra única de 44 px iniciada na linha 96; reúne voltar, abas, `+ CONTA`, mudo, layouts e detach. |
| `src/LegendLauncher.App/Views/Game/GameWorkspaceView.xaml:122` | Abas e ações permanentes | `SessionTabsScrollViewer` rola somente as abas; `+ CONTA` fica fora dele na linha 188, as ações começam na linha 195 e a coluna final reserva 150 px na linha 274 para os caption buttons da `MainWindow`. |
| `src/LegendLauncher.App/Views/Game/GameWorkspaceView.xaml.cs:13` | Abertura do idioma | Força a abertura do seletor da barra por clique e teclado, inclusive dentro da região de chrome próprio da janela. |
| `src/LegendLauncher.App/Views/Game/GameWorkspaceView.xaml:365` | Superfícies de jogo | Materializa `Workspace.VisibleSessions` na grade adaptativa abaixo da barra compacta. |
| `src/LegendLauncher.App/Views/Game/DetachedGameWindow.xaml:1` | `DetachedGameWindow` | Janela 1120×720, mínimo 760×500, com cabeçalho de 48 px, ações de 32 px e espaçamento próprio entre título, mudo, acoplar, encerrar e caption buttons. A superfície recebe margem 8×6 na linha 103. |
| `src/LegendLauncher.App/Views/Game/DetachedGameWindow.xaml.cs:10` | Ciclo da janela desacoplada | Fechar normalmente solicita reattach uma única vez; “Encerrar” suprime reattach somente após remoção; falha ao fechar permite nova tentativa. Minimize e maximize/restore usam os comandos compartilhados nas linhas 75 e 100. |
| `src/LegendLauncher.App/Views/Game/DetachedGameWindow.xaml.cs:117` | `DetachedWindowCloseState` | Torna pedido de fechamento e notificação de reattach idempotentes e permite suprimir/restaurar o reattach durante encerramento e rollback. |
| `src/LegendLauncher.App/BorderlessWindowCommands.cs:5` | `BorderlessWindowCommands` | Compartilha minimizar, maximizar/restaurar e o glifo de estado entre `MainWindow` e `DetachedGameWindow`. |
| `src/LegendLauncher.App/BorderlessWindowWorkArea.cs:12` | `BorderlessWindowWorkArea` | Aplica `WM_GETMINMAXINFO` para maximizar na work area e limita o tamanho normal/restaurado em DIPs. Reconhece refresh de settings/display/DPI na linha 58 e o agenda de forma coalescida a partir da linha 122, inclusive se a janela estiver parada. |
| `src/LegendLauncher.App/MainWindow.xaml.cs:272` | Orquestração de detach | Mantém uma janela por ID; `TryShow` registra antes de exibir e reanexa em rollback, enquanto `CloseAll` tenta fechar todas e limpa o registro no shutdown. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.cs:528` | `StartGameAsync()` | Volta à aba existente para o perfil ativo ou autentica/inicia outro GameHost, adota o `EffectiveProfile`, registra a nova sessão e abre o workspace; um processo não adotado é encerrado. |
| `src/LegendLauncher.App/LauncherComposition.cs:49` | Composição do workspace | Liga `settings.json`, localização compartilhada, `GameAudioService`, `GameWorkspaceViewModel`, runtime e view model principal. |

## Fluxo de uma sessão

1. O launcher autentica a conta e `LegacyGameRuntime` inicia um GameHost exclusivo.
2. O GameHost carrega o ActiveX e responde pelo Named Pipe com seu HWND; PID e ownership são validados antes de criar `GameSession`.
3. `GameWorkspaceViewModel.AddSession` cria `GameWindowAttachment`, registra o PID no áudio e adiciona uma aba.
4. `EmbeddedGameSurfaceHost` cria um proxy pertencente ao WPF. A janela externa recebe estilo filho e é colocada abaixo desse proxy; o ActiveX nunca entra no processo do launcher.
5. Trocar layout ou seleção rematerializa somente as superfícies necessárias. Na grade, 1/2/3–4 sessões ocupam respectivamente 1×1, 1×2 e 2×2. Destruir um presenter desanexa a janela, mas não encerra o processo.
6. Detach materializa outro presenter em `DetachedGameWindow`; falha de criação/exibição executa cleanup e reattach de rollback. Fechar a janela a reanexa no máximo uma vez; encerrar a sessão termina o GameHost e remove todos os seus vínculos.

## Barra, chrome e responsividade

- Toda a navegação do jogo fica em uma única barra de 44 px. Botões, abas e seletores de layout ocupam 34 px para conservar altura útil.
- Somente as abas rolam horizontalmente. `+ CONTA` continua visível à direita delas, e a última coluna de 150 px nunca recebe ações porque pertence aos três caption buttons compartilhados da janela principal.
- No workspace, o cabeçalho de 96 px do launcher colapsa. A `MainWindow` mantém a caption row de 44 px e seus botões de minimizar, maximizar/restaurar e fechar sobre a barra reservada.
- A janela desacoplada usa cabeçalho próprio de 48 px, ações de 32 px, margem entre cada comando e 8×6 ao redor da superfície do jogo; isso evita colisão entre título, ações e caption buttons na largura mínima.
- Maximizar usa a work area, não o retângulo completo do monitor. O tamanho normal/restaurado é limitado em DIPs e reavaliado quando DPI, display, work area, monitor ou estado mudam, ainda que a janela não seja movida.
- Toolbar, estado vazio, tooltips, nomes de acessibilidade e chrome desacoplado usam bindings do catálogo ativo. `MuteLabel` e `FooterStatus` são propriedades calculadas e recebem `PropertyChanged` na troca, sem recriar presenters nem tocar no processo do jogo.

## Settings e áudio global

`%LocalAppData%\LegendLauncherNext\data\settings.json` contém apenas `IsGameMuted`, o valor 1/2/4 de `LayoutMode`, `LastSelectedProfileId` e `LanguageCode`. Não contém login, senha, cookie, token, URI de sessão, PID nem HWND. Escritas usam a primitiva atômica de [Infrastructure](infrastructure.md); falha de persistência mantém a preferência somente durante a execução e não interrompe uma partida.

O áudio é global apenas dentro do workspace: todos os PIDs GameHost registrados recebem o mesmo estado. O padrão mudo reduz sobreposição sonora ao abrir várias contas. Uma sessão desacoplada continua registrada; outros processos do sistema não são alterados. Falhas recuperáveis de descoberta/aplicação são best effort, e `Dispose` aguarda callbacks do timer em voo.

## Segurança e ciclo de vida

- Cada sessão mantém processo, ActiveX, activation context e janela próprios; incorporação visual não equivale a execução no WPF e não cria uma sandbox.
- Tanto o GameHost quanto a App validam que o HWND pertence ao PID esperado. O proxy aceito por `GameWindowAttachment` precisa pertencer ao processo atual do launcher.
- O HWND é um identificador opaco e não sensível, mas nunca é confiado sem consulta ao kernel.
- Fechar uma aba encerra somente seu GameHost. Saída espontânea remove a aba automaticamente pela `Dispatcher`. Fechar o launcher tenta fechar todas as janelas desacopladas sem reattach e termina todas as sessões rastreadas.
- Layout quatro significa até quatro superfícies simultâneas, não quatro contas máximas. A grade usa 1×1, 1×2 ou 2×2 conforme a quantidade visível; sessões fora da capacidade continuam executando e podem ser selecionadas pelas abas/barra lateral.
- A janela principal e as desacopladas calculam a maximização pela área útil do monitor atual, preservando a barra de tarefas. Seus limites normais/restaurados acompanham dinamicamente mudanças de DPI, display e work area.

## Dependências, consumidores e referências cruzadas

O módulo usa WPF, `HwndHost`, Win32 `user32`, Core Audio e `AtomicJsonFileStore`. Consome `GameSession` e contratos do [Core](core.md), recebe a janela validada do [GameHost Legacy](game-host-legacy.md), usa paths/persistência de [Infrastructure](infrastructure.md), compartilha a [Localização](localizacao.md) e é coordenado pela [Launcher App](launcher-app.md). As decisões arquiteturais estão em [ADR-004](../decisoes/ADR-004-gamehost-incorporado-multissessao.md) e [ADR-005](../decisoes/ADR-005-localizacao-dinamica.md).

## Testes

- `GameWorkspaceViewModelTests.cs` cobre capacidades 1/2/4, grade adaptativa 1×1/1×2/2×2, manutenção da seleção visível, detach/reattach sem remoção, deduplicação por perfil, mudo e fechamento.
- `GameAudioServiceTests.cs` cobre registro/desregistro de PIDs, reaplicação do mudo global, captura de falhas recuperáveis e descarte aguardando callback em voo.
- `LauncherSettingsServiceTests.cs` cobre defaults, atualizações independentes, migração de settings antigos, normalização da cultura e recuperação de JSON corrompido.
- `GameWorkspaceLocalizationTests.cs` mantém mudo, rodapé e singular/plural sincronizados com `pt-BR`, `en-US` e `es-ES`, verifica as notificações e confirma que `Dispose` remove a assinatura global sem alterar sessões.
- `GameWindowAttachmentTests.cs` cobre estilos idempotentes, validação HWND/PID, ownership do proxy e guarda de detach.
- `BorderlessWindowWorkAreaTests.cs` cobre maximização taskbar-aware, conversão/clamp DPI do tamanho normal/restaurado e classificação de mensagens para refresh dinâmico.
- `GameWorkspaceXamlTests.cs` cobre a linha única de 44 px, controles/abas de 34 px, rolagem exclusiva das abas, `+ CONTA` sempre visível, reserva de 150 px para o chrome e os handlers de abertura do idioma.
- `BorderlessWindowCommandsTests.cs` cobre a decisão maximize/restore e o glifo compartilhado para cada estado da janela.
- `MainWindowLayoutXamlTests.cs` cobre tamanho 1420×820/mínimo 1180×700, caption row de 44 px, cabeçalho colapsável, caption buttons compartilhados e setup rolável com status/CTA/legenda fixos.
- `DetachedWindowLifecycleTests.cs` cobre fechamento/reattach idempotentes, supressão, rollback de criação/exibição e cleanup completo no shutdown.
- `GameHostWindowTests.cs`, `LaunchSessionIpcTests.cs` e `RuntimeModelsTests.cs` cobrem janela borderless, resposta versionada com HWND, ownership pelo PID e propagação opaca em `GameSession`.

A execução anterior confirmou duas sessões Reborn turco S115 reais; a build compacta final revalidou uma sessão jogável, comparou a barra compacta e o espaçamento da janela desacoplada e validou minimizar/restaurar/desacoplar/maximizar/acoplar. A maximização ocupou exatamente 3440×1392 da work area de um monitor 3440×1440. A comparação visual está aprovada em [`design-qa.md`](../../design-qa.md).
