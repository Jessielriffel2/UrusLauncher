# Design QA — launcher e workspace multissessão

- Referência selecionada: imagem gerada durante a sessão de design (artefato local não versionado).
- Captura comparada: estado real autenticado no Reborn turco S115, já na interface jogável, usando `artifacts\publish-multisession-final` em desktop maximizado.
- Viewport/estado: workspace aberto, uma sessão Flash embutida, modo quatro selecionado, grade adaptativa 1 × 1 e áudio global mudo.

## Iteração 1

### P0 — espaço vertical desperdiçado com duas sessões

A captura real mantinha duas linhas fixas no modo quatro, embora somente duas sessões estivessem visíveis. As duas superfícies ficavam comprimidas na parte superior e o restante da janela permanecia vazio.

Correção aplicada:

- `GameWorkspaceViewModel` agora calcula 1 × 1 para uma sessão, 1 × 2 para duas e 2 × 2 para três ou quatro.
- mudanças em `VisibleSessions` notificam novamente linhas e colunas;
- containers e `GameSurfacePresenter` esticam nos dois eixos;
- teste automatizado cobre a progressão de uma, duas e três sessões.

### P1 — maximização cobria a barra de tarefas

O chrome próprio usava toda a área do monitor ao maximizar. `BorderlessWindowWorkArea` agora responde a `WM_GETMINMAXINFO` com a work area do monitor atual; teste cobre inclusive origem negativa de monitor secundário.

### P1 — janela desacoplada e ciclo de vida

O título podia colidir em largura mínima e falhas durante `Show()` deixavam a sessão invisível. O título agora usa ellipsis, o contexto é tipado, o fechamento é idempotente e criação/show fazem rollback para a superfície incorporada.

### P1 — processo sem adoção

Erros ou cancelamentos após o GameHost iniciar podiam deixar o processo sem dono. O fluxo agora mantém um snapshot efetivo de perfil/plataforma/servidor, encerra sessões não adotadas e o próprio GameHost monitora a vida do launcher pai.

## Verificação automatizada

- build Release: 0 erros e 0 avisos;
- testes: 279 aprovados, 0 falhas;
- arquivos de código: todos abaixo de 800 linhas.

## Iteração 2 — build final autenticada

A referência aprovada e a captura final foram avaliadas lado a lado. A build final preserva o cabeçalho, a barra lateral compacta, as abas de contas, os seletores 1/2/4, o mudo global e a ação de desacoplar. O jogo real substitui corretamente as cenas ilustrativas do mock sem introduzir áreas brancas ou chrome externo.

Confirmações visuais e funcionais:

- com uma única sessão e o modo quatro selecionado, a superfície ocupa toda a área útil em 1 × 1;
- o Reborn turco S115 chegou à interface jogável dentro do launcher;
- a janela maximizada coincidiu exatamente com a work area do monitor e não cobriu a barra de tarefas;
- desacoplar abriu a sessão em janela temática com perfil e servidor no título; reanexar devolveu o mesmo jogo ao workspace sem novo login;
- o áudio global permaneceu mudo durante a sessão;
- uma execução anterior confirmou duas sessões Flash simultâneas e isoladas no mesmo workspace até 99% de carregamento.

### P3 — amplitude da validação multissessão real

A captura final usa a única conta persistida disponível. Quatro autenticações reais simultâneas não foram repetidas nessa build; a composição 1/2/4, a progressão 1 × 1/1 × 2/2 × 2 e o isolamento por perfil estão cobertos por testes automatizados, e a execução anterior confirmou duas sessões reais simultâneas. Esta limitação de cobertura não deixa defeito visual ou funcional P0, P1 ou P2 aberto.

## Iteração 3 — acabamento responsivo e chrome final (15/07/2026)

### Referências desta iteração

- controles da janela desacoplada: captura local de referência (não versionada);
- workspace maximizado com duas sessões reais S115: captura local de referência (não versionada);
- recorte da barra compacta: captura local de referência (não versionada);
- recorte inferior do launcher responsivo: captura local de referência (não versionada).

