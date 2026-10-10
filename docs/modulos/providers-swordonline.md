# Módulo Providers SwordOnline

## Objetivo do módulo

`LegendLauncher.Providers.SwordOnline` conecta o launcher ao servidor próprio **SwordOnline** (motor LOReborn) hospedado em `https://swordonline.tech`. A conta pode ser criada no site ou dentro do próprio launcher; o launcher troca **login + senha** por uma ficha de jogo de curta duração e entrega ao Flash interno o `Loading.swf` com os flashvars que o cliente espera. É um provedor de produção e faz parte de todo release público.

## Funções e classes principais

- `SwordOnlinePlatformCatalog` — plataforma única `swordonline` ("SwordOnline", gamecode `swo`, locale `pt-BR`), com `RequiresPassword` no padrão (`true`). Expõe `Host`, `PortalBaseUri`, `MovieUri` (`/Loading.swf`), `ConfigUri` (`/config.xml`) e `LauncherUri` (`/api/launcher`). Ref: `src/LegendLauncher.Providers.SwordOnline/SwordOnlinePlatformCatalog.cs`.
- `SwordOnlineServerDirectory` — um único `GameServer` (`swordonline`, recomendado, `LaunchUri` = `Loading.swf`), **sempre válido e sem rede**. Antes ele sondava `GET /config.xml` (5 s) e marcava o servidor inválido se falhasse, o que deixava o botão Jogar desativado em silêncio em PCs lentos ou com internet ruim; agora, se o portal estiver fora do ar, o login mostra a mensagem de rede. Outra plataforma recebe catálogo vazio.
- `SwordOnlineAuthenticationService` — `POST https://swordonline.tech/api/launcher` com `{"login","senha"}` em JSON. Resposta `200 {"ok":true,"login","ficha"}` onde `ficha` precisa ter 64 hex. A resposta é lida com limite de 16 KB e a chamada tem timeout de 15 s (cancelamento do chamador é propagado; timeout vira `request_timeout`).
- `SwordOnlineAuthenticationErrorCodes` — mesmos códigos do OAS/Elarionis (`invalid_credentials`, `authentication_rejected`, `http_error`, `network_error`, `request_timeout`, `response_too_large`, `invalid_authentication_response`, `unsupported_platform`, `invalid_server`), de modo que as mensagens localizadas já existentes valem sem chave nova.

## Criar conta dentro do launcher

- `SwordOnlineAccountRegistrationService` — `POST https://swordonline.tech/api/conta/criar` com `{"login","senha","email"}` (o mesmo endpoint que a página `/criar-conta` usa). Sucesso: `200`/`201` com `{"ok":true}` (o `login` canônico é usado se vier). Falhas: `409` → `registration_login_taken`, `429` → `registration_too_many_requests`, `400` → `registration_rejected` (com o texto `erro` do site), demais → `http_error`. Timeout de 15 s, resposta limitada a 16 KB, senha só por HTTPS e nunca em exceção, log ou mensagem (teste `ReportsNetworkFailureWithoutLeakingThePassword`).
- As regras do site são conferidas antes de abrir a rede (`IsValidLogin`: `^[A-Za-z][A-Za-z0-9_]{3,15}$`; `IsValidPassword`: 8–64 caracteres com ao menos uma letra e um número; e-mail opcional com até 120 caracteres e formato mínimo `a@b.c`). O servidor continua sendo a autoridade e pode recusar.
- `SwordOnlineRegistrationErrorCodes` — `registration_invalid_login`, `registration_invalid_password`, `registration_invalid_email`, `registration_login_taken`, `registration_too_many_requests`, `registration_rejected`; falhas de transporte reaproveitam `SwordOnlineAuthenticationErrorCodes`.
- Contrato no Core: `IAccountRegistrationService`, `AccountRegistrationRequest`, `AccountRegistrationResult`. `PlatformDefinition.SupportsAccountCreation` (verdadeiro só no SwordOnline) decide se o app oferece o botão.
- App: `MainWindowViewModel.Registration.cs`. `CanCreateAccount` exige serviço registrado e plataforma que suporta. A conta criada é **sempre um perfil novo**: o perfil selecionado/editado nunca é sobrescrito. O botão fica na coluna de contas da tela principal (`CreateAccountButton`, visível quando `CanCreateAccount`) e há um link extra no editor de conta de um perfil novo (`ShowCreateAccountInEditor`). O painel `RegistrationOverlay` (`MainWindow.xaml`) abre por cima de tudo; as senhas ficam em `PasswordBox` e só passam ao ViewModel em `PasswordChanged` (limpas quando o painel fecha). Ao criar, `CompleteRegistrationAsync` preenche login/senha, marca "salvar senha" e chama `SaveProfileAsync`: o perfil fica salvo, a senha vai ao Cofre do Windows e o botão ENTRAR E JOGAR já está habilitado. Se salvar o perfil falhar, a conta existe e o status avisa para salvar manualmente.
- Textos em `Registration_*` nos três idiomas.

## Fluxo de autenticação

```
launcher                         swordonline.tech (nginx -> portal.py)            Flash (GameHost)
────────                         ──────────────────────────────────────           ────────────────
POST /api/launcher {login,senha} ──► contas.autenticar + emitir_ficha
        ◄── {"ok":true,"login":"Heroi","ficha":"<64 hex>"}
LaunchSession(Loading.swf, user=<login canônico>, key=<ficha>, site=local,
              config=https://swordonline.tech/config.xml, playfb=0, http=https:)
                                                                          ──► Loading.swf
                                                                          POST /login (p = RSA)
                                                                          socket 9200 (gateway)
```

