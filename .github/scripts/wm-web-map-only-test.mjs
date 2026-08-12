#!/usr/bin/env node
// wm-web-map-only-test.mjs — <b>엔진이 영영 안 와도</b> 세계에서 논다 (TASK-WM-249).
//
// ★ 왜: 지도(2D)는 엔진을 기다리는 동안 보여 주는 것이었다(WM-234). 그런데 회선이 아주 나쁘면
//   엔진은 <b>몇십 초</b> 뒤에 오거나 끝내 안 온다. 그 사이 사람이 아무것도 못 하면
//   그건 「기다리는 화면」이지 게임이 아니다.
//
// 재는 것: three.js 를 <b>일부러 막고</b> 지도만으로 ① 걷고 ② 줍는다.
//   막는 것이 핵심이다 — 안 막으면 엔진이 곧바로 와서 지도를 안 거치고 지나간다.
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 논다 · 1 = 못 논다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5404);
const url = `http://127.0.0.1:${port}/`;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-maponly-')), 'world.json');

const WALK_UP_TO_MS = 20000;

function cannotRun(message) {
	console.error(`[web-maponly] CANNOT-RUN: ${message}`);
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

let world = null;
function killWorld() {
	if (world === null) return;
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
	world = null;
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-maponly-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
});

{
	const until = Date.now() + 120000;
	let up = false;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`${url}health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await new Promise((done) => setTimeout(done, 300));
	}

	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다');
	}
}

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

// ★ 엔진을 막는다 — 「끝내 안 오는 회선」을 그대로 만든다.
await page.route('**/three.module.min.js', (route) => route.abort());

await page.goto(url);

let mapAt = -1;
try {
	await page.waitForFunction(
		() => window.__wmEarly && window.__wmEarly.socket && window.__wmEarly.socket.readyState === 1,
		null, { timeout: 30000 });
	mapAt = Date.now();
} catch { /* 아래 칸이 잡는다 */ }

check('엔진이 안 와도 지도가 세계에 붙는다', mapAt > 0);
check('3D 는 정말로 안 왔다 (막은 것이 먹었다)',
	await page.evaluate(() => typeof window.__wmView !== 'object'));

if (mapAt < 0) {
	await browser.close();
	killWorld();
	console.log(`\n[web-maponly] RESULT: ${failures}건`);
	process.exit(1);
}

// 가방이 오는 것을 창 안에서 본다 — 주웠는지는 <b>세계가 준 가방</b>으로만 안다.
await page.evaluate(() => {
	window.__wmBag = [];
	window.__wmEarly.socket.addEventListener('message', (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'bag') window.__wmBag.push(said);
	});
});

// ── ① 걷는다 — 가장 가까운 주울 것 쪽으로, 손이 닿을 때까지 ─────────────
// 첫 전체 그림이 와야 주울 것을 안다 — 붙자마자 묻지 않는다.
await page.waitForFunction(() => window.__wmEarly.field().length > 0, null, { timeout: 15000 })
	.catch(() => { /* 아래 칸이 0 으로 잡는다 */ });

const goal = await page.evaluate(() => window.__wmEarly.nearestNode());
check('지도가 주울 것을 안다', goal !== null, goal ? `(${goal.x}, ${goal.z})` : '하나도 없다');

let reached = false;
if (goal !== null) {
	const until = Date.now() + WALK_UP_TO_MS;
	while (Date.now() < until) {
		const me = await page.evaluate(() => window.__wmEarly.drawnMe());
		if (me === null) { await new Promise((done) => setTimeout(done, 200)); continue; }

		const inReach = await page.evaluate(() => window.__wmEarly.reachable() !== null);
		if (inReach) { reached = true; break; }

		// 세계의 +Z 가 앞이다(지도는 눈이 안 돌아간다).
		const holdX = goal.x > me.x + 0.2 ? 'd' : (goal.x < me.x - 0.2 ? 'a' : null);
		const holdZ = goal.z > me.z + 0.2 ? 'w' : (goal.z < me.z - 0.2 ? 's' : null);

		for (const key of [holdX, holdZ]) {
			if (key === null) continue;

			await page.keyboard.down(key);
		}

		await new Promise((done) => setTimeout(done, 150));
		for (const key of [holdX, holdZ]) {
			if (key === null) continue;

			await page.keyboard.up(key);
		}
	}
}

check('지도에서 걸어서 손이 닿는 데까지 갔다', reached);

// ── ② 줍는다 ───────────────────────────────────────────────────────────
let picked = false;
if (reached) {
	await page.keyboard.press('e');
	try {
		await page.waitForFunction(
			() => window.__wmBag.some((one) => (one.items || []).some((item) => item.amount > 0)),
			null, { timeout: 10000 });
		picked = true;
	} catch { /* 아래 칸이 잡는다 */ }
}

const bag = await page.evaluate(() => window.__wmBag.slice(-1)[0] || null);
check('지도에서 주운 것이 가방에 들어왔다', picked,
	bag === null ? '가방이 안 왔다' : JSON.stringify(bag).slice(0, 160));
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
killWorld();

if (failures === 0) {
	console.log('[web-maponly] ✅ 엔진이 안 와도 지도만으로 걷고 줍는다');
	process.exit(0);
}

console.log(`\n[web-maponly] RESULT: ${failures}건`);
process.exit(1);
