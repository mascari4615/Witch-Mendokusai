#!/usr/bin/env node
// wm-frozen-neighbour-test.mjs — <b>이웃 세계와의 줄이 얼면 다시 잇나</b> (TASK-WM-357).
//
// ★ 왜: 사람 줄만 어는 게 아니다 — <b>세계와 세계 사이</b>의 줄도 언다(끊기지 않은 채 안 흐른다).
//   그때 「이웃에게 국경 소식 보내기」가 영원히 안 끝나고, <b>다시 잇는 길도 그 자리에서 막힌다</b>
//   (잇기는 같은 루프가 하기 때문이다). 이웃이 다시 안 돌아오면 국경 너머는 <b>영영</b> 안 보이고,
//   되살리려면 서버를 껐다 켜야 한다.
//
// 재는 것 (동·서 두 세계 · 그 사이 줄을 얼렸다 녹인다):
//   ① 얼기 전에는 국경 너머가 보인다 ② 얼면 세계가 그 줄을 <b>접는다</b>(숫자로 남는다)
//   ③ 녹이면 <b>사람 손 없이</b> 다시 이어져 국경 너머가 또 보인다
//   ④ 얼었다 녹기를 되풀이해도 <b>죽은 줄이 안 쌓인다</b> (TASK-WM-358 — 받는 쪽도 언다)
//
// 실행: node .github/scripts/wm-frozen-neighbour-test.mjs
// exit: 0 = 다시 잇는다 · 1 = 영영 막힌다 · 2 = 못 돌림
//
// [빨강-확인] ④ 줄을 놓을 때 장부에서 안 지우게 하니 「붙잡고 있는 이웃 줄 2개」로 빨강.
//   ⚠ ④ 를 <b>받는 쪽 얼림</b>으로는 못 빨갛게 했다: 이 시험은 녹이므로 저쪽이 접힐 때 RST 가 가서
//   이쪽도 곧 안다. 「받는 쪽이 조용하면 접는다」(30초)는 <b>안 녹는 경우</b>를 위한 것이고,
//   그 판은 여기서 못 만든다 — 못 밟아 본 사실을 적어 둔다(관문 규율 ⑦).
// [빨강-확인] 「못 나가면 포기한다」를 빼니(무한정 기다리게) 「접은 줄 0개」로 빨강 —
//   세계가 <b>얼어붙은 줄을 알아차리지 못한다</b>. (녹기만 하면 고인 것이 흘러 저절로 살아나므로
//   ②는 그때도 초록이었다 — 무서운 것은 <b>안 녹는 경우</b>다: 이웃 기계가 사라지면 그 줄은
//   죽은 채 남고, 다시 잇는 길도 그 자리에서 막혀 서버를 껐다 켜야 한다.)

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5700);
const westPort = eastPort + 1;
const linePort = eastPort + 2;   // 서쪽이 동쪽에게 말할 때 지나는 <b>얼릴 수 있는</b> 길

const SECRET = '두 세계만 아는 말';

