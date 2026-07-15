# Urus Launcher

Launcher Windows para Legend Online, escrito do zero em C#/.NET 10 e distribuído publicamente como **Urus Launcher**. A aplicação reaproveita apenas os endpoints e os arquivos locais de compatibilidade necessários para acessar o jogo; identidade, interface, persistência e fluxo de sessão pertencem ao projeto novo.

> **Projeto não oficial.** O Urus Launcher não é afiliado, patrocinado nem aprovado pelos responsáveis por Legend Online. Nomes, marcas e conteúdo do jogo pertencem aos respectivos titulares. O projeto não redistribui o Flash ActiveX nem arquivos proprietários do cliente antigo.

## Estado atual

| Área | Estado |
| --- | --- |
| Identidade Urus | Nome, slogan localizado, logo original transparente, ícone Windows, GameHost e metadados implementados; marcadores de prévia/teste removidos da UI |
| Interface WPF moderna x64 | Launcher e workspace implementados a partir dos mocks aprovados; comparação visual final aprovada |
| Idiomas da interface | Português brasileiro, inglês e espanhol implementados; troca dinâmica, persistida e compartilhada com novas instâncias do GameHost |
| Apoio voluntário | Modal PayPal/PIX localizado, abertura manual e lembrete avaliado somente ao abrir, com intervalo de cinco horas |
| Catálogo oficial OAS e fallback em cache | Implementado |
| Busca, disponibilidade e selos do catálogo | Implementado; último servidor do perfil fica no topo como recomendado, lançamento mais recente recebe selo próprio e os demais formam uma seção separada |
| Múltiplos perfis/contas | Implementado; o mesmo login OAS pode alternar entre Brasil, Classic, Reborn e demais variantes sem perder a senha, mantendo UID/histórico separados por versão |
| Workspace multissessão com abas | Implementado; barra única de 44 px, controles/abas de 34 px, abas roláveis e `+ CONTA` sempre visível; um GameHost isolado por perfil + versão + servidor |
| Layouts simultâneos 1, 2 e 4 | Implementados; grade adaptativa 1×1, 1×2 ou 2×2, sem limitar o número de abas |
| Desacoplar/acoplar sessão | Implementado sem reiniciar o jogo, com rollback e shutdown idempotente |
| Janela principal responsiva | Implementada em 1420×820, com mínimo 1180×700, cabeçalho recolhido no workspace, setup rolável e status/ação/legenda fixos |
| Maximização em chrome próprio | Implementada pela área útil do monitor, sem cobrir a barra de tarefas; tamanho normal/restaurado é limitado em DIPs ao monitor atual |
| Som global dos jogos | Implementado por PID, com padrão mudo, preferência persistida e descarte seguro de callbacks |
| Último servidor por perfil | Implementado; fixado no topo como **RECOMENDADO**, independentemente para cada perfil e variante |
| Senhas no Windows Credential Manager | Implementado |
| Autenticação Passport OAS | Implementada nas oito variantes; QA abriu o Reborn turco S115 até a interface jogável e validou Passport + sessão do Classic Português S100 |
| GameHost Flash x64 separado | Implementado, isolado por sessão e encerrado quando o processo pai desaparece; jogabilidade real confirmada no S115 |
| Execução direta sem `H2Proxy.exe` | Implementada |
| Distribuição Windows | Pipeline self-contained `win-x64`, instalador Inno Setup por usuário, ZIP portátil, manifesto e SHA-256 implementados |
| Atualizações públicas | Consulta antecipada por GitHub Releases ao abrir, download/validação automática por usuário, cache verificado e instalação somente após clique explícito |
| Ruffle | Avaliação futura |
| Favoritos/múltiplos servidores fixados por conta | Melhoria futura |
| Login social | Melhoria futura |

## Tecnologia e arquitetura

