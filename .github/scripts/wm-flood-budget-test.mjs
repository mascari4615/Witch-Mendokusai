#!/usr/bin/env node
// wm-flood-budget-test.mjs — <b>한 사람이 말을 퍼부어도 남들의 세계는 멀쩡한가</b> (TASK-WM-347).
//
// ★ 왜: 세계는 「보내는 쪽」이 밀릴 때를 여러 번 고쳤다(WM-217·228·340·345). 그런데 <b>받는 쪽</b>은
//   한 번도 안 쟀다 — 창 하나가 초당 수천 마디를 쏟으면 그 마디를 푸는 일이 세계의 한 실을 먹는다.
//   MMO 에서 이건 「한 사람이 모두를 멈추는」 자리라 코어다.
//
// 재는 것: 성한 봇 몇 + <b>퍼붓는 봇 하나</b>. 퍼붓기 전/중의 ① 세계의 판 간격 ② 성한 봇이 받는 판 수.
//
// 재는 것 ①남들이 받는 판 ②세계의 판 간격 ③<b>세계가 실제로 읽은 마디</b>(말 예산이 깎나)
//   ④그래도 퍼부은 사람이 걷기는 하나(예산이 사람을 굶겨 죽이면 그것도 결함이다)
//
// ⚠ 「보낸 것 ≠ 세계가 들은 것」 (2026-08-14): 제동 없이 쏟으면 초당 4만을 <b>보냈다</b>고 세지만
//   세계가 읽은 것은 241개다 — 나머지는 이쪽에 쌓여 있었다. 그래서 줄이 비었을 때만 밀어 넣고,
//   끝에 <b>세계가 읽은 수</b>로 확인한다(관문 규율 ⑧ — 자가 진짜 그 길을 갔는지).
//
// [빨강-확인] 말 예산(MessageBudget)을 꺼 보니 빨강 — 세계가 읽은 마디 241 → 20,000+ 로 뛰었다 (2026-08-14).
//
// 실행: node .github/scripts/wm-flood-budget-test.mjs
// exit: 0 = 남들이 멀쩡·예산이 깎는다 · 1 = 아니다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5410);

const HONEST = 8;
const WATCH_SECONDS = 6;

function cannotRun(message) {
	console.error(`[flood] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-flood-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-flood-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll), env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore',
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
	if (up === false) { killWorld(); cannotRun('세계가 안 떴다'); }
}

function join_(counting) {
	const one = { socket: new WebSocket(`ws://127.0.0.1:${worldPort}/ws`), plates: 0, lastAt: undefined };
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 수로 본다 */ };
	one.socket.onmessage = (event) => {
		try {
			const said = JSON.parse(String(event.data));
			if (said.type === 'welcome') one.id = said.id;
			if (said.type === 'me' && said.doll) one.here = { x: said.doll.x, z: said.doll.z };
			if (said.type !== 'world') return;
			if (counting) { one.plates += 1; one.lastAt = said.at; }
			if (Array.isArray(said.dolls) && one.id !== undefined) {
				const mine = said.dolls.find((doll) => doll.id === one.id);
				if (mine && typeof mine.x === 'number') one.here = { x: mine.x, z: mine.z };
			}
		} catch { /* 딴 소식 */ }
	};
	return one;
}

const honest = [];
for (let i = 0; i < HONEST; i += 1) honest.push(join_(true));
await wait(3000);

const health = (name) => fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

/** 성한 봇들이 이 몇 초 동안 받은 판 수 (합). */
async function watch(seconds) {
	for (const one of honest) one.plates = 0;
	const before = await health();
	await wait(seconds * 1000);
	const after = await health();
	return {
		plates: honest.reduce((sum, one) => sum + one.plates, 0),
		gap: after.longestTickGapMs,
		gapGrew: after.longestTickGapMs - before.longestTickGapMs,
		refused: after.refusedSteps - before.refusedSteps,
	};
}

const quiet = await watch(WATCH_SECONDS);
console.log(`[flood] 조용할 때 — 성한 ${HONEST}명이 ${WATCH_SECONDS}초에 ${quiet.plates}판 · 가장 벌어진 판 ${quiet.gap}ms`);

// 퍼붓는 봇 하나 — 걸음을 쉬지 않고 쏟는다(가장 흔한 마디).
const loud = join_(false);
await wait(1000);
const loudStart = loud.here ? { ...loud.here } : null;
// ⚠ <b>보낸 것 ≠ 세계가 들은 것</b> (2026-08-14 실측): 아무 제동 없이 쏟았더니 초당 4만 마디를
//   「보냈」지만 세계가 읽은 것은 <b>241개</b>였다 — 마디가 이쪽(보내는 프로그램)에 쌓여 있었다.
//   그건 세계 이야기가 아니라 <b>내 자의 이야기</b>다(관문 규율 ⑧). 그래서 줄이 비었을 때만 밀어 넣고,
//   끝에 「세계가 읽은 수」로 이 자가 진짜 퍼부었는지 확인한다.
let sent = 0;
const pouring = setInterval(() => {
	if (loud.socket.readyState !== 1) return;
	for (let i = 0; i < 40; i += 1) {
		if (loud.socket.bufferedAmount > 64 * 1024) break;
		loud.socket.send(JSON.stringify({ type: process.env.WM_FLOOD_KIND || 'move', x: 0.15, z: 0 }));
		sent += 1;
	}
}, 2);

