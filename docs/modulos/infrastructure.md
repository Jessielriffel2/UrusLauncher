# Módulo Infrastructure

## Objetivo do módulo

`LegendLauncher.Infrastructure` implementa os adaptadores locais do launcher: caminhos graváveis do usuário, inclusive o diretório confinado de downloads de atualização, persistência JSON atômica, cache de catálogos, perfis sem senha, settings não sensíveis, Windows Credential Manager e descoberta somente leitura dos assets Flash já instalados.

O módulo não lê nem migra dados do cliente antigo. Chaves do cofre devem começar com `LegendLauncherNext/`. O probe apenas localiza `Adobe.Flash.Control.manifest` e o OCX referenciado: não registra COM, não executa binários, não muda permissões e não depende de `H2Proxy.exe`.

## Arquivos, classes e funções principais

| Referência aproximada | Tipo/função | Responsabilidade, entrada e saída |
| --- | --- | --- |
| `src/LegendLauncher.Infrastructure/Paths/AppPaths.cs:6` | `AppPaths` | Calcula caminhos sob `%LocalAppData%\LegendLauncherNext`. Os overloads recebem opcionalmente base/nome da aplicação e expõem raiz, cache, dados, updates e arquivos JSON. |
| `src/LegendLauncher.Infrastructure/Paths/AppPaths.cs:42` | `UpdatesDirectory` | Define `%LocalAppData%\LegendLauncherNext\updates`, separado de cache/dados e usado somente por instaladores temporários/validados. |
| `src/LegendLauncher.Infrastructure/Paths/AppPaths.cs:65` | `EnsureDirectories()` | Cria somente `cache/`, `data/` e `updates/` pertencentes ao launcher novo. |
| `src/LegendLauncher.Infrastructure/Persistence/AtomicJsonFileStore.cs:10` | `AtomicJsonFileStore<TDocument>` | Primitiva JSON com trava compartilhada por caminho e opções de serialização seguras. Entrada: arquivo/documento; saída: documento tipado ou alteração persistida. |
| `src/LegendLauncher.Infrastructure/Persistence/AtomicJsonFileStore.cs:35` | `ReadAsync(...)` | Lê um documento sob trava, devolvendo `null` quando o arquivo não existe. |
| `src/LegendLauncher.Infrastructure/Persistence/AtomicJsonFileStore.cs:48` | `WriteAsync(...)` | Serializa para temporário no mesmo diretório e move sobre o destino; limpa o temporário em falhas. |
| `src/LegendLauncher.Infrastructure/Persistence/AtomicJsonFileStore.cs:67` | `UpdateAsync(...)` | Executa leitura-modificação-gravação como uma seção crítica por arquivo. |
| `src/LegendLauncher.Infrastructure/Persistence/AtomicJsonFileStore.cs:87` | `DeleteAsync(...)` | Remove o documento sob a mesma trava e informa se ele existia. |
| `src/LegendLauncher.App/Services/LauncherSettingsService.cs:7` | `LauncherSettingsSnapshot` | Consumidor da primitiva atômica que agrega mudo, layout, último perfil, idioma e `LastDonationPromptUtc`; default do horário é `null`. |
| `src/LegendLauncher.App/Services/LauncherSettingsService.cs:96` | `SaveDonationPromptShownAsync(...)` | Atualiza somente o timestamp UTC da exibição, preservando todas as demais preferências no mesmo documento. |
| `src/LegendLauncher.Infrastructure/Persistence/JsonServerCatalogCache.cs:10` | `JsonServerCatalogCache` | Implementa `IServerCatalogCache` em um dicionário por plataforma. |
| `src/LegendLauncher.Infrastructure/Persistence/JsonServerCatalogCache.cs:23` | `GetAsync(...)` | Busca por `platformId` sem diferenciar maiúsculas e devolve uma cópia marcada como cache. |
| `src/LegendLauncher.Infrastructure/Persistence/JsonServerCatalogCache.cs:43` | `SetAsync(...)` | Atualiza atomicamente somente a entrada da plataforma recebida. |
| `src/LegendLauncher.Infrastructure/Persistence/JsonProfileStore.cs:12` | `JsonProfileStore` | Implementa `IProfileStore`; valida `Guid` e namespace da chave do cofre antes de salvar metadados não secretos. |
| `src/LegendLauncher.Infrastructure/Persistence/JsonProfileRepository.cs:9` | `JsonProfileRepository<TProfile,TKey>` | Repositório genérico que lista, localiza, inclui/substitui e exclui perfis sobre o store atômico. |
| `src/LegendLauncher.Infrastructure/Security/CredentialKey.cs:7` | `CredentialKey` | Define o namespace do launcher. `ForProfile(Guid)` cria alvo estável e `Validate(string)` rejeita chave vazia, externa, longa ou com controle. |
| `src/LegendLauncher.Infrastructure/Security/WindowsCredentialVault.cs:14` | `WindowsCredentialVault` | Implementa `ICredentialVault` com credenciais genéricas do Windows, sem enumerar outros alvos. |
| `src/LegendLauncher.Infrastructure/Security/WindowsCredentialVault.cs:18` | `GetAsync(...)` | Lê usuário/senha por chave validada; devolve `null` para alvo ausente e zera cópias temporárias do blob. |
| `src/LegendLauncher.Infrastructure/Security/WindowsCredentialVault.cs:57` | `SetAsync(...)` | Valida limites, grava com `CredWriteW` e zera os bytes da senha após a chamada nativa. |
| `src/LegendLauncher.Infrastructure/Security/WindowsCredentialVault.cs:137` | `DeleteAsync(...)` | Exclui com `CredDeleteW`; ausência já é sucesso. |
| `src/LegendLauncher.Infrastructure/Security/WindowsCredentialNative.cs:5` | `WindowsCredentialNative` | Interop interno para `CredReadW`, `CredWriteW`, `CredDeleteW`, `CredFree` e estrutura `CREDENTIAL`. |
| `src/LegendLauncher.Infrastructure/Runtime/LegacyRuntimeProbe.cs:9` | `LegacyRuntimeProbe` | Descobre assets Flash sem escrever ou executar nada. |
| `src/LegendLauncher.Infrastructure/Runtime/LegacyRuntimeProbe.cs:13` | `Probe(...)` | Examina caminho configurado e ancestrais, prioriza o primeiro runtime completo e devolve o melhor diagnóstico parcial se não houver um utilizável. |
| `src/LegendLauncher.Infrastructure/Runtime/LegacyRuntimeProbe.cs:143` | `FindManifestFlashOcx(...)` | Lê XML com DTD proibido, limite de 1 MiB e resolver desativado; aceita somente OCX existente confinado à raiz do runtime. |
| `src/LegendLauncher.Infrastructure/Runtime/LegacyRuntimeProbeResult.cs:3` | `LegacyRuntimeProbeSource` | Informa se o resultado veio de caminho configurado, busca em ancestral ou não foi encontrado. |
| `src/LegendLauncher.Infrastructure/Runtime/LegacyRuntimeProbeResult.cs:10` | `LegacyRuntimeComponent` | Componentes necessários: manifesto Flash e controle ActiveX. `H2Proxy` não é componente. |
| `src/LegendLauncher.Infrastructure/Runtime/LegacyRuntimeProbeResult.cs:16` | `LegacyRuntimeProbeResult` | Saída imutável com usabilidade, raiz, paths encontrados, componentes faltantes e origem. |

