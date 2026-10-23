using Compare3DTrajectory.App.Services;

namespace Compare3DTrajectory.App.Tests.Shell;

internal sealed class StubConfirmation(bool answer) : IConfirmationService
{
    public int Asked { get; private set; }

    public bool Confirm(string title, string message, string confirm)
    {
        Asked++;
        return answer;
    }
}
