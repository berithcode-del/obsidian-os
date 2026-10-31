# Landing page do Obsidian OS

Landing page estática em português. O planejamento atualizado está em `PLANEJAMENTO.md`. A hero usa uma imagem cinematográfica completa do console branco se libertando da obsidiana. No explorador, o console desenhado em CSS e a tela ampliada oferecem a interface navegável.

## Abrir

Sirva esta pasta com um servidor HTTP estático (por exemplo, `python -m http.server 8765 --bind 127.0.0.1 --directory site-obsidian-os`, a partir da raiz do projeto). Abra `http://127.0.0.1:8765/`. Não há framework, compilação ou fonte remota. A leitura de release.json requer HTTP.

## Demonstração

Home, Jogos, Favoritos, Leitura, Vídeos, Aparência, Ajustes e Energia têm abas, explicações e navegação. Os botões do console funcionam com clique/toque; setas, Enter/A e Escape/B funcionam com foco na demonstração. START volta à Home, SELECT abre Ajustes, X abre Aparência, Y abre Favoritos e FN abre Energia.

Os quatro temas vêm do projeto e mudam a página e as duas telas juntas. Favoritos, tema e posição de leitura ficam salvos localmente quando o navegador permite. O conteúdo de biblioteca é original e ilustrativo: não há execução de jogos ou reprodução de vídeos no site. Som, brilho e energia afetam somente a demonstração.

## Ativar o download público

Edite release.json somente quando a versão pública estiver pronta para distribuição. Campos necessários:

- status: available
- version: a versão liberada
- installer.url: a URL HTTPS do instalador
- installer.fileName: o nome do arquivo
- installer.sha256: o SHA-256 real do arquivo, com 64 caracteres hexadecimais
- supportProfile: a lista de aparelhos e variantes realmente homologados
- installer.platform: versão do Windows compatível

Enquanto status continuar como preparing, o botão permanece fechado. A imagem de laboratório com customer_installation_allowed igual a false e o executável sem assinatura não devem ser indicados como pacote público. Antes de liberar, confira a URL, o hash medido do arquivo publicado e os perfis realmente homologados.

## Conteúdo

- index.html: apresentação e metadados do site
- styles.css: identidade visual responsiva e acessível
- app.js: demonstração compartilhada, navegação acessível e download configurável
- release.json: estado da publicação e metadados do instalador
- assets: logo oficial de abertura, fonte Sora do projeto, artes dos temas e rocha gerada para a composição
- qa/check_site.py: verificação em navegador de menus, favoritos, leitor, temas, energia, teclado e larguras responsivas

## Verificar

Com o servidor local aberto, execute `node --check site-obsidian-os/app.js` e `python site-obsidian-os/qa/check_site.py` a partir da raiz. O teste de navegador usa Playwright para Python e Chrome local em modo headless. Ele salva capturas em `qa/`; essa pasta não precisa ser publicada. Para publicação, use index.html, styles.css, hero.css, app.js, favicon.svg, release.json e assets/.

O sistema não distribui jogos, saves, livros ou vídeos.

A hero atual usa uma fotografia gerada completa em assets/hero-liberte-console.png. O desenho interativo foi mantido no explorador, alternável com a tela ampliada. hero.css contém os enquadramentos responsivos.

## Página de apoio e InfinitePay

A página `apoie.html` usa o Checkout Integrado da InfinitePay para gerar uma cobrança avulsa com o valor selecionado. Para executar o site com a integração local, use `node site-obsidian-os/servidor.js` na raiz e abra `http://127.0.0.1:8766/apoie.html`. O servidor estático descrito acima não fornece o endpoint de pagamento. A integração usa a InfiniteTag da conta, sem API Key. Valores são validados entre R$ 9,90 e R$ 999,90 no servidor. Gerar o link não significa que um pagamento foi realizado. A publicação exige hospedagem que execute o endpoint de servidor; copiar somente os arquivos estáticos não ativa o checkout.