- C# e .NET 10, com WPF nativo no launcher para manter consumo e distribuição menores que uma aplicação Electron.
- Identidade pública **Urus Launcher** com monograma “U” original, sem touro/escudo/trade dress, slogan localizado e executável principal `UrusLauncher.App.exe`. Namespaces/projetos `LegendLauncher.*` permanecem internos.
- Composição visual fiel ao mock de referência em 1584×992: contas à esquerda, catálogo ao centro e sessão à direita, com adaptação para outros tamanhos.
- MVVM simples, com responsabilidades separadas entre launcher, apresentação do catálogo, perfis, abertura e workspace multissessão.
- Localização dinâmica por 204 chaves em cada catálogo incorporado `pt-BR`, `en-US` e `es-ES`. Bindings observáveis atualizam launcher, workspace, atualizações, modal de apoio e janelas desacopladas sem reiniciar sessões; mensagens calculadas preservam chave/argumentos para serem reapresentadas na cultura ativa.
- O catálogo é ordenado por perfil e plataforma: `RecentServerIdsByPlatform[plataforma][0]`, gravado após uma abertura aceita, aparece primeiro com **RECOMENDADO**; os campos escalares são somente espelho/fallback de perfis antigos. O servidor válido já lançado mais recentemente, calculado por `StartTimeUtc` e desempate por `NumericId`, recebe **MAIS RECENTE**; um divisor localizado introduz os demais servidores e é recalculado durante a busca. Os dois selos podem coexistir.
- `HttpClient` para catálogo/Passport e curl nativo do Windows somente na entrada pós-login que a Cloudflare bloqueia no transporte .NET.
- Windows Credential Manager para senhas; o JSON local contém somente perfis e catálogo não sensíveis.
- GitHub Releases público para atualização preparada: o aplicativo consulta somente `Jessielriffel2/UrusLauncher`; se a API responder `403` ou `429` por rate limit, a versão 1.1.1+ tenta o manifesto público do último release. Na 1.1.3+, uma versão superior é baixada e validada em `%LocalAppData%\LegendLauncherNext\updates`, mas o setup só é executado depois do clique explícito em **Instalar**.
- Uma única instância do Urus Launcher fica ativa por sessão do Windows; tentar abri-lo novamente restaura/traz a janela existente para frente. Assim, perfis multissessão permanecem no mesmo processo e duas instâncias não disputam o cache do updater.
- WinForms/AxHost apenas no `LegendLauncher.GameHost.Legacy`, um processo x64 separado do launcher.
- Named Pipe restrito ao usuário e aos processos esperados para entregar a sessão autenticada ao GameHost sem expô-la na linha de comando.
- Um HWND-proxy pertencente ao WPF incorpora a janela validada do GameHost; o ActiveX continua no processo separado. Cada destino ativo (perfil + versão + servidor) tem PID/HWND próprios.
- A barra do workspace ocupa uma única linha de 44 px. Abas e controles têm 34 px; as abas rolam horizontalmente, `+ CONTA` permanece fora da rolagem e 150 px ficam reservados para os três botões compartilhados da janela principal.
- Abas rastreiam todas as sessões; layouts 1/2/4 controlam apenas quantas superfícies ficam visíveis. Na grade, 1/2/3–4 sessões ocupam 1×1/1×2/2×2. Uma sessão pode ser desacoplada e reanexada sem novo login.
- A janela principal abre em 1420×820, aceita até 1180×700, recolhe o cabeçalho de 96 px no workspace e compartilha os botões de minimizar/maximizar/fechar entre launcher e jogo. Na coluna de sessão, somente o setup rola; status de compatibilidade, ação principal e legenda permanecem fixos.
- `BorderlessWindowWorkArea` maximiza pela área útil do monitor atual e limita, considerando o DPI, o tamanho normal/restaurado à work area. Alterações de settings, display ou DPI disparam uma atualização coalescida mesmo com a janela parada. `BorderlessWindowCommands` centraliza minimizar, maximizar/restaurar e o glifo correspondente para a janela principal e as janelas desacopladas.
- A janela desacoplada mantém respiro entre título, ações de 32 px e caption buttons de 48 px; detach possui rollback, reattach único e fechamento coordenado no shutdown.
- Core Audio aplica um único estado de mudo somente aos PIDs GameHost registrados, captura falhas recuperáveis e aguarda callbacks pendentes no descarte.
- O resultado de abertura carrega o perfil efetivamente persistido; um GameHost iniciado mas não adotado pelo workspace é encerrado, assim como um GameHost cujo processo pai desapareça.
- A cultura ativa é configurada antes da janela principal e propagada a cada novo GameHost por `LEGEND_LAUNCHER_LANGUAGE` no ambiente do filho. Ela não entra na linha de comando, no Named Pipe nem na sessão autenticada.
- O pedido opcional de apoio é um overlay WPF local: o intervalo de cinco horas é avaliado uma vez por abertura, sem timer durante o jogo. O QR PayPal é um recurso imutável e o PIX copia somente a chave CNPJ pública para a área de transferência.
- O pipeline publica App e GameHost como self-contained `win-x64`, preserva o runtime WPF completo da App ao incorporar somente os quatro arquivos próprios do GameHost, executa um smoke de inicialização sem .NET global, produz instalador/ZIP do mesmo payload e emite manifesto/checksums. O .NET acompanha o pacote; o Flash ActiveX legado não é redistribuído.

