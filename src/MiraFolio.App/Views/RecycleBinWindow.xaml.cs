using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MiraFolio.App.ViewModels;

namespace MiraFolio.App.Views;

public partial class RecycleBinWindow : Window
{
    public RecycleBinWindow()
    {
        InitializeComponent();
        DataContext = ((App)Application.Current).Services?.GetRequiredService<RecycleBinViewModel>();
    }
}
