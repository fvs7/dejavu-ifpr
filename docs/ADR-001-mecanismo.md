# ADR 001: Mecanismo de Congelamento de Estado (Deep Freeze Open Source)

* **Status:** Proposta (Aguardando aprovação para Fase 1)
* **Data:** 22/10/2026
* **Autor:** Fernando Vieira (Técnico de TI - IFPR União da Vitória)
* **Contexto:** Projeto REA Freeze — Restauração de estado limpo a cada reinicialização nos computadores dos laboratórios acadêmicos.

---

## 1. Contexto e Problema
Os laboratórios do IFPR Campus União da Vitória (piloto no Laboratório 3, com 40 PCs + 1 PC professor) operam com máquinas Windows 11 integradas ao domínio Active Directory institucional (`acd.ifpr.local` gerenciado por servidor Zentyal). Alunos realizam login individual, utilizam ferramentas pesadas de desenvolvimento (Android Studio, IntelliJ, VS Code) e engenharia (AutoCAD).

Para manter a integridade operacional e eliminar acúmulo de arquivos temporários, alterações acidentais de configuração e infecções por malware, é necessário um mecanismo que reverta o sistema ao estado-base a cada boot (*congelamento*), com capacidade de alternar para modo descongelado (*thaw*) para manutenção.

---

## 2. Opções Avaliadas

### Opção A: Unified Write Filter (UWF)
O UWF é um recurso nativo do Windows em nível de setor de disco que redireciona todas as operações de gravação para um cache de sobreposição (*overlay*) na memória RAM ou em arquivo de disco temporário.

* **Requisitos de Edição do Windows:**
  - Suportado oficialmente no **Windows 10/11 Education, Enterprise e IoT Enterprise**.
  - Não é habilitado por padrão no Windows 11 Pro convencional (embora pacotes de servicing possam ser adicionados via DISM, a licença e suporte oficial exigem Education/Enterprise).
  - *Nota IFPR:* Instituições federais de ensino possuem convênios acadêmicos (Microsoft EES/M365 A3/A5) que fornecem chaves de ativação para **Windows 11 Education**.
* **Vantagens:**
  - **Nativo e Estável:** Desenvolvido e mantido pela Microsoft; dispensa drivers de terceiros não homologados.
  - **Exclusões Granulares Nativas:** Permite configurar exceções persistentes para arquivos específicos, pastas e chaves do Registro via WMI (`root\standardcimv2\embedded`).
  - **Excelente Desempenho:** Gravações vão para o overlay em memória RAM, tornando operações de I/O instantâneas.
  - **Proteção contra Desgaste de SSD:** Reduz drasticamente os ciclos de escrita na memória flash.
  - **Compatibilidade com Domínio:** Permite excluir a chave de registro do Netlogon ou persistir o cache de credenciais sem desassociar do domínio.
* **Desvantagens / Riscos:**
  - Limite de Overlay: Se o aluno baixar um arquivo de 10 GB na unidade C: e o overlay for RAM de 4 GB, o sistema pode travar ou emitir tela azul se o overlay esgotar (requer monitoramento e alocação de overlay em disco ou partição D: persistente).
  - Exige edição Education ou Enterprise.

---

### Opção B: Boot Nativo de VHDX com Disco Diferencial
Nesta arquitetura, o Windows não inicializa de uma partição física direta, mas sim de um arquivo VHDX mestre (`base.vhdx`, somente-leitura) associado a um VHDX diferencial (`child.vhdx`). A cada reinicialização, um script no BCD / WinPE / tarefa descarta o `child.vhdx` e recria um novo apontando para o pai.

* **Requisitos de Edição do Windows:**
  - Suportado no Windows 10/11 **Pro, Enterprise e Education**.
* **Vantagens:**
  - Funciona na edição **Pro** (caso o campus não possua chave Education).
  - Reset 100% determinístico a nível de bloco a cada boot.
