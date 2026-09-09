const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { webkit, devices } = require('playwright');

const base = new URL(process.argv[2] || 'http://127.0.0.1:5291');
assert(['localhost', '127.0.0.1', '[::1]'].includes(base.hostname), 'Validation is restricted to a local app; never use a production URL.');
const output = path.resolve(__dirname, '../.vs/phone-validation');
const fixture = JSON.parse(fs.readFileSync(path.join(output, 'fixture.json'), 'utf8'));
assert.equal(fixture.kind, 'synthetic-layout-only', 'This harness accepts only its generated layout fixture, not a Settings backup.');
const { defaultBrowserType, ...device } = devices['iPhone 17 Pro'];
const profiles = [
    { name: 'home-screen-portrait', viewport: device.screen, insets: { top: 62, right: 0, bottom: 34, left: 0 } },
    { name: 'safari-sized-portrait', viewport: device.viewport, insets: { top: 0, right: 0, bottom: 0, left: 0 } },
    { name: 'portrait-larger-text', viewport: device.screen, insets: { top: 62, right: 0, bottom: 34, left: 0 }, fontSize: '20px' },
    { name: 'narrow-portrait', viewport: { width: 375, height: 812 }, insets: { top: 59, right: 0, bottom: 34, left: 0 } },
    { name: 'home-screen-landscape', viewport: { width: 874, height: 402 }, insets: { top: 0, right: 62, bottom: 21, left: 62 } },
];
const phases = ['Command', 'Movement', 'Shooting', 'Charge', 'Fight'];
const stateKey = 'tombworld:battle:v1:' + fixture.roster.id;
const primaryId = fixture.roster.units.find(unit => unit.datasheetId === 'immortals').id;

async function settle(page) {
    await page.evaluate(async () => {
        await document.fonts.ready;
        await Promise.all(document.getAnimations()
            .filter(animation => animation.effect?.getTiming().iterations !== Infinity)
            .map(animation => animation.finished.catch(() => {})));
    });
}

async function windowFor(page, phase, turn) {
    await page.getByRole('button', { name: turn, exact: true }).tap();
    await page.getByRole('button', { name: phase, exact: true }).tap();
    await page.waitForFunction(({ phase, turn }) => {
        const selectedPhase = document.querySelector('.phase.on');
        const selectedTurn = document.querySelector('.turn.on');
        return selectedPhase?.getAttribute('aria-label') === phase && selectedTurn?.textContent.trim() === turn;
    }, { phase, turn });
    await settle(page);
}

async function layout(page, profile) {
    await settle(page);
    const measured = await page.evaluate(insets => {
        const visible = element => element.getClientRects().length && getComputedStyle(element).visibility !== 'hidden';
        const rect = element => {
            const box = element.getBoundingClientRect();
            return { x: box.x, y: box.y, width: box.width, height: box.height, right: box.right, bottom: box.bottom };
        };
        const selectors = '.cp-btn, .turn, .phase, .deck-dot, .deck-ov, .setup-status, .now-action, .counter button, .sname-btn, .st.clickable';
        const targets = [...document.querySelectorAll(selectors)].filter(visible).map(element => ({
            label: element.getAttribute('aria-label') || element.textContent.trim().slice(0, 70), ...rect(element),
        }));
        const cards = [...document.querySelectorAll('.now-action')].map(rect);
        const rows = new Map();
        cards.forEach(card => { const y = Math.round(card.y); rows.set(y, (rows.get(y) || 0) + 1); });
        const clipped = [...document.querySelectorAll('.now-action strong, .now-action small')].filter(visible)
            .filter(element => element.scrollWidth > element.clientWidth + 1 || element.scrollHeight > element.clientHeight + 1)
            .map(element => element.textContent.trim());
        const hud = document.querySelector('.hud');
        return {
            width: innerWidth,
            documentWidth: document.documentElement.scrollWidth,
            targets,
            cards: cards.length,
            rows: rows.size,
            maxCardsPerRow: Math.max(0, ...rows.values()),
            clipped,
            hud: rect(hud),
            hudButtons: hud.querySelectorAll('button').length,
            clearance: parseFloat(getComputedStyle(document.querySelector('.play')).paddingBottom),
            minimumTitleFont: Math.min(...[...document.querySelectorAll('.now-action strong')].map(element => parseFloat(getComputedStyle(element).fontSize))),
            insets,
        };
    }, profile.insets);
    assert(measured.documentWidth <= measured.width + 1, `${profile.name}: horizontal page overflow`);
    assert.equal(measured.hudButtons, 9, 'The HUD must contain only CP, turn and five phase controls.');
    assert(measured.hud.x >= profile.insets.left && measured.hud.right <= measured.width - profile.insets.right + 1, 'HUD overlaps a horizontal safe area.');
    assert(measured.hud.bottom <= profile.viewport.height - profile.insets.bottom + 1, 'HUD overlaps the home-indicator safe area.');
    assert(measured.clearance >= measured.hud.height + profile.insets.bottom, 'Content has insufficient clearance below the fixed HUD.');
    for (const target of measured.targets) {
        assert(target.width >= 43.5 && target.height >= 43.5, `${profile.name}: undersized target ${target.label}: ${target.width} x ${target.height}`);
        assert(target.x >= profile.insets.left - 1 && target.right <= measured.width - profile.insets.right + 1, `${profile.name}: target extends beyond the safe viewport: ${target.label}`);
    }
    assert(measured.maxCardsPerRow <= 3, 'Now must never exceed three cards per row.');
    assert.deepEqual(measured.clipped, [], 'Now names and details must not be clipped.');
    if (measured.cards) assert(measured.minimumTitleFont >= 13, 'Printed rule names must remain readable.');
    return { cards: measured.cards, rows: measured.rows, maxCardsPerRow: measured.maxCardsPerRow, targets: measured.targets.length };
}

