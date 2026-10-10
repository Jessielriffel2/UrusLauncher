namespace LegendLauncher.Providers.SwordOnline;

/// <summary>
/// Where one LOReborn portal lives and how the launcher must talk to it. The production
/// portal (<c>swordonline.tech</c>) and the developer-only local portal speak exactly the
/// same protocol (<c>POST /api/launcher</c> → ticket → Flash <c>/login</c>); only the
/// addresses and the <c>http</c> flashvar differ.
/// </summary>
/// <param name="PlatformId">Id of the <see cref="LegendLauncher.Core.Models.PlatformDefinition"/> this portal serves.</param>
/// <param name="ServerId">Id of the single server the portal exposes.</param>
/// <param name="LauncherUri">Endpoint that trades login + password for a game ticket.</param>
/// <param name="ConfigUri"><c>config.xml</c> handed to the Flash client.</param>
/// <param name="MovieUri"><c>Loading.swf</c> loaded by the Flash runtime.</param>
/// <param name="HttpFlashVar">
/// Value of the <c>http</c> flashvar: <c>"https:"</c> (with the colon) makes the client rewrite its
/// <c>http://</c> config entries to <c>https://</c>; <c>"http"</c> leaves them alone.
/// </param>
public sealed record SwordOnlinePortal(
    string PlatformId,
    string ServerId,
    Uri LauncherUri,
    Uri ConfigUri,
    Uri MovieUri,
    string HttpFlashVar);
