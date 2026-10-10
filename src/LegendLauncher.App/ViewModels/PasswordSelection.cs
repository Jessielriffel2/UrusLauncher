namespace LegendLauncher.App.ViewModels;

/// <summary>
/// Picks the password the user actually typed when the window has more than one password box
/// (the one inside the account editor and the one in the session card).
/// </summary>
internal static class PasswordSelection
{
    /// <summary>
    /// Returns the session-card password when it has text, otherwise the editor password, otherwise
    /// <see langword="null"/> to mean "nothing typed, keep whatever the view model already holds".
    /// Never returns an empty string: an untouched box must not erase a password typed elsewhere.
    /// </summary>
    public static string? FromBoxes(string? sessionCardPassword, string? editorPassword)
    {
        if (!string.IsNullOrEmpty(sessionCardPassword))
        {
            return sessionCardPassword;
        }

        return string.IsNullOrEmpty(editorPassword) ? null : editorPassword;
    }
}
