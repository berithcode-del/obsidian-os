# Histórico

## 2026-10-31 — o aplicativo grava o cartão sozinho (teste no leitor)

- **Pipeline completo no aplicativo:** conferir o pacote → bloquear/desmontar volumes (com retentativa e `mountvol /p`) → apagar a tabela de partições → testar o enlace com 16 MiB → gravar → reler e conferir → declarar e formatar a partição `OBSIDIAN` → criar `leitura/`, `videos/` e `saves/`. Um único identificador de disco atende enlace, gravação e releitura.
- **Defeito corrigido (o que travava tudo):** o Windows monta a FAT32 no instante em que a tabela de partições chega ao cartão e passa a negar escrita bruta naquela faixa (`Access to the path is denied`). A gravação agora escreve **o corpo da imagem primeiro (setor 1 ao fim) e o setor 0 por último**; sem tabela de partições não há volume montado durante a escrita. A tabela final é relida e conferida byte a byte.
- **Teste real no cartão de 15,7 GB (leitor USB):** `success: true` em 329 s — `image_hash_verified`, `cleaned`, `link_checked`, `readback_verified`, `layout_prepared`, `data_root H:\`. Cartão conferido depois: p1 0,38 GB (ext4), p2 `HOME` FAT32 0,5 GB com o launcher `de6fa0a3…`, p3 `OBSIDIAN` FAT32 13,63 GB com as três pastas.
- **Interface refeita no visual do site:** fundo `#0b0c0f`, superfícies `#14151a`, cobre `#c08a5e`, Sora embarcada, janela maximizada (o WPF diagramava 1240 DIP como 1550 px em escala 125% e cortava 25% do conteúdo), três passos (Conectar → Cartão → Gravar) e tudo o mais em segundo plano.
- **Splash e ícone:** a splash usa a arte do **sistema** (`obsidian-splash.png`, blocos de obsidiana com brasa) com marca, wordmark e barra de status, com tempo mínimo de 1,5 s na tela; o ícone (cubo de cobre com brasa) é embutido no executável e conferido por extração (`ApplicationIcon` + regeneração do apphost).

## 2026-10-31 — primeira inicialização aprovada no console (r3)

- **Defeito encontrado e corrigido:** o empacotador do overlay aplicava modo `0644` a todo arquivo fora de uma lista de exceção, e essa lista não incluía os scripts da base. Resultado: no primeiro boot todos os serviços recebiam `Permission denied` (rc=126) — sem `udevd` não havia dispositivos, sem `S30usb-gadget` não havia USB/serial e sem `S90backlight` a tela ficava preta. A imagem não podia iniciar.
- A causa só apareceu porque a revisão r2 passou a gravar um **diário de boot** em `/home/player/boot-rc.log` (partição FAT32, legível do Windows), com marcador de início/fim e código de retorno de cada serviço. O diário está preservado em `evidence/private/2026-10-30-cartao-formatado/boot-rc-r2.txt`.
- Correção: regra explícita de permissões no staging — tudo em `etc/init.d/`, `usr/local/bin/`, `usr/bin/`, `usr/sbin/`, `sbin/`, `bin/` e qualquer arquivo com `#!` sai como `0755`. O build agora **falha** se algum script de serviço não estiver executável dentro da imagem (`executáveis em etc/init.d + usr/local/bin: 31/31`).
- Revisão **r3**: `obsidian-os-1.0-lab-r3.img`, SHA-256 `c8543e46c7075ec82db3cc8f4335e3d914e8dd6d3cdc2d154f854aa132211b8a`.
- **Primeiro boot aprovado no console:** hostname `obsidian`, `PRETTY_NAME="Obsidian OS 1.0"`, todos os serviços com rc=0 no diário, `udevd`/`dbus`/`umtprd`/`appctl`/`triggerhappy` ativos, launcher `de6fa0a3…` rodando como `player`, DRM `card0/card1/renderD128`, brilho restaurado (5/10) e home com 319,6 MB livres.
- Área de dados criada no aparelho após o boot: `p3` no setor 2.097.152, FAT32 `OBSIDIAN`, 13,6 GB livres, montada em `/media/obsidian` com `leitura/`, `videos/` e `saves/`.
- As revisões r1 e r2 ficaram preservadas como evidência da falha (`obsidian-os-1.0-lab-r1-FALHA-BOOT/` e `artifacts/releases/obsidian-os-1.0-r2/`).

## 2026-10-30 — cartão da prova devolvido ao estado de novo

- Preservadas as evidências do cartão antes de apagar: relatório de boot da prova USB (`obsisidian-usb-installacao.txt`) e inventário de partições/arquivos em `evidence/private/2026-10-30-cartao-formatado/`.
- `tools/format-card.ps1`: `diskpart clean` + gravação de zeros no dispositivo inteiro com conferência por amostras; recusa disco de sistema, disco não-USB, série e tamanho divergentes. Executado com elevação em um cartão de 15.728.640.000 bytes (MXT-USB, série `150101v01`).
- Resultado: cartão sem tabela de partições, sem sistema de arquivos e com 100% dos setores sobrescritos por zeros — o mesmo estado de um cartão recém-comprado.

