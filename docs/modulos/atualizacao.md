# Módulo Atualização pelo GitHub Releases

## Objetivo do módulo

O módulo verifica cedo, sem bloquear a abertura do launcher, se existe uma versão pública mais recente do Urus Launcher em `Jessielriffel2/UrusLauncher`. Quando encontra uma versão superior, baixa e valida automaticamente o instalador no diretório privado da instalação por usuário. O resultado aparece em um cartão compacto no canto inferior esquerdo e o jogo continua disponível durante consulta e download. Depois da validação, o estado `ReadyToInstall` oferece **INSTALAR**; somente esse clique explícito autoriza executar o setup.

A consulta ocorre uma vez em cada abertura, logo depois de carregar as preferências e antes de perfis e catálogo. Não existe polling durante o jogo, execução silenciosa nem token do GitHub incorporado ao aplicativo. A partir da 1.1.1, uma resposta `403` ou `429` da API aciona a rota pública do manifesto do último release, melhorando a descoberta em redes/IPs compartilhados sem relaxar validações. Falha de rede, ausência de release ou documento inválido produz um estado recuperável com tentativa manual e não impede catálogo, login ou jogo. Os estados `Current` e `Failed` também expõem **VERIFICAR NOVAMENTE** ou **TENTAR DE NOVO**, respectivamente.

## Arquivos, funções e classes principais

| Referência aproximada | Elemento | Responsabilidade |
| --- | --- | --- |
| `src/LegendLauncher.App/Updates/ILauncherUpdateService.cs:3` | `ILauncherUpdateService` | Contrato para consultar, baixar e iniciar uma atualização. |
| `src/LegendLauncher.App/Updates/ILauncherUpdateService.cs:19` | `LauncherUpdateRelease` | Versão, tag, notas localizadas e metadados validados do instalador. |
| `src/LegendLauncher.App/Updates/UpdateDocuments.cs:5` | Documentos JSON | DTOs mínimos para a API do GitHub e `update-manifest.json`. |
| `src/LegendLauncher.App/Updates/LauncherUpdateValidation.cs:7` | `LauncherUpdateValidation` | Fixa repositório, nomes, limites, formato semântico, SHA-256, URLs oficiais do fallback e allowlist HTTPS. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:42` | `CheckForUpdateAsync(...)` | Consulta `releases/latest`; em rate limit `403`/`429`, tenta o manifesto público do último release e retorna somente uma versão superior à instalada depois das mesmas validações. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:115` | `ReadFallbackReleaseAsync(...)` | Busca o manifesto público fixo somente após rate limit da API e constrói internamente as URLs oficiais dos assets. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:129` | `DownloadInstallerAsync(...)` | Reutiliza somente o setup final de nome, bytes e SHA-256 exatos; caso contrário, baixa em até uma hora para `.part`, valida durante o stream e move atomicamente para o nome final. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:255` | `TryReuseCachedInstallerAsync(...)` | Abre e verifica integralmente o setup em cache; apaga uma cópia inválida e obriga novo download, sem aceitar apenas nome ou existência. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:290` | `LaunchInstallerAsync(...)` | Reabre e valida o arquivo confinado ao diretório de updates imediatamente antes de iniciar o Inno Setup com fechamento/reabertura coordenados. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:326` | `BuildRelease(...)` | Exige coerência entre tag, repositório, versão do manifesto, asset, nome, tamanho, digest disponível e notas nos três idiomas. |
| `src/LegendLauncher.App/Updates/UpdateManifestValidator.cs:6` | `UpdateManifestValidator` | Centraliza o contrato do manifesto usado tanto pela resposta normal quanto pelo fallback, incluindo três idiomas, versão, setup, bytes e SHA-256. |
| `src/LegendLauncher.App/Updates/UpdateDownloadCleanup.cs:6` | `UpdateDownloadCleanup` | Na criação do serviço, remove somente setup/`.part` oficiais com mais de 24 horas, apenas no nível superior; ignora reparse points e falhas/arquivos em uso. |
| `src/LegendLauncher.App/Updates/UpdateProcessStarter.cs:5` | `IUpdateProcessStarter` | Fronteira testável para iniciar o setup somente depois da validação. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:135` | Inicialização do updater | Cria comandos, observa sessões ativas e prepara os estados localizados de UI. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:158` | `BeginUpdateCheck()` | Dispara a consulta única e antecipada em cada abertura. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:167` | `CheckForUpdatesAsync()` | Consulta, baixa, valida e guarda o setup sem executá-lo; alterna entre `Checking`, `Current`, `Downloading`, `ReadyToInstall` e `Failed`. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:223` | `InstallUpdateAsync()` | Aceita somente setup já preparado em `ReadyToInstall`, exige nenhuma sessão nem login em andamento e executa apenas depois do clique explícito em **INSTALAR**. |
| `src/LegendLauncher.App/Views/Updates/UpdateStatusView.xaml:19` | Cartão de status | Mostra busca, versão, erro, progresso e ações localizadas no canto inferior esquerdo. |
| `src/LegendLauncher.App/Views/Updates/UpdateStatusView.xaml:100` | Popup de novidades | Exibe as notas da versão atualizadas com o idioma ativo e mantém a instalação opcional. |
| `src/LegendLauncher.App/LauncherComposition.cs:50` | Composição | Entrega o `HttpClient` compartilhado e `%LocalAppData%\LegendLauncherNext\updates` ao serviço. |
| `src/LegendLauncher.Infrastructure/Paths/AppPaths.cs:42` | `UpdatesDirectory` | Diretório gravável e exclusivo do usuário para downloads temporários e instaladores validados. |

