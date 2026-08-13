#!/usr/bin/env node
// wm-world-full-test.mjs — <b>세계가 가득 차면 이유를 말하고, 안에 있는 사람은 지킨다</b> (TASK-WM-349).
//
// ★ 왜: 여태 세계에 <b>정원이 없었다</b>. 사람이 계속 들어오면 모두가 같이 느려질 뿐,
//   「가득 찼다」고 말해 주는 자리가 없었다. 정원 없는 세계는 <b>모두가 나빠지는</b> 쪽으로 무너진다.
//   MMO 는 그 반대여야 한다 — 안에 있는 사람은 지키고, 못 받는 사람에게는 <b>이유</b>를 말한다.
//   (말없이 끊으면 사람은 자기 인터넷을 의심하고, 우리는 그가 왔었다는 것조차 모른다.)
//
// 재는 것 (정원을 낮춰 띄운 세계):
//   ① 정원까지는 다 들어간다 ② 정원을 넘은 사람은 <b>「가득 찼다」를 듣고</b> 닫힌다
//   ③ 그때 <b>안에 있던 사람들의 세계는 그대로 흐른다</b> ④ 한 명이 나가면 그 자리는 다시 열린다
//   ⑤ <b>국경을 넘어오는 사람은 정원에 안 걸린다</b> (TASK-WM-350) — 그 사람은 이미 옆 세계에서
//      나온 뒤라 여기서 돌려보내면 <b>어디에도 없는 사람</b>이 된다(가방은 통행증 안에 있다).
//
// ⚠ 정원(200)은 제품 상수라 시험에서 200명을 붙이면 느린 기계에서 기계 이야기가 된다.
//   그래서 세계를 <b>정원을 낮춘 채</b> 띄운다(WM_MOST_PEOPLE) — 규칙은 같고 수만 작다.
//
// 실행: node .github/scripts/wm-world-full-test.mjs
// exit: 0 = 정원이 산다 · 1 = 안 산다 · 2 = 못 돌림
//
// [빨강-확인] 문 앞에서 <b>도장</b>을 안 보게 하니 빨강 — 「가짜 통행증으로 들어와 버렸다(인형 8)」.
//   즉 「pass 라는 낱말이 있나」로만 보면 아무나 한 줄로 정원을 넘는다.
// [빨강-확인] ⑤ 통행증 보는 자리를 꺼 보니 빨강 — 「국경을 넘어오는 사람이 『가득 찼다』를 듣고 튕겼다」.
//   이 결함은 정원을 넣은 그날 <b>정원이 만든 것</b>이다(새 실패 경로는 한 번 밟아 본다 — 규율 ⑤).
// [빨강-확인] 정원 보는 자리를 꺼 보니 3건 빨강 (2026-08-14) — 넘어온 셋이 「가득 찼다」를 못 듣고
//   그냥 들어와 버렸고(줄도 안 닫힘), 돌려보낸 수도 0 이었다.
// ⚠ 만들면서 한 번 데었다: 말을 보내자마자 `CloseOutputAsync` 로 돌아갔더니 그 말이 <b>안 나갔다</b>
//   (창이 1006 으로 끊겼다 — 미들웨어가 돌아가는 순간 줄을 끊는다). 닫기 인사를 주고받는
//   `CloseAsync` 로 바꾸니 그제야 「가득 찼다」가 도착했다.

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5470);

/** 이 시험에서만 쓰는 작은 정원 — 규칙은 제품과 같고 수만 작다. */
const MOST = 6;