O GameHost separado reduz o impacto de uma falha do ActiveX, mas **não é uma sandbox de segurança**. O caminho atual carrega o jogo diretamente e não exige, inicia nem incorpora o `H2Proxy.exe` do cliente antigo.

## Dados locais

Os arquivos mutáveis do projeto ficam em `%LocalAppData%\LegendLauncherNext`:

- `cache\server-catalogs.json`: último catálogo não sensível por plataforma;
- `data\profiles.json`: nome do perfil, plataforma mais recente, usuário, chave opaca do cofre e UID/histórico recente separados por plataforma;
- `data\settings.json`: mudo global, layout 1/2/4, GUID do último perfil selecionado, código de idioma e horário UTC da última exibição do pedido de apoio; todos são não sensíveis;
- `updates\`: downloads `.part` e instaladores de atualização validados; artefatos oficiais com mais de 24 horas podem ser limpos na abertura;
- Windows Credential Manager: usuário e senha, em alvos exclusivos com prefixo `LegendLauncherNext/`.

O launcher não lê nem migra senhas salvas pelo cliente antigo.

Esses dados não fazem parte do código, do instalador, do ZIP nem do GitHub Release. Instalar em outra máquina começa sem contas, perfis ou senhas; atualizar na mesma conta do Windows preserva os dados locais existentes.

O diretório `LegendLauncherNext` é mantido como detalhe interno de compatibilidade nesta versão, mesmo com a marca pública Urus. Assim, atualizar o executável não cria um segundo conjunto de perfis/settings/cache.

## Idioma da interface

O seletor no cabeçalho oferece **Português (Brasil)**, **English** e **Español**. A escolha muda imediatamente todos os textos controlados pelo launcher, inclusive status já visível, workspace e janelas de jogo desacopladas, e é restaurada de `settings.json` na próxima abertura. Valores das famílias `pt`, `en` e `es` são normalizados respectivamente para `pt-BR`, `en-US` e `es-ES`; qualquer valor desconhecido usa português brasileiro.

Nomes de conta, nomes oficiais de plataforma/servidor e o conteúdo Flash vêm de fontes externas e não são traduzidos. Cada GameHost novo recebe a cultura ativa para suas mensagens de preparação, diagnóstico e erro; trocar o idioma não reinicia um GameHost que já está jogando.

## Apoio ao projeto

O botão PayPal compacto no cabeçalho abre manualmente um modal de apoio em qualquer momento. Na abertura do programa, o mesmo modal aparece somente se nunca tiver sido exibido ou se já passaram pelo menos cinco horas; essa condição é avaliada uma única vez por abertura. Manter o launcher aberto por 12 horas não gera interrupção sobre o jogo. O horário é registrado quando o modal aparece, e fechar por Escape, pelo botão superior ou por **Agora não** não o altera novamente.

O painel oferece duas opções:

- PayPal por QR: o JPEG fornecido é empacotado sem alteração, possui 62.216 bytes, SHA-256 `EADCCECE3D8D2EC926C81AF0386A169178FA0795D6BADF7FC90794648601C6FC` e decodifica para `https://www.paypal.com/qrcodes/p2pqrc/YBY4YD5EV8JJE`;
- PIX Brasil: chave CNPJ `57.646.942/0001-69`, exibida como texto selecionável e copiável com feedback localizado.

