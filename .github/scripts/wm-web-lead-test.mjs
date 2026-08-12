#!/usr/bin/env node
// wm-web-lead-test.mjs — <b>앞질러 그리기가 얼마나 앞서나</b> (TASK-WM-270).
//
// ★ 왜: 내 화면의 나는 세계의 답을 안 기다리고 먼저 간다(앞질러 그리기). 그래서 <b>앞서 있는 것이
//   정상</b>이다 — 회선이 늦은 만큼 앞선다. 그런데 그 앞섬이 「도로 끌어오는 거리」(4m)를 넘으면
//   창은 나를 <b>뒤로 끌어당긴다</b> — 사람은 그걸 「가끔 튄다」로 느낀다.
//
//   그러니 이 설계가 <b>어느 회선까지 버티나</b>는 잴 수 있는 값이다: 앞섬 ≈ 왕복시간 × 걸음속도.
//   지금까지 이 자리는 한 번도 안 쟀다(앞섬을 세는 자리조차 없었다).
//
// 재는 것: 같은 걸음을 두 회선에서 — 보통(왕복 200ms) / 아주 나쁨(왕복 800ms).
//   ① 앞섬이 셈과 맞나 ② 보통 회선에서 <b>도로 끌려가지 않나</b> ③ 창이 안 터지나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 버틴다 · 1 = 못 버틴다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5530);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-lead-')), 'world.json');

const WALK_SPEED = 3;              // walk.mjs 와 같은 값
const SNAP_DISTANCE = 4;           // 창의 POSITION_TELEPORT_DISTANCE
const WALK_FOR_MS = 6000;

function cannotRun(message) {
	console.error(`[web-lead] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-lead-app-')), 'app');
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

const browser = await chromium.launch();

/** 그 회선으로 붙어 한참 걷고, <b>앞섬</b>과 <b>도로 끌려간 횟수</b>를 받아 온다. */
async function walkOn(oneWayMs, listenPort, label) {
	const line = openBadLine({ listenPort, targetPort: port, latencyMs: oneWayMs, jitterMs: 10 });
	await line.listen();

	const page = await browser.newPage();
	const errors = [];
	page.on('pageerror', (error) => errors.push(String(error)));

	await page.goto(`http://127.0.0.1:${listenPort}/`);
	await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
		.catch(() => { /* 아래에서 잡힌다 */ });

	// 붙자마자의 출렁임은 안 센다 — 도는 중을 본다.
	await wait(1500);
	await page.evaluate(() => window.__wmView.forgetLead());

	const until = Date.now() + WALK_FOR_MS;
	while (Date.now() < until) {
		await page.keyboard.down('w');
		await wait(400);
		await page.keyboard.up('w');
		await wait(60);
	}

	const lead = await page.evaluate(() => window.__wmView.lead());
	await page.close();
	await line.close();

	console.log(`  ⓘ ${label}: 가장 많이 앞선 거리 ${lead.worst.toFixed(2)}m`
		+ ` · 도로 끌려간 횟수 ${lead.snapped}번 (셈으로는 ${(oneWayMs * 2 / 1000 * WALK_SPEED).toFixed(2)}m)`);

	return { ...lead, errors };
}

const usual = await walkOn(100, port + 1, '보통 회선(왕복 200ms)');
const awful = await walkOn(400, port + 2, '아주 나쁜 회선(왕복 800ms)');

// ① 앞섬은 <b>셈과 맞아야</b> 한다 — 안 맞으면 앞질러 그리기가 딴 셈을 쓰고 있는 것이다.
const expected = 0.2 * WALK_SPEED;
check('앞섬이 회선 지연 × 걸음속도 언저리다', usual.worst >= expected * 0.4 && usual.worst <= expected * 3.5,
	`${usual.worst.toFixed(2)}m · 셈 ${expected.toFixed(2)}m`);

// ② 보통 회선에서는 <b>도로 끌려가면 안 된다</b> — 그게 사람이 「튄다」고 느끼는 순간이다.
check('보통 회선에서는 도로 끌려가지 않는다', usual.snapped === 0, `${usual.snapped}번`);

// ③ 앞섬은 회선에 <b>비례해</b> 커진다 — 그러니 「어느 회선부터 끌려가나」를 셈해 적는다.
//   그 값이 이 설계의 한계다(숨기지 않고 적어 둔다). 폭주(회선보다 훨씬 빨리 벌어짐)만 막는다.
const slope = (awful.worst - usual.worst) / 0.6;
const base = usual.worst - (slope * 0.2);
const breaksAtMs = slope > 0 ? Math.round(((SNAP_DISTANCE - base) / slope) * 1000) : 0;

console.log(`  ⓘ 앞섬 ≈ ${base.toFixed(2)}m + ${slope.toFixed(2)}m/s × 왕복시간`
	+ ` → 왕복 <b>${breaksAtMs}ms</b> 부터 도로 끌려간다 (끌어오는 거리 ${SNAP_DISTANCE}m)`);

check('아주 나쁜 회선에서도 앞섬이 폭주하지 않는다 (회선의 두 배 안쪽)',
	awful.worst <= usual.worst * 3, `${awful.worst.toFixed(2)}m · 보통 회선 ${usual.worst.toFixed(2)}m`);
check('사람이 흔히 쓰는 회선(왕복 300ms)까지는 안 끌려간다', breaksAtMs >= 300,
	`왕복 ${breaksAtMs}ms 부터 끌려간다`);

check('창이 조용히 안 터졌다', usual.errors.length === 0 && awful.errors.length === 0,
	[...usual.errors, ...awful.errors].join(' | ') || '오류 없음');

await browser.close();
killWorld();

if (failures === 0) {
	console.log('[web-lead] ✅ 앞질러 그리기가 회선만큼만 앞서고, 도로 끌려가지 않는다');
	process.exit(0);
}

console.log(`\n[web-lead] RESULT: ${failures}건`);
process.exit(1);
