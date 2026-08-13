#!/usr/bin/env node
// wm-web-smoothness-test.mjs — 남이 걷는 게 <b>부드럽게</b> 보이나 (TASK-WM-223).
//
// ★ 왜: 지금까지 잰 것은 「얼마나 보냈나」(바이트·Hz)뿐이다. 그런데 사람이 느끼는 것은
//   <b>화면에 그려진 자리가 프레임마다 어떻게 움직였나</b>다. 서버가 20Hz 로 정확히 보내도
//   그 사이를 안 메우면 60fps 화면에서는 세 프레임 중 두 프레임이 <b>멎어 있다</b> — 툭툭 끊긴다.
//   그 자리는 서버 숫자로는 영영 안 보인다. 창 안에서 그려진 자리를 직접 봐야 한다.
//
// 재는 것: 봇 하나가 <b>일정한 속도로 곧게</b> 걷고, 진짜 창이 그걸 본다.
//   ① 멎은 프레임 비율 — 걷는 중인데 그려진 자리가 그대로인 프레임
//   ② 속도 흔들림 — 프레임마다의 속도가 얼마나 들쭉날쭉한가 (고르면 0 에 가깝다)
//   ③ 뒤처짐 — 그려진 자리가 세계가 아는 자리보다 얼마나 뒤에 있나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 부드럽다 · 1 = 끊긴다 · 2 = 못 돌림
//
// [빨강-확인] 사이 메우기(`follow`)를 빼고 세계가 준 자리로 딱딱 튀게 하니 2건 빨강 (2026-08-14):
//   「멎은 프레임」과 「속도 흔들림 1.49(기준 0.6)」 — 사람이 「툭툭 끊긴다」로 겪는 그 자리다.

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5397);
const url = `http://127.0.0.1:${port}/`;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-web-smooth-')), 'world.json');

/*
 * 기준 — 「사람이 안 느끼는가」를 숫자로 못박는다.
 *
 * 멎은 프레임: 서버 20Hz · 화면 60fps 면 안 메울 때 약 2/3 가 멎는다. 절반을 넘으면 확실히 끊긴다.
 *   메우면 0 에 가까워야 한다. 느린 기계(30fps)에서도 메우는 쪽은 안 멎으므로 판정은 안 흔들린다.
 * 속도 흔들림: 곧게 일정 속도로 걷는 상대다 — 그려진 속도도 고와야 한다.
 * 뒤처짐: 메우는 방식은 태생적으로 한 판만큼 뒤진다(그게 값이다). 한 판 = 3m/s × 50ms = 0.15m.
 *   넉넉히 잡아도 걸음 반 발짝(0.8m) 을 넘으면 「저 사람 어디 있지」가 된다.
 */
const MAX_FROZEN_FRAME_RATIO = 0.25;
const MAX_SPEED_WOBBLE = 0.6;
const MAX_LAG_METERS = 0.8;

