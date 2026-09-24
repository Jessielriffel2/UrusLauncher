using System.Collections.ObjectModel;
using System.ComponentModel;
using LegendLauncher.App.Localization;
using LegendLauncher.App.ViewModels;

namespace LegendLauncher.App.MacroAssistant;

internal sealed class MacroAssistantViewModel : ObservableObject, IDisposable
{
    private readonly LocalizationService _localization;
    private readonly RelayCommand _toggleAllCommand;
    private readonly RelayCommand _cycleModeCommand;
    private bool _overlayVisible = true;
    private MacroMode _mode = MacroMode.Gems;
    private bool _disposed;

    public MacroAssistantViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Sessions = [];
        _toggleAllCommand = new RelayCommand(ToggleAll, () => Sessions.Count > 0);
        _cycleModeCommand = new RelayCommand(CycleMode);
        _localization.LanguageChanged += LocalizationOnLanguageChanged;
    }

    public ObservableCollection<IMacroSession> Sessions { get; }

    public RelayCommand ToggleAllCommand => _toggleAllCommand;

    public RelayCommand CycleModeCommand => _cycleModeCommand;

    public MacroMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(ModeLabel));
                foreach (IMacroSession session in Sessions)
                {
                    session.SetMode(value);
                }
            }
        }
    }

    public bool OverlayVisible
    {
        get => _overlayVisible;
        set
        {
            if (SetProperty(ref _overlayVisible, value))
            {
                foreach (IMacroSession session in Sessions)
                {
                    session.OverlayVisible = value;
                }
            }
        }
    }

    public string ModeLabel => _localization.Get(Mode switch
    {
        MacroMode.Cosmo => "Macro_ModeCosmo",
        MacroMode.Clicks => "Macro_ModeClicks",
        _ => "Macro_ModeGems",
    });

    public bool HasActiveSessions => Sessions.Any(static session => session.IsActive);

    public string ToggleLabel => _localization.Get(
        Sessions.Any(static session => session.IsActive)
            ? "Macro_StopAll"
            : "Macro_StartAll");

    public string StatusLabel => _localization.Format(
        "Macro_StatusFormat",
        Sessions.Count,
        Sessions.Count(static session => session.IsActive));

    public string ErrorText =>
        Sessions.FirstOrDefault(static session => session.State == MacroSessionState.Error)?.StatusText ?? string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    internal void AddController(IMacroSession controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (_disposed)
        {
            controller.Dispose();
            return;
        }

        controller.SetMode(Mode);
        controller.OverlayVisible = OverlayVisible;
        controller.PropertyChanged += ControllerOnPropertyChanged;
        Sessions.Add(controller);
        RefreshCommandState();
    }

    internal void RemoveController(IMacroSession controller)
    {
        if (controller is null || !Sessions.Remove(controller))
        {
            return;
        }

        controller.PropertyChanged -= ControllerOnPropertyChanged;
        controller.Dispose();
        RefreshCommandState();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= LocalizationOnLanguageChanged;
        foreach (IMacroSession controller in Sessions.ToArray())
        {
            controller.PropertyChanged -= ControllerOnPropertyChanged;
            controller.Dispose();
        }

        Sessions.Clear();
    }

    private void ToggleAll()
    {
        if (Sessions.Any(static session => session.IsActive))
        {
            StopAll();
            return;
        }

        foreach (IMacroSession session in Sessions)
        {
            session.Start();
        }
    }

    private void StopAll()
    {
        foreach (IMacroSession session in Sessions)
        {
            session.Stop();
        }
    }

    private void CycleMode() => Mode = Mode switch
    {
        MacroMode.Gems => MacroMode.Cosmo,
        MacroMode.Cosmo => MacroMode.Clicks,
        _ => MacroMode.Gems,
    };

    private void ControllerOnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(IMacroSession.IsActive) or
            nameof(IMacroSession.State) or
            nameof(IMacroSession.StatusText))
        {
            RefreshCommandState();
        }
    }

    private void RefreshCommandState()
    {
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasActiveSessions));
        _toggleAllCommand.NotifyCanExecuteChanged();
    }

    private void LocalizationOnLanguageChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ErrorText));
        OnPropertyChanged(nameof(HasError));
    }
}
