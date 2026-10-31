# ADR 0001 — aplicação Windows, helper e pacotes independentes

**Estado:** proposta inicial; 29/10/2026.

## Decisão

Adotar C#/.NET 10 LTS com WPF como primeira opção para o aplicativo Windows x64. Publicar com runtime próprio. Manter um helper administrativo restrito e separar a versão do aplicativo das imagens, perfis e bootstraps.

## Motivos

O produto é específico de Windows e precisa interagir com PnP, WinUSB, volumes, discos físicos e elevação. O problema principal não é renderizar uma página; é controlar uma sequência de operações nativas com identidade de dispositivo e estados confiáveis. Uma interface WPF evita exigir WebView como parte central da distribuição.

## Alternativas consideradas

| Opção | Vantagem | Custo ou questão a validar |
| --- | --- | --- |
| WPF/.NET | Interface Windows e acesso às APIs da plataforma | Tamanho da publicação autossuficiente, assinatura e SDK de desenvolvimento |
| Tauri/Rust | Interface web e binário nativo | Cadeia adicional de build e estratégia de distribuição do WebView |
| Python empacotado | Reutilização rápida da prova | Distribuição de dependências, assinatura e manutenção do helper nativo |
| WinUI | Integração visual recente com Windows | Modelo de empacotamento e requisitos adicionais a comparar |

Esta comparação é arquitetural; as alternativas não foram compiladas nem submetidas a benchmark neste projeto.

## Consequências

Python e WSL continuam úteis no laboratório. A primeira implementação .NET deve reproduzir diagnóstico e compatibilidade antes de escrever discos. Não portar algoritmos de SPL por simplificação sem preservar o comportamento validado do sunxi-tools.

Um ZIP portátil auditável é o primeiro formato proposto. Arquivo inicial único continua como possibilidade, condicionado a extração protegida, verificação de componentes e experiência de atualização. Ainda não há decisão de licença do código próprio.
