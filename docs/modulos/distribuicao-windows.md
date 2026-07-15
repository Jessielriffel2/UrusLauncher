# Módulo Distribuição Windows

## Objetivo do módulo

O pipeline de distribuição transforma o código Release do Urus Launcher em dois entregáveis Windows x64 reproduzíveis: instalador por usuário criado com Inno Setup e ZIP portátil. Ambos usam o mesmo payload **self-contained**, contendo o runtime .NET necessário, `UrusLauncher.App.exe` e o GameHost isolado. O processo também produz manifesto de distribuição, manifesto de atualização, patch notes trilíngues e checksums. Um workflow por tag publica esses arquivos no GitHub Releases usado pelo updater.

Self-contained refere-se ao runtime .NET/WPF/WinForms. O Adobe Flash ActiveX legado não é redistribuído: enquanto esse runtime for necessário, a máquina ainda precisa possuir uma instalação local compatível e licenciada que o launcher possa detectar.

## Arquivos, funções e saídas principais

| Referência aproximada | Elemento | Responsabilidade |
| --- | --- | --- |
| `scripts/build-urus-distribution.ps1:1` | Parâmetros | Recebe versão semântica, RID restrito a `win-x64`, caminho do `ISCC.exe` e opção explícita `-SkipTests`. |
| `scripts/build-urus-distribution.ps1:19` | `Invoke-CheckedCommand(...)` | Executa dotnet/ISCC e transforma qualquer exit code não zero em falha do pipeline. |
| `scripts/build-urus-distribution.ps1:34` | `Get-VerifiedChildPath(...)` | Confina diretórios removidos/criados ao root esperado antes de qualquer limpeza recursiva. |
| `scripts/build-urus-distribution.ps1:71` | `Assert-SelfContainedApplication(...)` | Verifica runtimeconfig/deps, `includedFrameworks` e runtime pack `win-x64` para App e GameHost. |
| `scripts/build-urus-distribution.ps1:100` | `Assert-WpfWindowsBase(...)` | Exige a implementação WPF completa de `WindowsBase.dll`, impedindo que uma facade do GameHost substitua o runtime da App. |
| `scripts/build-urus-distribution.ps1:124` | `Test-PortableLauncherStartup(...)` | Inicia o payload sem runtime global, observa o processo e falha se ele não alcançar a janela responsiva. |
| `scripts/build-urus-distribution.ps1:181` | `Get-ArtifactRecord(...)` | Calcula SHA-256 por stream e devolve nome, bytes e hash do artefato. |
| `scripts/build-urus-distribution.ps1:205` | Preparação | Resolve solution, projetos, ISS, ICO, definição de release, dotnet e diretórios sob `artifacts/urus-distribution`. |
| `scripts/build-urus-distribution.ps1:228` | Definição da versão | Exige `docs/releases/vX.Y.Z.json`; a validação trilíngue e conversão das notas começam na linha 239. |
| `scripts/build-urus-distribution.ps1:278` | Teste e publish | Executa a suíte Release por padrão e publica App/GameHost separadamente como self-contained, sem trim e sem símbolos de debug. |
| `scripts/build-urus-distribution.ps1:324` | Composição e validação | Mantém o payload/runtime WPF da App e copia somente os quatro arquivos próprios do GameHost; inclui o ICO e rejeita o executável obsoleto. |
| `scripts/build-urus-distribution.ps1:366` | Instalador e ZIP | Compila o ISS, cria o ZIP do mesmo payload na linha 378 e valida que ambos existem. |
| `scripts/build-urus-distribution.ps1:390` | Manifesto do updater | Gera `update-manifest.json` com repositório, versão, setup, bytes, SHA-256 e notas nos três idiomas. |
| `scripts/build-urus-distribution.ps1:405` | Patch notes | Gera `RELEASE_NOTES.md` a partir da mesma definição fonte. |
| `scripts/build-urus-distribution.ps1:442` | Manifesto/checksums | Registra distribuição/updater e grava `SHA256SUMS.txt` também para manifesto e notas. |
| `installer/UrusLauncher.iss:1` | Definições do produto | Fixa nome, executáveis, versão, diretórios de entrada/saída e ICO. |
| `installer/UrusLauncher.iss:21` | `[Setup]` | Instalador x64 por usuário, sem elevação, para Windows 10+, com LZMA2, wizard moderno e identidade Urus. |
| `installer/UrusLauncher.iss:58` | Idiomas/tarefas | Oferece inglês, português brasileiro e espanhol e atalho de desktop opcional. |
| `installer/UrusLauncher.iss:66` | Arquivos/atalhos/run | Instala o payload completo, cria atalhos e oferece iniciar o launcher ao final. A linha 76 reabre após update e `[Code]` reconhece `/RELAUNCH`. |
| `.github/workflows/release.yml:15` | Job `build` | Usa somente `contents: read`, actions fixadas por commit, checkout sem credenciais persistidas, .NET 10 e Inno 6.7.1; testa/constrói e transfere o artefato com as majors `upload-artifact@v5` e `download-artifact@v6`, compatíveis com Node 24. |
| `.github/workflows/release.yml:72` | Job `publish` | Só inicia após `build`, baixa os artefatos validados e recebe `contents: write` apenas para criar o GitHub Release. |
| `docs/releases/v1.1.0.json:1` | Patch notes fonte histórica | Título e mudanças do primeiro bootstrap com updater nos três idiomas. |
| `docs/releases/v1.1.1.json:1` | Patch notes fonte atual | Título e mudanças do fallback de rate limit em `pt-BR`, `en-US` e `es-ES`; novas versões exigem arquivo equivalente. |
| `artifacts/urus-distribution/portable/UrusLauncher/` | Payload expandido | Diretório executável usado como origem comum do Inno Setup e do ZIP. |
| `artifacts/urus-distribution/distribution-manifest.json` | Manifesto | Produto, versão, RID, flag self-contained, data UTC, nomes/tamanhos/hashes e inventário agregado do payload. |
| `artifacts/urus-distribution/update-manifest.json` | Manifesto de atualização | Contrato estrito consumido pela App com metadados do setup e patch notes localizados. |
| `artifacts/urus-distribution/RELEASE_NOTES.md` | Notas do release | Corpo trilíngue usado pelo GitHub Release. |
| `artifacts/urus-distribution/SHA256SUMS.txt` | Checksums | SHA-256 em formato simples para validar instalador, ZIP, manifesto de atualização e notas. |

