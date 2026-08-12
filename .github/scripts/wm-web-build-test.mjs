#!/usr/bin/env node
// wm-web-build-test.mjs — <b>나쁜 회선에서 집이 서나</b> (TASK-WM-287).
//
// ★ 왜: 짓기는 세계가 판정한다(재료·겹침) — 그 판정은 서버 시험이 지킨다. 그런데 <b>창에서
//   눌러 짓는 길</b>은 진짜 창으로 한 번도 안 재봤다. 줍기는 쟀는데(WM-274·283) 짓기는 안 쟀다.
//   짓기는 줍기보다 한 겹 더 있다: 재료를 쓰고 · 땅을 고르고 · 세워진 것이 <b>남에게도</b> 보인다.
//
// 재는 것 (왕복 200ms 회선):
//   ① 눌렀을 때 곧바로 「짓는 중…」이라고 하나 ② 세운 것이 창에 뜨나 ③ 가방에서 재료가 빠지나
//   ④ 창이 조용히 안 터지나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 선다 · 1 = 못 선다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5940);
const linePort = port + 1;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-build-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const REACH = 2.5;
const GATHER_UP_TO_MS = 90000;

function cannotRun(message) {
	console.error(`[web-build] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

let chromium;
try {
	const root = process.env.WM_PLAYWRIGHT_ROOT;
	const req = createRequire(root ? join(resolve(root), 'package.json') : import.meta.url);
	chromium = req('playwright').chromium;
} catch (error) {
	cannotRun(`playwright 를 못 찾았다 — ${error.message} (WM_PLAYWRIGHT_ROOT 로 알려 준다)`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-build-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

let world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
});

function killWorld() {
	if (world === null) return;
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
	world = null;
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

{
	const until = Date.now() + 120000;
	let up = false;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}

	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다');
	}
}

const line = openBadLine({ listenPort: linePort, targetPort: port, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS });
await line.listen();

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(`http://127.0.0.1:${linePort}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });
await page.waitForFunction(
	() => (window.__wmView.dolls() || []).some((one) => one.isLocal), null, { timeout: 30000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

check('나쁜 회선으로 세계에 붙었다', await page.evaluate(() => typeof window.__wmView === 'object'));

await page.bringToFront();
await page.mouse.click(400, 300);

// ── 재료를 모은다 — 「지을 수 있다」가 될 때까지 걸어가서 줍는다 ────────
//   ⚠ 재기 전에 <b>잴 것이 있는지</b>부터 (domain-wm.md § 관문 규율).
// 창은 못 짓는 줄 앞에 <b>「· 」</b>를 붙인다(picker.mjs buildLabel) — 그게 없는 줄이 곧 지을 수 있는 것.
// ⚠ 「모자」 같은 낱말로 찾으면 안 된다 — 그런 글자는 애초에 안 붙는다(첫 판이 그래서
//    가방이 빈 채로 「지을 수 있다」가 됐다).
const buildableNow = async () => page.evaluate(() => {
	const picker = document.getElementById('buildpick');
	if (!picker || picker.options.length === 0) return null;

	const able = [...picker.options].filter((one) => one.textContent.startsWith('· ') === false);
	return able.length > 0 ? able[0].value : null;
});

let ready = false;
{
	const until = Date.now() + GATHER_UP_TO_MS;
	const taken = new Set();
	while (Date.now() < until) {
		const able = await buildableNow();
		if (able !== null) {
			// 지을 수 있는 것을 고른다 — 고르지 않으면 땅을 눌러도 아무 일도 안 일어난다.
			await page.selectOption('#buildpick', able);
			ready = true;
			break;
		}

		const near = await page.evaluate((seen) => {
			const me = (window.__wmView.dolls() || []).find((one) => one.isLocal);
			if (!me) return null;

			const field = (window.__wmView.field() || []).filter((one) => seen.includes(one.id) === false);
			let best = null;
			for (const one of field) {
				const away = Math.hypot(one.x - me.drawnX, one.z - me.drawnZ);
				if (best === null || away < best.away) best = { ...one, away, meX: me.drawnX, meZ: me.drawnZ };
			}

			return best;
		}, [...taken]);

		if (near === null) { await wait(300); continue; }

		if (near.away <= REACH * 0.6) {
			const where = await page.evaluate((spot) => window.__wmView.screenOf(spot.x, spot.z), near);
			await page.mouse.click(Math.round(where.x), Math.round(where.y));
			taken.add(near.id);
			await wait(600);
			continue;
		}

		const keyX = near.x > near.meX + 0.2 ? 'd' : (near.x < near.meX - 0.2 ? 'a' : null);
		const keyZ = near.z > near.meZ + 0.2 ? 'w' : (near.z < near.meZ - 0.2 ? 's' : null);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.down(key);
		}

		await wait(120);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.up(key);
		}
	}
}

if (ready === false) {
	await browser.close();
	await line.close();
	killWorld();
	cannotRun('재료를 못 모았다 — 지을 수 없으면 「집이 서나」를 잴 수 없다');
}

const bagBefore = await page.evaluate(() => (document.getElementById('bag').textContent || '').trim());
const builtBefore = await page.evaluate(() => window.__wmView.world().buildings);

// ── 땅을 눌러 짓는다 — 사람이 하는 그대로 ──────────────────────────────
await page.evaluate(() => {
	window.__wmSaid = { at: -1, text: '' };
	const before = (document.getElementById('status') || {}).textContent;
	const watch = setInterval(() => {
		const now = (document.getElementById('status') || {}).textContent;
		if (now !== before && window.__wmSaid.at < 0) {
			window.__wmSaid.at = Date.now();
			window.__wmSaid.text = now;
		}
	}, 10);

	setTimeout(() => clearInterval(watch), 8000);
	window.__wmPressedAt = Date.now();
});

// 내 발밑 옆의 빈 땅 — 주울 것 위를 누르면 줍기가 된다(그건 다른 길이다).
{
	const me = await page.evaluate(() => (window.__wmView.dolls() || []).find((one) => one.isLocal));
	const spot = { x: Math.floor(me.drawnX) + 1.5, z: Math.floor(me.drawnZ) + 1.5 };
	const where = await page.evaluate((one) => window.__wmView.screenOf(one.x, one.z), spot);
	await page.mouse.click(Math.round(where.x), Math.round(where.y));
}

const said = await page.evaluate(() => ({ ...window.__wmSaid, pressedAt: window.__wmPressedAt }));
const saidInMs = said.at < 0 ? -1 : said.at - said.pressedAt;

// 세운 것이 창에 뜰 때까지
let built = builtBefore;
{
	const until = Date.now() + 15000;
	while (Date.now() < until) {
		built = await page.evaluate(() => window.__wmView.world().buildings);
		if (built > builtBefore) break;

		await wait(200);
	}
}

const bagAfter = await page.evaluate(() => (document.getElementById('bag').textContent || '').trim());

check('누르자마자 「짓는 중」이라고 한다', saidInMs >= 0 && saidInMs <= 200,
	saidInMs < 0 ? '아무 말도 안 했다' : `${saidInMs}ms · "${said.text}"`);
check('세운 것이 창에 뜬다', built > builtBefore, `건물 ${builtBefore} → ${built}`);
check('가방에서 재료가 빠진다', bagAfter !== bagBefore, `${bagBefore} → ${bagAfter}`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
await line.close();
killWorld();

if (failures === 0) {
	console.log('[web-build] ✅ 왕복 200ms 회선에서도 눌러서 집이 선다');
	process.exit(0);
}

console.log(`\n[web-build] RESULT: ${failures}건`);
process.exit(1);
