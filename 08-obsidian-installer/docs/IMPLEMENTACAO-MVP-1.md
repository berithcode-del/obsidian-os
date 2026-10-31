# MVP 1 — guia do processo em modo de prévia

## Objetivo

Organizar a experiência do aplicativo como um percurso completo, da conexão USB à confirmação do primeiro boot, e mostrar com clareza o que já pode ser feito e o que ainda depende do motor de instalação.

## Código escrito nesta etapa

- Janela WPF em português, com status, lista de dispositivos USB e consulta iniciada manualmente.
- Enumeração com SetupAPI e consulta do estado PnP pelo Configuration Manager.
- Classificação visual dos três IDs observados: FEL 1F3A:EFE8, UMS temporário 1F3A:1010 e sistema Linux iniciado 1D6B:0104.
- IDs desconhecidos aparecem como não confirmados.
- A tela diz explicitamente que SoC e variante não foram confirmados por um VID/PID.
- Aplicativo principal solicitado como usuário comum.
- Seleção manual de manifesto e de arquivo .img/.bin, seguida de verificação do tamanho e do SHA-256 declarado.
- Leitura sequencial com progresso em bytes para imagens grandes.
- O catálogo de perfis do projeto é copiado ao output da aplicação para comparar os IDs declarados no pacote.
- Manifestos de laboratório continuam sem liberação; aprovação declarada no JSON não é tratada como assinatura.
- Verificador de assinatura destacada ECDSA P-256/SHA-256, com bytes exatos do manifesto e catálogo de chaves empacotado pelo app. O catálogo está vazio até o projeto definir a chave oficial; assinatura ausente/desconhecida nunca autentica o pacote.
- Navegação por seis etapas: conectar console, confirmar compatibilidade, escolher versão, preparar cartões, instalar e confirmar o funcionamento.
- Telas futuras explicam os dados que serão apresentados e por que ações de cartão, gravação e boot continuam indisponíveis.
- Navegação permite conhecer cada etapa, mas não marca etapas futuras como concluídas nem simula uma instalação.
- Catálogo de perfis locais lido com limite de tamanho e validação estrutural básica; IDs VID/PID candidatos vêm dos próprios perfis, não de uma regra de instalação embutida.
- Correspondência mostra modo declarado, nível de validação e bloqueio do perfil de laboratório; não representa prova física do SoC, placa ou painel.
- Máquina de estados no núcleo replica as transições e evidências críticas do contrato. A interface aciona somente início de diagnóstico, identificação incompleta e falha de consulta; não existe caminho de UI até gravação.

## Ainda não faz

Não abre WinUSB, não consulta a ROM FEL, não carrega bootstrap, não prepara driver, não enumera discos para instalação, não grava cartões nem instala uma imagem. O hash é calculado somente depois que o usuário escolhe um arquivo e clica em Conferir integridade. O reconhecimento físico do perfil continua futuro.

## Arquivos

- src/ObsidianInstaller.App/: janela e inicialização WPF.
- src/ObsidianInstaller.Core/: modelos e interpretação dos IDs de prova.
- src/ObsidianInstaller.Windows/: acesso de leitura ao inventário PnP via SetupAPI.

A solução compilou em Release com o SDK 10.0.403, com zero avisos e zero erros. O executável está em `src/ObsidianInstaller.App/bin/x64/Release/net10.0-windows/`; ele não foi iniciado. A consulta USB continua dependendo de um clique explícito após a abertura. A interface ainda não foi validada visualmente nem conduz uma instalação física.

O validador estrutural do projeto também passou nesta revisão. Essa validação cobre esquema e integridade dos artefatos locais, não a leitura de um console conectado. Nenhum acesso ao console foi feito.
