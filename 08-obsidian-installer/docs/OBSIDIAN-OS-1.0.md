# Obsidian OS 1.0 — referência congelada e próxima imagem

O usuário escolheu a atualização instalada em 30/10/2026 como referência **Obsidian OS 1.0**. O launcher dessa referência tem SHA-256 `de6fa0a3ac98c529c429554755cab021a5a7c85197acf8b12d4bef420165c018`, o mesmo hash da versão conferida e instalada no console ao organizar a estante. A biblioteca, vídeos, splash e interface integram o escopo funcional dessa referência.

O número 1.0 identifica a versão escolhida. Não significa que uma imagem de instalação desse conjunto já exista ou tenha sido homologada. A entrega em `artifacts/releases/obsidian-os-1.0-lab-r2/` é um **congelamento de desenvolvimento**, não um arquivo gravável nem um pacote para clientes. Contém o executável identificado, cópia dos fontes e uma seleção dos assets de leitura e vídeo. O manifesto registra cada caminho, origem, tamanho e SHA-256. Não contém livros, mangás, ROMs, saves ou configurações pessoais. A cópia anterior `obsidian-os-1.0-lab` foi preservada, mas usa uma abertura antiga e foi substituída como referência pela revisão r2.

## Imagem 1.0 produzida (30/10/2026)

A candidata de laboratório existe e está verificada em `artifacts/releases/obsidian-os-1.0-r1/`:

- **Imagem:** `obsidian-os-1.0-lab-r1.img`, **1.073.741.824 bytes**, SHA-256 `094f8e698a236d3e1db64ad4f69a2e3e2bebbb3846728595972167221c8b3fa8`.
- **Base:** `Obsisidian-prova-USB-base-goodluck-20261029.img`, SHA-256 `a113b3c…7ae7c5` (recusada qualquer base com hash diferente).
- **Overlay:** 533 componentes vindos do próprio console (captura verificada por SHA-256, `971dbe8c…`) mais `fbneo_libretro.so` do projeto. Launcher `de6fa0a3…` idêntico ao do aparelho; `appctl` `e4f612e9…`.
- **Conteúdo:** sistema da base + supervisão/splash Obsidian + home limpa com launcher, assets, temas, player, DraStic e FBNeo. Sem livros, ROMs, saves, backups ou dados pessoais.
- **Layout:** `p1` ext4 262.144+786.432, `p2` FAT32 1.048.576+1.048.576, e `p3` FAT32 `OBSIDIAN` criada pelo instalador a partir do setor 2.097.152 até o fim do cartão.
- **Espaço livre na imagem:** 113.152 KiB na raiz e 329.428 KiB na home.
- **Verificações:** `e2fsck`/`fsck.vfat` limpos; 533/533 hashes conferidos *dentro* da imagem; marcador `/etc/obsidian-release` presente; disco de prova script `S98obsisidian-usb-proof` removido; nenhum arquivo de backup/laboratório na home.
- **Ensaio sem mídia:** gravação, releitura e declaração de `p3` executadas em cartão simulado de 14,42 GiB; a `p3` foi formatada (`mkfs.vfat`) e montada com 13,4 GiB livres.
- **Restauração:** 86 livros copiados da imagem de conteúdo e de pasta, com hash por arquivo, política de conflito e relatório.

O passo a passo está em [Guia de instalação 1.0](GUIA-INSTALACAO-1.0.md). O que resta é
físico: gravar um cartão real no aplicativo, iniciar a imagem no console e
exercitar launcher, jogos, leitura, vídeo, MTP, TF2 e energia. Até isso ser
registrado, a imagem continua `channel: lab`, `customer_installation_allowed:
false` e `distribution_review: pending`.

## Formato necessário

Este aparelho inicializa a partir de um cartão com tabela de partições e dados específicos de inicialização. O instalador precisa de uma **imagem bruta de cartão `.img`**, acompanhada de manifesto, identificação do perfil, tamanho, hash e política de dados. ISO é normalmente uma imagem de disco óptico e não satisfaz, por si, o layout deste aparelho. O mecanismo de bootstrap para instalação por USB é um componente separado da imagem persistente.

Base de partida conhecida: `../02-imagens-personalizadas/Obsisidian-prova-USB-base-goodluck-20261029.img`, 1 GiB, SHA-256 registrado `a113b3c53a0d2c2c319f4ada65a694ccb6b16dbed053458a32669ba47b7ae7c5`. Ela iniciou na prova anterior **como goodluckOS**, mas não contém o launcher final e não é a imagem Obsidian 1.0. A cadeia persistente de boot dessa prova provém de backup do perfil físico: manter restrita a laboratório até concluir a revisão da origem e da distribuição. A imagem original experimental de 16 GB pertence a outro sistema e não é a base Obsidian 1.0.

## Trabalho necessário para produzir a candidata

