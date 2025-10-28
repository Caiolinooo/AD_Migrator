using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MigracaoAD.UI.Services;

public class MigrationRunner
{
    private readonly State _s;
    public bool DryRun { get; set; }
    public MigrationRunner(State s) { _s = s; }

    private static string OutPath(string relative) => Path.Combine(AppContext.BaseDirectory, relative);

    private static string Script(string relative) => OutPath(Path.Combine("projeto-migracao", relative));

    private IEnumerable<(string name, string file, string args)> Plan()
    {
        // Compor plano mínimo baseado nas escolhas do usuário
        yield return ("Prechecks", Script(Path.Combine("scripts", "00-Prechecks.ps1")), "");

        // Persistir parâmetros
        yield return (
            "Salvar parâmetros",
            Script(Path.Combine("scripts", "00-Write-Parameters.ps1")),
            $"-DomainName \"{_s.TargetDomainName ?? string.Empty}\" -OldDCHostname \"{_s.SourceDcHost ?? string.Empty}\" -NewDCHostname \"{_s.TargetDcHost ?? string.Empty}\" -SourceFileServer \"{_s.SourceFileServer ?? string.Empty}\" -DestinationFileServer \"{_s.DestinationFileServer ?? string.Empty}\" -DestinationRootPath \"E:\\\\Shares\""
        );

        if (_s.ModeOptionA)
        {
            yield return ("Discovery AD", Script(Path.Combine("scripts", "01-Discovery-AD.ps1")), "");
            yield return ("Promote New DC", Script(Path.Combine("scripts", "10-Promote-New-DC.ps1")), "");
            yield return ("Transfer FSMO", Script(Path.Combine("scripts", "20-Transfer-FSMO.ps1")), "");
        }
        if (_s.ModeOptionB)
        {
            yield return ("Create Trust (B)", Script(Path.Combine("scripts-b", "Create-Trust.ps1")),
                $"-SourceDomain \"{_s.SourceDomainName}\" -TargetDomain \"{_s.TargetDomainName}\"");
            yield return ("Enable SIDHistory (B)", Script(Path.Combine("scripts-b", "Enable-SIDHistory.ps1")),
                $"-SourceDomain \"{_s.SourceDomainName}\" -TargetDomain \"{_s.TargetDomainName}\"");

            // Provisionar ferramentas e chave
            yield return ("ADMT: Instalar (se necessário)", Script(Path.Combine("scripts-b", "ADMT-Install.ps1")), "");
            yield return ("RSAT-AD no destino", Script(Path.Combine("scripts-b", "Install-RSAT-AD.ps1")), "");
            yield return ("ADMT: Preparar PES", Script(Path.Combine("scripts-b", "ADMT-Prepare-PES.ps1")),
                $"-SourceDomain \"{_s.SourceDomainName}\" -TargetDomain \"{_s.TargetDomainName}\" -KeyFilePath \"..\\outputs\\admt\\pes.key\"");
            var pdc = _s.SourceDcHost ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(pdc))
                yield return ("PES: Instalar no PDC origem", Script(Path.Combine("scripts-b", "PES-Install.ps1")),
                    $"-SourcePDC \"{pdc}\" -KeyFilePath \"..\\outputs\\admt\\pes.key\"");

            // Migrações ADMT
            yield return ("ADMT: Migrar Grupos", Script(Path.Combine("scripts-b", "ADMT-Migrate-Groups.ps1")),
                $"-SourceDomain \"{_s.SourceDomainName}\" -TargetDomain \"{_s.TargetDomainName}\" ");
            yield return ("ADMT: Migrar Usuários", Script(Path.Combine("scripts-b", "ADMT-Migrate-Users.ps1")),
                $"-SourceDomain \"{_s.SourceDomainName}\" -TargetDomain \"{_s.TargetDomainName}\" ");
            yield return ("ADMT: Migrar Computadores", Script(Path.Combine("scripts-b", "ADMT-Migrate-Computers.ps1")),
                $"-SourceDomain \"{_s.SourceDomainName}\" -TargetDomain \"{_s.TargetDomainName}\" ");
            yield return ("ADMT: Translate Security (Add)", Script(Path.Combine("scripts-b", "ADMT-Translate-Security.ps1")),
                $"-SourceDomain \"{_s.SourceDomainName}\" -TargetDomain \"{_s.TargetDomainName}\" -Mode Add");
        }

        // Arquivos
        if (_s.UseRobocopy)
        {
            yield return ("Discovery File Shares", Script(Path.Combine("scripts", "02-Discovery-FileShares.ps1")), $"-ComputerName \"{_s.SourceFileServer}\"");
            yield return ("Gerar Maps", Script(Path.Combine("scripts", "24-Generate-Maps.ps1")), "");
            yield return ("Criar Shares Destino", Script(Path.Combine("scripts", "25-Create-Destination-Shares.ps1")), "");
            var mapsCsvAbs = OutPath(Path.Combine("projeto-migracao", "samples", "maps.csv"));
            yield return ("Robocopy Migração", Script(Path.Combine("scripts", "30-Robocopy-Migrate.ps1")),
                $"-CsvMapPath \"{mapsCsvAbs}\"");
        }

        if (_s.UseDFSN)
        {
            var ns = $"\\\\{_s.TargetDomainName}\\Arquivos";
            var rt1 = _s.SourceFileServer ?? string.Empty;
            var rt2 = _s.DestinationFileServer ?? string.Empty;
            var rootsArg = !string.IsNullOrWhiteSpace(rt1) || !string.IsNullOrWhiteSpace(rt2)
                ? $"-RootTargets '{rt1}','{rt2}'" : string.Empty;
            yield return ("DFSN Setup", Script(Path.Combine("scripts-b", "DFSN-Setup.ps1")), $"-NamespacePath \"{ns}\" {rootsArg}");
        }
        if (_s.UseDFSR)
        {
            yield return ("DFSR Setup", Script(Path.Combine("scripts-b", "DFSR-Setup.ps1")), "");
        }

        if (_s.ModeOptionA)
        {
            yield return ("Demote Old DC", Script(Path.Combine("scripts", "40-Demote-Old-DC.ps1")), "");
        }
    }

    public async Task<int> RunAsync(IProgress<string> log)
    {
        int stepIndex = 0;
        foreach (var (name, file, args) in Plan())
        {
            stepIndex++;
            if (!File.Exists(file))
            {
                log.Report($"[{stepIndex}] {name}: SKIP (script nao encontrado) => {file}\n");
                continue;
            }
            var cmdArgs = $"-NoProfile -ExecutionPolicy Bypass -File \"{file}\" {args}";
            log.Report($"[{stepIndex}] {name}: executando\n  ps> {cmdArgs}\n");
            if (DryRun)
            {
                log.Report("  (dry-run)\n");
                continue;
            }
            var (code, stdout, stderr) = await ProcessRunner.RunAsync("powershell.exe", cmdArgs, 60*60*1000);
            if (!string.IsNullOrWhiteSpace(stdout)) log.Report(stdout + "\n");
            if (!string.IsNullOrWhiteSpace(stderr)) log.Report("[stderr] " + stderr + "\n");
            log.Report($"=> ExitCode {code}\n");
            if (code != 0) return code;
        }
        return 0;
    }
}

