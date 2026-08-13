#!/usr/bin/env node
// wm-border-stampede-test.mjs — <b>여럿이 같은 순간에 국경을 넘는다</b> (TASK-WM-318).
//
// ★ 왜: 지금까지 국경은 <b>한 명씩</b>만 재봤다(WM-255 넘기 · WM-309 끊긴 넘기).
//   그런데 사람이 모이는 게임에서 국경은 <b>광장이 통째로 옮겨 가는</b> 자리다 —
//   통행증 맡아 두기(PassOnce) · 넘겨주기 유예(HANDOVER_GRACE) · 이웃 그림자가
//   <b>동시에</b> 여러 사람을 겪는다. 그때 하나라도 어긋나면 사람이 사라지거나 둘로 늘어난다.
//
// 재는 것 (여섯이 함께):
//   ① 다 넘나 ② 보낸 세계가 <b>비나</b>(두 세계에 걸친 사람 0) ③ 받는 세계 <b>장부 = 사람 수</b>
//      (장부가 더 크면 반쪽 도착이 남긴 유령 신원이다 — WM-309 에서 실제로 났다)
//
// 봇으로 잰다(창 아님) — 여기서 보려는 것은 <b>세계 쪽 경합</b>이지 그리기가 아니다.
// exit: 0 = 다 같이 넘는다 · 1 = 누가 사라지거나 늘어난다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5610);
const westPort = eastPort + 1;
const SECRET = '두 세계만 아는 말';

/** 한꺼번에 넘는 사람 수 — 둘은 「여럿」이 아니고, 너무 많으면 느린 기계에서 걷다 시간을 다 쓴다. */
const CROWD = Number(process.env.WM_STAMPEDE_CROWD || 6);

function cannotRun(message) {
	console.error(`[stampede] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-st-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-st-')), 'world.json');
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

startWorld(eastPort, 'east:0,-40,40,40', `west:-40,-40,0,40=ws://127.0.0.1:${westPort}/ws`);
startWorld(westPort, 'west:-40,-40,0,40', `east:0,-40,40,40=ws://127.0.0.1:${eastPort}/ws`);

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
	const one = { id: null, secret: '', moveOn: null };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify(pass ? { type: 'hello', secret, pass } : { type: 'hello', secret }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'welcome') { one.id = said.id; one.secret = said.secret; }
		if (said.type === 'moveon') one.moveOn = said;
		if (said.type === 'names' && Array.isArray(said.dolls) && one.id !== null) {
			const mine = said.dolls.find((doll) => doll.id === one.id);
			if (mine !== undefined && mine.name) one.name = mine.name;
		}
	};

	return one;
}

const send = (one, message) => {
	if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message));
};

const health = (port) => fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

// ── 여섯이 동쪽에 모인다 ──────────────────────────────────────────────
const crowd = [];
for (let i = 0; i < CROWD; i += 1) crowd.push(joinWorld(eastPort));

await wait(3500);

// ★ 저마다 <b>이름</b>을 단다 (TASK-WM-318): 이름은 통행증에 실려 건너간다.
//   도착만 세면 「손님으로 다시 태어난 것」도 초록이 된다 — 실패 경로를 밟아 보고 알았다
//   (통행증을 아무도 못 쓰게 막았는데 도착 6/6 · 장부 6 으로 통과했다).
for (let i = 0; i < crowd.length; i += 1) send(crowd[i], { type: 'rename', name: `nomad-${i + 1}`, did: 900 + i });

await wait(2000);

const joined = crowd.filter((one) => one.id !== null).length;
if (joined < CROWD) {
	for (const one of crowd) one.socket.close();
	killWorlds();
	cannotRun(`${joined}/${CROWD} 만 들어왔다 — 이 표본으로는 경합을 못 본다`);
}

// ── 다 같이 서쪽으로 (같은 순간에 국경에 닿게) ────────────────────────
for (let step = 0; step < 900; step += 1) {
	let everyoneTold = true;
	for (const one of crowd) {
		if (one.moveOn === null) {
			everyoneTold = false;
			send(one, { type: 'move', x: -0.15, z: 0, seq: step });
		}
	}

	if (everyoneTold) break;

	await wait(50);
}

const told = crowd.filter((one) => one.moveOn !== null).length;
if (told < CROWD) {
	for (const one of crowd) one.socket.close();
	killWorlds();
	cannotRun(`${told}/${CROWD} 만 국경에 닿았다 — 걷다가 시간이 다 갔다`);
}

// ── 통행증을 <b>동시에</b> 쓴다 ───────────────────────────────────────
const overs = crowd.map((one) => joinWorld(westPort, { secret: one.secret, pass: one.moveOn.pass }));
await wait(4000);

for (const one of crowd) one.socket.close();
await wait(3500);

const east = await health(eastPort);
const west = await health(westPort);
const arrived = overs.filter((one) => one.id !== null).length;

for (const one of overs) one.socket.close();
killWorlds();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ ${CROWD}명이 한꺼번에 — 도착 ${arrived} · 동쪽 사람 ${east.people}(장부 ${east.identities})`
	+ ` · 서쪽 사람 ${west.people}(장부 ${west.identities})`);

check('여럿이 한꺼번에 넘어도 다 도착한다', arrived === CROWD, `${arrived}/${CROWD}명`);
check('보낸 세계는 빈다 (두 세계에 걸친 사람 0)', east.people === 0, `동쪽에 남은 사람 ${east.people}명`);
const keptName = overs.filter((one, i) => one.name === `nomad-${i + 1}`).length;
check('건너간 사람이 <b>그 사람 그대로</b>다 (이름이 따라온다)', keptName === CROWD,
	keptName === CROWD
		? `${keptName}/${CROWD} 명이 제 이름으로 도착`
		: `${keptName}/${CROWD} — 나머지는 손님으로 다시 태어났다(통행증이 안 먹은 것이다)`);

check('받는 세계의 장부가 사람 수와 같다', west.identities === arrived,
	west.identities === arrived
		? `장부 ${west.identities} = 사람 ${arrived}`
		: `장부 ${west.identities} · 사람 ${arrived} — 반쪽 도착이 남긴 유령 신원이다(WM-309)`);

if (failures === 0) {
	console.log('[stampede] ✅ 광장이 통째로 옮겨 가도 아무도 안 사라지고 안 늘어난다');
	process.exit(0);
}

console.log(`\n[stampede] RESULT: ${failures}건`);
process.exit(1);