## Fluxo em tempo de execução

1. Assim que settings e idioma carregam em cada abertura, antes de perfis e catálogo, `BeginUpdateCheck()` inicia uma consulta assíncrona única.
2. O serviço pede o último release público à API do GitHub, sem autenticação e sem enviar conta do jogo.
3. Se a API responder especificamente `403` ou `429`, o serviço tenta `https://github.com/Jessielriffel2/UrusLauncher/releases/latest/download/update-manifest.json`. Outros erros continuam como falha recuperável e não acionam esse fallback.
4. A rota alternativa descobre somente o mesmo manifesto público. Ela pode levar à preparação automática do setup, mas nunca autoriza sua execução. Repositório, versão, tag derivada, origem, nome, tamanho e SHA-256 continuam sujeitos ao contrato estrito.
5. A tag `vX.Y.Z`, o asset `update-manifest.json` e o manifesto são validados contra o repositório fixo.
6. Se a versão não for superior, o cartão entra em `Current`, informa a versão instalada e permite **VERIFICAR NOVAMENTE**. Se consulta, download ou validação falhar, entra em `Failed`, oferece **TENTAR DE NOVO** e mantém o jogo disponível.
7. Se a versão for superior, o download começa automaticamente. Consulta e download não bloqueiam `CanStartGame`; contas abertas continuam ativas e novas sessões ainda podem ser iniciadas.
8. Antes da rede, um setup final existente é reutilizado apenas se nome/caminho, bytes e SHA-256 coincidirem exatamente com o release. Cache inválido é removido e o download recomeça.
9. Um novo setup é escrito primeiro como `.part`; limites, origem, tamanho e SHA-256 são validados antes da movimentação para o nome final. O cartão então entra em `ReadyToInstall` e mostra notas e **INSTALAR** nos três idiomas.
10. Sessões de jogo e logins ainda em abertura não impedem a preparação, mas desabilitam a instalação. Depois que a abertura termina e todas as contas são fechadas, **INSTALAR** é reabilitado sem baixar de novo o mesmo setup validado.
11. Somente o clique em **INSTALAR** é consentimento para execução. O arquivo é reaberto e revalidado imediatamente antes de o Inno Setup receber `/SILENT`, `/CLOSEAPPLICATIONS` e `/RELAUNCH`; depois que ele inicia, a janela atual encerra e a versão instalada volta a abrir.

## Manifesto e patch notes

Cada versão possui uma definição fonte em `docs/releases/vX.Y.Z.json`, com `schemaVersion`, versão, título e notas em `pt-BR`, `en-US` e `es-ES`. O pipeline converte essa definição em:

- `update-manifest.json`, consumido pelo launcher e contendo o instalador, bytes, SHA-256 e notas trilíngues;
- `RELEASE_NOTES.md`, usado como corpo do GitHub Release;
- registros correspondentes em `distribution-manifest.json` e `SHA256SUMS.txt`.

Não se deve editar manualmente os artefatos gerados. Uma nova versão exige novo JSON fonte, tag idêntica `vX.Y.Z` e build integral dos pacotes.

## Segurança e limitações

