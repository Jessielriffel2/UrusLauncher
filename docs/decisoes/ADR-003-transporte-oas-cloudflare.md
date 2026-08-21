# ADR-003 — Transporte compatível para entrada OAS

- **Status:** aceito
- **Data:** 2026-07-14; ampliado em 2026-07-26; ampliado em 2026-08-20

## Contexto

O Passport OAS Games atual aceita o `HttpClient` do .NET, mas a borda Cloudflare dos endpoints de entrada do jogo bloqueia essa assinatura TLS antes de a aplicação responder. Em 26/07/2026, o mesmo padrão passou a ocorrer no Passport Creaction usado pelo Reborn: prova anônima, sem query ou credencial, recebeu `403` no .NET e `200` no curl do Windows. O Passport OAS Games permaneceu `200` nos dois transportes. Forçar IPv4, copiar cabeçalhos de navegador, trocar HTTP/1.1/HTTP/2 ou usar WinHTTP não removeu o bloqueio anterior da entrada.

O `curl.exe` fornecido pelo Windows em `Environment.SystemDirectory` completou o mesmo fluxo autenticado do servidor Reborn turco S115. A senha continua pertencendo exclusivamente à etapa Passport: no Creaction, a URI dessa etapa entra no processo confiável somente pela configuração em `stdin`; o wrapper pós-login recebe apenas URI/cookies transitórios.

Em 20/08/2026, a mesma prova anônima passou a observar `403` também no Passport OAS Games pelo stack .NET (continuando `200` no curl do Windows). A borda Cloudflare passou a bloquear a assinatura TLS do `HttpClient` em ambos os Passports, e não apenas na entrada pós-login.

## Decisão

Usar seleção explícita dentro de `OasAuthenticationService`:

1. Todos os Passports OAS (Creaction/Reborn e OAS Games) usam `OasCurlPassportTransport`, com `Accept: application/json`, sem cookies de entrada e query limitada exatamente a `m=login`, `email` e `pwd` na ordem canônica. O wrapper aceita somente os dois host/path exatos `passport.creaction-network.com/index.php` e `passport.oasgames.com/index.php`.
2. GETs pós-Passport continuam usando `OasCurlLaunchTransport`.
3. Ambos os wrappers delegam execução a `OasSystemCurlTransport`, que não possui autoridade sobre destinos; cada wrapper aplica sua própria allowlist antes de chamar o executor.

O transporte compatível:

- executa exclusivamente `%SystemRoot%\System32\curl.exe`, nunca um binário encontrado no `PATH`;
- passa URI/query e, somente no launch, cabeçalho `Cookie` por configuração no `stdin`, nunca por argumentos de processo;
- desabilita configuração global do curl, força IPv4/HTTP 1.1 e restringe o protocolo a HTTPS;
- não segue redirects automaticamente; `OasOriginPolicy` continua validando cada salto;
- aceita somente hosts OAS permitidos, limita cabeçalhos/corpo/tempo e encerra a árvore no cancelamento;
- descarta `stderr` e devolve apenas erros locais saneados;
- reconstrói `HttpResponseMessage` preservando somente status, `Location`, `Set-Cookie` e corpo limitado.
- produz diagnóstico apenas com fase, transporte e status HTTP; nenhum texto remoto ou endereço cabe nesse contrato.

O construtor com `HttpMessageHandler` injetável continua usando o transporte falso também na resolução da sessão, mantendo testes determinísticos sem rede/processos.

## Consequências

- O launcher permanece leve e não ganha runtime Chromium.
- As etapas bloqueadas pela Cloudflare passam pela assinatura de rede validada anonimamente; login real permanece ação manual do usuário.
- Há dependência operacional do curl incluído no Windows; ausência ou falha do binário vira erro de rede saneado.
- A decisão não garante que o Flash carregará para sempre: mudanças na borda OAS ainda exigem smoke test e podem levar a uma API oficial ou WebView2 no futuro.
- O Flash/GameHost continua isolado e recebe a sessão apenas pelo Named Pipe protegido; nenhuma credencial entra em linha de comando.

## Alternativas consideradas

- **Continuar ajustando `HttpClient`/WinHTTP:** rejeitado porque IPv4, TLS, HTTP e cabeçalhos já foram testados sem remover o 403.
- **WebView2/CefSharp:** reservado como fallback para challenges com JavaScript; adicionaria runtime, perfil/cookies e ciclo STA ao bootstrap.
- **Reaproveitar binários do cliente antigo:** rejeitado por falta de contrato, versionamento e isolamento.
- **Endpoint dedicado do operador OAS:** seria preferível, mas não está disponível ao projeto.
