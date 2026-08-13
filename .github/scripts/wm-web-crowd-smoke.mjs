#!/usr/bin/env node
// wm-web-crowd-smoke.mjs — 사람이 몰려도 웹 창이 버티나 (TASK-WM-217 § 규모).
//
// ★ 왜: 「MMO 로 만들자」고 했는데 지금까지 잰 것은 <b>둘</b>이었다. 둘이 되는 것과
//   마흔이 되는 것은 다른 일이다 — 알림이 사람 수에 비례해 부풀면 창이 먼저 죽는다.
//   그 자리는 눈으로 못 본다(내 화면은 늘 나 혼자 열어 보니까). 그래서 기계가 몰아넣는다.
//
// 재는 것: 봇 여럿을 진짜 소켓으로 붙여 실제로 걷게 하고, 그 사이 진짜 창을 열어
//   ① 창이 사람들을 본다 ② 화면이 계속 그려진다(프레임) ③ 알림이 사람 수만큼 부풀지 않는다
//   ④ 창이 조용히 안 터진다 — 를 <b>수치로</b> 남긴다.
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 버틴다 · 1 = 못 버틴다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5396);
const url = `http://127.0.0.1:${port}/`;
const crowd = Number(process.env.WM_CROWD || 40);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-web-crowd-')), 'world.json');

/*
 * 버티는 기준 — 숫자로 못박되, **이 기계가 낼 수 있는 것**과 견준다 (2026-08-12).
 *
 * ★ 처음엔 「20fps 이상」이라는 절대값이었다. 그건 제품 주장이 아니라 **환경 주장**이라
 *   공유 러너(2코어 VM)에서 태어날 때부터 빨갰다 — 두 판 다 실패(18.9fps · 15.9fps).
 *   한 번도 초록인 적 없는 게이트는 사람이 곧 꺼 버린다.
 *
 * 이 검사가 진짜로 묻는 것은 「사람이 몰려도 버티나」다. 그러니 **같은 기계·같은 창**이
 *   한산할 때 낸 fps 를 먼저 재고, 사람이 몰린 뒤 그 몇 할을 지키는지 본다.
 *   기계가 느리면 둘 다 같이 느려지므로 판정은 흔들리지 않는다.
 *   바닥값도 같이 둔다 — 한산할 때부터 죽어 있으면 비율은 예쁘게 나오기 때문이다.
 */
const MIN_CROWD_KEEP_RATIO = 0.5;      // 한산할 때의 절반은 지켜야 한다
const MIN_FRAMES_PER_SECOND = 8;       // 그 아래는 비율과 무관하게 「안 그려진다」
const MAX_BYTES_PER_SECOND = 400000;   // 20Hz × 20KB. 이보다 크면 사람 수에 비례해 부푸는 중이다.

