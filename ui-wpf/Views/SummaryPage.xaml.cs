using System.Windows.Controls;

namespace MigracaoAD.UI.Views;

public partial class SummaryPage : Page
{
    public SummaryPage(State state)
    {
        InitializeComponent();
        DataContext = state;
    }
}

