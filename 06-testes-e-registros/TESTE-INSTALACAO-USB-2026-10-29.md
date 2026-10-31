# Prova de instalacao USB — Obsisidian OS

Data: 29/10/2026, America/Sao_Paulo.

## Objetivo e alcance

Testar uma instalacao pelo cabo no cartao preparado sem sistema, inserido no console. O teste usa a base goodluckOS de painel 1 que ja iniciou neste aparelho. A integracao completa do launcher final e a homologacao para outros aparelhos sao etapas separadas.

O usuario autorizou explicitamente o teste de instalacao. O destino e somente o cartao TESTE_FEL, de 15.728.640.000 bytes. Os discos internos e a imagem original em D:\R36S-backup-sistema-2026-10-28.img sao protegidos pelas verificacoes do gravador.

## Carregador temporario construido

- Fontes U-Boot: commit 5508406582f6f7120dce0c4b3059819a41788db2, versao 2026.10.
- Patch de inicializacao USB: Debianator, commit 8eadfd86e2c94bf812a6644bd51b6188b586e8d7, arquivo uboot/patches/0001-sunxi-probe-the-MUSB-gadget-in-board_usb_init.patch.
- Configuracao local: 04-ferramentas/FEL/build/obsisidian-ga36-fel_defconfig.
- DRAM 552 MHz, ZQ 15291, ODT habilitado; CPU 1008 MHz.
- DCDC1 3300 mV; DCDC2 1100 mV; DCDC3 1200 mV; DCDC4 desabilitado; DCDC5 1500 mV; ALDO2 2500 mV; ALDO3 e DLDO3 3000 mV. Valores comparados ao script.bin do backup.
- Ambiente U-Boot sem persistencia (ENV_IS_NOWHERE), autoboot desativado, video desativado.
- Quando iniciado por FEL, apresenta mmc 0 por USB mass storage. Nao grava uma imagem automaticamente.
- Arquivo: 04-ferramentas/FEL/build/out/obsisidian-ga36-fel-ums.bin, 528.912 bytes.
- SHA-256: 433cd84c1b0c92b49afc251b543b83a5b054de682d494dae26359562ba86ccdc.
- SPL: 24.576 bytes, assinatura eGON.BT0 e checksum verificados antes do envio.

Compilacao realizada em uma distribuicao WSL isolada chamada ObsisidianBuild-20261029, armazenada em 04-ferramentas/FEL/build/wsl-alpine. Imagem Alpine 3.22.7 obtida no servidor oficial, SHA-256 57e364a241bab0699cd8a760c0fd7b270e9cbcbd3cee822cb2b07ca6d925c16c. O Docker existente apresentou uma falha propria de inicializacao; suas configuracoes nao foram alteradas para este teste.

## Inicializacao RAM — comprovada

O sunxi-fel para Windows foi obtido no pacote de compilacao de eperie/build-scripts v1.3, revisao sunxi-tools 530adfa1420c1a1a73aa93df5b4d4c39f997f490, referenciado pela documentacao sunxi. SHA-256 do ZIP: 0fee8e293ac552f95e1a72585c31c59d89f77542294f89ef7de638de5b4a96c4.

A consulta inicial confirmou A33. O comando foi vinculado ao unico dispositivo FEL, bus 1, address 20. O envio registrou execucao do SPL, retorno ao FEL, transferencia de 496.080 bytes para DRAM em 0x4A000000 e inicio do U-Boot. Log completo: bootstrap-ums-2026-10-29.log.

Depois disso, o Windows enumerou:

- USB 1F3A:1010, classe armazenamento USB, status OK.
- Disco Linux UMS disk 0, USB, exatamente 15.728.640.000 bytes.
- Particao F:, FAT32, rotulo TESTE_FEL, com a geometria preparada anteriormente.

Isso demonstra execucao do carregador na memoria e acesso ao cartao dentro do console sem usar Linux instalado no SD.

## Escrita e leitura de arquivo — comprovadas

Foi criado no cartao um arquivo OBSISIDIAN_USB_PROBE.txt com identificador aleatorio da tentativa. A leitura retornou o mesmo conteudo. Os detalhes, identidade do disco e hash estao em prova-escrita-usb-2026-10-29.json.

## Imagem de instalacao preparada

- Origem: goodluckOS-GA36-A33-Rhododendron-painel1.img, SHA-256 4077a465e548971a83a58a965e2bfd19ef0e2ad29d3ad20c39c354ba3478557a.
- Copia de prova: 02-imagens-personalizadas/Obsisidian-prova-USB-base-goodluck-20261029.img.
- Tamanho: 1.073.741.824 bytes.
- SHA-256: a113b3c53a0d2c2c319f4ada65a694ccb6b16dbed053458a32669ba47b7ae7c5.
- Acrescentado um servico de primeiro boot S98obsisidian-usb-proof e o identificador OBSISIDIAN-USB-20261029-01.
- Verificacao e2fsck sem erros depois da alteracao da copia.

