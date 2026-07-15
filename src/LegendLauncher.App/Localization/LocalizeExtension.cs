using System.Windows.Data;
using System.Windows.Markup;

namespace LegendLauncher.App.Localization;

[MarkupExtensionReturnType(typeof(object))]
public sealed class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension(string key)
    {
        Key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("A localization key is required.", nameof(key))
            : key;
    }

    public string Key { get; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Current,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