async function sheet(page, profile, name) {
    const dialog = page.getByRole('dialog', { name, exact: true });
    await dialog.waitFor();
    await settle(page);
    const measured = await dialog.evaluate(element => {
        const box = element.getBoundingClientRect();
        const close = element.querySelector('.sheet-close').getBoundingClientRect();
        const body = element.querySelector('.sheet-body');
        const bodyBox = body.getBoundingClientRect();
        return {
            top: box.top, bottom: box.bottom, closeTop: close.top, closeRight: close.right,
            closeBottom: close.bottom, closeWidth: close.width, closeHeight: close.height,
            bodyBottom: bodyBox.bottom, bodyHeight: bodyBox.height, bodyScroll: body.scrollHeight,
            overflowY: getComputedStyle(body).overflowY,
        };
    });
    assert(measured.top >= 0 && measured.bottom <= profile.viewport.height - profile.insets.bottom + 1, `${name}: sheet extends beyond the safe viewport`);
    assert(measured.closeWidth >= 44 && measured.closeHeight >= 44, 'Sheet close target is too small.');
    assert(measured.closeTop >= profile.insets.top && measured.closeRight <= profile.viewport.width - profile.insets.right, 'Sheet close target overlaps a safe area.');
    assert(measured.closeBottom <= profile.viewport.height - profile.insets.bottom, 'Sheet close target is off screen.');
    assert(measured.bodyHeight > 0 && measured.bodyBottom <= measured.bottom + 1, 'Sheet body does not fit below its header.');
    assert.equal(measured.overflowY, 'auto', 'Long sheet content must scroll inside the sheet.');
    return dialog;
}

