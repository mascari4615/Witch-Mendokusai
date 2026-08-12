#!/usr/bin/env node
// wm-lost-action-test.mjs — <b>끊기는 순간 누른 것이 사라지지도, 두 번 되지도 않는다</b> (TASK-WM-305).
//
// ★ 무엇이 있었나 (실측 2026-08-13): 줍기를 보낸 <b>그 순간</b> 회선이 끊기면 그 줍기는
//   조용히 사라졌다 — 가방도 들판도 그대로, 창은 아무 말도 안 한다. 사람에게는
//   「눌렀는데 안 됐다」만 남는다. (같은 순서로 회선을 <b>안</b> 끊으면 잘 주워진다 —
//   재는 자의 고장이 아니라 진짜 구멍이었다. 이 관문도 그 대조군을 먼저 돌린다.)
//
// ★ 고친 자리: 행동마다 번호(`did`)를 붙이고, 세계가 「그 번호 했다」고 할 때까지 창이 들고 있다가
//   다시 붙으면 또 보낸다. 세계는 <b>사람(신원)마다</b> 최근 번호를 기억해 같은 것을 두 번 안 한다
//   (`ActionOnce`). 줄에 매달면 다시 붙는 순간 기억이 없어져 두 번 된다 — 그래서 신원이다.
//
// 재는 것 (지연 100ms · 유실 2%):
//   ① 대조군 — 회선을 안 끊으면 줍기가 된다 (재는 자가 성한지부터)
//   ② 끊기는 순간 보낸 줍기가 <b>다시 붙은 뒤</b> 살아난다
//   ③ 같은 번호를 여러 번 보내도 <b>한 번만</b> 주워진다
//
// exit: 0 = 사라지지도 두 번 되지도 않는다 · 1 = 사라지거나 두 번 됨 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5550);
const linePort = worldPort + 1;

const ONE_WAY_MS = 100;
const JITTER_MS = 30;
const LOSS_PERCENT = 2;

function cannotRun(message) {
	console.error(`[lost-action] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-lost-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-lost-')), 'world.json');
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

function newLine() {
	return openBadLine({
		listenPort: linePort, targetPort: worldPort,
		latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS, lossPercent: LOSS_PERCENT,
	});
}

function joinWorld(secretToUse) {
	const one = { id: null, secret: '', bag: new Map(), field: [], here: undefined };
	one.socket = new WebSocket(`ws://127.0.0.1:${linePort}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: secretToUse || '' }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'welcome') { one.id = said.id; one.secret = said.secret; }
		if (said.type === 'world') {
			if (Array.isArray(said.gatherables) && said.gatherables.length > 0) one.field = said.gatherables;
			if (Array.isArray(said.dolls) && one.id !== null) {
				const mine = said.dolls.find((doll) => doll.id === one.id);
				if (mine !== undefined) one.here = { x: mine.x, z: mine.z };
			}
		}

		if (said.type === 'me') one.here = { x: said.x, z: said.z };
		if (said.type === 'bag' && Array.isArray(said.items)) one.bag = new Map(said.items.map((item) => [item.itemId, item.amount]));
	};

	return one;
}

const send = (one, message) => {
	if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message));
};

/** 그 자리까지 걸어간다 — 내 자리는 세계 그림에서 읽는다. */
async function walkTo(one, spot) {
	for (let i = 0; i < 500; i += 1) {
		const mine = one.here;
		if (mine !== undefined && Math.hypot(spot.x - mine.x, spot.z - mine.z) < 1.2) return true;

		if (mine !== undefined) {
			send(one, {
				type: 'move', seq: i,
				x: Math.max(-0.15, Math.min(0.15, spot.x - mine.x)),
				z: Math.max(-0.15, Math.min(0.15, spot.z - mine.z)),
			});
		}

		await wait(50);
	}

	return false;
}

// ⚠ 자리마다 나오는 물건이 다르다 — 한 물건만 세면 <b>다른 물건이 들어온 것</b>을 못 본다.
//   그래서 가방 <b>전체 개수</b>로 센다(첫 판이 이걸로 헛빨강이었다).
const howMany = (bag) => [...bag.values()].reduce((sum, one) => sum + one, 0);

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

// ── ① 대조군: 회선을 안 끊으면 줍기가 되나 (재는 자부터 잰다) ─────────
let badLine = newLine();
await badLine.listen();

