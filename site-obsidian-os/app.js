(() => {
  'use strict';
  const $ = (selector, root = document) => root.querySelector(selector);
  const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
  const icon = name => `<svg aria-hidden="true"><use href="#${name}"/></svg>`;
  const themes = [
    { id: 'obsidian', name: 'Obsidian', image: 'obsidian-boot.png', color: '#c08a5e' },
    { id: 'floresta-viva', name: 'Floresta Viva', image: 'theme-forest.png', color: '#c4f36b' },
    { id: 'por-do-sol', name: 'Pôr do Sol', image: 'theme-sunset.png', color: '#75f7ca' },
    { id: 'cosmos-arcade', name: 'Cosmos Arcade', image: 'theme-cosmos.png', color: '#67e8f9' }
  ];
  const menus = {
    home: { label: 'Home', title: 'Tudo começa por aqui.', text: 'Uma entrada direta para o que você quer fazer. A Home reúne os caminhos do sistema em uma interface pensada para os controles do portátil.', items: ['Acesso às bibliotecas e à personalização', 'Navegação pelo direcional e pelos botões', 'Informações do sistema sempre à vista'], hint: 'Experimente: escolha um bloco na tela ou use o direcional e o botão A.' },
    games: { label: 'Jogos', title: 'A próxima partida está na sua biblioteca.', text: 'Encontre seus jogos em uma coleção visual e abra os detalhes antes de jogar. No aparelho, a execução depende do emulador e da compatibilidade do título.', items: ['Biblioteca organizada para navegar no console', 'Detalhes do título selecionado', 'Atalho para marcar seus favoritos'], hint: 'Abra um dos títulos ilustrativos e marque a estrela. Ele aparecerá em Favoritos.' },
    favorites: { label: 'Favoritos', title: 'Seus preferidos, sem procurar.', text: 'Separe os títulos para os quais você sempre volta. Os favoritos ficam reunidos em um lugar próprio, sem duplicar os arquivos da coleção.', items: ['Lista dedicada aos títulos marcados', 'Adicionar ou remover pela tela de detalhes', 'Acesso rápido à sua seleção pessoal'], hint: 'Esta lista acompanha as estrelas que você marca na demonstração.' },
    reading: { label: 'Leitura', title: 'Entre uma partida e outro capítulo.', text: 'Leve sua biblioteca com você. O leitor reúne livros e quadrinhos e permite adaptar a experiência à tela do portátil.', items: ['EPUB sem DRM, CBZ e pastas de imagens', 'Posição de leitura salva para continuar depois', 'Ajustes de fonte, tamanho e fundo'], hint: 'Abra o texto original da prévia: passe as páginas e experimente os fundos de leitura.' },
    videos: { label: 'Vídeos', title: 'Sua coleção também tem tela.', text: 'Navegue pelos vídeos do cartão e organize séries em pastas. A biblioteca facilita escolher o que assistir e percorrer os episódios.', items: ['Vídeos e séries organizados por pastas', 'Navegação entre episódios', 'Compatibilidade conforme formato e codec'], hint: 'Explore a série ilustrativa. A reprodução de arquivos pertence ao sistema no aparelho.' },
    themes: { label: 'Aparência', title: 'Uma interface com a sua energia.', text: 'Do brilho mineral da obsidiana aos cenários dos temas ilustrados, a aparência muda sem perder a identidade do sistema.', items: ['Quatro temas do projeto para explorar', 'Fundos, destaques e ícones coordenados', 'A mesma experiência em diferentes atmosferas'], hint: 'Escolha um tema aqui. As cores do console e desta página mudam juntas.' },
    settings: { label: 'Ajustes', title: 'Os detalhes sob seu controle.', text: 'Encontre os ajustes essenciais para o uso diário. Adapte som e brilho para a sua sessão e acompanhe as informações do aparelho.', items: ['Volume e modo silencioso', 'Brilho de tela ajustável', 'Estado de bateria e cartão no sistema'], hint: 'Os controles desta prévia alteram apenas a demonstração, sem mudar seu computador.' },
    power: { label: 'Energia', title: 'Toda jornada também tem uma pausa.', text: 'As ações de energia ficam reunidas em um menu próprio. Uma confirmação ajuda você a escolher com calma antes de sair.', items: ['Suspender e retomar a experiência', 'Reiniciar com a identidade de abertura', 'Desligar após confirmar a ação'], hint: 'Teste o reinício para ver a logo oficial na tela do console.' }
  };
  const menuKeys = Object.keys(menus);
  const homeKeys = ['games', 'reading', 'videos', 'favorites', 'themes', 'settings'];
  const games = [
    { id: 'horizonte', name: 'Horizonte', genre: 'Aventura', cover: 'sunset', text: 'Novos caminhos além da última montanha. Título ilustrativo criado para demonstrar a biblioteca.' },
    { id: 'orbita', name: 'Órbita', genre: 'Arcade', cover: 'cosmos', text: 'Uma viagem pelas cores do espaço. Título ilustrativo criado para demonstrar a biblioteca.' },
    { id: 'vale', name: 'Vale secreto', genre: 'Exploração', cover: 'forest', text: 'Uma trilha entre ruínas e cachoeiras. Título ilustrativo criado para demonstrar a biblioteca.' }
  ];
  const pages = [
    ['Um novo horizonte', 'Quando o vale acordou, a luz já atravessava as folhas. Lia guardou o pequeno mapa no bolso e escutou a água correndo por trás das pedras.', 'Havia uma trilha que ninguém da vila costumava seguir. Não era a mais curta, nem a mais fácil. Mas naquela manhã ela parecia levar exatamente ao lugar certo.'],
    ['A passagem', 'Depois da ponte, o caminho se abriu em duas direções. Lia escolheu a sombra das árvores. A cada passo, o som da vila ficava menor e o da floresta, maior.', 'Encontrou uma pedra escura com um veio da cor do pôr do sol. Quando a ergueu contra a luz, viu nela o reflexo de um céu que ainda não conhecia.'],
    ['O primeiro passo', 'Do alto, o vale já não parecia um limite. Era apenas uma parte do mundo, com seus rios, suas casas e suas pequenas histórias.', 'Lia abriu o mapa. Havia espaço de sobra nas margens. Sorriu, marcou o lugar onde estava e começou a desenhar a próxima trilha.']
  ];
  const episodes = ['A trilha', 'Além do vale', 'O caminho de volta'];
  const state = { menu: 'home', view: null, item: null, selected: 0, favorites: new Set(['horizonte']), page: 0, readerSize: 3.3, paper: 'sepia', theme: 'obsidian', volume: 70, brightness: 80, mute: false, power: 'on' };
  let toastTimer, bootTimer;
  try {
    const saved = JSON.parse(localStorage.getItem('obsidian-preview') || 'null');
    if (saved) {
      if (themes.some(t => t.id === saved.theme)) state.theme = saved.theme;
      if (Array.isArray(saved.favorites)) state.favorites = new Set(saved.favorites.filter(id => games.some(g => g.id === id)));
      if (Number.isInteger(saved.page)) state.page = Math.max(0, Math.min(2, saved.page));
    }
  } catch { /* Preview works without local storage. */ }
  function save() {
    try { localStorage.setItem('obsidian-preview', JSON.stringify({ theme: state.theme, favorites: [...state.favorites], page: state.page })); } catch { /* Optional persistence. */ }
  }
  function toast(message) {
    clearTimeout(toastTimer);
    $('#toast').textContent = message;
    $('#toast').classList.add('visible');
    toastTimer = setTimeout(() => $('#toast').classList.remove('visible'), 2600);
  }
  function action(label, name, value = '', className = 'ui-action') {
    return `<button class="${className}" data-action="${name}" data-value="${value}">${label}</button>`;
  }
  function openMenu(menu) {
    if (!menus[menu]) return;
    clearTimeout(bootTimer);
    Object.assign(state, { menu, view: null, item: null, selected: 0, power: 'on' });
    render();
  }
  function back() {
    if (state.power !== 'on') return wake();
    if (state.view === 'episode') state.view = 'series';
    else if (state.view) state.view = null;
    else state.menu = 'home';
    state.selected = 0;
    render();
  }
  function startBoot() {
    clearTimeout(bootTimer);
    Object.assign(state, { menu: 'home', view: null, selected: 0, power: 'boot' });
    render();
    bootTimer = setTimeout(() => { state.power = 'on'; render(); }, 1800);
  }
  function wake() { state.power === 'off' ? startBoot() : openMenu('home'); }
  function setTheme(theme) {
    if (!themes.some(t => t.id === theme)) return;
    state.theme = theme;
    save(); render();
    toast(`Tema ${themes.find(t => t.id === theme).name} aplicado`);
  }
  function renderCards(items) {
    if (!items.length) return `<div class="ui-empty"><p>A sua seleção começa com uma estrela.</p>${action('Explorar jogos', 'menu', 'games')}</div>`;
    return `<div class="ui-card-grid">${items.map(g => `<button class="ui-card" data-action="game" data-value="${g.id}"><span class="cover ${g.cover}"></span>${state.favorites.has(g.id) ? '<span class="favorite-star" aria-label="Favorito">★</span>' : ''}<strong>${g.name}</strong><small>${g.genre}</small></button>`).join('')}</div>`;
  }
  function reader() {
    const page = pages[state.page];
    return `<div class="reader" data-paper="${state.paper}" style="--reader-size:${state.readerSize}cqw"><div class="reader-head"><span>ALÉM DO HORIZONTE</span><div class="reader-tools"><button data-action="font" data-value="-1" aria-label="Diminuir texto">A−</button><button data-action="font" data-value="1" aria-label="Aumentar texto">A+</button><button data-action="paper" aria-label="Mudar fundo de leitura">◐</button></div></div><article class="reader-content"><h3>${page[0]}</h3><p>${page[1]}</p><p>${page[2]}</p></article><div class="reader-bottom"><button data-action="page" data-value="-1" aria-label="Página anterior" ${state.page === 0 ? 'disabled' : ''}>←</button><span>${state.page + 1} / ${pages.length} · texto original</span><button data-action="page" data-value="1" aria-label="Próxima página" ${state.page === pages.length - 1 ? 'disabled' : ''}>→</button></div></div>`;
  }
  function content() {
    if (state.view === 'game') {
      const g = games.find(game => game.id === state.item);
      return `<div class="ui-details"><div class="cover ${g.cover}"></div><div class="ui-detail-copy"><h3>${g.name}</h3><p>${g.text}</p>${action(state.favorites.has(g.id) ? '★ Remover favorito' : '☆ Adicionar favorito', 'favorite', g.id)}</div></div>`;
    }
    if (state.view === 'series') return `<h3 class="ui-title">Caminhos · Temporada 01</h3><div class="ui-list">${episodes.map((title, i) => action(`<span>0${i + 1} · ${title}</span><small>Episódio ilustrativo</small>`, 'episode', String(i), 'ui-row')).join('')}</div>`;
    if (state.view === 'episode') return `<div class="ui-details"><div class="cover forest"></div><div class="ui-detail-copy"><h3>${episodes[Number(state.item)]}</h3><p>Caminhos · episódio 0${Number(state.item) + 1}</p><p>Na versão instalada, você abre seus vídeos pelo cartão. Esta prévia demonstra a organização da biblioteca.</p>${action('Ver episódios', 'series')}</div></div>`;
    if (state.view === 'confirm') {
      const labels = { sleep: 'Suspender', restart: 'Reiniciar', off: 'Desligar' };
      return `<div class="ui-modal">${icon('power')}<h3>${labels[state.item]} a prévia?</h3><p>Esta ação afeta apenas o console demonstrativo da página.</p><div>${action('Cancelar', 'cancel', '', 'ui-action secondary')}${action('Confirmar', 'power-confirm', state.item)}</div></div>`;
    }
    switch (state.menu) {
      case 'home': return `<h3 class="ui-title">Seu mundo começa aqui.</h3><div class="ui-grid">${homeKeys.map(k => action(`${icon(k)}<span>${menus[k].label}</span><small>${({ games: 'Sua coleção', reading: 'Mais um capítulo', videos: 'Dê o play', favorites: 'Sempre por perto', themes: 'Seu estilo', settings: 'Seu controle' })[k]}</small>`, 'menu', k, 'ui-tile')).join('')}</div>`;
      case 'games': return `<h3 class="ui-title">Biblioteca <small>· 3 exemplos</small></h3>${renderCards(games)}`;
      case 'favorites': return `<h3 class="ui-title">Favoritos · ${state.favorites.size}</h3>${renderCards(games.filter(g => state.favorites.has(g.id)))}`;
      case 'reading': return `<h3 class="ui-title">Sua estante</h3><div class="ui-details"><div class="cover forest"></div><div class="ui-detail-copy"><h3>Além do horizonte</h3><p>Uma pequena jornada em três páginas. Texto original desta demonstração.</p>${action(state.page ? `Continuar · página ${state.page + 1}` : 'Começar a ler', 'read')}</div></div>`;
      case 'videos': return `<h3 class="ui-title">Vídeos & séries</h3><div class="ui-details"><div class="cover sunset"></div><div class="ui-detail-copy"><h3>Caminhos</h3><p>Temporada 01 · 3 episódios ilustrativos.</p>${action('Explorar episódios', 'series')}</div></div>`;
      case 'themes': return `<h3 class="ui-title">Escolha sua atmosfera</h3><div class="ui-themes">${themes.map(t => `<button class="ui-theme" data-theme="${t.id}" data-action="theme" data-value="${t.id}" aria-pressed="${state.theme === t.id}"><span>${t.name}</span></button>`).join('')}</div>`;
      case 'settings': return `<h3 class="ui-title">Do seu jeito</h3><div class="ui-settings"><label>Volume <output data-output="volume">${state.volume}%</output><input type="range" min="0" max="100" value="${state.volume}" data-setting="volume" aria-label="Volume da demonstração"></label><label>Brilho <output data-output="brightness">${state.brightness}%</output><input type="range" min="20" max="100" value="${state.brightness}" data-setting="brightness" aria-label="Brilho da demonstração"></label>${action(`Modo silencioso: ${state.mute ? 'ativado' : 'desativado'}`, 'mute')}<p>Ajustes locais da prévia. Nenhum áudio é reproduzido.</p></div>`;
      case 'power': return `<h3 class="ui-title">Energia</h3><div class="ui-list">${action('<span>Suspender</span><small>Uma pausa rápida</small>', 'power', 'sleep', 'ui-row')}${action('<span>Reiniciar</span><small>Um novo começo</small>', 'power', 'restart', 'ui-row')}${action('<span>Desligar</span><small>Até a próxima</small>', 'power', 'off', 'ui-row')}</div>`;
      default: return '';
    }
  }
  function screenMarkup() {
    if (state.power === 'boot') return '<div class="boot-screen"><img src="assets/obsidian-boot.png" alt="Inicialização Obsidian OS"></div>';
    if (state.power !== 'on') return `<div class="screen-off">${icon('power')}<p>${state.power === 'sleep' ? 'Uma pausa. Sua próxima história espera.' : 'Prévia desligada.'}</p>${action(state.power === 'sleep' ? 'Despertar' : 'Ligar', 'wake')}</div>`;
    if (state.view === 'reader') return reader();
    return `<div class="ui-shell"><div class="ui-top"><span class="ui-logo">${icon('cube')} OBSIDIAN OS</span><span class="ui-battery">PRÉVIA <i aria-hidden="true"></i></span></div><div class="ui-main">${content()}</div><div class="ui-foot"><span><b>A</b> selecionar &nbsp; <b>B</b> voltar</span><span>${menus[state.menu].label} / ${themes.find(t => t.id === state.theme).name}</span></div></div>`;
  }
  function navItems(screen) { return $$('button[data-action]:not(:disabled),input[data-setting]', screen); }
  function render() {
    const active = document.activeElement;
    const screen = active?.closest('.demo-screen');
    const focus = screen ? { id: screen.id, action: active.dataset.action, value: active.dataset.value, setting: active.dataset.setting } : null;
    document.documentElement.dataset.theme = state.theme;
    $$('[data-theme]', $('#theme-gallery')).forEach(el => el.setAttribute('aria-pressed', String(el.dataset.theme === state.theme)));
    $$('.menu-tab').forEach(el => { const selected = el.dataset.menu === state.menu; el.setAttribute('aria-selected', String(selected)); el.tabIndex = selected ? 0 : -1; });
    const menu = menus[state.menu];
    $('#menu-panel').setAttribute('aria-labelledby', `tab-${state.menu}`);
    $('#menu-description').innerHTML = `<span class="menu-number">0${menuKeys.indexOf(state.menu) + 1} / ${menu.label.toUpperCase()}</span><h3>${menu.title}</h3><p>${menu.text}</p><ul>${menu.items.map(item => `<li>${item}</li>`).join('')}</ul><p class="menu-hint">${menu.hint}</p>`;
    const markup = screenMarkup();
    $$('.demo-screen').forEach(el => {
      el.innerHTML = markup;
      const items = navItems(el);
      state.selected = Math.max(0, Math.min(state.selected, items.length - 1));
      items.forEach((item, index) => { item.dataset.index = index; item.classList.toggle('selected', index === state.selected); });
      el.style.filter = `brightness(${.65 + state.brightness * .0035})`;
    });
    if (focus) {
      const root = document.getElementById(focus.id);
      const match = navItems(root).find(el => focus.setting ? el.dataset.setting === focus.setting : el.dataset.action === focus.action && el.dataset.value === focus.value);
      (match || (focus.id === 'hero-screen' ? $('.console-unit') : root)).focus({ preventScroll: true });
    }
  }
  function handleAction(name, value) {
    switch (name) {
      case 'menu': return openMenu(value);
      case 'game': state.view = 'game'; state.item = value; state.selected = 0; break;
      case 'favorite': state.favorites.has(value) ? state.favorites.delete(value) : state.favorites.add(value); save(); toast(state.favorites.has(value) ? 'Adicionado aos favoritos' : 'Removido dos favoritos'); break;
      case 'read': state.view = 'reader'; state.selected = 0; break;
      case 'page': state.page = Math.max(0, Math.min(pages.length - 1, state.page + Number(value))); save(); break;
      case 'font': state.readerSize = Math.max(2.8, Math.min(4.7, state.readerSize + Number(value) * .3)); break;
      case 'paper': state.paper = ({ sepia: 'dark', dark: 'light', light: 'sepia' })[state.paper]; break;
      case 'series': state.view = 'series'; state.selected = 0; break;
      case 'episode': state.view = 'episode'; state.item = value; state.selected = 0; break;
      case 'theme': return setTheme(value);
      case 'mute': state.mute = !state.mute; break;
      case 'power': state.view = 'confirm'; state.item = value; state.selected = 0; break;
      case 'cancel': return back();
      case 'power-confirm': if (value === 'restart') return startBoot(); state.power = value; state.view = null; break;
      case 'wake': return wake();
      default: return;
    }
    render();
  }
  function adjust(setting, value) {
    state[setting] = Math.max(setting === 'brightness' ? 20 : 0, Math.min(100, value));
    $$(`[data-setting="${setting}"]`).forEach(el => el.value = state[setting]);
    $$(`[data-output="${setting}"]`).forEach(el => el.textContent = `${state[setting]}%`);
    $$('.demo-screen').forEach(el => el.style.filter = `brightness(${.65 + state.brightness * .0035})`);
  }
  function control(key, origin) {
    if (key === 'b') return back();
    if (state.power !== 'on') { if (key === 'a' || key === 'start') wake(); return; }
    const shortcuts = { start: 'home', select: 'settings', x: 'themes', y: 'favorites', fn: 'power' };
    if (shortcuts[key]) return openMenu(shortcuts[key]);
    const items = navItems($('#expanded-screen'));
    if (!items.length) return;
    if (key === 'a') { const item = items[state.selected]; if (item.dataset.setting) adjust(item.dataset.setting, Number(item.value) + 5); else handleAction(item.dataset.action, item.dataset.value); return; }
    if (!['up', 'down', 'left', 'right'].includes(key)) return;
    if (state.view === 'reader' && (key === 'left' || key === 'right')) return handleAction('page', key === 'right' ? '1' : '-1');
    const item = items[state.selected];
    if (item.dataset.setting && (key === 'left' || key === 'right')) return adjust(item.dataset.setting, Number(item.value) + (key === 'right' ? 5 : -5));
    const columns = state.view ? 1 : state.menu === 'home' || state.menu === 'games' || state.menu === 'favorites' ? 3 : state.menu === 'themes' ? 2 : 1;
    const step = ({ up: -columns, down: columns, left: -1, right: 1 })[key];
    state.selected = (state.selected + step + items.length) % items.length;
    const target = origin?.closest('.demo-screen') || $('#hero-screen');
    render();
    if (origin?.matches('button[data-action]')) navItems(target)[state.selected]?.focus({ preventScroll: true });
    navItems(target)[state.selected]?.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'instant' });
  }
  $('#menu-tabs').innerHTML = menuKeys.map(key => `<button class="menu-tab" id="tab-${key}" role="tab" aria-controls="menu-panel" aria-selected="${key === 'home'}" tabindex="${key === 'home' ? 0 : -1}" data-menu="${key}">${icon(key)}${menus[key].label}</button>`).join('');
  $('#theme-gallery').innerHTML = themes.map(t => `<button class="theme-card" data-theme="${t.id}" aria-pressed="${state.theme === t.id}" style="--swatch:${t.color}"><img src="assets/${t.image}" alt="Arte do tema ${t.name}" loading="lazy"><span>${t.name}<i aria-hidden="true"></i></span></button>`).join('');
  document.addEventListener('click', event => {
    const tab = event.target.closest('[data-menu]');
    if (tab) return openMenu(tab.dataset.menu);
    const theme = event.target.closest('.theme-card');
    if (theme) return setTheme(theme.dataset.theme);
    const button = event.target.closest('[data-action]');
    if (button && !button.disabled) return handleAction(button.dataset.action, button.dataset.value);
    const physical = event.target.closest('[data-control]');
    if (physical) control(physical.dataset.control, physical);
  });
  document.addEventListener('input', event => { const setting = event.target.dataset.setting; if (setting === 'brightness' || setting === 'volume') adjust(setting, Number(event.target.value)); });
  document.addEventListener('keydown', event => {
    const tab = event.target.closest('[role="tab"]');
    if (tab && ['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
      event.preventDefault();
      let index = menuKeys.indexOf(tab.dataset.menu);
      index = event.key === 'Home' ? 0 : event.key === 'End' ? menuKeys.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + menuKeys.length) % menuKeys.length;
      openMenu(menuKeys[index]); $(`#tab-${menuKeys[index]}`).focus({ preventScroll: true });
      $(`#tab-${menuKeys[index]}`).scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'instant' });
      return;
    }
    if (!event.target.closest('.console-unit,.expanded') || event.target.matches('input')) return;
    if (event.key === 'Enter' && event.target.matches('button')) return;
    const key = ({ ArrowUp: 'up', ArrowDown: 'down', ArrowLeft: 'left', ArrowRight: 'right', Enter: 'a', Escape: 'b', a: 'a', b: 'b', x: 'x', y: 'y' })[event.key];
    if (key) {
      event.preventDefault();
      if (event.target.dataset.index !== undefined) state.selected = Number(event.target.dataset.index);
      control(key, event.target);
    }
  });
  $('#replay-boot').addEventListener('click', () => {
    $('#experiencia').scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' });
    startBoot();
  });
  render();

  $('#demo-view-toggle').addEventListener('click', event => {
    const enabled = $('.big-display-wrap').classList.toggle('console-mode');
    event.currentTarget.setAttribute('aria-pressed', String(enabled));
    event.currentTarget.textContent = enabled ? 'Usar a tela ampliada ↗' : 'Experimentar no console ↗';
  });

  // A public download is enabled only with complete release metadata.
  async function loadRelease() {
    try {
      const response = await fetch('release.json', { cache: 'no-store' });
      if (!response.ok) return;
      const release = await response.json();
      const installer = release.installer || {};
      const profile = Array.isArray(release.supportProfile) ? release.supportProfile.filter(v => typeof v === 'string' && v.trim()).join(' · ') : release.supportProfile;
      if (release.status !== 'available' || typeof release.version !== 'string' || !release.version.trim() || typeof profile !== 'string' || !profile.trim() || typeof installer.fileName !== 'string' || !installer.fileName.trim() || typeof installer.url !== 'string' || !installer.url.trim() || !/^[a-f\d]{64}$/i.test(installer.sha256 || '')) return;
      const url = new URL(installer.url, location.href);
      if (url.username || url.password || !(url.protocol === 'https:' || (url.protocol === 'http:' && url.origin === location.origin))) return;
      const link = $('#download-action');
      link.href = 'apoie.html';

      link.removeAttribute('aria-disabled');
      link.classList.remove('is-disabled');
      link.textContent = 'Ir para download ↗';
      $('#release-eyebrow').textContent = 'SUA PRÓXIMA FASE COMEÇA AQUI';
      $('#release-title').textContent = `Obsidian OS ${release.version}`;
      $('#release-description').textContent = `Perfis compatíveis: ${profile}`;
      $('#release-meta').textContent = `${installer.platform || 'Windows'} · ${installer.fileName}`;
      $('#release-footnote').textContent = `SHA-256: ${installer.sha256}`;
      $('#faq-availability').textContent = `Sim. A versão ${release.version} está disponível nesta página para os perfis: ${profile}.`;
    } catch { /* Keep the preparing state if release metadata cannot be read. */ }
  }
  loadRelease();
})();
