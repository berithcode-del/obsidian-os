# Conhecimento validado e transição do projeto

Atualizado em 29/10/2026. Este documento é o ponto de retomada técnica do instalador.

## 1. Resultado e força da evidência

**Foi possível instalar uma imagem de sistema no SD dentro do console usando o cabo USB, começando com o cartão sem sistema inicializável.** O processo foi concluído em um aparelho GA36 A33, perfil Rhododendron, painel 1.

| Etapa | Evidência | Situação |
| --- | --- | --- |
| Detectar recuperação sem Linux no SD | USB `1F3A:EFE8` apareceu após conectar o aparelho | Comprovada |
| Consultar o processador | ROM respondeu `AWUSBFEX`, SoC `0x1667`, protocolo 1 | Comprovada; família A33/R16 |
| Iniciar código temporário | SPL executou, retornou ao FEL e U-Boot iniciou em DRAM | Comprovada |
| Acessar o cartão por USB | Windows enumerou `Linux UMS disk 0`, com o tamanho e rótulo preparados | Comprovada |
| Escrever no SD pelo console | Arquivo com nonce foi escrito e relido | Comprovada |
| Instalar uma imagem inteira | 1.073.741.824 bytes gravados; SHA-256 da releitura idêntico | Comprovada |
| Iniciar o sistema instalado | Após reinício, serial/MTP retornaram e o identificador da prova apareceu no arquivo gerado no boot | Comprovada |
| Criar a estrutura de jogos | Pastas verificadas em `/home/player/roms` | Comprovada |

Os registros brutos estão em `../evidence/private/2026-10-29/`. O resumo final é `resultado-final-instalacao-usb-2026-10-29.json`. Cada cópia está relacionada à origem em `../artifacts/migration-manifest.json`.

## 2. O que o teste não comprova

- Não foi validado um aplicativo gráfico portátil para clientes; a prova usou ferramentas e scripts de laboratório.
- A imagem instalada é uma cópia da base goodluckOS compatível com um serviço adicional de verificação. O launcher completo do Obsidian OS ainda precisa ser empacotado.
- A inicialização temporária usa U-Boot construído de fontes; a cadeia persistente da imagem ainda aproveita os dados de partida personalizados a partir do backup do perfil.
- O suporte a outras placas, painéis, cartões e Windows ainda precisa ser homologado.
- HOME continua em 512 MiB. A instalação não aproveitou automaticamente todo o cartão de 16 GB.
- A instalação automática com dois cartões, a preservação de saves e a recuperação de transferências interrompidas não foram testadas.
- A restauração integral da imagem de fábrica não foi realizada. O boot da imagem de prova não equivale a validar esse backup de fábrica.

## 3. Hardware e identidade

O diagnóstico do sistema funcional registra ARMv7, quatro núcleos Cortex-A7 e aproximadamente 512 MiB físicos de RAM, com kernel `7.2.9-dirty`, controles GA36-MB e painel suportado pela variante 1. A resposta de ROM `0x1667` confirma a família Allwinner A33/R16; não identifica uma placa GA36 por si só.

O `script.bin` do backup foi reconhecido como **GA36-MB A33 Rhododendron**, SHA-256 `4bdfa6b7deacf59d821063accdd5bf7789c70d240dc695fffa3e16f2e83dba6c`. A extração teve 39.048 bytes a partir do offset 20.340.736. Esse fingerprint é útil para reconhecer uma configuração conhecida; não foi demonstrado que ele seja exclusivo de uma revisão física.

Um handoff anterior menciona V1.1. O registro de estado ressalva que o fingerprint não comprova a revisão física. Portanto, o cadastro atual mantém **revisão física não confirmada** e evita promover essa inferência a um fato de compatibilidade.

## 4. Três modos USB distintos

| Modo | VID/PID observado | Capacidades observadas | Limites |
| --- | --- | --- | --- |
| FEL da ROM | `1F3A:EFE8` | Consulta do SoC e envio do bootstrap | Não é um disco SD nem uma identificação completa da placa |
| U-Boot temporário / UMS | `1F3A:1010` | Cartão exposto como armazenamento para o Windows | A enumeração precisa ser correlacionada à sessão; o número do disco muda |
| goodluckOS iniciado | `1D6B:0104` | Serial CDC e MTP | Depende do kernel e dos serviços já iniciados; o ID é genérico Linux |

O nome `GA36MB`, o PID de gadget Linux e a serial `0123456789` apresentada pelo sistema não são credenciais de identidade. Não usar “apareceu uma COM” como autorização para comandos de root em qualquer dispositivo.

