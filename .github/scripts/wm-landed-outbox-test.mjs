#!/usr/bin/env node
// wm-landed-outbox-test.mjs — <b>받은 세계가 껐다 켜져도</b> 도착 소식이 결국 간다 (TASK-WM-381).
//
// 세계는 통행증을 <b>먼저 쥐여 주고</b>, 창이 실제로 떠나면(줄이 닫히면) 그때 이 세계에서 내보낸다.
// 안 닫으면 유예(5초) 뒤 「안 넘어갔다」로 보고 <b>그대로 여기 산다</b>.
// 그런데 그 종이는 아직 30초 동안 살아 있다 — 다른 창으로 저쪽에 내밀면?
//
// ★ 왜: 도착 소식(landed, WM-377)은 <b>그 순간</b> 이웃 줄이 있어야 나간다. 없으면 조용히 흘러가고
//   그 사람은 두 세계에 남는다(가방 두 벌). 세계는 서로 껐다 켜지므로 이 판은 <b>드문 일이 아니다</b>.
//
// ★ 왜: WM-378 은 못 나간 도착 소식을 <b>기억에</b> 들고 있게 했다. 그런데 세계는 배포마다 껐다 켜진다 —
//   그 사이에 기억이 날아가면 그 사람은 영영 두 세계에 남는다(가방 두 벌).
//   여기서는 도착 <b>뒤에 받은 세계를 껐다 켜고</b>, 그 다음에야 이웃 줄을 잇는다.
//
// 고침: 못 보낸 도착 소식을 세계 기억 <b>옆에</b> 적어 둔다(`<world>.landed.json`) — 뜨면 다시 보낸다.
//
// [빨강-확인] 적어 두기 전 이 자가 빨갰다 — 「동 20 + 서 20 = 40」 (2026-08-14).
//   ⚠ 첫 판은 <b>내 자가 틀려</b> 초록이었다: 껐다 켠 뒤 확인하러 통행증을 다시 냈더니
//   그것이 <b>새 도착</b>이 되어 소식이 새로 나갔다. 확인은 <b>기기 열쇠로만</b> 붙어서 한다.
//
// exit: 0 = 한 벌 · 1 = 두 벌(복제) · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { createServer, connect } from 'node:net';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5440);
const westPort = eastPort + 1;
// ★ 서쪽이 동쪽에게 말할 때 지나는 <b>중계 문</b> — 이 문은 도착 <b>뒤에</b> 연다(줄이 늦게 이어지는 판).
const relayPort = eastPort + 2;
let relay = null;
function openRelay() {
	relay = createServer((incoming) => {
		const outgoing = connect(eastPort, '127.0.0.1');
		incoming.pipe(outgoing);
		outgoing.pipe(incoming);
		const bury = () => { try { incoming.destroy(); } catch { /* 이미 */ } try { outgoing.destroy(); } catch { /* 이미 */ } };
		incoming.on('error', bury);
		outgoing.on('error', bury);
	});
	relay.listen(relayPort, '127.0.0.1');
}
const SECRET = 'landed-outbox-gate-secret';
const STOCK = 20;
const ITEM_ID = 1;

const cannotRun = (m) => { console.error(`[도착장부] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-outbox-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
// ★ 세계 파일을 <b>밖에서</b> 정한다 — 서쪽은 껐다 켜야 하므로 같은 자리를 다시 써야 한다.
function startWorld(port, zone, neighbours, seed, worldFile) {
	const file = worldFile ?? join(mkdtempSync(join(tmpdir(), 'wm-outbox-')), 'world.json');
	if (seed) writeFileSync(file, JSON.stringify(seed), 'utf8');
	const child = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: file, WM_ZONE: zone, WM_ZONE_NEIGHBOURS: neighbours, WM_ZONE_SECRET: SECRET },
		stdio: 'ignore',
	});
	worlds.push(child);
	return child;
}

function killOne(child) {
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${child.pid} /F /T`, { stdio: 'ignore' });
		else child.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
	const at = worlds.indexOf(child);
	if (at >= 0) worlds.splice(at, 1);
}

