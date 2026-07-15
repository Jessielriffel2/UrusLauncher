# Módulo Pedido de Doação

## Objetivo do módulo

O módulo de pedido de doação oferece uma forma opcional e não intrusiva de apoiar o Urus Launcher. Ele mostra um modal somente quando a abertura do programa satisfaz o intervalo de cinco horas, mantém uma entrada manual pelo botão PayPal do cabeçalho e disponibiliza duas formas de apoio: QR do PayPal e PIX Brasil por chave CNPJ copiável.

O recurso não interfere em login, catálogo ou sessões abertas. Permanecer com o launcher aberto não dispara novos pedidos: o intervalo é avaliado uma única vez durante `InitializeAsync()`. Fechar o modal não encerra jogos e a ação manual continua disponível a qualquer momento.

## Arquivos, classes e funções principais

| Referência aproximada | Tipo/função | Responsabilidade, entrada e saída |
| --- | --- | --- |
| `src/LegendLauncher.App/MainWindow.xaml:71` | `DonationHeaderButton` | Botão PayPal compacto no cabeçalho. Executa `OpenDonationPromptCommand`, permanece clicável no chrome próprio e não invade os 150 px reservados aos caption buttons. |
| `src/LegendLauncher.App/MainWindow.xaml:744` | `DonationPromptOverlay` | Instância o modal como último filho visual da grade raiz, com `Panel.ZIndex=500` e visibilidade ligada a `IsDonationPromptVisible`. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml:1` | `DonationPromptView` | Overlay temático acessível, painel arredondado em duas colunas, conteúdo localizado, QR em painel branco, PIX copiável e ações de fechamento. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml:14` | Atalho Escape | Liga Escape diretamente a `CloseDonationPromptCommand`; a navegação por Tab permanece cíclica dentro do modal. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml:205` | Painel PayPal/QR | Exibe o JPEG original em 300×300, `Stretch=Uniform`, sem clip ou máscara e com `NearestNeighbor`, dentro de uma área branca com 14 px de padding. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml:263` | `PixDonationPanel` | Exibe CNPJ selecionável, ação copiar, orientação para área de transferência e feedback localizado de sucesso/falha. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml.cs:9` | `PixCnpj` / `CopyPixCnpj(...)` | Mantém a constante pública ao módulo e entrega exatamente `57.646.942/0001-69` ao writer recebido; o handler de UI começa na linha 23. |
| `src/LegendLauncher.App/Views/Donation/DonationPromptView.xaml.cs:42` | Foco e reset do modal | Ao tornar a superfície visível, oculta feedback PIX anterior e agenda foco no botão de dispensar para teclado/leitor de tela. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Donation.cs:8` | `DonationPromptInterval` | Intervalo fixo de cinco horas usado somente na avaliação da abertura. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Donation.cs:13` | Estado e comandos do modal | Expõe `IsDonationPromptVisible`, abertura assíncrona manual e fechamento local. Não conhece QR, CNPJ, credenciais ou área de transferência. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Donation.cs:23` | `EvaluateDonationPromptOnOpeningAsync(...)` | Recebe o snapshot de settings, avalia uma vez por instância e abre/persiste o horário se nunca foi exibido, se passaram pelo menos cinco horas ou se o relógio salvo está no futuro. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Donation.cs:42` | `OpenDonationPromptAsync()` | Abre manualmente sem consultar intervalo e grava o instante da exibição. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Donation.cs:51` | `TryPersistDonationPromptShownAsync(...)` | Persiste UTC; falhas recuperáveis de I/O não tornam o modal nem o launcher inutilizáveis. |
| `src/LegendLauncher.App/ViewModels/MainWindowViewModel.Donation.cs:66` | `ShouldShowDonationPrompt(...)` | Função determinística: `null`, cinco horas ou mais e timestamp futuro exibem; menos de cinco horas não exibem. |
| `src/LegendLauncher.App/Services/LauncherSettingsService.cs:7` | `LauncherSettingsSnapshot` | Inclui `LastDonationPromptUtc` não sensível junto das demais preferências. |
| `src/LegendLauncher.App/Services/LauncherSettingsService.cs:96` | `SaveDonationPromptShownAsync(...)` | Converte para UTC e atualiza somente o horário do pedido por leitura-modificação-gravação atômica. |
| `src/LegendLauncher.App/Assets/paypal-donation-qr.jpeg` | QR PayPal | Recurso WPF binário copiado sem transformação; 62.216 bytes e SHA-256 fixo. |
| `src/LegendLauncher.App/Localization/Resources/*.json:156` | Textos de apoio | 26 chaves `Donation_*` dentro dos 203 textos de cada catálogo: título, explicação, brincadeira dos diamantes, instruções PayPal/PIX, feedback de cópia, fechamento e automação em `pt-BR`, `en-US` e `es-ES`. |

