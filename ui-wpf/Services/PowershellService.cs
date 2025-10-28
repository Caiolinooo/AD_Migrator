using System.Text.Json;
using System.Threading.Tasks;

namespace MigracaoAD.UI.Services;

public static class PowershellService
{
    public static async Task<string> DetectRemoteAsync(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return "IP não informado.";

        var script = @"
param([string]$ip)
$ErrorActionPreference='SilentlyContinue'
$ports = 135,445,389,636,88,464,53,3268,3269,5985,5986
$tests = @()
foreach($p in $ports){
  try{ $r = Test-NetConnection -ComputerName $ip -Port $p -WarningAction SilentlyContinue
       $tests += [PSCustomObject]@{Port=$p;Reachable=[bool]$r.TcpTestSucceeded} }
  catch{ $tests += [PSCustomObject]@{Port=$p;Reachable=$false} }
}
$os = $null
try{ $os = Get-CimInstance Win32_OperatingSystem -ComputerName $ip } catch{}
$host = $null
try{ $host = (Resolve-DnsName $ip -ErrorAction SilentlyContinue).NameHost } catch{}
$result = [PSCustomObject]@{
  IP=$ip
  Host=$host
  OS=$os.Caption
  Version=$os.Version
  Build=$os.BuildNumber
  ReachablePorts=($tests | ?{$_.Reachable} | %{$_.Port}) -join ','
}
$result | ConvertTo-Json -Depth 4
";
        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"detect_{System.Guid.NewGuid():N}.ps1");
        await System.IO.File.WriteAllTextAsync(tmp, script);
        var (code, stdout, stderr) = await ProcessRunner.RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{tmp}\" -ip {ip}");
        try { System.IO.File.Delete(tmp); } catch { }
        if (code != 0 && string.IsNullOrWhiteSpace(stdout))
            return $"Falha ao detectar em {ip}: {stderr}";
        return stdout.Trim();
    }
}