O sistema de fábrica analisado continha configurações USB herdadas, mas não houve prova de serial/MTP nele. O caminho de recuperação FEL resolveu o acesso anterior ao Linux. A presença de eMMC ou outra origem de boot em outra placa pode impedir o mesmo fallback; esse caso continua pendente.

## 5. Driver Windows

Na primeira enumeração FEL, o Windows informou problema 28 e `0xC0000490`: faltava driver compatível. Foi usado Zadig 2.9 oficial, com assinatura Authenticode válida de Akeo Consulting, para associar WinUSB somente a `1F3A:EFE8`.

Depois da ação confirmada pelo usuário: serviço WinUSB, problema zero, provedor libwdi. O nome `oem16.inf` observado é específico deste PC e não deve ser fixado no aplicativo. O mesmo vale para COM3, F:, bus 1/address 20 e Disco 2.

Zadig foi uma ferramenta de laboratório. O aplicativo comercial precisa de preparação de driver restrita e suportável. Os descritores da ROM não são controlados pelo nosso sistema; não presumir que um driver será automaticamente instalado porque o kernel novo tem USB funcionando.

## 6. Consulta FEL e detalhes do protocolo

A versão foi lida primeiro por um cliente Python limitado ao comando `AW_FEL_VERSION` (`0x001`), sem APIs de escrita de memória ou execução. A estrutura USB de 32 bytes usa `AWUC`; respostas de transporte têm 13 bytes e assinatura `AWUS`. A versão tem 32 bytes com assinatura `AWUSBFEX`.

Resposta validada: SoC `0x1667`, protocolo 1, scratchpad `0x00007e00`, endpoints IN `0x82` e OUT `0x01`. O status FEL de 8 bytes observado foi `ffff000000000000`: marcador `0xFFFF`, tag zero, estado zero. Uma versão inicial do cliente confundiu esse marcador com erro; a interpretação foi corrigida e coberta por teste. O status de transporte USB e o estado FEL são campos diferentes.

Seis testes do cliente passaram: comando de versão exclusivo, erro USB, erro FEL, assinatura/estrutura inválida e SoC desconhecido sem autorização de instalação. Sete cenários do classificador passivo passaram, incluindo ausência de dispositivo, nome enganoso, driver ausente e múltiplos candidatos.

As dependências Python usadas foram PyUSB 1.3.1, libusb-package 1.0.30.0 e importlib-resources 7.1.0. Os wheels foram comparados aos hashes publicados no PyPI e instalados apenas na pasta de ferramentas. Isso descreve o laboratório; não é a arquitetura exigida no PC do cliente.

## 7. Bootstrap específico

U-Boot foi compilado do commit `5508406582f6f7120dce0c4b3059819a41788db2`, versão 2026.10. Foi aplicado o patch de inicialização MUSB do Debianator, commit `8eadfd86e2c94bf812a6644bd51b6188b586e8d7`.

O patch faz o controlador gadget ser encontrado quando USB Ethernet está desligado; sem ele, ACM/UMS podiam falhar com controlador não inicializado. Não foi necessário usar o firmware Debianator completo. O autor restringe aquele firmware a V1.0; reutilizar uma referência de código não homologa suas imagens para outra placa.

### Parâmetros preservados do perfil

| Parâmetro | Valor usado |
| --- | --- |
| Clock DRAM | 552 MHz |
| DRAM ZQ | 15291 |
| ODT | habilitado |
| Clock CPU | 1008 MHz |
| DCDC1 | 3300 mV |
| DCDC2 | 1100 mV |
| DCDC3 | 1200 mV |
| DCDC4 | desabilitado |
| DCDC5 | 1500 mV |
| ALDO2 | 2500 mV |
| ALDO3 / DLDO3 | 3000 mV |

A referência usava DCDC1 de 3000 mV; nosso backup registrava 3300 mV. O bootstrap de teste preservou 3300 mV. ALDO1 e outros parâmetros não explicitados pelo perfil continuam dependentes dos defaults revisados da versão fixada; isso merece auditoria ao construir outro perfil.

O vídeo foi desativado. O ambiente é `ENV_IS_NOWHERE`, sem gravação persistente de variáveis. O autoboot foi desativado (`BOOTDELAY=-1`). O preboot, quando iniciado por FEL, executa `mmc dev 0; ums 0 mmc 0`. Esse índice pertence à receita específica testada, não a uma regra universal de identidade de TF1.

