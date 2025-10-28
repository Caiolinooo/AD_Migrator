# Changelog

All notable changes to AD Migration Suite will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2025-10-28

### 🐛 Fixed

- **Windows Server 2012 R2 Compatibility**: Replaced `Test-NetConnection` with `Test-Connection` and `System.Net.Sockets.TcpClient` for port testing
  - Fixed agent connectivity tests on older Windows Server versions
  - Updated `AgentController.cs` to use compatible PowerShell commands
  - Updated `PowershellService.cs` to use TcpClient for port scanning

### 📚 Added

- **TROUBLESHOOTING.md**: Comprehensive troubleshooting guide with:
  - 10 common problems and solutions
  - PowerShell diagnostic commands
  - Installation checklist
  - Support information

### 🔧 Changed

- Agent version updated to 1.0.1
- Installer package updated to 1.0.1

---

## [1.0.0] - 2025-01-28

### Added

#### Core Features
- **Agent-Based Architecture**: Revolutionary client-server system replacing unreliable WinRM
  - Lightweight Windows Service agent (99 MB single executable)
  - REST API communication over HTTP (port 8765)
  - Token-based authentication (no username/password required)
  - Self-contained deployment with .NET runtime included
  
- **Management Console (WPF Application)**
  - Modern, intuitive interface with light theme
  - 7-step wizard for guided migration process
  - Real-time connection testing
  - Automatic network detection and IP scanning
  - Integrated credential management
  - Agent configuration and testing interface
  
- **Remote Management Capabilities**
  - Execute PowerShell commands remotely
  - Retrieve system information (OS, domain, roles)
  - Configure network settings (IP, DNS, gateway)
  - Install Windows roles and features
  - Manage Active Directory operations
  - Test connectivity between servers
  
#### Agent System
- **Installation Scripts**
  - `install-agent.ps1`: Automated agent installation and service configuration
  - `uninstall-agent.ps1`: Clean removal of agent and all components
  - Automatic firewall rule creation
  - Environment variable management for token storage
  
- **REST API Endpoints**
  - `/health`: Health check and status monitoring
  - `/api/agent/execute`: Execute PowerShell commands
  - `/api/agent/system-info`: Get system information
  - `/api/agent/domain-info`: Get Active Directory domain information
  - `/api/agent/network-config`: Configure network settings
  - `/api/agent/install-role`: Install Windows Server roles
  - `/api/agent/test-connectivity`: Test network connectivity
  - `/api/agent/configure-firewall`: Configure Windows Firewall
  
#### User Interface
- **Environment & Credentials Page**
  - Prominent agent configuration section (blue highlighted)
  - Token and port configuration
  - Individual connection testing for origin and destination servers
  - Automatic configuration buttons
  - Fallback credentials section (collapsed by default)
  - Network detection and IP scanning
  
- **Agent Test Page**
  - Comprehensive agent testing interface
  - Connection status indicators
  - System information retrieval
  - Custom PowerShell command execution
  - Real-time output display
  
- **Modern UI Design**
  - Light theme with high contrast
  - Large, readable fonts (14pt base)
  - Card-based layout
  - Color-coded sections (blue for agent, orange for fallback)
  - Responsive design
  
#### Documentation
- **README.md**: Professional corporate documentation with pricing and licensing
- **README_AGENTE.md**: Complete agent system documentation
- **COMO_FUNCIONA_AGENTE.md**: Detailed explanation of agent architecture
- **agent/INSTALACAO_RAPIDA.md**: Quick installation guide
- **agent/README.md**: Technical API reference
- **GUIA_AGENTE.md**: Comprehensive Portuguese guide
  
#### Installation & Deployment
- **MSI Installer (WiX)**
  - Component selection (Manager and/or Agent)
  - Custom installation paths
  - Agent token configuration during installation
  - Automatic firewall configuration
  - Start menu and desktop shortcuts
  - Optional launch after installation
  
