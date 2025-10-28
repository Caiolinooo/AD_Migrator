using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Threading.Tasks;
using MigracaoAD.UI.Services;
using System.Diagnostics;
using System.IO;

namespace MigracaoAD.UI.Views;

public partial class EnvironmentPage : Page
{
    private readonly State _state;
    private readonly NetworkDiscoveryService _discoveryService;

    public EnvironmentPage(State state)
    {
        InitializeComponent();
        DataContext = _state = state;
        _discoveryService = new NetworkDiscoveryService();

        // Restaurar senhas se já foram preenchidas
        if (!string.IsNullOrEmpty(_state.SourcePassword))
            SourcePasswordBox.Password = _state.SourcePassword;
        if (!string.IsNullOrEmpty(_state.TargetPassword))
            TargetPasswordBox.Password = _state.TargetPassword;

        // Auto-detectar ao carregar a página
        Loaded += async (s, e) => await AutoDetectAsync();
    }

    private void SourcePassword_Changed(object sender, RoutedEventArgs e)
    {
        _state.SourcePassword = SourcePasswordBox.Password;
    }

    private void TargetPassword_Changed(object sender, RoutedEventArgs e)
    {
        _state.TargetPassword = TargetPasswordBox.Password;
    }

    private async Task AutoDetectAsync()
    {
        try
        {
            AutoDetectBtn.IsEnabled = false;
            DetectOut.Text = "🔍 Detectando rede automaticamente...\n";

            var result = await _discoveryService.DiscoverNetworkAsync();

            if (result.Success && result.IsJoinedToDomain)
            {
                DetectOut.Text += $"✅ Domínio detectado: {result.CurrentDomain}\n";
                DetectOut.Text += $"✅ DC atual: {result.CurrentDomainController}\n";
                DetectOut.Text += $"✅ IP do DC: {result.CurrentDcIp}\n";
                DetectOut.Text += $"✅ IP local: {result.LocalIpAddress}\n";
                DetectOut.Text += $"✅ Nome do computador: {result.LocalComputerName}\n\n";

                // Preencher automaticamente os campos de origem
                if (string.IsNullOrWhiteSpace(_state.SourceDomainName))
                    _state.SourceDomainName = result.CurrentDomain;

                if (string.IsNullOrWhiteSpace(_state.SourceDcHost))
                    _state.SourceDcHost = result.CurrentDomainController;

                if (string.IsNullOrWhiteSpace(_state.SourceDcIp))
                    _state.SourceDcIp = result.CurrentDcIp;

                if (result.AvailableDomainControllers.Count > 0)
                {
                    DetectOut.Text += $"📋 DCs disponíveis no domínio:\n";
                    foreach (var dc in result.AvailableDomainControllers)
                    {
                        DetectOut.Text += $"   • {dc}\n";
                    }
                }

                DetectOut.Text += "\n💡 Preencha os dados do servidor destino (nuvem) manualmente ou use 'Escanear Rede'.\n";
            }
            else if (!result.IsJoinedToDomain)
            {
                DetectOut.Text += $"⚠️ Este computador não está em um domínio AD.\n";
                DetectOut.Text += $"✅ IP local: {result.LocalIpAddress}\n";
                DetectOut.Text += $"✅ Nome do computador: {result.LocalComputerName}\n\n";
                DetectOut.Text += "💡 Preencha os dados manualmente ou use 'Escanear Rede'.\n";
            }
            else
            {
                DetectOut.Text += $"❌ Erro: {result.ErrorMessage}\n";
            }
        }
        catch (Exception ex)
        {
            DetectOut.Text += $"❌ Erro na detecção automática: {ex.Message}\n";
        }
        finally
        {
            AutoDetectBtn.IsEnabled = true;
        }
    }

    private async void AutoDetect_Click(object sender, RoutedEventArgs e)
    {
        await AutoDetectAsync();
    }

