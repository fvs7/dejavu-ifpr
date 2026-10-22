# ADR-002: Compatibilidade com Active Directory e Relação de Confiança de Máquina

## Contexto
Nos laboratórios do IFPR Campus União da Vitória, as estações de trabalho ingressam no domínio Active Directory (`acd.ifpr.local`, gerenciado via Samba 4 / Zentyal). Em ambientes convencionais com Deep Freeze ou filtros de escrita (UWF), computadores congelados por longos períodos correm risco de perder a relação de confiança com o controlador de domínio (erro: *"The trust relationship between this workstation and the primary domain failed"* / ID de evento Netlogon 3210 ou 5719).

Por padrão, o Windows negocia a rotação da senha da conta de máquina (`MACHINE$`) a cada 30 dias.
Se a máquina estiver congelada, a troca de senha ocorre na memória do Windows (e é registrada no AD), mas no próximo boot ela é descartada do disco local. O cliente passa a tentar autenticar com a senha antiga e é rejeitado pelo domínio.

## Opções Analisadas

### Opção 1: Exclusão UWF da chave de registro Netlogon e segredos LSA
- **Como funciona:** Adiciona-se uma exclusão de registro no UWF para `HKLM\SYSTEM\CurrentControlSet\Services\Netlogon\Parameters`.
- **Problema Técnico:** A chave da conta de máquina (`$MACHINE.ACC`) e os segredos de autenticação kerberos residem em **`HKLM\SECURITY\Policy\Secrets`** (protegido por ACL de sistema/SYSTEM e gerido pelo subsistema LSASS).
- **Limitação do UWF:** O UWF não suporta com segurança a exclusão parcial de subchaves de `HKLM\SECURITY` sem introduzir instabilidades no subsistema de segurança local e problemas de sincronia transacional do registry hive.

### Opção 2: Desativar a troca periódica de senha da máquina no cliente (`DisablePasswordChange = 1`)
- **Como funciona:** Configura-se via chave de registro ou GPO a diretiva de segurança do Windows:
  `HKLM\SYSTEM\CurrentControlSet\Services\Netlogon\Parameters\DisablePasswordChange = 1`
- **Vantagens:**
  - Solução oficial e recomendada pela Microsoft para sistemas embarcados (Windows Embedded / IoT) e ambientes congelados.
  - Não depende de exclusão de chaves voláteis de segurança.
  - Máquinas podem ficar meses ou anos congeladas sem nunca quebrar o ingresso no domínio.
  - Não interfere nas senhas dos usuários ou alunos que logam pelo domínio.
- **Análise de Risco / Trade-off de Segurança:**
  - *Risco teórico:* Uma máquina clonada com o mesmo SID ou cuja senha de conta de computador seja capturada na rede poderia forjar a identidade da máquina.
  - *Mitigação no Lab:* Os computadores do laboratório estão em VLAN restrita, com tráfego SMB assinado e o tráfego dos alunos não possui privilégios de Administrador local. O benefício operacional no campus acadêmico supera largamente o risco teórico.

### Opção 3: Rotação exclusivamente durante a Janela de Manutenção (Thawed)
- **Como funciona:** O cliente mantém `DisablePasswordChange = 1` por padrão enquanto congelado. Durante a janela de manutenção programada (que ocorre em estado descongelado), um script pode invocar `Reset-ComputerMachinePassword` se necessário.

## Decisão
Adotar a **Opção 2 combinada com a Opção 3**:
1. O comando `dejavu domain-setup` aplicará automaticamente `DisablePasswordChange = 1` em `HKLM\SYSTEM\CurrentControlSet\Services\Netlogon\Parameters`.
2. Excluir explicitamente a pasta `%SystemRoot%\System32\GroupPolicy` e os arquivos de cache de kerberos/GPO para garantir que alterações de políticas institucionais sejam lidas do controlador de domínio sem inconsistências.
3. Permitir renovação forçada sob demanda via CLI em modo Thawed se o administrador desejar.

## Status
Aprovado para implementação na Fase 2.
