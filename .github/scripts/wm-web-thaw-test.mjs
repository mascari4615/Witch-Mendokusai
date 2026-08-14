#!/usr/bin/env node
// wm-web-thaw-test.mjs — <b>얼었다 녹은 창이 현재로 돌아오나</b> (TASK-WM-356).
//
// ★ 왜: 지하철에 들어갔다 나오는 30초 동안 줄은 <b>끊기지 않는다</b> — 세계는 그 줄로 계속 말하고,
//   그 말은 어딘가에 고여 있다가 녹는 순간 <b>한꺼번에</b> 창에 쏟아진다.
//   그때 창이 ① 고인 옛 판을 뒤늦게 그리거나 ② 그 무더기에 얹혀 멎거나 ③ 조용히 터질 수 있다.
//   끊김(WM-300)과는 다른 자리다: 거기서는 줄이 <b>끊겨</b> 새로 붙지만, 여기서는 같은 줄이 이어진다.
//
// 재는 것 (진짜 창 하나 · 봇 몇이 걷는 광장 · 회선을 30초 얼렸다 녹인다):
//   ① 녹은 뒤 창이 <b>현재</b>를 본다(남들이 다시 움직인다) ② 세계가 그 사람을 안 놓았다
//   ③ 창이 조용히 안 터졌다
//
// ⚠ 얼리는 시간은 세계가 놓아주는 시간(WM-355, 90초)보다 <b>짧아야</b> 한다 — 아니면 이건
//   「놓아주나」를 재는 것이 되고, 그건 다른 관문이다.
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 현재로 돌아온다 · 1 = 옛것에 머물거나 멎는다 · 2 = 못 돌림
//
// [빨강-확인] 놓아주는 시간(WM-355)을 얼리는 시간보다 짧게(10초) 두니 빨강 —
//   「그 사이 세계가 그 사람을 안 놓았다 — 놓아준 창 1개」. 즉 지하철 30초에 쫓겨나는 판이다.
//   이 관문이 지키는 것은 <b>두 규칙 사이의 약속</b>이다: 얼어붙은 창을 놓아주되(WM-355),
//   사람이 겪는 흔한 끊김(지하철·엘리베이터)에는 안 놓는다.
//
// ⓘ 「고인 옛 판이 현재를 덮나」도 걸어 봤다(지난 판 버리기를 꺼서) — 그래도 초록이었다:
//   창은 판을 <b>쌓아 두지 않고</b> 마지막 것만 그리기 때문이다(applyPendingWorld). 그 성함은 여기 적어 둔다.

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5580);
const linePort = worldPort + 1;

const CROWD = 12;
const FROZEN_SECONDS = 30;

/**
 * 녹은 뒤 이만큼 안에 현재로 돌아와야 한다.
 * [문턱-사유] (c) 사람이 느끼는 선 — 지하철에서 나와 <b>반 분</b>.
 * ⚠ 처음엔 10초로 뒀다가 CI(2코어)에서 거짓 빨강이 났다(2026-08-14: 「옛 자리에 머물렀다」) —
 *   느린 기계에서는 봇들이 덜 움직이고 창도 늦게 그린다. 사람이 참는 선 안에서 넉넉히 둔다.
 */
const BACK_WITHIN_MS = 30000;

function cannotRun(message) {
	console.error(`[녹은창] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-thaw-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-thaw-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll), env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore',
});

function killWorld() {
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
}

{
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}
	if (up === false) { killWorld(); cannotRun('세계가 안 떴다'); }
}

// 광장 — 봇들이 계속 움직여야 「현재로 돌아왔나」를 볼 수 있다(가만히 선 세계는 옛것과 같아 보인다).
const crowd = [];
for (let i = 0; i < CROWD; i += 1) {
	const socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onerror = () => { /* 수로 본다 */ };
	crowd.push(socket);
}

const milling = setInterval(() => {
	for (const socket of crowd) {
		if (socket.readyState === 1) socket.send(JSON.stringify({ type: 'move', x: 0.15, z: 0 }));
	}
}, 100);

await wait(2500);

const badLine = openBadLine({ listenPort: linePort, targetPort: worldPort });
await badLine.listen();

const browser = await chromium.launch();
const page = await browser.newPage();
const errors = [];
page.on('pageerror', (error) => errors.push(String(error)));
await page.goto(`http://127.0.0.1:${linePort}/`);

const painted = await page.waitForFunction(
	() => typeof window.__wmView === 'object' && window.__wmView.world().gatherables > 0,
	null, { timeout: 60000 }).then(() => true).catch(() => false);

if (painted === false) {
	clearInterval(milling);
	await browser.close();
	await badLine.close();
	killWorld();
	cannotRun('창이 안 떴다');
}

// ── 얼린다 ───────────────────────────────────────────────────────────────────
badLine.freeze();
await page.evaluate(() => {
	window.__before = Object.fromEntries(
		window.__wmView.dolls().map((doll) => [doll.id, { x: doll.serverX, z: doll.serverZ, mine: doll.isLocal }]));
});

await wait(FROZEN_SECONDS * 1000);

const whileFrozen = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

// ── 녹인다 ───────────────────────────────────────────────────────────────────
badLine.thaw();

const cameBack = await page.waitForFunction(() => {
	const before = window.__before || {};
	let moved = 0;
	for (const doll of window.__wmView.dolls()) {
		const was = before[doll.id];
		if (!was || was.mine) continue;
		if (Math.hypot(doll.serverX - was.x, doll.serverZ - was.z) > 1.0) moved += 1;
	}

	return moved >= 3;
}, null, { timeout: BACK_WITHIN_MS }).then(() => true).catch(() => false);

const after = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());
const seen = await page.evaluate(() => ({ ...window.__wmView.world() }));

clearInterval(milling);
for (const socket of crowd) { try { socket.close(); } catch { /* 이미 */ } }
await browser.close();
await badLine.close();
killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ ${FROZEN_SECONDS}초 얼림 · 그때 세계가 센 사람 ${whileFrozen.people}명`
	+ ` → 녹은 뒤 ${after.people}명 · 창이 그린 사람 ${seen.dolls}명 · 놓아준 창 ${after.letGoOfFrozen}개`);

check(`녹은 뒤 ${BACK_WITHIN_MS / 1000}초 안에 현재를 본다`, cameBack,
	cameBack ? '남들이 다시 움직였다' : '옛 자리에 머물렀다');

// ★ 90초 전에는 세계가 그 사람을 놓지 않아야 한다 (WM-355) — 지하철 30초에 쫓겨나면 그게 더 나쁘다.
check('그 사이 세계가 그 사람을 안 놓았다', after.letGoOfFrozen === 0,
	`놓아준 창 ${after.letGoOfFrozen}개`);

check('창이 조용히 안 터졌다', errors.length === 0, errors.slice(0, 2).join(' | ') || '오류 없음');

if (bad === 0) {
	console.log('[녹은창] ✅ 얼었다 녹은 창이 현재로 돌아온다');
	process.exit(0);
}

console.log(`\n[녹은창] RESULT: ${bad}건`);
process.exit(1);