function cannotRun(message) {
	console.error(`[web-crowd] CANNOT-RUN: ${message}`);
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

function buildWorld() {
	// publish 다 — 배포가 쓰는 그 모양이어야 창(wwwroot)이 같이 실린다.
	const out = join(mkdtempSync(join(tmpdir(), 'wm-web-crowd-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	return join(out, 'WM.Server.dll');
}

function startWorld(dll) {
	world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: worldFile },
		stdio: 'ignore',
	});
}

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
	dll = buildWorld();
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

startWorld(dll);
if (await waitHealthy(120000) === false) {
	killWorld();
	cannotRun('세계가 안 떴다');
}

// ── 봇을 몰아넣는다 — 진짜 소켓으로 붙어 진짜로 걷는다 ─────────────────
const bots = [];
for (let i = 0; i < crowd; i += 1) {
	const socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onerror = () => { /* 붙다 실패한 놈은 아래 숫자에서 빠진다 */ };
	bots.push(socket);
}

await new Promise((done) => setTimeout(done, 3000));

/* 걷기는 아직 시작하지 않는다 — 창을 열고 **한산할 때**를 먼저 재야 견줄 것이 생긴다. */
const startWalking = () => setInterval(() => {
	for (const socket of bots) {
		if (socket.readyState !== 1) continue;
		socket.send(JSON.stringify({
			type: 'move',
			x: (Math.random() - 0.5) * 2,
			z: (Math.random() - 0.5) * 2,
		}));
	}
}, 200);

const joined = await fetch(`${url}health`, { headers: { connection: 'close' } }).then((r) => r.json());
check(`봇 ${crowd}명이 세계에 들어갔다`, joined.people >= crowd, `세계가 세는 사람 ${joined.people}명`);

// ── 그 한복판에 진짜 창을 연다 ─────────────────────────────────────────
const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

// 창이 받는 것을 창 안에서 직접 센다 — 서버가 「보냈다」고 하는 것 말고 <b>도착한 것</b>.
await page.addInitScript(() => {
	window.__wmSeen = { messages: 0, bytes: 0, frames: 0, since: 0 };
	const RealSocket = window.WebSocket;
	window.WebSocket = function (...args) {
		const socket = new RealSocket(...args);
		socket.addEventListener('message', (event) => {
			window.__wmSeen.messages += 1;
			window.__wmSeen.bytes += (event.data || '').length;
		});
		return socket;
	};
	window.WebSocket.prototype = RealSocket.prototype;
	Object.assign(window.WebSocket, RealSocket);

	const tick = () => { window.__wmSeen.frames += 1; requestAnimationFrame(tick); };
	requestAnimationFrame(tick);
});

// ★ 「들어가서 세계가 보이기까지」 (TASK-WM-220) — 사람이 가장 먼저 겪는 것이다.
//   사람이 몰린 세계일수록 이 한 장이 커지고(들어올 때는 통째로 온다), 그러다 조용히
//   몇십 초가 되는 자리다. 절대 시간은 기계마다 다르니 <b>사람이 포기하는 선</b>으로만 자른다.
const cameIn = Date.now();
await page.goto(url);
await page.waitForFunction(
	() => (document.getElementById('status')?.textContent || '').includes('붙었다'),
	null, { timeout: 30000 }).catch(() => { /* 아래 칸이 잡는다 */ });

// 세계가 실제로 <b>보이기</b>까지 — 사람 수가 화면에 뜨면 그림이 선 것이다.
await page.waitForFunction(
	() => /\d/.test(document.getElementById('peers')?.textContent || ''),
	null, { timeout: 30000 }).catch(() => { /* 아래 칸이 잡는다 */ });
const openedInMilliseconds = Date.now() - cameIn;

// ── ① 한산할 때 — 사람은 다 들어와 있지만 아무도 안 움직인다. 이 기계의 기준선이다.
const reset = () => page.evaluate(() => { window.__wmSeen = { ...window.__wmSeen, messages: 0, bytes: 0, frames: 0, since: Date.now() }; });
const read = () => page.evaluate(() => ({ ...window.__wmSeen, spent: Date.now() - window.__wmSeen.since }));
await reset();
await new Promise((done) => setTimeout(done, 2000));
const idle = await read();
const idleFps = idle.frames / (idle.spent / 1000);

// ── ② 사람들이 한꺼번에 움직인다. 5초 동안 재는다 — 붙자마자가 아니라 <b>도는 중</b>을 본다.
const walking = startWalking();
await reset();
await new Promise((done) => setTimeout(done, 5000));
const seen = await read();

const seconds = seen.spent / 1000;
const fps = seen.frames / seconds;
const bytesPerSecond = seen.bytes / seconds;
const shown = await page.textContent('#peers');
const status = await page.textContent('#status');

check('창이 사람들 한복판에서도 붙어 있다', status === '붙었다', status);
// [문턱-사유] (b) 사람이 느끼는 선 — 10초는 「안 뜨네」 하고 닫는 자리다. 아주 넉넉해
//   느린 기계에서도 태생적 빨강이 안 된다(200명 광장에서도 실측 여유가 컸다).
check('들어가면 곧 세계가 보인다 (10초 안)', openedInMilliseconds <= 10000,
	`${(openedInMilliseconds / 1000).toFixed(1)}초`);
check(`창이 사람들을 본다`, /\d/.test(shown || '') && Number((shown || '').replace(/\D/g, '')) > 1,
	`화면 표시: ${shown}`);
const keepFloor = Math.max(MIN_FRAMES_PER_SECOND, idleFps * MIN_CROWD_KEEP_RATIO);
check('한산할 때부터 그려지고 있다', idleFps >= MIN_FRAMES_PER_SECOND, `${idleFps.toFixed(1)}fps`);
check(`사람이 몰려도 화면이 버틴다 (한산할 때의 ${Math.round(MIN_CROWD_KEEP_RATIO * 100)}% 이상)`,
	fps >= keepFloor,
	`${fps.toFixed(1)}fps · 한산할 때 ${idleFps.toFixed(1)}fps · 기준 ${keepFloor.toFixed(1)}fps`);
check(`알림이 사람 수만큼 부풀지 않는다`, bytesPerSecond <= MAX_BYTES_PER_SECOND,
	`초당 ${(bytesPerSecond / 1024).toFixed(1)}KB · ${(seen.messages / seconds).toFixed(1)}건`);
// ── ③ 사람들이 <b>깜빡이지 않나</b> — 「바뀐 것만」 보내기(TASK-WM-220)가 깨지는 자리 ──
//
// ★ 왜 재나: 안 실려 온 사람을 창이 「사라졌다」로 읽으면, 광장의 사람들이 매 판 사라졌다
//   나타난다. 화면은 여전히 그려지고(fps 초록), 바이트도 작아서(초록) 다른 눈에는 안 걸린다.
//   사람 눈에만 보이는 고장이라 <b>세어 보는 수밖에</b> 없다.
const seenCounts = [];
for (let i = 0; i < 30; i++) {
	const text = await page.textContent('#peers');
	const found = Number(String(text || '').replace(/\D/g, ''));
	if (Number.isFinite(found)) seenCounts.push(found);
	await new Promise((done) => setTimeout(done, 100));
}

const fewest = Math.min(...seenCounts);
const most = Math.max(...seenCounts);

// ⚠ 「한 명도 안 바뀐다」로 자르면 안 된다 — 사람들이 걸어다니면 가까운 순 상한(48명)에
//   드나드는 사람이 생겨 47~53 처럼 출렁인다. 그건 정상이다.
//   깜빡임은 그런 출렁임이 아니라 <b>화면이 통째로 비었다 채워지는 것</b>이다(실측 1~48명).
//   그래서 「많을 때의 7할 밑으로 떨어지나」로 본다.
check('사람들이 깜빡이지 않는다', fewest >= most * 0.7, `3초 동안 ${fewest}~${most}명`);

check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

console.log(`  ⓘ 사람 ${crowd}명 · 창 하나 — 초당 ${(bytesPerSecond / 1024).toFixed(1)}KB, ${fps.toFixed(1)}fps (한산할 때 ${idleFps.toFixed(1)}fps)`);

clearInterval(walking);
for (const socket of bots) { try { socket.close(); } catch { /* 이미 닫혔다 */ } }
await browser.close();
killWorld();

if (failures === 0) {
	console.log(`[web-crowd] ✅ 사람 ${crowd}명 한복판에서도 창이 논다`);
	process.exit(0);
}

console.log(`\n[web-crowd] RESULT: ${failures}건`);
process.exit(1);
