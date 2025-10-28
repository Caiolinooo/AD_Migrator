@{
    # Identidade / AD (deixe em branco para o orquestrador perguntar em runtime)
    DomainName            = ''
    SiteName              = ''
    OldDCHostname         = ''
    NewDCHostname         = ''
    DnsServer             = $null  # opcional: 'OLDDC01' ou IP do DNS

    # Caminhos do novo DC (ajuste discos/letras conforme sua VM)
    DatabasePath          = 'D:\\NTDS'
    LogPath               = 'E:\\Logs'
    SYSVOLPath            = 'D:\\SYSVOL'

    # Migração de Fileserver (deixe em branco para perguntar em runtime)
    SourceFileServer      = ''                           # servidor origem (NetBIOS)
    DestinationFileServer = ''                           # servidor destino (NetBIOS)
    DestinationRootPath   = ''                           # raiz onde criar pastas dos shares
    CreateDestinationShares = $true                      # cria shares no destino com mesmo nome

    # Robocopy
    FileCopyThreads       = 32
    RobocopyRetry         = 2
    RobocopyWait          = 2

    # Orquestração
    AutoProceed           = $true                        # default: automático; use -AutoProceed:$false para modo com confirmações
    UseRemoting           = $false                       # se $true, usa PowerShell Remoting p/ DC novo e FileServer destino

    # Layout / Paths
    OutputsDir            = '..\\outputs'
    MapsCsv               = '..\\samples\\maps.csv'     # será gerado ou usado pelo Robocopy
}
