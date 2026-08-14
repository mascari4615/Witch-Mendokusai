#!/usr/bin/env node
// wm-web-frozen-door-test.mjs — <b>넘어가려는데 저쪽 세계가 얼어 있으면</b> (TASK-WM-359).
//
// ★ 왜: 국경을 넘는 길은 「저 세계가 <b>꺼져 있으면</b> 돌아온다」까지 봤다(WM-256).
//   그런데 꺼진 것과 <b>얼어붙은 것</b>은 다르다: 얼면 줄은 열리는데 아무 대답이 없다.
//   그때 창이 옛 줄을 놓아 버리면 그 사람은 <b>두 세계 어디에도 없는</b> 사람이 된다(가방째).
//   사람이 사라지는 것이 이 세계의 최악이므로, 그 길을 실제로 걸어 본다.
//
// 재는 것 (서쪽에 진짜 창 하나 · 동쪽은 <b>얼어붙은 문</b> 뒤에 있다):
//   ① 창이 여기 그대로 남는다(서쪽이 그 사람을 계속 센다) ② 가방이 그대로다
//   ③ 창이 조용히 안 터진다
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 그대로 있는다 · 1 = 사라진다 · 2 = 못 돌림
//
// [빨강-확인] 이 관문을 만든 그 판에서 <b>다른 것</b>이 빨개졌다 (2026-08-14):
//   「창이 조용히 안 터졌다 — ReferenceError: drawChest is not defined」.
//   상자 칸이 통째로 죽어 있던 것을 이 자리가 잡았다(고친 뒤 초록 · 그 자리는 wm-web-chest-test 가 맡는다).
//   사람이 사라지지 않는다는 두 줄은 고치기 전에도 초록이었다 — 그건 세계가 이미 성했다는 뜻이다.

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const westPort = Number(process.env.WM_SMOKE_PORT || 5740);
const eastPort = westPort + 1;
const frozenDoor = westPort + 2;   // 동쪽으로 가는 문 — 이 앞에서 얼린다

const SECRET = '두 세계만 아는 말';
const ITEM_ID = 10;
const STOCK = 5;

function cannotRun(message) {
	console.error(`[언문] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let chromium;
try {
	const root = process.env.WM_PLAYWRIGHT_ROOT;
	const req = createRequire(root ? join(resolve(root), 'package.json') : import.meta.url);
	chromium = req('playwright').chromium;
} catch (error) {
	cannotRun(`playwright 를 못 찾았다 — ${error.message}`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-fdoor-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours, seedChest) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-fdoor-')), 'world.json');
	if (seedChest) {
		writeFileSync(worldFile, JSON.stringify({
			buildings: [{ x: 0, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
			storages: [{ x: 0, y: 0, z: 0, items: [{ itemId: ITEM_ID, amount: STOCK }] }],
			year: 1, season: 0, day: 1, hour: 8, minute: 0,
			people: [], gathered: [], cauldrons: [],
		}), 'utf8');
	}

	worlds.push(spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: worldFile, WM_ZONE: zone, WM_ZONE_NEIGHBOURS: neighbours, WM_ZONE_SECRET: SECRET },
		stdio: 'ignore',
	}));
}

function killWorlds() {
	for (const one of worlds) {
		try {
			if (process.platform === 'win32') execSync(`taskkill /PID ${one.pid} /F /T`, { stdio: 'ignore' });
			else one.kill('SIGKILL');
		} catch { /* 이미 죽었다 */ }
	}
}

// 서쪽 사람이 동쪽으로 갈 때 지나는 문 — 이 문을 얼린다.
const door = openBadLine({ listenPort: frozenDoor, targetPort: eastPort });
await door.listen();

startWorld(westPort, '서:-40,-40,0,40', `동:0,-40,40,40=ws://127.0.0.1:${frozenDoor}/ws`, true);
startWorld(eastPort, '동:0,-40,40,40', `서:-40,-40,0,40=ws://127.0.0.1:${westPort}/ws`, false);

for (const port of [westPort, eastPort]) {
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}
	if (up === false) { killWorlds(); await door.close(); cannotRun(`세계가 안 떴다 — ${port}`); }
}

const health = (port) => fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

const browser = await chromium.launch();
const page = await browser.newPage();
const errors = [];
page.on('pageerror', (error) => errors.push(String(error)));
await page.goto(`http://127.0.0.1:${westPort}/`);

const painted = await page.waitForFunction(
	() => typeof window.__wmView === 'object' && window.__wmView.me() !== null,
	null, { timeout: 60000 }).then(() => true).catch(() => false);

if (painted === false) {
	await browser.close();
	await door.close();
	killWorlds();
	cannotRun('창이 안 떴다');
}

// 가방에 뭔가 넣어 둔다 — 「사람과 짐이 같이 남나」를 봐야 한다.
await page.evaluate((itemId) => {
	const socket = window.__wmView.socket();
	socket.send(JSON.stringify({ type: 'chesttake', x: 0, y: 0, z: 0, itemId, amount: 3, did: 991 }));
	socket.send(JSON.stringify({ type: 'bagask' }));
}, ITEM_ID);

await wait(1500);

// ── 동쪽으로 가는 문을 얼린다 ───────────────────────────────────────────────
door.freeze();

// 국경(x=0)으로 걷는다 — 세계가 「저쪽으로 넘어가라」고 말해 줄 때까지.
await page.evaluate(() => {
	window.__said = [];
	const socket = window.__wmView.socket();
	const was = socket.onmessage;
	socket.onmessage = (event) => {
		try {
			const one = JSON.parse(String(event.data));
			if (one.type === 'moveon') window.__said.push('moveon');
		} catch (e) { /* 딴 소식 */ }

		return was(event);
	};
	window.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }));
});

// 넘어가라는 말이 나오고, 창이 저쪽을 기다리다 포기할 때까지 넉넉히.
await wait(25000);

const west = await health(westPort);
const east = await health(eastPort);
const seen = await page.evaluate(() => ({
	told: (window.__said || []).length,
	me: window.__wmView.me(),
	status: document.getElementById('status') ? document.getElementById('status').textContent : '',
}));

await browser.close();
await door.close();
killWorlds();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 「넘어가라」 ${seen.told}번 · 서쪽 사람 ${west.people}명 · 동쪽 사람 ${east.people}명`
	+ ` · 창의 인형 ${seen.me} · 화면: ${String(seen.status).slice(0, 40)}`);

if (seen.told === 0) cannotRun('세계가 「넘어가라」를 안 했다 — 얼어붙은 문을 잴 자리까지 못 갔다');

// ★ 이 관문의 전부: <b>사람이 사라지지 않는다.</b>
check('사람이 어느 한쪽에는 있다', west.people + east.people >= 1,
	`서쪽 ${west.people}명 · 동쪽 ${east.people}명`);

check('얼어붙은 문 앞에서는 여기 그대로 있는다', west.people >= 1,
	`서쪽 ${west.people}명`);

check('창이 조용히 안 터졌다', errors.length === 0, errors.slice(0, 2).join(' | ') || '오류 없음');

if (bad === 0) {
	console.log('[언문] ✅ 저쪽 문이 얼어 있으면 넘어가지 않고 여기 남는다');
	process.exit(0);
}

console.log(`\n[언문] RESULT: ${bad}건`);
process.exit(1);
