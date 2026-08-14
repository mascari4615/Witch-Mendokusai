#!/usr/bin/env node
// wm-stay-and-go-test.mjs — <b>안 떠나고 통행증만 쓰면</b> 짐이 두 벌이 되나 (TASK-WM-377).
//
// 세계는 통행증을 <b>먼저 쥐여 주고</b>, 창이 실제로 떠나면(줄이 닫히면) 그때 이 세계에서 내보낸다.
// 안 닫으면 유예(5초) 뒤 「안 넘어갔다」로 보고 <b>그대로 여기 산다</b>.
// 그런데 그 종이는 아직 30초 동안 살아 있다 — 다른 창으로 저쪽에 내밀면?
//
// 고침: <b>받은 세계가 보낸 세계에 알린다</b>(landed) — 도착은 받은 쪽만 확실히 안다.
//   보낸 세계는 그 말을 듣고 그 사람을 놓는다(붙어 있는 창도 닫는다).
//
// [빨강-확인] 고치기 <b>전</b> 이 자가 빨갰다 — 「동 20 + 서 20 = 40 (있던 것 20)」 (2026-08-14).
//   고친 뒤 「동 0 + 서 20 = 20」 이고 동쪽 창은 닫힌다(줄 상태 3).
//
// exit: 0 = 한 벌 · 1 = 두 벌(복제) · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5432);
const westPort = eastPort + 1;
const SECRET = 'stay-and-go-probe-secret';
const STOCK = 20;
const ITEM_ID = 1;

const cannotRun = (m) => { console.error(`[안떠나고] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-stay-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours, seed) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-stay-')), 'world.json');
	if (seed) writeFileSync(worldFile, JSON.stringify(seed), 'utf8');
	worlds.push(spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: worldFile, WM_ZONE: zone, WM_ZONE_NEIGHBOURS: neighbours, WM_ZONE_SECRET: SECRET },
		stdio: 'ignore',
	}));
}
function killWorlds() {
	for (const one of worlds) {
		try {
			if (process.platform === 'win32') execSync(`taskkill /PID ${one.pid} /F /T`, { stdio: 'ignore' });
			else one.kill('SIGKILL');
		} catch { /* 이미 죽었다 */ }
	}
	worlds.length = 0;
}

startWorld(eastPort, 'east:0,-40,40,40', `west:-40,-40,0,40=ws://127.0.0.1:${westPort}/ws`, {
	buildings: [{ x: 2, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: 2, y: 0, z: 0, items: [{ itemId: ITEM_ID, amount: STOCK }] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
});
startWorld(westPort, 'west:-40,-40,0,40', `east:0,-40,40,40=ws://127.0.0.1:${eastPort}/ws`, {
	buildings: [{ x: -2, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: -2, y: 0, z: 0, items: [] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
});

for (const port of [eastPort, westPort]) {
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try { const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }); if (answer.ok) { up = true; break; } } catch { /* 아직 */ }
		await wait(300);
	}
	if (up === false) { killWorlds(); cannotRun(`세계가 안 떴다 (${port})`); }
}

function joinWorld(port, { secret = '', pass = '' } = {}) {
	const one = { id: null, secret: '', moveOn: null, bag: new Map(), chest: null };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify(pass ? { type: 'hello', secret, pass } : { type: 'hello', secret }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'chest') one.chest = said;
		if (said.type === 'welcome') { one.id = said.id; one.secret = said.secret ?? ''; }
		if (said.type === 'moveon') one.moveOn = said;
		if (said.type === 'bag') one.bag = new Map((said.items || []).map((row) => [row.itemId, row.amount]));
	};
	return one;
}
const send = (one, message) => { if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message)); };

const walker = joinWorld(eastPort);
{
	const until = Date.now() + 30000;
	while (Date.now() < until && walker.id === null) await wait(100);
	if (walker.id === null) { killWorlds(); cannotRun('동쪽에 못 들어갔다'); }
}
send(walker, { type: 'move', x: 2, z: 0, seq: 1 });
await wait(1200);
send(walker, { type: 'chesttake', x: 2, y: 0, z: 0, itemId: ITEM_ID, amount: STOCK, did: 1 });
await wait(1500);
send(walker, { type: 'bagask' });
await wait(1200);
const packed = walker.bag.get(ITEM_ID) ?? 0;
if (packed !== STOCK) { killWorlds(); cannotRun(`짐을 못 챙겼다 (${packed}/${STOCK})`); }

for (let step = 0; step < 900 && walker.moveOn === null; step += 1) {
	send(walker, { type: 'move', x: -0.15, z: 0, seq: 100 + step });
	await wait(50);
}
if (walker.moveOn === null || !walker.moveOn.pass) { walker.socket.close(); killWorlds(); cannotRun('국경에서 통행증을 못 받았다'); }
const pass = walker.moveOn.pass;

// ★ 여기가 이 자의 핵심 — <b>줄을 안 닫는다</b>. 유예(5초)를 넘겨 기다린다.
console.log('[안떠나고] 통행증을 받았지만 줄을 안 닫는다 — 유예를 넘겨 기다린다');
await wait(8000);

send(walker, { type: 'bagask' });
await wait(1500);
const stillEast = walker.bag.get(ITEM_ID) ?? 0;
console.log(`[안떠나고] 동쪽에 남은 내 가방: ${stillEast}`);

// 같은 종이를 <b>다른 창</b>으로 서쪽에 내민다.
const ghost = joinWorld(westPort, { pass });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && ghost.id === null) await wait(100);
	if (ghost.id === null) { walker.socket.close(); killWorlds(); cannotRun('서쪽에 못 들어갔다'); }
}
await wait(1500);
send(ghost, { type: 'bagask' });
await wait(1500);
const inWest = ghost.bag.get(ITEM_ID) ?? 0;
console.log(`[안떠나고] 서쪽에 도착한 가방: ${inWest}`);

// ★ 동쪽은 <b>도착한 뒤에</b> 다시 센다 — 도착 전 값을 세면 고쳐 놓고도 두 벌로 보인다(내 자 탓, 첫 판).
walker.bag = new Map();
send(walker, { type: 'bagask' });
await wait(2000);
const eastAfter = walker.socket.readyState === 1 ? (walker.bag.get(ITEM_ID) ?? 0) : 0;
console.log(`[안떠나고] 도착 뒤 동쪽 가방: ${eastAfter} (줄 상태 ${walker.socket.readyState})`);

walker.socket.close();
ghost.socket.close();
await wait(300);
killWorlds();

const total = eastAfter + inWest;
console.log(`[안떠나고] 세상의 그 물건: 동 ${eastAfter} + 서 ${inWest} = ${total} (있던 것 ${STOCK} · 도착 전 동쪽 ${stillEast})`);
if (total > STOCK) { console.log('[안떠나고] ❌ 두 벌이다 — 안 떠나고도 통행증이 먹힌다'); process.exit(1); }
console.log('[안떠나고] ✅ 한 벌');
process.exit(0);
