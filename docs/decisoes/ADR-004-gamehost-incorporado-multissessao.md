# ADR-004 — GameHost incorporado e workspace multissessão

- **Status:** aceito
- **Data:** 2026-07-15

## Contexto

O launcher precisa manter várias contas abertas com troca rápida, sem repetir o peso e a instabilidade do cliente oficial. O ActiveX do Flash exige Windows/STA e já foi isolado em um GameHost x64 descartável. Abrir cada GameHost como janela superior independente preservaria o isolamento, mas fragmentaria a experiência; carregar todos os ActiveX dentro do processo WPF eliminaria a principal fronteira contra falhas do runtime legado.

O Windows permite compor visualmente uma janela de outro processo com `SetParent`, porém o `HwndHost` deve devolver um HWND pertencente à thread/processo WPF. Também é necessário impedir que um handle recebido por IPC seja usado sem validar sua associação ao processo filho esperado.

## Decisão

Adotar um workspace multissessão com as seguintes regras:

1. Cada sessão executa em um processo `LegendLauncher.GameHost.Legacy` próprio. O ActiveX e o activation context nunca são carregados pelo WPF.
2. O handshake do GameHost devolve o HWND por resposta binária versionada, sem relação com o transporte HTTP. O runtime valida pelo kernel que o HWND pertence ao PID iniciado e expõe `GameSession` com PID, handle e instante UTC.
3. A App valida novamente HWND/PID e usa um HWND-proxy criado no processo WPF. O proxy é devolvido ao `HwndHost`; a janela GameHost é um descendente cross-process com estilos de filho, sem chrome standalone enquanto incorporada.
4. `GameWindowAttachment` torna a associação reversível, preserva o estilo original, redimensiona para o client rect e impede um presenter antigo de desanexar uma janela já movida.
5. O workspace rastreia todas as sessões em abas. Layouts 1, 2 e 4 limitam somente quantas superfícies aparecem simultaneamente; não limitam o número de contas em execução. Na grade, uma sessão usa 1×1, duas usam 1×2 e três ou quatro usam 2×2.
6. Há no máximo uma sessão em execução por perfil. Nova tentativa com o mesmo perfil seleciona a aba existente.
7. Desacoplar move a mesma janela/processo para outra janela WPF. Fechar essa janela acopla novamente no máximo uma vez; criação/exibição falha executa cleanup e rollback, e shutdown suprime reattach e tenta fechar todas as janelas.
8. O áudio é controlado globalmente para os PIDs GameHost registrados via Core Audio. O padrão é mudo e a preferência vale também para sessões desacopladas. Falhas recuperáveis são best effort e o descarte aguarda callbacks do timer já iniciados.
9. Mudo, layout e último perfil selecionado são persistidos atomicamente em `data\settings.json`; o mesmo documento recebe depois a cultura não sensível definida pela [ADR-005](ADR-005-localizacao-dinamica.md). Nenhuma credencial ou sessão entra nesse documento.
10. Fechar uma sessão encerra somente seu processo; fechar o launcher encerra todas as sessões rastreadas. Cada GameHost também observa o PID pai e termina se o launcher desaparecer.
11. `SessionLaunchOutcome` devolve o `EffectiveProfile` efetivamente persistido. Se uma falha ocorrer depois de iniciar o runtime, ou se o workspace não adotar o PID/HWND retornado, a App encerra o processo não adotado.
12. Eventos de saída de processo são redirecionados para a `Dispatcher` WPF antes de modificar propriedades ou coleções observáveis.
13. Janelas de chrome próprio tratam `WM_GETMINMAXINFO` e maximizam pela work area do monitor atual, preservando a barra de tarefas. O tamanho normal/restaurado é recalculado em DIPs quando monitor, estado ou posição mudam e limitado à work area, sem perder os mínimos originais em monitores maiores. `WM_SETTINGCHANGE`, `WM_DISPLAYCHANGE` e `WM_DPICHANGED` também enfileiram uma atualização coalescida, inclusive com a janela parada.
14. O workspace usa uma única barra de 44 px. Abas e controles ocupam 34 px; as abas rolam horizontalmente, `+ CONTA` fica fora da rolagem e permanece acessível, e a coluna final reserva 150 px para os três caption buttons compartilhados da janela principal.
15. A janela principal abre em 1420×820, com mínimo 1180×700. Seu cabeçalho de 96 px existe apenas no launcher e colapsa no workspace; os caption buttons atravessam as duas superfícies. Na coluna de sessão, o setup é rolável, enquanto status de compatibilidade, CTA e legenda ficam ancorados. As janelas desacopladas mantêm cabeçalho próprio de 48 px e espaçamento entre título, ações e botões da janela.
16. Minimize, maximize/restore e o glifo da legenda são centralizados em `BorderlessWindowCommands`, compartilhado pela janela principal e pela desacoplada.