## Entregáveis 1.0.1

| Tipo | Caminho |
| --- | --- |
| Instalador Inno Setup | `artifacts/urus-distribution/UrusLauncher-Setup-1.0.1-win-x64.exe` — 55.911.364 bytes — SHA-256 `8F4C60EA43A2F8C36D49A47812F537BB55F5FA69878F3B949AF2865E15EDB841` |
| ZIP portátil | `artifacts/urus-distribution/UrusLauncher-1.0.1-portable-win-x64.zip` — 77.366.737 bytes — SHA-256 `E255109957C64A105F03B4EC00CB1C9177D517F0E04CC1EE5441476C42EFA2FF` |
| Aplicativo principal | `portable/UrusLauncher/UrusLauncher.App.exe` |
| GameHost isolado | `portable/UrusLauncher/LegendLauncher.GameHost.Legacy.exe` |

O payload expandido final contém 468 arquivos e 183.601.556 bytes. Esses valores e os hashes acima pertencem exatamente à execução 1.0.1, registrada em `distribution-manifest.json` e `SHA256SUMS.txt`; qualquer nova build precisa publicar seus próprios valores. A versão passada em `-Version` também entra nos nomes e metadados do instalador. A entrega 1.0.0 permanece registrada como histórico na iteração 7 de [`design-qa.md`](../../design-qa.md).

## Fluxo de construção

Pré-requisitos do mantenedor:

- Windows x64;
- .NET SDK 10;
- Inno Setup 6, normalmente em `%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe`;
- espaço para o publish self-contained expandido e os dois pacotes.

Na raiz do repositório:

```powershell
.\scripts\build-urus-distribution.ps1 -Version 1.1.1
```

O fluxo normal executa testes, publica os dois processos, compõe e valida o payload, faz o smoke do executável portátil, compila o instalador, cria o ZIP e emite manifesto/checksums. A composição não sobrepõe bibliotecas compartilhadas da App com as facades do staging do GameHost. `-SkipTests` existe para iterações locais conscientes; não deve ser usado na distribuição final.

Antes do comando, deve existir `docs/releases/v1.1.1.json` (ou o arquivo da versão passada) com conteúdo não vazio nas três culturas. Não há fallback para notas inventadas no build: versão do parâmetro, JSON fonte, tag, nome do setup e manifesto precisam coincidir.

## Publicação por tag e bootstrap

O workflow `.github/workflows/release.yml` aceita somente tags no formato `vMAJOR.MINOR.PATCH`. O job `build` possui apenas `contents: read`, usa actions fixadas por SHA, desabilita persistência de credenciais no checkout, instala uma versão fixa do Inno Setup, testa e gera os pacotes. Somente os arquivos explícitos são transferidos como artefato temporário para o job `publish`. Esse segundo job possui `contents: write` e usa `gh release create` para publicar setup, ZIP, `update-manifest.json` e `SHA256SUMS.txt`, com `RELEASE_NOTES.md` como corpo. O `GITHUB_TOKEN` é efêmero da execução e não entra no código ou pacote.

A 1.1.0 permanece registrada como a primeira publicação com updater. Como a 1.0.1 não contém esse módulo, seus usuários precisam instalar manualmente a versão pública mais recente, agora a 1.1.1. Usuários da 1.1.0 também podem precisar da instalação manual quando a API estiver limitada por um IP compartilhado; a 1.1.1 passa a recorrer à rota pública `releases/latest/download/update-manifest.json` em respostas `403`/`429`. Esse fallback só descobre o manifesto e não altera o clique obrigatório nem as validações do instalador. Antes de existir um release válido, a consulta retorna falha recuperável e o launcher continua utilizável.

