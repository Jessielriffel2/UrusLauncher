# Módulo Atualização pelo GitHub Releases

## Objetivo do módulo

O módulo verifica, sem bloquear a abertura do launcher, se existe uma versão pública mais recente do Urus Launcher em `Jessielriffel2/UrusLauncher`. O resultado aparece em um cartão compacto no canto inferior esquerdo. A pessoa pode ler as novidades e continuar usando a versão atual; nenhum instalador é baixado ou executado antes de um clique explícito em **Atualizar**.

A consulta ocorre uma vez em cada abertura. Não existe polling durante o jogo, atualização silenciosa nem token do GitHub incorporado ao aplicativo. A partir da 1.1.1, uma resposta `403` ou `429` da API aciona a rota pública do manifesto do último release, melhorando a descoberta em redes/IPs compartilhados sem relaxar validações. Falha de rede, ausência de release ou documento inválido produz um estado recuperável com tentativa manual e não impede catálogo, login ou jogo.

## Arquivos, funções e classes principais

| Referência aproximada | Elemento | Responsabilidade |
| --- | --- | --- |
| `src/LegendLauncher.App/Updates/ILauncherUpdateService.cs:3` | `ILauncherUpdateService` | Contrato para consultar, baixar e iniciar uma atualização. |
| `src/LegendLauncher.App/Updates/ILauncherUpdateService.cs:19` | `LauncherUpdateRelease` | Versão, tag, notas localizadas e metadados validados do instalador. |
| `src/LegendLauncher.App/Updates/UpdateDocuments.cs:5` | Documentos JSON | DTOs mínimos para a API do GitHub e `update-manifest.json`. |
| `src/LegendLauncher.App/Updates/LauncherUpdateValidation.cs:7` | `LauncherUpdateValidation` | Fixa repositório, nomes, limites, formato semântico, SHA-256, URLs oficiais do fallback e allowlist HTTPS. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:42` | `CheckForUpdateAsync(...)` | Consulta `releases/latest`; em rate limit `403`/`429`, tenta o manifesto público do último release e retorna somente uma versão superior à instalada depois das mesmas validações. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:115` | `ReadFallbackReleaseAsync(...)` | Busca o manifesto público fixo somente após rate limit da API e constrói internamente as URLs oficiais dos assets. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:129` | `DownloadInstallerAsync(...)` | Baixa sob limite para arquivo `.part`, calcula SHA-256 durante o stream e move para o nome final somente após validar bytes e hash. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:240` | `LaunchInstallerAsync(...)` | Reabre e valida o arquivo confinado ao diretório de updates antes de iniciar o Inno Setup com fechamento/reabertura coordenados. |
| `src/LegendLauncher.App/Updates/LauncherUpdateService.cs:276` | `BuildRelease(...)` | Exige coerência entre tag, repositório, versão do manifesto, asset, nome, tamanho, digest disponível e notas nos três idiomas. |
| `src/LegendLauncher.App/Updates/UpdateManifestValidator.cs:6` | `UpdateManifestValidator` | Centraliza o contrato do manifesto usado tanto pela resposta normal quanto pelo fallback, incluindo três idiomas, versão, setup, bytes e SHA-256. |
| `src/LegendLauncher.App/Updates/UpdateDownloadCleanup.cs:6` | `UpdateDownloadCleanup` | Na criação do serviço, remove somente setup/`.part` oficiais com mais de 24 horas, apenas no nível superior; ignora reparse points e falhas/arquivos em uso. |
| `src/LegendLauncher.App/Updates/UpdateProcessStarter.cs:5` | `IUpdateProcessStarter` | Fronteira testável para iniciar o setup somente depois da validação. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:127` | Inicialização do updater | Cria comandos, observa sessões ativas e prepara os estados de UI. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:150` | `BeginUpdateCheck()` | Dispara a consulta única iniciada pela carga normal do launcher. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:159` | `CheckForUpdatesAsync()` | Alterna entre procurando, atualizado, disponível e falha recuperável. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Updates.cs:199` | `InstallUpdateAsync()` | Exige nenhuma sessão antes de baixar e revalida depois do download; durante download/instalação, `CanStartGame` também impede abrir uma sessão nova. |
| `src/LegendLauncher.App/Views/Updates/UpdateStatusView.xaml:19` | Cartão de status | Mostra busca, versão, erro, progresso e ações localizadas no canto inferior esquerdo. |
| `src/LegendLauncher.App/Views/Updates/UpdateStatusView.xaml:100` | Popup de novidades | Exibe as notas da versão atualizadas com o idioma ativo e mantém a instalação opcional. |
| `src/LegendLauncher.App/LauncherComposition.cs:50` | Composição | Entrega o `HttpClient` compartilhado e `%LocalAppData%\LegendLauncherNext\updates` ao serviço. |
| `src/LegendLauncher.Infrastructure/Paths/AppPaths.cs:42` | `UpdatesDirectory` | Diretório gravável e exclusivo do usuário para downloads temporários e instaladores validados. |

