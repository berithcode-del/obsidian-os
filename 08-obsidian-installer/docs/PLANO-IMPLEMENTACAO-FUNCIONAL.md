# Plano de implementação funcional

O aplicativo será desenvolvido e revisado por etapas. Cada etapa precisa cumprir seus critérios de saída antes de liberar a operação seguinte. A interface não poderá mostrar uma ação como concluída quando apenas exibiu uma instrução ou simulou um resultado.

## Limite operacional desta fase

O console está reservado para outra função. Até o usuário liberar seu uso, não conectar, enumerar, consultar, reiniciar ou enviar comandos ao console. O desenvolvimento atual usa o código, os perfis e as evidências já arquivadas no projeto; nenhuma etapa depende do dispositivo físico para compilar.

## Etapas e critérios

| # | Etapa | Situação | Critério para considerar funcional |
|---|---|---|---|
| 0 | Base de desenvolvimento e compilação | Concluída | SDK fixado localmente; solução compila em Release sem avisos/erros. |
| 1 | Diagnóstico Windows e descoberta USB | Implementado; validação física pendente | A janela abre sem consultar USB; consulta manual lista os dispositivos, termina em `NeedsProfileEvidence` e não envia comandos. A validação ao vivo requer sessão de laboratório liberada. |
| 2 | Correspondência de perfis | Implementada no software; identificação física pendente | IDs USB são comparados com o catálogo; mostrar candidatos e nível de validação; nunca declarar placa compatível só por VID/PID. |
| 3 | Pacote do sistema | Verificador implementado; sem chave confiável | Confere manifesto, perfil, tamanho, hash e assinatura destacada ECDSA. Catálogo de confiança vazio; pacote cliente e imagem candidata ainda não existem. |
| 4 | Descoberta e identidade do cartão | Não iniciada | Enumeração somente leitura, identificação persistente, exclusão de discos do Windows e correlação com o console. Não avançar sem mídia de teste designada. |
| 5 | Recuperação e bootstrap em RAM | Não iniciada no aplicativo | Protocolo e componente restrito reproduzidos por perfil homologado; consentimento, sessão e transições correlacionados. |
| 6 | Plano administrativo e gravação | Não iniciada | Helper assinado revalida plano/alvo, pede elevação, grava somente destino autorizado e verifica releitura integral; interrupções ficam recuperáveis. |
| 7 | Layout e biblioteca de jogos | Não iniciada | Política de um/dois cartões homologada, espaço expandido conforme o pacote e dados existentes tratados explicitamente. |
| 8 | Primeiro boot e conclusão | Não iniciada | Sessão e versão do boot correspondem à instalação; relatório local contém evidência real de conclusão. |
| 9 | Análise integrada e distribuição | Pendente | Revisar ameaças, recuperação, assinatura/licenças, portabilidade e homologação ponta a ponta em Windows limpo e hardware liberado. |

## Ordem de desenvolvimento

1. Finalizar uma imagem candidata imutável do Obsidian OS e definir como a publicação será autenticada; a imagem ainda está em alteração.
2. Ligar as transições da máquina de estados às evidências reais e manter a gravação inalcançável enquanto faltar qualquer guarda.
3. Implementar e validar a assinatura do catálogo/pacote com uma chave pública de confiança escolhida para o produto.
4. Implementar descoberta de discos sem escrita; só consultar mídia quando houver uma sessão de laboratório liberada.
5. Definir o protocolo, correlacionar sessão FEL/UMS e construir os planos imutáveis.
6. Implementar o helper de gravação com limites e recuperação; validar somente em mídia de laboratório designada.
7. Integrar expansão de armazenamento, estrutura de jogos, confirmação do boot e revisão integrada para distribuição.

## Critério de bloqueio

Uma etapa para quando faltar evidência de hardware, imagem, identidade do alvo, consentimento ou ambiente de laboratório. O aplicativo deve explicar o bloqueio e preservar a etapa anterior; não tentar outro perfil, cartão ou bootstrap automaticamente.
