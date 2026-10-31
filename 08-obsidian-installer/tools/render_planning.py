"""Create a standalone, printable reading copy of the planning document."""
from html import escape
from pathlib import Path

import markdown

ROOT = Path(__file__).resolve().parents[1]


def main():
    source = ROOT / "PLANEJAMENTO-APLICACAO.md"
    renderer = markdown.Markdown(extensions=["extra", "toc", "sane_lists"], extension_configs={"toc": {"toc_depth": "2-3"}})
    body = renderer.convert(source.read_text(encoding="utf-8-sig"))
    page = """<!doctype html>
<html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Obsidian Installer — planejamento da aplicação</title>
<style>
:root { color-scheme: light; --ink:#202932; --muted:#53616b; --line:#d8dfe3; --accent:#245c59; }
* { box-sizing:border-box; } body { margin:0; color:var(--ink); background:#edf0f1; font:17px/1.7 system-ui,Segoe UI,sans-serif; }
.page { max-width:1060px; margin:36px auto; padding:60px 76px; background:white; border-top:7px solid var(--accent); }
.meta { color:var(--muted); font-size:13px; letter-spacing:.04em; margin:0 0 30px; }
h1 { font-size:38px; line-height:1.2; letter-spacing:-.03em; margin:0 0 24px; }
h2 { font-size:25px; line-height:1.35; margin:48px 0 18px; padding-top:12px; border-top:1px solid var(--line); }
h3 { font-size:20px; line-height:1.4; margin-top:30px; }
a { color:var(--accent); text-underline-offset:3px; overflow-wrap:anywhere; }
p,li { overflow-wrap:anywhere; } li { margin:7px 0; } ul,ol { padding-left:25px; }
table { border-collapse:collapse; width:100%; margin:24px 0; font-size:14px; line-height:1.55; }
th,td { padding:10px 12px; border:1px solid var(--line); vertical-align:top; text-align:left; }
th { background:#f0f5f4; } code { font-size:.88em; background:#f1f3f4; padding:2px 4px; border-radius:3px; }
pre { padding:18px; background:#f1f3f4; overflow:auto; } pre code { padding:0; }
blockquote { margin:24px 0; padding:4px 20px; border-left:3px solid var(--accent); color:var(--muted); }
nav { margin:28px 0 38px; padding:20px 24px; border:1px solid var(--line); font-size:14px; }
nav ul { margin:5px 0; } footer { font-size:13px; color:var(--muted); margin-top:50px; border-top:1px solid var(--line); padding-top:20px; }
@media(max-width:700px) { .page { margin:0; padding:30px 22px; } h1 { font-size:30px; } table { display:block; overflow:auto; } }
@media print { body { background:white; font-size:10.5pt; line-height:1.5; } .page { margin:0; padding:0; max-width:none; border-top:0; } nav { break-inside:avoid; } h2,h3 { break-after:avoid; } tr { break-inside:avoid; } h2 { margin-top:24pt; } a { color:inherit; } @page { size:A4; margin:18mm; } }
</style></head><body><main class="page">
<p class="meta">OBSIDIAN INSTALLER · PLANEJAMENTO · 29 OUT 2026</p>
<nav aria-label="Conteúdo"><strong>Conteúdo do planejamento</strong>__TOC__</nav>
__BODY__
<footer>Cópia de leitura gerada de __SOURCE__. A prévia Windows compilou localmente; o pacote para clientes e a homologação comercial ainda estão pendentes.</footer>
</main></body></html>"""
    page = page.replace("__TOC__", renderer.toc).replace("__BODY__", body).replace("__SOURCE__", escape(source.name))
    target = source.with_suffix(".html")
    target.write_text(page, encoding="utf-8")
    print(f"Documento gerado: {target}")


if __name__ == "__main__":
    main()
