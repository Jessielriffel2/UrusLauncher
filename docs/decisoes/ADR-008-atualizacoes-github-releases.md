# ADR-008 — Atualizações opcionais pelo GitHub Releases

- **Status:** aceito
- **Data:** 2026-07-15

## Contexto

O Urus Launcher será distribuído publicamente e precisa avisar sobre novas versões sem exigir que cada usuário acompanhe manualmente o repositório. A atualização não pode interromper contas em jogo, baixar executáveis sem consentimento nem depender de um servidor próprio ou de um token secreto dentro do aplicativo. As mudanças também precisam aparecer como patch notes em português brasileiro, inglês e espanhol.

O instalador Inno Setup já usa `AppId` e diretório por usuário estáveis, portanto uma versão nova pode substituir a anterior preservando perfis, settings, cache e credenciais, que ficam fora da pasta de instalação.

## Decisão

1. Usar o GitHub Releases público do repositório fixo `Jessielriffel2/UrusLauncher` como canal estável de atualização.
2. Consultar `releases/latest` uma vez em cada abertura, de forma assíncrona e não bloqueante. Não manter timer nem polling durante o jogo.
3. Mostrar o estado em um cartão compacto no canto inferior esquerdo: procurando, atualizado, disponível, baixando, instalando ou falha recuperável.
4. Nunca baixar ou executar o instalador durante a consulta. Somente um clique explícito em **Atualizar** autoriza o download.
5. Impedir instalação enquanto houver qualquer sessão de jogo ativa, bloquear novas sessões durante download/instalação e revalidar a ausência de sessões depois do download antes de executar o setup.
6. Versionar releases como `vMAJOR.MINOR.PATCH`; exigir igualdade entre tag, versão do manifesto e nome do setup.
7. Publicar `update-manifest.json` com schema, repositório, versão, nome, bytes, SHA-256 e notas em `pt-BR`, `en-US` e `es-ES`.
8. Manter cada patch note fonte em `docs/releases/vX.Y.Z.json`. O build gera o manifesto consumido pela App e o `RELEASE_NOTES.md` usado pelo GitHub Release a partir da mesma origem.
9. Restringir API, assets e redirects a HTTPS, porta padrão, repositório esperado e hosts GitHub permitidos; impor limites de tamanho, número de redirects e tempo.
10. Escrever o download como `.part`, calcular SHA-256 durante o stream, exigir bytes exatos e mover para o nome final somente após validação. Revalidar caminho, tamanho e hash imediatamente antes de executar.
11. Iniciar o Inno Setup em modo silencioso controlado, com fechamento da App e `/RELAUNCH`; o launcher atual encerra somente depois que o setup foi iniciado.
12. Usar o `GITHUB_TOKEN` efêmero do workflow apenas no GitHub Actions. O aplicativo público não contém PAT, token de usuário, certificado ou chave privada.
13. Limpar, em best effort, somente setups e `.part` oficiais com mais de 24 horas no nível superior do diretório de updates; não seguir reparse points nem deixar arquivo bloqueado impedir a abertura.
14. Disparar o workflow somente por tags `v*.*.*`, separar um job de build com `contents: read` de um job de publicação com `contents: write`, transferindo entre eles apenas os artefatos validados.
15. Fixar actions por commit e a versão do Inno Setup no job de build; o checkout não persiste credenciais.
16. Tratar a 1.1.0 como bootstrap: usuários da 1.0.1 precisam instalá-la manualmente uma vez; somente versões que já contêm o updater podem descobrir releases futuros.

## Segurança e confiança

O SHA-256 comprova que o arquivo baixado coincide com o manifesto e detecta corrupção ou troca incoerente entre assets. A validação de URL e repositório reduz redirecionamento para origem arbitrária. Essas garantias não substituem assinatura do publicador: se o repositório e o release forem comprometidos em conjunto, um manifesto novo pode acompanhar um binário novo.

Os artefatos atuais não possuem Authenticode. Até uma etapa futura de assinatura de código, o Windows pode exibir SmartScreen e a documentação deve dizer explicitamente que checksum não autentica identidade. A instalação permanece opt-in e falhas de rede/validação não afetam login ou jogo.

## Consequências

- Novas versões ficam visíveis no próprio launcher, com notas no idioma selecionado.
- O projeto não precisa operar backend de atualização nem distribuir credencial do GitHub.
- Tags e definições de release tornam versão e patch notes auditáveis no repositório.
- O primeiro instalador com updater ainda exige distribuição manual; o benefício passa a valer para os ciclos seguintes.
- O diretório local de updates pode reter o último setup validado por até um ciclo de limpeza, mas não contém perfis, senhas ou cookies; itens não oficiais e subpastas nunca são apagados pela rotina.
- A pessoa mantém controle sobre quando baixar e instalar, evitando interrupção de sessões.
- A ausência de Authenticode continua sendo uma limitação conhecida até haver certificado e etapa de assinatura antes do hash final.

## Alternativas consideradas

- **Atualizar automaticamente sem confirmação:** rejeitado por baixar/executar código e interromper sessões sem escolha explícita.
- **Polling contínuo:** rejeitado por tráfego desnecessário e risco de interferência durante o jogo.
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