Título, instruções, feedback, tooltips e acessibilidade existem em `pt-BR`, `en-US` e `es-ES`. O launcher não envia telemetria, não abre o navegador automaticamente e não mistura o pedido com perfis, credenciais ou sessões. Detalhes estão em [donation-prompt.md](docs/modulos/donation-prompt.md) e [ADR-006](docs/decisoes/ADR-006-lembrete-doacao-nao-intrusivo.md).

## Atualizações públicas

Em cada abertura, o launcher inicia antecipadamente e de forma assíncrona uma consulta ao último release público de `Jessielriffel2/UrusLauncher`. Um cartão discreto no canto inferior esquerdo mostra a busca e a versão instalada. Quando está atualizado ou ocorre uma falha recuperável, a pessoa pode consultar novamente sem reiniciar o programa.

Desde a 1.1.1, se a API pública do GitHub responder especificamente `403` ou `429` por limite compartilhado, a consulta tenta `https://github.com/Jessielriffel2/UrusLauncher/releases/latest/download/update-manifest.json`. Essa rota alternativa descobre apenas o mesmo manifesto público e não reduz as validações de repositório, versão, origem, nome, tamanho ou SHA-256. Isso melhora o comportamento em empresas, provedores e redes nas quais muitas máquinas compartilham um IP.

Na 1.1.3+, encontrar uma versão superior inicia automaticamente o download e a validação no diretório privado do usuário. Isso não executa código e não bloqueia catálogo, login ou jogo. Depois, a pessoa pode:

- continuar usando a versão instalada;
- abrir as novidades no idioma atual;
- clicar em **Instalar** quando não houver conta em jogo.

O setup é salvo primeiro como `.part` em `%LocalAppData%\LegendLauncherNext\updates`; URLs/repositório, tag, versão, nome, tamanho e SHA-256 são validados antes de ele ficar **pronto para instalar**. Um setup final já existente só é reaproveitado após nova conferência de tamanho e SHA-256. A janela total de download é de uma hora para tolerar conexões lentas. A instalação é bloqueada enquanto houver sessão ativa ou login em andamento, e o arquivo é revalidado imediatamente antes da execução. O instalador então fecha a App e relança a versão instalada. Perfis, settings e credenciais permanecem fora da pasta de instalação.

Os patch notes de cada versão nascem de `docs/releases/vX.Y.Z.json` em `pt-BR`, `en-US` e `es-ES`. O pipeline usa a mesma fonte para gerar `update-manifest.json` e o corpo do GitHub Release, evitando notas divergentes.

**Limite de confiança:** SHA-256 detecta corrupção e divergência de artefato, mas não substitui assinatura de código. Os pacotes atuais não possuem Authenticode e o Windows pode exibir SmartScreen. Uma futura assinatura deve acontecer antes do hash final. Consulte [atualizacao.md](docs/modulos/atualizacao.md), [ADR-008](docs/decisoes/ADR-008-atualizacoes-github-releases.md) e [SECURITY.md](SECURITY.md).

### Bootstrap e atualização para 1.1.3

A versão 1.0.1 não possui atualizador e precisa receber manualmente o instalador público mais recente. A 1.1.0 foi o primeiro bootstrap, mas sua consulta pode esbarrar na cota da API em redes de IP compartilhado; nesse caso, a passagem também é manual. As versões 1.1.1 e 1.1.2 detectam a 1.1.3 pelo fluxo anterior: nessa passagem única, a pessoa ainda clica em **Atualizar** para baixar e instalar. Depois de instalada a 1.1.3, versões futuras são baixadas e validadas automaticamente e ficam aguardando o clique em **Instalar**. Perfis, settings e senhas permanecem preservados.

## Desenvolvimento

Pré-requisitos:

- Windows 10 ou 11 x64;
- .NET SDK 10;
- uma instalação local compatível contendo `Adobe.Flash.Control.manifest` e o OCX referenciado, enquanto o caminho ActiveX for necessário.
- `curl.exe` incluído no diretório de sistema do Windows para a ponte compatível pós-Passport.