function cannotRun(message) {
	console.error(`[world-full] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-full-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

// 두 세계를 띄운다 — 하나는 가득 찰 곳(서), 하나는 여행자가 출발할 곳(동).
const SECRET = '두 세계만 아는 말';
const nextDoorPort = worldPort + 2;
const worlds = [];

function startWorld(port, zone, neighbours, most) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-full-')), 'world.json');
	worlds.push(spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: {
			...process.env, WM_WORLD_FILE: worldFile, WM_MOST_PEOPLE: String(most),
			WM_ZONE: zone, WM_ZONE_NEIGHBOURS: neighbours, WM_ZONE_SECRET: SECRET,
		},
		stdio: 'ignore',
	}));
}

startWorld(worldPort, '서:-40,-40,0,40', `동:0,-40,40,40=ws://127.0.0.1:${nextDoorPort}/ws`, MOST);
startWorld(nextDoorPort, '동:0,-40,40,40', `서:-40,-40,0,40=ws://127.0.0.1:${worldPort}/ws`, 50);

function killWorld() {
	for (const one of worlds) {
		try {
			if (process.platform === 'win32') execSync(`taskkill /PID ${one.pid} /F /T`, { stdio: 'ignore' });
			else one.kill('SIGKILL');
		} catch { /* 이미 죽었다 */ }
	}
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

function join_(port = worldPort, pass = '') {
	const one = { plates: 0, told: null, closed: false };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify(
		pass ? { type: 'hello', secret: '', pass } : { type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래에서 수로 본다 */ };
	one.socket.onclose = () => { one.closed = true; };
	one.socket.onmessage = (event) => {
		try {
			const said = JSON.parse(String(event.data));
			if (said.type === 'full') one.told = said;
			if (said.type === 'welcome') one.id = said.id;
			if (said.type === 'world') one.plates += 1;
			if (said.type === 'moveon') one.moveOn = said;
		} catch { /* 딴 소식 */ }
	};

	return one;
}

// ── 정원만큼 들어간다 ────────────────────────────────────────────────────────
const inside = [];
for (let i = 0; i < MOST; i += 1) { inside.push(join_()); await wait(200); }
await wait(2000);

const afterFilling = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

// ── 넘어온 셋 ────────────────────────────────────────────────────────────────
for (const one of inside) one.plates = 0;
const extra = [join_(), join_(), join_()];
await wait(3000);

const insidePlates = inside.map((one) => one.plates);
const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

// ── 한 명이 나가면 그 자리는 다시 열린다 ─────────────────────────────────────
try { inside[0].socket.close(); } catch { /* 이미 */ }
await wait(2500);
const late = join_();
await wait(2500);

// ── ⑤ 가득 찬 세계로 <b>국경을 넘어오는</b> 사람 ───────────────────────────
// 옆 세계(동)에서 걸어와 서쪽 국경을 넘는다. 서쪽은 지금 가득 차 있다.
const traveller = join_(nextDoorPort);
await wait(1500);
for (let step = 0; step < 200 && traveller.moveOn === undefined; step += 1) {
	if (traveller.socket.readyState === 1) traveller.socket.send(JSON.stringify({ type: 'move', x: -0.15, z: 0 }));
	await wait(50);
}

// 가짜 통행증 — 도장이 없는 아무 글자. 이걸로 들어와지면 정원은 없는 것과 같다.
const faker = join_(worldPort, 'pass-처럼-생긴-아무-글자');
await wait(2000);

let arrived = null;
if (traveller.moveOn) {
	arrived = join_(worldPort, traveller.moveOn.pass);
	await wait(2500);
}

for (const one of [...inside, ...extra, late, traveller, faker, ...(arrived ? [arrived] : [])]) {
	try { one.socket.close(); } catch { /* 이미 */ }
}
killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 정원 ${MOST}명 · 채운 뒤 세계가 세는 사람 ${afterFilling.people}명`
	+ ` · 돌려보낸 사람 ${health.turnedAwayPeople}명 · 세계가 말하는 정원 ${health.mostPeopleAtOnce}`);

check(`정원까지는 다 들어간다 (${MOST}명)`, afterFilling.people >= MOST, `${afterFilling.people}명`);

check('넘어온 사람은 「가득 찼다」를 듣는다',
	extra.every((one) => one.told !== null && one.told.most === MOST),
	extra.map((one) => (one.told ? `들었다(${one.told.most})` : '못 들었다')).join(' · '));

check('넘어온 사람의 줄은 닫힌다', extra.every((one) => one.closed),
	extra.map((one) => (one.closed ? '닫힘' : '열린 채')).join(' · '));

// ★ 이게 정원의 <b>목적</b>이다 — 밖을 막아 안을 지킨다.
// [문턱-사유] (c) 제품 상수 — 세계는 초당 20판을 말한다. 3초면 성한 창은 수십 장을 받는다.
check('가득 찬 사이에도 안에 있던 사람의 세계는 흐른다',
	insidePlates.every((count) => count >= 8),
	`3초에 받은 판 ${insidePlates.join('/')}`);

check('한 명이 나가면 그 자리는 다시 열린다', late.told === null && late.id !== undefined,
	late.told === null ? `들어갔다 (인형 ${late.id})` : '아직 가득 찼다고 한다');

check('세계가 돌려보낸 수를 적어 둔다', health.turnedAwayPeople >= 3, `${health.turnedAwayPeople}명`);

// ★ 이 줄이 없으면 정원이 <b>사람을 삼킨다</b> — 옆 세계에서 이미 나온 사람을 여기서 돌려보내면
//   그 사람은 어디에도 없다(가방은 통행증 안에 있다).
if (traveller.moveOn === undefined) {
	cannotRun('여행자가 국경까지 못 갔다 — ⑤ 를 잴 수 없다');
}

// ★ 「통행증이 있다고 <b>말만</b> 하는 사람」은 못 들어와야 한다 — 아니면 빗장이 말로만 있는 것이다.
check('가짜 통행증으로는 정원을 못 넘는다',
	faker.told !== null && faker.id === undefined,
	faker.told ? '「가득 찼다」를 듣고 튕겼다' : `들어와 버렸다 (인형 ${faker.id})`);

check('국경을 넘어오는 사람은 가득 차도 들어온다',
	arrived !== null && arrived.told === null && arrived.id !== undefined,
	arrived === null ? '못 붙었다' : (arrived.told ? '「가득 찼다」를 듣고 튕겼다' : `들어왔다 (인형 ${arrived.id})`));

if (bad === 0) {
	console.log('[world-full] ✅ 가득 차면 이유를 말하고, 안에 있는 사람은 지킨다');
	process.exit(0);
}

console.log(`\n[world-full] RESULT: ${bad}건`);
process.exit(1);
