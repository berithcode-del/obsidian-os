# Comece aqui — Obsidian OS no GA36

**Estado em 30/10/2026:** a imagem de laboratório do Obsidian OS 1.0 está pronta e verificada — `08-obsidian-installer/artifacts/releases/obsidian-os-1.0-r1/obsidian-os-1.0-lab-r1.img`, 1.073.741.824 bytes, SHA-256 `094f8e698a236d3e1db64ad4f69a2e3e2bebbb3846728595972167221c8b3fa8`. O instalador Windows grava o cartão de verdade (com releitura e criação da área `OBSIDIAN`) e restaura o backup de conteúdo de pasta ou de imagem `.img`. O passo a passo está no [guia de instalação 1.0](08-obsidian-installer/docs/GUIA-INSTALACAO-1.0.md).

**Projeto do instalador portátil:** [08-obsidian-installer/README.md](08-obsidian-installer/README.md). O [planejamento da aplicação](08-obsidian-installer/PLANEJAMENTO-APLICACAO.md) reúne a proposta comercial, a estrutura e as próximas etapas. O [conhecimento validado](08-obsidian-installer/docs/CONHECIMENTO-VALIDADO.md) consolida a prova de instalação USB concluída em 29/10/2026. O nome adotado é **Obsidian Installer / Obsidian OS**.

A investigação original do instalador para cartões novos está em [01-projeto/INSTALADOR-OBSISIDIAN-OS.md](01-projeto/INSTALADOR-OBSISIDIAN-OS.md), com diagnóstico USB passivo em `01-projeto/instalador/diagnosticar-usb.ps1`. Esses materiais preservam a grafia histórica “Obsisidian”; a consolidação atual está na nova pasta do instalador.

Projeto separado para estudar e criar uma interface GameMan para o console GA36 usando goodluckOS. O projeto Android existente continua no repositório E:\BerithCodex\GameMan.

- `01-projeto/PLANO.md`: visão, etapas, critérios e riscos.
- `01-projeto/ESTADO-ATUAL.md`: fatos observados, arquivos e pendências.
- `02-imagens-personalizadas/`: imagens goodluckOS A33 produzidas com dados de inicialização do cartão original. A opção painel 1 iniciou no aparelho.
- `03-imagens-base/`: imagens e arquivos oficiais de origem. NÃO gravar a imagem base diretamente: ela precisa dos dados de inicialização individuais.
- `04-ferramentas/`: Rufus portátil e sua configuração.
- `05-downloads-incompletos/`: tentativa incompleta de dARKOSRE. Não usar para instalação.
- `06-testes-e-registros/`: registros dos testes em aparelho e utilitários de captura.

O backup do cartão original permanece em `D:\R36S-backup-sistema-2026-10-28.img`; sua restauração integral não foi testada. O arquivo `D:\dArkOSRE_R36_trixie_03082026.img` não iniciou no teste e permanece onde estava. Preserve ambos e o cartão original.

Próxima etapa: gravar a imagem 1.0 em um cartão separado com o instalador, iniciar no console e registrar a conferência de splash, launcher, jogos, leitura, vídeo, USB/MTP, TF2 e energia. Depois, resolver assinatura e revisão de distribuição. O desenvolvimento da interface do sistema continua em `01-projeto/`.
