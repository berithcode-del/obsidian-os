# Plano de homologação e critérios de publicação

## Situação atual

Há uma instalação integral comprovada em um aparelho, no perfil de laboratório A33/Rhododendron/painel 1. O funcionamento de TF2 FAT32 com GBA foi confirmado em teste anterior do sistema, mas a instalação automática com dois cartões não foi executada.

## Matriz de ensaios

| Área | Casos necessários | Evidência de aprovação |
| --- | --- | --- |
| Windows | Sistema limpo, driver ausente, driver presente, usuário comum e UAC cancelado/não confirmado | Fluxo compreensível, erro específico e nova tentativa controlada |
| Hardware | Mesmo aparelho com outros cartões e outras unidades físicas do perfil | Registro de SoC, evidências da placa/painel e boot repetido |
| USB | Cabo de carga, cabo de dados, porta/hub diferente, mais de um dispositivo conectado | Sem seleção silenciosa do primeiro dispositivo |
| Mídia | Capacidades reais diferentes, leitor que apresenta partição implícita, cartão protegido e mídia com dados | Destino correto, preparação consentida e erro recuperável |
| Escrita | Erro de bloqueio, falta de espaço, imagem truncada e falha de escrita | Operação abortada sem apresentar sucesso |
| Releitura | Conteúdo diferente do esperado | Falha explícita de integridade e indicação de regravação |
| Interrupções | Desconexão em cada fase e fechamento do aplicativo | Estado persistido e recuperação definida para aquela fase |
| Inicialização | Reinício, boot não confirmado, serial ausente, painel sem imagem e imagem errada | Diagnóstico distinto e versão/tentativa confirmadas |
| Dados | Um cartão expandido, TF2 existente, saves e configurações | Preservação ou migração conferida conforme o plano |
| Atualizações | Troca do launcher, troca da base, pacote incompatível e catálogo adulterado | Rejeição correta, rollback pertinente e versão final reconhecida |
| Distribuição | Máquina sem Python/WSL/.NET prévio, pasta com espaços, caminho Unicode e pasta sem escrita | Pacote portátil utilizável e local de cache apropriado |

## Metas propostas para o piloto

Como meta inicial de engenharia, repetir pelo menos cinco instalações distribuídas entre cartões/capacidades diferentes e buscar três unidades físicas com perfil documentado antes de ampliar o serviço. Esses números são critérios propostos de saída, não validação estatística de todo o mercado. Uma divergência exige rever a definição do perfil.

Todo caso deve registrar versão do aplicativo, versão/hash do bootstrap, pacote do sistema, origem da identificação do hardware, mídia, Windows, fases e resultado. Identificadores completos ficam apenas no dossiê privado; o resumo compartilhável usa IDs de sessão.

## Barreiras para publicar um pacote

- Perfil aprovado para o canal e pacote correspondente.
- Fontes/componentes e condições de distribuição revisados.
- Integridade e autenticidade do catálogo e dos binários verificáveis.
- Instalação e boot testados com a imagem exata publicada.
- Política de dados, expansão e recuperação documentada.
- Relatório de atendimento sem dados pessoais desnecessários.
- Atendimento preparado para os modos de falha observados no piloto.

O perfil de laboratório e o pacote de prova deste repositório continuam com instalação pública desabilitada. A validação local dos arquivos não os promove para distribuição.
