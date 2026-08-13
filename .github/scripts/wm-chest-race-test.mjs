#!/usr/bin/env node
// wm-chest-race-test.mjs — <b>둘이 같은 상자를 동시에 비워도 물건이 늘지 않는다</b> (TASK-WM-332).
//
// ★ 왜: 상자는 <b>세계의 것</b>이다 — 내가 넣고 친구가 꺼낸다(WorldStorages 의 존재 이유).
//   그 말은 두 사람이 <b>같은 순간</b> 같은 칸을 건드릴 수 있다는 뜻이고, 거기서 하나만 어긋나면
//   물건이 <b>복제</b>된다. MMO 에서 가장 흔한 치명 결함이고, 한 번 새면 경제가 통째로 죽는다.
//   지금까지 상자를 여럿이 동시에 만지는 자리는 <b>한 번도 안 쟀다</b>(겨루기 관문은 들판만 본다).
//
// 재는 법: 상자 하나에 20개를 <b>세계 파일로 심어</b> 두고, 창 둘이 같은 순간 「20개 꺼내」를
//   보낸다. 그 뒤 둘의 가방을 더한다 —
//   ① 합이 20을 <b>넘지 않는다</b>(복제 없음) ② 합이 20이다(사라지지도 않음)
//   ③ 상자가 비어 있다 ④ 세계가 살아 있다.
//
// ⚠ 「하나만 성공」을 요구하지 않는다 — 둘이 10개씩 나눠 가져도 <b>제품으로는 옳다</b>.
//   문턱은 개수의 <b>보존</b>이지 승자의 이름이 아니다.
//
// exit: 0 = 안 는다 안 준다 · 1 = 복제되거나 사라졌다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5382);

/** 상자에 심어 둘 개수 — 둘이 동시에 「전부」를 노린다. */
const STOCK = 20;

/** 상자 자리. 사람은 여기 근처에서 시작해야 손이 닿는다(WorldStorages.REACH). */
const CELL = { x: 0, y: 0, z: 0 };

function cannotRun(message) {
	console.error(`[상자경합] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-chest-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

// 상자 하나에 20개를 심은 세계를 만든다 — 「지어서 채우는」 절차를 안 거쳐야 시험이 짧고 곧다.
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-chest-')), 'world.json');
const ITEM_ID = 1;
writeFileSync(worldFile, JSON.stringify({
	// 4005 = 보관 상자(칸 30). 상자 칸은 <b>지은 것</b>에서 나오므로(SlotsOf), 건물이 먼저 있어야
	// 심은 물건이 들어간다 — 이 한 줄이 없으면 세계는 조용히 빈 상자를 만든다.
	buildings: [{ x: CELL.x, y: CELL.y, z: CELL.z, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: CELL.x, y: CELL.y, z: CELL.z, items: [{ itemId: ITEM_ID, amount: STOCK }] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
	// ⚠ `identities` 는 <b>목록이 아니라 꾸러미</b>다. 여기에 `[]` 를 적으면 파일 전체가 못 읽히고
	//   세계는 <b>빈 세계로</b> 뜬다(심은 상자가 조용히 사라진다). 실제로 이 관문을 만들다 겪었다 —
	//   그 무음 실패는 따로 고친다(TASK-WM-333). 여기서는 적지 않는 것이 옳다.
	people: [], gathered: [], cauldrons: [],
}), 'utf8');

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

function join_(name) {
	const one = { name, id: null, bag: new Map(), chest: null };
	one.socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello' }));
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') one.id = said.id;
		if (said.type === 'bag') {
			one.bag = new Map((said.items || []).map((row) => [row.itemId, row.amount]));
		}
		if (said.type === 'chest') one.chest = said;
	};
	return one;
}

const two = [join_('가'), join_('나')];

// ⚠ 재기 전에 <b>잴 것이 왔는지</b>부터 (domain-wm.md § 관문 규율 ②).
{
	const until = Date.now() + 30000;
	while (Date.now() < until && two.some((one) => one.id === null)) await wait(100);
	if (two.some((one) => one.id === null)) {
		killWorld();
		cannotRun('둘 다 세계에 못 붙었다 — 이 상태로는 경합을 못 만든다');
	}
}

// 둘 다 상자 앞에 선다 — 손이 닿아야 꺼낼 수 있다.
for (const one of two) one.socket.send(JSON.stringify({ type: 'move', x: CELL.x, z: CELL.z }));
await wait(1000);

// 상자 안을 먼저 확인한다 — 심은 것이 실제로 들어갔나(안 들어갔으면 이 시험은 아무것도 안 잰다).
for (const one of two) one.socket.send(JSON.stringify({ type: 'chestask', x: CELL.x, y: CELL.y, z: CELL.z }));
await wait(1500);

const seeded = two.some((one) => (one.chest?.items || []).some((row) => row.itemId === ITEM_ID && row.amount === STOCK));
if (seeded === false) {
	const saw = JSON.stringify(two[0].chest?.items ?? null);
	killWorld();
	cannotRun(`상자에 ${STOCK}개가 안 심겼다 (${saw}) — 이 상태로 「안 늘었다」를 적으면 거짓 초록이다`);
}

// ★ 같은 순간 둘 다 「전부 꺼내」 — 한 틱 안에 들어가야 경합이다.
let did = 1;
for (const one of two) {
	one.socket.send(JSON.stringify({
		type: 'chesttake', x: CELL.x, y: CELL.y, z: CELL.z, itemId: ITEM_ID, amount: STOCK, did: did++,
	}));
}

await wait(3000);
for (const one of two) one.socket.send(JSON.stringify({ type: 'bagask' }));
for (const one of two) one.socket.send(JSON.stringify({ type: 'chestask', x: CELL.x, y: CELL.y, z: CELL.z }));
await wait(2000);

const mine = two.map((one) => one.bag.get(ITEM_ID) ?? 0);
const total = mine.reduce((sum, one) => sum + one, 0);
const leftInChest = (two[0].chest?.items || []).find((row) => row.itemId === ITEM_ID)?.amount ?? 0;

const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } })
	.then((one) => one.json()).catch(() => null);

for (const one of two) one.socket.close();
killWorld();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 상자에 ${STOCK}개 · 둘이 동시에 전부 요구 — 가 ${mine[0]}개 · 나 ${mine[1]}개 · 상자에 ${leftInChest}개`);

check('물건이 늘지 않았다 (복제 없음)', total + leftInChest <= STOCK,
	`가진 것 ${total} + 남은 것 ${leftInChest} = ${total + leftInChest} (심은 것 ${STOCK})`);
check('물건이 사라지지도 않았다', total + leftInChest === STOCK,
	`합 ${total + leftInChest} · 심은 것 ${STOCK}`);
check('상자가 비었다 — 둘 중 하나는 다 가져갔다', leftInChest === 0 || total === STOCK,
	`상자에 ${leftInChest}개 남음`);
check('세계가 살아 있다', health !== null && health.ok === true, health === null ? '대답 없음' : '대답함');

if (failures === 0) {
	console.log('[상자경합] ✅ 둘이 동시에 비벼도 개수는 보존된다');
	process.exit(0);
}

console.log(`\n[상자경합] RESULT: ${failures}건`);
process.exit(1);
