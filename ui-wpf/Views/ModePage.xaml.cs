using System.Windows.Controls;

namespace MigracaoAD.UI.Views;

public partial class ModePage : Page
{
    public ModePage(State state)
    {
        InitializeComponent();
        DataContext = state;
    }
}

