# Retomada do projeto

## Imagem 1.0 pronta — 30/10/2026

- Capturado o sistema do console por shell serial em blocos verificados (155.381.760 bytes, SHA-256 `971dbe8c…`), sem escrever no aparelho.
- Montada `artifacts/releases/obsidian-os-1.0-r1/obsidian-os-1.0-lab-r1.img`: 1.073.741.824 bytes, SHA-256 `094f8e69…3fa8`, 533 componentes com hash conferido dentro da imagem, launcher `de6fa0a3…`, sem conteúdo pessoal. Ver [guia](GUIA-INSTALACAO-1.0.md) e [referência](OBSIDIAN-OS-1.0.md).
- Verificações executadas: `e2fsck`/`fsck.vfat` limpos, estrutura conferida, 113.152 KiB livres na raiz e 329.428 KiB na home.
- Instalador: inventário de discos, gravação bruta com hash e releitura, `p3` `OBSIDIAN` declarada e formatada via `diskpart`, execução elevada por plano imutável, relatório por tentativa.
- Restauração: pasta ou `.img` FAT32, espaço verificado, conflitos controlados, hash por arquivo e relatório JSON; 86 livros copiados nos testes.
- Prova sem mídia: cartão simulado de 14,42 GiB recebeu a imagem, teve `p3` criada e formatada e as três partições montadas; 27 verificações do self-test .NET e 14 testes de contrato passaram.
- Pendente imediato: gravar um cartão físico, iniciar a imagem no console e registrar splash, launcher, jogos, leitura, vídeo, MTP, TF2, áudio, controles e energia. Depois: assinatura, custódia de chave e revisão de distribuição.
- Captura e staging são reproduzíveis: `tools/console-pull.py` → `tools/stage-overlay.py` → `tools/build-os-image.ps1`.
- Pacote portátil publicado em `packaging/out/instalador-win-x64/` (`ObsidianInstaller.exe`, autossuficiente, sem assinatura).
- Verificação visual das sete páginas feita por automação de UI e captura de tela; imagens em `evidence/private/2026-10-30-ui/` (privado). Ajustes aplicados depois da revisão: largura do conteúdo para não cortar em 100% de escala, rodapé da barra lateral sem altura sobrando e lista de destinos priorizando cartões `OBSIDIAN`/removíveis.
- Auditoria de UI do skill de design: 0 crítico e 0 aviso; os 75 itens informativos são falsos positivos de "estilo inline" porque o XAML referencia estilos nomeados do sistema de design.

## Progresso funcional — 30/10/2026 (antes da imagem)

- Concluída a primeira camada de catálogo em runtime: os IDs USB observados são comparados com os declarados nos perfis empacotados, e a interface mostra candidato, modo e nível de validação.
- A checagem de pacote usa o mesmo catálogo de perfis validado e agora verifica assinatura destacada ECDSA contra chaves empacotadas pelo app. O catálogo está vazio; hash correto não prova autoria e `CanInstall` permanece fechado.
- Núcleo da máquina de estados criado a partir do contrato; a UI só inicia a consulta e para em `NeedsProfileEvidence` ou `Failed`. Os demais estados permanecem sem integração de hardware.
- Release compilada com zero avisos/erros; validador estrutural passou. Nenhum console/cartão foi consultado e o aplicativo não foi iniciado.
- Próximo marco de software: definir e fixar a chave oficial, integrar a validação de pacote à máquina de estados e preparar uma imagem Obsidian candidata imutável quando ela sair da fase de alterações. Mídia física e gravação aguardam ambiente de laboratório liberado.
- Análise integrada registrada em `ANALISE-INTEGRADA.md`; o principal bloqueio arquitetural é conectar as guardas restantes a fontes reais de evidência, autorização e identidade de destino.

## Entrega de organização — 29/10/2026

