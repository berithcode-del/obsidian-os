# Guia de instalação — Obsidian OS 1.0 (laboratório)

Este guia descreve a instalação completa de um cartão limpo: gravar o sistema,
preparar a área de dados e restaurar o conteúdo. Ele reflete o que foi realmente
verificado e aponta, sem ambiguidade, o que ainda depende de mídia física.

## 1. Arquivos da entrega

Pasta `artifacts/releases/obsidian-os-1.0-r3/`:

| Arquivo | Conteúdo |
| --- | --- |
| `obsidian-os-1.0-lab-r3.img` | Imagem bruta de cartão (1.073.741.824 bytes) — **revisão que inicia no console** |
| `obsidian-os-1.0-lab-r3.manifest.json` | Manifesto no esquema `schemas/image-package.schema.json` |
| `SHA256SUMS` | SHA-256 da imagem |
| `build-report.txt` | Saída da montagem, com as verificações executadas |

O catálogo local `catalog/obsidian-os-1-0-lab-r1.json` descreve a mesma imagem como
referência externa (`artifacts/external-assets.json`), para que o validador
estrutural confira hash e tamanho sem duplicar o arquivo.

Valores de referência:

- Imagem r3: **1.073.741.824 bytes** · SHA-256 `c8543e46c7075ec82db3cc8f4335e3d914e8dd6d3cdc2d154f854aa132211b8a`
- Base: `Obsisidian-prova-USB-base-goodluck-20261029.img` · SHA-256 `a113b3c53a0d2c2c319f4ada65a694ccb6b16dbed053458a32669ba47b7ae7c5`
- Launcher instalado (Obsidian OS 1.0): `de6fa0a3ac98c529c429554755cab021a5a7c85197acf8b12d4bef420165c018`
- Supervisor `appctl`: `e4f612e9b587c778fd8aae4c90c1c7b0943f3fff6b61e0b889ebd2c67c4cd4cc`
- Captura do console usada no overlay: `971dbe8c10ec0d102a9c1d2f84b8563cee2e93132af96653aa95b5df9d9cea06` (155.381.760 bytes)

## 1.1 Fluxo pelo aplicativo (verificado em 31/10/2026)

