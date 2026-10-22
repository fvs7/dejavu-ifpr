# Referências Técnicas e Registro Clean-Room

Este documento registra formalmente as referências públicas consultadas para o desenvolvimento do **Dejavu IFPR**.

## Política Clean-Room
O projeto Dejavu IFPR é desenvolvido em estrita conformidade com o método **Clean-Room**:
1. Nenhum binário, driver de kernel (`DeepFrz.sys`), instalador ou documentação confidencial da Faronics Corporation foi descompilado, desmontado, extraído ou reutilizado.
2. Apenas manuais de usuário, fichas técnicas e páginas públicas de produto foram consultados para entender o comportamento funcional esperado (modos *Frozen*, *Thawed*, *ThawSpace* e janelas de manutenção).
3. Todas as implementações utilizam recursos nativos e documentados dos sistemas operacionais (Microsoft Windows e Linux).

---

## Fontes Públicas Consultadas

### 1. Microsoft Learn / Windows Documentation
* **Unified Write Filter (UWF):**
  - Visão Geral do UWF: https://learn.microsoft.com/en-us/windows/iot/iot-enterprise/customize/unified-write-filter
  - WMI Provider Reference (`root\standardcimv2\embedded`): https://learn.microsoft.com/en-us/windows-hardware/customize/enterprise/uwf-wmi-provider-reference
  - Classes WMI utilizadas: `UWF_Filter`, `UWF_Volume` (proteção e exclusões de arquivo), `UWF_Overlay` (consumo e limites), `UWF_OverlayConfig` (tipo e tamanho máximo) e `UWF_RegistryFilter` (exclusões de registro).
  - Gerenciamento de Overlay (RAM vs Disk-backed): https://learn.microsoft.com/en-us/windows-hardware/customize/enterprise/uwf-overlay-location-and-size
  - Exclusões de Arquivos e Registro: https://learn.microsoft.com/en-us/windows-hardware/customize/enterprise/uwf-exclusions
  - Integração com Domínio Active Directory e Conta de Máquina: https://learn.microsoft.com/en-us/troubleshoot/windows-server/identity/machine-account-password-process
* **Boot Nativo de VHDX (Native VHD Boot):**
  - Boot to a virtual hard disk (Native Boot): https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/boot-to-a-virtual-hard-disk--native-boot--add-a-virtual-hard-disk-to-the-boot-menu
  - VHD Diferencial em BCD: https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/deploy-windows-on-a-vhd--native-boot

### 2. Linux / Dual-Boot
* **OverlayFS Kernel Documentation:** https://docs.kernel.org/filesystems/overlayfs.html
* **overlayroot (Ubuntu/Debian):** Manpages e scripts de initramfs (`overlayroot.conf`, `man 8 overlayroot`).

### 3. Faronics Deep Freeze (Comportamento Funcional Público)
* **Manuais e Whitepapers Públicos de Produto:**
  - *Deep Freeze Enterprise User Guide* (Faronics Corporation - especificações públicas de interface e conceitos: Frozen State, Thawed State, ThawSpace, Maintenance Period).
  - *Faronics Core Console Guide* (conceito funcional de orquestração remota e relatórios de estado).
