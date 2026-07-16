# Módulo Distribuição Windows

## Objetivo do módulo

O pipeline de distribuição transforma o código Release do Urus Launcher em dois entregáveis Windows x64 reproduzíveis: instalador por usuário criado com Inno Setup e ZIP portátil. Ambos usam o mesmo payload **self-contained**, contendo o runtime .NET necessário, `UrusLauncher.App.exe`, o GameHost isolado e, quando uma origem autorizada é fornecida, o runtime registration-free em `runtime\`. O processo também produz manifesto de distribuição, manifesto de atualização, patch notes trilíngues e checksums. O GitHub Releases é a fonte pública usada pelo updater; a 1.1.4 foi publicada manualmente a partir da build local autorizada porque o runner hospedado não dispõe da origem do runtime legado.

Desde a preparação da 1.1.4, o build não aceita mais produzir um pacote anunciado como jogável sem manifesto/OCX. A origem vem de `-LegacyRuntimeSource`, `LEGEND_LEGACY_ROOT` ou da instalação Brov conhecida; não é versionada nem baixada automaticamente. A publicação pública da 1.1.4 usou uma origem local cuja redistribuição foi confirmada como autorizada pelo mantenedor, conforme a condição descrita no [ADR-009](../decisoes/ADR-009-provisionamento-runtime-legado.md).

## Arquivos, funções e saídas principais

| Referência aproximada | Elemento | Responsabilidade |
| --- | --- | --- |
| `scripts/build-urus-distribution.ps1:1` | Parâmetros | Recebe versão semântica, RID `win-x64`, `ISCC.exe`, origem opcional do runtime e switches conscientes de teste/smoke. |
| `scripts/build-urus-distribution.ps1:21` | `Invoke-CheckedCommand(...)` | Executa dotnet/ISCC e transforma qualquer exit code não zero em falha do pipeline. |
| `scripts/build-urus-distribution.ps1:36` | `Get-VerifiedChildPath(...)` | Confina diretórios removidos/criados ao root esperado antes de qualquer limpeza recursiva. |
| `scripts/build-urus-distribution.ps1:73` | `Assert-SelfContainedApplication(...)` | Verifica runtimeconfig/deps, `includedFrameworks` e runtime pack `win-x64` para App e GameHost. |
| `scripts/build-urus-distribution.ps1:102` | `Assert-WpfWindowsBase(...)` | Exige a implementação WPF completa de `WindowsBase.dll`, impedindo que uma facade do GameHost substitua o runtime da App. |
| `scripts/build-urus-distribution.ps1:126` | `Resolve-LegacyRuntimeSource(...)` | Resolve somente fonte explícita/local e falha quando não há runtime fornecido pelo mantenedor. |
| `scripts/build-urus-distribution.ps1:164` | `Get-ManifestActiveXPath(...)` | Lê XML com DTD proibido e localiza o OCX referenciado pelo manifesto. |
| `scripts/build-urus-distribution.ps1:196` | `Copy-LegacyRuntimePayload(...)` | Confina caminhos, valida tamanho/Authenticode e copia manifesto + OCX para `payload/runtime`. |
| `scripts/build-urus-distribution.ps1:250` | `Test-PortableLauncherStartup(...)` | Inicia o payload sem runtime global, observa o processo e falha se ele não alcançar a janela responsiva. |
| `scripts/build-urus-distribution.ps1:307` | `Get-ArtifactRecord(...)` | Calcula SHA-256 por stream e devolve nome, bytes e hash do artefato. |
| `scripts/build-urus-distribution.ps1:331` | Preparação | Resolve solution, projetos, ISS, ICO, definição de release, dotnet, runtime fonte e diretórios de distribuição. |
| `scripts/build-urus-distribution.ps1:367` | Definição da versão | Exige `docs/releases/vX.Y.Z.json` e valida conteúdo trilíngue antes de limpar saídas. |
| `scripts/build-urus-distribution.ps1:402` | Teste e publish | Executa a suíte Release por padrão e publica App/GameHost separadamente como self-contained, sem trim e sem símbolos de debug. |
| `scripts/build-urus-distribution.ps1:452` | Composição e validação | Mantém WPF da App, copia quatro arquivos próprios do GameHost, ICO e runtime registration-free, e rejeita executável obsoleto. |
| `scripts/build-urus-distribution.ps1:490` | Instalador e ZIP | Compila o ISS, cria o ZIP do mesmo payload na linha 508 e valida ambos. |
| `scripts/build-urus-distribution.ps1:523` | Manifesto do updater | Gera `update-manifest.json` com repositório, versão, setup, bytes, SHA-256 e notas nos três idiomas. |
| `scripts/build-urus-distribution.ps1:538` | Patch notes | Gera `RELEASE_NOTES.md` a partir da mesma definição fonte. |
| `scripts/build-urus-distribution.ps1:554` | Manifesto/checksums | Registra distribuição, updater, runtime e payload; grava `SHA256SUMS.txt`. |
| `installer/UrusLauncher.iss:1` | Definições do produto | Fixa nome, executáveis, versão, diretórios de entrada/saída e ICO. |
| `installer/UrusLauncher.iss:21` | `[Setup]` | Instalador x64 por usuário, sem elevação, para Windows 10+, com LZMA2, wizard moderno e identidade Urus. |
| `installer/UrusLauncher.iss:58` | Idiomas/tarefas | Oferece inglês, português brasileiro e espanhol e atalho de desktop opcional. |
| `installer/UrusLauncher.iss:66` | Arquivos/atalhos/run | Instala o payload completo, cria atalhos e oferece iniciar o launcher ao final. A linha 76 reabre após update e `[Code]` reconhece `/RELAUNCH`. |
| `.github/workflows/release.yml:15` | Job `build` | Usa somente `contents: read`, actions fixadas por commit, checkout sem credenciais persistidas, .NET 10 e Inno 6.7.1; testa/constrói e transfere o artefato com `upload-artifact@v7.0.1` e `download-artifact@v8.0.1`, ambos nativos em Node 24. |
| `.github/workflows/release.yml:72` | Job `publish` | Só inicia após `build`, baixa os artefatos validados e recebe `contents: write` apenas para criar o GitHub Release. |
| `docs/releases/v1.1.0.json:1` | Patch notes fonte histórica | Título e mudanças do primeiro bootstrap com updater nos três idiomas. |
| `docs/releases/v1.1.1.json:1` | Patch notes fonte histórica | Título e mudanças do fallback de rate limit em `pt-BR`, `en-US` e `es-ES`. |
| `docs/releases/v1.1.2.json:1` | Patch notes fonte histórica | Correção de acesso entre variantes OAS, Classic Português S100 e sessões por alvo exato nos três idiomas. |
| `docs/releases/v1.1.3.json:1` | Patch notes fonte pública histórica | Download automático validado, cache exato, consulta manual e instalação consentida nos três idiomas. |
| `docs/releases/v1.1.4.json:1` | Patch notes fonte pública atual | Runtime interno prioritário, instalação limpa e estado visual honesto nos três idiomas. |
| `artifacts/urus-distribution/portable/UrusLauncher/` | Payload expandido | Diretório executável usado como origem comum do Inno Setup e do ZIP. |
| `artifacts/urus-distribution/distribution-manifest.json` | Manifesto | Produto, versão, RID, flag self-contained, data UTC, nomes/tamanhos/hashes e inventário agregado do payload. |
| `artifacts/urus-distribution/update-manifest.json` | Manifesto de atualização | Contrato estrito consumido pela App com metadados do setup e patch notes localizados. |
| `artifacts/urus-distribution/RELEASE_NOTES.md` | Notas do release | Corpo trilíngue usado pelo GitHub Release. |
| `artifacts/urus-distribution/SHA256SUMS.txt` | Checksums | SHA-256 em formato simples para validar instalador, ZIP, manifesto de atualização e notas. |

## Entregáveis públicos 1.1.3

| Tipo | Caminho |
| --- | --- |
| Instalador Inno Setup | `GitHub Release/UrusLauncher-Setup-1.1.3-win-x64.exe` — 54.586.546 bytes — SHA-256 `C213061318B891FB866B990CA8378F1708FA2B1DC80F3DA51E667FECA952294F` |
| ZIP portátil | `GitHub Release/UrusLauncher-1.1.3-portable-win-x64.zip` — 78.153.034 bytes — SHA-256 `EC090E70AEDFA7645FDC9816CB27124E902F95D007F9155866B125F0BA024C4B` |
| Manifesto do updater | `GitHub Release/update-manifest.json` — 3.529 bytes — SHA-256 `6B9DA4B6A3E652073CC8C88DC3699DC6DF96328911CDC977ADC026AC7E3AB775` |
| Lista oficial de checksums | `GitHub Release/SHA256SUMS.txt` — 383 bytes — SHA-256 `6D51FC4A2C592165DFAC410AC1FABDDA7D26E18588C7D1570F7D24384C93DD5B` |
| Aplicativo principal | `portable/UrusLauncher/UrusLauncher.App.exe` |
| GameHost isolado | `portable/UrusLauncher/LegendLauncher.GameHost.Legacy.exe` |

Os artefatos públicos foram gerados pelo workflow da tag `v1.1.3`, no commit `18ccb5e982c9ff833d61819ec4d8602d38d19fa8`. App e GameHost foram publicados com ProductVersion `1.1.3+18ccb5e982c9ff833d61819ec4d8602d38d19fa8`/FileVersion `1.1.3.0`; o workflow aprovou **461/461** testes. A build canônica local também executou o smoke portátil por sete segundos sem runtime .NET global. O ZIP público foi baixado, conferido e expandido: contém 468 arquivos e 181.719.481 bytes de payload. `Get-AuthenticodeSignature` confirmou `NotSigned` no setup público baixado, coerente com a limitação atual. Os tamanhos e hashes da tabela são exatamente os publicados no GitHub Release, nos digests dos assets e em `SHA256SUMS.txt`; qualquer nova build precisa publicar seus próprios valores. A entrega 1.0.1 permanece como histórico anterior em [`design-qa.md`](../../design-qa.md).

## Entregáveis públicos 1.1.4

Release: [Urus Launcher 1.1.4](https://github.com/Jessielriffel2/UrusLauncher/releases/tag/v1.1.4), tag/target `31d6d16b063b43bdba161a028bc4edd4f3953b96`.

| Tipo | Caminho/resultado |
| --- | --- |
| Instalador com runtime | `GitHub Release/UrusLauncher-Setup-1.1.4-win-x64.exe` — 62.187.431 bytes — SHA-256 `96A5C3C67AB46C22B8C7AF713C3C6056D50EEA35087FFC4EBA2CCBC3B8BC02C1` |
| ZIP portátil | `GitHub Release/UrusLauncher-1.1.4-portable-win-x64.zip` — 88.711.074 bytes — SHA-256 `44DB6D46D572E3CBC8C1B0675D6691E098AA4EFDCBE1671D7DCCD3529F066644` |
| Manifesto do updater | `GitHub Release/update-manifest.json` — 1.551 bytes — SHA-256 `1E0264A7FD6EC90E30DDBE12B301C17F316AA118B37281CDEE5268E8DC09396F` |
| Lista oficial de checksums | `GitHub Release/SHA256SUMS.txt` — 383 bytes — SHA-256 `DE29AE0B71C98C7E021A530671F84B46274873B58C52F92D22D0B3DA365E1C03` |
| Manifesto registration-free | `runtime/Adobe.Flash.Control.manifest` — 5.868 bytes — SHA-256 `CCF4B2837D2AF91908BCB1C5F68FAF1B707E060972EC42EEB19BE18AB4F9D6ED` |
| ActiveX referenciado | `runtime/flash/Flash64_15_0_0_167.ocx` — 23.445.680 bytes — SHA-256 `7AC444D19AD9D7C8A26A1FE09A7052FB7C6C922CE6C4EE38798A963DF42E38EC` |

A build local autorizada 1.1.4 concluiu **465/465** testes Release, publish self-contained, validação Authenticode do OCX e smoke de sete segundos sem .NET global. App e GameHost possuem ProductVersion `1.1.4+31d6d16b063b43bdba161a028bc4edd4f3953b96` e FileVersion `1.1.4.0`. O payload possui 470 arquivos/205.798.704 bytes, e o ZIP contém o manifesto e o OCX nos caminhos esperados. A versão portátil foi aberta pela automação do Windows, o status mostrou **Pronto para jogar** em verde e `ENTRAR E JOGAR` apareceu habilitado com perfil/servidor/credencial já existentes.

O setup público permanece `NotSigned`, portanto o Windows SmartScreen ainda pode exibir aviso. Tamanhos e digests dos quatro assets coincidiram entre a API pública e os arquivos locais validados; os hashes do setup, ZIP e manifesto também coincidiram com suas entradas em `SHA256SUMS.txt`. O manifesto servido por `releases/latest/download/update-manifest.json` coincidiu com o asset da release. A publicação foi criada manualmente a partir dessa build porque o runner hospedado não tem a origem licenciada do runtime. A criação da tag pela API acionou inesperadamente o workflow `29471074585`, cancelado deliberadamente antes dos jobs de build/publicação; a release manual permaneceu pública e íntegra.

## Fluxo de construção

Pré-requisitos do mantenedor:

- Windows x64;
- .NET SDK 10;
- Inno Setup 6, normalmente em `%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe`;
- espaço para o publish self-contained expandido e os dois pacotes.

Na raiz do repositório:

```powershell
.\scripts\build-urus-distribution.ps1 -Version 1.1.4 `
    -LegacyRuntimeSource "C:\caminho\para\runtime-autorizado"
```

