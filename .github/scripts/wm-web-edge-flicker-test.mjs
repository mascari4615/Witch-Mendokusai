#!/usr/bin/env node
// wm-web-edge-flicker-test.mjs — <b>진짜 창</b>에서 멀어지는 사람이 깜빡이나 (TASK-WM-395).
//
// ★ 왜: WM-394 가 세계 쪽을 쟀다 — 넉넉히 보내니 세계는 안 깜빡인다. 남은 물음은 <b>창</b>이다.
//   창은 세계가 준 사람을 그린다. 멀어지는 사람이 목록에서 <b>들락날락</b>하면 그 사람은
//   눈앞에서 나타났다 사라졌다 한다(그리고 그때마다 인형을 만들었다 버린다).
//
// 재는 것: 창 하나가 보는 앞에서 봇 한 명이 <b>사라지는 언저리</b>를 여섯 번 오간다.
//   ① 오가는 동안 그린 인형이 <b>몇 번 나타났다 사라졌나</b>(뒤집힘)
//   ② 창이 조용히 안 터졌나
//
// 문턱: 뒤집힘은 왕복마다 <b>한 번씩</b>까지가 정상이다(들어오면 보이고 나가면 사라진다).
//   그보다 잦으면 그건 <b>같은 자리에서</b> 켜졌다 꺼지는 것이다 = 깜빡임.
//
// [빨강-확인] <b>정직하게 — 빨갛게 못 만들었다.</b> (2026-08-14)
//   두 번 시도했다: ① 창이 「이번 판에 안 온 사람 = 없는 사람」으로 치게 고쳐 봤다 →
//   서 있는 사람은 <b>한 번 사라지고 끝</b>(뒤집힘 0, 초록) · 오가는 사람은 늘 판에 실려 안 지워졌다.
//   ② 창 쪽 반경을 좁혀 봐도 오갈 때의 뒤집힘 수는 그대로였다(들어올 때 한 번·나갈 때 한 번).
//   ⇒ 이 자가 잡는 것은 <b>「같은 자리에서 켜졌다 꺼지는 것」</b> 하나뿐이고, 그 꼴을 이 무대에서
//   인위로 못 만들었다. 그래서 「빨개지는 것을 본 초록」이 아니다 — 그 사실을 여기 적어 둔다(규율 ⑦).
//   실측 초록: 서 있는 사람 뒤집힘 0 · 오가는 사람 11(정상 12 + 여유 4).
//
// exit: 0 = 안 깜빡인다 · 1 = 깜빡인다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5524);
const LAPS = 6;

const cannotRun = (m) => { console.error(`[창깜빡임] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
let failures = 0;
const check = (what, ok, detail) => { if (ok === false) failures += 1; console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`); };

let chromium;
try {
	const root = process.env.WM_PLAYWRIGHT_ROOT;
	const req = createRequire(root ? join(resolve(root), 'package.json') : import.meta.url);
	chromium = req('playwright').chromium;
} catch (error) {
	cannotRun(`playwright 를 못 찾았다 — ${error.message} (WM_PLAYWRIGHT_ROOT 로 알려 준다)`);
}
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-flick2-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-flick2-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll), env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore',
});
const killWorld = () => {
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 */ }
};

{
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try { const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }); if (answer.ok) { up = true; break; } } catch { /* 아직 */ }
		await wait(300);
	}
	if (up === false) { killWorld(); cannotRun('세계가 안 떴다'); }
}

