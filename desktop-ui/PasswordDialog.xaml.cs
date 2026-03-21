using System.Windows;

namespace SecureCloud.Desktop
{
    public partial class PasswordDialog : Window
    {
        public string Password { get; private set; } = "";

        public PasswordDialog()
        {
            InitializeComponent();
            PasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordBox.Password;
            
            if (string.IsNullOrEmpty(Password))
            {
                MessageBox.Show("Please enter a password or click 'Skip' to continue without encryption.", 
                    "Password Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Password.Length < 8)
            {
                MessageBox.Show("Password must be at least 8 characters long for security.", 
                    "Password Too Short", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            Password = "";
            DialogResult = false;
            Close();
        }
    }
}