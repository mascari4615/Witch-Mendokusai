#!/usr/bin/env node
// wm-web-crowd-windows-test.mjs — <b>진짜 창 여럿이 무리 한복판에서 동시에</b> 논다 (TASK-WM-345).
//
// ★ 왜 또 만드나 — 이 자리는 아직 한 번도 안 쟀다:
//   · 「무리」 관문(crowd-smoke·crowd-badline) = 봇 40명 + <b>진짜 창 하나</b>
//   · 「창 여럿」 관문(many-windows)          = <b>진짜 창 셋</b> + 남이 없음(서로만)
//   둘 다 초록이어도 <b>창 셋이 같은 무리 안에</b> 있을 때는 다르다: 세계는 창마다 따로
//   「밀렸나」를 보고 <b>보여 주는 사람 수를 줄이거나 판을 건너뛴다</b>(WM-228·WM-340).
//   그 줄임이 창마다 따로 걸리므로 한 창만 굶는 <b>편식</b>이 생길 수 있다 — 봇으로는 이걸 못 잰다
//   (봇은 rAF·three.js 가 없어 창처럼 느려지지 않는다).
//
// 재는 것 (봇 40명 광장 · 진짜 창 3개가 나쁜 회선 너머로 동시 입장, 15초):
//   ① 창마다 무리가 보인다 (적어도 10명)
//   ② <b>창끼리 공평하다</b> — 가장 덜 받은 창이 가장 많이 받은 창의 절반 위 (기계와 무관한 견줌)
//   ③ 무리가 <b>멈춰 보이지 않는다</b> — 창마다 남들이 실제로 움직인다
//   ④ 세계가 그 사이 안 멎는다 · 창이 조용히 안 터진다
//
// ⚠ 프레임 수는 문턱으로 안 쓴다 — 창 셋이 한 기계를 나눠 쓴다(many-windows 와 같은 이유). 적어만 둔다.
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 셋 다 무리 속에서 논다 · 1 = 누가 굶거나 멈춰 보인다 · 2 = 못 돌림
//
// [빨강-확인] 이 관문을 만든 그 날 <b>고치기 전 제품</b>이 곧바로 빨강이었다 (2026-08-14):
//   「보내는 중」 표를 안 내려놓는 옛 코드에서 셋째 창이 받은 판 27장(다른 둘은 302장) ·
//   보이는 사람 6명 · 공평 9% — 세 줄 모두 빨강. 표를 내려놓게 고치니 300/303/303 · 24명 · 99%.
//
// [빨강-확인] 「나간 사람이 사라진다」 (2026-08-14) — 이 줄은 <b>지우는 길이 셋</b>이라 하나씩 꺼서는 안 빨개졌다:
//   ① 「그 사람 나갔다」 목록을 안 듣게 → 초록(통째 판이 갈아 끼운다)
//   ② 통째 판도 덧붙이게 → 초록(유령 덫이 10초마다 물어봐 지운다)
//   ③ 셋째로 「내가 누굴 그리나」까지 안 물어보게 → <b>빨강</b>: 나간 5명이 창 셋 모두에 그대로(5/5/5, 그린 인형 43).
//   ⇒ 이 관문은 <b>세 겹이 다 무너질 때</b> 운다. 그 사실 자체가 이 판의 수확이다.

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5390);
const linePort = worldPort + 1;

const ONE_WAY_MS = 100;
const JITTER_MS = 30;
const LOSS_PERCENT = 2;

/** 창 몇 개 — 둘로는 「여럿」이 아니고, 넷은 2코어 CI 에서 기계 이야기가 된다(many-windows 와 같은 값). */
const WINDOWS = Number(process.env.WM_WINDOWS || 3);

/** 광장에 세울 봇 — CI 2코어 기준(crowd-smoke 와 같은 값). */
const CROWD = Number(process.env.WM_CROWD || 40);

const WATCH_SECONDS = 15;

