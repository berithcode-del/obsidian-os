# Fluxo do cliente

## Conectar

Explicar a porta de dados/OTG e o uso de um cabo de dados. Identificar se há sistema em execução ou modo de recuperação. Se nada aparecer, orientar alimentação, cabo e modo de partida; não declarar defeito ou incompatibilidade a partir da ausência de enumeração.

Um cartão de fábrica ainda inicializável pode impedir o fallback FEL. O cliente deve preservar esse cartão e usar o procedimento homologado para iniciar em recuperação com a mídia de destino. O piloto não depende de inserir/remover cartões com o aparelho ligado; hot-swap não foi testado.

## Verificar compatibilidade

Apresentar processador, perfil, painel e nível de comprovação. Os três resultados são: perfil atendido, identificação incompleta e hardware ainda não atendido. Mostrar a próxima ação útil em cada caso. Um SoC Allwinner genérico não recebe um bootstrap GA36 automaticamente.

## Escolher versão

Mostrar versão do Obsidian OS, canal, compatibilidade e efeitos sobre jogos/saves. Para a imagem local em desenvolvimento, identificar o candidato pelo hash calculado e registrá-lo como laboratório. Os clientes recebem somente pacotes publicados para seu perfil.

## Preparar cartões

Escolher um ou dois cartões apenas quando o modo estiver homologado. Apresentar capacidade real, função e conteúdo reconhecido. Em reinstalação, separar a possibilidade de preservar dados da necessidade de regravar o sistema. Nunca marcar formatação de um cartão de jogos existente como opção padrão.

## Confirmar e instalar

Apresentar o resumo do plano e pedir confirmação do destino. Depois, orientar a solicitação do próprio Windows para a etapa administrativa. Se a janela não aparecer ou for cancelada, manter o plano em estado recuperável e explicar como tentar novamente.

Durante a operação, mostrar fase, quantidade transferida e motivo de uma eventual pausa. Permitir cancelamento nas fases em que ele é seguro. Durante a escrita, avisar que interromper deixa o cartão incompleto e encaminhar para recuperação, sem simular conclusão.

## Reiniciar e concluir

Após sincronização e releitura, liberar o armazenamento e orientar desligar/ligar. Quando possível, verificar automaticamente o sistema pela interface prevista no pacote. A verificação deve relacionar versão, perfil e tentativa à imagem instalada; uma COM genérica não basta.

Ao concluir, informar onde adicionar jogos e o espaço disponível. Oferecer relatório local e acesso ao suporte contratado. Se o boot ainda depender de uma ação do cliente, mostrar “gravação conferida; inicialização aguardando confirmação”.