* **Desvantagens / Riscos:**
  - **Complexidade de Particionamento:** Quebra o fluxo simples de clonagem via FOG Project (`Single Disk – Resizable`), exigindo particionamento manual e manipulação complexa do BCD.
  - **Penalidade de Desempenho:** VHDs diferenciais têm overhead de I/O em leitura/escrita e sofrem com fragmentação.
  - **Queda de Energia:** Se ocorrer queda de energia enquanto o script de reset de boot recria o VHDX, a máquina pode entrar em loop de reparo automático do Windows (BSOD `INACCESSIBLE_BOOT_DEVICE`).
  - **Dificuldade de Exclusões:** Não permite excluir uma pasta específica (como D:\Persistente ou chaves do registro) de forma nativa e transparente sem montar múltiplos discos virtuais.

---

### Opção C: Fallback Linux (Overlayroot / OverlayFS)
Para laboratórios com dual-boot (ex.: Xubuntu / Debian / Arch):
* O pacote `overlayroot` (ou configuração customizada no initramfs com `overlayfs`) monta o sistema de arquivos raiz (`/`) em modo somente-leitura e cria uma camada de escrita temporária em memória (`tmpfs`).
* **Vantagens:** Extremamente leve, nativo do kernel Linux, robusto contra quedas de energia.
* **Aplicação:** Será adotado como padrão para os laboratórios e máquinas que operem sob Linux.

---

## 3. Matriz Comparativa

| Critério | Opção A: UWF (Windows) | Opção B: VHDX Diferencial | Opção C: Overlayroot (Linux) |
|---|---|---|---|
| **Compatibilidade de Edição** | Requer Education/Enterprise | Pro, Education, Enterprise | Qualquer distribuição Linux |
| **Robustez em Queda de Energia** | **Altíssima** (dados voláteis somem, sistema intacto) | **Média** (risco de corrupção do VHDX filho) | **Altíssima** (`tmpfs` descartado no boot) |
| **Desempenho I/O** | **Máximo** (leituras SSD, escritas em RAM) | Reduzido (overhead de VHDX dinâmico) | **Máximo** (RAM / tmpfs) |
| **Exclusões Granulares** | **Nativas** (arquivos, pastas, registry via WMI) | Difícil (exige partições extras) | Nativas via bind mount |
| **Compatibilidade AD / Domínio** | **Alta** (exclusão Netlogon documentada) | Complexa (risco de dessincronia) | Alta (via SSSD/Kerberos) |
| **Atualização da Imagem-Base** | **Fácil** (`reafreeze thaw` -> update -> `freeze`) | Complexa (merge de VHDs ou boot no pai) | Simples (`overlayroot-chroot`) |
| **Integração com FOG Project** | **Perfeita** (partição NTFS normal) | Ruim (imagens gigantes de VHD) | Perfeita |

---

## 4. Decisão Recomendada

1. **Ambiente Windows (Foco Principal do Lab 3):**
   * **Adotar a Opção A: Unified Write Filter (UWF)** como mecanismo central do **REA Freeze**.
   * **Justificativa:** É a solução mais alinhada à estabilidade em laboratórios de informática, possui suporte nativo a exclusões granulares (cruciais para não quebrar a relação de confiança com o Active Directory/Zentyal e permitir atualização do antivírus/Veyon) e integra-se perfeitamente com a infraestrutura de deploy do FOG Project já em uso.
   * **Verificação de Edição:** Se a imagem base instalada for Windows 11 Pro, ela deve ser ativada como **Windows 11 Education** utilizando a chave de licenciamento acadêmico institucional do IFPR (procedimento suportado pela Microsoft com `changepk.exe /ProductKey <chave-education>` sem reinstalação).

2. **Ambiente Linux (Labs Dual-Boot):**
   * Adotar a **Opção C: Overlayroot / OverlayFS** para o particionamento Linux.

---

## 5. Próximos Passos (Fase 1)
Com a aprovação desta ADR:
1. Desenvolvimento da ferramenta CLI `reafreeze.exe` em C# .NET 8 utilizando o namespace WMI `root\standardcimv2\embedded`.
2. Implementação dos comandos `status`, `freeze`, `thaw` e `thaw --once`.
3. Perfil de exclusões padrão em JSON (`exclusions.json`).
4. Logging no Windows Event Log (fonte `REA Freeze`) e arquivo local rotacionado.