O fluxo normal executa testes, publica os dois processos, compõe e valida o payload, copia o runtime fornecido para `runtime\`, faz o smoke do executável portátil, compila o instalador, cria o ZIP e emite manifesto/checksums. A composição não sobrepõe bibliotecas compartilhadas da App com as facades do staging do GameHost. `-SkipTests` existe para iterações locais conscientes; não deve ser usado na distribuição final.

Antes do comando, deve existir `docs/releases/v1.1.4.json` (ou o arquivo da versão passada) com conteúdo não vazio nas três culturas. A origem do runtime precisa conter `Adobe.Flash.Control.manifest` e o OCX assinado referenciado. Não há fallback para notas inventadas ou runtime baixado de mirror: versão, JSON fonte, tag, nome do setup e manifesto precisam coincidir.

## Publicação por tag e bootstrap

O workflow `.github/workflows/release.yml` aceita somente tags no formato `vMAJOR.MINOR.PATCH`. O job `build` possui apenas `contents: read`, usa actions fixadas por SHA, desabilita persistência de credenciais no checkout, instala uma versão fixa do Inno Setup, testa e gera os pacotes. Somente os arquivos explícitos são transferidos ao job `publish`, que recebe `contents: write`. Desde a 1.1.4, porém, a automação por tag permanece indisponível no runner hospedado enquanto não houver uma forma autorizada e segura de provisionar a origem licenciada do runtime; sem ela, o build falha antes de criar pacote incompleto. Por isso a 1.1.4 foi construída, validada e publicada manualmente a partir da máquina autorizada. O [workflow 29471074585](https://github.com/Jessielriffel2/UrusLauncher/actions/runs/29471074585), disparado pela criação da tag via API, foi cancelado antes de build/publicação para não concorrer com a release manual. O `GITHUB_TOKEN` é efêmero e não entra no código ou pacote.

A 1.1.0 permanece registrada como a primeira publicação com updater. Como a 1.0.1 não contém esse módulo, seus usuários precisam instalar manualmente a versão pública mais recente. A 1.1.1 adicionou a rota pública `releases/latest/download/update-manifest.json` para respostas `403`/`429`. As versões 1.1.1/1.1.2 recebem atualizações pelo fluxo antigo de **Atualizar**; a partir da 1.1.3, o launcher baixa e valida primeiro e aguarda **Instalar**. A 1.1.4 é a versão pública atual oferecida por esse fluxo. A execução sempre exige clique e ausência de sessões ativas.

## Instalador

O Inno Setup usa `AppId` estável, instala por padrão em `%LocalAppData%\Programs\Urus Launcher` e declara `PrivilegesRequired=lowest`, portanto não exige administrador. Aceita somente sistemas x64 compatíveis e Windows 10 ou superior. Cria entrada no menu Iniciar, desinstalador e, se selecionado, atalho de desktop. O ícone de instalação/desinstalação/atalhos vem de `urus-launcher.ico`.

O Inno consome recursivamente o payload já validado pelo pipeline. Antes da compilação, o PowerShell exige `UrusLauncher.App.exe`, `LegendLauncher.GameHost.Legacy.exe`, runtime .NET, WPF, WinForms, runtimeconfigs self-contained e `runtime\Adobe.Flash.Control.manifest` com OCX confinado/assinado. Também confirma que `WindowsBase.dll` é a implementação WPF completa e executa o launcher por sete segundos sem runtime .NET global. Assim, um pacote incompleto ou contaminado por uma facade do staging é rejeitado antes de gerar os entregáveis.

## ZIP portátil e Downloads

O ZIP contém a pasta `UrusLauncher` inteira, não um executável single-file. Para uso portátil, extraia a pasta e execute `UrusLauncher.App.exe`; mover somente esse EXE quebra as dependências e o GameHost.

O script de build **não copia automaticamente** instalador ou ZIP para `Downloads`. A saída local fica em `artifacts/urus-distribution`; o updater consome os artefatos públicos do GitHub Release. No handoff 1.1.4, o setup oficial, sua lista de checksums e uma cópia auditável do manifesto foram colocados explicitamente em `Downloads`:

- `%USERPROFILE%\Downloads\UrusLauncher-Setup-1.1.4-win-x64.exe`;
- `%USERPROFILE%\Downloads\UrusLauncher-SHA256SUMS-1.1.4.txt`;
- `%USERPROFILE%\Downloads\UrusLauncher-update-manifest-1.1.4.json`.

O SHA-256 do setup no handoff foi recalculado e coincidiu com `96A5C3C67AB46C22B8C7AF713C3C6056D50EEA35087FFC4EBA2CCBC3B8BC02C1`; versão, repositório, bytes e hash também coincidiram entre API, manifesto normal e fallback `releases/latest/download`. O ZIP portátil continua disponível no GitHub Release e foi auditado, mas não integra esse handoff em `Downloads`. O pipeline e o diretório local de build não dependem de `Downloads`.

## Segurança e limites

- Limpeza recursiva ocorre somente depois que os caminhos são normalizados e confirmados como filhos de `artifacts/urus-distribution`.
- O pipeline falha se App, GameHost, runtime, WPF, WinForms, ICO, ISS ou Inno Compiler estiverem ausentes, se `WindowsBase.dll` for uma facade ou se o processo encerrar durante o smoke de startup.
- O publish usa `PublishTrimmed=false`, evitando remover tipos usados por WPF, WinForms, COM ou reflexão.
- O pacote não inclui senha, perfil, settings, cache, token ou URI autenticada; dados continuam sob `%LocalAppData%\LegendLauncherNext` no computador de cada usuário.
- O repositório não contém o ActiveX. O build incorpora somente manifesto + OCX da origem fornecida; não copia `LegendOnline.exe`, `H2Proxy.exe`, perfis ou dados do cliente antigo.
- O manifesto de distribuição registra presença, caminho relativo, bytes e SHA-256 do runtime. Isso comprova integridade do artefato, não autorização jurídica.
- Os hashes são calculados com `System.Security.Cryptography.SHA256` sobre streams para funcionar tanto no PowerShell moderno quanto no Windows PowerShell, sem depender de `Get-FileHash`.
- Checksums detectam corrupção depois da geração, mas não substituem assinatura Authenticode. Os artefatos atuais não devem ser descritos como assinados se não houver certificado aplicado.
- O updater compara repositório, tag, manifesto, nome, bytes, digest disponível e SHA-256. O fallback 1.1.1 para rate limit usa somente a rota pública fixa do mesmo repositório e conserva essas verificações, mas manifesto e binário ainda compartilham o mesmo domínio de confiança do GitHub Release. Comprometimento do repositório não é resolvido somente por hash.
- Uma futura assinatura Authenticode deve ocorrer antes do cálculo/publicação dos hashes. Até lá, SmartScreen pode alertar e a documentação deve apresentar essa limitação.

## Dependências, consumidores e referências cruzadas

- O pipeline publica a [Launcher App](launcher-app.md) e o [GameHost Legacy](game-host-legacy.md).
- Ícone, executável e metadados seguem [Branding Urus](branding.md).
- A escolha arquitetural está em [ADR-007](../decisoes/ADR-007-identidade-e-distribuicao-urus.md) e o provisionamento/licenciamento em [ADR-009](../decisoes/ADR-009-provisionamento-runtime-legado.md).
- O consumo dos releases está em [atualizacao.md](atualizacao.md) e a decisão em [ADR-008](../decisoes/ADR-008-atualizacoes-github-releases.md).
- Requisitos do runtime legado estão em [ADR-002](../decisoes/ADR-002-runtime-flash.md).

## Testes e validação

`WindowsDistributionContractTests.cs` valida nomes públicos, configuração per-user/x64, ausência de caminho absoluto do computador de desenvolvimento, publicação self-contained, proteção do `WindowsBase.dll`, origem/cópia/Authenticode do runtime, entregáveis, hashing, definição trilíngue, manifesto do updater e relaunch do Inno. `LauncherCompositionTests.cs` fixa a prioridade de `runtime\`; `MainWindowLayoutXamlTests.cs` fixa status real e CTA inativo honesto. `GitHubReleaseContractTests.cs:5` cobre tag, permissões, artefatos, ausência de PAT e definições 1.1.0–1.1.4. A suíte da build pública 1.1.4 concluiu **465/465** em Release, além do smoke portátil, conferência visual automatizada e verificação pública de tamanhos/digests. A release histórica 1.1.3 continua auditada com **461/461** no workflow e hashes verificados. Testes de consulta/download/manifesto, cache e fallback de rate limit são detalhados em [atualizacao.md](atualizacao.md).
