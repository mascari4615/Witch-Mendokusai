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

// 버티는 기준 — 여기 못 미치면 빨강이다. 「느낌」이 아니라 숫자로 못박는다.
const MIN_FRAMES_PER_SECOND = 20;
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

const walking = setInterval(() => {
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

await page.goto(url);
await page.waitForFunction(
	() => (document.getElementById('status')?.textContent || '').includes('붙었다'),
	null, { timeout: 30000 }).catch(() => { /* 아래 칸이 잡는다 */ });

// 5초 동안 재는다 — 붙자마자가 아니라 <b>도는 중</b>을 본다.
await page.evaluate(() => { window.__wmSeen = { ...window.__wmSeen, messages: 0, bytes: 0, frames: 0, since: Date.now() }; });
await new Promise((done) => setTimeout(done, 5000));
const seen = await page.evaluate(() => ({ ...window.__wmSeen, spent: Date.now() - window.__wmSeen.since }));

const seconds = seen.spent / 1000;
const fps = seen.frames / seconds;
const bytesPerSecond = seen.bytes / seconds;
const shown = await page.textContent('#peers');
const status = await page.textContent('#status');

check('창이 사람들 한복판에서도 붙어 있다', status === '붙었다', status);
check(`창이 사람들을 본다`, /\d/.test(shown || '') && Number((shown || '').replace(/\D/g, '')) > 1,
	`화면 표시: ${shown}`);
check(`화면이 계속 그려진다 (${MIN_FRAMES_PER_SECOND}fps 이상)`, fps >= MIN_FRAMES_PER_SECOND,
	`${fps.toFixed(1)}fps`);
check(`알림이 사람 수만큼 부풀지 않는다`, bytesPerSecond <= MAX_BYTES_PER_SECOND,
	`초당 ${(bytesPerSecond / 1024).toFixed(1)}KB · ${(seen.messages / seconds).toFixed(1)}건`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

console.log(`  ⓘ 사람 ${crowd}명 · 창 하나 — 초당 ${(bytesPerSecond / 1024).toFixed(1)}KB, ${fps.toFixed(1)}fps`);

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
