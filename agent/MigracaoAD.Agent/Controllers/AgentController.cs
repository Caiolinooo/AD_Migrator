using Microsoft.AspNetCore.Mvc;

namespace MigracaoAD.Agent.Controllers;

[ApiController]
[Route("api")]
public class AgentController : ControllerBase
{
    private readonly IAgentService _agentService;

    public AgentController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// Executa um comando PowerShell no servidor
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] ExecuteCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
            return BadRequest(new { error = "Comando não pode ser vazio" });

        var result = await _agentService.ExecuteCommandAsync(request.Command, request.AsAdmin);
        return Ok(result);
    }

    /// <summary>
    /// Obtém informações do sistema
    /// </summary>
    [HttpGet("system")]
    public async Task<IActionResult> GetSystemInfo()
    {
        var info = await _agentService.GetSystemInfoAsync();
        return Ok(info);
    }

    /// <summary>
    /// Obtém informações do domínio (se for DC)
    /// </summary>
    [HttpGet("domain")]
    public async Task<IActionResult> GetDomainInfo()
    {
        var info = await _agentService.GetDomainInfoAsync();
        return Ok(info);
    }

    /// <summary>
    /// Lista compartilhamentos SMB
    /// </summary>
    [HttpGet("shares")]
    public async Task<IActionResult> GetShares()
    {
        var shares = await _agentService.GetSharesAsync();
        return Ok(new { shares });
    }

    /// <summary>
    /// Obtém informações de disco
    /// </summary>
    [HttpGet("disks")]
    public async Task<IActionResult> GetDisks()
    {
        var info = await _agentService.GetDiskInfoAsync();
        return Ok(info);
    }

    /// <summary>
    /// Configura IP estático
    /// </summary>
    [HttpPost("network/configure")]
    public async Task<IActionResult> ConfigureNetwork([FromBody] NetworkConfigRequest request)
    {
        var commands = new List<string>
        {
            $"New-NetIPAddress -InterfaceAlias '{request.InterfaceName}' -IPAddress {request.IpAddress} -PrefixLength {request.SubnetMask} -DefaultGateway {request.Gateway}",
            $"Set-DnsClientServerAddress -InterfaceAlias '{request.InterfaceName}' -ServerAddresses {string.Join(",", request.DnsServers)}"
        };

        var results = new List<CommandResult>();
        foreach (var cmd in commands)
        {
            var result = await _agentService.ExecuteCommandAsync(cmd);
            results.Add(result);
            if (!result.Success)
                break;
        }

        return Ok(new { results });
    }

    /// <summary>
    /// Instala role do Windows Server
    /// </summary>
    [HttpPost("roles/install")]
    public async Task<IActionResult> InstallRole([FromBody] InstallRoleRequest request)
    {
        var cmd = $"Install-WindowsFeature -Name {request.RoleName} -IncludeManagementTools";
        var result = await _agentService.ExecuteCommandAsync(cmd);
        return Ok(result);
    }

    /// <summary>
    /// Promove servidor a Domain Controller
    /// </summary>
    [HttpPost("domain/promote")]
    public async Task<IActionResult> PromoteToDC([FromBody] PromoteDCRequest request)
    {
        var cmd = request.IsNewForest
            ? $"Install-ADDSForest -DomainName {request.DomainName} -SafeModeAdministratorPassword (ConvertTo-SecureString '{request.SafeModePassword}' -AsPlainText -Force) -Force"
            : $"Install-ADDSDomainController -DomainName {request.DomainName} -SafeModeAdministratorPassword (ConvertTo-SecureString '{request.SafeModePassword}' -AsPlainText -Force) -Force";

        var result = await _agentService.ExecuteCommandAsync(cmd);
        return Ok(result);
    }

    /// <summary>
    /// Cria compartilhamento SMB
    /// </summary>
    [HttpPost("shares/create")]
    public async Task<IActionResult> CreateShare([FromBody] CreateShareRequest request)
    {
        var cmd = $"New-SmbShare -Name '{request.ShareName}' -Path '{request.Path}' -FullAccess '{request.FullAccessUsers}'";
        var result = await _agentService.ExecuteCommandAsync(cmd);
        return Ok(result);
    }

    /// <summary>
    /// Configura firewall
    /// </summary>
    [HttpPost("firewall/configure")]
    public async Task<IActionResult> ConfigureFirewall([FromBody] FirewallConfigRequest request)
    {
        var cmd = $"New-NetFirewallRule -DisplayName '{request.RuleName}' -Direction {request.Direction} -Protocol {request.Protocol} -LocalPort {request.Port} -Action Allow";
        var result = await _agentService.ExecuteCommandAsync(cmd);
        return Ok(result);
    }

    /// <summary>
    /// Reinicia o servidor
    /// </summary>
    [HttpPost("system/reboot")]
    public async Task<IActionResult> Reboot([FromBody] RebootRequest request)
    {
        var cmd = $"Restart-Computer -Force -Timeout {request.DelaySeconds}";
        var result = await _agentService.ExecuteCommandAsync(cmd);
        return Ok(result);
    }

    /// <summary>
    /// Testa conectividade com outro servidor
    /// </summary>
    [HttpPost("network/test")]
    public async Task<IActionResult> TestConnection([FromBody] TestConnectionRequest request)
    {
        var cmd = $"Test-NetConnection -ComputerName {request.Target} -Port {request.Port}";
        var result = await _agentService.ExecuteCommandAsync(cmd);
        return Ok(result);
    }
}

// Request models
public record ExecuteCommandRequest(string Command, bool AsAdmin = true);
public record NetworkConfigRequest(string InterfaceName, string IpAddress, int SubnetMask, string Gateway, List<string> DnsServers);
public record InstallRoleRequest(string RoleName);
public record PromoteDCRequest(string DomainName, string SafeModePassword, bool IsNewForest = false);
public record CreateShareRequest(string ShareName, string Path, string FullAccessUsers);
public record FirewallConfigRequest(string RuleName, string Direction, string Protocol, int Port);
public record RebootRequest(int DelaySeconds = 10);
public record TestConnectionRequest(string Target, int Port = 445);