const under = await watch(WATCH_SECONDS);
clearInterval(pouring);

console.log(`[flood] 퍼부을 때 — 성한 ${HONEST}명이 ${WATCH_SECONDS}초에 ${under.plates}판 · 가장 벌어진 판 ${under.gap}ms`);
console.log(`[flood] 퍼부은 마디 ${sent}개 (초당 ${Math.round(sent / WATCH_SECONDS)})`);

// ★ <b>빨리 보낸다고 빨리 가지는가</b> — 세계의 시계가 심판해야 한다(moveAllowance).
const wentFar = loudStart && loud.here
	? Math.hypot(loud.here.x - loudStart.x, loud.here.z - loudStart.z)
	: -1;
const clockAllows = 0.15 * 20 * (WATCH_SECONDS + 1);   // 걸음 0.15m · 초당 20판 · 본 시간
console.log(`[flood] 퍼부은 사람이 간 거리 ${wentFar.toFixed(1)}m (시계가 허락하는 최대 ${clockAllows.toFixed(1)}m)`);

const lines = await fetch(`http://127.0.0.1:${worldPort}/lines`, { headers: { connection: 'close' } })
	.then((one) => one.json()).catch(() => ({ lines: [] }));
const loudLine = lines.lines.find((one) => one.dollId === loud.id);
if (loudLine) console.log(`[flood] 세계가 <b>읽은</b> 마디 ${loudLine.heard}개 (퍼부은 ${sent}개)`);

for (const one of honest) { try { one.socket.close(); } catch { /* 이미 */ } }
try { loud.socket.close(); } catch { /* 이미 */ }
killWorld();

const share = quiet.plates === 0 ? 0 : under.plates / quiet.plates;
console.log(`[flood] 남들이 받은 판 ${Math.round(share * 100)}% (조용할 때 대비) · 판 간격 ${quiet.gap}ms → ${under.gap}ms`);
// ★ <b>거리보다 이 숫자가 선명하다</b>: 세계가 되돌린 걸음 수. 시계가 심판하면 퍼부은 것의
//   거의 전부가 되돌아온다(초당 20판을 넘는 몫은 다 거절). 심판이 없으면 <b>0</b> 이다.
//   [문턱-사유] (b) 세계의 답과의 비율 — 보낸 마디의 절반 위. 기계가 빠르면 보낸 것도 같이 는다.
let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

if (loudLine === undefined) cannotRun('퍼부은 사람의 줄 장부를 못 읽었다');

// [문턱-사유] (a) 같은 판의 <b>조용할 때</b>와의 견줌 — 기계가 느리면 두 값이 같이 준다.
check('퍼붓는 사람이 있어도 남들은 그대로 받는다', share >= 0.8,
	`${Math.round(share * 100)}% (조용할 때 ${quiet.plates}판 → ${under.plates}판)`);

// [문턱-사유] (c) 사람이 느끼는 선 — 0.5초 넘게 세계가 멎으면 모두가 안다.
check('세계가 그 사이 안 멎는다', under.gapGrew < 500,
	`가장 벌어진 판 ${quiet.gap}ms → ${under.gap}ms`);

// [문턱-사유] (b) 세계의 답과의 비율 — 말 예산이 깎으면 읽은 것은 보낸 것의 <b>한 줌</b>이다.
check('말 예산이 실제로 깎는다', loudLine.heard <= sent * 0.1,
	`세계가 읽은 마디 ${loudLine.heard}개 / 퍼부은 ${sent}개`);

// ★ 예산이 사람을 <b>굶겨 죽이면</b> 그것도 결함이다 — 퍼붓는 창도 걷기는 해야 한다.
// [문턱-사유] (a) 시계가 허락하는 거리와의 견줌 — 그 5% 는 「전혀 못 걸었다」만 잡는다.
check('그래도 퍼붓는 사람이 걷기는 한다', wentFar >= clockAllows * 0.05,
	`간 거리 ${wentFar.toFixed(1)}m (시계가 허락하는 최대 ${clockAllows.toFixed(1)}m)`);

// ★ 빨리 보낸 만큼 빨리 가지 못한다 — 시계가 심판한다(moveAllowance).
check('빨리 보낸다고 빨리 가지 않는다', wentFar <= clockAllows * 1.2,
	`간 거리 ${wentFar.toFixed(1)}m ≤ ${(clockAllows * 1.2).toFixed(1)}m`);

if (bad === 0) {
	console.log('[flood] ✅ 한 사람이 퍼부어도 세계는 남들 것이다');
	process.exit(0);
}

console.log(`
[flood] RESULT: ${bad}건`);
process.exit(1);