O binário tem 528.912 bytes, SHA-256 `433cd84c1b0c92b49afc251b543b83a5b054de682d494dae26359562ba86ccdc`. O SPL tem 24.576 bytes; a assinatura `eGON.BT0` e seu checksum foram conferidos antes do envio. O U-Boot principal transferido tinha 496.080 bytes e iniciou em `0x4A000000`.

## 8. Ferramenta nativa de recuperação

Foi utilizado `sunxi-fel.exe` do pacote eperie/build-scripts v1.3, compilado da revisão sunxi-tools `530adfa1420c1a1a73aa93df5b4d4c39f997f490`, indicado pela documentação sunxi. SHA-256 do ZIP: `0fee8e293ac552f95e1a72585c31c59d89f77542294f89ef7de638de5b4a96c4`.

A ferramenta realizou o tratamento das pilhas SRAM/FEL necessário para executar SPL. Não substituir esse processo por uma escrita ingênua do SPL em SRAM: as pilhas da ROM ocupam áreas que precisam ser preservadas. O log registrou `sp_irq=0x2000`, `sp=0x5E08` e MMU inicialmente desabilitada pela ROM.

O bus/address foi selecionado explicitamente na tentativa. No aplicativo final ele será obtido na sessão e revalidado, não salvo como constante.

## 9. Como o SD de teste foi preparado e reconhecido

Antes do teste, o cartão conectado ao leitor tinha 15.728.640.000 bytes. Ele foi formatado em FAT32, rótulo TESTE_FEL, com partição começando em 1 MiB. Os primeiros 128 MiB foram zerados para remover inicialização antiga; foram verificados antes da formatação e os setores em 8 KiB e 128 KiB foram conferidos depois.

Essa limpeza foi uma operação autorizada sobre o cartão de teste. Não é um procedimento automático para cartões de clientes com jogos ou sistema de fábrica. Formatar apenas a partição pode deixar bootloader fora dela; por outro lado, limpar 128 MiB destrói dados de partida que talvez precisem ser preservados.

O leitor apresentou mídia sem MBR como partição implícita em offset zero. `Initialize-Disk` informou disco já inicializado. A sequência DiskPart de limpeza, criação de partição e formatação resolveu o caso. O instalador final deve tratar o layout real, não copiar cegamente essa sequência para qualquer mídia.

Depois do bootstrap, o cartão apareceu dentro do console como `Linux UMS disk 0`, USB `1F3A:1010`, com os mesmos tamanho e rótulo. Um arquivo de teste com nonce foi gravado e relido antes da imagem completa. Esse teste de arquivo só é adequado a mídia preparada/consentida; para cartões existentes, a inspeção inicial deve ser passiva.

## 10. Imagem e escrita integral

Imagem de origem: goodluckOS A33 Rhododendron/painel 1, SHA-256 `4077a465e548971a83a58a965e2bfd19ef0e2ad29d3ad20c39c354ba3478557a`. Imagem de prova: `Obsisidian-prova-USB-base-goodluck-20261029.img`, 1.073.741.824 bytes, SHA-256 `a113b3c53a0d2c2c319f4ada65a694ccb6b16dbed053458a32669ba47b7ae7c5`.

| Região | Início | Tamanho / observação |
| --- | --- | --- |
| Dados anteriores à raiz | setor 0 até 262143 | Inclui MBR, cadeia de partida e kernel; não tratar como espaço livre |
| Raiz Linux | setor 262144 = 128 MiB | 786432 setores = 384 MiB, ext4 |
| HOME | setor 1048576 = 512 MiB | 1048576 setores = 512 MiB, FAT32 |

O construtor goodluckOS reutiliza setores da origem a partir do setor 1 até antes do 172032; o kernel Android boot image começa no setor 172032, em 84 MiB. A existência dessa região explica por que a base baixada sem personalização não bastava para iniciar o aparelho. Não há evidência de que todos esses bytes sejam chaves individuais do console; também não foi comprovada sua redistribuição como pacote universal.

Na cópia de prova foram inseridos `/etc/obsisidian-usb-test-id` e o serviço `S98obsisidian-usb-proof`; `e2fsck` passou. O identificador foi `OBSISIDIAN-USB-20261029-01`. A origem e o backup integral ficaram preservados.

O gravador validou hash da imagem, identidade e capacidade do disco, rótulo e arquivo de prova. Bloqueou e desmontou o volume com FSCTL antes de abrir o disco físico. Executou escrita, sincronização e releitura de todos os 1 GiB, com o mesmo SHA-256. Essas regras ainda contêm dados específicos da tentativa; os scripts históricos não são o motor de produção.

