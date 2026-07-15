# Módulo Network Bridge

## Objetivo

Substituir gradualmente o proxy legado por uma ponte local mínima, explícita e testável. O primeiro marco implementa apenas a política de segurança; ainda não abre portas.

## Funções e classes principais

- `BridgeSecurityPolicy.EnsureLoopback` — impede bind em interfaces externas (`src/LegendLauncher.NetworkBridge/BridgeSecurityPolicy.cs:28`).
- `BridgeSecurityPolicy.ValidateUpstream` — aceita somente HTTPS/WSS em domínios permitidos e rejeita credenciais/IP literal (`src/LegendLauncher.NetworkBridge/BridgeSecurityPolicy.cs:38`).
- `BridgeValidationResult` — resultado imutável de validação (`src/LegendLauncher.NetworkBridge/BridgeValidationResult.cs:3`).

## Entradas e saídas

- Entrada: endpoint local pretendido ou URI remota.
- Saída: aprovação ou motivo de bloqueio; violações de bind lançam exceção.

## Dependências

Somente bibliotecas base de rede do .NET. Será usado pelo [GameHost Legacy](game-host-legacy.md); não depende do launcher visual.