O instalador (`packaging/out/instalador-win-x64/ObsidianInstaller.exe`) faz o
processo inteiro sozinho, em três telas: **Conectar → Cartão → Gravar**. Ele traz a
imagem e o manifesto na pasta `image\`, confere o SHA-256, detecta o console em FEL,
envia o carregador, encontra o cartão exposto, limpa a tabela de partições, testa o
enlace com 16 MiB, grava, relê, escreve a tabela por último e prepara a `OBSIDIAN`.

Duas execuções completas aprovadas:

| Alvo | Duração | Observação |
| --- | ---: | --- |
| Cartão no leitor USB | 329 s | releitura integral do cartão |
| **Cartão no console em FEL** | **557 s** | gravação a 3,6 MiB/s; releitura de início e fim |

Ordem usada na gravação: **corpo da imagem primeiro (setor 1 ao fim) e setor 0 por
último**. Enquanto não há tabela de partições o Windows não monta volume e não nega
a escrita bruta; a tabela final é gravada e relida ao término.



### Detalhe operacional (importante)

O console **só liga quando não está conectado ao computador**. Por isso o fim do
processo é sempre: **desconectar o cabo USB e ligar o console**. Enquanto o cabo
estiver conectado o aparelho permanece desligado — e é assim que ele precisa estar
para a gravação (desligado, cabo conectado, sem o cartão, para entrar em modo FEL).

Sequência completa, como funciona na prática:

1. Console **desligado**, cabo USB conectado, **sem o cartão** → a ROM entra em FEL (`1F3A:EFE8`).
2. Instalador envia o carregador (`bootstrap.bin`) → o cartão aparece como `Linux UMS disk 0`.
3. Instalador grava, confere e prepara a `OBSIDIAN`.
4. **Desconectar o cabo** e ligar o console → primeira partida com o Obsidian OS.

## 2. Layout de armazenamento

| Partição | Papel | Início (setor) | Setores | Formato | Montagem |
| --- | --- | ---: | ---: | --- | --- |
| área reservada | bootloader/kernel | 0 | 262.144 | — | — |
| `p1` | sistema | 262.144 | 786.432 (384 MiB) | ext4 | `/` |
| `p2` | home | 1.048.576 | 1.048.576 (512 MiB) | FAT32 | `/home/player` |
| `p3` | dados Obsidian | 2.097.152 | até o fim do cartão | FAT32, rótulo `OBSIDIAN` | `/media/obsidian` |

A imagem contém apenas `p1` e `p2`. A `p3` é criada pelo instalador depois da
gravação, com o tamanho exato do cartão. O script `/etc/init.d/S29obsidian-data`
apenas monta uma `p3` existente — ele nunca particiona nem formata.

## 3. O que existe dentro da imagem

- Sistema goodluckOS da base comprovada (kernel 7.2.9, RetroArch com 8 cores,
  `puppy` como recuperação, drivers de painel/áudio/USB do perfil painel 1).
- Supervisão Obsidian: `appctl` com handoff de splash, `S00gameman-quiet`,
  `S12gameman-splash`, `S29obsidian-data`, `rcS` silencioso.
- Identidade: `/etc/os-release` e `/etc/obsidian-release` com produto, versão,
  hashes e layout; `/etc/issue` e hostname `obsidian`.
- Home limpa (193.820 KiB usados, 329.428 KiB livres): launcher, `gameman-assets`
  (fontes, telas da biblioteca, temas, player, capas), DraStic com BIOS,
  `fbneo_libretro.so`, regiões vazias `roms/`, `leitura/`, `videos/`.
- **Não** contém: livros, ROMs, saves, backups de launcher, índices de leitura,
  favoritos, diagnóstico de laboratório ou qualquer dado pessoal.

## 4. Antes de começar

1. Um cartão microSD de **16 GB ou mais** (a imagem precisa de 1 GiB; o restante
   vira `p3`). Anote a capacidade e a série que o instalador mostrar.
2. Um leitor de cartões USB no computador. O cartão diário do console **não**
   deve ser usado nesta etapa.
3. Windows 10/11. O aplicativo portátil está em
   `packaging/out/instalador-win-x64/ObsidianInstaller.exe` (autossuficiente);
   para recompilar: `powershell -File tools/build-app.ps1 -PublishPortable`.
4. Confira o hash da imagem se quiser independência do aplicativo:
   `Get-FileHash artifacts/releases/obsidian-os-1.0-r1/obsidian-os-1.0-lab-r1.img -Algorithm SHA256`

## 5. Instalação com o aplicativo

1. **Conectar console** (opcional): a etapa 1 apenas identifica dispositivos USB.
2. **Versão do Obsidian** (etapa 3): selecione
   `obsidian-os-1.0-lab-r1.manifest.json` e, em seguida, a imagem `.img`.
   Clique em **Conferir integridade**. O aplicativo lê o arquivo e compara
   tamanho e SHA-256 com o manifesto.
3. **Preparar cartão** (etapa 4): clique em **Procurar cartões**. Confira
   capacidade, modelo e série do cartão. O disco do Windows é recusado
   automaticamente.
4. **Gravar sistema** (etapa 5): revise o plano, marque a caixa de ciência e
   digite `GRAVAR`. O aplicativo:
   - recalcula o hash da imagem;
   - revalida a identidade do cartão (modelo, série e tamanho);
   - bloqueia/desmonta os volumes;
   - grava 1 GiB em fluxo com hash incremental;
   - **relê** o cartão e compara o SHA-256;
   - declara a `p3` com o restante do cartão;
   - formata a `p3` como FAT32 `OBSIDIAN` pelo `diskpart` e cria
     `leitura/`, `videos/` e `saves/`;
   - grava um relatório em `work/` ao lado do executável.
   Se o aplicativo não estiver elevado, ele se relança com a confirmação de
   administrador do Windows. Cancelar a elevação não grava nada.
5. **Primeiro funcionamento** (etapa 6): ejetar o cartão, inserir no console
   desligado (slot TF1), ligar e observar a splash do Obsidian OS.

### Preparar um cartão usado como se fosse novo

`tools/format-card.ps1` apaga um cartão de laboratório e o devolve ao estado de
"recém-comprado": `diskpart clean`, gravação de zeros no dispositivo inteiro e
conferência por amostras na releitura. Exige administrador e recusa disco de
sistema, disco não-USB, série ou tamanho diferentes do esperado.

```powershell
powershell -File tools/format-card.ps1 -DiskNumber 2 `
    -ExpectedSerial 150101v01 -ExpectedBytes 15728640000
```

Resultado esperado: `estilo=RAW`, **zero partições**, nenhum volume, e o Windows
passando a pedir formatação ao abrir o cartão. Em um leitor USB 2.0, 15,7 GB
levaram cerca de meia hora (~7,7 MiB/s). O log e o JSON do procedimento são
gravados em `D:\dsh-temp\format-card.log` e `format-card.json`.

### Alternativa sem o aplicativo (Rufus)

