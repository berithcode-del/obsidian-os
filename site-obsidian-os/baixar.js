(async () => {
  const params = new URLSearchParams(location.search);
  const fields = ['order_nsu', 'transaction_nsu', 'slug'];
  const payment = Object.fromEntries(fields.map(k => [k, params.get(k)]));
  if (fields.every(k => payment[k])) {
    history.replaceState(null, '', location.pathname);
    const message = document.querySelector('#payment-message');
    message.hidden = false;
    message.textContent = 'Conferindo sua contribuição...';
    try {
      const response = await fetch('/api/verificar-pagamento', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(payment) });
      const result = await response.json();
      message.textContent = response.ok && result.paid ? 'Pagamento confirmado. Obrigado por fortalecer o projeto! 🧡' : 'Não foi possível confirmar o pagamento agora. Você pode continuar para o download gratuito.';
    } catch { message.textContent = 'Não foi possível confirmar o pagamento agora. Você pode continuar para o download gratuito.'; }
  }
  try {
    const response = await fetch('release.json', { cache: 'no-store' });
    const release = await response.json(); const installer = release.installer || {};
    const profile = Array.isArray(release.supportProfile) ? release.supportProfile.filter(x => typeof x === 'string' && x.trim()).join(' · ') : release.supportProfile;
    if (release.status !== 'available' || typeof release.version !== 'string' || !release.version.trim() || typeof profile !== 'string' || !profile.trim() || typeof installer.fileName !== 'string' || !installer.fileName.trim() || !/^[a-f\d]{64}$/i.test(installer.sha256 || '')) return;
    const url = new URL(installer.url); if (url.protocol !== 'https:') return;
    const link = document.querySelector('#installer'); link.href = url.href; link.removeAttribute('aria-disabled'); link.textContent = 'Baixar instalador';
    document.querySelector('#download-description').textContent = `Obsidian OS ${release.version} · versão de testes. Compatibilidade: ${profile}.`;
    document.querySelector('#release-details').textContent = `${installer.platform || 'Windows'} · ${installer.fileName} · SHA-256: ${installer.sha256}`;
  } catch { /* Keep the download in preparation until release metadata is complete. */ }
})();
