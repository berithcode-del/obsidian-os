module.exports = async function handler(req, res) {
  res.setHeader('Cache-Control', 'no-store');
  if (req.method !== 'POST') return res.status(405).json({ paid: false });
  const body = req.body || {}; const fields = ['order_nsu', 'transaction_nsu', 'slug'];
  if (!fields.every(k => typeof body[k] === 'string' && /^[a-zA-Z0-9_-]{1,200}$/.test(body[k]))) return res.status(400).json({ paid: false });
  try {
    const response = await fetch('https://api.checkout.infinitepay.io/payment_check', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ handle: 'marco-antonio-9792', order_nsu: body.order_nsu, transaction_nsu: body.transaction_nsu, slug: body.slug }), signal: AbortSignal.timeout(15000) });
    const data = await response.json();
    return res.status(response.ok ? 200 : 502).json({ paid: response.ok && data.success === true && data.paid === true });
  } catch { return res.status(502).json({ paid: false }); }
};
