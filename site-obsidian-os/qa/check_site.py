"""Browser checks for the static preview. Run with a local server on port 8765."""
from pathlib import Path
import json
from playwright.sync_api import sync_playwright

OUT = Path(__file__).parent
URL = 'http://127.0.0.1:8765/'

with sync_playwright() as p:
    browser = p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe', headless=True)
    page = browser.new_page(viewport={'width': 1440, 'height': 1000}, device_scale_factor=1)
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.goto(URL, wait_until='networkidle')
    page.evaluate('''Promise.all([...document.images].map(image => {
        image.loading = 'eager'; return image.decode().catch(() => {});
    }))''')
    page.screenshot(path=str(OUT / 'desktop.png'), full_page=True)
    page.locator('.hero').screenshot(path=str(OUT / 'hero.png'))
    assert page.locator('#hero-screen .ui-tile').count() == 6
    release_meta = json.loads((Path(__file__).parent.parent / 'release.json').read_text(encoding='utf-8'))
    href = page.locator('#download-action').get_attribute('href')
    if release_meta.get('status') == 'available':
        assert href and href.endswith('apoie.html'), href
        assert release_meta['version'] in page.locator('#release-title').inner_text()
    else:
        assert href is None
    assert page.locator('[role=tab]').count() == 8
    for menu in ['home', 'games', 'favorites', 'reading', 'videos', 'themes', 'settings', 'power']:
        page.locator('#tab-' + menu).click()
        assert page.locator('#tab-' + menu).get_attribute('aria-selected') == 'true'
        assert page.locator('#menu-description h3').inner_text().strip()
        assert page.locator('#expanded-screen').text_content() == page.locator('#hero-screen').text_content()
    page.locator('#tab-games').click()
    page.locator('#expanded-screen [data-action=game][data-value=orbita]').click()
    page.locator('#expanded-screen [data-action=favorite]').click()
    page.locator('#tab-favorites').click()
    assert page.locator('#expanded-screen .ui-card').count() == 2
    page.locator('#expanded-screen [data-action=game][data-value=orbita]').click()
    page.locator('#expanded-screen [data-action=favorite]').click()
    page.locator('#tab-favorites').click()
    assert page.locator('#expanded-screen .ui-card').count() == 1
    page.locator('#tab-reading').click()
    page.locator('#expanded-screen [data-action=read]').click()
    page.locator('#expanded-screen [data-action=page][data-value="1"]').click()
    assert '2 / 3' in page.locator('#expanded-screen .reader-bottom').inner_text()
    page.locator('#expanded-screen [data-action=paper]').click()
    assert page.locator('#hero-screen .reader').get_attribute('data-paper') == 'dark'
    page.locator('#expanded-screen [data-action=font][data-value="1"]').click()
    page.locator('#tab-videos').click()
    page.locator('#expanded-screen [data-action=series]').click()
    assert page.locator('#expanded-screen [data-action=episode]').count() == 3
    page.locator('#expanded-screen [data-action=episode][data-value="1"]').click()
    assert 'Além do vale' in page.locator('#expanded-screen').inner_text()
    for theme in ['floresta-viva', 'por-do-sol', 'cosmos-arcade', 'obsidian']:
        page.locator(f'.theme-card[data-theme="{theme}"]').click()
        assert page.locator('html').get_attribute('data-theme') == theme
        assert page.locator(f'.theme-card[data-theme="{theme}"]').get_attribute('aria-pressed') == 'true'
    page.locator('#tab-settings').click()
    page.locator('#expanded-screen input[data-setting=volume]').fill('25')
    assert page.locator('#hero-screen [data-output=volume]').inner_text() == '25%'
    page.locator('#expanded-screen [data-action=mute]').click()
    assert 'ativado' in page.locator('#expanded-screen [data-action=mute]').inner_text()
    page.locator('#tab-power').click()
    page.locator('#expanded-screen [data-action=power][data-value=sleep]').click()
    page.locator('#expanded-screen [data-action=cancel]').click()
    assert page.locator('#expanded-screen [data-action=power]').count() == 3
    page.locator('#expanded-screen [data-action=power][data-value=sleep]').click()
    page.locator('#expanded-screen [data-action=power-confirm]').click()
    assert page.locator('#hero-screen .screen-off').count() == 1
    page.locator('#expanded-screen [data-action=wake]').click()
    assert page.locator('#hero-screen .ui-tile').count() == 6
    page.locator('#tab-power').click()
    page.locator('#expanded-screen [data-action=power][data-value=restart]').click()
    page.locator('#expanded-screen [data-action=power-confirm]').click()
    assert page.locator('#hero-screen .boot-screen').count() == 1
    page.wait_for_timeout(2000)
    assert page.locator('#hero-screen .ui-tile').count() == 6
    page.locator('#demo-view-toggle').click()
    page.locator('[data-control=x]').click()
    assert page.locator('#tab-themes').get_attribute('aria-selected') == 'true'
    page.locator('[data-control=y]').click()
    assert page.locator('#tab-favorites').get_attribute('aria-selected') == 'true'
    page.locator('[data-control=select]').click()
    assert page.locator('#tab-settings').get_attribute('aria-selected') == 'true'
    page.locator('[data-control=fn]').click()
    page.locator('#hero-screen [data-action=power][data-value=off]').click()
    page.locator('#hero-screen [data-action=power-confirm]').click()
    assert 'desligada' in page.locator('#hero-screen').inner_text()
    page.locator('#hero-screen [data-action=wake]').click()
    page.wait_for_timeout(2000)
    page.locator('.console-unit [data-control=start]').click()
    page.locator('.console-unit').focus()
    page.keyboard.press('ArrowRight')
    page.keyboard.press('Enter')
    assert page.locator('#tab-reading').get_attribute('aria-selected') == 'true'
    page.keyboard.press('Escape')
    assert page.locator('#tab-home').get_attribute('aria-selected') == 'true'
    page.locator('.header .brand').focus()
    page.keyboard.press('ArrowRight')
    assert page.locator('#hero-screen .selected').get_attribute('data-value') == 'games'
    page.locator('#tab-home').focus()
    page.keyboard.press('ArrowRight')
    assert page.locator('#tab-games').get_attribute('aria-selected') == 'true'
    page.locator('#tab-home').click()
    page.locator('#demo-view-toggle').click()
    page.locator('#expanded-screen [data-action=menu][data-value=games]').focus()
    page.keyboard.press('ArrowRight')
    page.keyboard.press('Enter')
    assert page.locator('#tab-reading').get_attribute('aria-selected') == 'true'
    page.locator('#tab-home').click()
    page.locator('#experiencia').screenshot(path=str(OUT / 'explorer.png'))
    for width in [320, 390, 768, 1024, 1440]:
        page.set_viewport_size({'width': width, 'height': 900})
        page.evaluate('window.scrollTo(0,0)')
        assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), f'Overflow at {width}'
        assert page.locator('.hero-art').is_visible()
        if width == 390:
            page.screenshot(path=str(OUT / 'mobile.png'), full_page=True)
            page.locator('.hero').screenshot(path=str(OUT / 'mobile-hero.png'))
    assert not errors, errors
    # Only intercepted metadata changes: no files or actual release state are changed.
    release = {'status': 'available', 'version': '1.0-test', 'supportProfile': ['Perfil de teste'], 'installer': {'url': 'https://example.test/obsidian.exe', 'fileName': 'obsidian.exe', 'sha256': 'a' * 64}}
    page.route('**/release.json', lambda route: route.fulfill(json=release))
    page.reload(wait_until='networkidle')
    assert page.locator('#download-action').get_attribute('href').endswith('apoie.html')
    assert 'Perfil de teste' in page.locator('#release-description').inner_text()
    release['installer']['url'] = 'javascript:alert(1)'
    page.reload(wait_until='networkidle')
    assert 'Perfil de teste' not in page.locator('#release-description').inner_text()
    release['installer']['url'] = 'https://example.test/obsidian.exe'
    release['installer']['sha256'] = 'invalid'
    page.reload(wait_until='networkidle')
    assert 'Perfil de teste' not in page.locator('#release-description').inner_text()
    result = {'result': 'passed', 'menus': 8, 'themes': 4, 'widths': [320,390,768,1024,1440], 'release_validation': 'passed', 'browser_errors': errors}
    (OUT / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))
    browser.close()