    private async void ScanNetwork_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = MessageBox.Show(
                "Deseja escanear a rede local em busca de servidores?\n\n" +
                "Isso pode levar alguns minutos e irá:\n" +
                "• Escanear todos os IPs da sua sub-rede\n" +
                "• Testar portas LDAP (389) para identificar DCs\n\n" +
                "Continuar?",
                "Escanear Rede",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            DetectOut.Text = "🌐 Escaneando rede local...\n";
            DetectOut.Text += "⏳ Isso pode levar alguns minutos...\n\n";

            var discovery = await _discoveryService.DiscoverNetworkAsync();
            var baseIp = discovery.LocalIpAddress ?? "192.168.1.1";

            DetectOut.Text += $"📡 Escaneando sub-rede baseada em: {baseIp}\n\n";

            var servers = await _discoveryService.ScanSubnetForServersAsync(baseIp, 100);
            DetectOut.Text += $"✅ Encontrados {servers.Count} servidores ativos:\n";

            foreach (var server in servers.Take(20)) // Limitar a 20 para não poluir
            {
                DetectOut.Text += $"   • {server}\n";
            }

            if (servers.Count > 20)
            {
                DetectOut.Text += $"   ... e mais {servers.Count - 20} servidores\n";
            }

            DetectOut.Text += "\n🔍 Testando portas LDAP (389) para identificar DCs...\n";
            var dcs = await _discoveryService.DiscoverDomainControllersInSubnetAsync(baseIp);

            if (dcs.Count > 0)
            {
                DetectOut.Text += $"\n✅ Possíveis Domain Controllers encontrados:\n";

                // Limpar lista anterior
                DetectedIPsList.Children.Clear();
                DetectedIPsPanel.Visibility = Visibility.Visible;

                foreach (var dc in dcs)
                {
                    DetectOut.Text += $"   🖥️ {dc}\n";

                    // Adicionar botão de copiar
                    var btn = new Button
                    {
                        Content = $"📋 {dc} - Clique para Copiar",
                        Margin = new Thickness(0, 4, 0, 4),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Tag = dc
                    };
                    btn.Click += (s, ev) =>
                    {
                        var ip = (s as Button)?.Tag?.ToString();
                        if (!string.IsNullOrEmpty(ip))
                        {
                            Clipboard.SetText(ip);
                            MessageBox.Show($"IP {ip} copiado para a área de transferência!", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    };
                    DetectedIPsList.Children.Add(btn);
                }

                DetectOut.Text += "\n💡 Clique em um botão acima para copiar o IP automaticamente.\n";
            }
            else
            {
                DetectOut.Text += "\n⚠️ Nenhum Domain Controller encontrado na rede local.\n";
                DetectOut.Text += "💡 Verifique se o servidor está na mesma sub-rede ou preencha manualmente.\n";
                DetectedIPsPanel.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            DetectOut.Text += $"\n❌ Erro ao escanear rede: {ex.Message}\n";
        }
    }

    private async void Detect_Click(object sender, RoutedEventArgs e)
    {
        DetectOut.Text = "Detectando...";
        var sb = new StringBuilder();
        sb.AppendLine($"Host local: {System.Environment.MachineName} - {System.Environment.OSVersion}");

        async Task AppendFor(string? label, string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) { sb.AppendLine($"{label}: IP não informado"); return; }
            var json = await PowershellService.DetectRemoteAsync(ip);
            sb.AppendLine($"{label} ({ip}):");
            try
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string os = root.TryGetProperty("OS", out var v) ? v.GetString() ?? "" : json;
                string ver = root.TryGetProperty("Version", out var vv) ? vv.GetString() ?? "" : "";
                string host = root.TryGetProperty("Host", out var vh) ? vh.GetString() ?? "" : "";
                string ports = root.TryGetProperty("ReachablePorts", out var rp) ? rp.GetString() ?? "" : "";
                sb.AppendLine($"  Hostname: {host}");
                sb.AppendLine($"  OS: {os} {ver}");
                sb.AppendLine($"  Portas OK: {ports}");
            }
            catch { sb.AppendLine("  " + json); }
        }

        await AppendFor("DC origem", _state.SourceDcIp);
        await AppendFor("DC destino", _state.TargetDcIp);

        sb.AppendLine();
        sb.AppendLine("Dica: Se falhar, verifique WinRM (5985/5986), RPC (135 + faixa), LDAP/Kerberos/DNS e permissões.");
        DetectOut.Text = sb.ToString();
    }

