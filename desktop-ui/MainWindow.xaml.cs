using System.Windows;
using SecureCloud.Desktop.ViewModels;

namespace SecureCloud.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}