async function waitUntilUp(port, patienceMs = 120000) {
	const until = Date.now() + patienceMs;
	while (Date.now() < until) {
		try { const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }); if (answer.ok) return true; } catch { /* 아직 */ }
		await wait(300);
	}
	return false;
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
const westFile = join(mkdtempSync(join(tmpdir(), 'wm-outbox-west-')), 'world.json');
const westZone = 'west:-40,-40,0,40';
const westNeighbours = `east:0,-40,40,40=ws://127.0.0.1:${relayPort}/ws`;
let west = startWorld(westPort, westZone, westNeighbours, {
	buildings: [{ x: -2, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: -2, y: 0, z: 0, items: [] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
}, westFile);

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

// ★ 여기가 이 자의 핵심 — <b>줄을 안 닫는다</b>. 유예(5초)를 넘겨 기다린다.
console.log('[도착장부] 통행증을 받았지만 줄을 안 닫는다 — 유예를 넘겨 기다린다');
await wait(8000);

send(walker, { type: 'bagask' });
await wait(1500);
const stillEast = walker.bag.get(ITEM_ID) ?? 0;
console.log(`[도착장부] 동쪽에 남은 내 가방: ${stillEast}`);

// 같은 종이를 <b>다른 창</b>으로 서쪽에 내민다.
// 진짜 창처럼 <b>기기 열쇠와 통행증을 같이</b> 낸다 — 그래야 껐다 켠 뒤 열쇠만으로 다시 붙는다.
const ghost = joinWorld(westPort, { secret: walkerSecret, pass });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && ghost.id === null) await wait(100);
	if (ghost.id === null) { walker.socket.close(); killWorlds(); cannotRun('서쪽에 못 들어갔다'); }
}
await wait(1500);
send(ghost, { type: 'bagask' });
await wait(1500);
const inWest = ghost.bag.get(ITEM_ID) ?? 0;
console.log(`[도착장부] 서쪽에 도착한 가방: ${inWest}`);

// ★ 여기가 이 관문의 핵심 — 도착 <b>뒤에 받은 세계를 껐다 켠다</b>. 기억에만 든 소식은 여기서 날아간다.
console.log('[도착장부] 받은 세계를 껐다 켠다');
ghost.socket.close();
await wait(1500);
killOne(west);
await wait(1500);
west = startWorld(westPort, westZone, westNeighbours, null, westFile);
if (await waitUntilUp(westPort) === false) { killWorlds(); cannotRun('서쪽이 다시 안 떴다'); }

// 도착 뒤에 이웃 줄이 이어진다.
//   그때까지 도착 소식은 <b>나갈 데가 없었다</b>. 나중에라도 가야 두 벌이 안 남는다.
console.log('[도착장부] 이제야 이웃 줄이 이어진다 — 중계 문을 연다');
openRelay();
await wait(12000);

// 서쪽은 껐다 켠 뒤 다시 붙어 확인한다 — 짐이 그대로 있어야 잴 것이 있다.
// ⚠ 여기서 통행증을 <b>다시 내면 안 된다</b> — 그 자체가 새 도착이라 소식이 새로 나간다
//   (첫 판에 그렇게 재다가 초록으로 봤다. 자가 재려는 것을 만들어 버렸다).
const backWest = joinWorld(westPort, { secret: walkerSecret });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && backWest.id === null) await wait(100);
}
await wait(1500);
send(backWest, { type: 'bagask' });
await wait(1500);
const westAfterSleep = backWest.bag.get(ITEM_ID) ?? 0;
console.log(`[도착장부] 껐다 켠 뒤 서쪽 가방: ${westAfterSleep}`);
backWest.socket.close();

// ★ 동쪽은 <b>도착한 뒤에</b> 다시 센다 — 도착 전 값을 세면 고쳐 놓고도 두 벌로 보인다(내 자 탓, 첫 판).
walker.bag = new Map();
send(walker, { type: 'bagask' });
await wait(2000);
const eastAfter = walker.socket.readyState === 1 ? (walker.bag.get(ITEM_ID) ?? 0) : 0;
console.log(`[도착장부] 도착 뒤 동쪽 가방: ${eastAfter} (줄 상태 ${walker.socket.readyState})`);

walker.socket.close();
ghost.socket.close();
await wait(300);
killWorlds();
try { if (relay) relay.close(); } catch { /* 이미 */ }

const total = eastAfter + westAfterSleep;
console.log(`[도착장부] 세상의 그 물건: 동 ${eastAfter} + 서 ${westAfterSleep} = ${total} (있던 것 ${STOCK} · 도착 직후 서쪽 ${inWest})`);
if (total > STOCK) { console.log('[도착장부] ❌ 두 벌이다 — 안 떠나고도 통행증이 먹힌다'); process.exit(1); }
console.log('[도착장부] ✅ 한 벌');
process.exit(0);