function cannotRun(message) {
	console.error(`[web-smooth] CANNOT-RUN: ${message}`);
	process.exit(2);
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

async function waitHealthy(milliseconds) {
	const until = Date.now() + milliseconds;
	while (Date.now() < until) {
		try {
			const response = await fetch(`${url}health`, { headers: { connection: 'close' } });
			if (response.ok) return true;
		} catch { /* 아직 안 떴다 */ }
		await new Promise((done) => setTimeout(done, 400));
	}
	return false;
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-web-smooth-app-')), 'app');
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

if (await waitHealthy(120000) === false) {
	killWorld();
	cannotRun('세계가 안 떴다');
}

// ── 걷는 상대 하나 — 곧게, 일정한 속도로 (세계가 허락하는 걸음 속도 3m/s) ──────
const walker = new WebSocket(`ws://127.0.0.1:${port}/ws`);
walker.onopen = () => walker.send(JSON.stringify({ type: 'hello', secret: '' }));
walker.onerror = () => { /* 아래에서 잡힌다 */ };
await new Promise((done) => setTimeout(done, 2000));

const STEP_MS = 50;
const STEP_METERS = 0.15; // 3m/s — 세계가 허락하는 속도 (MoveAllowance, TASK-WM-222)
const walking = setInterval(() => {
	if (walker.readyState === 1) walker.send(JSON.stringify({ type: 'move', x: STEP_METERS, z: 0 }));
}, STEP_MS);

// ── 보는 창 ───────────────────────────────────────────────────────────
const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(url);
await page.waitForFunction(
	() => (document.getElementById('status')?.textContent || '').includes('붙었다'),
	null, { timeout: 30000 }).catch(() => { /* 아래 칸이 잡는다 */ });

const sawSeam = await page.evaluate(() => typeof window.__wmView === 'object');
if (sawSeam === false) {
	clearInterval(walking);
	await browser.close();
	killWorld();
	cannotRun('창이 시험용 창구(__wmView)를 안 연다 — 잴 수가 없다');
}

// 프레임마다 남의 그려진 자리를 적는다.
await page.evaluate(() => {
	window.__wmTrail = [];
	const write = () => {
		const others = window.__wmView.dolls().filter((one) => one.isLocal === false);
		if (others.length > 0) {
			const one = others[0];
			window.__wmTrail.push({ at: performance.now(), drawnX: one.drawnX, serverX: one.serverX });
		}
		requestAnimationFrame(write);
	};
	requestAnimationFrame(write);
});

await new Promise((done) => setTimeout(done, 4000));
const trail = await page.evaluate(() => window.__wmTrail.slice());

clearInterval(walking);
try { walker.close(); } catch { /* 이미 닫혔다 */ }
await browser.close();
killWorld();

// ── 셈 ────────────────────────────────────────────────────────────────
// ⚠ 프레임이 모자란 것은 <b>못 잰 것</b>이다 (관문 규율 ②) — 느린 기계에서는 같은 시간에 덜 그린다.
//   여기가 exit 1 이라 기계 사정이 제품 빨강으로 적히고 있었다 (2026-08-14).
console.log(`  ⓘ 창이 걷는 상대를 본 프레임 ${trail.length}장 (적어도 31장)`);
if (trail.length <= 30)
	cannotRun(`프레임이 ${trail.length}장뿐이다 — 이 기계에서는 매끄러움을 못 가른다`);

let frozen = 0;
let moving = 0;
const speeds = [];
let lagSum = 0;

for (let i = 1; i < trail.length; i += 1) {
	const spent = trail[i].at - trail[i - 1].at;
	if (spent <= 0) continue;

	const went = Math.abs(trail[i].drawnX - trail[i - 1].drawnX);
	moving += 1;
	if (went < 0.0005) frozen += 1;
	speeds.push(went / (spent / 1000));
	lagSum += Math.abs(trail[i].serverX - trail[i].drawnX);
}

const frozenRatio = frozen / Math.max(1, moving);
const mean = speeds.reduce((sum, one) => sum + one, 0) / Math.max(1, speeds.length);
const variance = speeds.reduce((sum, one) => sum + ((one - mean) ** 2), 0) / Math.max(1, speeds.length);
const wobble = mean > 0 ? Math.sqrt(variance) / mean : 999;
const lag = lagSum / Math.max(1, speeds.length);
const fps = (trail.length - 1) / ((trail[trail.length - 1].at - trail[0].at) / 1000);

check('걷는 중인데 멎은 프레임이 적다', frozenRatio <= MAX_FROZEN_FRAME_RATIO,
	`${(frozenRatio * 100).toFixed(1)}% (기준 ${MAX_FROZEN_FRAME_RATIO * 100}%)`);
check('그려진 속도가 고르다', wobble <= MAX_SPEED_WOBBLE,
	`흔들림 ${wobble.toFixed(2)} (기준 ${MAX_SPEED_WOBBLE})`);
check('그려진 자리가 많이 안 뒤처진다', lag <= MAX_LAG_METERS,
	`${lag.toFixed(2)}m (기준 ${MAX_LAG_METERS}m)`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

console.log(`  ⓘ 프레임 ${trail.length}장 · ${fps.toFixed(0)}fps · 평균 그려진 속도 ${mean.toFixed(2)}m/s (걷는 속도 3m/s)`);

if (failures === 0) {
	console.log('[web-smooth] ✅ 남이 걷는 게 부드럽다');
	process.exit(0);
}

console.log(`\n[web-smooth] RESULT: ${failures}건`);
process.exit(1);
