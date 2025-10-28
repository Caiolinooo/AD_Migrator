using System.Windows.Controls;

namespace MigracaoAD.UI.Views;

public partial class FilesPage : Page
{
    public FilesPage(State state)
    {
        InitializeComponent();
        DataContext = state;
    }
}

