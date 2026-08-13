#!/usr/bin/env node
// wm-pass-replay-test.mjs — <b>같은 통행증을 두 번 내밀어도 짐은 한 벌</b> (TASK-WM-335).
//
// ★ 왜: 국경을 넘을 때 사람은 <b>통행증</b>에 짐을 실어 건넌다. 그 종이를 복사해 두 번 내밀면
//   가방이 두 벌 온다 — 물건이 무한히 는다(경제가 죽는 그 길, WM-332·334 와 같은 갈래).
//   세계에는 이미 막는 장치가 있다(`PassOnce`: 짐은 처음 한 번만 · 다시 들어오는 것 자체는 허락).
//   그런데 그 규칙은 <b>단위 시험</b>에서만 돌았다 — 진짜 세계 둘을 놓고 종이를 두 번 내미는
//   자리는 없었다. 막는 장치는 <b>실제로 막아 보여야</b> 장치다.
//
// ⚠ 「두 번째는 못 들어온다」를 요구하지 <b>않는다</b>: 통행증을 내밀다 줄이 끊긴 사람은
//   다시 들어와야 한다(WM-309). 요구하는 것은 하나 — <b>짐이 두 벌이 되지 않는다.</b>
//
// exit: 0 = 짐은 한 벌 · 1 = 두 벌이 됐다(복제) · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5412);
const westPort = eastPort + 1;
const SECRET = 'pass-replay-gate-secret';

/** 가방에 실어 건널 개수 — 두 벌이 되면 40이 된다. */
const STOCK = 20;
const ITEM_ID = 1;

function cannotRun(message) {
	console.error(`[통행증재사용] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-pass-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours, seed) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-pass-')), 'world.json');
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

// 동쪽에는 <b>짐을 채울 상자</b>를 심는다 — 빈 손으로 건너면 복제를 잴 수가 없다.
startWorld(eastPort, 'east:0,-40,40,40', `west:-40,-40,0,40=ws://127.0.0.1:${westPort}/ws`, {
	buildings: [{ x: 2, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: 2, y: 0, z: 0, items: [{ itemId: ITEM_ID, amount: STOCK }] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
});
// 서쪽에도 <b>빈 상자</b>를 심는다 — 도착한 사람이 짐을 내려놓을 자리다(아래 ★ 참고).
startWorld(westPort, 'west:-40,-40,0,40', `east:0,-40,40,40=ws://127.0.0.1:${eastPort}/ws`, {
	buildings: [{ x: -2, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
	// ⚠ 상자 <b>칸</b>은 저장된 storages 에서 살아난다 — 건물만 적고 여기를 비우면 넣을 데가 없다
	//   (그때 넣기는 조용히 실패하고 가방에 그대로 남는다). 빈 items 로라도 칸을 만들어 둔다.
	storages: [{ x: -2, y: 0, z: 0, items: [] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
});

for (const port of [eastPort, westPort]) {
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}
	if (up === false) {
		killWorlds();
		cannotRun(`세계가 안 떴다 (${port})`);
	}
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

const send = (one, message) => {
	if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message));
};

// ── 동쪽에서 짐을 챙긴다 ──────────────────────────────────────────────
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
if (packed !== STOCK) {
	killWorlds();
	cannotRun(`짐을 못 챙겼다 (${packed}/${STOCK}) — 빈 손으로는 복제를 못 잰다`);
}

// ── 서쪽으로 걸어간다 → 통행증을 받는다 ──────────────────────────────
for (let step = 0; step < 900 && walker.moveOn === null; step += 1) {
	send(walker, { type: 'move', x: -0.15, z: 0, seq: 100 + step });
	await wait(50);
}

if (walker.moveOn === null || !walker.moveOn.pass) {
	walker.socket.close();
	killWorlds();
	cannotRun('국경에서 통행증을 못 받았다 — 이 상태로는 재사용을 못 잰다');
}

const pass = walker.moveOn.pass;

// ★ 진짜 창은 <b>기기 열쇠와 통행증을 같이</b> 낸다 — 열쇠는 브라우저가 계속 들고 있기 때문이다.
//   (열쇠 없이 통행증만 내면 새 세계는 그 사람을 열쇠에 <b>못 묶고</b>, 다음에 통행증 없이 돌아온
//   그 사람은 손님이 된다. 처음에 그 꼴로 재다가 「주인 가방 0」이 나왔다 — 관문 쪽 흉내가 틀렸던 것.)
const walkerSecret = walker.secret;
walker.socket.close();
await wait(500);

// ── 통행증으로 서쪽에 들어간다 (첫 번째) ─────────────────────────────
const arrived = joinWorld(westPort, { secret: walkerSecret, pass });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && arrived.id === null) await wait(100);
	if (arrived.id === null) { killWorlds(); cannotRun('서쪽에 못 들어갔다'); }
}
await wait(1500);
send(arrived, { type: 'bagask' });
await wait(1500);
const firstBag = arrived.bag.get(ITEM_ID) ?? 0;

// ★ 여기가 이 관문의 핵심이다 (2026-08-14 에 배웠다). 도착한 사람은 짐을 <b>상자에 내려놓는다.</b>
//   그냥 종이만 다시 내밀어서는 복제가 안 보인다 — 통행증은 가방을 <b>덮어쓰기</b> 때문이다
//   (WelcomeTraveller: EmptyBag 뒤 채운다 = 「통행증이 진실」). 즉 같은 20이 다시 와도 20이다.
//   진짜 새는 길은 <b>내려놓고 다시 받는 것</b>이다: 상자에 20 + 가방에 20 = 40.
//   ⚠ 이 자리를 안 밟았다면 이 관문은 <b>막는 장치를 꺼도 초록</b>이었다(실제로 그랬다).
send(arrived, { type: 'move', x: -2, z: 0, seq: 500 });
await wait(1200);
send(arrived, { type: 'chestput', x: -2, y: 0, z: 0, itemId: ITEM_ID, amount: STOCK, did: 2 });
await wait(1500);
send(arrived, { type: 'bagask' });
await wait(1200);
const afterDrop = arrived.bag.get(ITEM_ID) ?? 0;
// 도착한 창이 새 열쇠를 못 받았으면(통행증으로 온 사람은 그렇다) <b>들고 온 열쇠</b>가 그대로 그 사람이다.
const westSecret = arrived.secret || walkerSecret;
arrived.socket.close();
await wait(500);

// ── ★ 같은 종이를 다시 내민다 (복사한 통행증) ────────────────────────
//   기기 열쇠는 안 준다 — 「남이 종이만 복사해 온」 가장 나쁜 꼴이다.
//   (그래서 이 창은 원래 주인이 아니다 — 여기서 짐이 또 오면 그게 복제다.)
const again = joinWorld(westPort, { pass });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && again.id === null) await wait(100);
	if (again.id === null) { killWorlds(); cannotRun('두 번째 시도가 아예 못 들어갔다'); }
}
await wait(1500);
send(again, { type: 'bagask' });
await wait(1500);
const secondBag = again.bag.get(ITEM_ID) ?? 0;

