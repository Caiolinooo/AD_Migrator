using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MigracaoAD.UI.Services;

public class NetworkDiscoveryResult
{
    public bool Success { get; set; }
    public string? CurrentDomain { get; set; }
    public string? CurrentDomainController { get; set; }
    public string? CurrentDcIp { get; set; }
    public List<string> AvailableDomainControllers { get; set; } = new();
    public string? LocalComputerName { get; set; }
    public string? LocalIpAddress { get; set; }
    public bool IsJoinedToDomain { get; set; }
    public string? ErrorMessage { get; set; }
}

public class NetworkDiscoveryService
{
    public async Task<NetworkDiscoveryResult> DiscoverNetworkAsync()
    {
        var result = new NetworkDiscoveryResult
        {
            LocalComputerName = Environment.MachineName
        };

        try
        {
            // Detectar IP local
            result.LocalIpAddress = GetLocalIPAddress();

            // Verificar se está em um domínio
            result.IsJoinedToDomain = IsComputerJoinedToDomain();

            if (result.IsJoinedToDomain)
            {
                // Obter informações do domínio atual
                await Task.Run(() =>
                {
                    try
                    {
                        var domain = Domain.GetComputerDomain();
                        result.CurrentDomain = domain.Name;

                        // Obter DC atual
                        var dc = domain.FindDomainController();
                        result.CurrentDomainController = dc.Name;
                        result.CurrentDcIp = GetDomainControllerIP(dc.Name);

                        // Listar todos os DCs
                        foreach (DomainController controller in domain.FindAllDomainControllers())
                        {
                            result.AvailableDomainControllers.Add($"{controller.Name} ({GetDomainControllerIP(controller.Name)})");
                        }

                        result.Success = true;
                    }
                    catch (Exception ex)
                    {
                        result.ErrorMessage = $"Erro ao obter informações do domínio: {ex.Message}";
                        result.Success = false;
                    }
                });
            }
            else
            {
                result.Success = true;
                result.ErrorMessage = "Este computador não está em um domínio Active Directory.";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Erro na descoberta de rede: {ex.Message}";
        }

        return result;
    }

    private bool IsComputerJoinedToDomain()
    {
        try
        {
            var domain = Domain.GetComputerDomain();
            return domain != null;
        }
        catch
        {
            return false;
        }
    }

    private string? GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch
        {
            // Ignora erros
        }
        return null;
    }

    private string? GetDomainControllerIP(string dcName)
    {
        try
        {
            // Remove o domínio do nome se presente
            var hostname = dcName.Split('.')[0];
            var hostEntry = Dns.GetHostEntry(hostname);
            var ip = hostEntry.AddressList.FirstOrDefault(addr => addr.AddressFamily == AddressFamily.InterNetwork);
            return ip?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<string>> ScanSubnetForServersAsync(string baseIp, int timeout = 100)
    {
        var servers = new List<string>();
        var tasks = new List<Task>();

        // Extrair os 3 primeiros octetos do IP base
        var parts = baseIp.Split('.');
        if (parts.Length != 4) return servers;

        var subnet = $"{parts[0]}.{parts[1]}.{parts[2]}";

        // Escanear IPs de 1 a 254
        for (int i = 1; i <= 254; i++)
        {
            var ip = $"{subnet}.{i}";
            tasks.Add(Task.Run(async () =>
            {
                if (await PingHostAsync(ip, timeout))
                {
                    lock (servers)
                    {
                        servers.Add(ip);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        return servers.OrderBy(s => s).ToList();
    }

    private async Task<bool> PingHostAsync(string host, int timeout)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeout);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestDomainControllerAsync(string dcHost)
    {
        try
        {
            // Testar porta LDAP (389)
            using var client = new TcpClient();
            await client.ConnectAsync(dcHost, 389);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> DiscoverDomainControllersInSubnetAsync(string baseIp)
    {
        var dcs = new List<string>();
        var servers = await ScanSubnetForServersAsync(baseIp, 50);

        var tasks = servers.Select(async ip =>
        {
            if (await TestDomainControllerAsync(ip))
            {
                lock (dcs)
                {
                    dcs.Add(ip);
                }
            }
        });

        await Task.WhenAll(tasks);
        return dcs;
    }
}