- Pasta independente `08-obsidian-installer/`, sem mover o desenvolvimento do launcher nem duplicar imagens grandes e ambientes WSL.
- Planejamento do aplicativo e da oferta comercial, em Markdown e HTML de leitura/impressão.
- Conhecimento técnico consolidado, com comprovações e limites separados.
- Arquitetura, fluxo do cliente, integração de imagens, operação e homologação.
- Perfil de laboratório e catálogo da imagem realmente instalada, ambos sem liberação para clientes.
- Bootstrap e ferramenta nativa preservados, configuração e patch de compilação, origem e hashes.
- 45 cópias históricas rastreáveis no manifesto de migração. Scripts com caminhos específicos foram arquivados como texto para consulta.
- Esquemas de perfil/pacote e contrato de estados, com validação local e dez testes de casos válidos e recusas relevantes.
- Prévia WPF com navegação por seis etapas: conexão, compatibilidade, versão, cartões, instalação e confirmação do boot. A interface distingue ações disponíveis e etapas futuras bloqueadas. O inventário USB usa apenas SetupAPI e só consulta após clique; os rótulos são conservadores.
- Correspondência dos IDs USB carregada dinamicamente do catálogo de perfis local. Um ID correspondente mostra perfil candidato e nível de validação; não comprova placa, SoC ou painel e não libera instalação.
- Plano funcional sequenciado, com critérios de saída e bloqueios explícitos; a próxima etapa é validar e endurecer pacotes, mantendo instalação bloqueada enquanto imagem e perfil forem de laboratório.
- Acrescentado fluxo local para conferir manifesto, perfil declarado, tamanho e SHA-256 do arquivo de imagem escolhido. O hash só é calculado após seleção explícita do arquivo e clique; não há escrita.

O aplicativo em `src/ObsidianInstaller.App/` agora apresenta o percurso completo como prévia; o núcleo carrega o catálogo e compara IDs USB declarados em perfil sem presumir compatibilidade física. O SDK .NET 10.0.403 foi instalado no perfil local de desenvolvimento; a solução compilou em Release, com zero avisos e zero erros, e o validador estrutural passou. A janela não foi iniciada e ainda não houve validação visual ou física. A etapa de cartões, o helper de produção, a gravação e a confirmação do boot não foram implementados. Não há pacote comercial assinado nem integração da imagem completa do Obsidian. A receita do bootstrap também não foi recompilada nesta etapa.

## Ordem de trabalho

Seguir também [PLANO-IMPLEMENTACAO-FUNCIONAL.md](PLANO-IMPLEMENTACAO-FUNCIONAL.md), que define critérios de saída e o limite de não acessar o console até ele ser liberado.

| Ordem | Tarefa concreta | Como saber que terminou |
| --- | --- | --- |
| 1 | Definir o processo de gerar uma imagem candidata do Obsidian OS a partir do sistema em desenvolvimento | Imagem congelada, manifesto, hash, origem, perfil e método de confirmação registrados |
| 2 | Integrar launcher e biblioteca vazia na candidata | Boot, vídeo, áudio, controles, appd e caminhos dos jogos verificados; versão identificável |
| 3 | Compilar e revisar o diagnóstico portátil Windows em modo de leitura | Aplicativo abre sem consultar USB; um clique expresso mostra dispositivos e limites de identificação; sem escrita |
| 4 | Implementar seleção/verificação de pacotes e plano da tentativa | Recusar perfil desconhecido, imagem incompatível e pacote adulterado; exibir destino e efeito sobre dados |
| 5 | Migrar o caminho FEL/UMS e a gravação para o aplicativo/helper | Repetir a prova em mídia de teste com identidade dinâmica, releitura integral e registro de boot |
| 6 | Resolver expansão da área de jogos e recuperação de falhas | Usar espaço do cartão conforme política testada e informar corretamente falhas/interrupções |
| 7 | Fechar a identificação de variantes e o piloto assistido | Homologação em outras unidades/Windows, distribuição revisada e atendimento mensurável |

Dois cartões, atualizações e atendimento a outras famílias entram conforme os critérios do planejamento. Eles não precisam ser presumidos para concluir a primeira instalação guiada em um cartão.

## Como conferir o estado sem tocar no console

Na pasta do projeto, executar `powershell -ExecutionPolicy Bypass -File tools/build-app.ps1` para compilar. O SDK está em `%LOCALAPPDATA%/ObsidianInstaller/dev-sdk`, selecionado por `global.json`. As validações de estrutura permanecem disponíveis com `python tools/validate_project.py`; a opção `--hash-external` relê as duas imagens para conferir os hashes do catálogo.

O resultado vai para `../artifacts/validation-report.json`, com data UTC, escopo e contagens. Esse relatório confirma arquivos e contratos, não substitui testes do aplicativo ou do hardware. Os resultados da instalação física anterior estão em `../evidence/private/2026-10-29/`.

## Preservação e separação

O backup original continua em `D:/R36S-backup-sistema-2026-10-28.img`, com 15.728.640.000 bytes. A restauração integral permanece não testada. O inventário externo registra a imagem de prova e sua origem. O projeto do sistema continua em `../../01-projeto/`; não modificar sua imagem ativa enquanto outro processo a estiver produzindo.

Para retomar a implementação, ler primeiro o planejamento, `CONHECIMENTO-VALIDADO.md` e `ARQUITETURA.md`. Não executar diretamente os scripts históricos: eles pertencem ao aparelho e à mídia da prova e contêm identificadores fixos.
