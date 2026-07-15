# Módulo Branding Urus

## Objetivo do módulo

O branding define **Urus Launcher** como a identidade pública do produto no cabeçalho, título da janela, metadados do Windows, ícone, GameHost e distribuição. A interface não exibe mais “Next”, “prévia”, “preview”, “technical”, “testes” ou equivalentes como parte da marca. Os estados normais e mensagens ao jogador também evitam jargão de implementação como PID, GameHost x64, runtime e processo isolado. **Legend Online** permanece inalterado quando identifica o jogo, suas plataformas ou servidores.

A marca visual é um monograma abstrato em forma de “U”/portal, construído em ciano, turquesa e azul com um pequeno acento dourado. Não representa um touro, não usa escudo e não reproduz trade dress de terceiros. O objetivo é fornecer uma assinatura própria, legível desde 16 px até o cabeçalho principal.

## Arquivos, classes e superfícies principais

| Referência aproximada | Elemento | Responsabilidade |
| --- | --- | --- |
| `src/LegendLauncher.App/Assets/Branding/urus-logo.png` | Logo principal | PNG RGBA transparente 1024×1024 usado nas superfícies WPF; SHA-256 `F996ACD7388043908817DDE0A5363B4AD078047EBC9210E3C682DAC46BC2E493`. |
| `src/LegendLauncher.App/Assets/Branding/urus-launcher.ico` | Ícone Windows | ICO 32 bpp com entradas 16, 24, 32, 48, 64, 128 e 256 px; usado pelo executável, atalhos e instalador; SHA-256 `9404FFE30F9A899DBEF02CEBC8BA485A84D00956BF712D15A0F5337A7E8AB0ED`. |
| `src/LegendLauncher.App/LegendLauncher.App.csproj:13` | Recursos de branding | Empacota `Assets\Branding\*.png`; `ApplicationIcon` aponta para o ICO por volta da linha 34. |
| `src/LegendLauncher.App/LegendLauncher.App.csproj:22` | Identidade do assembly | Publica `AssemblyName=UrusLauncher.App` e metadados Title/Product/Company/Description como “Urus Launcher”, preservando `RootNamespace=LegendLauncher.App`. |
| `src/LegendLauncher.App/app.manifest:3` | Identidade do manifesto | Usa `UrusLauncher.App` como `assemblyIdentity`, alinhado ao nome físico do executável. |
| `src/LegendLauncher.App/MainWindow.xaml:11` | Título público | Resolve `App_WindowTitle` nos catálogos, sempre “Urus Launcher”. |
| `src/LegendLauncher.App/MainWindow.xaml:58` | Cabeçalho principal | Exibe `urus-logo.png`, “U R U S  L A U N C H E R” e o slogan localizado `Brand_Subtitle`. |
| `src/LegendLauncher.App/Views/Game/DetachedGameWindow.xaml:49` | Janela desacoplada | Reutiliza o mesmo logo por Pack URI associado ao assembly `UrusLauncher.App`. |
| `src/LegendLauncher.App/Localization/Resources/*.json:2` | Nome e slogan | Mantém o nome invariável e oferece slogan em português, inglês e espanhol. |
| `src/LegendLauncher.GameHost.Legacy/LegacyGameHostForm.cs:18` | Chrome do GameHost | Usa “Urus GameHost” em títulos e erros próprios sem renomear o jogo Legend Online. |
| `src/LegendLauncher.GameHost.Legacy/Program.cs:19` | Falhas de inicialização | Apresenta “Urus GameHost” nas caixas de erro antes da criação do formulário. |

## Origem e tratamento dos assets

O conceito visual original foi gerado com o `imagegen` incorporado, a partir de um briefing específico da Urus. O arquivo gerado não foi copiado de marca, jogo ou launcher existente. O fundo cromakey foi removido localmente por processamento de imagem para produzir transparência real; o símbolo não foi reconstruído, redesenhado ou contaminado com elementos de terceiros nessa etapa.

O resultado canônico é `urus-logo.png`, PNG RGBA de 1024×1024. O `urus-launcher.ico` foi derivado localmente dessa fonte transparente em sete resoluções para evitar depender do redimensionamento do shell. Os antigos marks do protótipo foram removidos depois que todas as referências passaram a apontar para a identidade Urus.

## Nome, slogan e idioma

O nome público é sempre **Urus Launcher**. O slogan usa a chave `Brand_Subtitle`:

| Cultura | Slogan |
| --- | --- |
| `pt-BR` | `J O G U E  D O  S E U  J E I T O` |
| `en-US` | `P L A Y  Y O U R  W A Y` |
| `es-ES` | `J U E G A  A  T U  M A N E R A` |

As chaves antigas `App_WindowTitlePreview` e `Brand_TechnicalPreview` não fazem parte dos catálogos. Testes também rejeitam no texto público marcadores de “Next”, prévia, preview, technical ou testes. Essa regra não altera termos técnicos legítimos dentro de código, testes ou documentação histórica.

## Compatibilidade interna

- O arquivo público principal é `UrusLauncher.App.exe` e o assembly correspondente é `UrusLauncher.App`.
- Namespaces, nomes dos projetos-fonte e contratos internos `LegendLauncher.*` permanecem estáveis para evitar uma migração ampla sem ganho funcional.
- Os Pack URIs usam o novo nome de assembly; o `LogicalName` dos JSONs incorporados também acompanha `UrusLauncher.App` para que a localização continue sendo encontrada.
- `%LocalAppData%\LegendLauncherNext` continua sendo o diretório de dados nesta versão, preservando perfis, settings, cache e credenciais existentes. O nome interno não é mostrado como marca ao usuário.
- “Legend Online”, nomes de plataformas, servidores e conteúdo Flash continuam pertencendo ao jogo e não são substituídos por “Urus”.

## Dependências, consumidores e referências cruzadas

- A [Launcher App](launcher-app.md) consome logo, ícone, nome e slogan.
- [Localização](localizacao.md) fornece os três slogans e impede o retorno de marcadores de prévia.
- O [GameHost Legacy](game-host-legacy.md) usa a marca Urus somente no chrome próprio.
- A [Distribuição Windows](distribuicao-windows.md) usa `UrusLauncher.App.exe` e o ICO nos pacotes e atalhos.
- A decisão de preservar nomes internos e mudar somente a identidade pública está em [ADR-007](../decisoes/ADR-007-identidade-e-distribuicao-urus.md).

## Testes e QA

- `tests/LegendLauncher.Tests/App/BrandingAssetTests.cs:10` verifica PNG quadrado RGBA, ICO configurado e remoção dos marks antigos.
- `tests/LegendLauncher.Tests/App/MainWindowLayoutXamlTests.cs:88` fixa nome público, metadados, manifesto, namespace interno e Pack URIs do novo assembly.
- `tests/LegendLauncher.Tests/App/LocalizationCatalogTests.cs:82` fixa nome/slogan nos três idiomas e rejeita marcadores públicos de prévia/teste.
- `tests/LegendLauncher.Tests/GameHost/GameHostLocalizationTests.cs:66` preserva “Legend Online” como jogo e exige “Urus GameHost” nas superfícies próprias.
- A validação visual e o resultado final da suíte estão registrados em [`design-qa.md`](../../design-qa.md).