## Consequências

- Várias contas podem permanecer ativas e ser alternadas sem novo login, mantendo isolamento de processo por sessão.
- A UI apresenta uma experiência integrada, mas o reparenting Win32 continua exclusivo do Windows e exige cuidados com foco, resize, DPI e ciclo de vida de handles.
- Layout quatro não é um limite de processos; sessões adicionais continuam consumindo CPU/memória até serem encerradas.
- A falha de um GameHost remove sua aba sem invalidar as demais. O processo WPF continua independente, mas essa separação não constitui sandbox de segurança.
- O mudo por PID não altera outros aplicativos. Como sessões de áudio podem surgir depois do processo, o estado é reaplicado periodicamente.
- Settings corrompidos ou não graváveis não impedem o jogo: defaults ou estado em memória são usados.
- GameHosts sem pai e sessões iniciadas que não chegam a ser adotadas são encerrados, reduzindo processos órfãos em falhas e shutdown abrupto.
- A maximização é consistente em múltiplos monitores e não cobre a barra de tarefas, ao custo de uma integração Win32 específica para janelas borderless.
- A barra compacta preserva espaço horizontal previsível para o chrome e mantém `+ CONTA` acessível mesmo com muitas abas; a área de títulos é a única parte que rola.
- Em alturas menores, os campos de setup podem exigir rolagem, mas o estado de compatibilidade, a ação de entrar/voltar ao jogo e sua legenda permanecem visíveis.
- O clamp normal/restaurado evita que dimensões persistidas ou movidas de um monitor/DPI maior ultrapassem a work area atual; em monitores maiores, os mínimos declarados pela janela são restaurados.
- Compartilhar `BorderlessWindowCommands` mantém comportamento e glifos coerentes entre a janela principal e as desacopladas.

## Segurança

- A linha de comando do GameHost continua sem URL, token, senha ou sessão.
- O HWND é tratado como identificador opaco e validado contra o PID esperado tanto no runtime quanto antes da incorporação.
- O proxy precisa pertencer ao processo do launcher; `DetachIfParent` só atua quando ele ainda é o pai atual.
- Settings não armazenam identidade secreta, tokens, cookies, URLs autenticadas, PID ou HWND.
- O ActiveX permanece registration-free e restrito ao processo GameHost.
- O monitor do processo pai recebe apenas o PID não sensível já presente na linha de comando interna e não amplia o conteúdo do IPC.

## Alternativas consideradas

- **Hospedar ActiveX diretamente no WPF:** rejeitado porque remove o isolamento de falha e mistura múltiplas instâncias legadas com a UI principal.
- **Manter todas as sessões como janelas superiores independentes:** rejeitado como experiência principal; continua disponível de forma controlada pelo detach.
- **Capturar frames e reenviar input:** rejeitado por latência, complexidade, acessibilidade e maior superfície de erro.
- **Limitar o produto a uma única sessão:** rejeitado porque múltiplas contas são requisito central.
- **Substituir imediatamente por Chromium/Ruffle:** não resolve o ActiveX atual sem nova validação de compatibilidade; permanece caminho futuro.

## Referências

- [Game Session Workspace](../modulos/game-session-workspace.md)
- [Launcher App](../modulos/launcher-app.md)
- [GameHost Legacy](../modulos/game-host-legacy.md)
- [Core](../modulos/core.md)
- [Localização dinâmica](ADR-005-localizacao-dinamica.md)