// 상자에 내려놓은 것도 센다 — 세상의 그 물건은 <b>상자 + 가방</b>이다.
send(again, { type: 'move', x: -2, z: 0, seq: 600 });
await wait(1000);
send(again, { type: 'chestask', x: -2, y: 0, z: 0 });
await wait(1500);

// 원래 주인이 다시 들어오면 제 짐이 그대로 있어야 한다(줄이 끊겼다 돌아온 사람).
const owner = joinWorld(westPort, { secret: westSecret });
{
	const until = Date.now() + 30000;
	while (Date.now() < until && owner.id === null) await wait(100);
}
await wait(1200);
send(owner, { type: 'bagask' });
send(owner, { type: 'move', x: -2, z: 0, seq: 700 });
await wait(1200);
send(owner, { type: 'chestask', x: -2, y: 0, z: 0 });
await wait(1500);
const ownerBag = owner.bag.get(ITEM_ID) ?? 0;
const ownerSeesInChest = (owner.chest?.items || []).find((row) => row.itemId === ITEM_ID)?.amount ?? 0;

again.socket.close();
owner.socket.close();
killWorlds();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

const inChest = (again.chest?.items || []).find((row) => row.itemId === ITEM_ID)?.amount ?? 0;
const worldHas = inChest + secondBag;

console.log(`  ⓘ 동쪽에서 ${packed}개를 싣고 건넜다 — 도착 ${firstBag} → 상자에 내려놓고 ${afterDrop}`
	+ ` · 같은 종이로 다시 들어와 가방 ${secondBag} · 상자 ${inChest} → 서쪽에 있는 그 물건 ${worldHas}`);

check('짐은 처음 한 번만 온다', firstBag === STOCK, `첫 도착 ${firstBag}개 (실은 것 ${STOCK})`);
check('내려놓으면 가방이 빈다', afterDrop === 0, `내려놓은 뒤 가방 ${afterDrop}개`);
check('★ 내려놓고 종이를 다시 내밀어도 물건이 안 는다', worldHas <= STOCK,
	`서쪽에 있는 그 물건 ${worldHas}개 (상자 ${inChest} + 가방 ${secondBag}) · 건너온 것 ${STOCK}개`);
// ⚠ 주인 가방이 <b>0인 것이 옳다</b> — 방금 상자에 내려놨으니까. 지킬 것은 「가방에 20」이 아니라
//   「내려놓은 20이 그대로 있다」다. (처음엔 가방으로 재다가 빨갰다 — 관문이 이야기를 못 따라간 것.)
check('주인이 돌아와도 내려놓은 것이 그대로다', ownerSeesInChest === STOCK,
	`주인이 본 상자 ${ownerSeesInChest}개 · 주인 가방 ${ownerBag}개`);

if (failures === 0) {
	console.log('[통행증재사용] ✅ 종이를 두 번 내밀어도 짐은 한 벌이다');
	process.exit(0);
}

console.log(`\n[통행증재사용] RESULT: ${failures}건`);
process.exit(1);