function cannotRun(message) {
	console.error(`[언이웃] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-fnb-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-fnb-')), 'world.json');
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
}

// 서쪽이 동쪽에게 말하는 길만 <b>얼릴 수 있는 줄</b>로 지나가게 한다.
// ⚠ 줄에 담을 수 있는 양을 <b>작게</b> 둔다 (2026-08-14 실측): 넉넉하면 얼려도 그 안에 다 고여
//   보내기가 안 막히고, 그러면 이 시험은 <b>고치기 전 코드에서도 초록</b>이다(관문 규율 ⑧ — 자가 그 길을 안 간다).
//   진짜 회선도 버퍼가 차면 그때부터 막힌다 — 그 상태를 빨리 만드는 것뿐이다.
const badLine = openBadLine({ listenPort: linePort, targetPort: eastPort, queueBytes: 2048 });
await badLine.listen();

startWorld(eastPort, '동:0,-40,40,40', `서:-40,-40,0,40=ws://127.0.0.1:${westPort}/ws`);
startWorld(westPort, '서:-40,-40,0,40', `동:0,-40,40,40=ws://127.0.0.1:${linePort}/ws`);

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
	if (up === false) { killWorlds(); await badLine.close(); cannotRun(`세계가 안 떴다 — ${port}`); }
}

const health = (port) => fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

/** 서쪽에 사람 하나를 세우고 국경 쪽으로 걷게 한다 — 그래야 동쪽에 그림자가 비친다. */
function joinWest() {
	const one = { };
	one.socket = new WebSocket(`ws://127.0.0.1:${westPort}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래에서 수로 본다 */ };
	one.socket.onmessage = (event) => {
		try {
			const said = JSON.parse(String(event.data));
			if (said.type === 'welcome') one.id = said.id;
		} catch { /* 딴 소식 */ }
	};

	return one;
}

// 국경에 여럿을 세운다 — 소식이 커야 얼었을 때 줄이 <b>빨리</b> 찬다(작으면 며칠도 안 찬다).
const walkers = [];
for (let i = 0; i < 12; i += 1) walkers.push(joinWest());
const walker = walkers[0];
await wait(2500);

// 국경(x=0)까지 걸어간다 — 국경 띠 안에 있어야 옆 세계에 비친다.
const walking = setInterval(() => {
	for (const one of walkers) {
		if (one.socket.readyState === 1) one.socket.send(JSON.stringify({ type: 'move', x: 0.15, z: 0 }));
	}
}, 100);

// ⚠ CI(2코어)에서는 걷는 것도 이웃에게 말하는 것도 느리다 — 넉넉히 기다린다.
//   실측 2026-08-14: 이 기다림이 30초일 때 CI 에서 국경 너머가 0명이라 <b>거짓 빨강</b>이 났다.
let shadowsBefore = 0;
for (let step = 0; step < 120; step += 1) {
	await wait(500);
	shadowsBefore = (await health(eastPort)).shadows ?? 0;
	if (shadowsBefore > 0) break;
}

if (shadowsBefore === 0) {
	clearInterval(walking);
	killWorlds();
	await badLine.close();
	cannotRun('얼리기 전에도 국경 너머가 안 보였다 — 이 시험은 그 위에서만 뜻이 있다');
}

// ── 세계 사이의 줄을 얼린다 ────────────────────────────────────────────────
badLine.freeze();

// 서버 쪽 버퍼(수십 KB)까지 채워야 보내기가 진짜로 막힌다 — 느린 기계에서는 그만큼 더 걸린다.
// 그래서 「접었나」를 <b>보면서</b> 기다린다(다 찼으면 곧바로 다음으로 간다).
let foldedWhileFrozen = 0;
for (let step = 0; step < 120; step += 1) {
	await wait(1000);
	foldedWhileFrozen = (await health(westPort)).frozenNeighbourLines ?? 0;
	if (foldedWhileFrozen > 0 && step >= 20) break;
}
const whileFrozen = await health(westPort);
console.log(`  ⓘ 얼린 동안 접은 줄 ${whileFrozen.frozenNeighbourLines ?? 0}개 (기다린 뒤)`);
const eastWhileFrozen = await health(eastPort);

// ── 녹인다 — 사람 손 없이 다시 이어져야 한다 ───────────────────────────────
badLine.thaw();

let shadowsAfter = 0;
for (let step = 0; step < 120; step += 1) {
	await wait(500);
	shadowsAfter = (await health(eastPort)).shadows ?? 0;
	if (shadowsAfter > 0) break;
}

const afterLines = await health(eastPort);

clearInterval(walking);
for (const one of walkers) { try { one.socket.close(); } catch { /* 이미 */ } }
killWorlds();
await badLine.close();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 얼기 전 국경 너머 ${shadowsBefore}명 · 얼었을 때 동쪽에 ${eastWhileFrozen.shadows ?? 0}명`
	+ ` · 서쪽이 접은 언 줄 ${whileFrozen.frozenNeighbourLines}개 · 녹인 뒤 ${shadowsAfter}명`);

check('얼면 세계가 그 줄을 접는다', whileFrozen.frozenNeighbourLines >= 1,
	`접은 줄 ${whileFrozen.frozenNeighbourLines}개`);

check('녹이면 사람 손 없이 국경 너머가 다시 보인다', shadowsAfter > 0,
	`${shadowsAfter}명`);

// ★ 받는 쪽도 언다 (TASK-WM-358): 저쪽이 새로 이어 올 때마다 죽은 줄이 하나씩 남으면
//   그 줄마다 64KB 를 물고 있다 — 며칠 도는 세계에서는 그게 새는 자리가 된다.
// [문턱-사유] (c) 제품 구조 — 이웃이 하나면 살아 있는 줄도 <b>하나</b>다(오간 줄 수와 무관).
check("얼었다 녹아도 죽은 줄이 안 쌓인다", (afterLines.neighbourLinesHeld ?? 0) <= 1,
	`동쪽이 붙잡고 있는 이웃 줄 ${afterLines.neighbourLinesHeld ?? 0}개`);

if (bad === 0) {
	console.log('[언이웃] ✅ 세계 사이의 줄이 얼어도 스스로 다시 잇는다');
	process.exit(0);
}

console.log(`\n[언이웃] RESULT: ${bad}건`);
process.exit(1);