(async () => {
    fs.mkdirSync(output, { recursive: true });
    const browser = await webkit.launch();
    const report = { kind: fixture.kind, browser: await browser.version(), physicalDeviceVerified: false, actualArmyRehearsed: false, profiles: [] };
    try {
        for (const profile of profiles) {
            const unexpected = [];
            const errors = [];
            const context = await browser.newContext({ ...device, viewport: profile.viewport, screen: profile.viewport, serviceWorkers: 'block' });
            context.setDefaultTimeout(30000);
            context.on('page', page => page.on('pageerror', error => errors.push(error.message)));
            await context.route('**/*', async route => {
                const request = route.request();
                const url = new URL(request.url());
                if (url.origin !== base.origin) {
                    unexpected.push(`External request blocked: ${request.method()} ${url.origin}${url.pathname}`);
                    return route.abort();
                }
                if (url.pathname.startsWith('/api/')) {
                    const responses = {
                        '/api/whoami': fixture.user,
                        '/api/catalogue': fixture.catalogue,
                        '/api/settings': fixture.settings,
                        '/api/rosters': [fixture.roster],
                        ['/api/rosters/' + fixture.roster.id]: fixture.roster,
                        '/api/schedule-library': fixture.library,
                    };
                    if (request.method() === 'GET' && Object.hasOwn(responses, url.pathname))
                        return route.fulfill({ json: responses[url.pathname] });
                    if (request.method() === 'POST' && url.pathname === '/api/rosters/validate')
                        return route.fulfill({ json: fixture.validation });
                    unexpected.push(`API request blocked: ${request.method()} ${url.pathname}`);
                    return route.abort();
                }
                if (request.method() !== 'GET') {
                    unexpected.push(`Write blocked: ${request.method()} ${url.pathname}`);
                    return route.abort();
                }
                if (url.pathname.endsWith('.css')) {
                    const response = await route.fetch();
                    // Test-only safe-area simulation; no production stylesheet is modified.
                    const css = (await response.text()).replace(/env\(safe-area-inset-(top|right|bottom|left)\)/g, (_, side) => profile.insets[side] + 'px');
                    return route.fulfill({ response, body: css });
                }
                return route.continue();
            });
            let page = await context.newPage();
            const open = async () => {
                await page.goto(base.origin + '/play/' + fixture.roster.id, { waitUntil: 'domcontentloaded' });
                await page.locator('.now-ribbon').waitFor({ timeout: 120000 });
                if (profile.fontSize) await page.evaluate(size => document.documentElement.style.fontSize = size, profile.fontSize);
                await settle(page);
            };
            try {
                await open();
                await page.getByRole('button', { name: 'Gain command point', exact: true }).tap();
                await page.waitForFunction(() => document.querySelector('.cp-num')?.textContent === '1');
                await page.getByRole('button', { name: 'Gain command point', exact: true }).tap();
                await page.waitForFunction(() => document.querySelector('.cp-num')?.textContent === '2');
                await windowFor(page, 'Shooting', 'YOU');
                await layout(page, profile);
                await page.locator('.now-action').filter({ hasText: 'Technosorcerous Augmentations' }).tap();
                const choice = await sheet(page, profile, 'Technosorcerous Augmentations');
                assert((await choice.textContent()).includes('Atomic Disintegrators'));
                await choice.getByRole('button', { name: 'Anti-VEHICLE 5+', exact: true }).tap();
                await page.locator('.sheet').waitFor({ state: 'hidden' });
                const beforeTrack = Number((await page.locator('.statline .c-value').first().textContent()).split('/')[0]);
                await page.locator('.statline .counter button').first().tap();
                await page.waitForFunction(({ key, id, remaining }) => {
                    const state = JSON.parse(localStorage.getItem(key) || 'null');
                    return state?.CommandPoints === 2 && state.PartTracks[id] === remaining && state.ShootingChoices[id] === 'Anti-VEHICLE 5+';
                }, { key: stateKey, id: primaryId, remaining: beforeTrack - 1 });
                const saved = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), stateKey);
                await page.close();
                page = await context.newPage();
                await open();
                await page.waitForFunction(() => document.querySelector('.cp-num')?.textContent === '2');
                assert((await page.locator('.now-ribbon').textContent()).includes('Selected: Anti-VEHICLE 5+'));
                const resumed = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), stateKey);
                for (const field of ['CommandPoints', 'Phase', 'Turn', 'PartTracks', 'ShootingChoices']) assert.deepEqual(resumed[field], saved[field], `Resume lost ${field}`);
                const windows = [];
                await page.getByRole('tab', { name: 'Army overview', exact: true }).tap();
                assert.equal(await page.locator('.mx-scroll').count(), 0, 'Matrix must not be the default overview.');
                for (const turn of ['YOU', 'OPP']) {
                    for (const phase of phases) {
                        await windowFor(page, phase, turn);
                        const dimensions = await layout(page, profile);
                        windows.push({ turn, phase, ...dimensions });
                        await page.getByRole('button', { name: 'Matrix (wide)', exact: true }).tap();
                        await page.locator('.now-ribbon').waitFor({ state: 'hidden' });
                        assert.equal(await page.locator('.overview').count(), 1);
                        await page.getByRole('button', { name: 'Now cards', exact: true }).tap();
                        await page.locator('.now-ribbon').waitFor();
                    }
                }
                await windowFor(page, 'Fight', 'OPP');
                assert.equal(await page.locator('.now-action').filter({ hasText: 'Technosorcerous Augmentations' }).count(), 0);
                await page.locator('.now-action').filter({ hasText: 'Microscarab Swarm' }).tap();
                const stratagem = await sheet(page, profile, 'Microscarab Swarm');
                const text = (await stratagem.textContent()).replace(/\s+/g, ' ');
                for (const field of ['when', 'target', 'effect']) assert(text.includes(fixture.microscarab[field].replace(/\s+/g, ' ')), `Microscarab lost ${field}`);
                await page.screenshot({ path: path.join(output, profile.name + '-stratagem.png') });
                await stratagem.getByRole('button', { name: 'Close', exact: true }).tap();
                await page.locator('.sheet').waitFor({ state: 'hidden' });
                await page.getByTitle('Army & detachment rules', { exact: true }).tap();
                const rules = await sheet(page, profile, 'Army & detachment rules');
                const scrolled = await rules.locator('.sheet-body').evaluate(body => { body.scrollTop = body.scrollHeight; return body.scrollTop; });
                assert(scrolled > 0, 'The long rules sheet was not scrollable.');
                await sheet(page, profile, 'Army & detachment rules');
                await rules.getByRole('button', { name: 'Close', exact: true }).tap();
                await page.locator('.sheet').waitFor({ state: 'hidden' });
                await page.evaluate(() => scrollTo(0, 0));
                await settle(page);
                await page.screenshot({ path: path.join(output, profile.name + '-overview.png'), fullPage: true });
                assert.equal(await page.locator('#blazor-error-ui').isVisible(), false, 'Blazor reported an unhandled error.');
                assert.deepEqual(errors, [], 'Browser errors occurred.');
                assert.deepEqual(unexpected, [], 'Unexpected requests occurred; no production writes are permitted.');
                report.profiles.push({ ...profile, status: 'passed', reopenStatePreserved: true, rulesSheetScrolled: true, windows });
                console.log(`${profile.name}: passed ${windows.length} synthetic windows, layout, sheet and reopen checks`);
            } catch (error) {
                report.status = 'failed';
                report.profiles.push({ ...profile, status: 'failed', error: error.message, unexpected, errors });
                await page.screenshot({ path: path.join(output, profile.name + '-failure.png'), fullPage: true }).catch(() => {});
                throw error;
            } finally {
                await context.close();
            }
        }
        report.status = 'passed';
    } finally {
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify(report, null, 2));
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
