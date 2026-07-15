# ADR-007 — Identidade e distribuição Urus

- **Status:** aceito
- **Data:** 2026-07-15

## Contexto

O launcher deixou de ser uma prévia técnica e precisa de uma identidade pública própria, coerente no WPF, GameHost, Windows e arquivos entregues. O nome anterior misturava marca do produto com o nome do jogo e ainda expunha “Next”, preview/teste e assets de protótipo. Também faltava uma forma repetível de produzir instalador e pacote portátil sem exigir que o usuário instalasse o runtime .NET separadamente.

Ao mesmo tempo, renomear todos os namespaces, projetos e diretórios de dados aumentaria muito o risco sem benefício visível. Legend Online continua sendo o jogo acessado; essa marca não deve ser reescrita em nomes oficiais de plataforma, servidor ou conteúdo Flash.

## Decisão

1. Adotar **Urus Launcher** como nome visível único do produto e **Urus GameHost** para o chrome próprio do processo legado.
2. Remover de toda UI própria marcadores de “Next”, prévia, preview, technical, testes e equivalentes; manter testes que impeçam sua reintrodução em catálogos públicos.
3. Usar `Brand_Subtitle` para o slogan localizado: “Jogue do seu jeito”, “Play your way” e “Juega a tu manera”, apresentados com tracking visual no cabeçalho.
4. Usar um logo original gerado pelo `imagegen` incorporado, com cromakey removido localmente para um PNG RGBA transparente. O símbolo é um monograma “U”/portal abstrato; não contém touro, escudo nem trade dress de terceiros.
5. Manter `Assets/Branding/urus-logo.png` como fonte WPF e derivar `urus-launcher.ico` em sete resoluções para executável, instalador e atalhos. Remover os marks antigos depois que não houver referência.
6. Publicar o assembly/arquivo principal como `UrusLauncher.App`/`UrusLauncher.App.exe`; alinhar manifesto e metadados Title/Product/Company/Description. Preservar `RootNamespace=LegendLauncher.App` e os nomes dos projetos internos.
7. Ajustar Pack URIs e nomes lógicos dos catálogos incorporados ao novo assembly, sem alterar chaves, comportamento ou os três idiomas.
8. Preservar `LegendLauncher.*` quando for contrato interno e **Legend Online** quando for o jogo. Manter `%LocalAppData%\LegendLauncherNext` nesta versão para não perder perfis/settings/cache existentes.
9. Produzir distribuição `win-x64` self-contained e não trimmed, contendo App e GameHost em um único payload expandido.
10. Gerar dois entregáveis versionados a partir do mesmo payload: instalador por usuário via Inno Setup e ZIP portátil. A versão 1.0.0 usa `UrusLauncher-Setup-1.0.0-win-x64.exe` e `UrusLauncher-1.0.0-portable-win-x64.zip`.
11. Instalar sem elevação em `%LocalAppData%\Programs\Urus Launcher`, com idiomas inglês/português brasileiro/espanhol, menu Iniciar e atalho de desktop opcional.
12. Emitir `distribution-manifest.json` e `SHA256SUMS.txt`; validar runtimeconfig/deps, arquivos essenciais e a implementação WPF completa de `WindowsBase.dll`, além de executar um smoke do portátil sem runtime .NET global antes de concluir.
13. Não copiar automaticamente para `Downloads` dentro do script. `artifacts/urus-distribution` é a saída canônica; o handoff pode copiar explicitamente os dois pacotes para `%USERPROFILE%\Downloads` depois da validação, preservando/verificando o hash.
14. Não redistribuir Flash ActiveX, dados locais ou segredos. “Self-contained” cobre o runtime .NET, não o componente legado licenciado.
15. Preservar o payload WPF produzido pela App e incorporar do staging self-contained somente os quatro arquivos próprios do GameHost (`.exe`, `.dll`, `.deps.json` e `.runtimeconfig.json`), sem sobrepor assemblies compartilhados com facades incompatíveis.

## Consequências

- Usuários veem uma identidade consistente no launcher, GameHost, propriedades do EXE, instalador e atalhos.
- Slogans permanecem naturais nos três idiomas sem transformar o nome da marca.
- A fonte e o ícone são auditáveis e independentes de assets do protótipo ou marcas externas.
- O nome físico `UrusLauncher.App.exe` facilita suporte e distribuição, enquanto preservar namespaces internos reduz o risco de regressões sem valor de produto.
- Perfis e preferências existentes continuam acessíveis porque o diretório local não muda nesta versão.
- O payload self-contained aumenta o tamanho dos pacotes, mas reduz dependência da instalação global do .NET.
- Usar o mesmo payload para instalador/ZIP reduz divergência; manifesto e checksums registram exatamente o que foi entregue.
- A composição explícita evita que uma facade de `WindowsBase.dll` produzida para o GameHost substitua a implementação exigida pelo WPF; a validação de tamanho/versão e o smoke tornam essa regressão uma falha de build.
- A instalação per-user evita pedido de elevação e simplifica atualizações no mesmo AppId.
- Ausência de Authenticode pode produzir avisos do Windows. Checksums permitem integridade, mas não conferem reputação/assinatura.

## Segurança e propriedade intelectual

- O briefing e o asset foram criados para Urus; não foi copiado logo de jogo, launcher ou fabricante existente.
- O conceito exclui deliberadamente touro literal, escudo e trade dress reconhecível de terceiros.
- O processamento local removeu somente o fundo cromakey e produziu alpha; não importou elemento externo.
- O instalador não inclui credenciais, perfis, cache, tokens, sessão autenticada, Flash OCX ou arquivos do cliente antigo.
- O script confina qualquer remoção recursiva ao diretório de artefatos e falha em payload incompleto.
- Hashes devem ser recalculados a cada build. Se houver assinatura futura, ela deve ocorrer antes do hash final publicado.

## Alternativas consideradas

- **Manter o nome de prévia:** rejeitado porque comunica produto provisório e mistura a identidade com o jogo.
- **Usar um touro/urus literal:** rejeitado para evitar clichê, baixa legibilidade em 16 px e proximidade com identidades existentes.
- **Usar escudo gamer:** rejeitado por ser genérico e aproximar a marca de trade dress comum no segmento.
- **Copiar/recolorir o mark antigo:** rejeitado porque não cria identidade própria.
- **Renomear todos os namespaces e o diretório de dados:** rejeitado neste marco pelo alto risco e por quebrar continuidade local sem alterar a experiência visível.
- **Publicar framework-dependent:** rejeitado porque exigiria runtime .NET compatível pré-instalado.
- **Single-file/trimmed:** rejeitado por risco com WPF, WinForms, COM e reflexão e porque o GameHost precisa permanecer um processo separado.
- **Instalar em Program Files com administrador:** rejeitado porque o produto funciona como aplicação por usuário e não precisa elevar privilégios.
- **Gerar só instalador:** rejeitado para manter opção portátil e auditável.
- **Copiar silenciosamente para Downloads:** rejeitado porque Downloads fica fora da raiz autorizada do build e a cópia deve ser uma ação explícita do usuário/mantenedor.

## Referências

- [Branding Urus](../modulos/branding.md)
- [Distribuição Windows](../modulos/distribuicao-windows.md)
- [Launcher App](../modulos/launcher-app.md)
- [Localização](../modulos/localizacao.md)
- [GameHost Legacy](../modulos/game-host-legacy.md)
- [Runtime Flash](ADR-002-runtime-flash.md)
- [Design QA](../../design-qa.md)
