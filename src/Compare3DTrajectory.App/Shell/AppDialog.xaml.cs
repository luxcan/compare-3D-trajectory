using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Compare3DTrajectory.App.Shell;

/// <summary>
/// The app's own modal question, following PhotoGallery's AppDialog: themed like everything else,
/// with buttons named after what they do, and visible to UI Automation as an ordinary window.
/// </summary>
public partial class AppDialog : Window
{
    private bool _confirmed;

    private AppDialog() => InitializeComponent();

    /// <summary>
    /// Asks and returns whether the user agreed. Cancel is the default button, so a stray Return
    /// never performs the act.
    /// </summary>
    public static bool Confirm(Window? owner, string title, string message, string confirm)
    {
        AppDialog dialog = Create(owner, title, message);
        dialog.AddButton(confirm, isConfirm: true, isDefault: false);
        dialog.AddButton("Cancel", isConfirm: false, isDefault: true);
        dialog.ShowDialog();
        return dialog._confirmed;
    }

    /// <summary>Shows a message with one button, centred over the owner, until the user closes it.</summary>
    public static void Inform(Window? owner, string title, string message)
    {
        AppDialog dialog = Create(owner, title, message);
        dialog.AddButton("OK", isConfirm: true, isDefault: true);
        dialog.ShowDialog();
    }

    private static AppDialog Create(Window? owner, string title, string message)
    {
        var dialog = new AppDialog
        {
            Title = title,
            TitleText = { Text = title },
            MessageText = { Text = message },
        };

        if (owner is not null && owner.IsLoaded)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog;
    }

    private void AddButton(string text, bool isConfirm, bool isDefault)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 96,
            IsDefault = isDefault,
        };

        button.SetResourceReference(StyleProperty, isConfirm ? "PrimaryButton" : "SecondaryButton");
        button.Click += (_, _) =>
        {
            _confirmed = isConfirm;
            Close();
        };

        Buttons.Children.Add(button);
    }

    /// <summary>Escape declines wherever the focus is.</summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _confirmed = false;
            Close();
        }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
