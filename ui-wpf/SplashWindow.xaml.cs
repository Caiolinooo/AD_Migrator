using System.Windows;

namespace MigracaoAD.UI;

using System.Diagnostics;
using System.Windows.Navigation;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        DataContext = App.Branding;
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        var url = e.Uri?.ToString();
        if (!string.IsNullOrWhiteSpace(url))
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
        e.Handled = true;
    }
}

