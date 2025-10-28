using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MigracaoAD.UI.Services
{
    public enum ConnectionMethod
    {
        WinRM = 0,
        SSH = 1,
        PSExec = 2
    }

    public class ConnectionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Hint { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public string Hostname { get; set; } = "";
    }

    public class RemoteConfigService
    {
        private int _timeout = 30;
        private readonly State? _state;

        public RemoteConfigService(State? state = null)
        {
            _state = state;
        }

        public async Task<ConnectionResult> TestConnectionAsync(
            string host,
            string username,
            string password,
            ConnectionMethod method,
            string? domain = null)
        {
            // Se o agente estiver habilitado e configurado, usar o agente
            if (_state?.UseAgent == true && !string.IsNullOrWhiteSpace(_state.AgentToken))
            {
                try
                {
                    var client = new AgentClient(host, _state.AgentPort, _state.AgentToken);
                    var health = await client.CheckHealthAsync();

                    if (health != null)
                    {
                        var sysInfo = await client.GetSystemInfoAsync();
                        return new ConnectionResult
                        {
                            Success = true,
                            Message = $"Conectado via Agente com sucesso!",
                            Hostname = health.Hostname,
                            OSVersion = sysInfo?.WindowsVersion ?? health.Os
                        };
                    }
                }
                catch (Exception ex)
                {
                    // Se falhar com agente, tentar método tradicional
                    System.Diagnostics.Debug.WriteLine($"Falha ao conectar via agente: {ex.Message}");
                }
            }

            // Fallback para métodos tradicionais
            var result = new ConnectionResult();

            try
            {
                switch (method)
                {
                    case ConnectionMethod.WinRM:
                        return await TestWinRMAsync(host, username, password, domain);

                    case ConnectionMethod.SSH:
                        return await TestSSHAsync(host, username, password);

                    case ConnectionMethod.PSExec:
                        return await TestPSExecAsync(host, username, password, domain);
                    
                    default:
                        result.Message = "Método de conexão inválido";
                        return result;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.Hint = "Verifique conectividade de rede e credenciais";
                return result;
            }
        }

        private async Task<ConnectionResult> TestWinRMAsync(string host, string username, string password, string? domain)
        {
            var result = new ConnectionResult();

            try
            {
                // Criar script temporário para testar WinRM
                var scriptPath = Path.GetTempFileName() + ".ps1";
                var fullUsername = domain != null ? $"{domain}\\{username}" : username;

                var script = $@"
$ErrorActionPreference = 'Stop'
try {{
    $secPass = ConvertTo-SecureString '{password}' -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential('{fullUsername}', $secPass)
    
    $sessionOption = New-PSSessionOption -SkipCACheck -SkipCNCheck
    $session = New-PSSession -ComputerName '{host}' -Credential $cred -SessionOption $sessionOption -ErrorAction Stop
    
    $info = Invoke-Command -Session $session -ScriptBlock {{
        $os = Get-CimInstance Win32_OperatingSystem
        $cs = Get-CimInstance Win32_ComputerSystem
        @{{
            Hostname = $cs.Name
            OSVersion = $os.Caption
            OSBuild = $os.BuildNumber
        }}
    }}
    
    Remove-PSSession $session
    
    Write-Output ""SUCCESS|$($info.Hostname)|$($info.OSVersion) (Build $($info.OSBuild))""
}} catch {{
    Write-Output ""ERROR|$($_.Exception.Message)""
}}
";

                File.WriteAllText(scriptPath, script, Encoding.UTF8);

                var (exitCode, stdout, stderr) = await ProcessRunner.RunAsync(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    timeoutMs: _timeout
                );

                File.Delete(scriptPath);

                if (stdout.StartsWith("SUCCESS|"))
                {
                    var parts = stdout.Split('|');
                    result.Success = true;
                    result.Hostname = parts.Length > 1 ? parts[1].Trim() : "";
                    result.OSVersion = parts.Length > 2 ? parts[2].Trim() : "";
                    result.Message = $"{result.Hostname} - {result.OSVersion}";
                }
                else if (stdout.StartsWith("ERROR|"))
                {
                    result.Success = false;
                    result.Message = stdout.Substring(6).Trim();
                    result.Hint = GetWinRMHint(result.Message);
                }
                else
                {
                    result.Success = false;
                    result.Message = stderr.Length > 0 ? stderr : "Resposta inesperada do servidor";
                    result.Hint = GetWinRMHint(result.Message);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.Hint = GetWinRMHint(ex.Message);
            }

            return result;
        }

        private async Task<ConnectionResult> TestSSHAsync(string host, string username, string password)
        {
            var result = new ConnectionResult();

            try
            {
                // Tentar usar ssh.exe (Windows 10/11 built-in)
                var sshPath = FindSSHExecutable();
                if (sshPath == null)
                {
                    result.Success = false;
                    result.Message = "SSH não encontrado no sistema";
                    result.Hint = "Instale OpenSSH Client: Settings > Apps > Optional Features > OpenSSH Client";
                    return result;
                }

                // Criar script expect-like para automação de senha
                var command = $"hostname && ver";
                var args = $"-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null {username}@{host} \"{command}\"";

                // Nota: SSH com senha requer sshpass ou similar, que não está disponível no Windows por padrão
                // Vamos sugerir usar chave SSH ou WinRM
                result.Success = false;
                result.Message = "SSH com senha requer configuração adicional";
                result.Hint = "Use WinRM (recomendado) ou configure autenticação por chave SSH";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.Hint = "Verifique se OpenSSH está instalado e o servidor aceita conexões SSH";
            }

            return result;
        }

        private async Task<ConnectionResult> TestPSExecAsync(string host, string username, string password, string? domain)
        {
            var result = new ConnectionResult();

            try
            {
                // Verificar se PSExec está disponível
                var psexecPath = FindPSExec();
                if (psexecPath == null)
                {
                    result.Success = false;
                    result.Message = "PSExec não encontrado";
                    result.Hint = "Baixe PSExec do Sysinternals: https://docs.microsoft.com/sysinternals/downloads/psexec";
                    return result;
                }

                var fullUsername = domain != null ? $"{domain}\\{username}" : username;
                var args = $"\\\\{host} -u \"{fullUsername}\" -p \"{password}\" -accepteula hostname";

                var (exitCode, stdout, stderr) = await ProcessRunner.RunAsync(
                    psexecPath,
                    args,
                    timeoutMs: _timeout
                );

                if (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
                {
                    result.Success = true;
                    result.Hostname = stdout.Trim();
                    result.Message = $"Conectado via PSExec: {result.Hostname}";
                }
                else
                {
                    result.Success = false;
                    result.Message = stderr.Length > 0 ? stderr : "Falha ao conectar via PSExec";
                    result.Hint = "Verifique se o compartilhamento ADMIN$ está acessível e o firewall permite conexões";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.Hint = "PSExec requer acesso administrativo e compartilhamento ADMIN$ habilitado";
            }

            return result;
        }

        private string GetWinRMHint(string errorMessage)
        {
            if (errorMessage.Contains("WinRM") || errorMessage.Contains("5985") || errorMessage.Contains("5986"))
                return "WinRM pode não estar habilitado. Execute no servidor: Enable-PSRemoting -Force";
            
            if (errorMessage.Contains("Access is denied") || errorMessage.Contains("Acesso negado"))
                return "Credenciais inválidas ou usuário sem permissões administrativas";
            
            if (errorMessage.Contains("timeout") || errorMessage.Contains("timed out"))
                return "Timeout de conexão. Verifique firewall (porta 5985/5986) e conectividade de rede";
            
            if (errorMessage.Contains("TrustedHosts"))
                return "Adicione o host aos TrustedHosts: Set-Item WSMan:\\localhost\\Client\\TrustedHosts -Value '*' -Force";
            
            return "Verifique se WinRM está habilitado, firewall permite conexões e credenciais estão corretas";
        }

        private string? FindSSHExecutable()
        {
            // Procurar ssh.exe em locais comuns
            var paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenSSH", "ssh.exe"),
                "ssh.exe" // PATH
            };

            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                        return path;
                    
                    // Tentar executar para ver se está no PATH
                    if (path == "ssh.exe")
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "where",
                            Arguments = "ssh.exe",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            var output = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit();
                            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                                return output.Split('\n')[0].Trim();
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private string? FindPSExec()
        {
            // Procurar PSExec em locais comuns
            var paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "PSExec.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PSTools", "PSExec.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PSTools", "PSExec.exe"),
                "PSExec.exe" // PATH
            };

            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                        return path;
                    
                    if (path == "PSExec.exe")
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "where",
                            Arguments = "PSExec.exe",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            var output = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit();
                            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                                return output.Split('\n')[0].Trim();
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        // ============================================
        // Métodos de Configuração Automática
        // ============================================

        public async Task<ConnectionResult> EnableWinRMAsync(
            string host,
            string username,
            string password,
            ConnectionMethod fallbackMethod)
        {
            var result = new ConnectionResult();

            // Tentar habilitar WinRM via SSH ou PSExec
            if (fallbackMethod == ConnectionMethod.SSH)
            {
                result.Message = "Habilitação via SSH não implementada ainda";
                result.Hint = "Use PSExec ou habilite WinRM manualmente";
                return result;
            }

            if (fallbackMethod == ConnectionMethod.PSExec)
            {
                try
                {
                    var psexecPath = FindPSExec();
                    if (psexecPath == null)
                    {
                        result.Message = "PSExec não encontrado";
                        result.Hint = "Baixe PSExec ou habilite WinRM manualmente no servidor";
                        return result;
                    }

                    var command = "powershell.exe -Command \"Enable-PSRemoting -Force; Set-Item WSMan:\\localhost\\Client\\TrustedHosts -Value '*' -Force\"";
                    var args = $"\\\\{host} -u \"{username}\" -p \"{password}\" -accepteula -s {command}";

                    var (exitCode, stdout, stderr) = await ProcessRunner.RunAsync(
                        psexecPath,
                        args,
                        timeoutMs: 60000
                    );

                    if (exitCode == 0)
                    {
                        result.Success = true;
                        result.Message = "WinRM habilitado com sucesso via PSExec";
                    }
                    else
                    {
                        result.Message = $"Falha ao habilitar WinRM: {stderr}";
                        result.Hint = "Verifique credenciais e permissões administrativas";
                    }
                }
                catch (Exception ex)
                {
                    result.Message = ex.Message;
                    result.Hint = "Habilite WinRM manualmente: Enable-PSRemoting -Force";
                }
            }

            return result;
        }

        public async Task<ConnectionResult> ConfigureServerAsync(
            string host,
            string username,
            string password,
            ConnectionMethod method,
            string? domain,
            ServerConfigOptions options)
        {
            var result = new ConnectionResult();

            // Se o agente estiver habilitado, usar o agente
            if (_state?.UseAgent == true && !string.IsNullOrWhiteSpace(_state.AgentToken))
            {
                try
                {
                    return await ConfigureServerViaAgentAsync(host, options);
                }
                catch (Exception ex)
                {
                    // Se falhar com agente, tentar método tradicional
                    System.Diagnostics.Debug.WriteLine($"Falha ao configurar via agente: {ex.Message}");
                }
            }

            // Fallback para método tradicional via WinRM
            try
            {
                // Criar script de configuração
                var scriptPath = Path.GetTempFileName() + ".ps1";
                var script = GenerateConfigScript(options);
                File.WriteAllText(scriptPath, script, Encoding.UTF8);

                // Executar remotamente
                var fullUsername = domain != null ? $"{domain}\\{username}" : username;
                var psScript = $@"
$ErrorActionPreference = 'Stop'
try {{
    $secPass = ConvertTo-SecureString '{password}' -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential('{fullUsername}', $secPass)

    $sessionOption = New-PSSessionOption -SkipCACheck -SkipCNCheck
    $session = New-PSSession -ComputerName '{host}' -Credential $cred -SessionOption $sessionOption -ErrorAction Stop

    $scriptContent = Get-Content '{scriptPath}' -Raw
    $output = Invoke-Command -Session $session -ScriptBlock {{
        param($script)
        Invoke-Expression $script
    }} -ArgumentList $scriptContent

    Remove-PSSession $session

    Write-Output ""SUCCESS|Configuração aplicada com sucesso""
    Write-Output $output
}} catch {{
    Write-Output ""ERROR|$($_.Exception.Message)""
}}
";

                var psPath = Path.GetTempFileName() + ".ps1";
                File.WriteAllText(psPath, psScript, Encoding.UTF8);

                var (exitCode, stdout, stderr) = await ProcessRunner.RunAsync(
                    "powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{psPath}\"",
                    timeoutMs: 300000 // 5 minutos para configuração
                );

                File.Delete(scriptPath);
                File.Delete(psPath);

                if (stdout.StartsWith("SUCCESS|"))
                {
                    result.Success = true;
                    result.Message = stdout.Substring(8).Trim();
                }
                else if (stdout.StartsWith("ERROR|"))
                {
                    result.Success = false;
                    result.Message = stdout.Substring(6).Trim();
                }
                else
                {
                    result.Success = false;
                    result.Message = stderr.Length > 0 ? stderr : "Erro desconhecido";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
            }

            return result;
        }

        private string GenerateConfigScript(ServerConfigOptions options)
        {
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Continue'");
            sb.AppendLine("$results = @()");
            sb.AppendLine("");

            // Configurar IP estático
            if (options.ConfigureNetwork && !string.IsNullOrEmpty(options.IPAddress))
            {
                sb.AppendLine("# Configurar IP estático");
                sb.AppendLine("try {");
                sb.AppendLine($"    $adapter = Get-NetAdapter | Where-Object {{$_.Status -eq 'Up'}} | Select-Object -First 1");
                sb.AppendLine($"    Remove-NetIPAddress -InterfaceAlias $adapter.Name -AddressFamily IPv4 -Confirm:$false -ErrorAction SilentlyContinue");
                sb.AppendLine($"    Remove-NetRoute -InterfaceAlias $adapter.Name -Confirm:$false -ErrorAction SilentlyContinue");

                if (!string.IsNullOrEmpty(options.Gateway))
                {
                    sb.AppendLine($"    New-NetIPAddress -InterfaceAlias $adapter.Name -IPAddress '{options.IPAddress}' -PrefixLength {options.PrefixLength} -DefaultGateway '{options.Gateway}' | Out-Null");
                }
                else
                {
                    sb.AppendLine($"    New-NetIPAddress -InterfaceAlias $adapter.Name -IPAddress '{options.IPAddress}' -PrefixLength {options.PrefixLength} | Out-Null");
                }

                if (!string.IsNullOrEmpty(options.DNSServer))
                {
                    sb.AppendLine($"    Set-DnsClientServerAddress -InterfaceAlias $adapter.Name -ServerAddresses '{options.DNSServer}','8.8.8.8'");
                }

                sb.AppendLine("    $results += '✅ IP configurado'");
                sb.AppendLine("} catch {");
                sb.AppendLine("    $results += '❌ Erro ao configurar IP: ' + $_.Exception.Message");
                sb.AppendLine("}");
                sb.AppendLine("");
            }

            // Abrir portas do firewall
            if (options.OpenFirewallPorts)
            {
                sb.AppendLine("# Abrir portas do firewall");
                sb.AppendLine("$ports = @(");
                sb.AppendLine("    @{Nome='AD-DNS-TCP'; Protocolo='TCP'; Porta=53},");
                sb.AppendLine("    @{Nome='AD-DNS-UDP'; Protocolo='UDP'; Porta=53},");
                sb.AppendLine("    @{Nome='AD-Kerberos-TCP'; Protocolo='TCP'; Porta=88},");
                sb.AppendLine("    @{Nome='AD-RPC'; Protocolo='TCP'; Porta=135},");
                sb.AppendLine("    @{Nome='AD-LDAP'; Protocolo='TCP'; Porta=389},");
                sb.AppendLine("    @{Nome='AD-SMB'; Protocolo='TCP'; Porta=445},");
                sb.AppendLine("    @{Nome='AD-LDAPS'; Protocolo='TCP'; Porta=636},");
                sb.AppendLine("    @{Nome='AD-GC'; Protocolo='TCP'; Porta=3268},");
                sb.AppendLine("    @{Nome='AD-WinRM'; Protocolo='TCP'; Porta=5985}");
                sb.AppendLine(")");
                sb.AppendLine("foreach ($p in $ports) {");
                sb.AppendLine("    Remove-NetFirewallRule -DisplayName $p.Nome -ErrorAction SilentlyContinue");
                sb.AppendLine("    New-NetFirewallRule -DisplayName $p.Nome -Direction Inbound -Protocol $p.Protocolo -LocalPort $p.Porta -Action Allow -ErrorAction SilentlyContinue | Out-Null");
                sb.AppendLine("}");
                sb.AppendLine("New-NetFirewallRule -DisplayName 'AD-RPC-Dynamic' -Direction Inbound -Protocol TCP -LocalPort 49152-65535 -Action Allow -ErrorAction SilentlyContinue | Out-Null");
                sb.AppendLine("$results += '✅ Portas do firewall abertas'");
                sb.AppendLine("");
            }

            // Instalar roles
            if (options.InstallRoles)
            {
                sb.AppendLine("# Instalar roles");
                sb.AppendLine("$features = @('AD-Domain-Services', 'FS-DFS-Namespace', 'FS-DFS-Replication', 'FS-FileServer', 'RSAT-AD-Tools', 'RSAT-DFS-Mgmt-Con')");
                sb.AppendLine("foreach ($f in $features) {");
                sb.AppendLine("    Install-WindowsFeature -Name $f -IncludeManagementTools -ErrorAction SilentlyContinue | Out-Null");
                sb.AppendLine("}");
                sb.AppendLine("$results += '✅ Roles instalados'");
                sb.AppendLine("");
            }

            // Preparar disco
            if (options.PrepareDisk && options.DiskNumber > 0)
            {
                sb.AppendLine("# Preparar disco");
                sb.AppendLine("try {");
                sb.AppendLine($"    $disk = Get-Disk -Number {options.DiskNumber} -ErrorAction Stop");
                sb.AppendLine("    if ($disk.PartitionStyle -eq 'RAW') {");
                sb.AppendLine($"        Initialize-Disk -Number {options.DiskNumber} -PartitionStyle GPT | Out-Null");
                sb.AppendLine($"        New-Partition -DiskNumber {options.DiskNumber} -UseMaximumSize -DriveLetter '{options.DriveLetter}' | Out-Null");
                sb.AppendLine($"        Format-Volume -DriveLetter '{options.DriveLetter}' -FileSystem NTFS -NewFileSystemLabel 'Shares' -Confirm:$false | Out-Null");
                sb.AppendLine("        $results += '✅ Disco preparado'");
                sb.AppendLine("    } else {");
                sb.AppendLine("        $results += '⚠️ Disco já inicializado'");
                sb.AppendLine("    }");
                sb.AppendLine("} catch {");
                sb.AppendLine("    $results += '❌ Erro ao preparar disco: ' + $_.Exception.Message");
                sb.AppendLine("}");
                sb.AppendLine("");
            }

            sb.AppendLine("return ($results -join \"`n\")");
            return sb.ToString();
        }

        private async Task<ConnectionResult> ConfigureServerViaAgentAsync(string host, ServerConfigOptions options)
        {
            var result = new ConnectionResult();
            var client = new AgentClient(host, _state!.AgentPort, _state.AgentToken);
            var messages = new System.Collections.Generic.List<string>();

            try
            {
                // 1. Configurar rede
                if (options.ConfigureNetwork && !string.IsNullOrWhiteSpace(options.IPAddress))
                {
                    var dnsServers = new System.Collections.Generic.List<string>();
                    if (!string.IsNullOrWhiteSpace(options.DNSServer))
                        dnsServers.Add(options.DNSServer);

                    var netResult = await client.ConfigureNetworkAsync(
                        "Ethernet",  // Nome da interface (pode ser detectado dinamicamente)
                        options.IPAddress,
                        options.PrefixLength,
                        options.Gateway ?? "",
                        dnsServers
                    );

                    if (netResult?.Success == true)
                        messages.Add("✅ Rede configurada");
                    else
                        messages.Add($"❌ Erro na rede: {netResult?.Error}");
                }

                // 2. Abrir portas do firewall
                if (options.OpenFirewallPorts)
                {
                    var ports = new[] {
                        ("SMB", 445),
                        ("RPC", 135),
                        ("LDAP", 389),
                        ("Kerberos", 88),
                        ("DNS", 53)
                    };

                    foreach (var (name, port) in ports)
                    {
                        var fwResult = await client.ConfigureFirewallAsync(
                            $"Allow-{name}",
                            "Inbound",
                            "TCP",
                            port
                        );

                        if (fwResult?.Success == true)
                            messages.Add($"✅ Firewall: {name} (porta {port})");
                        else
                            messages.Add($"⚠️ Firewall {name}: {fwResult?.Error}");
                    }
                }

                // 3. Instalar roles
                if (options.InstallRoles)
                {
                    var roles = new[] { "AD-Domain-Services", "FS-DFS-Namespace", "FS-DFS-Replication", "FS-FileServer" };

                    foreach (var role in roles)
                    {
                        var roleResult = await client.InstallRoleAsync(role);

                        if (roleResult?.Success == true)
                            messages.Add($"✅ Role instalada: {role}");
                        else
                            messages.Add($"⚠️ Role {role}: {roleResult?.Error}");
                    }
                }

                // 4. Preparar disco
                if (options.PrepareDisk)
                {
                    var diskScript = $@"
$disk = Get-Disk -Number {options.DiskNumber} -ErrorAction SilentlyContinue
if ($disk -and $disk.PartitionStyle -eq 'RAW') {{
    Initialize-Disk -Number {options.DiskNumber} -PartitionStyle GPT | Out-Null
    New-Partition -DiskNumber {options.DiskNumber} -UseMaximumSize -DriveLetter '{options.DriveLetter}' | Out-Null
    Format-Volume -DriveLetter '{options.DriveLetter}' -FileSystem NTFS -NewFileSystemLabel 'Shares' -Confirm:$false | Out-Null
    Write-Output 'Disco preparado'
}} else {{
    Write-Output 'Disco já inicializado ou não encontrado'
}}
";
                    var diskResult = await client.ExecuteCommandAsync(diskScript, true);

                    if (diskResult?.Success == true)
                        messages.Add($"✅ Disco preparado: {diskResult.Output}");
                    else
                        messages.Add($"⚠️ Disco: {diskResult?.Error}");
                }

                result.Success = true;
                result.Message = string.Join("\n", messages);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Erro ao configurar via agente: {ex.Message}";
            }

            return result;
        }
    }

    public class ServerConfigOptions
    {
        public bool ConfigureNetwork { get; set; }
        public string? IPAddress { get; set; }
        public int PrefixLength { get; set; } = 24;
        public string? Gateway { get; set; }
        public string? DNSServer { get; set; }

        public bool OpenFirewallPorts { get; set; }
        public bool InstallRoles { get; set; }

        public bool PrepareDisk { get; set; }
        public int DiskNumber { get; set; }
        public string DriveLetter { get; set; } = "E";
    }
}

