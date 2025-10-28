using System.Diagnostics;
using System.Text.Json;

internal class Config
{
    public string? SourceDomainName { get; set; }
    public string? TargetDomainName { get; set; }
    public string? SourceDcHost { get; set; }
    public string? TargetDcHost { get; set; }
    public string? SourceFileServer { get; set; }
    public string? DestinationFileServer { get; set; }
    public string? OrchestratorScriptsRoot { get; set; } = @"projeto-migracao"; // base existente

    // Opção B
    public string? AdmtServer { get; set; } // servidor membro no novo domínio que terá ADMT
    public string ConnectivityMode { get; set; } = "Tunnel"; // Tunnel | Direct
    public int RpcPortMin { get; set; } = 49152;
    public int RpcPortMax { get; set; } = 49252;

    // Arquivos
    public string? MapsCsv { get; set; } = @"projeto-migracao\\samples\\maps.csv";
}

internal static class Runner
{
    public static int RunProcess(string file, string args, bool capture = true)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        if (capture)
        {
            p.OutputDataReceived += (_, e) => { if (e.Data != null) Console.Out.WriteLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
        }
        p.WaitForExit();
        return p.ExitCode;
    }

    public static int Pwsh(string scriptPath, string arguments = "")
    {
        var fullArgs = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}";
        return RunProcess("powershell.exe", fullArgs);
    }
}

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        var cfg = LoadConfig();
        var cmd = args[0].ToLowerInvariant();
        var rest = string.Join(" ", args.Skip(1));

        return cmd switch
        {
            "precheck" => Precheck(cfg),
            "setup-trust" => SetupTrust(cfg),
            "enable-sidhistory" => EnableSidHistory(cfg),
            "migrate-groups" => CallScript(cfg, "ADMT-Migrate-Groups.ps1", rest),
            "migrate-users" => CallScript(cfg, "ADMT-Migrate-Users.ps1", rest),
            "migrate-computers" => CallScript(cfg, "ADMT-Migrate-Computers.ps1", rest),
            "translate-security" => CallScript(cfg, "ADMT-Translate-Security.ps1", rest),
            "files-seed" => FilesSeed(cfg),
            "files-delta" => FilesDelta(cfg),
            "dfsn-setup" => CallScript(cfg, "DFSN-Create.ps1", rest),
            "dfsr-setup" => CallScript(cfg, "DFSR-Setup.ps1", rest),
            "validate" => Validate(cfg),
            _ => Unknown(cmd)
        };
    }

    static Config LoadConfig()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[WARN] appsettings.json não encontrado em {path}. Usando defaults e appsettings.sample.json como referência.");
            return new Config();
        }
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"MigracaoAD.Automator (Opção B - novo domínio/ADMT)
Uso:
  automator precheck
  automator setup-trust
  automator enable-sidhistory
  automator migrate-groups
  automator migrate-users
  automator migrate-computers
  automator translate-security
  automator files-seed
  automator files-delta
  automator dfsn-setup
  automator dfsr-setup
  automator validate

Config: coloque appsettings.json ao lado do .exe (veja appsettings.sample.json)." );
    }

    static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Comando desconhecido: {cmd}");
        PrintHelp();
        return 2;
    }

    static string ScriptsRoot(Config cfg) => cfg.OrchestratorScriptsRoot ?? "projeto-migracao";
    static string BDir(Config cfg) => Path.Combine(ScriptsRoot(cfg), "scripts-b");

    static int CallScript(Config cfg, string scriptName, string args = "")
    {
        var path = Path.Combine(BDir(cfg), scriptName);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Script não encontrado: {path}");
            return 3;
        }
        return Runner.Pwsh(path, args);
    }

    static int Precheck(Config cfg)
    {
        Console.WriteLine("[Precheck] Validando pré-requisitos básicos...");
        // Verifica PowerShell, permissões e diretórios de scripts
        var scripts = ScriptsRoot(cfg);
        if (!Directory.Exists(scripts)) { Console.Error.WriteLine($"Pasta de scripts não encontrada: {scripts}"); return 3; }
        var bdir = BDir(cfg);
        if (!Directory.Exists(bdir)) { Console.Error.WriteLine($"Pasta scripts-b não encontrada: {bdir}"); return 3; }
        Console.WriteLine("[OK] Estrutura de scripts encontrada.");
        return 0;
    }

    static int SetupTrust(Config cfg)
    {
        Console.WriteLine("[Trust] Criando trust bidirecional entre domínios (requer conectividade temporária ou Direct).");
        return CallScript(cfg, "Create-Trust.ps1", "");
    }

    static int EnableSidHistory(Config cfg)
    {
        Console.WriteLine("[Trust] Habilitando SIDHistory e desabilitando quarantine no trust.");
        return CallScript(cfg, "Enable-SIDHistory.ps1", "");
    }

    static int FilesSeed(Config cfg)
    {
        Console.WriteLine("[Files] Pré-cópia (seeding) com Robocopy.");
        var maps = cfg.MapsCsv ?? @"projeto-migracao\samples\maps.csv";
        var args = $"-CsvMapPath \"{maps}\" -NoPreCopy:$false";
        var path = Path.Combine(ScriptsRoot(cfg), "scripts", "30-Robocopy-Migrate.ps1");
        if (!File.Exists(path)) { Console.Error.WriteLine($"Script não encontrado: {path}"); return 3; }
        return Runner.Pwsh(path, args);
    }

    static int FilesDelta(Config cfg)
    {
        Console.WriteLine("[Files] Delta/Corte final com Robocopy (inclui /MIR). Requer janela de manutenção.");
        var maps = cfg.MapsCsv ?? @"projeto-migracao\samples\maps.csv";
        var args = $"-CsvMapPath \"{maps}\" -NoPreCopy:$true"; // sinaliza somente delta/corte
        var path = Path.Combine(ScriptsRoot(cfg), "scripts", "30-Robocopy-Migrate.ps1");
        if (!File.Exists(path)) { Console.Error.WriteLine($"Script não encontrado: {path}"); return 3; }
        return Runner.Pwsh(path, args);
    }

    static int Validate(Config cfg)
    {
        Console.WriteLine("[Validate] Execução de verificações pós-migração (logons, GPOs, shares, DFSR). (Placeholder)");
        // Podemos adicionar chamadas a scripts específicos de validação aqui.
        return 0;
    }
}

