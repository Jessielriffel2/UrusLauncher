# ADR-006 — Lembrete de doação não intrusivo

- **Status:** aceito
- **Data:** 2026-07-15

## Contexto

O projeto precisa oferecer uma forma voluntária de apoio sem transformar o launcher em publicidade recorrente, interromper uma sessão ou depender de infraestrutura remota. A pessoa deve poder doar por PayPal com o QR fornecido ou, no Brasil, copiar uma chave PIX CNPJ. O pedido precisa respeitar os três idiomas do produto, o chrome compacto e a integridade visual/técnica do QR.

Um lembrete baseado em timer enquanto o programa permanece aberto seria disruptivo: poderia surgir sobre o jogo depois de horas de sessão. Exibir em toda abertura também puniria quem reinicia o launcher com frequência. Alterar ou gerar novamente o QR acrescentaria risco de corrupção ou troca do destino.

## Decisão

1. Avaliar o lembrete automático somente uma vez durante a inicialização de cada abertura do launcher.
2. Usar intervalo de cinco horas entre exibições. Ausência de timestamp, diferença maior ou igual a cinco horas ou timestamp futuro abre o modal; uma diferença menor não abre.
3. Não criar timer de cinco horas. Se o launcher permanecer aberto por 12 horas, nenhum novo modal automático aparece; a regra volta a ser avaliada na próxima abertura.
4. Persistir `lastDonationPromptUtc` no `settings.json` no instante em que o modal é efetivamente exibido, tanto automática quanto manualmente. Fechar o modal não renova o horário.
5. Manter um botão PayPal compacto no cabeçalho para abertura manual a qualquer momento, independentemente do intervalo.
6. Usar overlay modal WPF como último filho visual e com Z-index explícito, sem nova janela, navegador incorporado ou interferência em sessões do GameHost.
7. Apresentar o conteúdo num painel arredondado em duas colunas, com uma assinatura discreta de fragmentos/diamantes e espaço reservado aos caption buttons.
8. Empacotar o JPEG fornecido como recurso WPF sem qualquer transformação. Exibi-lo quadrado, uniforme, sem clip/máscara, em painel branco com padding; o arredondamento não é aplicado à imagem.
9. Fixar o QR pelo SHA-256 `EADCCECE3D8D2EC926C81AF0386A169178FA0795D6BADF7FC90794648601C6FC`, 62.216 bytes. O conteúdo auditado aponta para `https://www.paypal.com/qrcodes/p2pqrc/YBY4YD5EV8JJE`.
10. Oferecer PIX Brasil com chave do tipo CNPJ `57.646.942/0001-69` e ação local de copiar. A chave copiada deve ser exatamente a exibida.
11. Localizar título, descrição, brincadeira dos diamantes, instruções, feedback PIX, fechamento e acessibilidade em `pt-BR`, `en-US` e `es-ES`. PayPal, PIX, URL e CNPJ permanecem invariantes.
12. Permitir Escape, Tab cíclico, foco inicial previsível, **Agora não**, fechamento superior e nomes de automação localizados.
13. Tratar falha recuperável de persistência ou área de transferência como não fatal. Login, catálogo e jogo continuam disponíveis.

## Consequências

- O pedido reaparece com cadência limitada sem interromper uma sessão longa.
- A entrada manual continua descoberta no cabeçalho, mas ocupa apenas um controle compacto e preserva os 150 px finais do chrome.
- PayPal atende o fluxo por QR e o PIX oferece um caminho direto para usuários do Brasil.
- Registrar a exibição, e não a dispensa, impede que fechar/reabrir o mesmo modal prolongue involuntariamente a cadência.
- Um relógio local movido para o futuro é tratado como anomalia: o modal aparece e o timestamp é reparado para o instante atual.
- O estado acrescenta somente um timestamp UTC não sensível ao settings existente.
- O QR estático torna a distribuição auditável; uma mudança legítima de destino exige substituir o asset e atualizar hash, testes e documentação na mesma tarefa.
- As traduções aumentam o catálogo compartilhado e permanecem sujeitas aos contratos de paridade e referências XAML.

## Segurança e privacidade

- Nenhuma credencial, login, UID, cookie, token, URI de sessão ou dado do jogo entra no modal ou no timestamp.
- O launcher não envia telemetria ao exibir, fechar ou copiar. O botão de copiar escreve somente o CNPJ conhecido na área de transferência local.
- O recurso não navega automaticamente para a URL decodificada e não faz requisição remota para obter o QR.
- Hash e tamanho são validados em teste para detectar substituição, reencodificação ou corrupção do JPEG.
- A chave PIX e a URL PayPal são dados públicos de destino; ainda assim, qualquer alteração exige revisão explícita.

## Alternativas consideradas

- **Exibir em toda abertura:** rejeitado por ser insistente, especialmente durante testes e reinícios frequentes.
- **Timer recorrente a cada cinco horas:** rejeitado porque poderia sobrepor uma sessão em andamento e transformar tempo aberto em interrupção.
- **Contar cinco horas desde a dispensa:** rejeitado porque ações diferentes de fechar poderiam produzir cadências inconsistentes; a exibição é o evento estável.
- **Abrir PayPal automaticamente no navegador:** rejeitado por ser surpreendente e ampliar a superfície externa; o QR e a abertura manual do modal são suficientes.
- **Baixar o QR em runtime:** rejeitado por depender de rede e permitir mudança não auditada do conteúdo.
- **Arredondar ou regenerar o QR:** rejeitado pelo risco de prejudicar leitura e por violar a preservação do arquivo fornecido.
- **Persistir o estado por perfil:** rejeitado porque a solicitação pertence ao launcher, não às contas, e multiplicaria o lembrete em uso multissessão.
- **Mostrar apenas PayPal ou apenas PIX:** rejeitado por reduzir acessibilidade de pagamento para parte da comunidade.

## Referências

- [Módulo Pedido de Doação](../modulos/donation-prompt.md)
- [Launcher App](../modulos/launcher-app.md)
- [Localização](../modulos/localizacao.md)
- [Infrastructure](../modulos/infrastructure.md)
- [Design QA](../../design-qa.md)
