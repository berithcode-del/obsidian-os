# Componentes e distribuição comercial

O inventário em `components.json` registra arquivos, origens, hashes e pendências. A presença de um arquivo aqui registra a prova técnica; não significa aprovação para publicar um pacote comercial.

| Componente | Origem e tratamento antes da distribuição |
| --- | --- |
| U-Boot e patch USB | Preservar avisos e conferir licenças aplicáveis aos arquivos e ao patch. Preparar o código-fonte correspondente, configuração, patch e instruções de compilação na forma exigida pelas licenças aplicáveis. O inventário fixa as revisões. |
| sunxi-tools / sunxi-fel Windows | Licença recebida preservada em `sunxi-tools-LICENSE.md`; preservar avisos, conferir o build de terceiros e fornecer o fonte correspondente conforme a licença. A receita do construtor também foi arquivada. |
| goodluckOS, kernel, ferramentas, emuladores e futuros componentes da imagem | Auditar a lista real de pacotes da imagem candidata, suas licenças, fontes, modificações e avisos. A imagem de prova não recebeu revisão de distribuição. |
| Dados de partida derivados do cartão de fábrica | Origem rastreada, mas direitos de redistribuição e possibilidade de substituição por componentes documentados continuam pendentes. Não assumir que possuir o cartão autoriza republicar todo o seu conteúdo. |
| Zadig / WinUSB | Zadig 2.9 foi usado manualmente na prova e não está incluído no pacote. Definir o método de preparação do driver, revisar distribuição, assinatura e experiência administrativa. WinUSB é parte da plataforma Windows; não publicar pacotes improvisados. |
| Código próprio do instalador | Licença e condições do serviço ainda serão escolhidas. Manter inventário de dependências e analisar as obrigações de cada integração. Separar processos, por si só, não resolve a análise de licenças. |
| Jogos, BIOS, capas e marcas | Não incluir material de terceiros sem permissão aplicável. Preparar pastas vazias e orientar o cliente a usar conteúdo autorizado. Revisar também o nome comercial antes do lançamento. |

A GPL permite cobrar pela distribuição e por serviços. Isso não elimina suas condições, inclusive as obrigações relativas ao código-fonte quando aplicáveis. A oferta comercial pode cobrar preparação, suporte, conveniência e manutenção; não deve prometer exclusividade sobre componentes de terceiros. Referência: [FAQ da GNU sobre venda de software GPL](https://www.gnu.org/licenses/gpl-faq.en.html#DoesTheGPLAllowMoney).

## Entregáveis antes da publicação

1. Inventário da imagem realmente entregue, incluindo versões e modificações.
2. Avisos e textos de licença de cada componente redistribuído.
3. Fontes correspondentes e instruções de obtenção/reconstrução conforme as obrigações de cada licença.
4. Registro da revisão dos dados de partida e demais conteúdos da imagem.
5. Condições claras de suporte e acesso às atualizações, sem impedir direitos concedidos pelas licenças dos componentes.
6. Pacote sem identificadores pessoais, relatórios privados ou jogos do aparelho de teste.

Este documento é uma lista de trabalho do projeto, não uma conclusão de que todos esses direitos já foram obtidos.