Comandos:

```powershell
dotnet restore .\LegendLauncherNext.slnx
dotnet build .\LegendLauncherNext.slnx --configuration Release
dotnet test .\LegendLauncherNext.slnx --configuration Release
dotnet run --project .\src\LegendLauncher.App\LegendLauncher.App.csproj
```

O projeto continua com o caminho-fonte `src\LegendLauncher.App`, mas o build gera `UrusLauncher.App.exe` por meio de `AssemblyName`. O namespace interno também permanece `LegendLauncher.App`.

### Gerar instalador e ZIP portátil

Com Inno Setup 6 instalado:

```powershell
.\scripts\build-urus-distribution.ps1 -Version 1.1.3
```

O script executa a suíte Release, publica App e GameHost self-contained para `win-x64`, valida o payload e produz:

- `artifacts\urus-distribution\UrusLauncher-Setup-1.1.3-win-x64.exe`;
- `artifacts\urus-distribution\UrusLauncher-1.1.3-portable-win-x64.zip`;
- `artifacts\urus-distribution\distribution-manifest.json`;
- `artifacts\urus-distribution\update-manifest.json`;
- `artifacts\urus-distribution\RELEASE_NOTES.md`;
- `artifacts\urus-distribution\SHA256SUMS.txt`.

Antes da build, deve existir `docs\releases\v1.1.3.json` (ou o arquivo da versão solicitada) com título e notas não vazias nos três idiomas. Para publicar no repositório público, envie uma tag exatamente no formato `vMAJOR.MINOR.PATCH`. O workflow separa build (`contents: read`) de publicação (`contents: write`), usa actions fixadas por commit e entrega ao job de release somente os artefatos validados. Nenhum PAT é incorporado ao aplicativo.

O instalador é per-user, sem elevação, e usa `%LocalAppData%\Programs\Urus Launcher`; oferece inglês, português brasileiro e espanhol e atalho de desktop opcional. O ZIP contém a pasta inteira `UrusLauncher`: extraia antes de executar `UrusLauncher.App.exe`.

O script não copia arquivos automaticamente para `Downloads`. No handoff 1.1.3, o setup público `UrusLauncher-Setup-1.1.3-win-x64.exe`, `UrusLauncher-SHA256SUMS-1.1.3.txt` e `UrusLauncher-update-manifest-1.1.3.json` foram baixados explicitamente para `%USERPROFILE%\Downloads`; bytes e hashes foram conferidos contra API, manifesto e fallback públicos. O ZIP portátil permanece em `artifacts\urus-distribution` e no GitHub Release. Veja [distribuicao-windows.md](docs/modulos/distribuicao-windows.md) para tamanhos e hashes completos.

O código-fonte deve permanecer fora de `Program Files`. A instalação antiga é consultada somente para localizar os assets de compatibilidade Flash existentes; dados novos são gravados no perfil do usuário.

## Como funciona o login

1. A pessoa seleciona ou cria um perfil, escolhe a plataforma e um servidor disponível.
2. A senha digitada, ou recuperada do Cofre do Windows, é enviada pelo `HttpClient` ao endpoint Passport atual da plataforma; o `loginKey` vira somente um cookie transitório `oas_user`.
3. A página de entrada é resolvida pela ponte limitada do curl do sistema. URI/cookies entram por `stdin`, redirects são validados um a um e nenhum segredo aparece nos argumentos do processo.
4. Após a OAS devolver a URI final de sessão, o launcher inicia um GameHost exclusivo e entrega essa URI por Named Pipe protegido.
5. O GameHost devolve seu HWND; o launcher valida HWND/PID, cria a aba e incorpora a janela no workspace sem carregar o ActiveX no WPF.
6. Depois de uma abertura aceita, o perfil registra o UID retornado e o último servidor. Se a opção de lembrar estiver ativa, a senha fica no Windows Credential Manager.

