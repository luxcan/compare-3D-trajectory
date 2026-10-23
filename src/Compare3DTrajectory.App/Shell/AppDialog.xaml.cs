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

        dialog.AddButton(confirm, isConfirm: true);
        dialog.AddButton("Cancel", isConfirm: false);
        dialog.ShowDialog();
        return dialog._confirmed;
    }

    private void AddButton(string text, bool isConfirm)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 96,
            IsDefault = !isConfirm,
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