A comparação visual usou `artifacts\publish-compact-workspace`. Depois dela, o refresh Win32 para mudanças de DPI/display/work area foi endurecido sem alterar o XAML; a mesma árvore foi recompilada e publicada em `artifacts\publish-compact-workspace-final`.

### Comparação da barra e da janela desacoplada

A barra final coincide com o recorte de referência em densidade e hierarquia: uma única linha de 44 px concentra voltar ao launcher, abas, `+ CONTA`, mudo, layouts 1/2/4 e desacoplar. Abas e controles têm 34 px; somente as abas rolam, `+ CONTA` permanece acessível fora do scroller e os 150 px finais ficam livres para minimizar, maximizar/restaurar e fechar. Assim, nenhuma ação entra sob o chrome mesmo com muitas sessões.

Na janela desacoplada, o cabeçalho de 48 px preserva respiro entre ícone/título, `SOM GLOBAL`, `ACOPLAR`, `ENCERRAR` e caption buttons. As ações têm 32 px, margens regulares e a superfície do jogo recebe afastamento 8×6. A comparação com o recorte confirmou ausência de colisão ou corte na largura mínima.

### Execução real, work area e ciclo da janela

- A build compacta final executou de fato uma sessão Reborn turco S115 até a interface jogável. A captura de referência com duas sessões registra a validação multissessão real feita na execução imediatamente anterior; as mudanças desta iteração ficaram restritas ao chrome e ao layout responsivo.
- O monitor media 3440×1440 e a work area disponível media exatamente 3440×1392. Maximizar ocupou 3440×1392 e parou acima da barra de tarefas.
- Minimizar e restaurar a janela principal foram testados; o tamanho normal/restaurado permaneceu limitado à work area considerando o DPI.
- Desacoplar, maximizar a janela desacoplada e acoplar novamente foram testados sobre a mesma sessão, sem novo login e sem perda do HWND incorporado.
- Mudanças de DPI, display ou work area atualizam os limites dinamicamente, inclusive se a janela estiver parada; o refresh é coalescido para evitar trabalho duplicado.
- Na janela principal 1420×820, mínimo 1180×700, o cabeçalho colapsa no workspace. No launcher, o setup pode rolar, mas status de compatibilidade, CTA e legenda continuam fixos e inteiros; o recorte inferior confirma que o botão e o texto não são cortados.

### Verificação final

- build Release: 0 erros e 0 avisos;
- suíte completa: 279/279 testes aprovados;
- contratos específicos: `GameWorkspaceXamlTests.cs`, `BorderlessWindowCommandsTests.cs` e `MainWindowLayoutXamlTests.cs` aprovados;
- minimizar, restaurar, desacoplar, maximizar e acoplar validados em execução real;
- nenhum defeito P0, P1 ou P2 permaneceu aberto.

final result: passed

## Iteração 4 — localização e seletores (15/07/2026)

A build de localização foi inspecionada em português brasileiro. A primeira captura revelou que o conteúdo selecionado usava a representação técnica de `LanguageOption`; `LanguageOption.ToString()` passou a devolver somente o nome nativo (`Português (Brasil)`, `English` ou `Español`).

O teste de interação também reproduziu o relato de que o seletor de idioma não abria de forma confiável. A correção aplica ao seletor do cabeçalho e ao seletor do workspace os mesmos handlers explícitos já usados pelo seletor de versões: clique, Enter, Espaço, F4 e Alt+Seta para baixo abrem `IsDropDownOpen`. Contratos XAML agora impedem que essa ligação seja removida por regressão.

Verificação desta iteração:

- build e publicação Release concluídas sem avisos;
- suíte completa: 331/331 testes aprovados;
- catálogos `pt-BR`, `en-US` e `es-ES` com 151 chaves, placeholders equivalentes e nenhuma referência ausente;
- todos os arquivos de código/XAML continuam abaixo de 800 linhas;
- a inspeção visual automatizada posterior foi interrompida pelo usuário antes do novo clique, portanto a confirmação visual final do popup permanece para o teste manual da build publicada.

final result: automated checks passed; manual popup confirmation pending

## Iteração 5 — pedido de apoio PayPal (15/07/2026)

