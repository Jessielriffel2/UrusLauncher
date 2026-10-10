using LegendLauncher.App.Localization;
using LegendLauncher.Core.Contracts;
using LegendLauncher.Core.Models;

namespace LegendLauncher.App.ViewModels;

/// <summary>
/// In-app account creation for platforms that allow it (<see cref="PlatformDefinition.SupportsAccountCreation"/>).
/// The panel opens from the account editor; on success the new login and password become the pending
/// profile and are saved right away (the password goes to the Windows vault), so the player can press play.
/// </summary>
internal sealed partial class MainWindowViewModel
{
    private IAccountRegistrationService? _accountRegistration;
    private CancellationTokenSource? _registrationCancellation;
    private bool _registrationOpenedFromEditor;
    private bool _isRegistrationVisible;
    private bool _isRegistering;
    private string _registrationLogin = string.Empty;
    private string _registrationPassword = string.Empty;
    private string _registrationPasswordConfirm = string.Empty;
    private string _registrationEmail = string.Empty;
    private LocalizedMessage? _registrationMessage;
    private string? _registrationServerMessage;

    public RelayCommand OpenRegistrationCommand { get; private set; } = null!;

    public RelayCommand CancelRegistrationCommand { get; private set; } = null!;

    public AsyncRelayCommand SubmitRegistrationCommand { get; private set; } = null!;

    /// <summary>
    /// Whether the launcher offers "create an account" for the current platform: a registration
    /// service exists and the platform supports it. The button lives on the main screen, so a player
    /// who already has profiles can still find it.
    /// </summary>
    public bool CanCreateAccount =>
        !_disposed &&
        _accountRegistration is not null &&
        SelectedPlatform.Model.SupportsAccountCreation;

    /// <summary>
    /// The extra link inside the account editor, only while a brand-new profile is being edited
    /// (so an existing profile is never overwritten by a different login).
    /// </summary>
    public bool ShowCreateAccountInEditor =>
        CanCreateAccount &&
        IsProfileEditorVisible &&
        SelectedProfile is null;

    public string CreateAccountButtonText =>
        _localization.Format("Registration_Button", SelectedPlatform.DisplayName);

    public string CreateAccountLinkText =>
        _localization.Format("Registration_OpenLink", SelectedPlatform.DisplayName);

    public string RegistrationTitle =>
        _localization.Format("Registration_Title", SelectedPlatform.DisplayName);

    public bool IsRegistrationVisible
    {
        get => _isRegistrationVisible;
        private set => SetProperty(ref _isRegistrationVisible, value);
    }

