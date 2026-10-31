const http = require('node:http');
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');

const root = __dirname;
const port = Number(process.env.PORT || 8766);
const handle = 'marco-antonio-9792';
const types = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.json': 'application/json; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png', '.ttf': 'font/ttf' };
function json(res, status, body) { res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' }); res.end(JSON.stringify(body)); }
http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname === '/api/checkout' && req.method === 'POST') {
      let body = '';
      for await (const chunk of req) { body += chunk; if (body.length > 1024) return json(res, 413, { error: 'Solicitação muito grande.' }); }
      let amount; try { amount = JSON.parse(body).amount; } catch { return json(res, 400, { error: 'Solicitação inválida.' }); }
      if (!Number.isInteger(amount) || amount < 990 || amount > 99990) return json(res, 400, { error: 'Escolha um valor entre R$ 9,90 e R$ 999,90.' });
      const response = await fetch('https://api.checkout.infinitepay.io/links', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ handle, items: [{ quantity: 1, price: amount, description: 'Apoio voluntário ao projeto Obsidian OS' }], order_nsu: crypto.randomUUID() }), signal: AbortSignal.timeout(15000) });
      const data = await response.json().catch(() => ({}));
      let checkout; try { checkout = new URL(data.url); } catch {}
      if (!response.ok || !checkout || checkout.protocol !== 'https:' || !(checkout.hostname === 'infinitepay.io' || checkout.hostname.endsWith('.infinitepay.io'))) { console.error('InfinitePay:', response.status, JSON.stringify(data)); return json(res, 502, { error: 'Não foi possível abrir o pagamento. Tente novamente mais tarde.' }); }
      return json(res, 200, { url: checkout.href });
    }
    if (req.method !== 'GET' && req.method !== 'HEAD') return json(res, 405, { error: 'Método não permitido.' });
    let pathname; try { pathname = decodeURIComponent(url.pathname); } catch { return json(res, 400, { error: 'Caminho inválido.' }); }
    if (pathname === '/') pathname = '/index.html';
    const filename = path.resolve(root, '.' + pathname);
    if (!filename.startsWith(root + path.sep) || !types[path.extname(filename).toLowerCase()] || filename.endsWith('servidor.js')) return json(res, 404, { error: 'Página não encontrada.' });
    const file = await fs.readFile(filename); res.writeHead(200, { 'content-type': types[path.extname(filename).toLowerCase()], 'x-content-type-options': 'nosniff' }); res.end(req.method === 'HEAD' ? undefined : file);
  } catch (error) { if (error.code === 'ENOENT' || error.code === 'EISDIR') return json(res, 404, { error: 'Página não encontrada.' }); console.error(error); json(res, 500, { error: 'Erro ao processar a solicitação.' }); }
}).listen(port, '127.0.0.1', () => console.log(`Obsidian OS: http://127.0.0.1:${port}/apoie.html`));