## Entradas, saídas e armazenamento

- `%LocalAppData%\LegendLauncherNext\cache\server-catalogs.json` armazena catálogos não sensíveis por plataforma.
- `%LocalAppData%\LegendLauncherNext\data\profiles.json` armazena perfis sem senha: identidade, UID opcional do provider, chave opaca e último servidor.
- `%LocalAppData%\LegendLauncherNext\data\settings.json` armazena somente mudo global, layout 1/2/4, GUID do último perfil selecionado, `languageCode` normalizado para `pt-BR`, `en-US` ou `es-ES` e `lastDonationPromptUtc`. PID, HWND, login, senha, cookie, token e URI autenticada não são persistidos.
- `%LocalAppData%\LegendLauncherNext\updates` recebe `.part` durante download e o instalador final somente após validação. Não contém perfil, senha, cookie ou token.
- O Windows Credential Manager guarda `CredentialSecret` em alvos próprios; o JSON não recebe senha.
- O probe recebe caminho configurado opcional e pasta inicial e devolve apenas metadados de diagnóstico. O App passa a raiz aprovada ao GameHost; o projeto Infrastructure não inicia esse processo.

## Segurança e comportamento de falha

- O cofre nunca enumera credenciais e rejeita qualquer chave fora do namespace novo.
- Buffers gerenciados que contêm a senha são zerados após uso; mensagens nativas não incluem chave, usuário ou segredo.
- Escritas JSON usam temporário no mesmo diretório e substituição, reduzindo risco de arquivo parcial.
- `LauncherSettingsService`, compartilhado pelo [workspace](game-session-workspace.md), pela [localização](localizacao.md) e pelo [pedido de doação](donation-prompt.md), reutiliza `AtomicJsonFileStore`; documento ausente/corrompido usa defaults e a próxima atualização substitui o conteúdo inválido. Settings antigos sem idioma usam `pt-BR` e sem timestamp permitem a primeira exibição.
- Atualizações de `lastDonationPromptUtc` são independentes: registrar o modal não apaga mudo, layout, perfil ou idioma. Falha recuperável de I/O não impede o modal, login ou jogo.
- A leitura do manifesto bloqueia DTD/entidades externas e confina o caminho do OCX à raiz candidata.
- Nenhuma classe deste módulo executa `H2Proxy.exe`; o caminho de jogo atual é direto.
- `AppPaths` fornece apenas a raiz de updates. Download, allowlist, hashing e execução pertencem ao módulo [Atualização](atualizacao.md); Infrastructure não acessa GitHub nem inicia instalador.