const control = joinWorld('');
await wait(3000);
if (control.id === null || control.field.length === 0) {
	badLine.close();
	killWorld();
	cannotRun('세계에 못 들어갔거나 들판을 못 받았다');
}

const controlSpot = control.field[0];
if (await walkTo(control, controlSpot) === false) {
	badLine.close();
	killWorld();
	cannotRun('들판까지 못 걸어갔다 — 이 상태로는 줍기를 잴 수 없다');
}

send(control, { type: 'gather', nodeId: controlSpot.id, did: 1 });
await wait(2000);
send(control, { type: 'bagask' });
await wait(1500);

const controlItems = [...control.bag.entries()];
if (controlItems.length === 0) {
	badLine.close();
	killWorld();
	cannotRun('회선을 안 끊었는데도 줍기가 안 됐다 — 재는 자가 고장 난 것이다');
}

const [itemId, controlAmount] = controlItems[0];
check('회선이 성하면 줍기가 된다 (대조군)', controlAmount > 0, `#${itemId}×${controlAmount}`);

// ── ② 끊기는 순간 보낸 줍기가 살아나나 ────────────────────────────────
const secondSpot = control.field.find((one) => one.id !== controlSpot.id);
if (secondSpot === undefined) {
	badLine.close();
	killWorld();
	cannotRun('들판 자리가 하나뿐이라 두 번째 줍기를 못 한다');
}

if (await walkTo(control, secondSpot) === false) {
	badLine.close();
	killWorld();
	cannotRun('두 번째 들판까지 못 걸어갔다');
}

const beforeAmount = howMany(control.bag);
const cutId = 2;
send(control, { type: 'gather', nodeId: secondSpot.id, did: cutId });
badLine.close();

await wait(3000);

badLine = newLine();
await badLine.listen();

const again = joinWorld(control.secret);
await wait(3000);
if (again.id === null) {
	badLine.close();
	killWorld();
	cannotRun('다시 못 붙었다');
}

// 창이 하는 그대로: 답 못 받은 것을 <b>같은 번호로</b> 다시 보낸다.
send(again, { type: 'gather', nodeId: secondSpot.id, did: cutId });
await wait(2000);
send(again, { type: 'bagask' });
await wait(1500);

const afterOne = howMany(again.bag);
check('끊기는 순간 누른 것이 다시 붙은 뒤 살아난다', afterOne > beforeAmount,
	`가방 ${beforeAmount} → ${afterOne} (안 살아나면 사람은 「눌렀는데 안 됐다」만 겪는다)`);

// ── ③ 같은 번호를 여러 번 보내도 한 번만 ──────────────────────────────
//
// ⚠ <b>줍기로는 못 잰다</b>: 한 번 주운 자리는 세계가 「아직 다시 자라는 중」이라 어차피 거절한다 —
//   중복 차단을 꺼도 초록이 나온다(실패 경로를 밟아 보고 알았다). 그래서 <b>같은 짓을 두 번 하면
//   두 번 되는</b> 일로 잰다: 먹기. 같은 번호로 세 번 보내 가방이 한 번만 줄면 옳다.
const eating = [...again.bag.entries()].find(([, amount]) => amount >= 2);
if (eating === undefined) {
	badLine.close();
	killWorld();
	cannotRun('두 개 이상 든 물건이 없어 「두 번 되나」를 못 잰다');
}

const beforeEating = howMany(again.bag);
const eatId = 3;
for (let i = 0; i < 3; i += 1) {
	send(again, { type: 'consume', itemId: eating[0], amount: 1, did: eatId });
	await wait(700);
}

send(again, { type: 'bagask' });
await wait(1500);

const afterEating = howMany(again.bag);
check('같은 번호를 여러 번 보내도 한 번만 된다', afterEating === beforeEating - 1,
	`가방 ${beforeEating} → ${afterEating} (한 번만 먹었으면 하나만 준다 · 세 번 먹으면 셋이 준다)`);

badLine.close();
killWorld();

if (failures === 0) {
	console.log('[lost-action] ✅ 끊기는 순간 누른 것이 사라지지도, 두 번 되지도 않는다');
	process.exit(0);
}

console.log(`\n[lost-action] RESULT: ${failures}건`);
process.exit(1);
