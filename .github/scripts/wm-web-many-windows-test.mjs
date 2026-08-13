#!/usr/bin/env node
// wm-web-many-windows-test.mjs — <b>진짜 창 여럿이 동시에 들어와도 서로 보인다</b> (TASK-WM-312).
//
// ★ 왜: 「무리」를 재던 관문은 전부 <b>봇</b>(raw WebSocket)이었다 — 200명 광장도, 우르르 들어오기도.
//   그런데 사람은 <b>브라우저</b>로 온다: three.js·DOM·rAF·탭 하나당 프로세스.
//   봇 200개가 멀쩡한 것과 창 셋이 멀쩡한 것은 다른 이야기다(그 자리를 한 번도 안 쟀다).
//
// 재는 것 (지연 100ms·유실 2% 회선 너머, 창 셋 동시):
//   ① 모두 첫 화면을 보나 ② <b>서로가 서로를 보나</b> ③ 창이 조용히 안 터지나
//   ④ 세계가 그 사이에 멎지 않나(판 간격)
//
// ⚠ 프레임 수는 <b>문턱으로 안 쓴다</b>: 창 셋이 한 기계의 CPU 를 나눠 쓰므로 (실측: 1창 40fps →
//   4창 13fps) 그건 제품이 아니라 기계 이야기다. 대신 숫자는 <b>적어만</b> 둔다.
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 여럿이 같이 논다 · 1 = 누가 못 보거나 터진다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5600);
const linePort = worldPort + 1;

const ONE_WAY_MS = 100;
const JITTER_MS = 30;
const LOSS_PERCENT = 2;

/** 창 몇 개를 한꺼번에 — 둘로는 「여럿」이 아니고, 넷은 2코어 CI 에서 기계 이야기가 된다. */
const WINDOWS = Number(process.env.WM_WINDOWS || 3);

/** 창 하나가 첫 화면을 보기까지 봐 주는 시간 — 느린 기계를 넉넉히 덮는다. */
const PAINT_WITHIN_MS = 60000;

/** 이보다 오래 세계가 멎으면 창들이 세계를 굶긴 것이다. */
const MOST_TICK_GAP_MS = 1500;

function cannotRun(message) {
	console.error(`[many-windows] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-many-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-many-')), 'world.json');
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
		null, { timeout: PAINT_WITHIN_MS })
		.then(() => true).catch(() => false);

	windows.push({ page, errors, paintedInMs: painted ? Date.now() - openedAt : -1 });
}

// 다 같이 걷는다 — 서로 움직이는 것이 보여야 「같이 논다」다.
// ★ 걷기 <b>전</b>에 남들이 어디 있는지 적어 둔다 (TASK-WM-328). 이게 없으면 이 관문은
//   「남의 인형이 <b>서 있는 채로</b> 보이기만 해도」 초록이다 — 부하가 걸려 남의 움직임이
//   안 오기 시작해도 그대로 통과한다. 우르르 관문에서 겪은 그 구멍과 같은 꼴이다(WM-318:
//   손님으로 도착해도 수가 맞아 초록이었다 → 이름을 들고 왔나로 고쳤다).
for (const one of windows) {
	await one.page.evaluate(() => {
		window.__before = Object.fromEntries(
			window.__wmView.dolls().map((doll) => [doll.id, { x: doll.serverX, z: doll.serverZ, mine: doll.isLocal }]));
		window.__frames = 0;
		const count = () => { window.__frames += 1; requestAnimationFrame(count); };
		requestAnimationFrame(count);
		window.dispatchEvent(new KeyboardEvent('keydown', { key: 'd' }));
	});
}

await wait(15000);

const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

const seen = [];
for (const one of windows) {
	seen.push(await one.page.evaluate(() => {
		// 남이 <b>세계 좌표에서</b> 얼마나 움직였나 — 그리기(보간)가 아니라 세계가 보낸 자리로 본다.
		const before = window.__before || {};
		let mine = 0;
		let othersLeast = Infinity;
		let others = 0;
		for (const doll of window.__wmView.dolls()) {
			const was = before[doll.id];
			if (!was) continue;   // 걷기 시작한 뒤에 들어온 인형 — 견줄 이전이 없다
			const moved = Math.hypot(doll.serverX - was.x, doll.serverZ - was.z);
			if (was.mine) { mine = moved; continue; }
			others += 1;
			if (moved < othersLeast) othersLeast = moved;
		}
		return {
			...window.__wmView.world(),
			frames: window.__frames,
			mineMoved: mine,
			othersCounted: others,
			othersLeastMoved: others === 0 ? -1 : othersLeast,
		};
	}));
}

await browser.close();
badLine.close();
killWorld();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

const painted = windows.filter((one) => one.paintedInMs > 0);
if (painted.length === 0) cannotRun('창이 하나도 안 떴다 — 이 기계에서는 여럿을 잴 수 없다');

console.log(`  ⓘ 창 ${WINDOWS}개 · 회선 ${ONE_WAY_MS}ms/${LOSS_PERCENT}% — 첫 화면 `
	+ windows.map((one) => (one.paintedInMs > 0 ? `${one.paintedInMs}ms` : '못 봄')).join(' · ')
	+ ` · 초당 프레임 ${seen.map((one) => Math.round(one.frames / 15)).join('/')}`
	+ ` (기계를 나눠 쓰므로 문턱 아님)`);

check('모든 창이 첫 화면을 본다', painted.length === WINDOWS,
	`${painted.length}/${WINDOWS}개`);
check('서로가 서로를 본다', seen.every((one) => one.dolls >= WINDOWS),
	`창마다 보이는 사람 ${seen.map((one) => one.dolls).join('/')} (세계는 ${health.people}명)`);
// ★ 「보인다」로는 모자란다 — 서 있는 인형만 보여도 그건 초록이다. 부하 아래서 <b>남이 계속
//   움직이는 것</b>이 이 관문의 제품 주장이다. 같은 15초 동안 모두 같은 키를 눌렀으므로
//   남이 움직인 거리는 내가 움직인 거리와 비슷해야 한다.
//   [문턱-사유] (a) 같은 판의 내 이동거리와의 견줌 — 기계가 느리면 둘 다 같이 줄어든다.
const walkedTogether = seen.every((one) =>
	one.othersCounted > 0 && one.othersLeastMoved >= one.mineMoved * 0.5);
check('부하 아래서도 남이 계속 움직인다', walkedTogether,
	seen.map((one) => one.othersCounted === 0
		? '견줄 남이 없었다'
		: `남 ${one.othersLeastMoved.toFixed(1)}m / 나 ${one.mineMoved.toFixed(1)}m`).join(' · '));

check('창이 조용히 안 터졌다', windows.every((one) => one.errors.length === 0),
	windows.flatMap((one) => one.errors).slice(0, 2).join(' | ') || '오류 없음');
check('세계가 그 사이 안 멎는다', health.longestTickGapMs <= MOST_TICK_GAP_MS,
	`가장 벌어진 판 ${health.longestTickGapMs}ms (한도 ${MOST_TICK_GAP_MS}ms)`);
check('기억도 계속 남는다', health.savesFailed === 0, `저장 실패 ${health.savesFailed}번`);

if (failures === 0) {
	console.log('[many-windows] ✅ 진짜 창 여럿이 같이 논다');
	process.exit(0);
}

console.log(`\n[many-windows] RESULT: ${failures}건`);
process.exit(1);
