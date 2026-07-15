# MAPA — Urus Launcher

Fonte da verdade para a estrutura e os módulos do projeto.

- **Última atualização:** 2026-07-15
- **Raiz:** `LegendLauncherNext/`
- **Escopo da árvore:** fontes, testes e documentação; `bin/`, `obj/` e `artifacts/` são saídas geradas e ficam fora.

## Estrutura completa

```text
LegendLauncherNext/
├── .github/
│   └── workflows/
│       └── release.yml
├── .gitignore
├── CHANGELOG.md
├── Directory.Build.props
├── LegendLauncherNext.slnx
├── LICENSE
├── README.md
├── SECURITY.md
├── design-qa.md
├── docs/
│   ├── MAPA.md
│   ├── decisoes/
│   │   ├── ADR-001-stack-e-isolamento.md
│   │   ├── ADR-002-runtime-flash.md
│   │   ├── ADR-003-transporte-oas-cloudflare.md
│   │   ├── ADR-004-gamehost-incorporado-multissessao.md
│   │   ├── ADR-005-localizacao-dinamica.md
│   │   ├── ADR-006-lembrete-doacao-nao-intrusivo.md
│   │   ├── ADR-007-identidade-e-distribuicao-urus.md
│   │   └── ADR-008-atualizacoes-github-releases.md
│   ├── releases/
│   │   └── v1.1.0.json
│   └── modulos/
│       ├── atualizacao.md
│       ├── branding.md
│       ├── core.md
│       ├── distribuicao-windows.md
│       ├── donation-prompt.md
│       ├── game-session-workspace.md
│       ├── game-host-legacy.md
│       ├── infrastructure.md
│       ├── launcher-app.md
│       ├── localizacao.md
│       ├── network-bridge.md
│       ├── providers-oas.md
│       └── providers-sevenwan.md
├── installer/
│   └── UrusLauncher.iss
├── scripts/
│   └── build-urus-distribution.ps1
├── src/
│   ├── LegendLauncher.App/
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── AssemblyInfo.cs
│   │   ├── app.manifest
│   │   ├── BorderlessWindowCommands.cs
│   │   ├── BorderlessWindowWorkArea.cs
│   │   ├── LauncherComposition.cs
│   │   ├── LegendLauncher.App.csproj
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── Assets/
│   │   │   ├── Branding/
│   │   │   │   ├── urus-launcher.ico
│   │   │   │   └── urus-logo.png
│   │   │   ├── castle-background.png
│   │   │   ├── paypal-donation-qr.jpeg
│   │   │   └── selected-server-castle-banner.png
│   │   ├── GameHosting/
│   │   │   ├── EmbeddedGameSurfaceHost.cs
│   │   │   ├── GameSurfacePresenter.cs
│   │   │   ├── GameWindowAttachment.cs
│   │   │   └── NativeWindowMethods.cs
│   │   ├── Localization/
│   │   │   ├── LocalizationService.cs
│   │   │   ├── LocalizedMessage.cs
│   │   │   ├── LocalizeExtension.cs
│   │   │   └── Resources/
│   │   │       ├── en-US.json
│   │   │       ├── es-ES.json
│   │   │       └── pt-BR.json
│   │   ├── Properties/
│   │   │   └── AssemblyInfo.cs
│   │   ├── Services/
│   │   │   ├── CoreAudioInterop.cs
│   │   │   ├── GameAudioService.cs
│   │   │   ├── GameLayoutMode.cs
│   │   │   ├── LauncherSettingsService.cs
│   │   │   ├── PlatformAdapterRegistry.cs
│   │   │   ├── ProfileStorageCoordinator.cs
│   │   │   ├── ServerCatalogPresentation.cs
│   │   │   └── SessionLaunchCoordinator.cs
│   │   ├── Themes/
│   │   │   ├── Colors.xaml
│   │   │   ├── Controls.xaml
│   │   │   └── WindowStyles.xaml
│   │   ├── Updates/
│   │   │   ├── ILauncherUpdateService.cs
│   │   │   ├── LauncherUpdateService.cs
│   │   │   ├── LauncherUpdateValidation.cs
│   │   │   ├── UpdateDownloadCleanup.cs
│   │   │   ├── UpdateDocuments.cs
│   │   │   └── UpdateProcessStarter.cs
│   │   ├── ViewModels/
│   │   │   ├── AsyncRelayCommand.cs
│   │   │   ├── GameSessionViewModel.cs
│   │   │   ├── GameWorkspaceViewModel.cs
│   │   │   ├── MainWindowViewModel.Catalog.cs
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── MainWindowViewModel.Donation.cs
│   │   │   ├── MainWindowViewModel.Localization.cs
│   │   │   ├── MainWindowViewModel.Profiles.cs
│   │   │   ├── MainWindowViewModel.Updates.cs
│   │   │   ├── ObservableObject.cs
│   │   │   ├── PlatformItemViewModel.cs
│   │   │   ├── ProfileItemViewModel.cs
│   │   │   ├── RelayCommand.cs
│   │   │   └── ServerRowViewModel.cs
│   │   └── Views/
│   │       ├── Donation/
│   │       │   ├── DonationPromptView.xaml
│   │       │   └── DonationPromptView.xaml.cs
│   │       ├── Game/
│   │       │   ├── DetachedGameWindow.xaml
│   │       │   ├── DetachedGameWindow.xaml.cs
│   │       │   ├── GameWorkspaceView.xaml
│   │       │   └── GameWorkspaceView.xaml.cs
│   │       └── Updates/
│   │           ├── UpdateStatusView.xaml
│   │           └── UpdateStatusView.xaml.cs
│   ├── LegendLauncher.Core/
│   │   ├── LegendLauncher.Core.csproj
│   │   ├── Contracts/
│   │   │   ├── ICredentialVault.cs
│   │   │   ├── IGameAuthenticationService.cs
│   │   │   ├── IGameRuntime.cs
│   │   │   ├── IProfileStore.cs
│   │   │   ├── IServerCatalogCache.cs
│   │   │   └── IServerDirectory.cs
│   │   └── Models/
│   │       ├── AccountProfile.cs
│   │       ├── AuthenticationModels.cs
│   │       ├── GameServer.cs
│   │       ├── PlatformDefinition.cs
│   │       ├── RuntimeModels.cs
│   │       └── ServerCatalog.cs
│   ├── LegendLauncher.GameHost.Legacy/
│   │   ├── app.manifest
│   │   ├── FlashActiveXControl.cs
│   │   ├── FlashRuntimeConfiguration.cs
│   │   ├── FlashSessionParameters.cs
│   │   ├── GameHostLocalization.cs
│   │   ├── GameHostOptions.cs
│   │   ├── GameHostWindowIdentity.cs
│   │   ├── LaunchSessionIpcCodec.cs
│   │   ├── LaunchSessionPipeClient.cs
│   │   ├── LaunchSessionPipeIdentity.cs
│   │   ├── LaunchSessionPipeServer.cs
│   │   ├── LegacyGameHostForm.cs
│   │   ├── LegacyGameRuntime.cs
│   │   ├── LegacyLaunchUriPolicy.cs
│   │   ├── LegacyRuntimeAssets.cs
│   │   ├── LegendLauncher.GameHost.Legacy.csproj
│   │   ├── NamedPipePeerProcess.cs
│   │   ├── OneTimeNonceValidator.cs
│   │   ├── ParentProcessExitSource.cs
│   │   ├── ParentProcessLifetimeMonitor.cs
│   │   ├── PipeCompletionProtocol.cs
│   │   ├── PipeMessageFraming.cs
│   │   ├── Program.cs
│   │   ├── RegistrationFreeActivationContext.cs
│   │   └── Properties/
│   │       └── AssemblyInfo.cs
│   ├── LegendLauncher.Infrastructure/
│   │   ├── LegendLauncher.Infrastructure.csproj
│   │   ├── Paths/
│   │   │   └── AppPaths.cs
│   │   ├── Persistence/
│   │   │   ├── AtomicJsonFileStore.cs
│   │   │   ├── JsonProfileRepository.cs
│   │   │   ├── JsonProfileStore.cs
│   │   │   └── JsonServerCatalogCache.cs
│   │   ├── Runtime/
│   │   │   ├── LegacyRuntimeProbe.cs
│   │   │   └── LegacyRuntimeProbeResult.cs
│   │   └── Security/
│   │       ├── CredentialKey.cs
│   │       ├── WindowsCredentialNative.cs
│   │       └── WindowsCredentialVault.cs
│   ├── LegendLauncher.NetworkBridge/
│   │   ├── BridgeSecurityPolicy.cs
│   │   ├── BridgeValidationResult.cs
│   │   └── LegendLauncher.NetworkBridge.csproj
│   ├── LegendLauncher.Providers.Oas/
│   │   ├── BoundedHttpContentReader.cs
│   │   ├── LegendLauncher.Providers.Oas.csproj
│   │   ├── OasAuthenticationErrorCodes.cs
│   │   ├── OasAuthenticationService.cs
│   │   ├── OasCurlLaunchTransport.cs
│   │   ├── OasCurlResponseParser.cs
│   │   ├── OasLaunchPageParser.cs
│   │   ├── OasOriginPolicy.cs
│   │   ├── OasPassportResponseParser.cs
│   │   ├── OasPlatformCatalog.cs
│   │   ├── OasServerDirectory.cs
│   │   ├── OasServerDirectoryException.cs
│   │   ├── OasServerPayloadParser.cs
│   │   └── Properties/
│   │       └── AssemblyInfo.cs
│   └── LegendLauncher.Providers.SevenWan/
│       ├── LegendLauncher.Providers.SevenWan.csproj
│       ├── SevenWanPlatformCatalog.cs
│       ├── SevenWanServerDirectory.cs
│       ├── SevenWanServerDirectoryException.cs
│       └── SevenWanServerPayloadParser.cs
└── tests/
    └── LegendLauncher.Tests/
        ├── LegendLauncher.Tests.csproj
        ├── App/
        │   ├── AppTestDoubles.cs
        │   ├── BorderlessWindowCommandsTests.cs
        │   ├── BorderlessWindowWorkAreaTests.cs
        │   ├── BrandingAssetTests.cs
        │   ├── DetachedWindowLifecycleTests.cs
        │   ├── DonationPromptAssetTests.cs
        │   ├── DonationPromptTests.cs
        │   ├── GameAudioServiceTests.cs
        │   ├── GitHubReleaseContractTests.cs
        │   ├── GameWindowAttachmentTests.cs
        │   ├── GameWorkspaceLocalizationTests.cs
        │   ├── GameWorkspaceViewModelTests.cs
        │   ├── GameWorkspaceXamlTests.cs
        │   ├── LauncherSettingsServiceTests.cs
        │   ├── LauncherUpdateLayoutTests.cs
        │   ├── LauncherUpdateViewModelTests.cs
        │   ├── LocalizationCatalogTests.cs
        │   ├── LocalizationServiceTests.cs
        │   ├── MainWindowLayoutXamlTests.cs
        │   ├── MainWindowViewModelLocalizationTests.cs
        │   ├── MainWindowViewModelTests.cs
        │   ├── PlatformAdapterRegistryTests.cs
        │   ├── ProfileStorageCoordinatorTests.cs
        │   ├── ServerCatalogPresentationTests.cs
        │   ├── ServerRowLocalizationTests.cs
        │   ├── ServerRowViewModelTests.cs
        │   ├── SessionLaunchCoordinatorTests.cs
        │   ├── WindowsDistributionContractTests.cs
        │   └── Updates/
        │       ├── UpdateDownloadCleanupTests.cs
        │       ├── LauncherUpdateServiceCheckTests.cs
        │       ├── LauncherUpdateServiceDownloadTests.cs
        │       └── UpdateTestSupport.cs
        ├── Core/
        │   ├── AuthenticationModelsTests.cs
        │   └── RuntimeModelsTests.cs
        ├── GameHost/
        │   ├── GameHostLocalizationTests.cs
        │   ├── GameHostOptionsTests.cs
        │   ├── GameHostWindowTests.cs
        │   ├── LaunchSessionIpcTests.cs
        │   ├── LegacyLaunchUriPolicyTests.cs
        │   └── ParentProcessLifetimeMonitorTests.cs
        ├── Infrastructure/
        │   ├── AppPathsTests.cs
        │   ├── AtomicJsonFileStoreTests.cs
        │   ├── CredentialKeyTests.cs
        │   ├── JsonPersistenceTests.cs
        │   ├── LegacyRuntimeProbeTests.cs
        │   └── TemporaryDirectory.cs
        ├── NetworkBridge/
        │   └── BridgeSecurityPolicyTests.cs
        ├── Oas/
        │   ├── OasAuthenticationServiceTests.cs
        │   ├── OasCurlLaunchTransportTests.cs
        │   ├── OasCurlResponseParserTests.cs
        │   ├── OasLiveSmokeTests.cs
        │   ├── OasPlatformCatalogTests.cs
        │   └── OasServerDirectoryTests.cs
        └── SevenWan/
            └── SevenWanServerDirectoryTests.cs
```