O projeto aceita vários perfis e preserva um histórico independente para cada um. Ao selecionar o perfil, o primeiro ID recente — registrado somente depois de entrar no jogo — é fixado no topo como **RECOMENDADO**; perfis antigos sem histórico usam `LastServerId` como fallback. Abaixo do divisor **OUTROS SERVIDORES**, o lançamento válido mais recente recebe **MAIS RECENTE** e os demais seguem em ordem decrescente. Se o último servidor usado também for o lançamento mais novo, os dois selos aparecem juntos. A busca recalcula a separação para não deixar um divisor sem o item fixado. Favoritos ou vários servidores fixados manualmente por conta ainda não fazem parte do MVP.

## Como usar várias contas

1. Abra uma conta normalmente com **ENTRAR E JOGAR**. Depois do handshake, o workspace aparece com uma aba para a sessão.
2. Use **+ CONTA** ou **VOLTAR AO LAUNCHER** para selecionar/criar outro perfil e iniciar outra conta. As abas podem rolar sem ocultar **+ CONTA**. Cada abertura usa autenticação isolada, e cada perfil em execução mantém GameHost, PID e HWND próprios.
3. Use os botões **1**, **2** ou **4** para escolher quantos jogos aparecem juntos. No modo **4**, uma conta ocupa 1×1, duas ocupam 1×2 e três ou quatro ocupam 2×2. Sessões além dessa capacidade continuam abertas e ficam acessíveis pelas abas/barra lateral.
4. **DESACOPLAR** move a sessão selecionada para outra janela. Fechar essa janela normalmente acopla o mesmo jogo de volta; **ENCERRAR** termina somente aquele GameHost.
5. **SOM GLOBAL** alterna o mudo de todos os jogos rastreados, inclusive desacoplados. O padrão é mudo e a escolha fica em `settings.json`.

Tentar jogar novamente com um perfil que já está em execução apenas seleciona sua aba. Fechar o launcher encerra todas as sessões rastreadas.

## Segurança e validação

O Adobe Flash ActiveX é legado e descontinuado. Ele nunca é carregado no processo WPF, não é registrado globalmente e não deve ser redistribuído sem permissão de licença. URIs de abertura são limitadas a HTTPS e origens aprovadas, e senha/sessão não entram em argumentos de processo nem em mensagens de diagnóstico.

Testes automatizados cobrem composição de catálogo, perfis, cofre, migração de estado por plataforma, autenticação OAS cruzada, isolamento SevenWan, alvo exato de sessão, transporte compatível, políticas de URI, settings, localização e propagação da cultura, pedido de apoio/intervalo/PIX/QR, áudio por PID e descarte concorrente, layouts 1/2/4, detach/reattach, maximização taskbar-aware, cleanup de sessão não adotada e protocolo launcher/GameHost. O updater possui contratos próprios para API/manifesto, fallback de rate limit, allowlist e redirects, download automático sem execução, cache revalidado, SHA-256, confinamento, instalação sob clique, caminho por usuário e workflow build/publish com permissões separadas. `LocalizationCatalogTests.cs` fixa 204 chaves equivalentes nos três idiomas. A validação histórica 1.0.1 permanece documentada em [`design-qa.md`](design-qa.md); resultados e hashes públicos ficam registrados na documentação de distribuição.

A validação 1.1.3 concluiu **461/461** testes em Debug e **461/461** em Release. O build canônico repete a suíte antes do smoke self-contained e da criação do setup, ZIP, manifesto e checksums; os resultados finais ficam em [distribuicao-windows.md](docs/modulos/distribuicao-windows.md).

O layout novo compila e preserva as funções documentadas do launcher. A captura autenticada da build final foi comparada lado a lado com a referência e aprovada em [`design-qa.md`](design-qa.md).

Consulte [docs/MAPA.md](docs/MAPA.md) para a estrutura completa, [branding.md](docs/modulos/branding.md) para a identidade Urus, [distribuicao-windows.md](docs/modulos/distribuicao-windows.md) para os pacotes, [localizacao.md](docs/modulos/localizacao.md) para os idiomas, [donation-prompt.md](docs/modulos/donation-prompt.md) para o apoio voluntário, [game-session-workspace.md](docs/modulos/game-session-workspace.md) para o workspace e [docs/decisoes](docs/decisoes) para as decisões arquiteturais.
