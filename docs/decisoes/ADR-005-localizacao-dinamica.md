# ADR-005 — Localização dinâmica do launcher e do GameHost

- **Status:** aceito
- **Data:** 2026-07-15

## Contexto

O launcher precisa oferecer português brasileiro, inglês e espanhol sem criar builds separadas e sem exigir reinício ao trocar o idioma. A interface combina textos estáticos em quatro superfícies XAML, mensagens calculadas e persistidas em view models, menus criados no code-behind, milhares de linhas possíveis no catálogo, o pedido PayPal/PIX e janelas de jogo desacopladas já materializadas. O processo GameHost é WinForms separado e também possui mensagens próprias de preparação, diagnóstico e falha.

Uma tradução puramente estática não atualizaria janelas abertas. Guardar frases já traduzidas em campos também deixaria o último status no idioma anterior. Assinar um evento global em cada linha de servidor ampliaria o custo e criaria risco de retenção de milhares de objetos. Por fim, a cultura do processo .NET não é herdada automaticamente como preferência de UI por outro processo.

## Decisão

1. Suportar três culturas canônicas: `pt-BR`, `en-US` e `es-ES`. Entradas das famílias `pt`, `en` e `es` são normalizadas; entrada ausente ou não suportada usa `pt-BR`.
2. Manter paridade integral entre os três catálogos JSON incorporados ao assembly `LegendLauncher.App`; após o fluxo de atualização, cada catálogo possui 203 chaves. Arquivos externos não participam do runtime.
3. Usar uma única instância `LocalizationService.Current`, inicializada pelo `App` antes da janela principal. O serviço expõe indexador, `Get`, `Format`, cultura e eventos observáveis.
4. Textos estáticos WPF usam `LocalizeExtension`, que devolve um binding para o indexador. A troca emite `PropertyChanged("Item[]")`, atualizando a janela principal, workspace e janelas desacopladas existentes.
5. Textos de estado usam `LocalizedMessage` com chave e argumentos. O texto é resolvido no getter, permitindo reapresentar o mesmo estado na nova cultura.
6. `MainWindowViewModel` centraliza a seleção e, em `LanguageChanged`, notifica propriedades calculadas e chama `RefreshLocalization()` nas linhas do catálogo. Cada linha não assina o evento global individualmente.
7. O workspace compartilha o mesmo serviço, atualiza rótulo de mudo e rodapé e remove seu handler no descarte.
8. Menus montados por `MainWindow.xaml.cs` consultam o serviço no momento de abertura. Tooltips e `AutomationProperties.Name` também são localizados.
9. Persistir `languageCode` no `data\settings.json` existente por uma atualização independente e atômica. Settings antigos, corrompidos ou com idioma desconhecido usam o padrão sem impedir o jogo.
10. `LocalizationService` aplica `CurrentCulture`, `CurrentUICulture` e defaults de novas threads, para que formatação de número/data e novos processos usem a cultura selecionada.
11. Ao iniciar um GameHost, `LegacyGameRuntime` grava a cultura normalizada em `LEGEND_LAUNCHER_LANGUAGE` no ambiente do filho. O GameHost a aplica antes de criar qualquer mensagem e usa um catálogo interno fortemente enumerado.
12. O idioma não entra na linha de comando, no Named Pipe nem na `LaunchSession`. O GameHost em execução não é reiniciado após uma troca; seu conteúdo Flash permanece intacto, enquanto o chrome WPF muda imediatamente.
13. Traduzir somente texto pertencente ao projeto. Marca, dados da conta, nomes oficiais de plataformas/servidores e conteúdo entregue pelo jogo não são alterados.
14. Localizar integralmente o pedido de apoio, incluindo PayPal, PIX, feedback de área de transferência e acessibilidade. Marca PayPal, termo PIX, URL, hash e chave CNPJ permanecem invariantes.

## Consequências

- A pessoa pode alternar entre os três idiomas sem reiniciar o launcher ou fechar sessões.
- Todas as superfícies WPF compartilham a mesma fonte de verdade e janelas desacopladas recebem a atualização automaticamente.
- Status anteriores não ficam presos ao idioma em que foram criados.
- Catálogos incorporados tornam a distribuição autocontida e impedem substituição externa acidental, mas uma nova tradução exige nova build.
- Uma chave ausente usa português; ausência também no catálogo padrão produz `[Chave]`, tornando o defeito visível em vez de exibir vazio.
- As três traduções precisam manter chaves e placeholders equivalentes. Esse contrato deve ser verificado por teste sempre que o catálogo mudar.
- Atualizar milhares de linhas é uma operação explícita e rara; evita milhares de handlers permanentes, mas ainda deve ser considerada no QA com catálogos grandes.
- Rótulos em inglês e espanhol podem ser maiores. A barra compacta preserva textos curtos e tooltips completos, e o QA deve incluir largura/altura mínimas e DPI elevado.
- O GameHost recebe o idioma somente no início. Isso é suficiente para suas telas transitórias; reiniciá-lo apenas para mudar texto violaria o requisito de preservar a sessão.
- O modal de apoio já aberto também muda imediatamente entre `pt-BR`, `en-US` e `es-ES`; falha de cópia do PIX apresenta orientação local sem expor exceção técnica.

## Segurança e privacidade

- `languageCode` e `LEGEND_LAUNCHER_LANGUAGE` são não sensíveis e aceitam somente valores normalizados.
- Nenhum catálogo contém credencial, identificador de conta, token ou URI de sessão.
- Mensagens remotas são transformadas em chaves locais por código de erro; texto arbitrário de provider não é usado como catálogo.
- A propagação por ambiente evita ampliar o protocolo protegido e mantém URL/token fora de argumentos do processo.

## Alternativas consideradas

- **`x:Static`/recursos gerados sem binding:** rejeitado porque não atualiza objetos WPF já materializados.
- **Trocar `ResourceDictionary` global:** viável para XAML, mas insuficiente sozinho para mensagens calculadas e menos simples de testar fora da thread WPF.
- **Converter de tradução:** rejeitado porque a mudança global não altera necessariamente a origem do binding e o converter pode não ser reexecutado.
- **Reiniciar o launcher ao trocar idioma:** rejeitado porque derrubaria sessões e pioraria a experiência multissessão.
- **Assinar `LanguageChanged` em cada servidor:** rejeitado por fan-out e risco de retenção com catálogos grandes; o view model pai coordena o refresh.
- **Passar cultura por argumento do GameHost ou pelo Named Pipe:** rejeitado por ser desnecessário e ampliar contratos sensíveis. Uma variável de ambiente normalizada por processo é suficiente.
- **Traduzir nomes e o conteúdo Flash:** rejeitado porque esses dados pertencem à plataforma e não ao launcher.

## Referências

- [Localização](../modulos/localizacao.md)
- [Launcher App](../modulos/launcher-app.md)
- [Game Session Workspace](../modulos/game-session-workspace.md)
- [GameHost Legacy](../modulos/game-host-legacy.md)
- [Infrastructure](../modulos/infrastructure.md)
- [Pedido de Doação](../modulos/donation-prompt.md)
- [ADR-006 — Lembrete de doação não intrusivo](ADR-006-lembrete-doacao-nao-intrusivo.md)