O modal de apoio foi aberto em execução real na build `artifacts\publish-donation-final`. A inspeção confirmou que o botão PayPal permanece compacto no cabeçalho, fora dos 150 px de minimizar/maximizar/fechar, e que o overlay é exibido acima do launcher sem criar uma janela branca ou bloquear os caption buttons depois do fechamento.

### Acabamento e integridade visual

- o painel central usa cantos arredondados, fundo midnight e duas colunas com hierarquia clara entre explicação e forma de pagamento;
- o trilho diagonal de fragmentos/diamantes permanece discreto e não compete com a leitura;
- o QR aparece inteiro, quadrado e sem máscara dentro do painel branco com padding; nenhum canto da imagem é arredondado ou recortado;
- o conteúdo em `pt-BR`, `en-US` e `es-ES` troca ao vivo e preserva título, texto, instruções, fechamento e acessibilidade sem overflow;
- Escape, botão superior e **Agora não** fecham a camada, e a navegação por teclado permanece contida no modal;
- a composição foi aprovada visualmente em execução real, além dos contratos XAML.

### Verificação funcional desta etapa

- o botão do cabeçalho abre manualmente sem consultar o intervalo;
- o intervalo de cinco horas é avaliado somente na inicialização: não existe timer que abra o modal sobre um jogo após horas de uso;
- o JPEG possui 62.216 bytes e SHA-256 `EADCCECE3D8D2EC926C81AF0386A169178FA0795D6BADF7FC90794648601C6FC`;
- a leitura auditada do QR aponta para `https://www.paypal.com/qrcodes/p2pqrc/YBY4YD5EV8JJE`;
- a build Release terminou sem erros/avisos;
- antes do acréscimo do PIX, os três catálogos tinham 165 chaves equivalentes e a suíte completa passou com 343/343 testes.

final result: passed

## Iteração 6 — PIX Brasil (15/07/2026)

O mesmo modal passou a oferecer `PIX BRASIL · CNPJ`, chave selecionável `57.646.942/0001-69` e botão **COPIAR PIX**. Sucesso e falha de área de transferência possuem feedback e nomes de automação em português, inglês e espanhol; uma falha mantém a chave selecionável para cópia manual. O QR PayPal e seu hash não foram alterados.

Verificação automatizada pós-PIX:

- build e publicação Release concluídas;
- suíte completa: 345/345 testes aprovados;
- contratos dedicados do pedido de apoio: 14/14 aprovados;
- catálogos atuais `pt-BR`, `en-US` e `es-ES` com 180 chaves, paridade total e referências XAML válidas; as três chaves posteriores pertencem aos papéis e ao divisor da lista de servidores;
- teste dedicado confirma que o botão copia exatamente `57.646.942/0001-69`;
- contratos preservam bytes/hash/empacotamento e ausência de clip no QR;
- todos os arquivos de código e XAML permanecem abaixo de 800 linhas.

O artefato republicado corresponde aos binários Release auditados e contém o BAML do modal, o QR com hash exato e o bloco PIX. Não existe achado bloqueante de código.

A tentativa de inspeção visual final da composição já com PIX foi interrompida por Escape durante o controle do aplicativo. Nenhuma ação adicional foi executada depois disso. Assim, o QA visual real da etapa PayPal permanece aprovado, enquanto a confirmação manual específica do bloco PIX na build final fica pendente.

final result: automated checks passed; post-PIX manual confirmation pending

## Iteração 7 — identidade Urus e distribuição 1.0.0 (15/07/2026)

A identidade visível foi consolidada como **Urus Launcher**. O logo é um monograma abstrato “U”/portal, gerado originalmente com o imagegen integrado e tratado localmente para remover o chromakey; não usa touro, escudo nem trade dress de launcher, jogo ou fabricante. O PNG transparente passou a ser a fonte visual da janela principal e da janela desacoplada, e o ICO derivado atende shell, executável, instalador e atalhos. Os marks anteriores foram removidos depois da substituição e da busca de referências.

### Contratos de marca e localização

