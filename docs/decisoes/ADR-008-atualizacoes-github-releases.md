# ADR-008 — Atualizações opcionais pelo GitHub Releases

- **Status:** aceito
- **Data:** 2026-07-15

## Contexto

O Urus Launcher será distribuído publicamente e precisa avisar sobre novas versões sem exigir que cada usuário acompanhe manualmente o repositório. A preparação da atualização não pode interromper contas em jogo nem depender de um servidor próprio ou de um token secreto dentro do aplicativo. O download antecipado é aceitável quando o arquivo passa pelo contrato estrito do release; executar o instalador continua exigindo consentimento explícito. As mudanças também precisam aparecer como patch notes em português brasileiro, inglês e espanhol.

O instalador Inno Setup já usa `AppId` e diretório por usuário estáveis, portanto uma versão nova pode substituir a anterior preservando perfis, settings, cache e credenciais, que ficam fora da pasta de instalação.

## Decisão

1. Usar o GitHub Releases público do repositório fixo `Jessielriffel2/UrusLauncher` como canal estável de atualização.
2. Consultar `releases/latest` cedo uma vez em cada abertura, logo depois das preferências e antes de perfis/catálogo, de forma assíncrona e não bloqueante. Se, e somente se, a API responder `403` ou `429`, tentar o manifesto em `https://github.com/Jessielriffel2/UrusLauncher/releases/latest/download/update-manifest.json`. Não manter timer nem polling durante o jogo.
3. Mostrar o estado em um cartão compacto no canto inferior esquerdo: procurando, atualizado, baixando, pronto para instalar (`ReadyToInstall`), instalando ou falha recuperável. Em atualizado (`Current`) e falha (`Failed`), oferecer nova consulta manual.
4. Ao descobrir uma versão superior, baixar e validar automaticamente o instalador. Esta cláusula **substitui somente** a regra anterior deste item que proibia download sem clique; ela não altera a proibição de execução automática. Somente **INSTALAR**, acionado explicitamente depois de `ReadyToInstall`, autoriza iniciar o setup.
5. Manter catálogo, login e abertura de jogo disponíveis durante consulta e download. Impedir a instalação enquanto houver qualquer sessão de jogo ativa ou uma autenticação/abertura ainda em andamento; iniciar uma conta durante o download apenas deixa **INSTALAR** desabilitado até a operação terminar e todas as contas serem fechadas.
6. Versionar releases como `vMAJOR.MINOR.PATCH`; exigir igualdade entre tag, versão do manifesto e nome do setup.
7. Publicar `update-manifest.json` com schema, repositório, versão, nome, bytes, SHA-256 e notas em `pt-BR`, `en-US` e `es-ES`.
8. Manter cada patch note fonte em `docs/releases/vX.Y.Z.json`. O build gera o manifesto consumido pela App e o `RELEASE_NOTES.md` usado pelo GitHub Release a partir da mesma origem.
9. Restringir API, manifesto alternativo, assets e redirects a HTTPS, porta padrão, repositório esperado e hosts GitHub permitidos; impor limites de tamanho, número de redirects e tempo. O fallback descobre somente o mesmo manifesto, conserva todas as validações e pode preparar o download, mas nunca autoriza a execução sem **INSTALAR**.
10. Antes de usar a rede, reutilizar o setup final em cache apenas se caminho/nome, bytes e SHA-256 coincidirem exatamente com o release. Apagar cache inválido, escrever novo download como `.part`, calcular SHA-256 durante o stream, exigir bytes exatos e mover para o nome final somente após validação. Revalidar caminho, tamanho e hash imediatamente antes de executar.
11. Manter uma única instância do launcher por sessão do Windows, restaurando a janela já aberta quando houver nova tentativa de inicialização. Isso concentra todas as contas no workspace e impede disputa cross-process pelo mesmo `.part`/setup.
12. Permitir até uma hora para o download total do setup. O limite continua finito contra conexões travadas, mas deixa de pressupor a velocidade da máquina/rede de desenvolvimento.
11. Iniciar o Inno Setup em modo silencioso controlado, com fechamento da App e `/RELAUNCH`; o launcher atual encerra somente depois que o setup foi iniciado.
12. Usar o `GITHUB_TOKEN` efêmero do workflow apenas no GitHub Actions. O aplicativo público não contém PAT, token de usuário, certificado ou chave privada.
13. Limpar, em best effort, somente setups e `.part` oficiais com mais de 24 horas no nível superior do diretório de updates; não seguir reparse points nem deixar arquivo bloqueado impedir a abertura.
14. Disparar o workflow somente por tags `v*.*.*`, separar um job de build com `contents: read` de um job de publicação com `contents: write`, transferindo entre eles apenas os artefatos validados.
15. Fixar actions por commit e a versão do Inno Setup no job de build; o checkout não persiste credenciais.
16. Preservar a 1.1.0 como o primeiro bootstrap histórico com updater. Usuários da 1.0.1, que não têm updater, devem instalar manualmente a versão pública mais recente; usuários da 1.1.0 afetados pela cota compartilhada da API também podem passar manualmente para a 1.1.1, que introduz o fallback `403`/`429` para os ciclos seguintes.

