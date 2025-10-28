using System;
using System.IO;
using System.Windows;

namespace MigracaoAD.UI;

public partial class App : Application
{
    public static Branding Branding { get; private set; } = new Branding();
    public State State { get; private set; } = new State();

    protected override void OnStartup(StartupEventArgs e)
    {
        // Handler global de exceções
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            LogError("UnhandledException", ex.ExceptionObject as Exception);
            MessageBox.Show($"Erro fatal: {(ex.ExceptionObject as Exception)?.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, ex) =>
        {
            LogError("DispatcherUnhandledException", ex.Exception);
            MessageBox.Show($"Erro na interface: {ex.Exception.Message}\n\nDetalhes: {ex.Exception.StackTrace}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        try
        {
            base.OnStartup(e);
            Branding = Branding.Load();
            Resources["Branding"] = Branding;

            var splash = new SplashWindow();
            splash.Show();
            var main = new MainWindow();
            main.ContentRendered += (_, __) => splash.Close();
            main.Show();
        }
        catch (Exception ex)
        {
            LogError("OnStartup", ex);
            MessageBox.Show($"Erro ao iniciar aplicação:\n\n{ex.Message}\n\nDetalhes: {ex.StackTrace}", "Erro Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void LogError(string context, Exception? ex)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex?.Message}\n{ex?.StackTrace}\n\n";
            File.AppendAllText(logPath, message);
        }
        catch
        {
            // Ignora erros ao logar
        }
    }
}