    private async void ConfigureSource_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.SourceDcIp))
        {
            MessageBox.Show("Informe o IP do servidor local primeiro.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_state.SourceUsername) || string.IsNullOrWhiteSpace(_state.SourcePassword))
        {
            MessageBox.Show("Configure as credenciais na etapa anterior (Credenciais & Acesso).", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Configurar automaticamente o servidor local ({_state.SourceDcIp})?\n\n" +
            "Isso irá:\n" +
            "• Habilitar WinRM (se necessário)\n" +
            "• Abrir portas do firewall (AD, RPC, SMB, etc.)\n" +
            "• Instalar roles (AD DS, DFS, File Server)\n\n" +
            "Deseja continuar?",
            "Confirmar Configuração",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result != MessageBoxResult.Yes)
            return;

        ConfigureSourceBtn.IsEnabled = false;
        DetectOut.Text = "⏳ Configurando servidor local...\n";

        try
        {
            var remoteConfig = new RemoteConfigService(_state);

            // Testar conexão primeiro
            var testResult = await remoteConfig.TestConnectionAsync(
                _state.SourceDcIp,
                _state.SourceUsername,
                _state.SourcePassword,
                (ConnectionMethod)_state.SourceConnectionMethod,
                _state.SourceUseDomainCreds ? _state.SourceDomainName : null
            );

            if (!testResult.Success)
            {
                DetectOut.Text += $"❌ Falha na conexão: {testResult.Message}\n";
                DetectOut.Text += $"💡 Dica: {testResult.Hint}\n";
                MessageBox.Show($"Não foi possível conectar ao servidor:\n\n{testResult.Message}\n\nDica: {testResult.Hint}",
                    "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DetectOut.Text += $"✅ Conectado: {testResult.Message}\n";
            DetectOut.Text += "⏳ Aplicando configurações...\n";

            // Configurar servidor
            var configOptions = new ServerConfigOptions
            {
                OpenFirewallPorts = true,
                InstallRoles = true,
                ConfigureNetwork = false // Não alterar IP do servidor local
            };

            var configResult = await remoteConfig.ConfigureServerAsync(
                _state.SourceDcIp,
                _state.SourceUsername,
                _state.SourcePassword,
                (ConnectionMethod)_state.SourceConnectionMethod,
                _state.SourceUseDomainCreds ? _state.SourceDomainName : null,
                configOptions
            );

            if (configResult.Success)
            {
                DetectOut.Text += $"✅ Configuração concluída!\n{configResult.Message}\n";
                MessageBox.Show("Servidor local configurado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                DetectOut.Text += $"❌ Erro: {configResult.Message}\n";
                MessageBox.Show($"Erro ao configurar servidor:\n\n{configResult.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            DetectOut.Text += $"❌ Exceção: {ex.Message}\n";
            MessageBox.Show($"Erro inesperado:\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ConfigureSourceBtn.IsEnabled = true;
        }
    }

    private async void ConfigureTarget_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.TargetDcIp))
        {
            MessageBox.Show("Informe o IP do servidor nuvem primeiro.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_state.TargetUsername) || string.IsNullOrWhiteSpace(_state.TargetPassword))
        {
            MessageBox.Show("Configure as credenciais na etapa anterior (Credenciais & Acesso).", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Perguntar configurações de rede
        var networkDialog = new NetworkConfigDialog(_state.TargetDcIp);
        if (networkDialog.ShowDialog() != true)
            return;

        ConfigureTargetBtn.IsEnabled = false;
        DetectOut.Text = "⏳ Configurando servidor nuvem...\n";

        try
        {
            var remoteConfig = new RemoteConfigService(_state);

            // Testar conexão primeiro
            var testResult = await remoteConfig.TestConnectionAsync(
                _state.TargetDcIp,
                _state.TargetUsername,
                _state.TargetPassword,
                (ConnectionMethod)_state.TargetConnectionMethod,
                _state.TargetUseDomainCreds ? _state.TargetDomainName : null
            );

            if (!testResult.Success)
            {
                DetectOut.Text += $"❌ Falha na conexão: {testResult.Message}\n";
                DetectOut.Text += $"💡 Dica: {testResult.Hint}\n";
                MessageBox.Show($"Não foi possível conectar ao servidor:\n\n{testResult.Message}\n\nDica: {testResult.Hint}",
                    "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DetectOut.Text += $"✅ Conectado: {testResult.Message}\n";
            DetectOut.Text += "⏳ Aplicando configurações...\n";

            // Configurar servidor
            var configOptions = new ServerConfigOptions
            {
                ConfigureNetwork = networkDialog.ConfigureNetwork,
                IPAddress = networkDialog.IPAddress,
                PrefixLength = networkDialog.PrefixLength,
                Gateway = networkDialog.Gateway,
                DNSServer = networkDialog.DNSServer,
                OpenFirewallPorts = true,
                InstallRoles = true,
                PrepareDisk = networkDialog.PrepareDisk,
                DiskNumber = networkDialog.DiskNumber,
                DriveLetter = networkDialog.DriveLetter
            };

            var configResult = await remoteConfig.ConfigureServerAsync(
                _state.TargetDcIp,
                _state.TargetUsername,
                _state.TargetPassword,
                (ConnectionMethod)_state.TargetConnectionMethod,
                _state.TargetUseDomainCreds ? _state.TargetDomainName : null,
                configOptions
            );

            if (configResult.Success)
            {
                DetectOut.Text += $"✅ Configuração concluída!\n{configResult.Message}\n";
                MessageBox.Show("Servidor nuvem configurado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                DetectOut.Text += $"❌ Erro: {configResult.Message}\n";
                MessageBox.Show($"Erro ao configurar servidor:\n\n{configResult.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            DetectOut.Text += $"❌ Exceção: {ex.Message}\n";
            MessageBox.Show($"Erro inesperado:\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ConfigureTargetBtn.IsEnabled = true;
        }
    }

    // Testar conexão com agente no servidor origem
    private async void TestAgentSource_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.SourceDcIp))
        {
            MessageBox.Show("Por favor, preencha o IP do servidor origem primeiro.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DetectOut.Text = "🔌 Testando conexão com agente no servidor ORIGEM...\n";

        try
        {
            var client = new AgentClient(_state.SourceDcIp, _state.AgentPort, _state.AgentToken);
            var health = await client.CheckHealthAsync();

            if (health != null)
            {
                DetectOut.Text += $"✅ CONECTADO com sucesso!\n";
                DetectOut.Text += $"   Hostname: {health.Hostname}\n";
                DetectOut.Text += $"   OS: {health.Os}\n";
                DetectOut.Text += $"   Versão do agente: {health.Version}\n";
                DetectOut.Text += $"   Status: {health.Status}\n\n";

                // Obter informações detalhadas
                var sysInfo = await client.GetSystemInfoAsync();
                if (sysInfo != null)
                {
                    DetectOut.Text += $"📊 Informações do Sistema:\n";
                    DetectOut.Text += $"   Windows: {sysInfo.WindowsVersion}\n";
                    DetectOut.Text += $"   Domínio: {sysInfo.Domain}\n";
                    DetectOut.Text += $"   CPU: {sysInfo.ProcessorCount} cores\n";
                    DetectOut.Text += $"   RAM: {sysInfo.TotalMemoryGB} GB\n";
                    DetectOut.Text += $"   Roles instaladas: {sysInfo.InstalledRoles.Count}\n";
                }

                MessageBox.Show($"Agente conectado com sucesso!\n\nHostname: {health.Hostname}\nOS: {health.Os}",
                    "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                DetectOut.Text += $"❌ Não foi possível conectar ao agente.\n";
                DetectOut.Text += $"   Verifique se:\n";
                DetectOut.Text += $"   - O agente está instalado e rodando\n";
                DetectOut.Text += $"   - O IP está correto: {_state.SourceDcIp}\n";
                DetectOut.Text += $"   - A porta está correta: {_state.AgentPort}\n";
                DetectOut.Text += $"   - O firewall está liberado\n";

                MessageBox.Show($"Não foi possível conectar ao agente no servidor origem.\n\nVerifique se o agente está instalado e rodando.",
                    "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            DetectOut.Text += $"❌ Erro: {ex.Message}\n";
            MessageBox.Show($"Erro ao testar conexão:\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Testar conexão com agente no servidor destino
    private async void TestAgentTarget_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.TargetDcIp))
        {
            MessageBox.Show("Por favor, preencha o IP do servidor destino primeiro.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DetectOut.Text = "🔌 Testando conexão com agente no servidor DESTINO...\n";

        try
        {
            var client = new AgentClient(_state.TargetDcIp, _state.AgentPort, _state.AgentToken);
            var health = await client.CheckHealthAsync();

            if (health != null)
            {
                DetectOut.Text += $"✅ CONECTADO com sucesso!\n";
                DetectOut.Text += $"   Hostname: {health.Hostname}\n";
                DetectOut.Text += $"   OS: {health.Os}\n";
                DetectOut.Text += $"   Versão do agente: {health.Version}\n";
                DetectOut.Text += $"   Status: {health.Status}\n\n";

                // Obter informações detalhadas
                var sysInfo = await client.GetSystemInfoAsync();
                if (sysInfo != null)
                {
                    DetectOut.Text += $"📊 Informações do Sistema:\n";
                    DetectOut.Text += $"   Windows: {sysInfo.WindowsVersion}\n";
                    DetectOut.Text += $"   Domínio: {sysInfo.Domain}\n";
                    DetectOut.Text += $"   CPU: {sysInfo.ProcessorCount} cores\n";
                    DetectOut.Text += $"   RAM: {sysInfo.TotalMemoryGB} GB\n";
                    DetectOut.Text += $"   Roles instaladas: {sysInfo.InstalledRoles.Count}\n";
                }

                MessageBox.Show($"Agente conectado com sucesso!\n\nHostname: {health.Hostname}\nOS: {health.Os}",
                    "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                DetectOut.Text += $"❌ Não foi possível conectar ao agente.\n";
                DetectOut.Text += $"   Verifique se:\n";
                DetectOut.Text += $"   - O agente está instalado e rodando\n";
                DetectOut.Text += $"   - O IP está correto: {_state.TargetDcIp}\n";
                DetectOut.Text += $"   - A porta está correta: {_state.AgentPort}\n";
                DetectOut.Text += $"   - O firewall está liberado\n";

                MessageBox.Show($"Não foi possível conectar ao agente no servidor destino.\n\nVerifique se o agente está instalado e rodando.",
                    "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            DetectOut.Text += $"❌ Erro: {ex.Message}\n";
            MessageBox.Show($"Erro ao testar conexão:\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Abrir guia de instalação do agente
    private void ShowAgentGuide_Click(object sender, RoutedEventArgs e)
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
                MessageBox.Show($"Erro ao abrir guia:\n\n{ex.Message}\n\nCaminho: {guidePath}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show($"Guia não encontrado em:\n{guidePath}\n\nO guia está na raiz do projeto: GUIA_AGENTE.md",
                "Arquivo não encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

