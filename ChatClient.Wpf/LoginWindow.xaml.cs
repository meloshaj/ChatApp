using System.Windows;
using System.Windows.Controls;
using ChatClient.Wpf.Services;

namespace ChatClient.Wpf;

public partial class LoginWindow : Window
{
    private readonly AuthenticationClient authClient = new();
    private bool isRegistering = false;

    public LoginWindow()
    {
        InitializeComponent();
        UsernameTextBox.Focus();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        string host = HostTextBox.Text.Trim();
        string port = PortTextBox.Text.Trim();
        string username = UsernameTextBox.Text.Trim();
        string password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowStatus("Please enter both username and password.", isError: true);
            return;
        }

        if (password.Length < 6)
        {
            ShowStatus("Password must be at least 6 characters.", isError: true);
            return;
        }

        LoginButton.IsEnabled = false;
        LoginButton.Content = isRegistering ? "Creating Account..." : "Signing In...";

        try
        {
            var result = await authClient.AuthenticateAsync(host, port, username, password, isRegistering);

            if (result.Success)
            {
                // Open main window
                var mainWindow = new MainWindow(authClient, username);
                mainWindow.Show();
                this.Close();
            }
            else
            {
                ShowStatus(result.ErrorMessage ?? "Authentication failed.", isError: true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Connection error: {ex.Message}", isError: true);
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = isRegistering ? "Sign Up" : "Sign In";
        }
    }

    private void RegisterLink_Click(object sender, RoutedEventArgs e)
    {
        isRegistering = !isRegistering;

        if (isRegistering)
        {
            Title = "Chat App - Register";
            LoginButton.Content = "Sign Up";
            RegisterLink.Inlines.Clear();
            RegisterLink.Inlines.Add("Already have an account? ");
            var signInRun = new System.Windows.Documents.Run("Sign In") { FontWeight = FontWeights.SemiBold };
            RegisterLink.Inlines.Add(signInRun);
        }
        else
        {
            Title = "Chat App - Login";
            LoginButton.Content = "Sign In";
            RegisterLink.Inlines.Clear();
            RegisterLink.Inlines.Add("Don't have an account? ");
            var signUpRun = new System.Windows.Documents.Run("Sign Up") { FontWeight = FontWeights.SemiBold };
            RegisterLink.Inlines.Add(signUpRun);
        }

        StatusBorder.Visibility = Visibility.Collapsed;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusTextBlock.Text = message;
        StatusBorder.Background = isError ? 
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242)) : 
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244));
        StatusTextBlock.Foreground = isError ? 
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 27, 27)) : 
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52));
        StatusBorder.Visibility = Visibility.Visible;
    }
}