## Módulos de produção

| Módulo | Responsabilidade | Arquivos principais | Documento |
| --- | --- | --- | --- |
| `LegendLauncher.App` | Código-fonte WPF x64 do Urus Launcher, publicado como `UrusLauncher.App.exe`; oferece launcher de três colunas, catálogo ordenado por perfil, login, chrome taskbar/DPI-aware e integração não bloqueante com atualizações públicas. | `MainWindow.xaml`, `Themes/WindowStyles.xaml`, `Services/ServerCatalogPresentation.cs`, `MainWindowViewModel*.cs`, `SessionLaunchCoordinator.cs`, `LauncherComposition.cs` | [launcher-app.md](modulos/launcher-app.md) |
| `LegendLauncher.App/Branding` | Identidade pública Urus Launcher: logo original transparente, ícone multirresolução, slogan localizado, metadados e remoção de marcadores Next/preview/teste da UI. | `Assets/Branding/*`, `LegendLauncher.App.csproj`, `app.manifest`, `MainWindow.xaml`, `Localization/Resources/*.json` | [branding.md](modulos/branding.md) |
| `LegendLauncher.App/Game Session Workspace` | Barra única de 44 px com controles/abas de 34 px, abas roláveis, `+ CONTA` persistente, reserva de 150 px para o chrome, layouts 1/2/4, áudio global, detach/reattach e incorporação reversível de HWND/PID. | `Views/Game/*`, `GameHosting/*.cs`, `GameWorkspaceViewModel.cs`, `GameSessionViewModel.cs`, `GameAudioService.cs`, `LauncherSettingsService.cs`, `BorderlessWindowCommands.cs` | [game-session-workspace.md](modulos/game-session-workspace.md) |
| `LegendLauncher.App/Localization` | Localização dinâmica por 203 chaves em `pt-BR`, `en-US` e `es-ES`, inclusive título/slogan, catálogo, doação e todo o fluxo de atualização, com recursos incorporados, bindings observáveis e persistência da cultura ativa. | `Localization/*.cs`, `Localization/Resources/*.json`, `MainWindowViewModel.Localization.cs`, `MainWindowViewModel.Updates.cs` | [localizacao.md](modulos/localizacao.md) |
| `LegendLauncher.App/Donation Prompt` | Pedido opcional PayPal/PIX, lembrete de cinco horas avaliado uma vez por abertura, acesso manual, QR imutável, cópia local do CNPJ e acessibilidade localizada. | `Views/Donation/*`, `MainWindowViewModel.Donation.cs`, `LauncherSettingsService.cs`, `paypal-donation-qr.jpeg` | [donation-prompt.md](modulos/donation-prompt.md) |
| `LegendLauncher.App/Atualização` | Consulta opcional do GitHub Releases na abertura, cartão inferior esquerdo, patch notes trilíngues, download sob consentimento, validação estrita e reinício coordenado sem sessões ativas. | `Updates/*.cs`, `Views/Updates/*`, `MainWindowViewModel.Updates.cs`, `AppPaths.UpdatesDirectory` | [atualizacao.md](modulos/atualizacao.md) |
| `Distribuição Windows` | Publica App e GameHost self-contained para `win-x64`, gera setup/ZIP, manifesto de atualização, patch notes, checksums e GitHub Release automatizado por tag. | `scripts/build-urus-distribution.ps1`, `installer/UrusLauncher.iss`, `.github/workflows/release.yml`, `docs/releases/*.json` | [distribuicao-windows.md](modulos/distribuicao-windows.md) |
| `LegendLauncher.Core` | Modelos imutáveis e contratos sem dependência de UI, rede, JSON ou chamadas Win32; o HWND da sessão é apenas um identificador opaco. | `Models/*.cs`, `Contracts/*.cs` | [core.md](modulos/core.md) |
| `LegendLauncher.Infrastructure` | Paths — incluindo diretório privado de updates —, JSON atômico para perfis/cache/settings, Windows Credential Manager e probe do Flash instalado. | `AppPaths.cs`, `AtomicJsonFileStore.cs`, `JsonProfileStore.cs`, `WindowsCredentialVault.cs`, `LegacyRuntimeProbe.cs` | [infrastructure.md](modulos/infrastructure.md) |
| `LegendLauncher.Providers.Oas` | Oito plataformas OAS, catálogo/cache, Passport atual, transporte compatível, parsers e allowlist. | `OasAuthenticationService.cs`, `OasCurlLaunchTransport.cs`, `OasServerDirectory.cs`, `OasOriginPolicy.cs` | [providers-oas.md](modulos/providers-oas.md) |
| `LegendLauncher.Providers.SevenWan` | Quatorze variantes Wartune/7wan e catálogo público normalizado; autenticação permanece indisponível. | `SevenWanPlatformCatalog.cs`, `SevenWanServerDirectory.cs`, `SevenWanServerPayloadParser.cs` | [providers-sevenwan.md](modulos/providers-sevenwan.md) |
| `LegendLauncher.GameHost.Legacy` | Processo x64 separado que herda a cultura normalizada, recebe sessão por Named Pipe, ativa COM sem registro, hospeda Flash ActiveX, devolve uma superfície nativa validada e encerra quando o processo pai desaparece. | `LegacyGameRuntime.cs`, `GameHostLocalization.cs`, `GameHostWindowIdentity.cs`, `ParentProcessLifetimeMonitor.cs`, `PipeCompletionProtocol.cs`, `FlashActiveXControl.cs` | [game-host-legacy.md](modulos/game-host-legacy.md) |
| `LegendLauncher.NetworkBridge` | Política compartilhada de bind/upstream; valida destinos e não abre proxy neste marco. | `BridgeSecurityPolicy.cs`, `BridgeValidationResult.cs` | [network-bridge.md](modulos/network-bridge.md) |