1. Reconectar o console, identificar o dispositivo e confirmar em leitura o hash do launcher, montagem das partições, kernel, arquivos de inicialização, scripts e dependências em execução. Comparar com os arquivos locais sem sobrescrever nada.
2. Construir inventário de distribuição com fontes, licenças e hashes. Reconciliar launcher, fontes Sora/Inter, imagens do sistema, temas, player, bibliotecas SDL/FFmpeg, serviços de controle/energia, configurações de emuladores, scripts de boot, montagem p3 e MTP. O diretório congelado é parcial e não presume que os assets selecionados sejam toda a instalação do aparelho.
3. Criar cópia isolada da base conhecida, nunca alterar imagens de origem nem o cartão diário. Integrar overlay aprovado, identificação de versão e biblioteca vazia. Excluir conteúdo pessoal e remover dados de prova que não pertencem ao produto.
4. Definir e testar layout real e criação segura da partição de dados. A prova de 1 GiB ocupa todo o espaço disponível nas duas primeiras partições; não anunciar p3 funcional ou expansão antes de testar a imagem em mídia maior. Não reutilizar automaticamente um script de particionamento de um aparelho sem verificar precondições e risco de apagar dados.
5. Criar manifesto no esquema `schemas/image-package.schema.json`, com `channel: lab`, `customer_installation_allowed: false`, tamanho e SHA-256 reais da imagem final, perfil de laboratório e método de verificação de boot próprios desta versão. Não reutilizar a identificação de boot da prova antiga como se validasse a nova imagem.
6. Validar a imagem em um cartão separado, identificando o destino e preservando o cartão diário. Exercitar partida fria, bibliotecas vazias, jogos e retorno, tela, áudio, controles, energia, USB/MTP, TF2, leitura e vídeo. Registrar as evidências e testar recuperação/rollback.
7. Depois, resolver assinatura, guarda da chave, licenças e direitos, demais variantes e o motor real de escrita do aplicativo. Só liberar uma distribuição de instalação após os critérios de `HOMOLOGACAO.md` estarem comprovados.

## Aplicativo instalador

O aplicativo Windows atual é uma prévia que compila, enumera USB sob ação do usuário e verifica metadados/hash/assinatura, mas não identifica de forma suficiente o painel, não seleciona cartões, não escreve a imagem e não confirma o boot. A lista de chaves de confiança está vazia. Manter a operação de instalação desabilitada até implementar e testar essas etapas; não incorporar o congelamento parcial como imagem.

## Verificações desta preparação

- Após reconexão do console, o hash do executável instalado foi confirmado. A abertura antiga foi substituída no congelamento r2 pela versão cujo hash confere com o aparelho; demais assets de biblioteca e vídeo selecionados também têm hashes iguais aos arquivos observados.
- Solução Windows compilada em Release com .NET SDK 10.0.403, sem erros ou avisos.
- Testes do contrato do instalador e validador estrutural locais aprovados. Nenhuma escrita em disco ou teste de boot de imagem 1.0 foi realizado nesta etapa.
- Para repetir o congelamento em um ambiente novo, usar `python tools/freeze_os_1_0.py`: ele confere o hash do launcher e recusa sobrescrever uma referência existente. O script não gera imagem nem consulta hardware.
- Os scripts e recursos selecionados do console foram copiados, sem conteúdo pessoal, para `artifacts/releases/obsidian-1.0-componentes-lab.tar`. O SHA-256 `d48c1266a3833462e03c64d92e3d4868b18dffb071663304d584f3b2ffba56bd` foi conferido no aparelho e no computador. É uma captura parcial, não uma imagem gravável ou prova de direitos de distribuição. Inclui arquivos de boot, configuração MTP, supervisor, launcher.sh, fontes, temas, telas de biblioteca e player. A configuração MTP e quaisquer arquivos capturados devem ser revisados antes de inclusão no produto.

## Imagem separada de conteúdo e restauração

Foi criada `artifacts/releases/obsidian-conteudo-livros-1.0-lab.img`: imagem de cartão FAT32 separada, não inicializável, contendo 86 EPUBs e pastas vazias `roms`, `videos` e `saves`. O manifesto ao lado registra hash, layout e verificação dos arquivos. É conteúdo pessoal; não incluí-lo na imagem do sistema nem distribuí-lo. Ainda não foi gravado em cartão. A imagem pode servir como mídia de conteúdo em laboratório.

A sétima página do instalador, Restaurar conteúdo, agora executa a cópia: aceita uma pasta de backup **ou** uma imagem `.img` FAT32, reconhece `roms`, `leitura`, `videos` e `saves`, calcula o espaço necessário, respeita política de conflito (pular, substituir, manter as duas cópias), confere o hash de cada arquivo copiado e grava um relatório JSON. Saves restaurados para a home do sistema vão para `.config/retroarch/saves`. Os jogos, livros, séries e saves continuam fora da imagem do sistema.