## Instalador

O Inno Setup usa `AppId` estável, instala por padrão em `%LocalAppData%\Programs\Urus Launcher` e declara `PrivilegesRequired=lowest`, portanto não exige administrador. Aceita somente sistemas x64 compatíveis e Windows 10 ou superior. Cria entrada no menu Iniciar, desinstalador e, se selecionado, atalho de desktop. O ícone de instalação/desinstalação/atalhos vem de `urus-launcher.ico`.

O Inno consome recursivamente o payload já validado pelo pipeline. Antes da compilação, o PowerShell exige `UrusLauncher.App.exe`, `LegendLauncher.GameHost.Legacy.exe`, runtime .NET, WPF, WinForms e os runtimeconfigs self-contained. Também confirma que `WindowsBase.dll` é a implementação WPF completa e executa o launcher por sete segundos sem runtime .NET global. Assim, um pacote incompleto ou contaminado por uma facade do staging é rejeitado antes de gerar os entregáveis.

## ZIP portátil e Downloads

O ZIP contém a pasta `UrusLauncher` inteira, não um executável single-file. Para uso portátil, extraia a pasta e execute `UrusLauncher.App.exe`; mover somente esse EXE quebra as dependências e o GameHost.

O script de build **não copia automaticamente** instalador ou ZIP para `Downloads`. Os artefatos canônicos ficam em `artifacts/urus-distribution`. No handoff 1.0.1, foram copiados explicitamente para `Downloads`:

- `%USERPROFILE%\Downloads\UrusLauncher-Setup-1.0.1-win-x64.exe`;
- `%USERPROFILE%\Downloads\UrusLauncher-SHA256SUMS-1.0.1.txt`.

O SHA-256 do setup foi recalculado depois da cópia e coincide com o checksum publicado. O ZIP portátil continua disponível na saída canônica, mas não integra esse handoff em `Downloads`. O pipeline e o diretório canônico não dependem de `Downloads`.

## Segurança e limites

- Limpeza recursiva ocorre somente depois que os caminhos são normalizados e confirmados como filhos de `artifacts/urus-distribution`.
- O pipeline falha se App, GameHost, runtime, WPF, WinForms, ICO, ISS ou Inno Compiler estiverem ausentes, se `WindowsBase.dll` for uma facade ou se o processo encerrar durante o smoke de startup.
- O publish usa `PublishTrimmed=false`, evitando remover tipos usados por WPF, WinForms, COM ou reflexão.
- O pacote não inclui senha, perfil, settings, cache, token ou URI autenticada; dados continuam sob `%LocalAppData%\LegendLauncherNext` no computador de cada usuário.
- O ActiveX Flash e arquivos do cliente antigo não são incorporados ao instalador/ZIP.
- Os hashes são calculados com `System.Security.Cryptography.SHA256` sobre streams para funcionar tanto no PowerShell moderno quanto no Windows PowerShell, sem depender de `Get-FileHash`.
- Checksums detectam corrupção depois da geração, mas não substituem assinatura Authenticode. Os artefatos atuais não devem ser descritos como assinados se não houver certificado aplicado.
- O updater compara repositório, tag, manifesto, nome, bytes, digest disponível e SHA-256. O fallback 1.1.1 para rate limit usa somente a rota pública fixa do mesmo repositório e conserva essas verificações, mas manifesto e binário ainda compartilham o mesmo domínio de confiança do GitHub Release. Comprometimento do repositório não é resolvido somente por hash.
- Uma futura assinatura Authenticode deve ocorrer antes do cálculo/publicação dos hashes. Até lá, SmartScreen pode alertar e a documentação deve apresentar essa limitação.

## Dependências, consumidores e referências cruzadas

- O pipeline publica a [Launcher App](launcher-app.md) e o [GameHost Legacy](game-host-legacy.md).
- Ícone, executável e metadados seguem [Branding Urus](branding.md).
- A escolha arquitetural está em [ADR-007](../decisoes/ADR-007-identidade-e-distribuicao-urus.md).
- O consumo dos releases está em [atualizacao.md](atualizacao.md) e a decisão em [ADR-008](../decisoes/ADR-008-atualizacoes-github-releases.md).
- Requisitos do runtime legado estão em [ADR-002](../decisoes/ADR-002-runtime-flash.md).

## Testes e validação

`WindowsDistributionContractTests.cs` valida nomes públicos, configuração per-user/x64, publicação self-contained, proteção do `WindowsBase.dll`, entregáveis, hashing, definição trilíngue, manifesto do updater e relaunch do Inno. `GitHubReleaseContractTests.cs:5` fixa tag, permissões, teste, artefatos e ausência de PAT no workflow. Os hashes/tamanhos acima continuam sendo somente o histórico 1.0.1; não se atribuem valores à 1.1.1 antes de o novo pipeline terminar. Testes de consulta/download/manifesto e fallback de rate limit são detalhados em [atualizacao.md](atualizacao.md).