## Arquivos e decisões principais

| Caminho | Descrição |
| --- | --- |
| `README.md` | Estado funcional, build/execução, fluxo de login e limites de segurança. |
| `CHANGELOG.md` | Histórico público resumido por versão semântica. |
| `LICENSE` | Código e assets publicamente visíveis com direitos reservados; não concede redistribuição. |
| `SECURITY.md` | Canal responsável para vulnerabilidades e orientação contra exposição de credenciais. |
| `LegendLauncherNext.slnx` | Solução com sete projetos de produção e um projeto de testes. |
| `Directory.Build.props` | Linguagem atual, build determinístico e warnings como erros. |
| `.gitignore` | Exclui saídas e estado local. |
| `design-qa.md` | Registro de QA visual e de acabamento; barra/workspace, janela desacoplada, execução real S115, work area, modal PayPal, identidade Urus, distribuição versionada e contrato da lista de servidores por perfil. |
| `docs/MAPA.md` | Este documento central; muda junto com estrutura ou lista de módulos. |
| `docs/decisoes/ADR-001-stack-e-isolamento.md` | Escolha de .NET/WPF e processo separado. |
| `docs/decisoes/ADR-002-runtime-flash.md` | Estratégia de compatibilidade Flash e futuro Ruffle. |
| `docs/decisoes/ADR-003-transporte-oas-cloudflare.md` | Ponte pós-Passport via curl do Windows, limites e alternativas. |
| `docs/decisoes/ADR-004-gamehost-incorporado-multissessao.md` | Decisão por um GameHost por sessão, proxy HWND, layouts, barra compacta, chrome responsivo, detach, áudio e settings. |
| `docs/decisoes/ADR-005-localizacao-dinamica.md` | Decisão por catálogos incorporados, bindings observáveis, persistência e propagação não sensível da cultura ao GameHost. |
| `docs/decisoes/ADR-006-lembrete-doacao-nao-intrusivo.md` | Decisão pela avaliação única na abertura, cadência de cinco horas, acesso manual, QR preservado e PIX copiável. |
| `docs/decisoes/ADR-007-identidade-e-distribuicao-urus.md` | Decisão pela marca pública Urus, asset original, compatibilidade interna e dois formatos de distribuição Windows. |
| `docs/decisoes/ADR-008-atualizacoes-github-releases.md` | Decisão por consulta única na abertura, consentimento explícito, manifesto validado, patch notes e GitHub Releases. |
| `docs/modulos/atualizacao.md` | Contratos, UI, segurança, bootstrap e testes do atualizador. |
| `docs/modulos/branding.md` | Origem, integração, contratos visuais/localizados e validação dos assets Urus. |
| `docs/modulos/distribuicao-windows.md` | Pipeline Release, instalação, portabilidade, hashes, segurança e operação dos pacotes. |
| `docs/releases/v1.1.0.json` | Fonte trilíngue dos títulos e patch notes da versão 1.1.0. |
| `.github/workflows/release.yml` | Gera e publica GitHub Release quando uma tag `vMAJOR.MINOR.PATCH` é enviada. |
| `docs/modulos/game-session-workspace.md` | Contrato funcional e técnico do workspace multissessão. |
| `docs/modulos/localizacao.md` | Contrato dos três idiomas, atualização em runtime, limites e integração com settings/GameHost. |
| `docs/modulos/donation-prompt.md` | Contrato temporal, visual, de integridade do QR, PIX, acessibilidade e persistência do pedido de apoio. |
| `installer/UrusLauncher.iss` | Receita Inno Setup x64, per-user e trilíngue do instalador Urus Launcher. |
| `scripts/build-urus-distribution.ps1` | Pipeline self-contained que testa, publica, valida, empacota e calcula checksums com `System.Security.Cryptography.SHA256`. |
| `artifacts/urus-distribution/` | Saída gerada e fora da árvore-fonte: payload, instalador, ZIP, manifesto e checksums da versão. |
| `tests/LegendLauncher.Tests/` | Suíte xUnit organizada pelas mesmas fronteiras dos projetos; inclui contratos XAML, branding, localização, doação, updater, GitHub Release e distribuição Windows. |

