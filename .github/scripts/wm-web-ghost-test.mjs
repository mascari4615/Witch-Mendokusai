#!/usr/bin/env node
// wm-web-ghost-test.mjs — <b>나간 사람이 화면에 남지 않는다</b> (TASK-WM-306).
//
// ★ 왜: 세계는 「그 사람 나갔다」(gone)를 <b>한 번만</b> 말한다. 그 한 판을 창이 놓치면 그 사람은
//   그 창에 <b>영영</b> 남는다 — 세계에는 없는 사람이 화면에서 계속 서 있다.
//   실측 2026-08-13(나쁜 회선·여덟씩 드나듦): 세계 사람 1명일 때 창은 <b>5명</b>을 그리고 있었다.
//
// ★ 어디서 새던가: 창은 한 프레임에 온 판 중 <b>마지막 것만</b> 세운다(200명에서 그리기가 밀려
//   그렇게 했다 — WM-217). 자리 값은 다음 판이 다시 말해 주니 괜찮지만, <b>「나갔다」는 다시 안 온다</b>.
//   그래서 덮어쓰기를 <b>합치기</b>로 바꿨다(mergePlates).
//
// 재는 것: 나쁜 회선 너머 창 하나 · 봇 여덟이 들어와 걷다가 나가기를 여러 판 —
//   판마다 <b>창이 그리는 사람 수</b>와 <b>세계가 세는 사람 수</b>를 견준다.
//   창이 세계보다 <b>많으면</b> 그만큼이 유령이다(적은 것은 관심 반경 때문이라 정상).
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 유령 없음 · 1 = 유령 남음 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5560);
const linePort = worldPort + 1;

const ONE_WAY_MS = 100;
const JITTER_MS = 30;
const LOSS_PERCENT = 2;

/** 한 판에 들어오는 사람 수 — 한 프레임에 판이 여럿 오게 하려면 여럿이어야 한다. */
const CROWD = Number(process.env.WM_GHOST_CROWD || 8);

/** 몇 판 돌리나. */
const ROUNDS = Number(process.env.WM_GHOST_ROUNDS || 4);

/** 나간 뒤 이만큼은 기다려 준다 (ms) — 「나갔다」가 오는 데 걸리는 시간(실측 ~270ms)의 몇 배. */
const SETTLE_MS = 5000;

/**
 * 한 사람이 나가고 <b>이 시간 안에</b> 화면에서 사라져야 한다 (ms).
 *
 * ★ 왜 시간까지 재나: 사람 수만 견주면 「5초 뒤에는 맞다」로 통과한다 — 그런데 WM-306 에서
 *   <b>20초가 지나도 안 사라진</b> 판이 한 번 있었다(그 뒤 26번 시도해도 재현 X).
 *   수만 보는 관문은 그 판을 못 잡는다. 시간을 재면 다시 오면 그때 잡힌다.
 *
 * 실측 2026-08-13 (15판): 가운데값 264ms · 가장 오래 508ms — 사람이 못 느끼는 자리다.
 * 3초는 그 여섯 배 — 느린 기계에서 태생적 빨강이 안 되게 넉넉히 잡았다.
 */
const VANISH_WITHIN_MS = 3000;

function cannotRun(message) {
	console.error(`[web-ghost] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-ghost-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-ghost-')), 'world.json');
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
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(`http://127.0.0.1:${linePort}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래가 잡는다 */ });

// ⚠ 재기 전에 잴 것이 왔는지부터 (domain-wm.md § 관문 규율).
const inWorld = await page.waitForFunction(() => window.__wmView.world().gatherables > 0, null, { timeout: 30000 })
	.then(() => true).catch(() => false);

if (inWorld === false) {
	await browser.close();
	badLine.close();
	killWorld();
	cannotRun('창이 세계를 못 받았다 — 이 상태로는 유령을 잴 수 없다');
}

const truth = () => fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());
const drawn = () => page.evaluate(() => window.__wmView.world().dolls);

const bots = [];
function joinBot() {
	const one = { id: null, socket: new WebSocket(`ws://127.0.0.1:${worldPort}/ws`) };
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		try {
			const said = JSON.parse(event.data);
			if (said.type === 'welcome') one.id = said.id;
		} catch { /* 우리 말이 아니다 */ }
	};

	bots.push(one);
}

