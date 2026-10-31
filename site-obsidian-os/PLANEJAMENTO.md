## Revisão da hero — referências cinematográficas

A hero usa uma única imagem completa do console sendo liberto da obsidiana, com iluminação, cenário e fragmentos integrados. O console navegável fica no explorador de funcionalidades, acessível pelo botão Experimentar no console. A arte está em assets/hero-liberte-console.png; o prompt completo está em qa/hero-prompt.md. Esta revisão substitui a composição da hero em camadas descrita abaixo.

# Obsidian OS: nova landing page

Atualizado em 30/10/2026. Este plano substitui as duas propostas visuais anteriores.

## Direção aprovada

- Conceito central: **Liberte seu console.** Um portátil branco vertical emerge de uma rocha fraturada de obsidiana, com iluminação cinematográfica e partículas discretas.
- Aparelho: desenho interativo baseado na foto fornecida, com direcional à esquerda, grade central de som, X azul acima, Y verde à esquerda, A vermelho à direita, B amarelo abaixo, FN/SELECT/START e dois analógicos inferiores.
- A tela será HTML navegável. A carcaça e os botões serão componentes do site, permitindo interação por clique, toque e teclado.
- Usar a arte oficial de inicialização e conservar a identidade do cubo de obsidiana.
- Linguagem visual jovem e gamer: Sora, títulos fortes, recortes e profundidade. Sem títulos serifados ou seções com cores arbitrárias.
- Nenhuma marca, personagem, capa de jogo ou referência comercial de terceiros na apresentação. Conteúdo demonstrativo original.

## Temas como sistema de cores

Uma única paleta ativa governa a página e a demonstração. As quatro opções vêm do projeto:

| Tema | Superfície | Destaque | Complemento |
| --- | --- | --- | --- |
| Obsidian | #0B0C0F | #C08A5E | #9DB9D8 |
| Floresta Viva | #123D32 | #C4F36B | #A7F3D0 |
| Pôr do Sol | #4B293F | #75F7CA | #F0A6C8 |
| Cosmos Arcade | #302052 | #67E8F9 | #E879F9 |

O tema Obsidian é o padrão. O seletor também muda a aparência do console, sem alterar a carcaça branca. A luz cobre da arte de rocha faz parte da identidade mineral do produto.

## Estrutura

1. Cabeçalho com marca, navegação e download.
2. Hero épico com console saindo da obsidiana e interface utilizável.
3. Explorador de menus: Home, Jogos, Favoritos, Leitura, Vídeos, Aparência, Ajustes e Energia. Cada aba mostra a interface e explica suas funções.
4. Seletor dos quatro temas e suas artes reais.
5. Identidade de inicialização com a arte oficial.
6. Instalação: identificar perfil, preparar cartão, seguir o instalador.
7. Dúvidas e área do instalador.

## Interações

- Direcional, A e B no console: selecionar, abrir e voltar; START retorna à Home; SELECT abre Ajustes.
- Teclado quando o console está focado: setas, Enter/A, Escape/B; não capturar teclas fora da demonstração.
- Jogos ilustrativos: consultar detalhes e alternar favorito, com a lista de Favoritos sincronizada.
- Leitura: páginas de um texto original, tamanho e cor de fundo configuráveis.
- Vídeos: percorrer uma série e seus episódios ilustrativos. Não prometer reprodução real de arquivos nesta prévia.
- Aparência: aplicar os temas do sistema também à página.
- Ajustes: controles locais de demonstração, sem alterar o computador.
- Energia: suspender, despertar, reiniciar ou desligar somente a prévia, com confirmação.

## Conteúdo e distribuição

O site apresenta as funções do sistema e identifica a interface web como demonstração, não como emulador. Exemplos não acompanham a distribuição do sistema. Compatibilidade, desempenho e formatos não devem ser ampliados além do que o projeto comprova.

O instalador final ainda não foi fornecido. Manter release.json em preparing. A ativação exige URL HTTPS ou local de mesma origem, versão, nome, SHA-256 real e perfil aprovado. Não servir arquivos de laboratório como release pública.

## Entrega e verificação

Site estático, sem dependências de rede para fontes, interface ou artes. Verificar navegação entre menus, favoritos, leitura, temas, energia, links, ausência de mídia comercial e adaptação de largura. Preservar os fontes do sistema e trabalhar somente na pasta do site.
