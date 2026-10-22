# Integração do Dejavu IFPR com o Veyon IFPR

Este guia documenta como controlar remotamente o estado de congelamento dos computadores do laboratório utilizando o **Veyon Master** (`fvs7/veyon_ifpr`).

## 1. Método Nativo via Veyon Master (Execução de Comandos)

O Veyon possui o recurso nativo **Executar Programa / Comandos** (`ExecuteProgram` / `ExecuteCommand`). Como o Veyon Service nos clientes roda sob a conta `SYSTEM`, ele possui privilégios totais para invocar o CLI `dejavu.exe` diretamente.

### Configuração das Ações Rápidas no Veyon Configurator:

1. No Veyon Master do professor, abra as predefinições de comandos.
2. Cadastre os comandos pré-definidos:

- **Congelar Máquinas (Freeze + Reiniciar):**
  ```cmd
  "%ProgramFiles%\Dejavu IFPR\dejavu.exe" freeze C: --restart
  ```

- **Descongelar Máquinas (Thaw + Reiniciar):**
  ```cmd
  "%ProgramFiles%\Dejavu IFPR\dejavu.exe" thaw --restart
  ```

- **Descongelamento Único para Manutenção (Thaw-Once):**
  ```cmd
  "%ProgramFiles%\Dejavu IFPR\dejavu.exe" thaw --once --restart
  ```

- **Disparar Janela Completa de Manutenção:**
  ```cmd
  "%ProgramFiles%\Dejavu IFPR\dejavu.exe" maintenance --run
  ```

## 2. Método via API REST (Serviço Local em Segundo Plano)

Cada cliente do laboratório pode executar o servidor de API na porta administrativa 7777:
```cmd
dejavu.exe serve --port 7777 --token "TOKEN_SECRETO_ADMIN"
```

A partir da máquina do professor ou do servidor de administração, o comando `cluster` do Dejavu permite operar todas as 40 estações simultaneamente em paralelo:

```bash
# Consultar estado de todas as estações e gerar relatório HTML/CSV:
dejavu cluster status --hosts lab3-hosts.txt --token TOKEN_SECRETO_ADMIN --html painel-lab3.html

# Congelar todos os computadores do Lab 3 e reiniciar:
dejavu cluster freeze --hosts lab3-hosts.txt --token TOKEN_SECRETO_ADMIN --restart

# Descongelar todos os computadores em modo Thaw-Once:
dejavu cluster thaw --hosts lab3-hosts.txt --token TOKEN_SECRETO_ADMIN --once --restart
```

## 3. Arquivo de Hosts do Laboratório (`lab3-hosts.txt`)

Lista simples contendo o nome DNS ou IP das estações:
```text
lab3-pc01.acd.ifpr.local
lab3-pc02.acd.ifpr.local
...
lab3-pc40.acd.ifpr.local
```
