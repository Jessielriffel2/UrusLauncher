using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LegendLauncher.App.Localization;

namespace LegendLauncher.App.MacroAssistant;

/// <summary>
/// Small modal that lists the logged accounts so the user chooses in which ones the macro
/// runs. One account, some of them or all: the macro starts only on the checked sessions.
/// </summary>
internal sealed class MacroSessionPickerWindow : Window
{
    private readonly List<(IMacroSession Session, CheckBox CheckBox)> _rows = [];
    private CheckBox? _selectAllCheckBox;

    private MacroSessionPickerWindow(Window owner, IReadOnlyList<IMacroSession> sessions)
    {
        Owner = owner;
        Title = Localize("Macro_PickerTitle", "Onde o macro vai rodar?");
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        Width = 340;
        ShowInTaskbar = false;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(5, 16, 29));
        BorderBrush = new SolidColorBrush(Color.FromRgb(44, 96, 124));
        BorderThickness = new Thickness(1);

        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };

        root.Children.Add(new TextBlock
        {
            Text = Localize("Macro_PickerTitle", "Onde o macro vai rodar?"),
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        });
        root.Children.Add(new TextBlock
        {
            Text = Localize("Macro_PickerHint", "Escolha a conta ou as contas em que o macro deve executar."),
            Foreground = new SolidColorBrush(Color.FromRgb(173, 194, 211)),
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });

        if (sessions.Count > 1)
        {
            _selectAllCheckBox = CreateCheckBox(
                Localize("Macro_PickerAll", "Todas as contas"),
                isChecked: true);
            _selectAllCheckBox.Checked += (_, _) => SetAll(checkedState: true);
            _selectAllCheckBox.Unchecked += (_, _) => SetAll(checkedState: false);
            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(31, 86, 107)),
                Margin = new Thickness(0, 8, 0, 8),
            };
            root.Children.Add(_selectAllCheckBox);
            root.Children.Add(separator);
        }

        foreach (IMacroSession session in sessions)
        {
            CheckBox row = CreateCheckBox(session.SessionTitle, isChecked: true);
            row.Checked += (_, _) => SyncSelectAll();
            row.Unchecked += (_, _) => SyncSelectAll();
            _rows.Add((session, row));
            root.Children.Add(row);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0),
        };

        var startButton = new Button
        {
            Content = Localize("Macro_PickerStart", "Iniciar macro"),
            Width = 122,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(13, 148, 136)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(45, 212, 191)),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand,
            IsDefault = true,
        };
        startButton.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        var cancelButton = new Button
        {
            Content = Localize("Common_Cancel", "Cancelar"),
            Width = 96,
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(13, 33, 51)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(47, 86, 107)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            IsCancel = true,
        };

        buttons.Children.Add(startButton);
        buttons.Children.Add(cancelButton);
        root.Children.Add(buttons);
        Content = root;
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }

    /// <summary>Returns the sessions the user kept checked, or null when cancelled.</summary>
    public static IReadOnlyList<IMacroSession>? Pick(Window owner, IReadOnlyList<IMacroSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(sessions);
        var picker = new MacroSessionPickerWindow(owner, sessions);
        bool accepted = picker.ShowDialog() == true;
        return accepted
            ? picker._rows
                .Where(row => row.CheckBox.IsChecked == true)
                .Select(row => row.Session)
                .ToArray()
            : null;
    }

    private void SetAll(bool checkedState)
    {
        foreach ((_, CheckBox checkBox) in _rows)
        {
            checkBox.IsChecked = checkedState;
        }
    }

    private void SyncSelectAll()
    {
        if (_selectAllCheckBox is null)
        {
            return;
        }

        bool allChecked = _rows.All(row => row.CheckBox.IsChecked == true);
        if (_selectAllCheckBox.IsChecked != allChecked)
        {
            _selectAllCheckBox.IsChecked = allChecked;
        }
    }

    private static CheckBox CreateCheckBox(string text, bool isChecked) =>
        new()
        {
            Content = text,
            IsChecked = isChecked,
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 3, 0, 3),
            Cursor = Cursors.Hand,
        };

    private static string Localize(string key, string fallback) =>
        LocalizationService.Current.Get(key) is string value && value != $"[{key}]"
            ? value
            : fallback;
}