## Fluxo em tempo de execução

1. Depois que perfis, settings e catálogo carregam, `BeginUpdateCheck()` inicia uma consulta assíncrona única.
2. O serviço pede o último release público à API do GitHub, sem autenticação e sem enviar conta do jogo.
3. Se a API responder especificamente `403` ou `429`, o serviço tenta `https://github.com/Jessielriffel2/UrusLauncher/releases/latest/download/update-manifest.json`. Outros erros continuam como falha recuperável e não acionam esse fallback.
4. A rota alternativa descobre somente o mesmo manifesto público; ela não baixa o setup nem autoriza instalação. Repositório, versão, tag derivada, origem, nome, tamanho e SHA-256 continuam sujeitos ao contrato estrito.
5. A tag `vX.Y.Z`, o asset `update-manifest.json` e o manifesto são validados contra o repositório fixo.
6. Se a versão não for superior, o cartão informa que o launcher está atualizado. Se for superior, mostra versão, notas no idioma ativo e os botões de ação.
7. Somente **Atualizar** inicia o download. Sessões de jogo ativas desabilitam a instalação; enquanto a operação está ativa, novas sessões também ficam bloqueadas.
8. O setup é escrito primeiro como `.part`; nome, caminho, tamanho e SHA-256 são conferidos antes e imediatamente antes da execução.
9. Depois do download, as sessões são revalidadas. Se alguma surgiu, o setup não é executado e o cartão volta ao estado disponível.
10. O Inno Setup recebe `/SILENT`, `/CLOSEAPPLICATIONS` e `/RELAUNCH`; depois que ele inicia, a janela atual encerra e a versão instalada volta a abrir.

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
- Não existe atualização forçada. A consulta automática pode ser repetida manualmente em erro, mas baixar e executar dependem sempre da pessoa.
- A instalação é bloqueada enquanto houver GameHost ativo para evitar encerrar contas sem aviso.

## Bootstrap e passagem para 1.1.2

A versão 1.0.1 não contém este updater e não consegue buscar releases novos. A 1.1.0 permanece no histórico como o primeiro bootstrap com updater, mas sua consulta pode ser limitada pela cota da API em redes que compartilham um IP. A 1.1.1 já possui o fallback público e consegue oferecer a 1.1.2:

1. publicar o repositório público e enviar a tag `v1.1.2`;
2. deixar o workflow criar o GitHub Release com setup, ZIP, manifesto e checksums;
3. distribuir o instalador 1.1.2 manualmente aos usuários da 1.0.1 e aos usuários da 1.1.0 cuja rede esteja bloqueada pelo rate limit;
4. usuários da 1.1.1 fecham as sessões de jogo e confirmam **Atualizar**; perfis, settings e credenciais permanecem fora da pasta instalada;
5. versões posteriores continuam sendo publicadas por novas tags, com o mesmo fallback em respostas `403`/`429`.

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
- `LauncherUpdateServiceDownloadTests.cs:8` cobre streaming, progresso, tamanho, SHA-256, `.part`, confinamento e argumentos do setup.
- `UpdateDownloadCleanupTests.cs:6` cobre idade mínima, reconhecimento exato, escopo top-level e tolerância a arquivo em uso/inacessível.
- `LauncherUpdateViewModelTests.cs:10` cobre consulta automática, escolha explícita, idioma dinâmico, erro e bloqueio com sessão ativa.
- `LauncherUpdateLayoutTests.cs:3` fixa cartão inferior esquerdo, popup, ações e bindings.
- `GitHubReleaseContractTests.cs:5` fixa workflow por tag, ausência de PAT incorporado e definições trilíngues versionadas, incluindo a 1.1.2.
- `AppPathsTests.cs:18` fixa o diretório de updates sob a raiz privada do aplicativo.

Na validação da 1.1.2, a suíte completa concluiu **445/445** nos modos Debug e Release. A build canônica repetiu os **445/445** testes Release antes de gerar setup, ZIP, manifesto e checksums.
