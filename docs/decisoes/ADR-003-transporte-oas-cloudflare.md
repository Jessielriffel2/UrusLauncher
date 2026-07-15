# ADR-003 — Transporte compatível para entrada OAS

- **Status:** aceito
- **Data:** 2026-07-14

## Contexto

O Passport OAS atual aceita o `HttpClient` do .NET, mas a borda Cloudflare dos endpoints de entrada do jogo bloqueia essa assinatura TLS antes de a aplicação responder. Forçar IPv4, copiar cabeçalhos de navegador, trocar HTTP/1.1/HTTP/2 ou usar WinHTTP não removeu o bloqueio. O cliente oficial evita essa incompatibilidade usando Chromium, porém incorporar WebView2/CefSharp aumentaria significativamente o tamanho e a complexidade do launcher.

O `curl.exe` fornecido pelo Windows em `Environment.SystemDirectory` completou o mesmo fluxo autenticado do servidor Reborn turco S115. A senha continua pertencendo exclusivamente à etapa Passport; a ponte compatível recebe somente a URI/cookies transitórios necessários para resolver a página de entrada.

## Decisão

Usar dois transportes dentro de `OasAuthenticationService`:

1. Passport permanece no `HttpClient` endurecido, com handler e `CookieContainer` novos por tentativa.
2. Somente os GETs pós-Passport de `ResolveLaunchSessionAsync` usam `OasCurlLaunchTransport` no construtor de produção.

O transporte compatível:

- executa exclusivamente `%SystemRoot%\System32\curl.exe`, nunca um binário encontrado no `PATH`;
- passa URI, query e cabeçalho `Cookie` por configuração no `stdin`, nunca por argumentos de processo;
- desabilita configuração global do curl, força IPv4/HTTP 1.1 e restringe o protocolo a HTTPS;
- não segue redirects automaticamente; `OasOriginPolicy` continua validando cada salto;
- aceita somente hosts OAS permitidos, limita cabeçalhos/corpo/tempo e encerra a árvore no cancelamento;
- descarta `stderr` e devolve apenas erros locais saneados;
- reconstrói `HttpResponseMessage` preservando somente status, `Location`, `Set-Cookie` e corpo limitado.

O construtor com `HttpMessageHandler` injetável continua usando o transporte falso também na resolução da sessão, mantendo testes determinísticos sem rede/processos.

## Consequências

- O launcher permanece leve e não ganha runtime Chromium.
- A etapa bloqueada pela Cloudflare passa pela assinatura de rede que foi validada no S115.
- Há dependência operacional do curl incluído no Windows; ausência ou falha do binário vira erro de rede saneado.
- A decisão não garante que o Flash carregará para sempre: mudanças na borda OAS ainda exigem smoke test e podem levar a uma API oficial ou WebView2 no futuro.
- O Flash/GameHost continua isolado e recebe a sessão apenas pelo Named Pipe protegido; nenhuma credencial entra em linha de comando.

## Alternativas consideradas

- **Continuar ajustando `HttpClient`/WinHTTP:** rejeitado porque IPv4, TLS, HTTP e cabeçalhos já foram testados sem remover o 403.
- **WebView2/CefSharp:** reservado como fallback para challenges com JavaScript; adicionaria runtime, perfil/cookies e ciclo STA ao bootstrap.
- **Reaproveitar binários do cliente antigo:** rejeitado por falta de contrato, versionamento e isolamento.
- **Endpoint dedicado do operador OAS:** seria preferível, mas não está disponível ao projeto.