- São aceitos somente HTTPS, porta padrão, repositório fixo e hosts GitHub previstos; redirects são limitados e revalidados.
- JSON e instalador possuem limites de tamanho; assets duplicados, nomes inesperados e versões divergentes são rejeitados.
- O fallback é limitado a respostas `403`/`429` da consulta à API e a uma URL pública fixa do mesmo repositório. Ele não é usado para contornar documento inválido e conserva as mesmas verificações de versão, origem, nome, bytes e SHA-256.
- Download parcial é apagado em falha. O executável final deve continuar confinado ao diretório `updates`.
- Ao iniciar o serviço, uma limpeza best effort considera somente nomes oficiais de setup e `.part` com mais de 24 horas no diretório exato. Ela não percorre subpastas, não segue reparse points e ignora com segurança arquivos bloqueados/inacessíveis.
- O SHA-256 detecta corrupção e divergência entre manifesto, metadados do GitHub e arquivo baixado. Ele **não autentica sozinho o publicador** se o repositório e o release forem comprometidos.
- Os pacotes atuais não possuem assinatura Authenticode. Até existir assinatura de código, o Windows pode exibir SmartScreen e o sistema não deve prometer identidade criptográfica do publicador.
- Não existe instalação forçada. Consulta e download validado são automáticos em cada abertura, enquanto `Current` e `Failed` permitem nova tentativa manual; executar o setup depende sempre de **INSTALAR**.
- A instalação é bloqueada enquanto houver GameHost ativo ou login/abertura em andamento para evitar interromper contas e autenticações.
- `App.OnStartup` mantém uma única instância por sessão do Windows e restaura a janela existente; assim, outra App não disputa o mesmo cache nem esconde sessões ativas do bloqueio de instalação.

## Bootstrap e passagem para 1.1.3

A versão 1.0.1 não contém este updater e não consegue buscar releases novos. A 1.1.0 permanece no histórico como o primeiro bootstrap com updater, mas sua consulta pode ser limitada pela cota da API em redes que compartilham um IP. As versões 1.1.1 e 1.1.2 possuem o fallback público, porém ainda usam a política antiga em que **Atualizar** baixa e inicia a instalação. A transição para a preparação automática ocorre uma vez ao instalar a 1.1.3:

1. publicar o repositório público e enviar a tag `v1.1.3`;
2. deixar o workflow criar o GitHub Release com setup, ZIP, manifesto e checksums;
3. distribuir o instalador 1.1.3 manualmente aos usuários da 1.0.1 e aos usuários da 1.1.0 cuja rede esteja bloqueada pelo rate limit;
4. usuários da 1.1.1/1.1.2 fecham as sessões e usam o antigo **Atualizar** uma última vez para instalar a 1.1.3;
5. a partir da 1.1.3, releases posteriores são consultados e baixados automaticamente, permanecem em `ReadyToInstall` e só executam quando a pessoa clicar **INSTALAR**; perfis, settings e credenciais continuam fora da pasta instalada.

Antes de existir um primeiro release válido, a consulta pode apresentar falha recuperável; isso não bloqueia as demais funções.

## Dependências e referências cruzadas

- A composição e o ciclo da janela pertencem à [Launcher App](launcher-app.md).
- Textos de status, acessibilidade e ações usam [Localização](localizacao.md).
- O diretório gravável é fornecido por [Infrastructure](infrastructure.md).
- Manifestos, notas e artefatos vêm de [Distribuição Windows](distribuicao-windows.md).
- A política arquitetural está em [ADR-008](../decisoes/ADR-008-atualizacoes-github-releases.md).
- O processo público de vulnerabilidades está em [`SECURITY.md`](../../SECURITY.md).

## Testes

- `LauncherUpdateServiceCheckTests.cs:7` cobre versão, manifesto, notas, URLs, redirects, limites, duplicatas e falhas de contrato.
- `LauncherUpdateServiceFallbackTests.cs:8` cobre `403`/`429`, redirect permitido, recusa de fallback para `500`/JSON inválido/erro posterior e rejeição de manifesto alternativo inválido.
- `LauncherUpdateServiceDownloadTests.cs:8` cobre streaming, progresso, tamanho, SHA-256, `.part`, confinamento, reutilização exata de cache, substituição de cache inválido e argumentos do setup.
- `UpdateDownloadCleanupTests.cs:6` cobre idade mínima, reconhecimento exato, escopo top-level e tolerância a arquivo em uso/inacessível.
- `LauncherUpdateViewModelTests.cs:10` cobre consulta e download automáticos na abertura, `ReadyToInstall`, execução somente após **INSTALAR**, jogo disponível durante download, nova verificação em `Current`/`Failed`, idioma dinâmico e bloqueio da instalação com sessão ativa ou login ainda em abertura.
- `LauncherUpdateLayoutTests.cs:3` fixa cartão inferior esquerdo, popup, ações e bindings.
- `GitHubReleaseContractTests.cs:5` fixa workflow por tag, ausência de PAT incorporado e definições trilíngues versionadas, incluindo a 1.1.3.
- `AppPathsTests.cs:18` fixa o diretório de updates sob a raiz privada do aplicativo.

Na alteração desta política, o conjunto focado de updater, localização e contratos de distribuição concluiu **82/82**. A suíte completa concluiu **461/461** em Debug e **461/461** em Release.
