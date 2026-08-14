#!/usr/bin/env node
// wm-slow-crossing-test.mjs — 국경을 <b>느리게</b> 넘다 통행증이 죽으면 짐은 어디 있나 (TASK-WM-385).
//
// ★ 왜: 통행증은 30초를 산다. 회선이 나쁘거나 창이 느리면 그 안에 저쪽에 못 붙는다 —
//   그때 그 사람은 <b>손님</b>으로 도착한다(도장이 죽었으니까). 보낸 세계는 이미 그를 지웠고,
//   받은 세계는 그를 모른다 ⇒ 가방·자리·몸이 <b>통째로</b> 사라진다.
//   이건 복제보다 나쁘다 — 복제는 세상이 부자가 되지만 이건 그 사람이 잃는다.
//
// 고침: 보낸 세계는 <b>도착이 확인될 때</b>(landed, WM-377·381) 기억을 지운다.
//   못 넘은 사람은 여기 제 짐 그대로 있고, 넘은 사람은 저쪽 소식이 오는 즉시 여기서 사라진다.
//
// [빨강-확인] 고치기 전 이 자가 빨갰다 — 「동 0 + 서 0 (들고 떠난 것 20)」 (2026-08-14).
//   고친 뒤 「동 20 + 서 0」.
//
// exit: 0 = 짐이 어딘가에 남아 있다 · 1 = 사라졌다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5448);
const westPort = eastPort + 1;
const SECRET = 'slow-crossing-gate-secret';
const STOCK = 20;
const ITEM_ID = 1;

const cannotRun = (m) => { console.error(`[느린국경] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-slow-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours, seed) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-slow-')), 'world.json');
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

const walkerSecret = walker.secret;

// ★ 여기가 이 자의 핵심 — 제대로 떠나고, 통행증이 <b>죽을 때까지</b> 기다린다(30초 + 여유).
walker.socket.close();
console.log('[느린국경] 떠났다 — 통행증이 죽을 때까지 기다린다 (35초)');
await wait(35000);

// 뒤늦게 서쪽에 닿는다 — 도장은 이미 죽었다.
const late = joinWorld(westPort, { secret: walkerSecret, pass });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && late.id === null) await wait(100);
	if (late.id === null) { killWorlds(); cannotRun('서쪽에 아예 못 들어갔다'); }
}
await wait(1500);
send(late, { type: 'bagask' });
await wait(1500);
const inWest = late.bag.get(ITEM_ID) ?? 0;
late.socket.close();
await wait(500);
console.log(`[느린국경] 늦게 도착한 서쪽 가방: ${inWest}`);

// 돌아서 동쪽으로 다시 온다 — 제 열쇠로. 여기 남아 있어야 한다.
const back = joinWorld(eastPort, { secret: walkerSecret });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && back.id === null) await wait(100);
	if (back.id === null) { killWorlds(); cannotRun('동쪽에 다시 못 들어갔다'); }
}
await wait(1500);
send(back, { type: 'bagask' });
await wait(1500);
const inEast = back.bag.get(ITEM_ID) ?? 0;
back.socket.close();
await wait(300);
killWorlds();

console.log(`[느린국경] 그 물건: 동 ${inEast} + 서 ${inWest} (들고 떠난 것 ${STOCK})`);
if (inEast + inWest < STOCK) {
	console.log('[느린국경] ❌ 사라졌다 — 느리게 넘다 통행증이 죽으면 그 사람이 통째로 잃는다');
	process.exit(1);
}
if (inEast + inWest > STOCK) {
	console.log('[느린국경] ❌ 두 벌이다');
	process.exit(1);
}
console.log('[느린국경] ✅ 짐은 그대로 어딘가에 있다');
process.exit(0);
