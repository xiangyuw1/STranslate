using CommunityToolkit.Mvvm.DependencyInjection;
using STranslate.ViewModels;
using System.ComponentModel;

namespace STranslate.Views;

public partial class WelcomeSetupWindow
{
    private readonly WelcomeSetupViewModel _viewModel;

    public WelcomeSetupWindow()
    {
        _viewModel = Ioc.Default.GetRequiredService<WelcomeSetupViewModel>();
        DataContext = _viewModel;

        InitializeComponent();
    }

    private void OnClosing(object? sender, CancelEventArgs e) => _viewModel.SaveAll();

    private void OnClosed(object? sender, EventArgs e) => _viewModel.Dispose();
}