// ── 지켜보는 진짜 창 ──────────────────────────────────────────────────
const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error.message)));
await page.goto(`http://127.0.0.1:${port}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래가 잡는다 */ });
await page.waitForFunction(() => (window.__wmView.dolls() || []).some((one) => one.isLocal), null, { timeout: 30000 })
	.catch(() => { /* 아래가 잡는다 */ });

const meAt = await page.evaluate(() => {
	const me = (window.__wmView.dolls() || []).find((one) => one.isLocal);
	return me ? { x: me.serverX, z: me.serverZ } : null;
});
if (meAt === null) { await browser.close(); killWorld(); cannotRun('창에 내 인형이 안 왔다'); }

// ── 멀어지는 봇 ───────────────────────────────────────────────────────
const walker = { id: null, x: 0 };
walker.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
walker.socket.onopen = () => walker.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
walker.socket.onerror = () => { /* 아래가 잡는다 */ };
walker.socket.onmessage = (event) => {
	let said;
	try { said = JSON.parse(String(event.data)); } catch { return; }
	if (said.type === 'welcome') walker.id = said.id;
	for (const d of (said.dolls || [])) if (d.id === walker.id) walker.x = d.x;
	if (said.beat !== undefined && walker.socket.readyState === 1) walker.socket.send(JSON.stringify({ type: 'beat', beat: said.beat }));
};
{
	const until = Date.now() + 30000;
	while (Date.now() < until && walker.id === null) await wait(100);
	if (walker.id === null) { await browser.close(); killWorld(); cannotRun('봇이 못 들어갔다'); }
}
const send = (m) => { if (walker.socket.readyState === 1) walker.socket.send(JSON.stringify(m)); };

/** 창이 그 사람을 지금 그리고 있나. */
const drawn = () => page.evaluate((id) => (window.__wmView.dolls() || []).some((one) => one.id === id), walker.id);

// ★ <b>사라지는 자리</b>를 찾아 간다 — 자리를 못 박지 않는다(세계가 넉넉히 보내는 폭은 규칙이 정한다).
let vanishedAt = null;
{
	const until = Date.now() + 180000;
	let step = 0;
	while (Date.now() < until && vanishedAt === null) {
		send({ type: 'move', x: 0.15, z: 0, seq: step += 1 });
		await wait(60);
		if (step % 10 !== 0) continue;
		if (await drawn() === false) vanishedAt = walker.x;
	}
}
if (vanishedAt === null) {
	await browser.close();
	killWorld();
	cannotRun(`걸어 나가도 그 사람이 창에서 안 사라졌다 (x ${walker.x?.toFixed(1)}) — 이 무대로는 못 잰다`);
}
console.log(`  ⓘ 창이 보는 사람은 x ${meAt.x.toFixed(1)} · 그 사람이 사라진 자리 x ${vanishedAt.toFixed(1)}`);

// ── ① <b>그 자리에 서서</b> 본다 — 안 움직이는 사람은 켜졌다 꺼지면 안 된다 ─────────
//   이게 진짜 깜빡임 자다: 오갈 때의 뒤집힘은 <b>정상</b>(들어오면 보이고 나가면 사라진다)이라
//   문턱을 아슬아슬하게 만든다(실측 11 vs 한도 12). 서 있는 사람의 뒤집힘은 <b>0 이어야 한다</b>.
let standingFlips = 0;
{
	let seen = await drawn();
	const until = Date.now() + 8000;
	while (Date.now() < until) {
		await wait(250);
		const now = await drawn();
		if (now !== seen) { standingFlips += 1; seen = now; }
	}
}

check('서 있는 사람은 켜졌다 꺼지지 않는다 (8초)', standingFlips === 0, `뒤집힘 ${standingFlips}번`);

// ── ② 그 언저리를 여섯 번 오간다 ──────────────────────────────────────
let was = await drawn();
let flips = 0;
for (let lap = 0; lap < LAPS; lap += 1) {
	for (const way of [-0.15, 0.15]) {
		for (let step = 0; step < 30; step += 1) {
			send({ type: 'move', x: way, z: 0, seq: 10000 + lap * 200 + (way < 0 ? 0 : 100) + step });
			await wait(50);
			if (step % 5 !== 0) continue;

			const now = await drawn();
			if (now !== was) { flips += 1; was = now; }
		}
	}
}

// 오갈 때는 <b>왕복마다 둘</b>이 정상이다(들어올 때·나갈 때). 여유를 넷 둔다 —
// 걸음은 세계가 속도를 재서 깎으므로 어떤 왕복은 선을 두 번 스칠 수 있다(실측 11/12 로 아슬아슬했다).
check(`오갈 때마다 한 번씩만 나타났다 사라진다 (왕복 ${LAPS}번)`, flips <= LAPS * 2 + 4,
	`뒤집힘 ${flips}번 (정상 ${LAPS * 2}번 + 여유 4)`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

walker.socket.close();
await browser.close();
killWorld();

if (failures === 0) {
	console.log('[창깜빡임] ✅ 멀어지는 사람이 깜빡이지 않는다');
	process.exit(0);
}
console.log(`\n[창깜빡임] RESULT: ${failures}건`);
process.exit(1);
