using System.Windows.Controls;

namespace MigracaoAD.UI.Views;

public partial class ConnectivityPage : Page
{
    public ConnectivityPage(State state)
    {
        InitializeComponent();
        DataContext = state;
    }
}

