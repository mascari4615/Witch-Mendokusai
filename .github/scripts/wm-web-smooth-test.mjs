#!/usr/bin/env node
// wm-web-smooth-test.mjs — <b>남의 움직임이 나쁜 회선에서 매끄러운가</b> (TASK-WM-304).
//
// ★ 왜: 세계는 초당 20번 말하고 화면은 60번 그린다. 그 사이를 어떻게 메우느냐가 「이 게임 끊긴다」
//   느낌의 대부분이다. 그런데 지금까지의 관문은 전부 <b>내 걸음</b>(앞질러 그리기·화해)만 쟀다 —
//   정작 화면의 대부분을 차지하는 <b>남</b>이 어떻게 움직이는지는 아무도 안 봤다.
//
// 재는 것: 꾸준히 걷는 사람 하나를 나쁜 회선 너머 창에서 <b>매 프레임</b> 지켜보고
//   ① 한 프레임에 가장 크게 건너뛴 거리 ② 아예 멎어 있던 프레임 비율 ③ 가장 길게 멎어 있던 시간.
//
// 문턱은 <b>걸음 한 판</b>(3m/s × 50ms = 0.15m)의 배수다 — 절대 미터가 아니라 이 세계의 걸음에
// 매인 값이라 기계가 느려도 뜻이 안 변한다.
//
// 실측 기준선 (2026-08-13, 지연 100ms·유실 2%): 도약 0.27m · 멎음 1% · 가장 길게 80ms.
//
// ⚠ 이 관문이 지키는 것은 <b>지금 방식</b>(최신 자리로 다가가기)이다. 보간(받은 두 자리 사이 잇기)으로
//   바꿔 봤다가 <b>같은 판</b>을 놓고 나란히 재니 모든 항목에서 졌다 — 도약 0.81m · 멎음 8% ·
//   뒤처짐 147ms (다가가기는 0.27m · 1% · 49ms). 그래서 안 바꿨다. 다시 시도할 사람은 이 관문으로
//   먼저 재라.
//
// exit: 0 = 매끄럽다 · 1 = 튄다/멎는다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5540);
const linePort = worldPort + 1;

const ONE_WAY_MS = 100;
const JITTER_MS = 30;
const LOSS_PERCENT = 2;

/** 이 세계의 한 걸음 (m) — 3m/s 로 50ms. 문턱은 전부 이것의 배수다. */
const ONE_STEP_M = 0.15;

/** 한 프레임에 이보다 크게 건너뛰면 사람 눈에 <b>순간이동</b>이다 (기준선 1.8걸음). */
const MOST_JUMP_M = ONE_STEP_M * 4;

/** 아예 멎어 있던 프레임이 이보다 많으면 <b>끊겨</b> 보인다 (기준선 1%). */
const MOST_STILL_PERCENT = 5;

/** 한 번에 이보다 오래 멎으면 사람이 알아챈다 (기준선 80ms). */
const MOST_STILL_MS = 250;

function cannotRun(message) {
	console.error(`[web-smooth] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-smooth-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-smooth-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
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

	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다');
	}
}

const badLine = openBadLine({
	listenPort: linePort, targetPort: worldPort,
	latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS, lossPercent: LOSS_PERCENT,
});
await badLine.listen();

// 꾸준히 걷는 사람 — 이 사람 쪽 회선은 이 시험의 관심 밖이라 곧은 회선으로 붙는다.
const walker = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
let walkerId = null;
walker.onopen = () => walker.send(JSON.stringify({ type: 'hello', secret: '' }));
walker.onerror = () => { /* 아래가 잡는다 */ };
walker.onmessage = (event) => {
	try {
		const said = JSON.parse(event.data);
		if (said.type === 'welcome') walkerId = said.id;
	} catch { /* 우리 말이 아니다 */ }
};

await wait(2500);
if (walkerId === null) {
	badLine.close();
	killWorld();
	cannotRun('걷는 사람이 세계에 못 들어갔다');
}

let step = 0;
const walking = setInterval(() => {
	step += 1;
	if (walker.readyState === 1) walker.send(JSON.stringify({ type: 'move', x: ONE_STEP_M, z: 0, seq: step }));
}, 50);

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(`http://127.0.0.1:${linePort}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래가 잡는다 */ });

// ⚠ 재기 전에 <b>잴 것이 왔는지</b>부터 (domain-wm.md § 관문 규율).
const sawWalker = await page.waitForFunction(
	(id) => window.__wmView.dolls().some((one) => one.id === id), walkerId, { timeout: 30000 })
	.then(() => true).catch(() => false);

if (sawWalker === false) {
	clearInterval(walking);
	await browser.close();
	badLine.close();
	killWorld();
	cannotRun('창이 걷는 사람을 아예 못 봤다 — 이 상태로는 매끄러움을 잴 수 없다');
}

await page.evaluate((id) => {
	window.__frames = [];
	const watch = () => {
		const one = window.__wmView.dolls().find((doll) => doll.id === id);
		if (one) window.__frames.push({ at: performance.now(), x: one.drawnX });

		requestAnimationFrame(watch);
	};

	requestAnimationFrame(watch);
}, walkerId);

await wait(15000);
clearInterval(walking);
const frames = await page.evaluate(() => window.__frames);
await browser.close();
badLine.close();
killWorld();

if (frames.length < 300)
	cannotRun(`프레임이 너무 적다 (${frames.length}개) — 이 기계에서는 매끄러움을 못 가른다`);

let biggestJump = 0;
let still = 0;
let longestStill = 0;
let stillSince = null;
let walked = 0;
for (let i = 1; i < frames.length; i += 1) {
	const jump = Math.abs(frames[i].x - frames[i - 1].x);
	walked += jump;
	if (jump > biggestJump) biggestJump = jump;

	if (jump < 0.0005) {
		still += 1;
		if (stillSince === null) stillSince = frames[i - 1].at;
		if (frames[i].at - stillSince > longestStill) longestStill = frames[i].at - stillSince;
	} else {
		stillSince = null;
	}
}

if (walked < 5)
	cannotRun(`그 사람이 화면에서 거의 안 움직였다 (${walked.toFixed(1)}m) — 잴 것이 없다`);

const stillPercent = 100 * still / (frames.length - 1);
const seconds = (frames[frames.length - 1].at - frames[0].at) / 1000;
console.log(`  ⓘ 회선 ${ONE_WAY_MS}ms · 유실 ${LOSS_PERCENT}% — 프레임 ${frames.length}개 / ${seconds.toFixed(1)}초`
	+ ` · 화면에서 ${walked.toFixed(1)}m 걸었다`);

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

check('한 프레임에 순간이동하지 않는다', biggestJump <= MOST_JUMP_M,
	`가장 큰 도약 ${biggestJump.toFixed(3)}m (한 걸음 ${ONE_STEP_M}m · 한도 ${MOST_JUMP_M.toFixed(2)}m · 기준선 0.27m)`);
check('멎어 있는 프레임이 드물다', stillPercent <= MOST_STILL_PERCENT,
	`${stillPercent.toFixed(0)}% (한도 ${MOST_STILL_PERCENT}% · 기준선 1%)`);
check('한 번에 오래 멎지 않는다', longestStill <= MOST_STILL_MS,
	`가장 길게 ${longestStill.toFixed(0)}ms (한도 ${MOST_STILL_MS}ms · 기준선 80ms)`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

if (failures === 0) {
	console.log('[web-smooth] ✅ 나쁜 회선에서도 남의 걸음이 매끄럽다');
	process.exit(0);
}

console.log(`\n[web-smooth] RESULT: ${failures}건`);
process.exit(1);