## Relação entre módulos

1. `LegendLauncher.App` compõe `Core`, `Infrastructure`, `Providers.Oas`, `Providers.SevenWan` e `GameHost.Legacy` por `PlatformAdapterRegistry`.
2. O `Game Session Workspace` recebe `GameSession` e o perfil efetivo da App, rastreia uma sessão por perfil ativo e compõe o HWND validado sem carregar ActiveX no WPF; eventos de processo retornam à `Dispatcher` antes de alterar a UI.
3. `Providers.Oas` implementa catálogo/autenticação de `Core`; usa cache por contrato e o curl do Windows somente na entrada pós-Passport.
4. `Providers.SevenWan` implementa catálogo de `Core`; a App associa suas variantes a autenticação indisponível explícita.
5. `Localization` atualiza a App, o workspace, o pedido de apoio e o updater pela instância observável compartilhada; sua cultura é persistida no mesmo settings e propagada ao GameHost somente por ambiente normalizado.
6. `Donation Prompt` usa UI/localização da App, um timestamp não sensível no settings e a área de transferência local para PIX; não acessa providers, credenciais ou sessões.
7. `Branding` fornece assets e textos públicos à App, ao GameHost e ao instalador; não altera os nomes internos mantidos por compatibilidade.
8. `Atualização` consulta o GitHub Release público, usa o diretório fornecido por Infrastructure e consome o manifesto/notas produzidos por Distribuição; nenhuma instalação ocorre sem clique ou com sessão ativa.
9. `Distribuição Windows` consome builds Release, branding, definição trilíngue e testes para gerar os pacotes e, por tag, publicar o GitHub Release usado pelo updater.
10. `Infrastructure` implementa persistência/cofre definidos por `Core`; também fornece diretórios privados para cache, dados e downloads de atualização.
11. `GameHost.Legacy` implementa `IGameRuntime`, usa a política do `NetworkBridge`, localiza mensagens próprias, devolve PID/HWND, não recebe senha e monitora o PID pai para não sobreviver ao launcher.
12. `NetworkBridge` mantém apenas validação neste marco; `H2Proxy.exe` não é iniciado.

