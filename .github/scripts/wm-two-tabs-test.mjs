#!/usr/bin/env node
// wm-two-tabs-test.mjs — <b>같은 신원으로 두 번 들어와도 몸은 하나다</b> (TASK-WM-334).
//
// ★ 왜: 사람은 탭을 두 개 연다. 새로고침이 끊기지 않은 채 겹치기도 한다. 그때 세계가 몸을
//   <b>둘</b> 만들면 광장에 같은 사람이 둘 서 있고, 더 나쁘게는 <b>가방이 둘</b>이 된다 —
//   한 가방에서 꺼내 다른 가방으로 옮기면 물건이 는다(경제가 죽는 그 길, WM-332 와 같은 갈래).
//   세계에는 이미 「나중에 온 쪽이 이긴다」(kicked)가 있는데, 그 규칙이 <b>실제로 도는지</b>
//   재는 자리는 없었다(창 쪽 문구만 관문에 있었다).
//
// 재는 것: 상자에서 20개를 챙긴 창을 <b>같은 열쇠로</b> 다시 열고 —
//   ① 앞 창이 쫓겨난다 ② 세계 안 사람이 하나다 ③ 뒤 창의 가방이 그 20개 그대로다(복제도 분실도 X)
//   ④ 쫓겨난 창이 시키는 일은 세계가 안 듣는다(유령 몸이 안 남는다)
//
// exit: 0 = 몸도 가방도 하나다 · 1 = 둘이 됐다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5402);

const STOCK = 20;
const ITEM_ID = 1;
const CELL = { x: 0, y: 0, z: 0 };

function cannotRun(message) {
	console.error(`[두창] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-tabs-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-tabs-')), 'world.json');
writeFileSync(worldFile, JSON.stringify({
	buildings: [{ x: CELL.x, y: CELL.y, z: CELL.z, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: CELL.x, y: CELL.y, z: CELL.z, items: [{ itemId: ITEM_ID, amount: STOCK }] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
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

function open_(name, secret = '') {
	const one = { name, id: null, secret: '', bag: new Map(), kicked: false, closed: false };
	one.socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret }));
	one.socket.onclose = () => { one.closed = true; };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') { one.id = said.id; one.secret = said.secret ?? ''; }
		if (said.type === 'kicked') one.kicked = true;
		if (said.type === 'bag') one.bag = new Map((said.items || []).map((row) => [row.itemId, row.amount]));
	};
	return one;
}

const first = open_('앞창');

{
	const until = Date.now() + 30000;
	while (Date.now() < until && (first.id === null || first.secret === '')) await wait(100);
	if (first.id === null || first.secret === '')
		{ killWorld(); cannotRun('앞 창이 열쇠를 못 받았다 — 같은 신원으로 다시 열 수가 없다'); }
}

// 상자에서 전부 챙긴다 — 가방에 뭔가 있어야 「가방이 복제됐나」를 잴 수 있다.
first.socket.send(JSON.stringify({ type: 'move', x: CELL.x, z: CELL.z }));
await wait(800);
first.socket.send(JSON.stringify({ type: 'chesttake', x: CELL.x, y: CELL.y, z: CELL.z, itemId: ITEM_ID, amount: STOCK, did: 1 }));
await wait(1500);
first.socket.send(JSON.stringify({ type: 'bagask' }));
await wait(1200);

const carried = first.bag.get(ITEM_ID) ?? 0;
if (carried !== STOCK) {
	killWorld();
	cannotRun(`앞 창이 ${STOCK}개를 못 챙겼다 (${carried}개) — 이 상태로는 가방 복제를 못 잰다`);
}

const beforePeople = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } })
	.then((one) => one.json()).then((one) => one.people);

// ★ 같은 열쇠로 두 번째 창 — 사람이 탭을 하나 더 여는 그 순간이다.
const second = open_('뒤창', first.secret);
{
	const until = Date.now() + 20000;
	while (Date.now() < until && second.id === null) await wait(100);
	if (second.id === null) { killWorld(); cannotRun('뒤 창이 못 들어갔다'); }
}

await wait(2000);
second.socket.send(JSON.stringify({ type: 'bagask' }));
await wait(1500);

// 쫓겨난 창이 시키는 일 — 세계가 들으면 안 된다(유령 몸으로 상자를 또 비우면 물건이 는다).
first.socket.send(JSON.stringify({ type: 'move', x: 30, z: 30 }));
await wait(1500);

const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } })
	.then((one) => one.json());

const secondBag = second.bag.get(ITEM_ID) ?? 0;

first.socket.close();
second.socket.close();
killWorld();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 앞창 ${first.id}(가방 ${carried}) → 같은 열쇠로 뒤창 ${second.id}(가방 ${secondBag})`
	+ ` · 세계 사람 ${beforePeople} → ${health.people}`);

// ⚠ <b>몸 번호는 같지 않아도 된다</b> — 이 세계는 붙을 때마다 새 인형 번호를 준다(설계).
//   이어지는 것은 <b>신원</b>이고, 그 증거는 번호가 아니라 <b>가방과 사람 수</b>다.
//   (처음엔 번호가 같아야 한다고 적었다가 이 자리에서 배웠다 — 관문이 제품을 가르치면 안 된다.)
check('돌아온 창이 그 사람으로 이어진다', second.id !== null && secondBag === STOCK,
	`앞창 ${first.id} → 뒤창 ${second.id} · 가방 ${secondBag}개 그대로`);
check('앞 창은 물러난다', first.kicked || first.closed, first.kicked ? '「다른 곳에서 접속했다」를 받았다' : (first.closed ? '줄이 끊겼다' : '아직 붙어 있다'));
check('몸이 둘로 안 늘었다', health.people <= beforePeople, `${beforePeople} → ${health.people}`);
check('가방도 하나다 — 복제도 분실도 없다', secondBag === STOCK, `뒤창 가방 ${secondBag} (앞창이 챙긴 것 ${STOCK})`);

if (failures === 0) {
	console.log('[두창] ✅ 탭을 둘 열어도 몸도 가방도 하나다');
	process.exit(0);
}

console.log(`\n[두창] RESULT: ${failures}건`);
process.exit(1);
