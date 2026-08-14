#!/usr/bin/env node
// wm-build-race-test.mjs — <b>둘이 같은 자리에 동시에 지으면</b> (TASK-WM-351).
//
// ★ 왜: 세계가 판정하는 겹침은 서버 시험이 지킨다 — 그런데 그건 <b>한 사람씩</b> 부를 때다.
//   MMO 의 진짜 자리는 <b>같은 밀리초</b>다: 둘이 같은 칸을 노리면 ① 집이 둘 서거나
//   ② 진 사람의 재료가 사라지거나(먼저 빼고 못 지으면 돌려주는 길이 있다) ③ 둘 다 못 짓는다.
//   상자 경합(WM-330)은 쟀는데 <b>짓기 경합</b>은 한 번도 안 쟀다.
//
// 재는 것 (봇 둘 · 같은 칸 · 같은 순간, 여러 판):
//   ① 그 칸에 선 집은 <b>하나</b>다 ② 둘의 재료 합이 <b>맞는다</b>(진 사람 것은 돌아온다)
//   ③ 세계가 그 사이 안 멎는다
//
// 실행: node .github/scripts/wm-build-race-test.mjs
// exit: 0 = 하나만 선다 · 1 = 둘 서거나 재료가 샌다 · 2 = 못 돌림
//
// [빨강-확인] 진 사람에게 재료를 안 돌려주게 하니 빨강 (2026-08-14):
//   「쓴 재료 32개 · 선 집 8채 × 2 = 16개」 — 겨루기에서 진 사람마다 재료가 조용히 사라진다.
//
// ⓘ 실측 (2026-08-14, 8판): 선 집 8채(판마다 하나) · 쓴 재료 16개 = 선 집 × 값. 세계는 이미 성했다 —
//   이 관문은 <b>그 성함을 지키는 자물쇠</b>다(짓기 경합은 여태 아무도 안 재고 있었다).

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5510);

/** 모루(4001) — 재료는 10번 물건 2개다(buildings.json). 창에게 안 묻고 세계 목록이 정본이다. */
const BUILDING_ID = 4001;
const COST_ITEM = 10;
const COST_AMOUNT = 2;

/** 몇 판을 겨루나 — 한 판으로는 「어쩌다 안 겹쳤다」와 못 가른다. */
const ROUNDS = 8;

/** 재료를 넉넉히 — 판마다 둘이 각자 2개씩 쓴다(못 지으면 돌아온다). */
const STOCK = ROUNDS * COST_AMOUNT * 4;