## Estado funcional resumido

O MVP possui interface WPF moderna, marca pública **Urus Launcher** e 203 textos próprios equivalentes em português brasileiro, inglês e espanhol. O arquivo público é `UrusLauncher.App.exe`; nomes internos e `%LocalAppData%\LegendLauncherNext` permanecem por compatibilidade. Há múltiplos perfis/contas, senha no Cofre do Windows, catálogo ordenado por histórico, autenticação OAS e um GameHost separado por sessão. O workspace oferece abas, layouts 1/2/4, detach/reattach e mudo global por PID. A janela acompanha work area/DPI, o pedido opcional de apoio não interrompe o jogo e o cartão de atualização consulta uma vez na abertura o release público esperado. Uma versão superior mostra patch notes no idioma ativo; download e instalação dependem de clique e ficam bloqueados com sessão ativa. O setup baixado é limitado e validado por repositório, URL, versão, nome, bytes e SHA-256, mas os artefatos ainda não possuem Authenticode. A distribuição 1.0.1 continua sendo o pacote histórico validado; `docs/releases/v1.1.0.json` prepara o bootstrap do primeiro release com updater, que ainda precisa ser instalado manualmente por usuários da 1.0.1. Login social, favoritos manuais, assinatura Authenticode e autenticação 7wan continuam fora do marco atual.
