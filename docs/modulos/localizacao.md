# Módulo Localização

## Objetivo do módulo

O módulo `LegendLauncher.App/Localization` oferece localização dinâmica de toda a interface pertencente ao launcher em português brasileiro, inglês e espanhol. A troca não reinicia o programa nem encerra jogos: textos XAML, mensagens de estado, linhas do catálogo, workspace, pedido de apoio, atualização e janelas desacopladas observam uma única cultura ativa. O GameHost herda essa cultura quando cada processo é iniciado e localiza suas próprias telas de preparação, diagnóstico e erro. A superfície principal usa linguagem simples e não expõe ao jogador detalhes como PID, GameHost x64, runtime, processo isolado ou nomes internos do fluxo de autenticação.

Os códigos canônicos persistidos são `pt-BR`, `en-US` e `es-ES`. Códigos das mesmas famílias, como `en`, `es-MX` ou `pt-PT`, são normalizados para uma das três opções; valor ausente ou desconhecido usa `pt-BR`. Nomes próprios, nomes oficiais de plataformas/servidores, dados digitados pela pessoa e o conteúdo Flash fornecido pela plataforma não são traduzidos.

## Arquivos, classes e funções principais

| Referência aproximada | Tipo/função | Responsabilidade, entrada e saída |
| --- | --- | --- |
| `src/LegendLauncher.App/Localization/LocalizationService.cs:9` | `LanguageOption` | Opção imutável do seletor com código canônico e nome nativo; `ToString()` devolve o nome para que templates WPF compactos nunca exibam a representação técnica do record. |
| `src/LegendLauncher.App/Localization/LocalizationService.cs:14` | `LocalizationService` | Fonte global observável da cultura e dos 204 textos de cada catálogo. Recebe um código opcional e expõe idioma, cultura, indexador, formatação e eventos de atualização. |
| `src/LegendLauncher.App/Localization/LocalizationService.cs:53` | `Get(...)` / `Format(...)` | Resolve uma chave no catálogo ativo; chave ausente tenta `pt-BR` e por fim retorna um marcador visível. `Format` usa a cultura ativa para números e datas. |
| `src/LegendLauncher.App/Localization/LocalizationService.cs:69` | `SetLanguage(...)` | Normaliza e aplica o idioma, atualiza a cultura das threads quando habilitado e notifica `LanguageCode`, `Culture`, `Item[]` e `LanguageChanged`. Saída: se houve troca efetiva. |
| `src/LegendLauncher.App/Localization/LocalizationService.cs:96` | `EnableThreadCultureUpdates()` | Faz a cultura selecionada valer para a thread atual e novas threads do launcher. |
| `src/LegendLauncher.App/Localization/LocalizationService.cs:102` | `NormalizeLanguageCode(...)` | Mapeia famílias `pt`, `en` e `es` para os três códigos canônicos; qualquer outra entrada volta a `pt-BR`. |
| `src/LegendLauncher.App/Localization/LocalizationService.cs:137` | `LoadCatalogs()` | Lê os três JSONs incorporados ao assembly, rejeitando recurso ausente, vazio ou com chave/valor em branco. |
| `src/LegendLauncher.App/Localization/LocalizedMessage.cs:3` | `LocalizedMessage` | Guarda chave e argumentos, não o texto já traduzido. Permite que um status visível seja resolvido novamente depois da troca de idioma. |
| `src/LegendLauncher.App/Localization/LocalizeExtension.cs:7` | `LocalizeExtension` | Markup extension WPF que cria um `Binding` de uma via para o indexador da instância global. A notificação `Item[]` atualiza inclusive janelas já abertas. |
| `src/LegendLauncher.App/Localization/Resources/pt-BR.json:1` | Catálogo português | Catálogo padrão e fallback com 204 chaves. |
| `src/LegendLauncher.App/Localization/Resources/en-US.json:1` | Catálogo inglês | Traduções em inglês americano com as mesmas 204 chaves. |
| `src/LegendLauncher.App/Localization/Resources/es-ES.json:1` | Catálogo espanhol | Traduções em espanhol com as mesmas 204 chaves. |
| `src/LegendLauncher.App/Localization/Resources/*.json:2` | Marca e slogan | `App_WindowTitle` mantém “Urus Launcher”; `Brand_Subtitle` oferece “Jogue do seu jeito”, “Play your way” e “Juega a tu manera”. As antigas chaves de prévia foram removidas. |
| `src/LegendLauncher.App/App.xaml.cs:9` | `App.OnStartup(...)` | Lê `settings.json` antes de criar a janela, aplica o idioma salvo ou `pt-BR` em falha e então habilita as culturas de thread. |
| `src/LegendLauncher.App/MainWindow.xaml:110` | Seletor de idioma | Combobox compacto no cabeçalho, ligado a `Languages` e `SelectedLanguage`. Usa os handlers explícitos já validados no seletor de versões para abrir por clique, Enter, Espaço, F4 ou Alt+Seta para baixo. |
| `src/LegendLauncher.App/Views/Game/GameWorkspaceView.xaml:200` | Seletor no workspace | Mantém a troca de idioma disponível na barra compacta; os handlers equivalentes ficam em `GameWorkspaceView.xaml.cs:13`. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml:1` | Pedido de apoio | Usa 26 chaves `Donation_*` para conteúdo PayPal/PIX, feedback de cópia, tooltips e nomes de automação; a troca atualiza o modal mesmo enquanto ele está aberto. |
| `src/LegendLauncher.App/Localization/Resources/*.json:182` | Atualização | As 24 chaves `Update_*` cobrem consulta, versão atual, download, `ReadyToInstall`, instalação, falha, nova verificação, tooltip, popup e acessibilidade nos três idiomas. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:85` | Status e ações localizados | Resolve título/detalhe para cada estado, **INSTALAR**, nova verificação/retry e patch notes pelo código ativo; propriedades são notificadas novamente quando o idioma muda. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Localization.cs:6` | Estado de localização da janela | Expõe idiomas, seleção e textos localizados; reapresenta status, servidor selecionado, runtime e linhas existentes quando `LanguageChanged` é emitido. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Localization.cs:111` | `PersistLanguageAsync(...)` | Grava a preferência sem interromper a execução se o arquivo não puder ser atualizado. |
| `src/LegendLauncher.App/ViewModels/GameWorkspaceViewModel.cs:24` | Localização do workspace | Usa a mesma instância para mudo/rodapé e notifica os bindings na troca; remove o handler durante `Dispose`. |
| `src/LegendLauncher.App/ViewModels/ServerRowViewModel.cs:44` | Textos da linha de servidor | Resolve os papéis independentes do último servidor e do lançamento mais recente, os selos/tooltips, o divisor dos demais servidores, disponibilidade, data de abertura e nomes fallback; `RefreshLocalization()` atualiza somente as propriedades traduzíveis. |
| `src/LegendLauncher.GameHost.Legacy/GameHostLocalization.cs:23` | `GameHostLocalization` | Catálogo interno do processo legado para preparação, diagnóstico e erros; normaliza a cultura recebida e não depende do WPF. |
| `src/LegendLauncher.GameHost.Legacy/LegacyGameRuntime.cs:129` | Propagação ao GameHost | Inclui a cultura normalizada em `LEGEND_LAUNCHER_LANGUAGE` no ambiente exclusivo do processo filho, sem colocá-la no protocolo ou na linha de comando. |

## Fluxo de seleção e atualização

1. `App.OnStartup` lê o snapshot atual e configura `LocalizationService.Current` antes da criação da `MainWindow`.
2. `LocalizeExtension` liga textos estáticos de XAML ao indexador observável. View models usam `Get`, `Format` ou `LocalizedMessage` para textos calculados.
3. O seletor altera `SelectedLanguage`; o serviço troca imediatamente a cultura e o view model solicita `SaveLanguageAsync`.
4. `PropertyChanged("Item[]")` atualiza bindings XAML. `LanguageChanged` atualiza propriedades calculadas, o workspace e as linhas de servidor já materializadas, inclusive **RECOMENDADO/RECOMMENDED**, **MAIS RECENTE/NEWEST/MÁS RECIENTE** e **OUTROS SERVIDORES/OTHER SERVERS/OTROS SERVIDORES**.
5. Um novo GameHost recebe a cultura vigente pelo ambiente e a aplica antes de montar mensagens e formulários WinForms. GameHosts já em execução continuam jogando; o chrome WPF ao redor deles muda imediatamente.
6. O pedido de apoio usa o mesmo `LocalizeExtension`: título, instruções do QR, PIX, ação de copiar, feedback de sucesso/falha e acessibilidade mudam ao vivo. PayPal, PIX, URL, hash e CNPJ são dados invariantes.
7. O cartão do updater reapresenta consulta, download automático, `ReadyToInstall`, instalação, `Current` e `Failed`, além de ações, tooltip e acessibilidade. **INSTALAR/INSTALL/INSTALAR** continua sendo o único consentimento de execução; `Current` oferece **VERIFICAR NOVAMENTE/CHECK AGAIN/VERIFICAR DE NUEVO** e `Failed` oferece **TENTAR DE NOVO/TRY AGAIN/REINTENTAR**. `GetNotes(languageCode)` troca imediatamente os patch notes entre `pt-BR`, `en-US` e `es-ES` sem nova consulta ou download.

## Persistência e comportamento de falha

`%LocalAppData%\LegendLauncherNext\data\settings.json` inclui `languageCode` ao lado de mudo, layout e último perfil. A atualização é uma leitura-modificação-gravação atômica, portanto trocar o idioma não apaga as demais preferências. Um documento antigo sem o campo, um valor fora das três famílias ou uma falha de leitura usa `pt-BR`. Falha de gravação mantém a escolha durante a execução e não interfere em login ou sessão.

Os catálogos são `EmbeddedResource`; nenhum arquivo de tradução externo é carregado em runtime. Falta ou conteúdo estruturalmente inválido falha cedo durante a inicialização do tipo, evitando uma interface parcialmente vazia. Chave individual ausente cai para português e, se também não existir no catálogo padrão, aparece como `[Chave]` para tornar a regressão observável.

## Limites e segurança

- O idioma é preferência não sensível. O valor enviado ao GameHost é limitado a três códigos normalizados e não contém conta, senha, cookie, token ou URI.
- A variável de ambiente existe somente no filho iniciado e não altera o ambiente global do Windows.
- Mensagens de autenticação são selecionadas por código seguro no launcher; diagnósticos arbitrários do provider não definem a linguagem da interface.
- O módulo traduz somente superfícies controladas pelo projeto. O jogo Flash, seus textos e os nomes retornados pelo catálogo continuam pertencendo à plataforma escolhida.
- “Urus Launcher” é marca invariável; somente o slogan e textos funcionais são traduzidos. “Legend Online” permanece o nome do jogo e não é tratado como marca do launcher.

## Dependências, consumidores e referências cruzadas

O módulo usa `System.Globalization`, `System.Text.Json`, recursos incorporados e bindings WPF. É composto pela [Launcher App](launcher-app.md), persiste sua opção por meio de [Infrastructure](infrastructure.md), atualiza o [Game Session Workspace](game-session-workspace.md), o [Pedido de Doação](donation-prompt.md) e a [Atualização](atualizacao.md), e propaga a cultura ao [GameHost Legacy](game-host-legacy.md). Nome/slogan seguem [Branding Urus](branding.md). As decisões estão em [ADR-005](../decisoes/ADR-005-localizacao-dinamica.md), [ADR-007](../decisoes/ADR-007-identidade-e-distribuicao-urus.md) e [ADR-008](../decisoes/ADR-008-atualizacoes-github-releases.md).

## Testes

- `LocalizationCatalogTests.cs:9` verifica conjuntos de chaves, valores não vazios, assinaturas de placeholders, referências usadas pela App e ausência de chaves órfãs nos três catálogos.
- `LocalizationServiceTests.cs:16` cobre normalização/fallback; os testes seguintes cobrem opções estáveis, notificações, formatação cultural e reapresentação de `LocalizedMessage` em runtime.
- `LauncherSettingsServiceTests.cs:10` cobre defaults; os casos a partir da linha 62 validam migração de documento antigo e persistência normalizada sem apagar outras preferências.
- `MainWindowViewModelLocalizationTests.cs:13` cobre troca em runtime, restauração antes do catálogo, mapeamento local seguro de erros de autenticação e persistência independente.
- `GameWorkspaceLocalizationTests.cs:12` cobre rótulo de mudo, rodapé, singular/plural, notificações e remoção do handler no descarte.
- `ServerRowLocalizationTests.cs:16` cobre recomendado, lançamento mais recente, divisor dos demais servidores, disponibilidade, fallbacks, endereço ausente, abertura futura e refresh das propriedades traduzíveis.
- `GameHostLocalizationTests.cs` cobre normalização das famílias, presença das mensagens nos três idiomas e propagação por variável de ambiente sem iniciar processo.
- `MainWindowLayoutXamlTests.cs` e `GameWorkspaceXamlTests.cs` fixam os handlers de abertura por mouse/teclado nos dois seletores de idioma.
- `DonationPromptAssetTests.cs` fixa as referências localizadas de PayPal/PIX, incluindo feedback de cópia e nomes de automação; a suíte valida 204 chaves equivalentes em cada idioma.
- `LauncherUpdateViewModelTests.cs:184` cobre a troca ao vivo dos patch notes; `LauncherUpdateLayoutTests.cs:22` fixa textos, ações e nomes de automação do cartão/popup. `LocalizationCatalogTests.cs:97` fixa **INSTALAR**, nova verificação e os estados formatados nos três idiomas. O conjunto focado da revisão concluiu **82/82**; a suíte completa passou **461/461** em Debug e **461/461** em Release.
- `LocalizationCatalogTests.cs:82` exige “Urus Launcher”, os três slogans e ausência de chaves/marcadores públicos de Next, preview, prévia, technical ou testes.
