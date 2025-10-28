using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configurar para rodar como Windows Service
builder.Host.UseWindowsService();

// Adicionar serviços
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IAgentService, AgentService>();

// Configurar CORS para aceitar conexões do app manager
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowManager", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Middleware de autenticação simples por token
app.Use(async (context, next) =>
{
    // Permitir health check sem autenticação
    if (context.Request.Path.StartsWithSegments("/health"))
    {
        await next();
        return;
    }

    var token = context.Request.Headers["X-Agent-Token"].FirstOrDefault();
    var expectedToken = Environment.GetEnvironmentVariable("AGENT_TOKEN") ?? "default-token-change-me";
    
    if (token != expectedToken)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "Token inválido" });
        return;
    }
    
    await next();
});

app.UseCors("AllowManager");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => new
{
    status = "healthy",
    version = "1.0.0",
    hostname = Environment.MachineName,
    os = Environment.OSVersion.ToString(),
    timestamp = DateTime.UtcNow
});

app.Run("http://0.0.0.0:8765");

// Interface do serviço
public interface IAgentService
{
    Task<CommandResult> ExecuteCommandAsync(string command, bool asAdmin = true);
    Task<SystemInfo> GetSystemInfoAsync();
    Task<DomainInfo> GetDomainInfoAsync();
    Task<List<string>> GetSharesAsync();
    Task<DiskInfo> GetDiskInfoAsync();
}

// Implementação do serviço
public class AgentService : IAgentService
{
    public async Task<CommandResult> ExecuteCommandAsync(string command, bool asAdmin = true)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = asAdmin ? "runas" : ""
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return new CommandResult { Success = false, Error = "Falha ao iniciar processo" };

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new CommandResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var info = new SystemInfo
        {
            Hostname = Environment.MachineName,
            OS = Environment.OSVersion.ToString(),
            Domain = Environment.UserDomainName,
            Username = Environment.UserName,
            Is64Bit = Environment.Is64BitOperatingSystem,
            ProcessorCount = Environment.ProcessorCount,
            TotalMemoryGB = GetTotalMemoryGB()
        };

        // Detectar versão do Windows Server
        var versionCmd = await ExecuteCommandAsync("(Get-WmiObject -Class Win32_OperatingSystem).Caption");
        if (versionCmd.Success)
            info.WindowsVersion = versionCmd.Output.Trim();

        // Detectar roles instaladas
        var rolesCmd = await ExecuteCommandAsync("Get-WindowsFeature | Where-Object {$_.Installed -eq $true} | Select-Object -ExpandProperty Name");
        if (rolesCmd.Success)
            info.InstalledRoles = rolesCmd.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).ToList();

        return info;
    }

    public async Task<DomainInfo> GetDomainInfoAsync()
    {
        var info = new DomainInfo();

        // Verificar se é DC
        var isDcCmd = await ExecuteCommandAsync("(Get-WmiObject -Class Win32_ComputerSystem).DomainRole");
        if (isDcCmd.Success && int.TryParse(isDcCmd.Output.Trim(), out int role))
        {
            info.IsDomainController = role >= 4; // 4 = Backup DC, 5 = Primary DC
        }

        if (info.IsDomainController)
        {
            // Obter informações do domínio
            var domainCmd = await ExecuteCommandAsync("(Get-ADDomain).DNSRoot");
            if (domainCmd.Success)
                info.DomainName = domainCmd.Output.Trim();

            var forestCmd = await ExecuteCommandAsync("(Get-ADForest).Name");
            if (forestCmd.Success)
                info.ForestName = forestCmd.Output.Trim();

            var levelCmd = await ExecuteCommandAsync("(Get-ADDomain).DomainMode");
            if (levelCmd.Success)
                info.FunctionalLevel = levelCmd.Output.Trim();

            // Contar objetos
            var usersCmd = await ExecuteCommandAsync("(Get-ADUser -Filter *).Count");
            if (usersCmd.Success && int.TryParse(usersCmd.Output.Trim(), out int users))
                info.UserCount = users;

            var groupsCmd = await ExecuteCommandAsync("(Get-ADGroup -Filter *).Count");
            if (groupsCmd.Success && int.TryParse(groupsCmd.Output.Trim(), out int groups))
                info.GroupCount = groups;
        }

        return info;
    }

    public async Task<List<string>> GetSharesAsync()
    {
        var cmd = await ExecuteCommandAsync("Get-SmbShare | Select-Object -ExpandProperty Name");
        if (cmd.Success)
            return cmd.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        return new List<string>();
    }

    public async Task<DiskInfo> GetDiskInfoAsync()
    {
        var info = new DiskInfo { Drives = new List<DriveInfo>() };

        var cmd = await ExecuteCommandAsync("Get-PSDrive -PSProvider FileSystem | Select-Object Name, @{N='UsedGB';E={[math]::Round($_.Used/1GB,2)}}, @{N='FreeGB';E={[math]::Round($_.Free/1GB,2)}} | ConvertTo-Json");
        if (cmd.Success)
        {
            try
            {
                var drives = System.Text.Json.JsonSerializer.Deserialize<List<DriveInfo>>(cmd.Output);
                if (drives != null)
                    info.Drives = drives;
            }
            catch { }
        }

        return info;
    }

    private double GetTotalMemoryGB()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            return Math.Round(gcMemoryInfo.TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0, 2);
        }
        catch
        {
            return 0;
        }
    }
}

// Modelos de dados
public record CommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
    public int ExitCode { get; init; }
}

public record SystemInfo
{
    public string Hostname { get; set; } = "";
    public string OS { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Username { get; set; } = "";
    public bool Is64Bit { get; set; }
    public int ProcessorCount { get; set; }
    public double TotalMemoryGB { get; set; }
    public string WindowsVersion { get; set; } = "";
    public List<string> InstalledRoles { get; set; } = new();
}

public record DomainInfo
{
    public bool IsDomainController { get; set; }
    public string DomainName { get; set; } = "";
    public string ForestName { get; set; } = "";
    public string FunctionalLevel { get; set; } = "";
    public int UserCount { get; set; }
    public int GroupCount { get; set; }
}

public record DiskInfo
{
    public List<DriveInfo> Drives { get; set; } = new();
}

public record DriveInfo
{
    public string Name { get; init; } = "";
    public double UsedGB { get; init; }
    public double FreeGB { get; init; }
}

