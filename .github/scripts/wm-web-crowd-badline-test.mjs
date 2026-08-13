#!/usr/bin/env node
// wm-web-crowd-badline-test.mjs — <b>사람이 몰린 곳</b>을 나쁜 회선에서 다시 잰다 (TASK-WM-269).
//
// ★ 왜: 몰린 곳 측정(WM-217 § 규모)은 전부 loopback 이었다 — 지연 0·흔들림 0.
//   그런데 몰린 곳에서 제일 먼저 무너지는 것이 <b>회선</b>이다. 지연이 붙으면 창은 판을
//   제때 못 받고, 세계는 「뒤처진 창」으로 보고 보여 주는 사람 수를 줄인다(WM-228).
//   그 줄임이 <b>진짜 회선에서도</b> 제대로 도는지는 한 번도 안 봤다.
//
// 재는 법: 봇 여럿은 세계에 <b>바로</b> 붙고(그들의 회선은 이 시험의 관심사가 아니다),
//   진짜 창만 나쁜 회선으로 붙는다. 같은 무리를 두 번 본다 — 곧은 회선 / 나쁜 회선.
//   ★ 재기 전에 <b>자를 먼저 잰다</b>: 그 회선이 정말 그만큼 나르는지부터 확인한다.
//
// 실측 기록 (2026-08-13, 이 기계):
//   사람  40명 — 곧은 35.8fps·8.1KB/s·보이는 사람 41 · 나쁜 30.8fps·8.2KB/s·41
//   사람 200명 — 곧은 34.1fps·9.4KB/s·보이는 사람 59 · 나쁜 32.3fps·9.1KB/s·49
//   → 사람이 다섯 배로 늘어도 <b>창에 도착하는 양은 거의 그대로</b>다(관심 반경 + 가까운 48명 상한).
//     늦은 회선에서는 보이는 사람이 59 → 49 로 줄어든다 — 그게 「뒤처진 창에는 줄여서 준다」(WM-228)다.
//   CI 는 2코어라 기본 40명으로 돈다. 200명은 `WM_CROWD=200` 로 손수 잰다.
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 버틴다 · 1 = 못 버틴다 · 2 = 못 돌림
//
// [빨강-확인] 늦은 창에게 늘 두 명만 보여 주게 하니 <b>견줌만으로는 초록</b>이었다(곧은 2·나쁜 2) — 그래서 바닥(10명)을 두고서야 빨강이 된다 (2026-08-14)

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5500);
const linePort = port + 1;
const crowd = Number(process.env.WM_CROWD || 40);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-crowd-badline-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const MEASURE_MS = 5000;

/*
 * 기준 — <b>이 기계가 곧은 회선에서 낸 것</b>과 견준다(절대값은 환경 주장이라 CI 에서 태생적 빨강).
 * 나쁜 회선은 늦게 올 뿐, 사람이 사라지거나 화면이 멎어서는 안 된다.
 */
const KEEP_FRAMES_RATIO = 0.6;    // 곧은 회선의 6할은 그려야 한다
const KEEP_PEOPLE_RATIO = 0.5;    // 보이는 사람이 절반 밑으로 떨어지면 그건 「좁힌」 게 아니라 「잃은」 것
const MOST_BYTES_RATIO = 1.6;     // 늦게 온다고 <b>더 많이</b> 오면 안 된다(다시 보내기 폭발)

