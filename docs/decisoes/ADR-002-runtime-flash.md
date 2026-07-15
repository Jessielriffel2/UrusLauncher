# ADR-002 — Estratégia para Flash e ponte de rede

- **Status:** aceito para spike
- **Data:** 2026-07-14

## Contexto

O jogo é carregado como SWF remoto. O cliente antigo usa Flash ActiveX 15 de 2014 por COM registration-free e um `H2Proxy.exe` não assinado com OpenSSL 1.1.1. WebView2 não executa Flash.

## Decisão

Ruffle será testado primeiro como destino de longo prazo. Compatibilidade não será presumida: ActionScript, sockets, teclado, áudio, resize, desempenho e uma sessão real precisam passar pelo roteiro de validação.

Se Ruffle falhar, o MVP usará o ActiveX já presente na máquina somente dentro de `GameHost.Legacy` x64. O host:

- não registra OCX globalmente nem exige administrador;
- não recebe senha, token ou URL pela linha de comando;
- recebe uma sessão por Named Pipe autenticado para o usuário e nonce de uso único;
- aceita apenas origens previamente permitidas;
- nunca é incorporado ao processo do launcher.

O proxy antigo servirá apenas como oracle de comportamento. A ponte nova será ligada a loopback, em porta efêmera, com allowlist explícita, sem endpoint de proxy aberto e com testes contra SSRF.

## Critério de remoção do legado

`H2Proxy.exe` e o caminho ActiveX só poderão ser removidos depois que Ruffle/ponte nova passarem pela mesma matriz de servidor, recursos, WebSocket, áudio e estabilidade.

## Limites

A redistribuição do OCX depende de licença. Até essa confirmação, o projeto apenas detecta uma cópia já instalada e não baixa Flash de terceiros.
