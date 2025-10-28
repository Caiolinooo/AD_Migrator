using System.Windows.Controls;

namespace MigracaoAD.UI.Views;

public partial class WelcomePage : Page
{
    public WelcomePage(State state)
    {
        InitializeComponent();
        DataContext = state;
    }
}

