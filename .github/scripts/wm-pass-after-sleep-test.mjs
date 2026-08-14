#!/usr/bin/env node
// wm-pass-after-sleep-test.mjs — 세계를 <b>껐다 켜도</b> 쓴 통행증은 여전히 쓴 것이다 (TASK-WM-382).
//
// 세계는 통행증을 <b>먼저 쥐여 주고</b>, 창이 실제로 떠나면(줄이 닫히면) 그때 이 세계에서 내보낸다.
// 안 닫으면 유예(5초) 뒤 「안 넘어갔다」로 보고 <b>그대로 여기 산다</b>.
// 그런데 그 종이는 아직 30초 동안 살아 있다 — 다른 창으로 저쪽에 내밀면?
//
// ★ 왜: 도착 소식(landed, WM-377)은 <b>그 순간</b> 이웃 줄이 있어야 나간다. 없으면 조용히 흘러가고
//   그 사람은 두 세계에 남는다(가방 두 벌). 세계는 서로 껐다 켜지므로 이 판은 <b>드문 일이 아니다</b>.
//
// ★ 왜: 「이 통행증은 이미 썼다」(PassOnce, WM-335)는 <b>기억에만</b> 있다.
//   세계가 껐다 켜지면 그 장부가 사라진다 — 통행증은 30초를 사니, 그 사이 재시작 한 번이면
//   같은 종이로 짐을 <b>또</b> 받는다. 받은 짐을 상자에 내려놓고 다시 받으면 그대로 복제다
//   (WM-335 관문이 잡은 바로 그 길 — 여기서는 <b>재시작</b>이 자물쇠를 연다).
//
// [빨강-확인] 적어 두기 전 이 자가 빨갰다 — 「상자 20 + 가방 20 = 40」 (2026-08-14).
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
const eastPort = Number(process.env.WM_SMOKE_PORT || 5444);
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
const SECRET = 'pass-after-sleep-gate-secret';
const STOCK = 20;
const ITEM_ID = 1;

const cannotRun = (m) => { console.error(`[자고난통행증] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-sleeppass-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
// ★ 세계 파일을 <b>밖에서</b> 정한다 — 서쪽은 껐다 켜야 하므로 같은 자리를 다시 써야 한다.
function startWorld(port, zone, neighbours, seed, worldFile) {
	const file = worldFile ?? join(mkdtempSync(join(tmpdir(), 'wm-sleeppass-')), 'world.json');
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
const westFile = join(mkdtempSync(join(tmpdir(), 'wm-sleeppass-west-')), 'world.json');
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

// 제대로 떠난다 — 이 관문이 보는 것은 「안 떠나기」가 아니라 <b>재시작</b>이다.
walker.socket.close();
await wait(1000);

// ── 통행증으로 서쪽에 들어간다 ────────────────────────────────────────
const arrived = joinWorld(westPort, { secret: walkerSecret, pass });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && arrived.id === null) await wait(100);
	if (arrived.id === null) { killWorlds(); cannotRun('서쪽에 못 들어갔다'); }
}
await wait(1200);
send(arrived, { type: 'bagask' });
await wait(1200);
const firstBag = arrived.bag.get(ITEM_ID) ?? 0;
if (firstBag !== STOCK) { killWorlds(); cannotRun(`도착 짐이 ${firstBag}개다 — 잴 것이 없다`); }

// 짐을 <b>상자에 내려놓는다</b> — 통행증은 가방을 덮어쓰므로, 내려놓아야 복제가 보인다 (WM-335 에서 배운 것).
send(arrived, { type: 'move', x: -2, z: 0, seq: 500 });
await wait(1200);
send(arrived, { type: 'chestput', x: -2, y: 0, z: 0, itemId: ITEM_ID, amount: STOCK, did: 2 });
await wait(1500);
send(arrived, { type: 'bagask' });
await wait(1200);
const afterDrop = arrived.bag.get(ITEM_ID) ?? 0;

// ★ 여기가 이 관문의 핵심 — <b>받은 세계를 껐다 켠다</b>. 기억에만 든 「썼다」는 여기서 날아간다.
console.log('[자고난통행증] 받은 세계를 껐다 켠다');
arrived.socket.close();
await wait(1200);
killOne(west);
await wait(1500);
west = startWorld(westPort, westZone, westNeighbours, null, westFile);
if (await waitUntilUp(westPort) === false) { killWorlds(); cannotRun('서쪽이 다시 안 떴다'); }

// ── 같은 종이를 다시 내민다 (통행증은 30초를 산다 — 재시작이 그 안에 끝난다) ──
const again = joinWorld(westPort, { pass });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && again.id === null) await wait(100);
	if (again.id === null) { killWorlds(); cannotRun('다시 내밀기가 아예 못 들어갔다'); }
}
await wait(1500);
send(again, { type: 'bagask' });
await wait(1500);
send(again, { type: 'move', x: -2, z: 0, seq: 600 });
await wait(1200);
send(again, { type: 'chestask', x: -2, y: 0, z: 0 });
await wait(1500);

const secondBag = again.bag.get(ITEM_ID) ?? 0;
const inChest = (again.chest?.items || []).find((row) => row.itemId === ITEM_ID)?.amount ?? 0;

again.socket.close();
await wait(300);
killWorlds();
try { if (relay) relay.close(); } catch { /* 이미 */ }

const worldHas = inChest + secondBag;
console.log(`[자고난통행증] 도착 ${firstBag} → 상자에 내려놓고 가방 ${afterDrop}`
	+ ` · 껐다 켠 뒤 같은 종이로 가방 ${secondBag} · 상자 ${inChest} → 서쪽에 있는 그 물건 ${worldHas}`);

if (worldHas > STOCK) {
	console.log(`[자고난통행증] ❌ 두 벌이다 — 재시작이 「이미 썼다」를 지웠다 (${worldHas} > ${STOCK})`);
	process.exit(1);
}

console.log('[자고난통행증] ✅ 껐다 켜도 쓴 통행증은 쓴 것이다');
process.exit(0);
