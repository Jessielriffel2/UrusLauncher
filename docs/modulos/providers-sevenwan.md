# Módulo Providers SevenWan

## Objetivo do módulo

`LegendLauncher.Providers.SevenWan` reconhece e normaliza os catálogos públicos de Wartune/Wartune Reborn expostos pela 7wan. Ele existe para preservar no seletor as variantes conhecidas do cliente legado sem misturá-las ao protocolo OAS. O módulo fornece catálogo/cache, mas não implementa autenticação: a [aplicação](launcher-app.md) associa essas variantes a uma resposta explícita de serviço indisponível.

## Funções e classes principais

- `SevenWanPlatformCatalog` — define 14 combinações de jogo/provedor, IDs persistidos, nomes, `ProviderPlatformId` e os dois endpoints HTTPS oficiais. `All` expõe somente `PlatformDefinition`; `Find` devolve a variante completa por ID. Referência: `src/LegendLauncher.Providers.SevenWan/SevenWanPlatformCatalog.cs:8`, `:33`, `:36` e `:73`.
- `SevenWanServerDirectory(...)` — implementação de `IServerDirectory` com `HttpClient`, cache, timeout, relógio e cota injetáveis. Defaults: 12 segundos e 2 MiB. Referência: `src/LegendLauncher.Providers.SevenWan/SevenWanServerDirectory.cs:10` e `:21`.
- `SevenWanServerDirectory.GetServersAsync(platform, userId, cancellationToken)` — exige variante exata e UID não negativo, busca catálogo remoto, atualiza cache e usa fallback em falhas recuperáveis. Entrada: plataforma/UID/cancelamento; saída: `Task<ServerCatalog>`. Referência: `src/LegendLauncher.Providers.SevenWan/SevenWanServerDirectory.cs:44`.
- `SevenWanServerDirectory.FetchAsync` — envia GET JSON com streaming, valida status e URI efetiva idêntica à solicitada, limita bytes e delega ao parser. Referência: `src/LegendLauncher.Providers.SevenWan/SevenWanServerDirectory.cs:80`.
- `SevenWanServerDirectory.ValidatePlatform` / `ValidateResponseOrigin` — impedem endpoint customizado, variante forjada e redirect silencioso. Referências: `src/LegendLauncher.Providers.SevenWan/SevenWanServerDirectory.cs:161` e `:175`.
- `SevenWanServerPayloadParser.ParseAsync` — seleciona no JSON o bucket do `ProviderPlatformId`, descarta entradas inválidas/duplicadas e produz catálogo remoto sem histórico de conta. Referência: `src/LegendLauncher.Providers.SevenWan/SevenWanServerPayloadParser.cs:9`.
- `SevenWanServerPayloadParser.ParseServer` — normaliza `sid`, linha, nome, estados de serviço e abertura Unix; constrói URI HTTPS `7.wan.com/game/login/?sid=...`. Referência: `src/LegendLauncher.Providers.SevenWan/SevenWanServerPayloadParser.cs:59` e `:101`.
- `SevenWanServerDirectoryException` — erro público contextual para catálogo ausente, origem inesperada, JSON inválido ou falta de cache utilizável. Referência: `src/LegendLauncher.Providers.SevenWan/SevenWanServerDirectoryException.cs:3`.

## Dependências e consumidores

O módulo depende da BCL (`HttpClient`, JSON e APIs de URI/tempo), dos modelos/contratos do [Core](core.md) e opcionalmente de `IServerCatalogCache`, implementado na [Infrastructure](infrastructure.md). A [aplicação](launcher-app.md) registra as 14 variantes por `PlatformAdapterRegistry`, compartilha o cache e bloqueia a tentativa de login com `UnavailablePlatformAuthenticationService` enquanto o protocolo 7wan não for suportado. O provider não depende de WPF, cofre, Passport OAS ou GameHost.

## Testes e estado funcional

`tests/LegendLauncher.Tests/SevenWan/SevenWanServerDirectoryTests.cs` cobre nomes/IDs das 14 variantes, parsing e disponibilidade, endpoint/origem estritos, cache, fallback, cota, timeout e payload inválido. Catálogo reconhecido não significa login funcional: a autenticação 7wan permanece deliberadamente indisponível e é informada antes de abrir o GameHost.

Referências cruzadas: o registro comum está descrito em [Launcher App](launcher-app.md); o provider jogável principal está em [Providers OAS](providers-oas.md).
