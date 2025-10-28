using System;
using System.Windows;
using System.Windows.Controls;
using MigracaoAD.UI.Services;

namespace MigracaoAD.UI.Views
{
    public partial class CredentialsPage : Page
    {
        private readonly State _state;

        public CredentialsPage(State state)
        {
            InitializeComponent();
            _state = state;
            LoadState();
        }

        private void LoadState()
        {
            // Carregar credenciais salvas (se houver)
            SourceUsername.Text = _state.SourceUsername ?? "Administrator";
            TargetUsername.Text = _state.TargetUsername ?? "Administrator";
            
            SourceConnectionMethod.SelectedIndex = _state.SourceConnectionMethod;
            TargetConnectionMethod.SelectedIndex = _state.TargetConnectionMethod;
            
            SourceUseDomainCreds.IsChecked = _state.SourceUseDomainCreds;
            TargetUseDomainCreds.IsChecked = _state.TargetUseDomainCreds;
            
            AutoConfigureSource.IsChecked = _state.AutoConfigureSource;
            AutoConfigureTarget.IsChecked = _state.AutoConfigureTarget;
            
            WinRMPortHTTP.Text = _state.WinRMPortHTTP.ToString();
            WinRMPortHTTPS.Text = _state.WinRMPortHTTPS.ToString();
            SSHPort.Text = _state.SSHPort.ToString();
            
            UseHTTPS.IsChecked = _state.UseHTTPS;
            SkipCACheck.IsChecked = _state.SkipCACheck;
            SkipCNCheck.IsChecked = _state.SkipCNCheck;
            
            ConnectionTimeout.Text = _state.ConnectionTimeout.ToString();
        }

        private void SaveState()
        {
            _state.SourceUsername = SourceUsername.Text;
            _state.SourcePassword = SourcePassword.Password;
            _state.SourceConnectionMethod = SourceConnectionMethod.SelectedIndex;
            _state.SourceUseDomainCreds = SourceUseDomainCreds.IsChecked == true;
            
            _state.TargetUsername = TargetUsername.Text;
            _state.TargetPassword = TargetPassword.Password;
            _state.TargetConnectionMethod = TargetConnectionMethod.SelectedIndex;
            _state.TargetUseDomainCreds = TargetUseDomainCreds.IsChecked == true;
            
            _state.AutoConfigureSource = AutoConfigureSource.IsChecked == true;
            _state.AutoConfigureTarget = AutoConfigureTarget.IsChecked == true;
            
            if (int.TryParse(WinRMPortHTTP.Text, out int httpPort))
                _state.WinRMPortHTTP = httpPort;
            
            if (int.TryParse(WinRMPortHTTPS.Text, out int httpsPort))
                _state.WinRMPortHTTPS = httpsPort;
            
            if (int.TryParse(SSHPort.Text, out int sshPort))
                _state.SSHPort = sshPort;
            
            _state.UseHTTPS = UseHTTPS.IsChecked == true;
            _state.SkipCACheck = SkipCACheck.IsChecked == true;
            _state.SkipCNCheck = SkipCNCheck.IsChecked == true;
            
            if (int.TryParse(ConnectionTimeout.Text, out int timeout))
                _state.ConnectionTimeout = timeout;
        }

        private async void TestSourceConnection_Click(object sender, RoutedEventArgs e)
        {
            SaveState();
            SourceConnectionStatus.Text = "⏳ Testando...";
            SourceConnectionStatus.Foreground = System.Windows.Media.Brushes.Yellow;
            TestSourceConnection.IsEnabled = false;

            try
            {
                var remoteConfig = new RemoteConfigService();
                var result = await remoteConfig.TestConnectionAsync(
                    _state.SourceDCIP,
                    _state.SourceUsername,
                    _state.SourcePassword,
                    (ConnectionMethod)_state.SourceConnectionMethod,
                    _state.SourceUseDomainCreds ? _state.SourceDomainName : null
                );

                if (result.Success)
                {
                    SourceConnectionStatus.Text = $"✅ Conectado! {result.Message}";
                    SourceConnectionStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                }
                else
                {
                    SourceConnectionStatus.Text = $"❌ Falhou: {result.Message}";
                    SourceConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
                    MessageBox.Show($"Erro ao conectar:\n\n{result.Message}\n\nDica: {result.Hint}", 
                        "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SourceConnectionStatus.Text = $"❌ Erro: {ex.Message}";
                SourceConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                TestSourceConnection.IsEnabled = true;
            }
        }

        private async void TestTargetConnection_Click(object sender, RoutedEventArgs e)
        {
            SaveState();
            TargetConnectionStatus.Text = "⏳ Testando...";
            TargetConnectionStatus.Foreground = System.Windows.Media.Brushes.Yellow;
            TestTargetConnection.IsEnabled = false;

            try
            {
                var remoteConfig = new RemoteConfigService();
                var result = await remoteConfig.TestConnectionAsync(
                    _state.TargetDCIP,
                    _state.TargetUsername,
                    _state.TargetPassword,
                    (ConnectionMethod)_state.TargetConnectionMethod,
                    _state.TargetUseDomainCreds ? _state.TargetDomainName : null
                );

                if (result.Success)
                {
                    TargetConnectionStatus.Text = $"✅ Conectado! {result.Message}";
                    TargetConnectionStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                }
                else
                {
                    TargetConnectionStatus.Text = $"❌ Falhou: {result.Message}";
                    TargetConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
                    MessageBox.Show($"Erro ao conectar:\n\n{result.Message}\n\nDica: {result.Hint}", 
                        "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                TargetConnectionStatus.Text = $"❌ Erro: {ex.Message}";
                TargetConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                TestTargetConnection.IsEnabled = true;
            }
        }
    }
}