- `urus-logo.png`: 1024×1024, RGBA, 293.632 bytes, SHA-256 `F996ACD7388043908817DDE0A5363B4AD078047EBC9210E3C682DAC46BC2E493`;
- `urus-launcher.ico`: 52.816 bytes, sete entradas 32 bpp em 16, 24, 32, 48, 64, 128 e 256 px, SHA-256 `9404FFE30F9A899DBEF02CEBC8BA485A84D00956BF712D15A0F5337A7E8AB0ED`;
- título, cabeçalho, modal de apoio, GameHost, metadados, ícone e atalhos usam Urus Launcher/Urus GameHost;
- os slogans são “JOGUE DO SEU JEITO”, “PLAY YOUR WAY” e “JUEGA A TU MANERA”, mantendo a composição espaçada do cabeçalho;
- os marcadores públicos Next, preview, prévia técnica e teste foram removidos; “Legend Online” permanece somente como nome do jogo/plataforma;
- `UrusLauncher.App.exe` é o arquivo público; namespaces, projetos e diretório local legados continuam internos por compatibilidade;
- `BrandingAssetTests.cs` e os contratos de localização/layout validam assets, Pack URIs, metadados, textos e ausência dos marks antigos.

O PNG foi inspecionado visualmente sobre transparência e em miniatura; a leitura mostra o monograma sem borda cromática residual relevante. A confirmação manual da composição completa dentro da janela WPF rebatizada não foi repetida nesta etapa, portanto não é declarada como QA visual real aprovado.

### Pacotes Windows finais

O pipeline `scripts/build-urus-distribution.ps1` publicou App e GameHost separadamente como self-contained `win-x64`, preservou o payload WPF da App e incorporou somente os quatro arquivos próprios do GameHost. Depois validou runtimeconfig/deps, `WindowsBase.dll` e componentes WPF/WinForms, compilou o Inno Setup e gerou o ZIP portátil desse mesmo payload.

| Entregável | Tamanho | SHA-256 |
| --- | ---: | --- |
| `UrusLauncher-Setup-1.0.0-win-x64.exe` | 55.899.429 bytes | `9E26F4F87BA6DEE58895F2C097BB458A532E2F31A6F5B5CF2291215A0B79821F` |
| `UrusLauncher-1.0.0-portable-win-x64.zip` | 79.725.226 bytes | `CDF1EE6637AA03E68DDD599170DEEF2F7888686DE84F1AF250B03C03A9AF1D49` |

- payload expandido: `portable/UrusLauncher`, 468 arquivos e 183.600.532 bytes;
- App e GameHost passaram nas verificações self-contained; `WindowsBase.dll` final tem 2.177.328 bytes, assembly 10.0.0.0 e produto 10.0.2;
- um smoke sem runtime .NET global confirmou durante sete segundos processo responsivo, janela “Urus Launcher” e módulos WPF carregados;
- instalação e desinstalação silenciosas retornaram exit code 0/0 e não deixaram resíduo;
- as 468 entradas do ZIP foram lidas sem erro;
- `WindowsDistributionContractTests.cs` fixa nome, instalação per-user, idiomas, arquitetura, publish self-contained e arquivos de handoff;
- a suíte Release final passou com **358/358 testes**. Uma falha transitória anterior em `GameAudioServiceTests` não se repetiu no teste isolado nem na execução integral do pipeline;
- os binários não possuem assinatura Authenticode; hashes comprovam integridade, não autoria;
- o script mantém `artifacts/urus-distribution` como saída canônica e não escreve automaticamente em `Downloads`; o handoff explícito copiou instalador, ZIP, checksums e a pasta portátil para Downloads, com hashes conferidos depois da cópia.

final result: branding/package automated checks passed; full-window Urus visual confirmation pending

## Iteração 8 — hierarquia de servidores por perfil (15/07/2026)

A lista do catálogo passou a diferenciar duas informações que antes podiam ser confundidas. O servidor efetivamente usado por último no perfil selecionado fica fixado no topo e recebe **RECOMENDADO**; o servidor válido já aberto mais recentemente recebe **MAIS RECENTE**. Esse segundo papel é calculado pelo maior `StartTimeUtc`, com desempate pelo maior `NumericId`, e não pela recomendação genérica do payload OAS.

### Contrato de apresentação

