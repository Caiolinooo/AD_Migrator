# Checklist de Migração (AD + Shares)

- [ ] Conectividade L3 entre on-prem e nuvem (VPN/ER) validada.
- [ ] Resolução DNS cruzada funcionando (nuvem <-> on-prem).
- [ ] `01-Discovery-AD.ps1` executado e revisado (níveis funcionais, DCs, FSMO, sites, trusts, zonas DNS).
- [ ] `02-Discovery-FileShares.ps1` executado e revisado (shares, ACL raiz, métricas básicas).
- [ ] Decisão do destino de identidade: estender mesmo domínio (este fluxo) ou inter-florestas (ADMT/SIDHistory).
- [ ] VM na nuvem preparada para novo DC (discos para NTDS/Logs/SYSVOL, IP fixo, DNS apontando para on-prem inicialmente).
- [ ] `10-Promote-New-DC.ps1` executado no novo DC; replicação validada.
- [ ] `20-Transfer-FSMO.ps1` executado para mover FSMO ao novo DC.
- [ ] Clientes/servidores apontados para DNS dos DCs na nuvem (ajuste GPO/GPP conforme necessário).
- [ ] `30-Robocopy-Migrate.ps1` testado com pré-cópia; delta final planejado (janela de manutenção).
- [ ] Se Azure Files: RBAC aplicado no share e acesso testado com NTFS herdado.
- [ ] `40-Demote-Old-DC.ps1` executado após todas as validações.
- [ ] Pós-corte: limpeza de metadados (nTDSDSA) se necessário; remoção de registros DNS antigos; documentação atualizada.
