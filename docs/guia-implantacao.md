# Guia de Implantação — Dejavu IFPR (Windows)

Procedimento para preparar uma máquina de laboratório com o `dejavu.exe`. Todos os comandos devem ser executados em um terminal **como Administrador**.

## 1. Pré-requisitos

1. Windows 11 **Education** ou **Enterprise** (ver [ADR-001](ADR-001-mecanismo.md)). Para converter uma imagem Pro:
   ```
   changepk.exe /ProductKey <chave-education-institucional>
   ```
2. Recurso do UWF habilitado (requer reinicialização):
   ```
   dism /online /enable-feature /featurename:Client-UnifiedWriteFilter /all
   ```
3. Máquina já ingressada no domínio `acd.ifpr.local`, com Veyon e softwares do laboratório instalados.
4. Copiar `dejavu.exe` (artefato do CI, conferir o `.sha256`) e `exclusions.default.json` para `C:\Program Files\Dejavu\`.

## 2. Configuração inicial (sistema descongelado)

```
cd "C:\Program Files\Dejavu"
dejavu setup-domain
dejavu setup-thawspace C:\Persistente
dejavu apply-profile exclusions.default.json
dejavu config-overlay --type ram --max-mb 4096 --warn-mb 3072 --crit-mb 3840
dejavu setup-overlay-monitor --interval 5
dejavu status
```

Ajuste o tamanho do overlay à memória RAM da máquina. Para máquinas com pouca RAM, use `--type disk`.

## 3. Congelar

```
dejavu freeze C: --restart
```

Após o boot, `dejavu status` deve mostrar **Estado Atual: CONGELADO**.

## 4. Operação do dia a dia

| Situação | Comando |
|---|---|
| Instalar ou alterar software uma vez | `dejavu thaw --once --restart` — o próximo boot fica descongelado e o seguinte volta congelado |
| Manutenção prolongada | `dejavu thaw --restart`, fazer as alterações e depois `dejavu freeze --restart` |
| Atualizações automáticas (winget, Windows Update, GPO) | `dejavu maintenance` e reiniciar — a máquina atualiza, recongela e reinicia sozinha |
| Verificar o estado | `dejavu status` ou `dejavu status --json` |

O estado da última manutenção fica em `HKLM\SOFTWARE\Dejavu\Maintenance` (`State` = `COMPLETED`, `FAILED`, `FAILED_STILL_FROZEN` ou `ABORTED_LOOP_DETECTED`).

## 5. Logs

* Arquivo: `C:\ProgramData\Dejavu\logs\dejavu.log`
* Visualizador de Eventos: log **Aplicativo**, fonte `Dejavu IFPR`

## 6. Tarefas agendadas criadas

| Tarefa | Quando roda | Função |
|---|---|---|
| `Dejavu_FinalizeThawOnce` | Inicialização (somente após `thaw --once`) | Programa o recongelamento |
| `Dejavu_MaintenanceRun` | Inicialização (somente após `maintenance`) | Executa a janela de manutenção |
| `Dejavu_OverlayMonitor` | A cada N minutos | Avisa e reinicia se o overlay atingir o limite crítico |
