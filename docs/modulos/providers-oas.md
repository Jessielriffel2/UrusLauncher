# Módulo Providers OAS

## Objetivo do módulo

`LegendLauncher.Providers.Oas` integra catálogo, Passport e entrada de servidores OAS aos contratos do [Core](core.md). O módulo normaliza servidores, mantém cache sem dados de conta, autentica cada tentativa em um jar de cookies isolado e entrega somente uma `LaunchSession` final permitida. Senha, `loginKey`, cookies e query de sessão não são persistidos nem aparecem em `ToString()`, exceções ou argumentos de processo.

O protocolo Passport e a resolução até `Loading.swf` foram validados de forma controlada no Reborn turco S115 em 14/07/2026. Em 15/07/2026, o mesmo backend foi validado no Classic Português S100: Passport aceitou a credencial salva e resolveu uma sessão em `s100sqptclas.creaction-network.com`. No QA integrado com a [aplicação](launcher-app.md) e o [GameHost legado](game-host-legacy.md), duas sessões simultâneas e embutidas carregaram até 99%; na build final anterior, uma sessão Reborn chegou à interface jogável dentro do launcher.

## Catálogo de servidores

- `OasPlatformCatalog` — contém as oito variantes OAS jogáveis: Brasil (`lobr`), turca (`lotr`), Classic portuguesa (`lorpt`), Reborn turca (`lortr`), polonesa (`lopl`), espanhola (`loes`), alemã (`lode`) e árabe (`loar`). `All` preserva a ordem da UI e `Find` compara IDs sem diferenciar maiúsculas/minúsculas. Referência: `src/LegendLauncher.Providers.Oas/OasPlatformCatalog.cs:8`.
- `OasServerDirectory(...)` — implementação de `IServerDirectory` com transporte/cache/timeout/relógio/cota injetáveis. Defaults: 10 segundos e 8 MiB. Referência: `src/LegendLauncher.Providers.Oas/OasServerDirectory.cs:9` e `:21`.
- `OasServerDirectory.GetServersAsync(...)` — valida plataforma/UID, consulta o endpoint fixo e grava apenas catálogo sanitizado; em falha recuperável usa cache ou lança `OasServerDirectoryException`. Entrada: plataforma, UID e cancelamento; saída: `Task<ServerCatalog>`. Referência: `src/LegendLauncher.Providers.Oas/OasServerDirectory.cs:49`.
- `OasServerDirectory.FetchAsync` — envia GET JSON com `ResponseHeadersRead`, exige origem efetiva exata, status de sucesso e corpo dentro da cota. Referência: `src/LegendLauncher.Providers.Oas/OasServerDirectory.cs:84`.
- `OasServerDirectory.TryUpdateCacheAsync`, `TryReadCacheAsync` e `RemoveAccountHistory` — removem `Played`/`Current` antes da fronteira compartilhável; falha de cache não invalida resposta remota válida. Referências: `src/LegendLauncher.Providers.Oas/OasServerDirectory.cs:127`, `:152` e `:224`.
- `OasServerPayloadParser.ParseAsync` / `BuildCatalog` / `ParseServer` — reconhecem wrappers e shapes legados, unem entradas, removem duplicatas, normalizam IDs/nomes/URLs/recomendação/abertura e apontam histórico para objetos canônicos. Referências: `src/LegendLauncher.Providers.Oas/OasServerPayloadParser.cs:11`, `:46` e `:205`.

## Autenticação e resolução da sessão

