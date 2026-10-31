"""Refresh the review images without changing the preview state."""
from pathlib import Path
from playwright.sync_api import sync_playwright

out = Path(__file__).parent
with sync_playwright() as p:
    browser = p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe', headless=True)
    page = browser.new_page(viewport={'width': 1440, 'height': 1000})
    page.goto('http://127.0.0.1:8765/', wait_until='networkidle')
    page.evaluate('''Promise.all([...document.images].map(image => {
        image.loading = 'eager'; return image.decode().catch(() => {});
    }))''')
    page.screenshot(path=str(out / 'desktop.png'), full_page=True)
    page.locator('.hero').screenshot(path=str(out / 'hero.png'))
    page.locator('#temas').screenshot(path=str(out / 'themes.png'))
    page.set_viewport_size({'width': 390, 'height': 844})
    page.locator('.hero').screenshot(path=str(out / 'mobile-hero.png'))
    page.screenshot(path=str(out / 'mobile.png'), full_page=True)
    for width in [320, 390, 768, 1024, 1440]:
        page.set_viewport_size({'width': width, 'height': 900})
        assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
        assert page.locator('.hero-art').is_visible()
    print('Visuals refreshed; responsive checks passed.')
    browser.close()
