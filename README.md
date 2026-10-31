# Obsidian OS

Sistema para o console portátil GA36/R36S (Allwinner A33): jogos, leitura, vídeos e
personalização em um só lugar, com um instalador que grava o cartão sozinho.

![Liberte seu console](site-obsidian-os/assets/hero-liberte-console.png)

## Baixar

**[Baixar o Obsidian OS 1.0 (v1.0-r3)](https://github.com/berithcode-del/obsidian-os/releases/latest)** — instalador com a imagem embutida.

O instalador e a imagem são publicados em **Releases** (arquivos grandes não entram
no histórico do Git):

- `ObsidianOS-1.0-Instalador-win64.zip` — instalador Windows com a imagem embutida
- `obsidian-os-1.0-lab-r3.img` — imagem crua, para gravar com outra ferramenta
- `SHA256SUMS.txt` — hashes de todos os arquivos

## Como instalar

1. Console **desligado**, cabo USB no computador, **sem o cartão** → ele entra em modo FEL.
2. Abra o instalador → **Continuar** → insira o cartão → confirme → **Gravar sistema**.
3. Ao terminar, **desconecte o cabo USB e ligue o console** (ele só liga desconectado).

O instalador confere o SHA-256 da imagem, apaga a tabela de partições, testa o enlace,
grava, relê e cria a área `OBSIDIAN` (FAT32) com `leitura/`, `videos/` e `saves/`.

## O que tem aqui

| Pasta | Conteúdo |
| --- | --- |
| `08-obsidian-installer/` | Instalador WPF (.NET 10), biblioteca de imagem/restauração, ferramentas de build, documentação e testes |
| `site-obsidian-os/` | Site do projeto (HTML/CSS/JS estático) |
| `06-testes-e-registros/` | Registros e capturas de validação |

## Documentação

- `08-obsidian-installer/docs/GUIA-INSTALACAO-1.0.md` — instalação completa
- `08-obsidian-installer/docs/NOTAS-DA-VERSAO-1.0.md` — notas da versão
- `08-obsidian-installer/docs/PUBLICAR-NO-GITHUB.md` — como publicar a release
- `08-obsidian-installer/CHANGELOG.md` — histórico técnico

## Estado

Canal **lab**: distribuição para testes com o console GA36/R36S (painel 1). O
executável ainda **não é assinado** (SmartScreen pode pedir “Executar mesmo assim”).