## Fluxo automático e manual

1. Na abertura, `MainWindowViewModel.InitializeAsync()` carrega `LastDonationPromptUtc` do mesmo snapshot usado pelas outras preferências.
2. A avaliação acontece no máximo uma vez naquela instância do launcher. Ausência de data, intervalo maior ou igual a cinco horas ou data futura abre o modal e registra o horário corrente em UTC.
3. Se a última exibição ocorreu há menos de cinco horas, nada aparece automaticamente. Deixar o programa aberto por mais de cinco horas não inicia um temporizador nem interrompe uma sessão.
4. Em qualquer momento no launcher, o botão PayPal do cabeçalho abre manualmente o modal e registra essa exibição, mesmo dentro do intervalo.
5. Escape, o botão superior de fechar e **Agora não** apenas ocultam a superfície; fechar não altera novamente o horário.
6. A pessoa pode escanear o QR PayPal ou copiar a chave PIX CNPJ `57.646.942/0001-69`. Copiar afeta somente a área de transferência local e apresenta feedback localizado.

## Integridade do QR e PIX

O arquivo `paypal-donation-qr.jpeg` possui exatamente 62.216 bytes e SHA-256 `EADCCECE3D8D2EC926C81AF0386A169178FA0795D6BADF7FC90794648601C6FC`. Seu conteúdo decodifica para `https://www.paypal.com/qrcodes/p2pqrc/YBY4YD5EV8JJE`. A aplicação não arredonda, recorta, reencoda, aplica máscara ou altera opacidade da imagem; o arredondamento pertence somente ao painel branco externo.

A chave PIX é texto explícito do tipo CNPJ: `57.646.942/0001-69`. O botão copia exatamente esse valor, sem formatação adicional, consulta remota ou inclusão de dados da conta do jogador.

## Localização e acessibilidade

Todos os textos controlados pelo modal usam os catálogos incorporados `pt-BR`, `en-US` e `es-ES`; marcas, URL, CNPJ e imagem não são traduzidos. Tooltips, feedback da cópia, nome do diálogo, QR e ações possuem nomes de automação localizados. O foco entra no botão de dispensa, Tab circula entre controles e Escape fecha o modal.

## Dependências e consumidores

- A [Launcher App](launcher-app.md) hospeda o botão e o overlay e fornece os comandos do view model.
- [Infrastructure](infrastructure.md) fornece a primitiva JSON atômica usada pelo settings; o timestamp é não sensível.
- [Localização](localizacao.md) atualiza o modal aberto sem reinício.
- O módulo não referencia providers, credenciais, sessão autenticada, GameHost ou conteúdo Flash.
- A decisão de produto e suas alternativas estão em [ADR-006](../decisoes/ADR-006-lembrete-doacao-nao-intrusivo.md).

## Testes e validação

- `tests/LegendLauncher.Tests/App/DonationPromptTests.cs:12` cobre primeira abertura, antes/exatamente/depois de cinco horas, timestamp futuro, abertura manual, fechamento e avaliação única por abertura.
- `tests/LegendLauncher.Tests/App/DonationPromptAssetTests.cs:18` fixa bytes/hash/empacotamento do QR; os casos seguintes fixam painel branco sem clip, entrada manual, overlay no topo, Escape, foco cíclico e ações de fechamento. A partir da linha 124, o contrato exige painel PIX acessível, chave selecionável, feedback localizado e cópia exata do CNPJ.
- Os testes de settings verificam que timestamp, idioma, perfil, mudo e layout sobrevivem a atualizações independentes.
- Os contratos de localização verificam as 203 chaves e referências XAML nos três idiomas.
- Os contratos `DonationPromptTests` e `DonationPromptAssetTests` somam 14/14 casos aprovados. Na etapa específica do PIX, a suíte completa possuía 345 testes; depois dos contratos de branding/distribuição, a contagem final é **358/358**. O QA visual real do modal PayPal e a pendência de confirmação manual específica do bloco PIX estão registrados em [`design-qa.md`](../../design-qa.md).