/**
 * 무리가 무리로 보이려면 최소 이만큼.
 * [문턱-사유] (c) 제품 상수 — 세계가 한 창에 보여 주기로 한 수(48)의 넉넉한 아래쪽 (crowd-smoke 와 같은 바닥).
 */
const LEAST_IN_A_CROWD = 10;

/**
 * 창끼리 공평함 — 가장 덜 받은 창 / 가장 많이 받은 창.
 * [문턱-사유] (a) 같은 판 안의 다른 창과의 견줌 — 기계가 느리면 셋이 <b>같이</b> 줄어들므로
 *   이 비율은 기계 몫을 지운다. 세계가 밀린 창에 내리는 가장 낮은 박자가 <b>절반</b>이므로
 *   (SendPlan.EVERY_NTH_WHEN_LAGGING) 절반이 곧 제품이 약속한 바닥이고,
 *   그 아래면 「한 창만 굶었다」는 뜻이다.
 */
const LEAST_SHARE = 0.5;

/**
 * 나간 사람이 사라지기까지 봐 주는 시간.
 * [문턱-사유] (c) 사람이 느끼는 선 — 「유령 20초」가 이 관문이 생긴 이유다. 그 절반도 안 되는 8초를 준다
 *   (세계는 초당 20번 말하므로 성한 창에서는 한 판 만에 사라진다).
 */
const GHOST_WITHIN_MS = 8000;

/** 이보다 오래 세계가 멎으면 창·봇이 세계를 굶긴 것이다(many-windows 와 같은 값). */
const MOST_TICK_GAP_MS = 1500;