## Dependências e consumidores

- O projeto referencia somente [`core.md`](core.md), cujos contratos `IServerCatalogCache`, `IProfileStore` e `ICredentialVault` são implementados aqui.
- Usa apenas APIs da plataforma .NET e interoperabilidade Win32; não possui pacote de produção adicional.
- É consumido por [`launcher-app.md`](launcher-app.md), pelo [workspace multissessão](game-session-workspace.md), por [localizacao.md](localizacao.md), pelo [pedido de doação](donation-prompt.md) e pela [atualização](atualizacao.md) para composição, persistência, diretórios privados e diagnóstico do runtime.
- O GameHost descrito em [`game-host-legacy.md`](game-host-legacy.md) não referencia Infrastructure. O App apenas entrega a ele a raiz previamente descoberta.

## Referências cruzadas

- Catálogos persistidos aqui são produzidos por [`providers-oas.md`](providers-oas.md).
- Sessões autenticadas e o isolamento de processo estão em [`game-host-legacy.md`](game-host-legacy.md).
- A política de rede do jogo está em [`network-bridge.md`](network-bridge.md); Infrastructure não abre portas nem faz proxy.
- O lembrete não usa rede nem cofre; [ADR-006](../decisoes/ADR-006-lembrete-doacao-nao-intrusivo.md) define a cadência e o timestamp não sensível.

## Testes

`tests/LegendLauncher.Tests/Infrastructure/` cobre paths — inclusive `UpdatesDirectory` nas linhas 18 e 35 de `AppPathsTests.cs` —, leitura/escrita/atualização atômicas, concorrência por arquivo, cache, CRUD de perfis, validação de chaves e descoberta segura do runtime. `tests/LegendLauncher.Tests/App/LauncherSettingsServiceTests.cs` cobre defaults, atualizações independentes, compatibilidade e recuperação de JSON corrompido. Os testes do updater usam diretórios temporários isolados e nunca acessam Downloads, perfis ou credenciais reais do usuário.
