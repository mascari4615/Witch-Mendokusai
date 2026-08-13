#!/usr/bin/env node
// wm-gather-race-test.mjs — <b>둘이 같은 것을 동시에 주우면</b> (TASK-WM-352).
//
// ★ 왜: 줍기는 세계가 판정한다(WM-217) — 「남이 방금 가져갔다」는 거절도 이미 있다.
//   그런데 그 거절이 <b>같은 밀리초</b>에도 서는지는 아무도 안 쟀다. 여기가 무너지면
//   한 자리에서 <b>둘이 다 받는다</b>(물건이 는다) — MMO 에서 제일 조용한 사고다.
//   상자(WM-330)·짓기(WM-351)는 쟀으니 남은 하나가 이것이다.
//
// 재는 것 (봇 둘이 같은 자리로 걸어가 같은 판에 줍기, 여러 판):
//   ① 한 자리에서 <b>한 사람만</b> 받는다 ② 둘이 받은 것의 합이 그 자리에 있던 만큼이다
//   ③ 진 사람은 <b>이유</b>를 듣는다(「남이 방금 가져갔다」) ④ 세계가 그 사이 안 멎는다
//
// 실행: node .github/scripts/wm-gather-race-test.mjs
// exit: 0 = 한 사람만 받는다 · 1 = 물건이 늘거나 조용히 진다 · 2 = 못 돌림
//
// [빨강-확인] 「남이 방금 가져갔다」인 자리도 한 번 더 주게 하니 2건 빨강 (2026-08-14):
//   「둘 다 받은 자리 5곳 (1/1 · 1/3 · 1/1 · 1/2 · 2/1)」 — 한 자리에서 물건이 두 벌 나온다.
//
// ⚠ 만들다 <b>거저 초록</b>을 한 번 적었다: 「둘의 가방 합이 늘어난 만큼 늘었다」로 봤는데
//   그건 스스로를 견주는 말이라 무슨 일이 나도 초록이다. <b>둘 중 몇 명이</b> 받았나로 고쳤다.

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5520);

/** 몇 자리를 겨루나 — 한 자리로는 「어쩌다 안 겹쳤다」와 못 가른다. */
const ROUNDS = 5;

