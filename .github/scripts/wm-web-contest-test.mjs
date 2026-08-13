#!/usr/bin/env node
// wm-web-contest-test.mjs — <b>겨루기에서 진 쪽 창</b>은 무엇을 보나 (TASK-WM-274).
//
// ★ 왜: 같은 것을 둘이 노리면 하나만 가진다 — 그 판정은 세계가 한다(시험 있음, loopback).
//   그런데 <b>진 사람 화면</b>은 아무도 안 봤다. 회선이 늦으면 진 사람은 200ms 동안
//   「아직 거기 있는 것」을 보고 있고, 눌러도 아무 일이 안 일어난다.
//   그때 ① 왜 안 됐는지 <b>말해 주나</b> ② 없어진 것이 화면에서 <b>사라지나</b>(유령이 안 남나)
//   — 조용히 실패하면 사람은 「고장났다」로 읽는다.
//
// 재는 법: 진짜 창은 나쁜 회선(왕복 200ms)으로 붙고, 봇은 세계에 바로 붙어 <b>먼저</b> 줍는다.
//   창은 그 사이에 같은 것을 주우려 한다 — 사람이 겪는 그대로.
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 말해 준다 · 1 = 조용히 실패한다 · 2 = 못 돌림
//
// [빨강-확인] 세계가 「왜 안 됐는지」를 말 안 하게 하니 2건 빨강 — 「아무 말도 없었다」(사람은 고장으로 읽는다) (2026-08-14)

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5620);
const linePort = port + 1;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-contest-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;

function cannotRun(message) {
	console.error(`[web-contest] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-contest-app-')), 'app');
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

check('나쁜 회선으로 세계에 붙었다', await page.evaluate(() => typeof window.__wmView === 'object'));

// 창이 아는 들판에서 <b>가장 가까운 것</b>을 고른다 — 사람이 겨냥하는 그것이다.
await page.waitForFunction(() => (window.__wmView.world().gatherables || 0) > 0, null, { timeout: 20000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

// ⚠ 들판은 <b>창의 상태</b>에서 읽는다. 소식을 뒤늦게 엿들으면 첫 전체 판을 놓쳐
//   「들판을 모른다」가 된다(첫 판이 그랬다) — 그건 세계 탓이 아니라 재는 자리의 문제다.
await page.evaluate(() => {
	window.__wmDenied = [];
	window.__wmView.socket().addEventListener('message', (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'denied') window.__wmDenied.push(said);
	});
});

let field = null;
{
	const until = Date.now() + 15000;
	while (Date.now() < until) {
		field = await page.evaluate(() => window.__wmView.field());
		if (field !== null && field.length > 0) break;

		await wait(200);
	}
}

check('창이 들판을 안다', field !== null && field.length > 0, field === null ? '못 받았다' : `${field.length}자리`);
if (field === null || field.length === 0) {
	await browser.close();
	await line.close();
	killWorld();
	console.log(`\n[web-contest] RESULT: ${failures}건`);
	process.exit(1);
}

// 둘 다 <b>손이 닿는 데까지</b> 가야 겨루기가 된다 — 멀리서 누르면 그건 그냥 「안 닿는다」다.
const REACH = 2.5;   // walk.mjs REACH · 세계도 같은 값을 본다
const target = field.slice().sort((a, b) => Math.hypot(a.x, a.z) - Math.hypot(b.x, b.z))[0];

const bot = new WebSocket(`ws://127.0.0.1:${port}/ws`);
let botId = 0;
let botAt = { x: 0, z: 0 };
bot.onopen = () => bot.send(JSON.stringify({ type: 'hello', secret: '' }));
bot.onmessage = (event) => {
	let said;
	try { said = JSON.parse(event.data); } catch { return; }

	if (said.type === 'welcome' && said.id) botId = said.id;
	if (said.type === 'me' && said.doll) botAt = { x: said.doll.x, z: said.doll.z };
	if (said.type === 'world' && Array.isArray(said.dolls)) {
		const mine = said.dolls.find((one) => one.id === botId);
		if (mine && typeof mine.x === 'number') botAt = { x: mine.x, z: mine.z };
	}
};

{
	const until = Date.now() + 8000;
	while (Date.now() < until && botId === 0) await wait(100);
}

check('겨룰 상대가 세계에 있다', botId !== 0);

// 봇은 <b>제가 아는 자리</b>에서 그 자리 쪽으로 한 걸음씩 — 고정된 방향으로 밀면 지나쳐 버린다.
{
	const until = Date.now() + 15000;
	while (Date.now() < until) {
		const away = Math.hypot(target.x - botAt.x, target.z - botAt.z);
		if (away <= REACH * 0.6) break;

		const step = Math.min(1.4, away);
		bot.send(JSON.stringify({
			type: 'move',
			x: (target.x - botAt.x) / away * step,
			z: (target.z - botAt.z) / away * step,
		}));

		await wait(60);
	}
}

check('겨룰 상대가 그 자리까지 갔다', Math.hypot(target.x - botAt.x, target.z - botAt.z) <= REACH,
	`${Math.hypot(target.x - botAt.x, target.z - botAt.z).toFixed(2)}m`);

// 창도 그 자리까지 <b>걸어서</b> 간다 — 사람이 하는 그대로(키를 누른다).
{
	const until = Date.now() + 30000;
	while (Date.now() < until) {
		const me = await page.evaluate(() => (window.__wmView.dolls() || []).find((one) => one.isLocal) || null);
		if (me === null) { await wait(200); continue; }

		const away = Math.hypot(target.x - me.drawnX, target.z - me.drawnZ);
		if (away <= REACH * 0.6) break;

		// 세계의 +Z 가 앞이다(눈은 처음 방향 그대로 둔다).
		const keyX = target.x > me.drawnX + 0.2 ? 'd' : (target.x < me.drawnX - 0.2 ? 'a' : null);
		const keyZ = target.z > me.drawnZ + 0.2 ? 'w' : (target.z < me.drawnZ - 0.2 ? 's' : null);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.down(key);
		}

		await wait(120);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.up(key);
		}
	}
}