`04-ferramentas/Rufus/rufus-4.15p.exe` grava a imagem em modo "DD". Nesse
caminho a `p3` **não** é criada: o sistema inicia, mas a área de dados só existirá
depois de rodar o aplicativo (ou de criar a partição manualmente com
`diskpart`: `create partition primary offset=2097152` + `format fs=fat32 quick label=OBSIDIAN`).

## 6. Restaurar o conteúdo

A restauração é independente da gravação e pode ser repetida.

1. Etapa 7 → **Escolher pasta** (backup em pastas) ou **Escolher imagem .img**
   (a imagem de conteúdo de 257 MB, `obsidian-conteudo-livros-1.0-lab.img`).
2. Marque as pastas reconhecidas (`roms`, `leitura`, `videos`, `saves`).
3. Escolha o destino:
   - **cartão de conteúdo (TF2)**: é onde o launcher procura `roms/` e `videos/`;
   - **área OBSIDIAN do cartão de sistema** (aparece como volume `OBSIDIAN`):
     o launcher lê livros em `/media/obsidian/leitura`;
   - **home do cartão de sistema** (volume `FAT32` de 512 MiB): aceita livros em
     `/home/player/leitura`; saves vão automaticamente para
     `.config/retroarch/saves`.
4. Escolha a política de conflito: pular, substituir ou manter as duas cópias.
5. Clique em **Restaurar conteúdo**. Cada arquivo é copiado, tem o hash
   conferido e o conjunto vira um relatório em `work/restauracao-*.json`.

Com o cartão de conteúdo montado no console, a estrutura esperada é:

```text
/media/external/roms/{nds,gba,snes,psp,cps2,cps3}   jogos
/media/external/videos                              séries e episódios
/media/external/leitura                             livros (opcional)
/media/obsidian/leitura                             livros na área de dados
/home/player/.config/retroarch/saves                saves do RetroArch
```

## 7. Verificação no console

Depois do primeiro boot, confira:

1. splash e entrada no launcher (sem texto de console);
2. Ajustes → versão exibida `Obsidian OS 1.0`;
3. área `OBSIDIAN` montada (`/media/obsidian`) — o volume aparece no MTP como
   "Obsidian";
4. um jogo GBA/SNES, um NDS (DraStic), um livro restaurado e um vídeo;
5. retorno ao launcher, áudio, brilho, botão de energia e TF2.

O console expõe um shell serial em `COM3` (115200) e MTP com três áreas
(`HOME`, `External card`, `Obsidian`) — útil para conferir sem desmontar nada.

## 8. O que já foi provado e o que falta

Provado nesta rodada (sem cartão físico):

- imagem montada e desmontada com `e2fsck`/`fsck.vfat` sem erros;
- 533/533 componentes do overlay com hash conferido dentro da imagem;
- launcher na imagem idêntico ao do console;
- gravação, releitura e declaração de `p3` exercitadas no cartão simulado
  (14,42 GiB), com `mkfs.vfat` e montagem da `p3` bem-sucedidas;
- restauração real de 86 livros a partir da imagem de conteúdo e de uma pasta,
  com hash por arquivo e relatório;
- 14 testes de contrato Python e 27 verificações do self-test .NET aprovados;
- compilação Release sem avisos nem erros.

Falta (exige cartão/leitor físico):

- gravar o cartão no aplicativo (caminho `\\.\PhysicalDriveN` + `diskpart`);
- primeiro boot da imagem idêntica no console, com splash, launcher e `p3`;
- MTP, TF2, áudio, controles e energia na imagem nova;
- restauração do backup de conteúdo direto para o cartão TF2 real;
- assinatura/comercial: o manifesto é `channel: lab`,
  `customer_installation_allowed: false` e `distribution_review: pending`.

## 9. Reversão

- **Cartão de teste**: basta regravar a imagem da prova
  (`Obsisidian-prova-USB-base-goodluck-20261029.img`) ou qualquer imagem anterior.
- **Console**: a imagem não altera o cartão diário. Para desfazer, regrave o
  backup do cartão original (ou o cartão que estava em uso) no mesmo cartão.
- **Restauração de conteúdo**: nada no sistema é apagado; arquivos copiados
  podem ser removidos do cartão de destino. A política "manter as duas cópias"
  evita sobrescrever saves por engano.

## 10. Como reproduzir a imagem

```powershell
cd 08-obsidian-installer
python tools/console-pull.py --out D:\dsh-temp\pull\obsidian-1.0-overlay.tar   # opcional: nova captura
python tools/stage-overlay.py
powershell -File tools/build-os-image.ps1 -Force
```

`tools/apply-image.sh` recusa qualquer base com hash diferente do comprovado e
falha se a verificação estrutural interna não passar.