function cannotRun(message) {
	console.error(`[crowd-badline] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-crowd-badline-app-')), 'app');
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

// ── 자를 먼저 잰다 — 그 회선이 정말 늦게 나르나 ─────────────────────────
{
	const straightAt = Date.now();
	await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
	const straightMs = Date.now() - straightAt;

	const roughAt = Date.now();
	await fetch(`http://127.0.0.1:${linePort}/health`, { headers: { connection: 'close' } });
	const roughMs = Date.now() - roughAt;

	// 왕복 200ms 회선이면 한 번 오가는 데 그만큼은 더 걸려야 한다(TCP 손짓까지 하면 더).
	check('나쁜 회선이 정말 나쁘다 (재는 자부터 확인)', roughMs - straightMs >= ONE_WAY_MS,
		`곧은 회선 ${straightMs}ms · 나쁜 회선 ${roughMs}ms`);
	if (roughMs - straightMs < ONE_WAY_MS) {
		await line.close();
		killWorld();
		cannotRun('회선이 안 느려졌다 — 이 상태로 잰 값은 뜻이 없다');
	}
}

// ── 봇을 몰아넣는다 (세계에 바로 붙는다 — 그들의 회선은 이 시험의 관심사가 아니다) ──
const bots = [];
for (let i = 0; i < crowd; i += 1) {
	const socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onerror = () => { /* 못 붙은 놈은 아래 숫자에서 빠진다 */ };
	bots.push(socket);
}

await wait(3000);

const joined = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((r) => r.json());
check(`봇 ${crowd}명이 세계에 들어갔다`, joined.people >= crowd, `세계가 세는 사람 ${joined.people}명`);

const walking = setInterval(() => {
	for (const socket of bots) {
		if (socket.readyState !== 1) continue;

		socket.send(JSON.stringify({ type: 'move', x: (Math.random() - 0.5) * 2, z: (Math.random() - 0.5) * 2 }));
	}
}, 200);

const browser = await chromium.launch();

/** 창 하나를 열어 <b>도착한 것</b>을 센다 — 서버가 「보냈다」고 하는 것 말고. */
async function watch(where, label) {
	const page = await browser.newPage();
	const errors = [];
	page.on('pageerror', (error) => errors.push(String(error)));

	await page.addInitScript(() => {
		window.__wmSeen = { messages: 0, bytes: 0, frames: 0, since: Date.now() };
		const RealSocket = window.WebSocket;
		window.WebSocket = function (...args) {
			const socket = new RealSocket(...args);
			socket.addEventListener('message', (event) => {
				window.__wmSeen.messages += 1;
				window.__wmSeen.bytes += typeof event.data === 'string' ? event.data.length : 0;
			});

			return socket;
		};

		window.WebSocket.prototype = RealSocket.prototype;

		// ⚠ 붙박이 값(OPEN 등)까지 옮겨야 한다 — 안 옮기면 창의 `socket.readyState !== WebSocket.OPEN`
		//   이 늘 참이 되어 창은 <b>한 마디도 못 보낸다</b>(인사조차). 그러면 세계는 조용하고,
		//   나는 「사람이 안 보인다」를 세계 탓으로 적게 된다(실측: #peers 가 「·」 그대로였다).
		Object.assign(window.WebSocket, RealSocket);

		const count = () => { window.__wmSeen.frames += 1; requestAnimationFrame(count); };
		requestAnimationFrame(count);
	});

	await page.goto(where);
	await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
		.catch(() => { /* 아래에서 잡힌다 */ });

	// 붙자마자가 아니라 <b>도는 중</b>을 본다.
	await wait(1500);
	await page.evaluate(() => { window.__wmSeen = { ...window.__wmSeen, messages: 0, bytes: 0, frames: 0, since: Date.now() }; });
	// ★ <b>선 위의 양</b>도 같이 잰다 (TASK-WM-339 에서 배웠다): 창이 세는 바이트는 <b>푼 뒤</b>의 크기라
	//   세계가 눌러 보내는 이 세계에서는 9배까지 부풀어 보인다(실측 창 15KB/s · 선 1.6KB/s).
	//   견줌(나쁜 ÷ 곧은)은 단위가 같아 그대로 뜻이 있지만, <b>얼마나 나르나</b>는 선 위의 값이 진실이다.
	const carriedBefore = line.peek().reduce((sum, one) => sum + (one.carried || 0), 0);
	await wait(MEASURE_MS);
	const carriedAfter = line.peek().reduce((sum, one) => sum + (one.carried || 0), 0);

	const seen = await page.evaluate(() => ({ ...window.__wmSeen, spent: Date.now() - window.__wmSeen.since }));
	const peersText = await page.textContent('#peers');
	const engineHere = await page.evaluate(() => typeof window.__wmView === 'object');
	const people = Number(String(peersText || '').replace(/\D/g, ''));
	if (people === 0)
		console.log(`  ⓘ ${label} 진단: #peers="${peersText}" · 엔진 왔나 ${engineHere}`);
	const seconds = seen.spent / 1000;
	const measured = {
		fps: seen.frames / seconds,
		bytesPerSecond: seen.bytes / seconds,
		wireBytesPerSecond: (carriedAfter - carriedBefore) / seconds,
		people,
		errors,
	};

	console.log(`  ⓘ ${label}: ${measured.fps.toFixed(1)}fps · 창이 푼 뒤 초당 ${(measured.bytesPerSecond / 1024).toFixed(1)}KB`
		+ ` · <b>선 위</b> 초당 ${(measured.wireBytesPerSecond / 1024).toFixed(1)}KB · 보이는 사람 ${people}명`);

	await page.close();
	return measured;
}

const straight = await watch(`http://127.0.0.1:${port}/`, '곧은 회선');
const rough = await watch(`http://127.0.0.1:${linePort}/`, `나쁜 회선(왕복 ${ONE_WAY_MS * 2}ms)`);

clearInterval(walking);

// ★ 견줌만으로는 <b>둘 다 망가진 판</b>을 못 잡는다 (2026-08-14 실측): 세계가 모두에게 두 명만
//   보여 주게 해 놓고 돌렸더니, 곧은 2명·나쁜 2명이라 「50% 이상」이 통과했다 —
//   무리 40명 한복판에서 두 명만 보이는데 초록이었다. 그래서 <b>바닥</b>을 같이 둔다.
//   [문턱-사유] (c) 제품 상수 — 세계가 한 창에 보여 주기로 한 사람 수(InterestCrowd.MAX_VISIBLE_DOLLS)의
//   절반이다. 기계 속도와 무관하고, 광장이 광장으로 보이려면 그만큼은 있어야 한다.
const LEAST_IN_A_CROWD = 10;

check(`곧은 회선에서는 무리가 보인다 (적어도 ${LEAST_IN_A_CROWD}명 · 견줄 기준)`,
	straight.people >= LEAST_IN_A_CROWD && straight.fps > 5,
	`${straight.people}명 · ${straight.fps.toFixed(1)}fps (무리 ${crowd}명 한복판)`);
check(`나쁜 회선에서도 화면이 그려진다 (곧은 회선의 ${Math.round(KEEP_FRAMES_RATIO * 100)}% 이상)`,
	rough.fps >= straight.fps * KEEP_FRAMES_RATIO,
	`${rough.fps.toFixed(1)}fps · 곧은 회선 ${straight.fps.toFixed(1)}fps`);
check(`나쁜 회선에서도 사람이 보인다 (곧은 회선의 ${Math.round(KEEP_PEOPLE_RATIO * 100)}% 이상)`,
	rough.people >= straight.people * KEEP_PEOPLE_RATIO,
	`${rough.people}명 · 곧은 회선 ${straight.people}명`);
// ⚠ 견줌은 <b>창이 센 바이트</b>로 한다 — 두 판의 단위가 같아서 뜻이 있다.
//   선 위의 양은 <b>나쁜 회선 판에만</b> 있다(곧은 판은 회선을 안 거친다) — 그래서 견줌에 못 쓴다.
//   대신 <b>얼마나 나르나</b>를 알려면 그 값이 진실이라 위 ⓘ 줄에 같이 찍는다 (TASK-WM-339).
check('늦게 온다고 더 많이 오지는 않는다', rough.bytesPerSecond <= straight.bytesPerSecond * MOST_BYTES_RATIO,
	`초당 ${(rough.bytesPerSecond / 1024).toFixed(1)}KB · 곧은 회선 ${(straight.bytesPerSecond / 1024).toFixed(1)}KB`
		+ ` · 나쁜 회선의 <b>선 위</b> 실제 ${(rough.wireBytesPerSecond / 1024).toFixed(1)}KB`);
check('창이 조용히 안 터졌다', straight.errors.length === 0 && rough.errors.length === 0,
	[...straight.errors, ...rough.errors].join(' | ') || '오류 없음');

for (const socket of bots) { try { socket.close(); } catch { /* 이미 닫혔다 */ } }
await browser.close();
await line.close();
killWorld();

if (failures === 0) {
	console.log(`[crowd-badline] ✅ 사람 ${crowd}명 한복판 · 왕복 ${ONE_WAY_MS * 2}ms 회선에서도 창이 버틴다`);
	process.exit(0);
}

console.log(`\n[crowd-badline] RESULT: ${failures}건`);
process.exit(1);
