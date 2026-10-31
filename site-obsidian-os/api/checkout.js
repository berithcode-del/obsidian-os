const { randomUUID } = require('node:crypto');
module.exports = async function handler(req, res) {
  res.setHeader('Cache-Control', 'no-store');
  if (req.method !== 'POST') return res.status(405).json({ error: 'Método não permitido.' });
  const amount = req.body && req.body.amount;
  if (!Number.isInteger(amount) || amount < 990 || amount > 99990) return res.status(400).json({ error: 'Escolha um valor entre R$ 9,90 e R$ 999,90.' });
  try {
    const response = await fetch('https://api.checkout.infinitepay.io/links', {
      method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ handle: 'marco-antonio-9792', items: [{ quantity: 1, price: amount, description: 'Apoio voluntário ao projeto Obsidian OS' }], order_nsu: randomUUID(), redirect_url: 'https://obsidian-os-theta.vercel.app/baixar.html' }),
      signal: AbortSignal.timeout(15000)
    });
    const data = await response.json().catch(() => ({}));
    let destination; try { destination = new URL(data.url); } catch {}
    if (!response.ok || !destination || destination.protocol !== 'https:' || !(destination.hostname === 'infinitepay.io' || destination.hostname.endsWith('.infinitepay.io'))) {
      console.error('InfinitePay status:', response.status);
      return res.status(502).json({ error: 'Não foi possível abrir o pagamento. Tente novamente mais tarde.' });
    }
    return res.status(200).json({ url: destination.href });
  } catch (error) { console.error('Checkout request failed:', error.message); return res.status(502).json({ error: 'Não foi possível abrir o pagamento. Tente novamente mais tarde.' }); }
};