## 11. Elevação e remoção segura

A primeira solicitação de administrador não resultou no início do gravador. O Windows informou cancelamento e o usuário relatou que a solicitação não apareceu. Não foi diagnosticada a causa da ausência da janela. Uma nova tentativa, depois de revalidar o alvo, iniciou e concluiu a operação.

A aplicação deverá tratar essa experiência como estado explícito de autorização pendente/cancelada, com mensagem e possibilidade de repetição. Não presumir que o usuário recusou deliberadamente nem deixar uma operação silenciosa em segundo plano.

Após a conferência integral, `CM_Request_Device_EjectW` retornou sucesso sem veto para o dispositivo UMS. O usuário desconectou o cabo, desligou completamente, ligou com o cartão no TF1, aguardou e reconectou à porta OTG. O reinício físico foi necessário nesta prova; não foi demonstrado um reboot automático do U-Boot UMS para o sistema instalado.

## 12. Confirmação de boot

Às 21:28:46 -03:00, a serial e MTP do goodluckOS voltaram. A leitura pela serial comprovou:

- Identificador exclusivo em `/etc/obsisidian-usb-test-id`.
- Mesmo identificador no relatório criado pelo serviço de boot em `/home/player/obsisidian-usb-installacao.txt`.
- Kernel `7.2.9-dirty`, `armv7l`, raiz ext4 em `mmcblk0p1` e HOME vfat em `mmcblk0p2`.
- Cartão SD com capacidade coerente com o destino da gravação.
- Pastas de jogos, incluindo GBA, NDS, SNES, PSX, PSP, CPS2 e CPS3.

O sucesso não foi inferido apenas de uma imagem escrita ou de um dispositivo USB aparecendo. Foi confirmado pelo sistema iniciado a partir da imagem de prova.

## 13. TF2, biblioteca e interface

Um teste anterior com goodluckOS e TF2 FAT32 teve confirmação do usuário de jogos GBA aparecendo e abrindo. Isso demonstra funcionamento do slot nessa configuração, mas não instalação automática em dois cartões nem suporte equivalente no firmware de fábrica.

O launcher usa `appd/appctl`; sua entrada é `/home/player/.local/launcher.sh` e o controle passa por `/run/appd/control`. A interface gráfica é direta por SDL/KMSDRM, sem X11/Wayland. Essa arquitetura deve ser preservada no empacotamento para evitar launcher e emulador disputando o DRM.

No código examinado, várias bibliotecas apontavam para `/media/external` e SNES usava `roms/snes/Roms`; também havia entradas de jogos específicos. O pacote comercial precisa ser verificado com biblioteca vazia e raiz de dados configurável. O desenvolvimento do launcher continua no projeto original e pode ter mudado depois dessa leitura.

## 14. Compilação e arquivos externos

O Docker Desktop instalado apresentou erro próprio ao iniciar um serviço de inference. Foi usado um WSL Alpine isolado, `ObsisidianBuild-20261029`, em `04-ferramentas/FEL/build/wsl-alpine` do projeto anterior. Ele foi encerrado após o teste. Não é um requisito do computador do cliente.

Alpine 3.22.7: SHA-256 `57e364a241bab0699cd8a760c0fd7b270e9cbcbd3cee822cb2b07ca6d925c16c`. Toolchain ARM: gcc-arm-none-eabi 15.1.0, com binutils 2.44. Foram necessários bash, bison, flex, dtc, Python/setuptools/elftools, OpenSSL e demais ferramentas de compilação. O host tool EFI capsule foi desativado por não fazer parte dessa receita, evitando dependência de GnuTLS.

O arquivo Ubuntu parcialmente baixado não é uma imagem válida de compilação e não deve entrar no pacote. Imagens de sistema, VHDX e backups integrais não foram copiados para o novo projeto; o inventário externo mantém os caminhos e estados conhecidos.

## 15. Regra para a próxima pessoa ou agente

Começar por este documento, pelo planejamento e pelo relatório final. Os protótipos históricos têm índices de disco, COM, letras e caminhos fixos. Consultá-los como evidência; migrar as operações para planos dinâmicos e restritos antes de oferecer execução geral. Não executar um script de preparação de cartão para testar a estrutura do projeto.

Para instalar a imagem atual do Obsidian OS, primeiro produzir um pacote candidato imutável, verificar seu perfil, launcher, layout e política de dados; depois repetir a instalação guiada e a confirmação do boot. A edição contínua da interface não deve alterar silenciosamente uma imagem já homologada.