- `OasAuthenticationService()` — implementação de produção de `IGameAuthenticationService`. Cria `SocketsHttpHandler`, `HttpClient` e `CookieContainer` novos por tentativa; Passport usa o cliente .NET e somente a entrada pós-Passport usa o transporte compatível. Referências: `src/LegendLauncher.Providers.Oas/OasAuthenticationService.cs:14`, `:45` e `:73`.
- `OasAuthenticationService(handlerFactory, ...)` — construtor de DI/testes. Mantém todas as etapas no handler injetado, sem abrir processo externo. Defaults: 15 segundos no fluxo completo, 64 KiB para JSON e 1 MiB para HTML, com teto de 16 MiB. Referência: `src/LegendLauncher.Providers.Oas/OasAuthenticationService.cs:59`.
- `AuthenticateAsync(request, cancellationToken)` — valida UTF-16, plataforma, host e `/serverlist/s<dígitos>` antes da rede; executa Passport e resolução no mesmo jar isolado. Cancelamento explícito é propagado e timeout/rede/HTTP/tamanho/payload/origem viram erros locais estáveis. Referência: `src/LegendLauncher.Providers.Oas/OasAuthenticationService.cs:95`.
- `AuthenticatePassportAsync` — usa `https://passport.creaction-network.com/index.php` somente para `lortr` e `https://passport.oasgames.com/index.php` nas demais variantes. Envia `m=login`, usuário e senha escapados, exige origem efetiva exata, lê `val.loginKey`/`val.id` e cria o cookie seguro/HttpOnly `oas_user` para `.creaction-network.com`. Referências: `src/LegendLauncher.Providers.Oas/OasAuthenticationService.cs:181`, `:438`, `:448` e `:461`.
- `ResolveLaunchSessionAsync` — força `pay_later=1`, preserva os demais parâmetros e processa HTML/redirect controlado por no máximo dois saltos adicionais. `login?token` é follow-up; `game.jsp` vira `Loading.swf`. Referência: `src/LegendLauncher.Providers.Oas/OasAuthenticationService.cs:239`.
- `SendGetAsync` / `SendLaunchGetAsync` — o primeiro envia Passport com `Accept`, `User-Agent` real e cookies do jar; o segundo escolhe `OasCurlLaunchTransport` somente na composição de produção, mantendo o handler falso em testes. Referências: `src/LegendLauncher.Providers.Oas/OasAuthenticationService.cs:370` e `:392`.
- `OasPassportResponseParser.Parse` — aceita sucesso apenas com `val` objeto, `loginKey` textual válido e `id` positivo textual ou numérico. Falhas preservam somente `err_code` seguro e usam mensagem local; `ToString()` informa presença, nunca valores. Referências: `src/LegendLauncher.Providers.Oas/OasPassportResponseParser.cs:11`, `:57` e `:153`.
- `OasLaunchPageParser.Parse` / `TryCreateGameLaunch` — removem conteúdo não visual, localizam `frame`/`iframe`, fazem HTML decode, permitem apenas origens OAS e trocam exclusivamente `game.jsp` por `Loading.swf` preservando a query. Referências: `src/LegendLauncher.Providers.Oas/OasLaunchPageParser.cs:24` e `:65`.
- `OasOriginPolicy` — restringe Passport ao endpoint esperado, primeira página ao host da plataforma e follow-ups a HTTPS/443 sem userinfo dentro de `creaction-network.com`. Referência: `src/LegendLauncher.Providers.Oas/OasOriginPolicy.cs:8`.
- `BoundedHttpContentReader.ReadUtf8Async` — limita `Content-Length` e bytes efetivamente lidos, inclusive após descompressão. Referência: `src/LegendLauncher.Providers.Oas/BoundedHttpContentReader.cs:5`.
- `OasAuthenticationErrorCodes` — códigos locais estáveis para credencial, plataforma, servidor, HTTP, rede, timeout, cota, payload e origem. Referência: `src/LegendLauncher.Providers.Oas/OasAuthenticationErrorCodes.cs:7`.

## Transporte compatível da entrada

- `OasCurlLaunchTransport(requestTimeout, maximumResponseBytes)` — usa exclusivamente `Environment.SystemDirectory\curl.exe` nos GETs pós-Passport bloqueados pela borda Cloudflare. Entrada: URI OAS permitida, jar isolado e cancelamento; saída: `HttpResponseMessage` limitado. Referência: `src/LegendLauncher.Providers.Oas/OasCurlLaunchTransport.cs:13` e `:25`.
- `OasCurlLaunchTransport.SendGetAsync` — valida novamente a allowlist, monta configuração em memória, executa o processo sem janela e passa URI/cookie por `stdin`. `--disable` impede `.curlrc`; IPv4, HTTP/1.1, HTTPS-only, ausência de redirect automático, timeout e tamanho são argumentos estáticos. Referências: `src/LegendLauncher.Providers.Oas/OasCurlLaunchTransport.cs:51`, `:93`, `:139`, `:262` e `:317`.
- `OasCurlLaunchTransport.EscapeConfigValue` — rejeita controles/CRLF e escapa barras/aspas antes do formato de configuração. Referência: `src/LegendLauncher.Providers.Oas/OasCurlLaunchTransport.cs:114`.
- `OasCurlResponseParser.Parse` — lê o último bloco HTTP válido, limita 64 KiB de cabeçalhos e o corpo configurado, rejeita múltiplos `Location` e preserva somente `Location`/`Set-Cookie`. Referências: `src/LegendLauncher.Providers.Oas/OasCurlResponseParser.cs:7`, `:14` e `:82`.

