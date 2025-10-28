using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using MigracaoAD.UI.Services;
using System.Diagnostics;
using System.IO;

namespace MigracaoAD.UI.Views;

public partial class AgentTestPage : Page
{
    private readonly State _state;

    public AgentTestPage(State state)
    {
        InitializeComponent();
        DataContext = _state = state;
    }

    // Testar conexão com servidor origem
    private async void TestSourceConnection_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.SourceDcIp))
        {
            MessageBox.Show("Por favor, preencha o IP do servidor origem.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SourceStatus.Text = "🔄 Testando...";
        SourceOutput.Text = "Conectando ao agente...\n";

        try
        {
            var client = new AgentClient(_state.SourceDcIp, _state.AgentPort, _state.AgentToken);
            var health = await client.CheckHealthAsync();

            if (health != null)
            {
                SourceStatus.Text = "✅ Conectado";
                SourceOutput.Text = $"✅ CONECTADO com sucesso!\n\n";
                SourceOutput.Text += $"Hostname: {health.Hostname}\n";
                SourceOutput.Text += $"OS: {health.Os}\n";
                SourceOutput.Text += $"Versão: {health.Version}\n";
                SourceOutput.Text += $"Status: {health.Status}\n";
                SourceOutput.Text += $"Timestamp: {health.Timestamp:yyyy-MM-dd HH:mm:ss}\n";
            }
            else
            {
                SourceStatus.Text = "❌ Falhou";
                SourceOutput.Text = "❌ Não foi possível conectar ao agente.\n\n";
                SourceOutput.Text += "Verifique:\n";
                SourceOutput.Text += $"- IP: {_state.SourceDcIp}\n";
                SourceOutput.Text += $"- Porta: {_state.AgentPort}\n";
                SourceOutput.Text += $"- Token: {_state.AgentToken.Substring(0, Math.Min(10, _state.AgentToken.Length))}...\n";
                SourceOutput.Text += "- Agente instalado e rodando\n";
                SourceOutput.Text += "- Firewall liberado\n";
            }
        }
        catch (Exception ex)
        {
            SourceStatus.Text = "❌ Erro";
            SourceOutput.Text = $"❌ Erro: {ex.Message}\n";
        }
    }

    // Obter informações do servidor origem
    private async void GetSourceInfo_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.SourceDcIp))
        {
            MessageBox.Show("Por favor, preencha o IP do servidor origem.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SourceOutput.Text = "Obtendo informações do sistema...\n";

        try
        {
            var client = new AgentClient(_state.SourceDcIp, _state.AgentPort, _state.AgentToken);
            
            var sysInfo = await client.GetSystemInfoAsync();
            if (sysInfo != null)
            {
                SourceOutput.Text = "📊 INFORMAÇÕES DO SISTEMA\n\n";
                SourceOutput.Text += $"Hostname: {sysInfo.Hostname}\n";
                SourceOutput.Text += $"Windows: {sysInfo.WindowsVersion}\n";
                SourceOutput.Text += $"OS: {sysInfo.OS}\n";
                SourceOutput.Text += $"Domínio: {sysInfo.Domain}\n";
                SourceOutput.Text += $"Usuário: {sysInfo.Username}\n";
                SourceOutput.Text += $"Arquitetura: {(sysInfo.Is64Bit ? "64-bit" : "32-bit")}\n";
                SourceOutput.Text += $"CPU: {sysInfo.ProcessorCount} cores\n";
                SourceOutput.Text += $"RAM: {sysInfo.TotalMemoryGB} GB\n\n";
                
                if (sysInfo.InstalledRoles.Count > 0)
                {
                    SourceOutput.Text += $"Roles Instaladas ({sysInfo.InstalledRoles.Count}):\n";
                    foreach (var role in sysInfo.InstalledRoles.Take(20))
                    {
                        SourceOutput.Text += $"  - {role}\n";
                    }
                    if (sysInfo.InstalledRoles.Count > 20)
                        SourceOutput.Text += $"  ... e mais {sysInfo.InstalledRoles.Count - 20}\n";
                }
            }

            var domainInfo = await client.GetDomainInfoAsync();
            if (domainInfo != null && domainInfo.IsDomainController)
            {
                SourceOutput.Text += $"\n🏢 INFORMAÇÕES DO DOMÍNIO\n\n";
                SourceOutput.Text += $"É Domain Controller: Sim\n";
                SourceOutput.Text += $"Domínio: {domainInfo.DomainName}\n";
                SourceOutput.Text += $"Floresta: {domainInfo.ForestName}\n";
                SourceOutput.Text += $"Nível Funcional: {domainInfo.FunctionalLevel}\n";
                SourceOutput.Text += $"Usuários: {domainInfo.UserCount}\n";
                SourceOutput.Text += $"Grupos: {domainInfo.GroupCount}\n";
            }

            var diskInfo = await client.GetDiskInfoAsync();
            if (diskInfo != null && diskInfo.Drives.Count > 0)
            {
                SourceOutput.Text += $"\n💾 DISCOS\n\n";
                foreach (var drive in diskInfo.Drives)
                {
                    var total = drive.UsedGB + drive.FreeGB;
                    SourceOutput.Text += $"{drive.Name}: {drive.UsedGB:F2} GB usado / {total:F2} GB total\n";
                }
            }
        }
        catch (Exception ex)
        {
            SourceOutput.Text = $"❌ Erro: {ex.Message}\n";
        }
    }

    // Executar comando no servidor origem
    private async void ExecuteSourceCommand_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.SourceDcIp))
        {
            MessageBox.Show("Por favor, preencha o IP do servidor origem.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var command = Microsoft.VisualBasic.Interaction.InputBox(
            "Digite o comando PowerShell para executar:",
            "Executar Comando",
            "Get-Service | Select-Object -First 10 | Format-Table Name, Status"
        );

        if (string.IsNullOrWhiteSpace(command))
            return;

        SourceOutput.Text = $"Executando: {command}\n\n";

        try
        {
            var client = new AgentClient(_state.SourceDcIp, _state.AgentPort, _state.AgentToken);
            var result = await client.ExecuteCommandAsync(command);

            if (result != null)
            {
                if (result.Success)
                {
                    SourceOutput.Text += $"✅ Sucesso (Exit Code: {result.ExitCode})\n\n";
                    SourceOutput.Text += $"OUTPUT:\n{result.Output}\n";
                }
                else
                {
                    SourceOutput.Text += $"❌ Falhou (Exit Code: {result.ExitCode})\n\n";
                    SourceOutput.Text += $"ERROR:\n{result.Error}\n";
                }
            }
        }
        catch (Exception ex)
        {
            SourceOutput.Text += $"❌ Erro: {ex.Message}\n";
        }
    }

    // Métodos para servidor destino (similares aos de origem)
    private async void TestTargetConnection_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.TargetDcIp))
        {
            MessageBox.Show("Por favor, preencha o IP do servidor destino.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TargetStatus.Text = "🔄 Testando...";
        TargetOutput.Text = "Conectando ao agente...\n";

        try
        {
            var client = new AgentClient(_state.TargetDcIp, _state.AgentPort, _state.AgentToken);
            var health = await client.CheckHealthAsync();

            if (health != null)
            {
                TargetStatus.Text = "✅ Conectado";
                TargetOutput.Text = $"✅ CONECTADO com sucesso!\n\n";
                TargetOutput.Text += $"Hostname: {health.Hostname}\n";
                TargetOutput.Text += $"OS: {health.Os}\n";
                TargetOutput.Text += $"Versão: {health.Version}\n";
                TargetOutput.Text += $"Status: {health.Status}\n";
                TargetOutput.Text += $"Timestamp: {health.Timestamp:yyyy-MM-dd HH:mm:ss}\n";
            }
            else
            {
                TargetStatus.Text = "❌ Falhou";
                TargetOutput.Text = "❌ Não foi possível conectar ao agente.\n\n";
                TargetOutput.Text += "Verifique:\n";
                TargetOutput.Text += $"- IP: {_state.TargetDcIp}\n";
                TargetOutput.Text += $"- Porta: {_state.AgentPort}\n";
                TargetOutput.Text += $"- Token: {_state.AgentToken.Substring(0, Math.Min(10, _state.AgentToken.Length))}...\n";
                TargetOutput.Text += "- Agente instalado e rodando\n";
                TargetOutput.Text += "- Firewall liberado\n";
            }
        }
        catch (Exception ex)
        {
            TargetStatus.Text = "❌ Erro";
            TargetOutput.Text = $"❌ Erro: {ex.Message}\n";
        }
    }

    private async void GetTargetInfo_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.TargetDcIp))
        {
            MessageBox.Show("Por favor, preencha o IP do servidor destino.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TargetOutput.Text = "Obtendo informações do sistema...\n";

        try
        {
            var client = new AgentClient(_state.TargetDcIp, _state.AgentPort, _state.AgentToken);
            
            var sysInfo = await client.GetSystemInfoAsync();
            if (sysInfo != null)
            {
                TargetOutput.Text = "📊 INFORMAÇÕES DO SISTEMA\n\n";
                TargetOutput.Text += $"Hostname: {sysInfo.Hostname}\n";
                TargetOutput.Text += $"Windows: {sysInfo.WindowsVersion}\n";
                TargetOutput.Text += $"OS: {sysInfo.OS}\n";
                TargetOutput.Text += $"Domínio: {sysInfo.Domain}\n";
                TargetOutput.Text += $"Usuário: {sysInfo.Username}\n";
                TargetOutput.Text += $"Arquitetura: {(sysInfo.Is64Bit ? "64-bit" : "32-bit")}\n";
                TargetOutput.Text += $"CPU: {sysInfo.ProcessorCount} cores\n";
                TargetOutput.Text += $"RAM: {sysInfo.TotalMemoryGB} GB\n\n";
                
                if (sysInfo.InstalledRoles.Count > 0)
                {
                    TargetOutput.Text += $"Roles Instaladas ({sysInfo.InstalledRoles.Count}):\n";
                    foreach (var role in sysInfo.InstalledRoles.Take(20))
                    {
                        TargetOutput.Text += $"  - {role}\n";
                    }
                    if (sysInfo.InstalledRoles.Count > 20)
                        TargetOutput.Text += $"  ... e mais {sysInfo.InstalledRoles.Count - 20}\n";
                }
            }

            var domainInfo = await client.GetDomainInfoAsync();
            if (domainInfo != null && domainInfo.IsDomainController)
            {
                TargetOutput.Text += $"\n🏢 INFORMAÇÕES DO DOMÍNIO\n\n";
                TargetOutput.Text += $"É Domain Controller: Sim\n";
                TargetOutput.Text += $"Domínio: {domainInfo.DomainName}\n";
                TargetOutput.Text += $"Floresta: {domainInfo.ForestName}\n";
                TargetOutput.Text += $"Nível Funcional: {domainInfo.FunctionalLevel}\n";
                TargetOutput.Text += $"Usuários: {domainInfo.UserCount}\n";
                TargetOutput.Text += $"Grupos: {domainInfo.GroupCount}\n";
            }

            var diskInfo = await client.GetDiskInfoAsync();
            if (diskInfo != null && diskInfo.Drives.Count > 0)
            {
                TargetOutput.Text += $"\n💾 DISCOS\n\n";
                foreach (var drive in diskInfo.Drives)
                {
                    var total = drive.UsedGB + drive.FreeGB;
                    TargetOutput.Text += $"{drive.Name}: {drive.UsedGB:F2} GB usado / {total:F2} GB total\n";
                }
            }
        }
        catch (Exception ex)
        {
            TargetOutput.Text = $"❌ Erro: {ex.Message}\n";
        }
    }

    private async void ExecuteTargetCommand_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.TargetDcIp))
        {
            MessageBox.Show("Por favor, preencha o IP do servidor destino.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var command = Microsoft.VisualBasic.Interaction.InputBox(
            "Digite o comando PowerShell para executar:",
            "Executar Comando",
            "Get-Service | Select-Object -First 10 | Format-Table Name, Status"
        );

        if (string.IsNullOrWhiteSpace(command))
            return;

        TargetOutput.Text = $"Executando: {command}\n\n";

        try
        {
            var client = new AgentClient(_state.TargetDcIp, _state.AgentPort, _state.AgentToken);
            var result = await client.ExecuteCommandAsync(command);

            if (result != null)
            {
                if (result.Success)
                {
                    TargetOutput.Text += $"✅ Sucesso (Exit Code: {result.ExitCode})\n\n";
                    TargetOutput.Text += $"OUTPUT:\n{result.Output}\n";
                }
                else
                {
                    TargetOutput.Text += $"❌ Falhou (Exit Code: {result.ExitCode})\n\n";
                    TargetOutput.Text += $"ERROR:\n{result.Error}\n";
                }
            }
        }
        catch (Exception ex)
        {
            TargetOutput.Text += $"❌ Erro: {ex.Message}\n";
        }
    }

    // Testes avançados
    private async void TestBoth_Click(object sender, RoutedEventArgs e)
    {
        AdvancedOutput.Text = "Testando ambos os servidores...\n\n";

        var tasks = new[]
        {
            TestServerAsync("ORIGEM", _state.SourceDcIp),
            TestServerAsync("DESTINO", _state.TargetDcIp)
        };

        var results = await Task.WhenAll(tasks);
        AdvancedOutput.Text += string.Join("\n", results);
    }

    private async Task<string> TestServerAsync(string name, string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return $"❌ {name}: IP não configurado\n";

        try
        {
            var client = new AgentClient(ip, _state.AgentPort, _state.AgentToken);
            var health = await client.CheckHealthAsync();
            
            if (health != null)
                return $"✅ {name}: {health.Hostname} - {health.Os}\n";
            else
                return $"❌ {name}: Não foi possível conectar\n";
        }
        catch (Exception ex)
        {
            return $"❌ {name}: {ex.Message}\n";
        }
    }

    private async void TestInterServer_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.SourceDcIp) || string.IsNullOrWhiteSpace(_state.TargetDcIp))
        {
            MessageBox.Show("Configure os IPs de ambos os servidores primeiro.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AdvancedOutput.Text = "Testando conectividade entre servidores...\n\n";

        try
        {
            // Origem -> Destino
            var sourceClient = new AgentClient(_state.SourceDcIp, _state.AgentPort, _state.AgentToken);
            var sourceToTarget = await sourceClient.TestConnectionAsync(_state.TargetDcIp, 445);
            
            AdvancedOutput.Text += $"ORIGEM → DESTINO:\n";
            if (sourceToTarget != null && sourceToTarget.Success)
                AdvancedOutput.Text += $"✅ Conectividade OK\n{sourceToTarget.Output}\n\n";
            else
                AdvancedOutput.Text += $"❌ Falhou\n{sourceToTarget?.Error}\n\n";

            // Destino -> Origem
            var targetClient = new AgentClient(_state.TargetDcIp, _state.AgentPort, _state.AgentToken);
            var targetToSource = await targetClient.TestConnectionAsync(_state.SourceDcIp, 445);
            
            AdvancedOutput.Text += $"DESTINO → ORIGEM:\n";
            if (targetToSource != null && targetToSource.Success)
                AdvancedOutput.Text += $"✅ Conectividade OK\n{targetToSource.Output}\n";
            else
                AdvancedOutput.Text += $"❌ Falhou\n{targetToSource?.Error}\n";
        }
        catch (Exception ex)
        {
            AdvancedOutput.Text += $"❌ Erro: {ex.Message}\n";
        }
    }

    private async void ListShares_Click(object sender, RoutedEventArgs e)
    {
        AdvancedOutput.Text = "Listando compartilhamentos...\n\n";

        try
        {
            if (!string.IsNullOrWhiteSpace(_state.SourceDcIp))
            {
                var sourceClient = new AgentClient(_state.SourceDcIp, _state.AgentPort, _state.AgentToken);
                var sourceShares = await sourceClient.GetSharesAsync();
                
                AdvancedOutput.Text += $"📁 ORIGEM ({_state.SourceDcIp}):\n";
                if (sourceShares != null && sourceShares.Count > 0)
                {
                    foreach (var share in sourceShares)
                        AdvancedOutput.Text += $"  - {share}\n";
                }
                else
                {
                    AdvancedOutput.Text += "  (nenhum compartilhamento)\n";
                }
                AdvancedOutput.Text += "\n";
            }

            if (!string.IsNullOrWhiteSpace(_state.TargetDcIp))
            {
                var targetClient = new AgentClient(_state.TargetDcIp, _state.AgentPort, _state.AgentToken);
                var targetShares = await targetClient.GetSharesAsync();
                
                AdvancedOutput.Text += $"📁 DESTINO ({_state.TargetDcIp}):\n";
                if (targetShares != null && targetShares.Count > 0)
                {
                    foreach (var share in targetShares)
                        AdvancedOutput.Text += $"  - {share}\n";
                }
                else
                {
                    AdvancedOutput.Text += "  (nenhum compartilhamento)\n";
                }
            }
        }
        catch (Exception ex)
        {
            AdvancedOutput.Text += $"❌ Erro: {ex.Message}\n";
        }
    }

    private void OpenGuide_Click(object sender, RoutedEventArgs e)
    {
        var guidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "GUIA_AGENTE.md");
        guidePath = Path.GetFullPath(guidePath);

        if (File.Exists(guidePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(guidePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir guia:\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show($"Guia não encontrado.\n\nProcure por GUIA_AGENTE.md na raiz do projeto.", 
                "Arquivo não encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