- `RecentServerIds[0]`, gravado depois de uma abertura aceita, vence qualquer `LastServerId` divergente; este último permanece somente como fallback legado, sempre limitado à plataforma do perfil, e `catalog.Current` cobre a ausência dos dois;
- a linha seguinte ao item fixado inicia a seção **OUTROS SERVIDORES**, entre dois traços discretos;
- o lançamento mais recente aparece antes dos demais itens não fixados;
- um servidor que seja simultaneamente o último usado e o lançamento mais novo mostra os dois selos na mesma linha;
- a busca recalcula a seção para cada conjunto visível: sem o servidor fixado, ou com apenas um resultado, o divisor desaparece;
- os textos existem em português brasileiro, inglês e espanhol como **RECOMENDADO/RECOMMENDED**, **MAIS RECENTE/NEWEST/MÁS RECIENTE** e **OUTROS SERVIDORES/OTHER SERVERS/OTROS SERVIDORES**.

`ServerCatalogPresentationTests.cs`, `MainWindowViewModelTests.cs`, `ProfileStorageCoordinatorTests.cs`, `ServerRowLocalizationTests.cs` e `MainWindowLayoutXamlTests.cs` contêm os contratos automatizados correspondentes. A suíte Release passou com **364/364 testes**; os casos novos cobrem troca de perfil, histórico real contra fallback divergente, data futura/inválida, datas ausentes, coexistência dos selos e divisor filtrado.

Por solicitação expressa do usuário, não foi executado QA visual manual pelo agente nesta iteração. O usuário fará a conferência diretamente ao abrir o launcher e informará qualquer ajuste visual necessário.

final result: implementation and automated contracts passed; manual visual review delegated to the user

## Iteração 9 — distribuição Urus 1.0.1 (15/07/2026)

A versão 1.0.1 incorpora a hierarquia de servidores por perfil da iteração anterior e passa a ser a entrega Windows atual. App e GameHost foram publicados como self-contained `win-x64` no mesmo payload, o smoke do portátil permaneceu ativo e responsivo por sete segundos e o pipeline concluiu a composição do instalador Inno, ZIP, manifesto e checksums.

### Artefatos atuais

| Entregável | Tamanho | SHA-256 |
| --- | ---: | --- |
| `UrusLauncher-Setup-1.0.1-win-x64.exe` | 55.911.364 bytes | `8F4C60EA43A2F8C36D49A47812F537BB55F5FA69878F3B949AF2865E15EDB841` |
| `UrusLauncher-1.0.1-portable-win-x64.zip` | 77.366.737 bytes | `E255109957C64A105F03B4EC00CB1C9177D517F0E04CC1EE5441476C42EFA2FF` |

- payload expandido: `portable/UrusLauncher`, 468 arquivos e 183.601.556 bytes;
- o setup e `UrusLauncher-SHA256SUMS-1.0.1.txt` foram copiados explicitamente para `%USERPROFILE%\Downloads`;
- o SHA-256 do setup foi recalculado depois da cópia e coincide com o valor publicado;
- o ZIP permanece na saída canônica `artifacts/urus-distribution` e não foi incluído nesse handoff em Downloads.

### Compatibilidade e instalação

O cálculo de hashes do pipeline passou a abrir cada artefato como stream e usar `[System.Security.Cryptography.SHA256]::Create()`. Isso remove a dependência de `Get-FileHash` e mantém o empacotamento compatível com Windows PowerShell. Os dois contratos dedicados à alteração passaram em saída isolada (**2/2**).

Uma instalação silenciosa temporária do setup 1.0.1 materializou 470 arquivos. A inspeção não encontrou arquivos de perfil, conta ou senha no diretório instalado. A desinstalação silenciosa retornou exit code 0 e removeu o diretório por completo.

A suíte funcional completa passou com **364/364 testes antes da pequena correção de hashing**. Ela não foi repetida depois dessa alteração; somente os dois contratos diretamente relacionados foram executados após a correção. Não houve nova inspeção visual manual nesta iteração.

final result: 1.0.1 packaged, hashed, copied and silently install/uninstall validated; post-hash full suite not rerun
