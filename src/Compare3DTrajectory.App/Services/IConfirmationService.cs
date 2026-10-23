namespace Compare3DTrajectory.App.Services;

/// <summary>Asks the user before something that would be tedious to undo.</summary>
public interface IConfirmationService
{
    /// <summary>
    /// Shows the question and returns whether the user agreed. <paramref name="confirm"/> labels the
    /// agreeing button with the act it performs, rather than "OK".
    /// </summary>
    bool Confirm(string title, string message, string confirm);
}