A motivação, alternativas e consequências dessa fronteira estão em [ADR-003 — Transporte compatível para entrada OAS](../decisoes/ADR-003-transporte-oas-cloudflare.md).

## Limites de confiança e persistência

1. Catálogo e autenticação aceitam somente definições exatas de `OasPlatformCatalog`; redirects são manuais e cada URI é revalidada antes do envio de cookies.
2. Cada login possui handler/jar próprios. `oas_user` e cookies da borda morrem ao final da tentativa e não são compartilhados entre contas.
3. A senha viaja somente na requisição Passport do `HttpClient`. A ponte curl nunca recebe senha; URI/cookie de sessão entram por `stdin`, não em argumentos, logs ou exceções.
4. `loginKey` alimenta exclusivamente `oas_user`; `id` pode ser persistido como UID não secreto do perfil. O restante de `val` é ignorado.
5. A sessão entregue ao consumidor contém a URI `Loading.swf` com query opaca. O provider não duplica token em parâmetros nem o imprime.
6. A aplicação pode reutilizar a mesma credencial do login entre variantes OAS, mas cada tentativa recebe handler/jar novo e cada variante conserva UID e histórico próprios. Essa compatibilidade não se estende ao provider SevenWan.

## Dependências, consumidores e referências cruzadas

O projeto depende da BCL (`HttpClient`, `SocketsHttpHandler`, `CookieContainer`, JSON, regex, URI e `System.Diagnostics.Process`) e, no Windows, do `curl.exe` de `Environment.SystemDirectory`. Implementa contratos/modelos do [Core](core.md), usa o cache fornecido pela [Infrastructure](infrastructure.md) e é composto pela [aplicação](launcher-app.md). A `LaunchSession` segue pelo IPC do [GameHost legado](game-host-legacy.md); o provider não depende de WPF, do cofre concreto ou do ActiveX.

## Testes e validação

- `OasServerDirectoryTests.cs` cobre endpoint/origem, parsing, cache sanitizado, fallback, cota, timeout e cancelamento.
- `OasPlatformCatalogTests.cs` fixa oito variantes, labels/gamecodes/ordem/imutabilidade e endpoints.
- `OasAuthenticationServiceTests.cs` cobre endpoints Passport por plataforma, escaping, `loginKey`/`id`, `oas_user`, isolamento concorrente, redirects/follow-ups, allowlist, limites, timeout, cancelamento e representações sem segredo.
- `OasCurlLaunchTransportTests.cs` cobre caminho confiável, argumentos estáticos, config por `stdin`, escaping, allowlist e ausência de URI/cookie na linha de comando.
- `OasCurlResponseParserTests.cs` cobre status, cookies, redirects, proxy/interim, limites e payloads malformados.
- `OasLiveSmokeTests.cs` só acessa rede/cofre quando `LEGEND_OAS_LIVE_SMOKE=1`; usa o perfil local já salvo e valida S115 até `Loading.swf` sem iniciar Flash ou registrar segredo. A execução controlada de 14/07/2026 passou.

Suíte padrão permanece determinística e não acessa credenciais reais. O fluxo integrado confirmou duas sessões simultâneas e embutidas carregando até 99%, e a validação manual autenticada da build final confirmou uma sessão S115 na interface jogável. A verificação controlada do Classic Português S100 parou antes de iniciar Flash e confirmou somente Passport + resolução segura da sessão; nenhum teste automatizado registra senha, cookie ou URI autenticada.
