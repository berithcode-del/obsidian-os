# Teste de deteccao USB com cartao sem sistema

Data: 29/10/2026, America/Sao_Paulo.

## Preparacao e contexto

O cartao USB de 15.728.640.000 bytes foi preparado como FAT32, rotulo TESTE_FEL, particao iniciando em 1 MiB. Os primeiros 128 MiB foram zerados e verificados antes da formatacao; depois foram conferidos os setores nas posicoes 8 KiB e 128 KiB. A imagem original em D:\R36S-backup-sistema-2026-10-28.img permaneceu com tamanho e data de modificacao anteriores.

Foi solicitado ao usuario inserir o cartao no TF1, deixar TF2 vazio e conectar o console pela porta OTG, acionando o botao de ligar. O usuario respondeu "conectei". A disposicao fisica dos cartoes nao foi inspecionada remotamente.

## Resultado observado

- Coleta anterior: nenhum dos IDs USB conhecidos presente.
- Coleta apos a conexao, as 20:13:55 -03:00: um candidato Allwinner FEL.
- Identificador: USB\VID_1F3A&PID_EFE8\5&16ACEB2E&0&1.
- Hardware ID adicional: USB\VID_1F3A&PID_EFE8&REV_02;3.
- Status Windows: Error.
- DEVPKEY_Device_ProblemCode / ConfigManagerErrorCode: 28.
- DEVPKEY_Device_ProblemStatus: 3221226640 (0xC0000490).
- Nenhum servico de driver associado foi informado.

Relatorios: diagnostico-usb-antes-teste-fel-2026-10-29.json e diagnostico-usb-cartao-vazio-2026-10-29.json, nesta pasta.

## Interpretacao

A enumeracao coincide com o VID/PID FEL definido no sunxi-tools e surgiu apos a conexao relatada pelo usuario. Isso e evidencia de acesso USB de recuperacao independente do Linux do cartao. O codigo 28 e o status 0xC0000490 indicam falta de driver compativel no Windows, nao falha de partida do goodluckOS.

Ainda nao houve troca de comandos FEL. Esta coleta nao identifica por protocolo o SoC, a revisao da placa ou o painel e nao demonstra inicializacao em RAM, leitura dos slots ou instalacao. Nenhum bootloader foi enviado e nenhuma gravacao no console foi feita durante esta deteccao.

## Proximo marco

Preparar um driver de acesso USB apropriado, restrito ao dispositivo FEL identificado, e uma ferramenta FEL de procedencia verificada. Comecar por consulta de versao/identificacao do SoC; manter o carregamento de SPL/kernel e a gravacao de cartoes dependentes da homologacao do perfil da placa. Evitar instalar filtros USB gerais ou substituir drivers de outros dispositivos.

Fontes: [IDs FEL no sunxi-tools](https://github.com/linux-sunxi/sunxi-tools/blob/master/fel_lib.h) e [Microsoft: codigo 28 e STATUS_PNP_NO_COMPAT_DRIVERS](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/cm-prob-failed-install).

## Continuacao — comunicacao comprovada

O usuario autorizou continuar e confirmou a instalacao do driver no Zadig. O executavel oficial Zadig 2.9 foi obtido na release v1.5.1 de pbatard/libwdi, com assinatura Authenticode valida de Akeo Consulting. SHA-256: 4ecaa95df3da3621486a043aef8b3050b8bafe7c901402871e816229ef82039b.

O Windows passou a informar servico WinUSB, codigo de problema zero, INF oem16.inf e provedor libwdi. Evidencia: fel-driver-instalado-2026-10-29.json.

A consulta AW_FEL_VERSION foi implementada em 01-projeto/instalador/fel_version.py, sem comandos de escrita de memoria, execucao ou acesso a cartoes. PyUSB 1.3.1, libusb-package 1.0.30.0 e importlib-resources 7.1.0 foram instalados somente na pasta de ferramentas do projeto; os hashes dos arquivos foram comparados aos metadados PyPI e registrados em 04-ferramentas/FEL/dependencias-verificadas.json.

As 20:22:34 -03:00, a ROM respondeu:

- Assinatura AWUSBFEX.
- SoC 0x1667: familia Allwinner A33 / R16.
- Protocolo FEL versao 1.
- Scratchpad 0x00007e00.
- Status FEL ffff000000000000: marcador 0xFFFF, tag zero, estado zero.
- Endpoint IN 0x82; OUT 0x01.

Resposta integral: fel-versao-processador-2026-10-29.json. Os seis testes de protocolo passaram, incluindo rejeicao de estado de erro USB/FEL, assinatura invalida e verificacao de que somente o comando de versao e transmitido. A primeira versao do cliente interpretava erroneamente o marcador 0xFFFF como erro; a interpretacao foi corrigida conforme [FEL/Protocol](https://linux-sunxi.org/FEL/Protocol) antes de aceitar o resultado.

Portanto, a comunicacao com a ROM pelo USB esta comprovada no teste com cartao preparado sem sistema. Isso ainda nao comprova a partida em RAM ou a instalacao nos cartoes.

## Comparacao preliminar para partida independente

O script.bin foi novamente lido do backup, sem modifica-lo: 39.048 bytes, SHA-256 4bdfa6b7deacf59d821063accdd5bf7789c70d240dc695fffa3e16f2e83dba6c. A extracao esta em perfil-original-para-boot-fel-2026-10-29.json.

DRAM: clock 552 MHz, ZQ 15291 e ODT habilitado coincidem com a configuracao U-Boot do Debianator. Ha diferenca em DCDC1: o original registra 3300 mV; a referencia configura 3000 mV. DLDO3 e 3000 mV nos dois. Coincidencia parcial de parametros nao homologa outra revisao da placa; a referencia continua restrita pelo autor a V1.0. Fontes e commits consultados foram registrados em 04-ferramentas/FEL/fontes-referencia/proveniencia-fontes.json.

Nenhum SPL, U-Boot ou kernel foi carregado no console nesta etapa. Para o proximo teste, preparar e revisar um perfil proprio antes de qualquer execucao em RAM. O Docker Desktop instalado foi solicitado para iniciar como possivel ambiente de compilacao; nao houve confirmacao de que o motor Linux ficou disponivel nem compilacao concluida.

O usuario interrompeu o controle da tela com Escape durante a verificacao do ambiente de compilacao. Nenhuma outra acao de interface foi feita depois dessa interrupcao.