- **NSIS Installer**
  - Alternative installer with custom UI
  - Interactive agent configuration page
  - Service installation and startup
  - Uninstaller included
  
- **Portable ZIP Package**
  - Self-contained installation script
  - No external dependencies
  - Command-line installation options
  - Silent installation support
  - 42 MB compressed package
  
#### Development Tools
- **Build Scripts**
  - `installer/build-installer.ps1`: WiX-based MSI builder
  - `installer/Create-Installer.ps1`: Complete build and package script
  - `installer/Package-Installer.ps1`: Quick packaging from existing builds
  - Automated compilation and packaging
  
### Changed
- **Replaced WinRM with Agent System**: Complete architectural change for better reliability
- **Merged Credential Page**: Integrated credentials into Environment page for better UX
- **Reorganized UI**: Prioritized agent configuration over traditional credentials
- **Improved Error Handling**: Better error messages and fallback mechanisms

### Fixed
- **WinRM Reliability Issues**: Eliminated by replacing with agent-based architecture
- **Text Visibility**: Fixed input field text visibility issues
- **XAML Resource Embedding**: Fixed EXE not opening by properly embedding XAML resources
- **Network Detection**: Improved automatic IP detection and scanning
- **Build Errors**: Resolved PublishReadyToRun and LINQ extension method issues

### Security
- **Token-Based Authentication**: Secure authentication without exposing credentials
- **Environment Variable Storage**: Secure token storage in system environment
- **Firewall Integration**: Automatic firewall rule creation for secure communication
- **No Credential Storage**: Agent system eliminates need to store domain credentials

### Performance
- **Single Executable**: Agent compiled as single 99 MB self-contained executable
- **Fast Communication**: Direct HTTP communication faster than WinRM
- **Efficient Packaging**: 42 MB compressed installer package
- **Quick Installation**: Automated installation in under 2 minutes

### Technical Details
- **.NET 8.0**: Built on latest .NET framework
- **ASP.NET Core**: Modern web API framework for agent
- **WPF**: Windows Presentation Foundation for rich UI
- **PowerShell Integration**: Native PowerShell command execution
- **Windows Service**: Agent runs as background Windows Service
- **Self-Contained Deployment**: No .NET runtime installation required for agent

### Known Issues
- Agent requires .NET 8.0 runtime on target servers (included in self-contained build)
- Firewall rules must be manually configured if installation script fails
- Service may require manual start if installation occurs during system updates

### Upgrade Notes
- First release - no upgrade path from previous versions
- Clean installation recommended
- Backup existing configurations before installing

### Deprecations
- WinRM support maintained as fallback but deprecated
- Traditional credential-based authentication deprecated in favor of token-based

### Roadmap for v1.1.0
- [ ] HTTPS support for encrypted communication
- [ ] Multi-token support for different security levels
- [ ] Web-based management console
- [ ] Automated migration scheduling
- [ ] Migration rollback capabilities
- [ ] Detailed logging and audit trail
- [ ] Email notifications for migration events
- [ ] Support for multiple simultaneous migrations

---

## Release Statistics

- **Total Files**: 50+
- **Lines of Code**: 15,000+
- **Documentation Pages**: 8
- **API Endpoints**: 8
- **Installation Methods**: 3 (MSI, NSIS, ZIP)
- **Supported Windows Versions**: Server 2012 R2, 2016, 2019, 2022
- **Package Size**: 42 MB (compressed), 150 MB (installed)

---

## Contributors

- Development Team: AD Migration Suite Team
- Architecture: Agent-based client-server system
- UI/UX: Modern WPF with light theme
- Documentation: Comprehensive multi-language support

---

## License

Commercial Software - Enterprise License Required

Copyright © 2025 AD Migration Suite Team. All rights reserved.

---

[1.0.0]: https://github.com/yourusername/ad-migration-suite/releases/tag/v1.0.0

