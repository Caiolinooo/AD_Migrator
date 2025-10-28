using System.Windows;

namespace MigracaoAD.UI.Views
{
    public partial class NetworkConfigDialog : Window
    {
        public bool ConfigureNetwork => ConfigureNetworkCheck.IsChecked == true;
        public string IPAddress => IPAddressBox.Text;
        public int PrefixLength => int.TryParse(PrefixLengthBox.Text, out int val) ? val : 24;
        public string Gateway => GatewayBox.Text;
        public string DNSServer => DNSServerBox.Text;
        
        public bool PrepareDisk => PrepareDiskCheck.IsChecked == true;
        public int DiskNumber => int.TryParse(DiskNumberBox.Text, out int val) ? val : 1;
        public string DriveLetter => DriveLetterBox.Text.ToUpper();

        public NetworkConfigDialog(string suggestedIP)
        {
            InitializeComponent();
            IPAddressBox.Text = suggestedIP;
            
            // Habilitar/desabilitar painéis baseado nos checkboxes
            ConfigureNetworkCheck.Checked += (s, e) => NetworkPanel.IsEnabled = true;
            ConfigureNetworkCheck.Unchecked += (s, e) => NetworkPanel.IsEnabled = false;
            
            PrepareDiskCheck.Checked += (s, e) => DiskPanel.IsEnabled = true;
            PrepareDiskCheck.Unchecked += (s, e) => DiskPanel.IsEnabled = false;
            
            NetworkPanel.IsEnabled = ConfigureNetworkCheck.IsChecked == true;
            DiskPanel.IsEnabled = PrepareDiskCheck.IsChecked == true;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Validar campos
            if (ConfigureNetwork)
            {
                if (string.IsNullOrWhiteSpace(IPAddress))
                {
                    MessageBox.Show("Informe o IP Address.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(DNSServer))
                {
                    MessageBox.Show("Informe o DNS Server (IP do DC local).", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            
            if (PrepareDisk)
            {
                if (DiskNumber < 0)
                {
                    MessageBox.Show("Número do disco inválido.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var result = MessageBox.Show(
                    $"ATENÇÃO: O disco {DiskNumber} será FORMATADO e todos os dados serão perdidos!\n\nDeseja continuar?",
                    "Confirmar Formatação",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );
                
                if (result != MessageBoxResult.Yes)
                    return;
            }
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

