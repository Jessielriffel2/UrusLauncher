using LegendLauncher.App.Localization;
using LegendLauncher.App.ViewModels;
using LegendLauncher.Core.Models;

namespace LegendLauncher.Tests.App;

public sealed class ServerRowLocalizationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 14, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("pt-BR", "RECOMENDADO", "MAIS RECENTE", "OUTROS SERVIDORES", "Servidor 3257", "Seu último servidor · disponível")]
    [InlineData("en-US", "RECOMMENDED", "NEWEST", "OTHER SERVERS", "Server 3257", "Your last server · available")]
    [InlineData("es-ES", "RECOMENDADO", "MÁS RECIENTE", "OTROS SERVIDORES", "Servidor 3257", "Tu último servidor · disponible")]
    public void LocalizedPropertiesUseRequestedCatalog(
        string language,
        string expectedRecommendedBadge,
        string expectedLatestBadge,
        string expectedSectionLabel,
        string expectedName,
        string expectedAvailability)
    {
        var localization = new LocalizationService(language);
        var row = new ServerRowViewModel(
            CreateServer("Server 3257"),
            Now,
            isCurrent: true,
            isLatestReleased: true,
            localization: localization);

        Assert.Equal(expectedRecommendedBadge, row.RecommendedBadgeText);
        Assert.Equal(expectedLatestBadge, row.LatestBadgeText);
        Assert.Equal(expectedSectionLabel, row.SectionLabelText);
        Assert.Equal(expectedName, row.Name);
        Assert.Equal(expectedName, row.FullName);
        Assert.Equal(expectedAvailability, row.AvailabilityLabel);
        Assert.DoesNotContain("[Server_", row.RecommendedBadgeToolTip, StringComparison.Ordinal);
        Assert.DoesNotContain("[Server_", row.LatestBadgeToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshLocalizationRaisesEveryAffectedPropertyAndUpdatesFallbackText()
    {
        var localization = new LocalizationService("pt-BR");
        var row = new ServerRowViewModel(
            CreateServer("Server 3257"),
            Now,
            localization: localization);
        var changedProperties = new List<string?>();
        row.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        localization.SetLanguage("en-US");
        row.RefreshLocalization();

        Assert.Equal("Server 3257", row.Name);
        Assert.Equal("Available for selection", row.AvailabilityLabel);
        Assert.Equal(
            [
                nameof(ServerRowViewModel.Name),
                nameof(ServerRowViewModel.FullName),
                nameof(ServerRowViewModel.RecommendedBadgeText),
                nameof(ServerRowViewModel.RecommendedBadgeToolTip),
                nameof(ServerRowViewModel.LatestBadgeText),
                nameof(ServerRowViewModel.LatestBadgeToolTip),
                nameof(ServerRowViewModel.SectionLabelText),
                nameof(ServerRowViewModel.AvailabilityLabel),
            ],
            changedProperties);
    }

    [Theory]
    [InlineData("pt-BR", "Endereço seguro de jogo ausente")]
    [InlineData("en-US", "Secure game address is missing")]
    [InlineData("es-ES", "Falta la dirección segura del juego")]
    public void MissingSecureAddressIsLocalized(string language, string expected)
    {
        var localization = new LocalizationService(language);
        GameServer server = CreateServer("Moon Shadow") with
        {
            LaunchUri = new Uri("http://example.invalid/game"),
        };

        var row = new ServerRowViewModel(server, Now, localization: localization);

        Assert.Equal(expected, row.AvailabilityLabel);
    }

    [Theory]
    [InlineData("pt-BR", "Abre em")]
    [InlineData("en-US", "Opens at")]
    [InlineData("es-ES", "Abre el")]
    public void FutureOpeningDateUsesLocalizedPrefixAndCulture(string language, string prefix)
    {
        var localization = new LocalizationService(language);
        GameServer server = CreateServer("Moon Shadow") with
        {
            StartTimeUtc = Now.AddDays(2),
        };

        var row = new ServerRowViewModel(server, Now, localization: localization);

        Assert.StartsWith(prefix, row.AvailabilityLabel);
        Assert.Equal(
            localization.Format("Server_OpensAt", server.StartTimeUtc!.Value.ToLocalTime()),
            row.AvailabilityLabel);
    }

    private static GameServer CreateServer(string name) => new(
        "3257",
        3257,
        "S3257",
        name,
        name,
        new Uri("https://lobr.creaction-network.com/serverlist/s3257"),
        true,
        true,
        null,
        Now.AddDays(-1));
}