Pontos que não são óbvios:

- `user` usa o **login canônico devolvido pelo site** (a grafia da conta), não o que foi digitado: o `/login` do jogo compara a conta e a ficha com essa grafia.
- `http=https:` (com os dois-pontos) liga `ComponentSetting.ISFROMHTTPS` no cliente (`Loading.as:145-148` do jogo) e faz o próprio cliente trocar `http` por `https` nas entradas do `config.xml`. Por isso o `config.xml` do servidor continua com `http://swordonline.tech/…`; colocar `https://` ali viraria `httpss`.
- A senha só trafega por HTTPS para `LauncherUri`, nunca entra em exceção, log ou `ErrorMessage` (teste `ReportsNetworkFailureWithoutLeakingThePassword`). `ErrorMessage` só repassa o campo `erro` do site, limitado a 200 caracteres.
- A ficha vale 2 h no servidor e é guardada só como hash; o launcher não a persiste.
- O cliente Flash abre socket em `swordonline.tech:9200` e pede a policy em `843`; ambos são do servidor (ver LOReborn `docs/modulos/` de produção).

## Ordem, selo HOT, abertura e senha

- **Primeiro da lista:** `LauncherComposition` registra o SwordOnline antes dos demais; `PlatformAdapterRegistry` mantém a ordem e a janela abre com `platformItems[0]` selecionado. Medido na árvore de UI do app instalado: `SwordOnline | HOT`, OAS (8), 7wan (13) e Elarionis.
- **Selo HOT:** `PlatformDefinition.IsHot` → `PlatformItemViewModel.IsHot` → `MainWindow.xaml` (item do combo de plataforma): texto vermelho com `DropShadowEffect` cujo `BlurRadius`, a cor e a opacidade pulsam em `Storyboard` infinito. Medido em 6 capturas seguidas: a cor média do selo variou de `(196,55,56)` a `(213,103,98)`.
- **Abertura:** `MainWindowViewModel.LoadProfilesAsync(..., preferHotPlatform: true)` (só em `InitializeAsync`). Prefere um perfil que já seja da plataforma HOT; sem nenhum, mantém o último perfil usado, e força a plataforma HOT por cima (a seleção de perfil normalmente troca para a plataforma dele). **Nunca abre o formulário de perfil sozinho**; com zero perfis o formulário abre como no launcher original. Um perfil é um login que pode jogar em qualquer plataforma, e a plataforma gravada nele é a do último uso (`platformId` em `profiles.json`).
- **Senha:** `CanStartGame` exige `PendingPassword` ou senha salva no Cofre (`MainWindowViewModel.cs`, `CanStartGame`). `ShowInlinePassword` mostra um `PasswordBox` no cartão da conta quando não há senha salva; o texto digitado vai para `PendingPassword` e o botão libera. "Manter/salvar senha" (`RememberPassword`) grava no Cofre no lançamento. Antes disso o único campo de senha ficava em "Editar conta" e o salvar o limpava. `PlayButton_OnClick` só copia uma senha realmente digitada (`PasswordSelection.FromBoxes`); antes ele copiava o campo vazio do editor e apagava a senha do cartão antes de o comando rodar. Medido na janela real por UI Automation: botão desabilitado → digitar texto no campo "Senha" → habilitado → apagar → desabilitado.

## Registro e listas de permissão

- Registro: `LauncherComposition.cs` cria `SwordOnlineServerDirectory`/`SwordOnlineAuthenticationService` com o `HttpClient` compartilhado e os registra em `PlatformAdapterRegistry`.
- `BridgeSecurityPolicy.DefaultHostSuffixes` (`swordonline.tech`) — vale também para `LegacyLaunchUriPolicy`, que delega a ela.
- `ServerRowViewModel.IsAllowedLaunchAddress` — habilita o botão Jogar para `swordonline.tech` e subdomínios, só em HTTPS/443; `swordonline.tech.attacker.example` e `http://` continuam bloqueados.

## Testes

`tests/LegendLauncher.Tests/SwordOnline/` cobre catálogo (plataforma única, senha exigida, endpoint HTTPS), diretório (válido com 200; inválido com 404/502/portal fora do ar; outra plataforma sem rede) e autenticação (corpo JSON do POST, flashvars completos, mapeamento 401/400/429/500, corpo de sucesso malformado, resposta grande demais, falha de rede sem vazar a senha, cancelamento, credencial vazia sem rede, plataforma/servidor estranhos). As allowlists ganharam `swordonline.tech` em `BridgeSecurityPolicyTests`, `LegacyLaunchUriPolicyTests` e `ServerRowViewModelTests`, incluindo `http://` e `swordonline.tech.attacker.example` negados.

## Dependências e consumidores

Depende apenas do [Core](core.md) e da BCL. É consumido pela [aplicação](launcher-app.md) (registro) e pelo [GameHost Legacy](game-host-legacy.md) (política do filme).

## Não determinado

- A abertura real do jogo pelo ActiveX do launcher contra `swordonline.tech` (Loading → criação de personagem → mundo) ainda precisa ser feita com o launcher aberto; os testes automatizados cobrem só a parte do launcher.
