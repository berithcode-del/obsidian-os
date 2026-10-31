# Publicar o instalador no GitHub (download público)

Este documento cobre o caminho mais simples e gratuito para distribuir o
**Obsidian OS 1.0**: um repositório com o código/instalador e uma **Release** com os
arquivos grandes como anexos. O GitHub aceita **até 2 GB por arquivo** em Releases —
nossa imagem (1.024 MiB) e o pacote (~1,1 GB) cabem com folga.

> Importante: **não** coloque o `.img` no histórico do Git. Arquivos grandes em Git
> deixam o clone lento e estouram o limite de 100 MB por arquivo. Use sempre
> **Releases** (anexos), nunca `git add` da imagem.

## 1. Criar o repositório

No GitHub: **New repository** → nome sugerido `obsidian-os` → visibilidade
`Public` (se o download precisa ser aberto) → **não** marque "Add a README".

No computador, dentro da pasta do projeto:

```powershell
git init
git branch -M main
# .gitignore já deve excluir *.img, release/, packaging/out/ e work/
git add .
git commit -m "Obsidian OS 1.0: instalador, imagem e documentação"
git remote add origin https://github.com/<usuario>/obsidian-os.git
git push -u origin main
```

## 2. Instalar o GitHub CLI (para publicar a Release em um comando)

```powershell
winget install --id GitHub.cli -e
gh auth login          # escolha: GitHub.com -> HTTPS -> login pelo navegador
```

## 3. Publicar a Release com os arquivos

```powershell
$tag = 'v1.0-r3'
$dir = 'D:\R36S-sistema-novo\release\obsidian-os-v1.0-r3'

gh release create $tag `
  "$dir\ObsidianOS-1.0-Instalador-win64.zip" `
  "$dir\SHA256SUMS.txt" `
  "$dir\LEIA-ME.txt" `
  "$dir\imagem\obsidian-os-1.0-lab-r3.img" `
  "$dir\imagem\obsidian-os-1.0-lab-r3.manifest.json" `
  --title "Obsidian OS 1.0 (r3)" `
  --notes-file "D:\R36S-sistema-novo\08-obsidian-installer\docs\NOTAS-DA-VERSAO-1.0.md"
```

O `gh` mostra o envio do arquivo de 1 GB (leva alguns minutos, depende da sua
internet). Ao final ele imprime a URL da release, algo como
`https://github.com/<usuario>/obsidian-os/releases/tag/v1.0-r3`.

## 4. Ligar o botão do site ao download

No site, o botão de download deve apontar para a URL da release (ou direto para o
asset):

```html
<a class="button primary" href="https://github.com/<usuario>/obsidian-os/releases/download/v1.0-r3/ObsidianOS-1.0-Instalador-win64.zip">
  Baixar o instalador
</a>
```

Sugestão de texto junto ao botão: **Windows 10/11 · 64 bits · pacote de ~1,1 GB com a
imagem inclusa** e o link secundário **“só a imagem (1 GB)”** para quem já tem o
instalador.

## 5. Alternativas se não quiser usar o `gh`

- **Só pelo navegador**: no GitHub, aba **Releases** → *Draft a new release* → escolha
  a tag → anexe os arquivos arrastando para a área de upload → *Publish release*.
  O limite por arquivo continua 2 GB; o upload de 1 GB pelo navegador é mais frágil
  que o `gh` (se cair, recomece).
- **Arquivo em outro lugar**: se um dia o pacote passar de 2 GB, use um bucket
  (Cloudflare R2, Backblaze B2) e mantenha no GitHub apenas o link e o checksum.

## 6. Checklist antes de anunciar

- [ ] `SHA256SUMS.txt` da release confere com os arquivos enviados.
- [ ] O ZIP baixado abre e contém `ObsidianInstaller.exe` **e** a pasta `image\`.
- [ ] Teste em uma máquina limpa: o instalador abre sem pedir .NET (self-contained).
- [ ] A página de doações/venda aponta para a URL correta da release.
- [ ] Versão e data no `release.json` do pacote correspondem à tag publicada.