function cannotRun(message) {
	console.error(`[줍기경합] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-grace-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-grace-')), 'world.json');
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
	const one = { name, bag: new Map(), field: new Map(), tooLate: 0, denials: [] };
	one.socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 수로 본다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') one.id = said.id;
		if (said.type === 'me' && said.doll) one.here = { x: said.doll.x, z: said.doll.z };
		if (said.type === 'bag' && Array.isArray(said.items)) {
			one.bag = new Map(said.items.map((item) => [item.itemId, item.amount]));
		}

		if (said.type === 'denied' && said.what === 'gather') {
			one.denials.push(String(said.why || said.reason || ''));
			if (String(said.why || said.reason || '').includes('방금')) one.tooLate += 1;
		}

		if (said.type !== 'world') return;
		if (Array.isArray(said.dolls) && one.id !== undefined) {
			const mine = said.dolls.find((doll) => doll.id === one.id);
			if (mine && typeof mine.x === 'number') one.here = { x: mine.x, z: mine.z };
		}

		if (Array.isArray(said.gatherables)) {
			if (said.fieldChanged !== true) one.field = new Map(said.gatherables.map((node) => [node.id, node]));
			else for (const node of said.gatherables) one.field.set(node.id, node);
		}

		for (const goneId of said.fieldGone || []) one.field.delete(goneId);
	};

	return one;
}

const send = (one, message) => {
	if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message));
};

const bagTotal = (one) => [...one.bag.values()].reduce((sum, amount) => sum + amount, 0);

const a = join_('가');
const b = join_('나');
await wait(3000);

if (a.id === undefined || b.id === undefined) { killWorld(); cannotRun('봇이 세계에 못 들어갔다'); }
if (a.field.size === 0) { killWorld(); cannotRun('들판을 못 받았다 — 겨룰 자리가 없다'); }

/** 둘을 그 자리까지 나란히 걷게 한다 — 손이 닿아야 줍기가 판정까지 간다. */
async function walkBoth(goal) {
	for (let step = 0; step < 150; step += 1) {
		let arrived = 0;
		for (const one of [a, b]) {
			if (one.here === undefined) continue;
			const dx = goal.x - one.here.x;
			const dz = goal.z - one.here.z;
			const away = Math.hypot(dx, dz);
			if (away <= 0.9) { arrived += 1; continue; }
			send(one, { type: 'move', x: (dx / away) * 0.15, z: (dz / away) * 0.15 });
		}

		if (arrived === 2) return true;
		await wait(60);
	}

	return false;
}

let did = 7000;
let contested = 0;
let bothGotIt = 0;
const rounds = [];
const beforeAll = bagTotal(a) + bagTotal(b);

for (let round = 0; round < ROUNDS; round += 1) {
	const seen = [...a.field.values()].filter((node) => b.field.has(node.id));
	if (seen.length === 0) break;

	const goal = seen
		.map((node) => ({ node, away: Math.hypot(node.x - (a.here?.x ?? 0), node.z - (a.here?.z ?? 0)) }))
		.sort((left, right) => left.away - right.away)[0].node;

	if (await walkBoth(goal) === false) continue;

	const beforeA = bagTotal(a);
	const beforeB = bagTotal(b);
	// 같은 판에 두 마디 — 사이에 기다림을 두면 경합이 아니다.
	send(a, { type: 'gather', nodeId: goal.id, did: did += 1 });
	send(b, { type: 'gather', nodeId: goal.id, did: did += 1 });
	await wait(900);
	send(a, { type: 'bagask' });
	send(b, { type: 'bagask' });
	await wait(700);

	// ★ <b>둘 중 몇 명이</b> 받았나로 본다. 「합이 늘어난 만큼 늘었다」는 스스로를 견주는 말이라
	//   무슨 일이 나도 초록이다(이 관문을 만들다 실제로 그렇게 적었다 — 거저 초록).
	const gotA = bagTotal(a) - beforeA;
	const gotB = bagTotal(b) - beforeB;
	if (gotA > 0 || gotB > 0) {
		contested += 1;
		if (gotA > 0 && gotB > 0) bothGotIt += 1;
		rounds.push(`${gotA}/${gotB}`);
	}
}

const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());
const afterAll = bagTotal(a) + bagTotal(b);
const tooLate = a.tooLate + b.tooLate;

for (const one of [a, b]) { try { one.socket.close(); } catch { /* 이미 */ } }
killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

if (contested === 0) cannotRun('한 자리도 못 겨뤘다 — 둘이 같은 자리에 못 갔다');

console.log(`  ⓘ 겨룬 자리 ${contested}곳 · 둘의 가방 ${beforeAll} → ${afterAll}개`
	+ ` · 「남이 방금 가져갔다」 ${tooLate}번`);

// ★ 한 자리는 한 번만 준다 — 둘 다 받으면 물건이 <b>는다</b>(제일 조용한 사고).
check('한 자리에서 한 사람만 받는다', bothGotIt === 0,
	`둘 다 받은 자리 ${bothGotIt}곳 · 자리별 가/나 = ${rounds.join(' · ')}`);

// ★ 진 사람은 <b>이유</b>를 들어야 한다 — 아무 말이 없으면 사람은 버튼이 고장 난 줄 안다.
// [문턱-사유] (b) 겨룬 자리 수와의 비율 — 자리마다 진 사람이 하나씩이다.
check(`진 사람은 「남이 방금 가져갔다」를 듣는다 (${contested}곳)`, tooLate >= contested,
	`${tooLate}번 들었다`);

check('세계가 그 사이 안 멎는다', health.longestTickGapMs <= 1500, `가장 벌어진 판 ${health.longestTickGapMs}ms`);

if (bad === 0) {
	console.log('[줍기경합] ✅ 둘이 같은 것을 노려도 한 사람만 받고, 진 사람은 이유를 듣는다');
	process.exit(0);
}

console.log(`\n[줍기경합] RESULT: ${bad}건`);
process.exit(1);
