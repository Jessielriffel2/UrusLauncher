# ADR-009 — Provisionamento do runtime legado na distribuição

- **Status:** aceito; publicação autorizada pelo distribuidor
- **Data:** 2026-07-16

## Contexto

Até a 1.1.3, o setup levava .NET, WPF, WinForms, App e GameHost, mas não o manifesto/OCX exigido pelo GameHost. Nesta máquina o problema ficava oculto porque o launcher encontrava o cliente Brov em `Program Files`. Em uma instalação limpa, `LegacyRuntimeProbe.IsUsable` permanecia falso e `CanStartGame` desabilitava a ação sem hover ou clique.

O controle encontrado é software de terceiros, legado e descontinuado. A Adobe informa que redistribuição exige licença vigente. Uma cópia existente e assinada comprova origem técnica, não concede ao Urus direito de publicação por si só; antes da publicação da 1.1.4, o usuário/distribuidor confirmou possuir a autorização aplicável para redistribuí-lo.

## Decisão

1. O payload executável reserva `runtime\` para o manifesto registration-free e o OCX x64 referenciado.
2. A composição procura primeiro `runtime\` ao lado de `UrusLauncher.App.exe`; `LEGEND_LEGACY_ROOT` e instalações Brov conhecidas permanecem somente como fallback de desenvolvimento/migração.
3. O pipeline recebe `-LegacyRuntimeSource`, depois tenta `LEGEND_LEGACY_ROOT` e o caminho Brov conhecido. Se nenhuma origem estiver disponível, a distribuição falha.
4. Somente o manifesto e o OCX referenciado são copiados. `H2Proxy.exe`, executável do cliente antigo, perfis e demais arquivos não entram no pacote.
5. O build proíbe caminho absoluto/escape do manifesto, exige OCX maior que 1 MiB e assinatura Authenticode válida, e registra tamanho/SHA-256 no manifesto de distribuição.
6. O binário de terceiros não é versionado no Git nem baixado de mirrors pelo build.
7. Setup/ZIP com esse runtime só podem ser publicados depois que o distribuidor confirmar a autorização aplicável. Essa confirmação foi registrada para a 1.1.4; distribuições futuras continuam obrigadas a usar uma origem autorizada.

## Registro de publicação da 1.1.4

- A versão pública 1.1.4 foi gerada e validada localmente com a origem cuja redistribuição foi confirmada pelo usuário/distribuidor.
- O commit e alvo da tag são `31d6d16b063b43bdba161a028bc4edd4f3953b96`.
- O runner hospedado não recebeu a fonte licenciada; por isso, a execução disparada pela tag foi cancelada e não foi usada para construir os artefatos.
- Setup, ZIP, manifesto de atualização e checksums foram publicados manualmente a partir do build local validado.
- O GitHub Release público e sua rota `latest` agora apresentam a 1.1.4 ao atualizador instalado em qualquer máquina.

## Consequências

- Um pacote formado com fonte autorizada funciona em computador sem o cliente Brov instalado e sem registro COM global.
- O mesmo payload alimenta instalador e ZIP, evitando diferença entre os dois formatos.
- O workflow automático por tag precisa receber uma fonte licenciada para construir e publicar; sem ela, deve falhar ou ser cancelado. A publicação manual permanece válida somente quando parte de um build local validado com origem autorizada e mantém rastreabilidade de commit, hashes e artefatos.
- O risco técnico do Flash EOL permanece. O isolamento por processo reduz impacto de falha, mas não transforma o runtime em sandbox nem restaura suporte de segurança.
- Ruffle continua sendo o caminho de longo prazo, condicionado à matriz de compatibilidade real do jogo.

## Alternativas consideradas

- **Copiar silenciosamente o OCX para o repositório público:** rejeitado porque visibilidade/download anterior não equivale a licença de redistribuição.
- **Baixar de site de terceiros durante instalação:** rejeitado por procedência, integridade, disponibilidade e licença.
- **Continuar dependendo de caminho fixo em `Program Files`:** rejeitado porque produz um botão permanentemente indisponível em PCs limpos.
- **Trocar imediatamente por Ruffle:** adiado até validar AS3, rede, FlashVars, teclado, áudio e desempenho nos servidores suportados.

## Referências

- [ADR-002 — Estratégia para Flash e ponte de rede](ADR-002-runtime-flash.md)
- [Distribuição Windows](../modulos/distribuicao-windows.md)
- [GameHost Legacy](../modulos/game-host-legacy.md)
- Política de redistribuição Adobe: <https://www.adobe.com/licensing/distribution/strategies/sms.html>