O servico cria as pastas roms/gba, nds, snes, psx, psp, cps2 e cps3 na HOME e grava obsisidian-usb-installacao.txt com o identificador de teste, kernel em execucao, raiz montada e dados dos cartoes. Esse arquivo devera ser consultado depois de iniciar a imagem gravada.

## Gravacao e boot — verificacao pendente

O gravador foi preparado para exigir identidade exata do disco USB, capacidade, rotulo TESTE_FEL, arquivo de prova, integridade da imagem e ausencia de particoes de sistema do PC. Ele bloqueia e desmonta o volume antes de gravar e compara o SHA-256 de uma releitura integral com a imagem original.

O inicio da gravacao depende da elevacao solicitada pelo Windows. O andamento e o resultado real sao registrados em instalacao-usb-gravacao-2026-10-29.json; somente phase=complete comprovara gravacao e releitura. Depois sera necessario reiniciar fisicamente o console e verificar o identificador gravado pelo servico de boot.

Esta prova nao demonstra compatibilidade universal nem uma imagem publica independente de todos os dados de fabrica: a base persistente usada aqui ainda contem a cadeia de partida que foi extraida do backup deste perfil. Demonstraremos separadamente acesso USB, gravacao integral e boot, sem confundir as etapas.

## Estado ao encerrar esta tentativa

O Windows informou que a solicitacao de elevacao foi cancelada pelo usuario. O processo elevado do gravador nao iniciou, portanto a imagem completa nao foi gravada. O cartao continua com TESTE_FEL e o arquivo da prova de escrita. A inicializacao em RAM e a leitura/escrita de arquivo pelo console passaram; a gravacao integral e o boot da imagem continuam pendentes. O carregador permanece apresentando o cartao por USB, permitindo retomar a partir da elevacao, sem repetir a compilacao.

## Retomada apos o usuario informar que a solicitacao nao apareceu

A identidade do disco, capacidade, rotulo e arquivo de prova foram novamente conferidos. A segunda tentativa de elevacao iniciou o gravador como processo 21472. O relatorio passou para phase=writing e registrou transferencia efetiva de dados pelo USB do console. A releitura integral e a verificacao de boot ainda devem ser concluidas; o estado atual esta em instalacao-usb-gravacao-2026-10-29.json.

## Gravacao integral — comprovada

As 21:18:43 -03:00, o gravador concluiu a escrita dos 1.073.741.824 bytes e a releitura integral pelo USB do console. O SHA-256 lido do cartao foi a113b3c53a0d2c2c319f4ada65a694ccb6b16dbed053458a32669ba47b7ae7c5, identico ao da imagem de prova. Relatorio: instalacao-usb-gravacao-2026-10-29.json, phase=complete.

As 21:20:00 -03:00, a remocao segura do dispositivo USB 1F3A:1010 foi aceita pelo Windows (CM_Request_Device_EjectW retornou CR_SUCCESS, sem veto). Evidencia: ejetar-console-usb-2026-10-29.json. O usuario foi orientado a desconectar o cabo, manter o cartao no TF1, desligar completamente, ligar e reconectar o USB. A verificacao do boot permanece pendente da reconexao.

## Boot pelo cartao instalado — comprovado

O usuario confirmou que reiniciou e reconectou. As 21:28:46 -03:00, o Windows voltou a enumerar GA36MB/MTP e USB serial COM3, identificador 1D6B:0104, todos com status OK. A consulta pela serial confirmou o identificador OBSISIDIAN-USB-20261029-01 em /etc/obsisidian-usb-test-id e no relatorio produzido pelo servico S98 durante o boot. Evidencia integral: instalacao-usb-boot-2026-10-29.json, boot_verified=true.

O kernel em execucao e goodluckOS 7.2.9-dirty armv7l. A raiz esta em mmcblk0p1, ext4; HOME esta em mmcblk0p2, vfat. O cartao foi identificado como SD com 15.360.000 blocos de 1 KiB, coerente com os 15.728.640.000 bytes do destino. As pastas gba, nds, snes, psx, psp, cps2 e cps3 foram verificadas em /home/player/roms, alem das pastas ja existentes na base.

Resultado desta prova: **APROVADA neste aparelho**. Foram demonstrados identificacao FEL do processador, inicializacao independente em RAM, acesso USB ao SD dentro do console, escrita e releitura integral de uma imagem e inicializacao dessa imagem pelo cartao.

Limites do resultado: esta imagem usa a base goodluckOS testada e seus dados de partida personalizados. A interface completa do Obsisidian OS nao foi integrada nesta prova. HOME permanece em 512 MiB e o restante do cartao nao foi expandido para jogos. Instalacao com dois cartoes, catalogo para outras variantes, partida persistente sem os dados de fabrica e interface publica do instalador continuam como trabalho de produto, nao como resultados deste teste.
