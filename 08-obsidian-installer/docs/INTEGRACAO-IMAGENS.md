# Integração das imagens do Obsidian OS

## Princípio

O sistema continua em desenvolvimento. O instalador deve consumir uma entrega identificada, e não uma pasta de trabalho mutável. A imagem e o bootstrap são versionados separadamente.

## Entrega do projeto do sistema

Cada candidato inclui: imagem, manifesto, versão, commit/origem do build quando disponível, perfil permitido, hash, tamanho expandido, layout, requisitos de boot, política de dados e método de confirmar a instalação. A primeira versão completa também precisa iniciar com biblioteca vazia, resolver a raiz dos jogos e declarar os emuladores incluídos.

Os esquemas em `../schemas/` definem o contrato inicial. O registro de prova em `../catalog/` é de laboratório e aponta para uma imagem externa existente; não é um pacote redistribuível nem uma atualização estável.

## Promoção de versão

1. Congelar uma cópia do candidato; alterações posteriores geram outro identificador/hash.
2. Conferir layout, arquivos de partida, serviços e bootstrap compatível.
3. Instalar em mídia de teste e confirmar boot, launcher, áudio, controles e bibliotecas.
4. Executar os ensaios de armazenamento pertinentes e registrar resultado.
5. Revisar componentes, fontes correspondentes e condições de distribuição.
6. Assinar metadados de publicação e registrar a aprovação do canal.

Um build do instalador não pode mudar silenciosamente `customer_installation_allowed` ou a aprovação do perfil. A assinatura de um catálogo também não transforma uma imagem não testada em imagem homologada.

## Três tipos de mudança

| Mudança | Operação provável | Verificação necessária |
| --- | --- | --- |
| Tema/launcher/assets | Atualização de arquivos | Integridade, ABI, appd, rollback e preservação de configurações |
| Kernel/serviços da base | Pacote de sistema ou imagem | Boot, USB, controles, vídeo, áudio e compatibilidade com dados |
| Layout/cadeia de partida | Reinstalação ou migração dedicada | Backup, partições, bootstrap, capacidade e recuperação |

A operação é declarada pelo pacote e validada pelo instalador. Não escolher o tipo pela extensão de um arquivo sozinho.

## Armazenamento

O primeiro objetivo é um cartão com HOME expandida. A imagem precisa fornecer uma receita testada de crescimento no primeiro boot ou o instalador precisa aplicar uma receita específica ao layout, preservando as regiões de partida. A raiz não deve ser redimensionada por suposição.

Em dois cartões, o pacote declara o controlador/slot esperado e a raiz da biblioteca. Dados existentes são inspecionados antes de qualquer preparação. Extensões, capas, saves e BIOS devem ter regras explícitas por emulador e por versão do sistema.

## Independência do cartão de fábrica

O bootstrap temporário já funcionou a partir de fontes compiladas. A imagem persistente de prova ainda reutiliza dados de partida do backup do perfil. Para o produto, decidir entre uma cadeia própria homologada e um pacote de perfil cuja origem/distribuição estejam resolvidas. Não tratar um dump pessoal como artefato público apenas porque iniciou no aparelho.

## Atualização do catálogo

O cliente deve poder instalar um pacote local aprovado quando estiver offline. Downloads usam catálogo autenticado, cache por hash e verificação depois da extração. Uma falha de rede não exige apagar o cartão. Arquivos temporários ficam separados das versões aprovadas e dos backups do usuário.