function cannotRun(message) {
	console.error(`[짓기경합] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-brace-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

// 재료가 든 상자 하나를 심은 세계 — 「주우러 다니는」 절차를 안 거쳐야 시험이 짧고 곧다.
const CHEST = { x: 0, y: 0, z: 0 };
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-brace-')), 'world.json');
writeFileSync(worldFile, JSON.stringify({
	buildings: [{ x: CHEST.x, y: CHEST.y, z: CHEST.z, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: CHEST.x, y: CHEST.y, z: CHEST.z, items: [{ itemId: COST_ITEM, amount: STOCK }] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
	people: [], gathered: [], cauldrons: [],
}), 'utf8');

const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll), env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore',
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
	if (up === false) { killWorld(); cannotRun('세계가 안 떴다'); }
}

function join_(name) {
	const one = { name, bag: new Map(), denied: 0, buildings: 0 };
	one.socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 수로 본다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') one.id = said.id;
		if (said.type === 'bag' && Array.isArray(said.items)) {
			one.bag = new Map(said.items.map((item) => [item.itemId, item.amount]));
		}

		if (said.type === 'denied' && String(said.what || '').includes('place')) one.denied += 1;
		if (said.type === 'world' && Array.isArray(said.buildings)) one.buildings = said.buildings.length;
	};

	return one;
}

const send = (one, message) => {
	if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message));
};

const a = join_('가');
const b = join_('나');
await wait(2500);

if (a.id === undefined || b.id === undefined) { killWorld(); cannotRun('봇이 세계에 못 들어갔다'); }

let did0 = 100;
// 상자에서 재료를 꺼내 나눈다.
for (const one of [a, b]) {
	send(one, { type: "chesttake", x: CHEST.x, y: CHEST.y, z: CHEST.z, itemId: COST_ITEM, amount: ROUNDS * COST_AMOUNT, did: did0 += 1 });
	await wait(400);
	send(one, { type: 'bagask' });
	await wait(400);
}

const stockA = a.bag.get(COST_ITEM) || 0;
const stockB = b.bag.get(COST_ITEM) || 0;
if (stockA < ROUNDS * COST_AMOUNT || stockB < ROUNDS * COST_AMOUNT) {
	killWorld();
	cannotRun(`재료를 못 나눠 가졌다 — 가 ${stockA}개 · 나 ${stockB}개`);
}

// ── 같은 칸을 같은 순간에 노린다 ────────────────────────────────────────────
let did = 5000;
for (let round = 0; round < ROUNDS; round += 1) {
	const cell = { x: 10 + round, y: 0, z: 10 };
	// 두 마디를 <b>같은 판에</b> 밀어 넣는다 — 사이에 기다림을 두면 경합이 아니다.
	send(a, { type: 'place', ...cell, buildingId: BUILDING_ID, did: did += 1 });
	send(b, { type: 'place', ...cell, buildingId: BUILDING_ID, did: did += 1 });
	await wait(400);
}

await wait(1500);
for (const one of [a, b]) { send(one, { type: 'bagask' }); await wait(400); }

const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());
const seen = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

const leftA = a.bag.get(COST_ITEM) || 0;
const leftB = b.bag.get(COST_ITEM) || 0;

for (const one of [a, b]) { try { one.socket.close(); } catch { /* 이미 */ } }
killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

// 심어 둔 상자 1채 + 판마다 한 채씩만 서야 한다.
const built = seen.buildings - 1;
console.log(`  ⓘ ${ROUNDS}판 · 선 집 ${built}채 · 남은 재료 가 ${leftA}개 · 나 ${leftB}개 (각자 ${ROUNDS * COST_AMOUNT}개로 시작)`);

check(`같은 칸에는 한 채만 선다 (${ROUNDS}판)`, built === ROUNDS, `${built}채 섰다 (판마다 하나면 ${ROUNDS}채)`);

// ★ 진 사람의 재료는 <b>돌아와야</b> 한다 — 먼저 빼고 못 지으면 돌려주는 길이 그래서 있다.
//   둘이 쓴 재료의 합 = 선 집 수 × 값. 그 위면 <b>재료가 샌 것</b>이다.
const spent = (ROUNDS * COST_AMOUNT - leftA) + (ROUNDS * COST_AMOUNT - leftB);
check('진 사람의 재료는 돌아온다', spent === built * COST_AMOUNT,
	`쓴 재료 ${spent}개 · 선 집 ${built}채 × ${COST_AMOUNT} = ${built * COST_AMOUNT}개`);

// [문턱-사유] (c) 사람이 느끼는 선 — 세계가 1.5초 넘게 멎으면 걷던 사람이 그 자리에서 튄다.
//   (다른 관문들과 같은 값 — many-windows·crowd-windows 도 이 선을 쓴다.)
const MOST_TICK_GAP_MS = 1500;
check('세계가 그 사이 안 멎는다', health.longestTickGapMs <= MOST_TICK_GAP_MS, `가장 벌어진 판 ${health.longestTickGapMs}ms`);

if (bad === 0) {
	console.log('[짓기경합] ✅ 둘이 같은 자리를 노려도 한 채만 서고, 진 사람 재료는 돌아온다');
	process.exit(0);
}

console.log(`\n[짓기경합] RESULT: ${bad}건`);
process.exit(1);