## 2026-10-30 — imagem Obsidian OS 1.0 e gravação real

- Capturado o sistema do console por shell serial (transferência base64 em blocos de 4 MiB com hash por bloco): 155.381.760 bytes, SHA-256 `971dbe8c10ec0d102a9c1d2f84b8563cee2e93132af96653aa95b5df9d9cea06`, sem escrita no aparelho.
- Montada a imagem `obsidian-os-1.0-lab-r1.img` (1.073.741.824 bytes, SHA-256 `094f8e698a236d3e1db64ad4f69a2e3e2bebbb3846728595972167221c8b3fa8`) a partir da base de prova comprovada (`a113b3c5…`) com overlay de 533 componentes, launcher `de6fa0a3…`, home limpa e marcador `/etc/obsidian-release`.
- Verificação da imagem: `e2fsck`/`fsck.vfat` limpos, 533/533 hashes conferidos dentro da imagem, partições e espaços livres registrados, nenhum dado pessoal presente.
- Instalador Windows: inventário de discos/volumes, gravação bruta com hash incremental e releitura, declaração da partição `p3` e formatação FAT32 `OBSIDIAN` pelo `diskpart`, execução elevada com plano imutável e relatório por tentativa.
- Restauração real de conteúdo: pasta ou imagem `.img` FAT32, reconhecimento de `roms`/`leitura`/`videos`/`saves`, checagem de espaço, política de conflito, hash por arquivo e relatório JSON.
- Interface: etapas 4, 5 e 7 saíram da prévia; seleção de cartão, confirmação por escrito (`GRAVAR`) e restauração passaram a executar operações reais.
- Validação: 14 testes de contrato Python, validador estrutural e 27 verificações do self-test .NET aprovados; gravação exercitada em cartão simulado de 14,42 GiB com `p3` formatada e montada.
- Pendente: gravação em cartão físico, primeiro boot da imagem no console, restauração para o cartão TF2 real, MTP/áudio/controles na imagem nova, assinatura e revisão de distribuição.

## 2026-10-30 — implementação funcional por etapas

- Planejado o caminho de implementação com critérios de saída e bloqueios de segurança por etapa.
- O catálogo local de perfis passou a alimentar a correspondência informativa de IDs USB; variantes desconhecidas e perfis de laboratório não são tratados como compatíveis para instalação.
- A verificação do pacote passou a usar o mesmo catálogo; uma conferência bem-sucedida do hash não libera instalação. A autorização permanece fechada até existir assinatura confiável e revisão de distribuição.
- Implementada a máquina de estados em runtime, com as guardas de evidência do contrato; a interface só percorre estados de diagnóstico e não tem transições para operações de instalação.
- Adicionado verificador de assinatura destacada ECDSA P-256/SHA-256 e catálogo de confiança do aplicativo, inicialmente vazio para falhar fechado até a escolha de uma chave oficial.
- Compilação Release concluída sem avisos/erros e validador estrutural aprovado; não houve acesso físico ao console ou cartão.

## 2026-10-29 — criação do projeto do instalador

- Consolidada a prova FEL → carregador na RAM → cartão por USB → gravação → releitura → boot.
- Separados desenvolvimento do sistema, aplicação Windows, perfis, pacotes e evidências.
- Preservadas cópias verificadas dos componentes de laboratório e referências às imagens existentes.
- Documentados produto comercial, arquitetura, suporte, homologação e integração de imagens em evolução.
- Criados contratos, esquemas e validação local. Ainda não há aplicativo gráfico publicado.

## 2026-10-29 — início do aplicativo Windows

- Escrita a primeira tela WPF em português e o inventário USB passivo do Windows.
- A consulta só acontece após clique explícito; abrir a janela não enumera dispositivos.
- Registrados os três modos USB já observados e a distinção entre ID reconhecido e perfil confirmado.
- Criados solução .NET, script de compilação e instruções. O código ainda não foi compilado e o aplicativo não foi iniciado.
- Acrescentada seleção de manifesto e verificação sob solicitação do arquivo de imagem local por tamanho e SHA-256; continua sem gravação ou instalação.
- Exibido progresso da leitura durante a conferência de imagens grandes.
- Instalado o SDK .NET 10.0.403 no perfil local de desenvolvimento e compilada a solução WPF em Release: zero avisos, zero erros. O aplicativo não foi iniciado.
- Atualizada a interface para uma prévia guiada pelas seis etapas do atendimento, com bloqueios explícitos para leitura de cartões, gravação e confirmação do boot. Recompilação Release: zero avisos, zero erros; aplicativo e console não foram abertos para esta validação.
- Adicionado carregamento local dos perfis e correspondência informativa dos IDs USB declarados, mantendo incompatibilidade/desconhecido e perfil de laboratório bloqueados. Compilação Release e validador estrutural passaram; não houve consulta a hardware.
