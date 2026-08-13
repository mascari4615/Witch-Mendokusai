#!/usr/bin/env node
// wm-honest-budget-test.mjs — <b>성한 창이 말 예산에 걸리지는 않나</b> (TASK-WM-348).
//
// ★ 왜: 예산(WM-218)은 초당 30 마디다. 그런데 진짜 창이 보내는 것을 세어 보면 걸음 + 숨소리 +
//   <b>답 안 온 걸음 되굴리기</b>(WM-271)가 겹친다 — 회선이 늦을수록 되굴림이 는다.
//   넘긴 말은 <b>조용히 버려진다</b>(끊지 않는다). 그 조용함이 이 관문이 있는 이유다:
//   성한 사람이 「가끔 안 먹히는 게임」을 겪어도 아무 데도 안 적힌다.
//
// 재는 것 (나쁜 회선 너머 진짜 창 하나가 12초를 걷는다):
//   ① 세계가 그 창의 말을 <b>하나도 안 버렸나</b> ② 그 창의 말수가 예산의 얼마나 되나
//   (여유가 얇으면 지금은 초록이어도 다음 기능 하나에 무너진다 — 수치를 늘 찍어 둔다)
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 성한 창은 안 버려진다 · 1 = 버려진다 · 2 = 못 돌림
//
// [빨강-확인] 손짓 몫(ROOM_FOR_DOING)을 0 으로 두니 빨강 — 「남는 몫 0.1마디」(2026-08-14).
//   고치기 전 예산 30 에서는 남는 몫이 6.1 이었다(걷기만으로 80% 를 먹었다).

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5430);
const linePort = worldPort + 1;

/** 늦은 회선일수록 <b>되굴린 걸음</b>이 는다 — 예산이 가장 아슬아슬해지는 자리다. */
const ONE_WAY_MS = 200;
const JITTER_MS = 40;
const WALK_SECONDS = 12;

/** 세계가 정한 말 예산 (MessageBudget.REFILL_PER_SECOND = 걸음 20 + 숨소리 4 + 손짓 16). */
const BUDGET_PER_SECOND = 40;

/**
 * 사람이 <b>손으로 하는 일</b>에 남아 있어야 하는 몫.
 * [문턱-사유] (b) 제품 상수와의 견줌 — MessageBudget.ROOM_FOR_DOING 의 절반 위.
 *   여기가 얇으면 지금 초록이어도 줍기 한 번에 성한 사람의 말이 조용히 버려진다.
 */
const ROOM_LEFT_PER_SECOND = 8;

function cannotRun(message) {
	console.error(`[honest-budget] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-hb-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-hb-')), 'world.json');
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

const badLine = openBadLine({ listenPort: linePort, targetPort: worldPort, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS });
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
	await browser.close();
	await badLine.close();
	killWorld();
	cannotRun('창이 안 떴다');
}

// 사람처럼 걷는다 — 걸음 · 숨소리 · 되굴림이 다 나가는 자리다.
await page.evaluate(() => {
	// 무엇을 얼마나 말하는지 갈라 둔다 — 「말이 많다」만으로는 어디를 줄일지 모른다.
	window.__said = {};
	const socket = window.__wmView.socket();
	const was = socket.send.bind(socket);
	socket.send = (text) => {
		try {
			const kind = JSON.parse(String(text)).type || '?';
			window.__said[kind] = (window.__said[kind] || 0) + 1;
		} catch (e) { /* 딴 것 */ }
		return was(text);
	};
	window.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }));
	window.dispatchEvent(new KeyboardEvent('keydown', { key: 'w' }));
});

await wait(WALK_SECONDS * 1000);

const byKind = await page.evaluate(() => window.__said || {});
const lines = await fetch(`http://127.0.0.1:${worldPort}/lines`, { headers: { connection: 'close' } })
	.then((one) => one.json()).catch(() => ({ lines: [] }));

await browser.close();
await badLine.close();
killWorld();

const mine = lines.lines[0];
if (mine === undefined) cannotRun('창의 줄 장부를 못 읽었다');

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

const spokePerSecond = mine.heard / WALK_SECONDS;
console.log(`  ⓘ 회선 왕복 ${ONE_WAY_MS * 2}ms · ${WALK_SECONDS}초 — 창이 한 말 ${mine.heard}마디`
	+ ` (초당 ${spokePerSecond.toFixed(1)}) · 예산 초당 ${BUDGET_PER_SECOND}`
	+ ` · 여유 ${Math.round((1 - spokePerSecond / BUDGET_PER_SECOND) * 100)}%`);
console.log(`  ⓘ 무엇을 말했나 — ${Object.entries(byKind).map(([kind, count]) => `${kind} ${count}`).join(" · ")}`);

check('세계가 성한 창의 말을 하나도 안 버렸다', mine.dropped === 0, `버린 마디 ${mine.dropped}개`);

// ★ 지금 초록이어도 <b>여유</b>가 얇으면 다음 기능 하나에 무너진다.
check(`손으로 할 몫이 초당 ${ROOM_LEFT_PER_SECOND}마디는 남는다`,
	BUDGET_PER_SECOND - spokePerSecond >= ROOM_LEFT_PER_SECOND,
	`걷기만으로 초당 ${spokePerSecond.toFixed(1)} · 남는 몫 ${(BUDGET_PER_SECOND - spokePerSecond).toFixed(1)}`);

check('창이 조용히 안 터졌다', errors.length === 0, errors.slice(0, 2).join(' | ') || '오류 없음');

if (bad === 0) {
	console.log('[honest-budget] ✅ 성한 창은 예산에 안 걸린다');
	process.exit(0);
}

console.log(`\n[honest-budget] RESULT: ${bad}건`);
process.exit(1);
