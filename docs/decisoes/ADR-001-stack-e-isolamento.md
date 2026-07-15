# ADR-001 — Stack nativa e isolamento do jogo

- **Status:** aceito
- **Data:** 2026-07-14

## Contexto

O cliente de referência é um executável C++/Qt 5 x64 sem código-fonte. Ele combina interface, login WebView2, Flash ActiveX e um proxy de rede no mesmo produto. A meta é uma interface moderna, manutenção previsível, múltiplas contas e consumo menor que o cliente oficial.

## Decisão

O launcher será implementado em C#/.NET 10 com WPF e tema Fluent. A execução do jogo ficará em processo x64 separado atrás de `IGameRuntime`. Segredos serão armazenados pelo Windows Credential Manager e nunca em JSON, Registro próprio, argumentos de processo ou logs.

O fluxo fica dividido em:

1. `Launcher.App`: interface e coordenação.
2. `Launcher.Core`: modelos e contratos sem dependência de UI/rede.
3. `Launcher.Infrastructure`: persistência, cofre e integração Windows.
4. Providers: adapters de catálogo e autenticação por plataforma.
5. `GameHost.Legacy`: compatibilidade temporária com ActiveX, se necessária.
6. `NetworkBridge`: substituição gerenciada e restrita do proxy antigo.

## Consequências

- O launcher permanece nativo e mais leve que Electron, sem empacotar Chromium e Node.
- A dependência Windows é aceita porque o runtime legado já é ActiveX exclusivo do Windows.
- Falhas do jogo não derrubam a interface principal.
- Login e runtime podem evoluir sem reescrever a tela.
- A distribuição inicial será x64 e descompactada; instalador e assinatura virão após o spike funcional.

## Alternativas consideradas

- **Electron:** boa velocidade de UI, mas consumo e tamanho maiores sem vantagem para ActiveX.
- **WinUI 3:** visual moderno, porém adiciona Windows App SDK e complexidade de implantação.
- **Avalonia:** excelente para multiplataforma, benefício inexistente enquanto o jogo depender de ActiveX.
- **Reescrever em Qt:** tecnicamente possível, mas não melhora a manutenção nem a segurança do runtime legado.
