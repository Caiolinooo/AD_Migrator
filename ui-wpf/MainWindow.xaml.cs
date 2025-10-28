using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Diagnostics;

namespace MigracaoAD.UI;

public partial class MainWindow : Window
{
    private readonly State _state = new();

    public MainWindow()
    {
        InitializeComponent();
        StepsList.SelectionChanged += (_, __) => Navigate(StepsList.SelectedIndex);
        Navigate(0);
    }

    private void Navigate(int idx)
    {
        _state.CurrentStep = idx;
        RunBtn.Visibility = idx == 6 ? Visibility.Visible : Visibility.Collapsed;
        BackBtn.IsEnabled = idx > 0;
        NextBtn.IsEnabled = idx < 6;
        ContentFrame.Content = idx switch
        {
            0 => new Views.WelcomePage(_state),
            1 => new Views.EnvironmentPage(_state), // Agora inclui credenciais
            2 => new Views.AgentTestPage(_state),
            3 => new Views.ConnectivityPage(_state),
            4 => new Views.ModePage(_state),
            5 => new Views.FilesPage(_state),
            6 => new Views.SummaryPage(_state),
            _ => new Views.WelcomePage(_state)
        };
        StepsList.SelectedIndex = idx;
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e) => Navigate(_state.CurrentStep - 1);
    private void NextBtn_Click(object sender, RoutedEventArgs e) => Navigate(_state.CurrentStep + 1);
    private void RunBtn_Click(object sender, RoutedEventArgs e)
    {
        var win = new RunWindow(_state) { Owner = this };
        win.ShowDialog();
    }

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        var url = e.Uri?.ToString();
        if (!string.IsNullOrWhiteSpace(url))
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
        e.Handled = true;
    }
}

public class State
{
    public int CurrentStep { get; set; }
    // Configurações principais
    public string? SourceDomainName { get; set; }
    public string? TargetDomainName { get; set; }
    public string? SourceDcHost { get; set; }
    public string? TargetDcHost { get; set; }
    public string? SourceDcIp { get; set; }
    public string? TargetDcIp { get; set; }
    public string? SourceFileServer { get; set; }
    public string? DestinationFileServer { get; set; }

    public string ConnectivityMode { get; set; } = "Tunnel"; // Tunnel | Direct
    public int RpcPortMin { get; set; } = 49152;
    public int RpcPortMax { get; set; } = 49252;

    public bool ModeOptionA { get; set; } // Mesmo domínio
    public bool ModeOptionB { get; set; } = true; // Novo domínio (padrão)

    // Credenciais e Acesso Remoto
    public string? SourceUsername { get; set; }
    public string? SourcePassword { get; set; }
    public int SourceConnectionMethod { get; set; } = 0; // 0=WinRM, 1=SSH, 2=PSExec
    public bool SourceUseDomainCreds { get; set; } = true;

    public string? TargetUsername { get; set; }
    public string? TargetPassword { get; set; }
    public int TargetConnectionMethod { get; set; } = 0;
    public bool TargetUseDomainCreds { get; set; } = false;

    // Agente (novo sistema cliente-servidor)
    public string AgentToken { get; set; } = "default-token-change-me";
    public int AgentPort { get; set; } = 8765;
    public bool UseAgent { get; set; } = true; // Usar agente em vez de WinRM

    public bool AutoConfigureSource { get; set; } = true;
    public bool AutoConfigureTarget { get; set; } = true;

    public int WinRMPortHTTP { get; set; } = 5985;
    public int WinRMPortHTTPS { get; set; } = 5986;
    public int SSHPort { get; set; } = 22;
    public bool UseHTTPS { get; set; } = false;
    public bool SkipCACheck { get; set; } = true;
    public bool SkipCNCheck { get; set; } = true;
    public int ConnectionTimeout { get; set; } = 30;

    public bool UseDFSN { get; set; } = true;
    public bool UseDFSR { get; set; } = true;
    public bool UseRobocopy { get; set; } = true;

    // Aliases para compatibilidade com código existente
    public string? SourceDCIP => SourceDcIp;
    public string? TargetDCIP => TargetDcIp;
}