const meNow = await page.evaluate(() => (window.__wmView.dolls() || []).find((one) => one.isLocal) || null);
check('창도 그 자리까지 걸어갔다',
	meNow !== null && Math.hypot(target.x - meNow.drawnX, target.z - meNow.drawnZ) <= REACH,
	meNow === null ? '나를 못 찾았다' : `${Math.hypot(target.x - meNow.drawnX, target.z - meNow.drawnZ).toFixed(2)}m`);

// 봇이 <b>먼저</b> 줍는다(회선이 곧으니 늘 이긴다). 그 뒤 창이 같은 것을 누른다.
bot.send(JSON.stringify({ type: 'gather', nodeId: target.id }));
await wait(250);

await page.evaluate((nodeId) => {
	window.__wmView.socket().send(JSON.stringify({ type: 'gather', nodeId }));
}, target.id);

// ① 왜 안 됐는지 말해 주나 ② 없어진 것이 화면에서 사라지나
let told = [];
let stillThere = true;
{
	const until = Date.now() + 12000;
	while (Date.now() < until) {
		told = await page.evaluate(() => window.__wmDenied || []);
		const nowField = await page.evaluate(() => window.__wmView.field());
		stillThere = nowField.some((one) => one.id === target.id);
		if (told.length > 0 && stillThere === false) break;

		await wait(200);
	}
}

check('진 쪽에게 왜 안 됐는지 말해 준다', told.length > 0,
	told.length === 0 ? '아무 말도 없었다 — 사람은 「고장났다」로 읽는다' : JSON.stringify(told[0]));
// 「자라는 중」과 「방금 남이」는 사람이 겪는 일이 다르다 (TASK-WM-275) — 진 사람은 뒤엣말을 들어야 한다.
check('진 이유가 <b>남이 가져갔다</b>로 온다 (그냥 자라는 중이 아니라)',
	told.some((one) => String(one.why || '').includes('남이')),
	told.map((one) => one.why).join(' | ') || '아무 말도 없었다');
check('없어진 것이 화면에서도 사라진다 (유령이 안 남는다)', stillThere === false,
	stillThere ? '진 쪽 화면에는 아직 있다' : '사라졌다');
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

try { bot.close(); } catch { /* 이미 닫혔다 */ }
await browser.close();
await line.close();
killWorld();

if (failures === 0) {
	console.log('[web-contest] ✅ 나쁜 회선에서 겨루기에 져도 창이 이유를 말하고, 유령이 안 남는다');
	process.exit(0);
}

console.log(`\n[web-contest] RESULT: ${failures}건`);
process.exit(1);