let worstGhosts = 0;
let joined = 0;
for (let round = 0; round < ROUNDS; round += 1) {
	for (let i = 0; i < CROWD; i += 1) joinBot();

	await wait(2500);
	joined += CROWD;

	// 흩어져 걷는다 — 판이 자주 오게(그래야 한 프레임에 여럿 겹친다).
	for (let step = 0; step < 40; step += 1) {
		for (const bot of bots) {
			if (bot.socket.readyState !== 1) continue;

			const angle = (bot.id || 1) * 0.7 + step * 0.1;
			bot.socket.send(JSON.stringify({ type: 'move', x: Math.cos(angle) * 0.15, z: Math.sin(angle) * 0.15, seq: step }));
		}

		await wait(50);
	}

	// 절반이 나간다.
	for (let i = 0; i < CROWD / 2; i += 1) {
		const leaving = bots.shift();
		if (leaving !== undefined) leaving.socket.close();
	}

	await wait(SETTLE_MS);

	const now = await truth();
	const shown = await drawn();
	const ghosts = shown - now.people;
	if (ghosts > worstGhosts) worstGhosts = ghosts;

	console.log(`  ⓘ ${round + 1}판 — 창 ${shown}명 · 세계 ${now.people}명 (유령 ${ghosts > 0 ? ghosts : 0})`);
}

// ── 마지막: 한 사람이 나가고 <b>얼마 만에</b> 사라지는지 (수가 아니라 시간) ────────────
const lastOne = { id: null, socket: new WebSocket(`ws://127.0.0.1:${worldPort}/ws`) };
lastOne.socket.onopen = () => lastOne.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
lastOne.socket.onerror = () => { /* 아래가 잡는다 */ };
lastOne.socket.onmessage = (event) => {
	try {
		const said = JSON.parse(event.data);
		if (said.type === 'welcome') lastOne.id = said.id;
	} catch { /* 우리 말이 아니다 */ }
};

await wait(2500);

let vanishedInMs = -2;
if (lastOne.id !== null) {
	const sawIt = await page.waitForFunction(
		(id) => window.__wmView.dolls().some((one) => one.id === id), lastOne.id, { timeout: 15000 })
		.then(() => true).catch(() => false);

	if (sawIt) {
		const leftAt = Date.now();
		lastOne.socket.close();

		vanishedInMs = -1;
		while (Date.now() - leftAt < VANISH_WITHIN_MS * 3) {
			const still = await page.evaluate((id) => window.__wmView.dolls().some((one) => one.id === id), lastOne.id);
			if (still === false) { vanishedInMs = Date.now() - leftAt; break; }

			await wait(100);
		}
	}
}

if (lastOne.socket.readyState === 1) lastOne.socket.close();

for (const bot of bots) bot.socket.close();
await wait(SETTLE_MS);

const endTruth = await truth();
const endShown = await drawn();
const endGhosts = endShown - endTruth.people;
if (endGhosts > worstGhosts) worstGhosts = endGhosts;

await browser.close();
badLine.close();
killWorld();

if (joined < CROWD * ROUNDS) {
	cannotRun(`사람이 다 안 들어왔다 (${joined}) — 이 표본으로는 유령을 못 가른다`);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

check('나간 사람이 화면에 안 남는다', worstGhosts <= 0,
	`가장 많을 때 유령 ${worstGhosts > 0 ? worstGhosts : 0}명 (창이 세계보다 많으면 그만큼이 유령이다)`);
check('마지막에도 창과 세계가 같다', endGhosts <= 0, `창 ${endShown}명 · 세계 ${endTruth.people}명`);
check('나간 사람이 <b>곧</b> 사라진다', vanishedInMs >= 0 && vanishedInMs <= VANISH_WITHIN_MS,
	vanishedInMs === -2 ? '잴 사람이 안 들어왔다'
		: vanishedInMs === -1 ? `${VANISH_WITHIN_MS * 3}ms 가 지나도 안 사라졌다 — WM-306 의 그 판이 돌아온 것이다`
			: `${vanishedInMs}ms (한도 ${VANISH_WITHIN_MS}ms · 실측 가운데값 264ms)`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

if (failures === 0) {
	console.log('[web-ghost] ✅ 나쁜 회선에서 여럿이 드나들어도 유령이 안 남는다');
	process.exit(0);
}

console.log(`\n[web-ghost] RESULT: ${failures}건`);
process.exit(1);