function cannotRun(message) {
	console.error(`[crowd-windows] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-cw-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-cw-')), 'world.json');
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

// 봇은 <b>곧은 회선</b>으로 붙는다 — 그들의 회선은 이 시험의 관심사가 아니다(crowd-badline 과 같은 법).
const crowd = [];
for (let i = 0; i < CROWD; i += 1) {
	const socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onerror = () => { /* 아래에서 수로 본다 */ };
	// 자기 번호를 알아 둔다 — 나간 <b>그 사람</b>이 창에서 지워졌나를 봐야 한다(수만 보면 속는다).
	socket.onmessage = (event) => {
		try {
			const said = JSON.parse(String(event.data));
			if (said.type === 'welcome') socket.dollId = said.id;
		} catch { /* 딴 소식 */ }
	};
	crowd.push(socket);
}

await wait(4000);

{
	const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());
	if (health.people < CROWD) {
		killWorld();
		cannotRun(`봇이 다 못 들어갔다 — ${health.people}/${CROWD}명`);
	}
}

// 광장은 계속 움직인다 — 가만히 선 무리는 「바뀐 것만」 규칙 때문에 회선을 거의 안 쓴다(부하가 아니다).
const milling = setInterval(() => {
	for (const socket of crowd) {
		if (socket.readyState !== 1) continue;
		socket.send(JSON.stringify({ type: 'move', x: Math.random() < 0.5 ? 0.15 : -0.15, z: 0 }));
	}
}, 100);

const badLine = openBadLine({
	listenPort: linePort, targetPort: worldPort,
	latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS, lossPercent: LOSS_PERCENT,
});
await badLine.listen();

const browser = await chromium.launch();
const windows = [];

for (let i = 0; i < WINDOWS; i += 1) {
	const page = await browser.newPage();
	const errors = [];
	page.on('pageerror', (error) => errors.push(String(error)));

	const openedAt = Date.now();
	await page.goto(`http://127.0.0.1:${linePort}/`);

	const painted = await page.waitForFunction(
		() => typeof window.__wmView === 'object' && window.__wmView.world().gatherables > 0,
		null, { timeout: 60000 })
		.then(() => true).catch(() => false);

	windows.push({ page, errors, paintedInMs: painted ? Date.now() - openedAt : -1 });
}

// 창마다 <b>받은 판</b>을 센다 — 굶는 창을 잡으려면 화면이 아니라 도착한 소식을 봐야 한다.
// 남들이 움직였나도 세계 좌표로 본다(그리기 보간이 아니라 세계가 보낸 자리).
for (const one of windows) {
	if (one.paintedInMs < 0) continue;
	await one.page.evaluate(() => {
		window.__plates = 0;
		window.__wmView.socket().addEventListener('message', (event) => {
			try {
				if (JSON.parse(String(event.data)).type === 'world') window.__plates += 1;
			} catch (e) { /* 딴 소식 */ }
		});
		window.__before = Object.fromEntries(
			window.__wmView.dolls().map((doll) => [doll.id, { x: doll.serverX, z: doll.serverZ, mine: doll.isLocal }]));
		window.__frames = 0;
		const count = () => { window.__frames += 1; requestAnimationFrame(count); };
		requestAnimationFrame(count);
	});
}

await wait(WATCH_SECONDS * 1000);

// ★ <b>나간 사람이 창마다 사라지나</b> (TASK-WM-346) — 「유령 20초」의 자리다.
//   굶는 창은 「그 사람 나갔다」가 실린 판을 못 받는다(작은 한 장에는 그 목록이 없다).
//   그래서 굶주림과 유령은 같은 뿌리다 — 여기서 <b>같은 판에</b> 본다.
// ⚠ <b>아무나 내보내면 안 된다</b> (2026-08-14 실측): 굶는 창은 가까운 여섯 명만 그린다 —
//   그 여섯 밖의 사람을 내보내면 「아직 그려진 사람 0명」으로 <b>거저 초록</b>이다(빨강 걷기에서 잡혔다).
//   그래서 <b>창 셋이 지금 다 그리고 있는 사람</b> 중에서 고른다.
const drawnEverywhere = [];
for (const one of windows) {
	if (one.paintedInMs < 0) continue;
	drawnEverywhere.push(await one.page.evaluate(() => window.__wmView.dolls().map((doll) => doll.id)));
}

const sharedIds = drawnEverywhere.length === 0
	? []
	: drawnEverywhere[0].filter((id) => drawnEverywhere.every((list) => list.includes(id)));

const leftIds = [];
for (const socket of crowd) {
	if (leftIds.length >= 5) break;
	if (socket.readyState !== 1 || socket.dollId === undefined) continue;
	if (sharedIds.includes(socket.dollId) === false) continue;
	leftIds.push(socket.dollId);
	try { socket.close(); } catch { /* 이미 닫혔다 */ }
}

if (leftIds.length === 0) cannotRun('창 셋이 다 그리고 있는 봇을 못 찾았다 — 유령을 잴 수 없다');
for (const one of windows) {
	if (one.paintedInMs < 0) continue;
	await one.page.evaluate((ids) => { window.__left = ids; }, leftIds);
}

await wait(GHOST_WITHIN_MS);

const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

const seen = [];
for (const one of windows) {
	if (one.paintedInMs < 0) continue;
	seen.push(await one.page.evaluate(() => {
		const before = window.__before || {};
		let movedOthers = 0;
		let watched = 0;
		for (const doll of window.__wmView.dolls()) {
			const was = before[doll.id];
			if (!was || was.mine) continue;
			watched += 1;
			if (Math.hypot(doll.serverX - was.x, doll.serverZ - was.z) > 0.5) movedOthers += 1;
		}

		return {
			...window.__wmView.world(),
			plates: window.__plates,
			drawn: window.__wmView.dolls().length,
			stillThere: window.__wmView.dolls().map((doll) => doll.id).filter((id) => (window.__left || []).includes(id)),
			frames: window.__frames,
			watched,
			movedOthers,
		};
	}));
}

clearInterval(milling);
await browser.close();
for (const socket of crowd) { try { socket.close(); } catch { /* 이미 닫혔다 */ } }
badLine.close();
killWorld();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

if (seen.length < WINDOWS) {
	cannotRun(`창 ${WINDOWS}개 중 ${seen.length}개만 떴다 — 이 기계에서는 무리 속 여럿을 잴 수 없다`);
}

const plates = seen.map((one) => one.plates);
const share = Math.max(...plates) === 0 ? 0 : Math.min(...plates) / Math.max(...plates);

console.log(`  ⓘ 봇 ${CROWD}명 · 창 ${WINDOWS}개 · 회선 ${ONE_WAY_MS}ms/${LOSS_PERCENT}% — `
	+ `받은 판 ${plates.join('/')} (${WATCH_SECONDS}초) · 초당 프레임 `
	+ `${seen.map((one) => Math.round(one.frames / WATCH_SECONDS)).join('/')} (기계를 나눠 쓰므로 문턱 아님)`);

check(`창마다 무리가 보인다 (적어도 ${LEAST_IN_A_CROWD}명)`,
	seen.every((one) => one.dolls >= LEAST_IN_A_CROWD),
	`보이는 사람 ${seen.map((one) => one.dolls).join('/')} (세계는 ${health.people}명)`);

check(`창끼리 공평하다 (덜 받은 창 ≥ 가장 많이 받은 창의 ${Math.round(LEAST_SHARE * 100)}%)`,
	share >= LEAST_SHARE, `${Math.round(share * 100)}% — 받은 판 ${plates.join('/')}`);

// ★ <b>몇 명이 움직였나</b>를 절대 수로 자르면 안 된다 (2026-08-14 CI 실측: 7/12 · 7/14 · 11/23 로 빨강).
//   느린 기계에서는 보이는 사람 자체가 줄고(24 → 12) 봇도 덜 움직인다 — 그건 기계 이야기다.
//   [문턱-사유] (a) 같은 판에서 <b>지켜본 사람 수와의 비율</b> — 기계가 느리면 분모도 같이 준다.
//   + 붕괴 감지선 3명(거의 아무도 안 움직이는 판만 잡는다).
const MOVED_SHARE = 0.4;
const LEAST_MOVERS = 3;
check(`창마다 무리가 실제로 움직인다 (지켜본 사람의 ${Math.round(MOVED_SHARE * 100)}% 위)`,
	seen.every((one) => one.watched > 0
		&& one.movedOthers >= Math.max(LEAST_MOVERS, Math.round(one.watched * MOVED_SHARE))),
	seen.map((one) => `${one.movedOthers}/${one.watched}명 움직임`).join(' · '));

// 세계가 세는 사람 수 아래로 창이 그린 인형이 내려와야 한다 — 나간 다섯이 지워졌다는 뜻이다.
// ★ <b>그 사람</b>이 지워졌나로 본다 — 「그린 수가 세계 수보다 작다」로는 좁혀진 창이 거저 통과한다.
check(`나간 사람이 창마다 사라진다 (${GHOST_WITHIN_MS / 1000}초 안)`,
	seen.every((one) => one.stillThere.length === 0),
	`나간 ${leftIds.length}명 중 아직 그려진 사람 ${seen.map((one) => one.stillThere.length).join('/')}`
		+ ` · 창이 그린 인형 ${seen.map((one) => one.drawn).join('/')}`);

check('창이 조용히 안 터졌다', windows.every((one) => one.errors.length === 0),
	windows.flatMap((one) => one.errors).slice(0, 2).join(' | ') || '오류 없음');

check('세계가 그 사이 안 멎는다', health.longestTickGapMs <= MOST_TICK_GAP_MS,
	`가장 벌어진 판 ${health.longestTickGapMs}ms (한도 ${MOST_TICK_GAP_MS}ms)`);

if (failures === 0) {
	console.log('[crowd-windows] ✅ 창 셋이 무리 한복판에서도 고르게 논다');
	process.exit(0);
}

console.log(`\n[crowd-windows] RESULT: ${failures}건`);
process.exit(1);
