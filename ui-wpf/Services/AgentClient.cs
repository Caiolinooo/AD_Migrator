using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MigracaoAD.UI.Services;

/// <summary>
/// Cliente para comunicação com o agente instalado nos servidores
/// </summary>
public class AgentClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _token;

    public AgentClient(string serverIp, int port = 8765, string token = "default-token-change-me")
    {
        _baseUrl = $"http://{serverIp}:{port}";
        _token = token;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.Add("X-Agent-Token", _token);
    }

    /// <summary>
    /// Testa se o agente está respondendo
    /// </summary>
    public async Task<AgentHealthResponse?> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AgentHealthResponse>();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executa um comando PowerShell no servidor remoto
    /// </summary>
    public async Task<CommandResult?> ExecuteCommandAsync(string command, bool asAdmin = true)
    {
        try
        {
            var request = new { Command = command, AsAdmin = asAdmin };
            var response = await _httpClient.PostAsJsonAsync("/api/execute", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResult>();
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                Error = $"Erro ao executar comando: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Obtém informações do sistema
    /// </summary>
    public async Task<SystemInfo?> GetSystemInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/system");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SystemInfo>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Obtém informações do domínio
    /// </summary>
    public async Task<DomainInfo?> GetDomainInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/domain");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DomainInfo>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lista compartilhamentos SMB
    /// </summary>
    public async Task<List<string>?> GetSharesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/shares");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SharesResponse>();
            return result?.Shares;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Obtém informações de disco
    /// </summary>
    public async Task<DiskInfo?> GetDiskInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/disks");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DiskInfo>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Configura rede (IP estático, gateway, DNS)
    /// </summary>
    public async Task<CommandResult?> ConfigureNetworkAsync(string interfaceName, string ipAddress, 
        int subnetMask, string gateway, List<string> dnsServers)
    {
        try
        {
            var request = new
            {
                InterfaceName = interfaceName,
                IpAddress = ipAddress,
                SubnetMask = subnetMask,
                Gateway = gateway,
                DnsServers = dnsServers
            };
            var response = await _httpClient.PostAsJsonAsync("/api/network/configure", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<NetworkConfigResponse>();
            return result?.Results?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Instala uma role do Windows Server
    /// </summary>
    public async Task<CommandResult?> InstallRoleAsync(string roleName)
    {
        try
        {
            var request = new { RoleName = roleName };
            var response = await _httpClient.PostAsJsonAsync("/api/roles/install", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResult>();
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Promove servidor a Domain Controller
    /// </summary>
    public async Task<CommandResult?> PromoteToDCAsync(string domainName, string safeModePassword, bool isNewForest = false)
    {
        try
        {
            var request = new
            {
                DomainName = domainName,
                SafeModePassword = safeModePassword,
                IsNewForest = isNewForest
            };
            var response = await _httpClient.PostAsJsonAsync("/api/domain/promote", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResult>();
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Cria compartilhamento SMB
    /// </summary>
    public async Task<CommandResult?> CreateShareAsync(string shareName, string path, string fullAccessUsers)
    {
        try
        {
            var request = new
            {
                ShareName = shareName,
                Path = path,
                FullAccessUsers = fullAccessUsers
            };
            var response = await _httpClient.PostAsJsonAsync("/api/shares/create", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResult>();
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Configura regra de firewall
    /// </summary>
    public async Task<CommandResult?> ConfigureFirewallAsync(string ruleName, string direction, 
        string protocol, int port)
    {
        try
        {
            var request = new
            {
                RuleName = ruleName,
                Direction = direction,
                Protocol = protocol,
                Port = port
            };
            var response = await _httpClient.PostAsJsonAsync("/api/firewall/configure", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResult>();
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Reinicia o servidor
    /// </summary>
    public async Task<CommandResult?> RebootAsync(int delaySeconds = 10)
    {
        try
        {
            var request = new { DelaySeconds = delaySeconds };
            var response = await _httpClient.PostAsJsonAsync("/api/system/reboot", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResult>();
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Testa conectividade com outro servidor
    /// </summary>
    public async Task<CommandResult?> TestConnectionAsync(string target, int port = 445)
    {
        try
        {
            var request = new { Target = target, Port = port };
            var response = await _httpClient.PostAsJsonAsync("/api/network/test", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResult>();
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Error = ex.Message };
        }
    }
}

// Modelos de resposta
public record AgentHealthResponse
{
    public string Status { get; init; } = "";
    public string Version { get; init; } = "";
    public string Hostname { get; init; } = "";
    public string Os { get; init; } = "";
    public DateTime Timestamp { get; init; }
}

public record CommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
    public int ExitCode { get; init; }
}

public record SystemInfo
{
    public string Hostname { get; init; } = "";
    public string OS { get; init; } = "";
    public string Domain { get; init; } = "";
    public string Username { get; init; } = "";
    public bool Is64Bit { get; init; }
    public int ProcessorCount { get; init; }
    public double TotalMemoryGB { get; init; }
    public string WindowsVersion { get; init; } = "";
    public List<string> InstalledRoles { get; init; } = new();
}

public record DomainInfo
{
    public bool IsDomainController { get; init; }
    public string DomainName { get; init; } = "";
    public string ForestName { get; init; } = "";
    public string FunctionalLevel { get; init; } = "";
    public int UserCount { get; init; }
    public int GroupCount { get; init; }
}

public record DiskInfo
{
    public List<DriveInfo> Drives { get; init; } = new();
}

public record DriveInfo
{
    public string Name { get; init; } = "";
    public double UsedGB { get; init; }
    public double FreeGB { get; init; }
}

public record SharesResponse
{
    public List<string> Shares { get; init; } = new();
}

public record NetworkConfigResponse
{
    public List<CommandResult> Results { get; init; } = new();
}