## Segurança e confiança

O SHA-256 comprova que o arquivo baixado coincide com o manifesto e detecta corrupção ou troca incoerente entre assets. A validação de URL e repositório reduz redirecionamento para origem arbitrária. Essas garantias não substituem assinatura do publicador: se o repositório e o release forem comprometidos em conjunto, um manifesto novo pode acompanhar um binário novo.

Os artefatos atuais não possuem Authenticode. Até uma etapa futura de assinatura de código, o Windows pode exibir SmartScreen e a documentação deve dizer explicitamente que checksum não autentica identidade. A instalação permanece opt-in e falhas de rede/validação não afetam login ou jogo. Consulta e download podem ocorrer automaticamente, mas nenhum caminho de estado executa o setup antes do comando **INSTALAR**.

## Consequências

- Novas versões ficam visíveis no próprio launcher, com notas no idioma selecionado.
- O projeto não precisa operar backend de atualização nem distribuir credencial do GitHub.
- Redes corporativas, provedores e outras origens com IP compartilhado deixam de depender exclusivamente da cota não autenticada da API para descobrir o manifesto mais recente.
- Tags e definições de release tornam versão e patch notes auditáveis no repositório.
- Instalações 1.0.1 e instalações 1.1.0 presas no rate limit ainda exigem passagem manual para a 1.1.1; o benefício do fallback vale a partir dela.
- O diretório local de updates pode reter o último setup validado por até um ciclo de limpeza, mas não contém perfis, senhas ou cookies; itens não oficiais e subpastas nunca são apagados pela rotina.
- O instalador pode consumir rede e disco antecipadamente, mas um setup idêntico já validado é reutilizado em vez de baixado de novo.
- A pessoa mantém controle sobre quando executar e instalar; a instalação permanece bloqueada enquanto houver sessões, evitando interrupção de contas.
- A ausência de Authenticode continua sendo uma limitação conhecida até haver certificado e etapa de assinatura antes do hash final.

## Alternativas consideradas

- **Executar ou instalar automaticamente sem confirmação:** rejeitado por iniciar código e potencialmente interromper sessões sem escolha explícita.
- **Exigir clique antes do download:** adotado originalmente e depois substituído pela preparação automática validada; atrasava o handoff e fazia o usuário aguardar depois de já escolher instalar.
- **Polling contínuo:** rejeitado por tráfego desnecessário e risco de interferência durante o jogo.
- **Usar apenas a API pública sem fallback:** rejeitado porque a cota é compartilhada por IP e pode impedir a descoberta em redes com muitos usuários, mesmo quando o release e seu manifesto continuam públicos.
- **Servidor próprio de updates:** rejeitado pelo custo operacional e por duplicar o canal público já exigido.
- **Incorporar PAT no aplicativo:** rejeitado porque clientes públicos não conseguem proteger segredo reutilizável.
- **Consumir somente o texto livre do GitHub Release:** rejeitado por não fornecer contrato estrito de nome, bytes, hash e notas trilíngues.
- **Confiar apenas no SHA-256 publicado:** rejeitado como autenticação de origem; hash é mantido como verificação de integridade, com Authenticode registrado como evolução necessária.
- **Instalar com contas abertas:** rejeitado para não encerrar GameHosts e sessões sem controle.

## Referências

- [Módulo Atualização](../modulos/atualizacao.md)
- [Distribuição Windows](../modulos/distribuicao-windows.md)
- [Launcher App](../modulos/launcher-app.md)
- [Localização](../modulos/localizacao.md)
- [Infrastructure](../modulos/infrastructure.md)
- [`SECURITY.md`](../../SECURITY.md)
