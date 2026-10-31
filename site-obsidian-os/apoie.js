const buttons = [...document.querySelectorAll('[data-value]')];
const custom = document.querySelector('#custom');
const help = document.querySelector('#amount-help');
const checkout = document.querySelector('.checkout');
const status = document.querySelector('.payment-note');
const checkoutEnabled = true;
let selected = 4990;
function select(button) {
  buttons.forEach(item => { const active = item === button; item.classList.toggle('selected', active); item.setAttribute('aria-pressed', String(active)); });
}
function update() { checkout.disabled = !checkoutEnabled || selected === null; }
buttons.forEach(button => { button.setAttribute('aria-pressed', String(button.classList.contains('selected'))); button.addEventListener('click', () => { select(button); selected = Math.round(Number(button.dataset.value) * 100); custom.value = ''; help.textContent = 'Escolha entre R$ 9,90 e R$ 999,90.'; update(); }); });
custom.addEventListener('input', () => {
  select(null);
  const raw = custom.value.trim().replace(/\s/g, '').replace(',', '.');
  const value = Number(raw);
  selected = /^\d+(\.\d{1,2})?$/.test(raw) && value >= 9.9 && value <= 999.9 ? Math.round(value * 100) : null;
  help.textContent = raw && selected === null ? 'Digite um valor válido entre R$ 9,90 e R$ 999,90.' : 'Escolha entre R$ 9,90 e R$ 999,90.';
  update();
});
checkout.addEventListener('click', async () => {
  if (selected === null) return;
  checkout.disabled = true; status.textContent = 'Preparando seu pagamento na InfinitePay...';
  try {
    const response = await fetch('/api/checkout', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ amount: selected }) });
    const data = await response.json();
    if (!response.ok || !data.url) throw new Error(data.error || 'Não foi possível abrir o pagamento.');
    const destination = new URL(data.url);
    if (destination.protocol !== 'https:' || !(destination.hostname === 'infinitepay.io' || destination.hostname.endsWith('.infinitepay.io'))) throw new Error('Não foi possível verificar o endereço de pagamento.');
    location.assign(destination.href);
  } catch (error) { status.textContent = error.message || 'Não foi possível abrir o pagamento. Tente novamente.'; update(); }
});



fetch('release.json', { cache: 'no-store' }).then(r => r.json()).then(release => { const i = release.installer || {}; const profile = Array.isArray(release.supportProfile) ? release.supportProfile.some(x => typeof x === 'string' && x.trim()) : typeof release.supportProfile === 'string' && release.supportProfile.trim(); let url; try { url = new URL(i.url); } catch {} if (release.status === 'available' && typeof release.version === 'string' && release.version.trim() && profile && typeof i.fileName === 'string' && i.fileName.trim() && /^[a-f\d]{64}$/i.test(i.sha256 || '') && url?.protocol === 'https:') document.querySelector('#release-warning').hidden = true; }).catch(() => {});