    public bool IsRegistering
    {
        get => _isRegistering;
        private set
        {
            if (SetProperty(ref _isRegistering, value))
            {
                OnPropertyChanged(nameof(RegistrationSubmitLabel));
                SubmitRegistrationCommand.NotifyCanExecuteChanged();
                CancelRegistrationCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string RegistrationSubmitLabel => _localization.Get(
        IsRegistering ? "Registration_Submitting" : "Registration_Submit");

    public string RegistrationLogin
    {
        get => _registrationLogin;
        set
        {
            if (SetProperty(ref _registrationLogin, value ?? string.Empty))
            {
                ClearRegistrationMessage();
            }
        }
    }

    public string RegistrationPassword
    {
        get => _registrationPassword;
        set
        {
            if (SetProperty(ref _registrationPassword, value ?? string.Empty))
            {
                ClearRegistrationMessage();
            }
        }
    }

    public string RegistrationPasswordConfirm
    {
        get => _registrationPasswordConfirm;
        set
        {
            if (SetProperty(ref _registrationPasswordConfirm, value ?? string.Empty))
            {
                ClearRegistrationMessage();
            }
        }
    }

    public string RegistrationEmail
    {
        get => _registrationEmail;
        set
        {
            if (SetProperty(ref _registrationEmail, value ?? string.Empty))
            {
                ClearRegistrationMessage();
            }
        }
    }

    public string RegistrationMessage =>
        _registrationServerMessage ?? _registrationMessage?.Resolve(_localization) ?? string.Empty;

    public bool HasRegistrationMessage => RegistrationMessage.Length > 0;

    private void InitializeRegistration(IAccountRegistrationService? accountRegistration)
    {
        _accountRegistration = accountRegistration;
        OpenRegistrationCommand = new RelayCommand(OpenRegistration, () => CanCreateAccount && !IsLaunching);
        CancelRegistrationCommand = new RelayCommand(CloseRegistration, () => !IsRegistering);
        SubmitRegistrationCommand = new AsyncRelayCommand(
            SubmitRegistrationAsync,
            () => IsRegistrationVisible && !IsRegistering);
    }

    private void NotifyRegistrationAvailability()
    {
        OnPropertyChanged(nameof(CanCreateAccount));
        OnPropertyChanged(nameof(ShowCreateAccountInEditor));
        OnPropertyChanged(nameof(CreateAccountButtonText));
        OnPropertyChanged(nameof(CreateAccountLinkText));
        OnPropertyChanged(nameof(RegistrationTitle));
        OpenRegistrationCommand.NotifyCanExecuteChanged();
    }

    private void OpenRegistration()
    {
        if (!CanCreateAccount)
        {
            return;
        }

        _registrationOpenedFromEditor = IsProfileEditorVisible;
        ResetRegistrationFields();
        IsRegistrationVisible = true;
        SubmitRegistrationCommand.NotifyCanExecuteChanged();
    }

    private void CloseRegistration()
    {
        if (IsRegistering)
        {
            return;
        }

        IsRegistrationVisible = false;
        ResetRegistrationFields();
        SubmitRegistrationCommand.NotifyCanExecuteChanged();
    }

    private void ResetRegistrationFields()
    {
        _registrationLogin = string.Empty;
        _registrationPassword = string.Empty;
        _registrationPasswordConfirm = string.Empty;
        _registrationEmail = string.Empty;
        _registrationMessage = null;
        _registrationServerMessage = null;
        OnPropertyChanged(nameof(RegistrationLogin));
        OnPropertyChanged(nameof(RegistrationPassword));
        OnPropertyChanged(nameof(RegistrationPasswordConfirm));
        OnPropertyChanged(nameof(RegistrationEmail));
        OnPropertyChanged(nameof(RegistrationMessage));
        OnPropertyChanged(nameof(HasRegistrationMessage));
    }

    private void ShowRegistrationMessage(string key, string? serverMessage = null)
    {
        _registrationMessage = LocalizedMessage.Create(key);
        _registrationServerMessage = string.IsNullOrWhiteSpace(serverMessage) ? null : serverMessage;
        OnPropertyChanged(nameof(RegistrationMessage));
        OnPropertyChanged(nameof(HasRegistrationMessage));
    }

    private void ClearRegistrationMessage()
    {
        if (_registrationMessage is null && _registrationServerMessage is null)
        {
            return;
        }

        _registrationMessage = null;
        _registrationServerMessage = null;
        OnPropertyChanged(nameof(RegistrationMessage));
        OnPropertyChanged(nameof(HasRegistrationMessage));
    }

    private async Task SubmitRegistrationAsync()
    {
        IAccountRegistrationService? service = _accountRegistration;
        if (service is null || !CanCreateAccount || IsRegistering)
        {
            return;
        }

        string login = RegistrationLogin.Trim();
        string password = RegistrationPassword;
        if (login.Length == 0 || password.Length == 0)
        {
            ShowRegistrationMessage("Registration_MissingFields");
            return;
        }

        if (!string.Equals(password, RegistrationPasswordConfirm, StringComparison.Ordinal))
        {
            ShowRegistrationMessage("Registration_PasswordsDiffer");
            return;
        }

        PlatformDefinition platform = SelectedPlatform.Model;
        ClearRegistrationMessage();
        IsRegistering = true;
        _registrationCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _registrationCancellation = cancellation;
        try
        {
            AccountRegistrationResult result = await service
                .RegisterAsync(
                    new AccountRegistrationRequest(platform, login, password, RegistrationEmail),
                    cancellation.Token)
                .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                ShowRegistrationFailure(result);
                return;
            }

            await CompleteRegistrationAsync(result.Login ?? login, password).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The window is closing: nothing to report.
        }
        catch (Exception exception)
        {
            LogFailure(
                "registration.submit",
                "The account could not be created.",
                exception,
                ("platformId", platform.Id),
                ("login", login));
            ShowRegistrationMessage("Registration_Failed");
        }
        finally
        {
            if (ReferenceEquals(_registrationCancellation, cancellation))
            {
                _registrationCancellation = null;
            }

            cancellation.Dispose();
            IsRegistering = false;
        }
    }

    private async Task CompleteRegistrationAsync(string login, string password)
    {
        // The account is always saved as a NEW profile: whatever profile was selected on the main
        // screen (or edited) stays exactly as it was.
        bool keepTypedLabel = _registrationOpenedFromEditor &&
            SelectedProfile is null &&
            !string.IsNullOrWhiteSpace(ProfileLabel);
        SelectedProfile = null;
        ProfileLabel = keepTypedLabel ? ProfileLabel : login;
        LoginHint = login;
        PendingPassword = password;
        RememberPassword = true;
        IsRegistrationVisible = false;
        ResetRegistrationFields();

        await SaveProfileAsync().ConfigureAwait(true);

        // SaveProfileAsync closes the editor and selects the new profile; when it could not save
        // (it keeps the editor open), the account still exists and the player must be told.
        bool saved = SelectedProfile is not null && !IsProfileEditorVisible;
        if (!saved)
        {
            IsProfileEditorVisible = true;
        }

        SetStatusMessage(saved ? "Registration_Created" : "Registration_CreatedNotSaved", login);
        CatalogStatusBrush = saved ? OnlineBrush : WarningBrush;
    }

    private void ShowRegistrationFailure(AccountRegistrationResult result)
    {
        switch (result.ErrorCode)
        {
            case "registration_invalid_login":
                ShowRegistrationMessage("Registration_InvalidLogin");
                break;
            case "registration_invalid_password":
                ShowRegistrationMessage("Registration_InvalidPassword");
                break;
            case "registration_invalid_email":
                ShowRegistrationMessage("Registration_InvalidEmail");
                break;
            case "registration_login_taken":
                ShowRegistrationMessage("Registration_LoginTaken");
                break;
            case "registration_too_many_requests":
                ShowRegistrationMessage("Registration_TooManyRequests");
                break;
            case "registration_rejected":
                // The site explains exactly what it refused (reserved name, bad e-mail...).
                ShowRegistrationMessage("Registration_Rejected", result.ErrorMessage);
                break;
            case "network_error":
                ShowRegistrationMessage("Auth_NetworkError");
                break;
            case "request_timeout":
                ShowRegistrationMessage("Auth_Timeout");
                break;
            default:
                ShowRegistrationMessage("Registration_Failed");
                break;
        }
    }

    private void DisposeRegistration()
    {
        _registrationCancellation?.Cancel();
    }
}
