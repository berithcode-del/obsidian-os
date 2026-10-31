# Obsidian Installer

**Estado em 30/10/2026:** a **imagem Obsidian OS 1.0 de laboratório está pronta e verificada** (`artifacts/releases/obsidian-os-1.0-r1/`, 1.073.741.824 bytes, SHA-256 `094f8e69…3fa8`). O instalador Windows passou a gravar o cartão de verdade (bloqueio de volumes, escrita bruta, releitura, partição `OBSIDIAN` e formatação) e a restaurar conteúdo de pasta ou de imagem `.img`. Falta apenas a prova física: gravar um cartão, iniciar a imagem no console e registrar os testes de lazer, leitura, vídeo, MTP e energia. O manifesto segue `channel: lab`, com instalação de cliente desativada.

Aplicativo portátil Windows para identificar aparelhos compatíveis, instalar ou recuperar o Obsidian OS por USB e preparar o armazenamento de jogos.

## Comece por aqui

1. [Guia de instalação 1.0](docs/GUIA-INSTALACAO-1.0.md) — gravar o cartão, preparar a área de dados, restaurar o conteúdo e conferir no console.
2. [Obsidian OS 1.0 — referência e imagem](docs/OBSIDIAN-OS-1.0.md) — composição da imagem, hashes e o que falta provar.
3. [Planejamento da aplicação](PLANEJAMENTO-APLICACAO.md) — produto, serviço comercial, funcionalidades e prioridades ([versão para leitura](PLANEJAMENTO-APLICACAO.html)).
4. [Conhecimento validado](docs/CONHECIMENTO-VALIDADO.md) — o que foi medido, como reproduzir e o que continua pendente.
5. [Arquitetura](docs/ARQUITETURA.md), [fluxo do cliente](docs/FLUXO-CLIENTE.md) e [integração das imagens](docs/INTEGRACAO-IMAGENS.md).
6. [Homologação](docs/HOMOLOGACAO.md), [operação do serviço](docs/OPERACAO-SERVICO.md) e [decisões técnicas](docs/decisoes/0001-base-tecnica.md).
7. [Ponto de retomada e tarefas](docs/RETOMADA.md) e [análise integrada](docs/ANALISE-INTEGRADA.md).

## O que o aplicativo faz hoje

| Etapa | Estado |
| --- | --- |
| 1 Conectar console | Consulta USB sob clique, leitura apenas |
| 2 Compatibilidade | Correspondência informativa por catálogo local; perfil de laboratório |
| 3 Versão | Seleção de manifesto e imagem com conferência de tamanho e SHA-256 |
| 4 Preparar cartão | Inventário de discos físicos e volumes, com recusa automática do disco do Windows |
| 5 Gravar sistema | Escrita bruta real, releitura com hash, `p3` `OBSIDIAN` criada e formatada, relatório por tentativa |
| 6 Primeiro funcionamento | Roteiro de conferência manual no console |
| 7 Restaurar conteúdo | Cópia real de `roms`/`leitura`/`videos`/`saves` de pasta ou `.img`, com hashes e relatório |

A gravação exige confirmação por escrito (`GRAVAR`) e elevação do Windows. Salvar
o plano, reler o cartão e conferir o hash antes e depois da escrita são etapas
obrigatórias do executável, não apenas da interface.

## Organização

| Pasta | Uso |
| --- | --- |
| `src/` | Aplicativo WPF, núcleo testável (imagem, MBR, restauração, FAT32), camada Windows e self-test |
| `tools/` | Captura do console, staging do overlay, montagem da imagem, build, validação |
| `build/` | Captura verificada do console e árvore do overlay usado na imagem |
| `contracts/`, `schemas/` | Estados da instalação e formatos de perfis e pacotes |
| `profiles/` | Perfil de laboratório comprovado; continua bloqueado para distribuição pública |
| `catalog/` | Catálogos das imagens de prova e da imagem 1.0 |
| `artifacts/releases/` | Imagem 1.0, manifesto, somas e relatório de montagem; congelamento e imagem de conteúdo |
| `evidence/private/` | Relatórios completos do aparelho; excluídos do controle de versão |
| `third_party/` | Origem, versões, hashes e situação das licenças |

## Validar e montar

```powershell
python tools/validate_project.py
python -m unittest discover -s tests -v
powershell -File tools/build-os-image.ps1 -Force        # monta a imagem a partir de build/image-overlay.tar
powershell -File tools/build-app.ps1                    # compila a solução (SDK .NET 10)
dotnet src\ObsidianInstaller.SelfTest\bin\Release\net10.0\ObsidianInstaller.SelfTest.dll D:\dsh-temp\selftest
```

O self-test exercita a tabela de partições, a gravação e a releitura em cartão
simulado, a leitura da imagem de conteúdo FAT32 e a cópia com verificação de
hash — sem tocar em disco físico.

## Limite essencial

O identificador USB FEL e a resposta A33 não provam a revisão da placa nem o
painel. A prova física anterior usou um perfil específico e uma base cuja cadeia
de boot vem do backup do cartão original. A imagem 1.0 reutiliza essa base
comprovada; a partida da imagem nova em cartão separado ainda não foi registrada.
O layout de referência está em `../02-imagens-personalizadas/LEIA-ME-3-PARTICOES.md`.

Nos registros anteriores aparecem os nomes “Obsisidian” e “Obsidian”. Este projeto
adota **Obsidian Installer / Obsidian OS**; os nomes históricos foram preservados
nas evidências para manter rastreabilidade.
