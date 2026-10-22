# Dejavu IFPR

Equivalente Open Source ao Deep Freeze para laboratórios acadêmicos do **IFPR – Campus União da Vitória**.

[![License: GPL v2](https://img.shields.io/badge/License-GPL%20v2-blue.svg)](LICENSE)

---

## Objetivo
Garantir que, a cada reinicialização, os computadores dos laboratórios retornem exatamente ao seu estado original padronizado, eliminando acúmulo de arquivos, infecções por malware e desconfigurações feitas por alunos, com suporte a modos **Congelado** (*Frozen*) e **Descongelado** (*Thawed*) para manutenção preventiva e remota.

---
## Princípios de Projeto
1. **Clean-Room:** Projeto desenvolvido 100% com base em documentação pública e APIs nativas do Windows e Linux. Nenhuma engenharia reversa ou código proprietário.
2. **Mecanismos Nativos:** Uso do **Unified Write Filter (UWF)** no Windows e **OverlayFS/overlayroot** no Linux, eliminando a necessidade de drivers de kernel de terceiros.
3. **Compatibilidade Institucional:** Proteção da relação de confiança com o domínio Active Directory / Zentyal (`acd.ifpr.local`) e integração nativa com o **Veyon IFPR**.

---
## Documentação de Arquitetura
* [ADR 001 — Comparativo e Decisão de Mecanismo](docs/ADR-001-mecanismo.md)
* [ADR 002 — Compatibilidade com Active Directory](docs/ADR-002-dominio-ad.md)
* [Guia de Implantação no Laboratório](docs/guia-implantacao.md)
* [Referências e Registro Clean-Room](docs/referencias.md)
