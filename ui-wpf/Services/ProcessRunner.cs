using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace MigracaoAD.UI.Services;

public static class ProcessRunner
{
    public static async Task<(int exitCode, string stdout, string stderr)> RunAsync(
        string fileName,
        string arguments,
        int timeoutMs = 120_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdOut.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) stdErr.AppendLine(e.Data); };
        proc.Exited += (_, __) => tcs.TrySetResult(proc.ExitCode);

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
        await using (cts.Token.Register(() => tcs.TrySetCanceled()))
        {
            int code;
            try { code = await tcs.Task.ConfigureAwait(false); }
            catch { try { if (!proc.HasExited) proc.Kill(); } catch { } throw; }
            return (code, stdOut.ToString(), stdErr.ToString());
        }
    }
}

