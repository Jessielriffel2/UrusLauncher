# Módulo Macro Assistant

## Objetivo

O módulo Macro Assistant integra o reconhecimento visual do `GemMacroAssistant` à interface do `LegendLauncher.App` sem mover o cursor físico do Windows. Cada sessão do workspace possui um controlador independente, uma mira/overlay e uma fila de ações. Isso permite manter o launcher, o jogo e as janelas desacopladas em execução enquanto o usuário continua usando o mouse normalmente.

A primeira versão contempla os módulos **Gemas/Cristais**, **Cosmo** e **Cliques**. A captura é feita somente na superfície da sessão; o motor Python continua responsável pela visão, stability frames, solver e regras do Cosmo. O modo Cliques não depende do worker Python: ele usa a mesma área selecionada e envia cliques diretos para a janela do jogo.

## Fluxo

1. `MacroAssistantCoordinator` acompanha `GameWorkspaceViewModel.Sessions`.
2. Cada `GameSessionViewModel` recebe um `MacroSessionController` independente.
3. **INICIAR MACRO** abre `MacroSetupWindow`; a bolinha compacta é o alvo visual e a moldura grande é opcional.
4. Gemas/Cristais e Cosmo mantêm uma região de análise segura, independente da bolinha; a região e o alvo são normalizados para a geometria atual.
5. No modo Gemas/Cristais ou Cosmo, **Play** fecha a configuração e inicia o reconhecimento.
6. `GameSurfaceCapture` captura a HWND validada do GameHost com `PrintWindow`/GDI, sem `ImageGrab` global.
7. `GemMacroBridgeClient` envia a imagem JPEG e o recorte selecionado para `gem_macro_assistant.worker` por stdin e recebe JSON com coordenadas locais.
8. `DirectGameInput` localiza a janela filha da sessão e envia `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`, `WM_LBUTTONUP` e, quando necessário, mensagens de tecla diretamente para a HWND.
9. No modo **Cliques**, o controlador usa a bolinha como ponto alvo e repete `WM_*` conforme quantidade/intervalo; `0` significa infinito. Velocidade, intervalo, alvo e região são salvos por perfil e modo.

O novo caminho não chama `SetCursorPos`, `mouse_event` ou `SendInput`. Não existe fallback automático para entrada global: se o Flash/ActiveX não aceitar a mensagem direta, a sessão exibe erro em vez de sequestrar o mouse do usuário.

## Dependências e caminho do motor

O launcher procura o projeto Python nesta ordem:

1. variável de ambiente `GEM_MACRO_ASSISTANT_ROOT`;
2. `GemMacroAssistant` ao lado do executável;
3. `%USERPROFILE%\\Documents\\GemMacroAssistant`.

O executável Python pode ser alterado com `GEM_MACRO_PYTHON`; o padrão é `python`. O caminho de desenvolvimento atual é:

```text
C:\Users\Jessiel\Documents\GemMacroAssistant
```

O motor precisa de Python, Pillow, OpenCV e NumPy. O empacotamento congelado do Electron existente é separado; a integração do launcher usa o worker Python por enquanto.

## Sessões e janelas

- O limite de layout continua sendo visual (1, 2 ou 4); o número de sessões executando não é limitado a quatro.
- Todas as sessões, inclusive as desacopladas, têm controle próprio.
- Ao mover, redimensionar, dividir ou reanexar uma janela, a próxima captura recalcula a geometria do cliente.
- Uma interrupção de superfície durante detach/reattach é tratada como estado transitório e o loop tenta novamente.
- Fechar a sessão, encerrar o launcher ou pressionar **PARAR MACRO** cancela o worker e oculta o overlay.

## Arquivos principais

| Arquivo | Responsabilidade |
| --- | --- |
| `src/LegendLauncher.App/MacroAssistant/MacroAssistantCoordinator.cs` | Cria e remove controladores conforme as sessões são adicionadas/removidas. |
| `MacroSetupWindow.cs` | Bolinha compacta, moldura opcional, Play, Parar, velocidade e parâmetros do modo Cliques. |
| `ProfileMacroPreferences.cs` | Modelo normalizado por perfil/modo para alvo, região, velocidade e cliques. |
| `ProfilePreferencesStore.cs` | Persistência atômica das preferências do Macro Assistant. |
| `MacroSessionController.cs` | Loop de captura, análise, cooldown, cancelamento, persistência e overlay por sessão. |
| `GemMacroBridgeClient.cs` | Processo Python persistente e protocolo JSON por linha. |
| `GameSurfaceCapture.cs` | Captura da HWND do jogo em JPEG. |
| `DirectGameInput.cs` | Mensagens Win32 direcionadas e coordenadas locais. |
| `MacroOverlayWindow.cs` | Overlay WPF click-through, visual e dependente da geometria da sessão. |
| `MacroAssistantViewModel.cs` | Comandosglobais de iniciar/parar, modo e rótulos localizados. |
| `C:\Users\Jessiel\Documents\GemMacroAssistant\gem_macro_assistant\worker.py` | Worker de visão que reutiliza os detectores/solvers existentes sem entrada global. |

## Validação manual necessária

O build e os testes de contrato não substituem o teste com o Flash real. Em uma sessão de teste, deve-se confirmar:

1. o botão **INICIAR MACRO** abre a bolinha compacta, sem iniciar o worker; ativar a moldura grande só quando desejado;
2. o clique de Gemas/Cristais ocorre no tabuleiro;
3. o clique de Cosmo ocorre no ponto visual;
4. no modo Cliques, quantidade `0` continua até **PARAR MACRO**;
5. o intervalo configurado é respeitado e o cursor físico não se move;
6. a operação continua após dividir em 2/4;
7. a operação continua ao desacoplar, mover e reacoplar a janela;
8. **PARAR MACRO** encerra o loop e fecha os overlays;
9. a entrada de texto/Enter do Cosmo é aceita pelo Flash; se não for, o launcher informa a limitação em vez de mover o mouse.

## Testes

- `MacroAssistantContractTests.cs` verifica bindings da UI e ausência das APIs de mouse global no novo backend.
- `tests/test_worker.py` verifica o protocolo de imagem, coordenadas locais e ação do Cosmo.
- A suíte Python completa depende de capturas temporárias externas não presentes em todos os ambientes; falhas apenas por `FileNotFoundError` nessas amostras são pré-existentes e não indicam regressão do worker.
