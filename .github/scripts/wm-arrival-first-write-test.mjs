#!/usr/bin/env node
// wm-arrival-first-write-test.mjs — 도착을 <b>적기 전에</b> 말하면 그 사람이 사라지나 (TASK-WM-386).
//
// 세계는 통행증을 <b>먼저 쥐여 주고</b>, 창이 실제로 떠나면(줄이 닫히면) 그때 이 세계에서 내보낸다.
// 안 닫으면 유예(5초) 뒤 「안 넘어갔다」로 보고 <b>그대로 여기 산다</b>.
// 그런데 그 종이는 아직 30초 동안 살아 있다 — 다른 창으로 저쪽에 내밀면?
//
// ★ 왜: 도착 소식(landed, WM-377)은 <b>그 순간</b> 이웃 줄이 있어야 나간다. 없으면 조용히 흘러가고
//   그 사람은 두 세계에 남는다(가방 두 벌). 세계는 서로 껐다 켜지므로 이 판은 <b>드문 일이 아니다</b>.
//
// ★ 왜: 「받았다」(landed)는 보낸 세계에게 <b>그 사람을 놓아라</b>는 말이다 (WM-377·385).
//   말해 놓고 받은 세계가 <b>적기 전에</b> 죽으면 — 저쪽은 놓았고 여기는 안 적혔다 ⇒ 통째로 사라진다.
//   세계는 사람이 한 일을 0.3초 뒤에 적는다(WM-310). 그 0.3초가 이 구멍이다.
//
// 무대: 받은 세계의 「했다 뒤 적기」를 10분으로 늘려 놓고(WM_SAVE_AFTER_DEED_MS) 도착 직후 죽인다 —
//   「적기 전에 죽은 세계」를 <b>운에 안 맡기고</b> 만든다.
//   ⚠ <b>느긋한 적기</b>(5초마다)도 같이 늘려야 한다 — 처음엔 그걸 안 늘려서 고침을 빼도 초록이었다
//   (그 5초 짜리가 대신 적어 줬다). 무대를 만들 때는 <b>모든 적는 길</b>을 막아야 한다.
//
// 고침: 도착을 <b>먼저 기억에 박고</b> 그 다음에 말한다(먼저 쓰고 나중에 알린다).
//
// [빨강-확인] 고치기 전 이 자가 빨갰다 — 「동 0 + 서 0 (들고 떠난 것 20)」 (2026-08-14).
//
// exit: 0 = 짐이 어딘가에 남아 있다 · 1 = 사라졌다(또는 두 벌) · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { createServer, connect } from 'node:net';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5452);
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
const SECRET = 'arrival-write-gate-secret';
const STOCK = 20;
const ITEM_ID = 1;

const cannotRun = (m) => { console.error(`[먼저적기] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-write-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
// ★ 세계 파일을 <b>밖에서</b> 정한다 — 서쪽은 껐다 켜야 하므로 같은 자리를 다시 써야 한다.
function startWorld(port, zone, neighbours, seed, worldFile, extraEnv = {}) {
	const file = worldFile ?? join(mkdtempSync(join(tmpdir(), 'wm-write-')), 'world.json');
	if (seed) writeFileSync(file, JSON.stringify(seed), 'utf8');
	const child = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: file, WM_ZONE: zone, WM_ZONE_NEIGHBOURS: neighbours, WM_ZONE_SECRET: SECRET, ...extraEnv },
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
const westFile = join(mkdtempSync(join(tmpdir(), 'wm-write-west-')), 'world.json');
const westZone = 'west:-40,-40,0,40';
const westNeighbours = `east:0,-40,40,40=ws://127.0.0.1:${eastPort}/ws`;
let west = startWorld(westPort, westZone, westNeighbours, {
	buildings: [{ x: -2, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: -2, y: 0, z: 0, items: [] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
}, westFile, { WM_SAVE_AFTER_DEED_MS: '600000', WM_SAVE_INTERVAL_MS: '600000' });

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



// 제대로 떠난다.
walker.socket.close();
await wait(1000);

// ── 통행증으로 서쪽에 들어간다 ────────────────────────────────────────
const arrived = joinWorld(westPort, { secret: walkerSecret, pass });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && arrived.id === null) await wait(100);
	if (arrived.id === null) { killWorlds(); cannotRun('서쪽에 못 들어갔다'); }
}
await wait(1500);
send(arrived, { type: 'bagask' });
await wait(1500);
const inWest = arrived.bag.get(ITEM_ID) ?? 0;
console.log(`[먼저적기] 서쪽에 도착한 가방: ${inWest}`);
if (inWest !== STOCK) { killWorlds(); cannotRun(`도착 짐이 ${inWest}개다 — 잴 것이 없다`); }

// 소식이 동쪽에 닿을 틈만 준다 — 그 다음 <b>적기 전에</b> 죽인다.
await wait(2000);
console.log('[먼저적기] 받은 세계를 죽인다 (했다-뒤-적기는 10분으로 미뤄 뒀다)');
arrived.socket.close();
killOne(west);
await wait(1500);
west = startWorld(westPort, westZone, westNeighbours, null, westFile, { WM_SAVE_AFTER_DEED_MS: '600000', WM_SAVE_INTERVAL_MS: '600000' });
if (await waitUntilUp(westPort) === false) { killWorlds(); cannotRun('서쪽이 다시 안 떴다'); }

// 되살아난 서쪽에 제 열쇠로 붙는다.
const westAgain = joinWorld(westPort, { secret: walkerSecret });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && westAgain.id === null) await wait(100);
}
await wait(1500);
send(westAgain, { type: 'bagask' });
await wait(1500);
const westAfter = westAgain.bag.get(ITEM_ID) ?? 0;
westAgain.socket.close();
console.log(`[먼저적기] 죽었다 깬 서쪽 가방: ${westAfter}`);

// 동쪽에도 제 열쇠로 붙어 본다.
const eastAgain = joinWorld(eastPort, { secret: walkerSecret });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && eastAgain.id === null) await wait(100);
}
await wait(1500);
send(eastAgain, { type: 'bagask' });
await wait(1500);
const eastAfter = eastAgain.bag.get(ITEM_ID) ?? 0;
eastAgain.socket.close();
await wait(300);
killWorlds();
try { if (relay) relay.close(); } catch { /* 이미 */ }

const total = eastAfter + westAfter;
console.log(`[먼저적기] 그 물건: 동 ${eastAfter} + 서 ${westAfter} = ${total} (들고 떠난 것 ${STOCK})`);
if (total < STOCK) {
	console.log('[먼저적기] ❌ 사라졌다 — 적기 전에 말했다');
	process.exit(1);
}
if (total > STOCK) {
	console.log('[먼저적기] ❌ 두 벌이다');
	process.exit(1);
}
console.log('[먼저적기] ✅ 죽어도 짐은 남는다 — 먼저 적고 나중에 말한다');
process.exit(0);
