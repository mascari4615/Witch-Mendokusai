#!/usr/bin/env node
// wm-crash-durability-test.mjs — <b>「했다」고 답한 일은 세계가 갑자기 죽어도 남는다</b> (TASK-WM-310).
//
// ★ 무엇이 있었나 (실측 2026-08-13): 세계는 줍기에 「그거 했다」(did, WM-305)고 답한 뒤
//   최대 <b>5초</b>를 디스크에 안 적었다. 그 사이에 세계가 갑자기 죽으면 <b>답해 놓고 없던 일</b>이 된다 —
//   세 판 중 <b>한 판</b>에서 주운 물건이 사라졌다. 사람에게는 「분명히 주웠는데」만 남는다.
//   (노트북이 메모리 고갈로 강제 재부팅된 날이라 이건 상상이 아니라 <b>그날 실제로 일어난 꼴</b>이다.)
//
// ★ 고친 자리: 「했다」를 저장 뒤로 <b>미루지 않는다</b>(그러면 손맛이 통째로 느려진다 — WM-283).
//   대신 <b>적는 쪽을 당긴다</b>: 사람이 한 일이 생기면 0.3초 안에 적는다(그 밖의 것은 예전대로 5초).
//
// 재는 것: 줍는다 → 세계가 「했다」 → <b>1초</b> 뒤 세계를 <b>강제로</b> 죽인다(taskkill /F) →
//   되살린다 → 가방이 그대로인가.
//
// exit: 0 = 남는다 · 1 = 사라졌다 · 2 = 못 돌림
//
// [빨강-확인] 빠른 저장(300ms → 300000ms)을 끄니 「답해 놓고 없던 일」 빨강 (가방 1 → 1)

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5580);

/** 「했다」를 받고 이만큼 뒤에 죽인다 — 세계가 약속한 창(0.3초)보다 넉넉히 길다. */
const KILL_AFTER_MS = 1000;

function cannotRun(message) {
	console.error(`[crash-durability] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-crash-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

// ⚠ 세계 파일은 <b>같은 것</b>을 계속 쓴다 — 죽였다 살릴 때 그 파일이 곧 기억이다.
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-crash-')), 'world.json');

let world = null;
function startWorld() {
	world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: worldFile },
		stdio: 'ignore',
	});
}

/** <b>강제로</b> 죽인다 — 곱게 닫으면 세계가 마지막에 한 번 적을 기회를 얻어 시험이 무의미해진다. */
function killWorldHard() {
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
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) return true;
		} catch { /* 아직 */ }
		await wait(300);
	}

	return false;
}

function joinWorld(secret) {
	const one = { id: null, secret: '', here: undefined, bag: new Map(), field: [], acked: [] };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: secret || '' }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'welcome') { one.id = said.id; one.secret = said.secret; }
		if (said.type === 'did') one.acked.push(said.did);
		if (said.type === 'me') one.here = { x: said.x, z: said.z };
		if (said.type === 'world') {
			if (Array.isArray(said.gatherables) && said.gatherables.length > 0) one.field = said.gatherables;
			if (Array.isArray(said.dolls) && one.id !== null) {
				const mine = said.dolls.find((doll) => doll.id === one.id);
				if (mine !== undefined) one.here = { x: mine.x, z: mine.z };
			}
		}

		if (said.type === 'bag' && Array.isArray(said.items)) one.bag = new Map(said.items.map((item) => [item.itemId, item.amount]));
	};

	return one;
}

const send = (one, message) => {
	if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message));
};

const bagSize = (one) => [...one.bag.values()].reduce((sum, amount) => sum + amount, 0);

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

startWorld();
if (await waitHealthy(120000) === false) {
	killWorldHard();
	cannotRun('세계가 안 떴다');
}

const me = joinWorld('');
await wait(3000);
for (let i = 0; i < 40 && me.field.length === 0; i += 1) await wait(250);

if (me.id === null || me.field.length === 0) {
	killWorldHard();
	cannotRun('세계에 못 들어갔거나 들판을 못 받았다');
}

const mineNow = me.here || { x: 0, z: 0 };
const nearby = [...me.field].sort((one, other) =>
	Math.hypot(one.x - mineNow.x, one.z - mineNow.z) - Math.hypot(other.x - mineNow.x, other.z - mineNow.z));

// 먼저 하나 주워서 <b>저장이 한 번 지나가게</b> 한다 — 그래야 다음 줍기가 「저장 사이」에 놓인다.
for (const spot of nearby.slice(0, 5)) {
	if (bagSize(me) > 0) break;
	if (await walkTo(me, spot) === false) continue;

	send(me, { type: 'gather', nodeId: spot.id, did: spot.id });
	await wait(900);
	send(me, { type: 'bagask' });
	await wait(900);
}

if (bagSize(me) === 0) {
	killWorldHard();
	cannotRun('아무것도 못 주웠다 — 이 상태로는 「살아남나」를 잴 수 없다');
}

await wait(6000); // 느긋한 저장(5초)이 한 번 지나가게 둔다.
send(me, { type: 'bagask' });
await wait(1200);

const before = bagSize(me);
const second = nearby.find((one) => me.field.some((alive) => alive.id === one.id));
if (second === undefined || (await walkTo(me, second)) === false) {
	killWorldHard();
	cannotRun('두 번째 들판까지 못 갔다');
}

// ── 여기가 핵심: 「했다」를 받고 곧바로 강제 종료 ──────────────────────
const deedId = 424242;
const ackedBefore = me.acked.length;
send(me, { type: 'gather', nodeId: second.id, did: deedId });

{
	const until = Date.now() + 5000;
	while (Date.now() < until && me.acked.includes(deedId) === false) await wait(50);
}

if (me.acked.includes(deedId) === false) {
	killWorldHard();
	cannotRun(`세계가 「했다」를 안 줬다 (답 ${me.acked.length - ackedBefore}개) — 이 시험은 그 답을 전제로 한다`);
}

await wait(KILL_AFTER_MS);
killWorldHard();

await wait(1500);
startWorld();
if (await waitHealthy(120000) === false) {
	cannotRun('되살린 세계가 안 떴다');
}

const again = joinWorld(me.secret);
await wait(3500);
send(again, { type: 'bagask' });
await wait(2000);

const after = bagSize(again);
killWorldHard();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 「했다」를 받고 ${KILL_AFTER_MS}ms 뒤 강제 종료 — 가방 ${before} → (주움) → ${after}`);

check('「했다」한 일이 갑작스런 죽음에도 남는다', after > before,
	after > before
		? `가방 ${before} → ${after}`
		: `가방 ${before} → ${after} — 답해 놓고 없던 일이 됐다(예전 실측: 세 판 중 한 판)`);

check('세계가 되살아났다', after >= 0, `가방 ${after}개를 다시 셌다`);

if (failures === 0) {
	console.log('[crash-durability] ✅ 답한 일은 죽어도 남는다');
	process.exit(0);
}

console.log(`\n[crash-durability] RESULT: ${failures}건`);
process.exit(1);
