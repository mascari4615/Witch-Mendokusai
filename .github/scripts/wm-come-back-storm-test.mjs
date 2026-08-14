#!/usr/bin/env node
// wm-come-back-storm-test.mjs — 세계를 껐다 켜면 <b>400명이 다 돌아오나</b> (TASK-WM-389).
//
// ★ 왜: 배포는 곧 재시작이다. 그 순간 붙어 있던 <b>모두가 동시에</b> 다시 붙는다 —
//   MMO 가 가장 자주 겪는 몰림이고, 하필 세계가 막 깬 순간이다(기억을 되살리는 중).
//   여태 잰 것은 「한 사람이 다시 붙는다」(reconnect) 뿐이었다. <b>다 같이</b>는 안 쟀다.
//
// 재는 것:
//   ① 껐다 켜도 <b>같은 사람</b>으로 돌아온다 (신원 번호가 그대로다 — 열쇠로 알아본다)
//   ② 다 돌아온다 (한 명도 문 앞에서 안 막힌다)
//   ③ 세계는 그 몰림에도 안 멎는다 (판 사이 = 판 하나의 열 배 안)
//
// 실측 (이 기계, 봇 400명): 껐다 켠 뒤 <b>0.9초</b> 만에 400명이 <b>제 신원 번호 그대로</b> 돌아왔다 ·
//   세계가 센 사람 400 · 가장 긴 판 사이 107ms(판 하나의 2.1배).
//
// [빨강-확인] 신원 장부를 <b>안 적게</b> 해 보니 빨강 — 「같은 사람으로 돌아온다 1/60」 (2026-08-14).
//   즉 이 자는 「기억이 안 남으면」을 진짜로 잡는다.
//
// exit: 0 = 다 같은 사람으로 돌아온다 · 1 = 누가 못 돌아오거나 남이 된다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5482);
const HOW_MANY = Number(process.env.WM_COME_BACK || 400);
const PLAY_MS = Number(process.env.WM_COME_BACK_MS || 6000);
const TICK_MS = 50; // 세계의 한 판 (초당 20판) — 제품 상수다

const cannotRun = (m) => { console.error(`[다시몰림]  CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-storm-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-storm-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
});
const killWorld = () => {
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
};

{
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try { const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }); if (answer.ok) { up = true; break; } } catch { /* 아직 */ }
		await wait(300);
	}
	if (up === false) { killWorld(); cannotRun(`세계가 안 떴다 (${port})`); }
}

const health = async () => (await (await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })).json());

// ── 봇을 정원만큼 붙인다 ──────────────────────────────────────────────
const bots = [];
for (let i = 0; i < HOW_MANY; i += 1) {
	const one = { id: null, who: 0, secret: '', plates: 0, closed: false };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: one.secret }));
	one.socket.onerror = () => { /* 아래가 센다 */ };
	one.socket.onclose = () => { one.closed = true; };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') {
			one.id = said.id;
			// 세계가 주는 열쇠를 챙긴다 — 껐다 켠 뒤 <b>이 열쇠로</b> 같은 사람임을 보인다.
			if (said.secret) one.secret = said.secret;
			if (said.identityId) one.who = said.identityId;
		}
		// ★ 진짜 창처럼 <b>숨소리</b>를 돌려준다 (WM-343) — 안 그러면 세계가 다른 길로 보낸다.
		if (said.type === 'world' || said.type === 'delta') {
			one.plates += 1;
			if (said.beat !== undefined) one.socket.send(JSON.stringify({ type: 'beat', beat: said.beat }));
		}
	};
	bots.push(one);
	if (i % 25 === 24) await wait(50); // 한꺼번에 400개를 열면 이 자가 먼저 넘어진다
}

{
	const until = Date.now() + 60000;
	while (Date.now() < until && bots.filter((one) => one.id !== null).length < HOW_MANY) await wait(200);
}
const joined = bots.filter((one) => one.id !== null).length;
if (joined < HOW_MANY) {
	const before = await health();
	killWorld();
	cannotRun(`${HOW_MANY}명 중 ${joined}명만 들어갔다 (세계가 센 사람 ${before.people}) — 이 기계에서는 정원을 못 잰다`);
}

for (const one of bots) one.plates = 0; // 여기서부터 센다

const walking = setInterval(() => {
	for (const one of bots) {
		if (one.socket.readyState !== 1) continue;
		one.socket.send(JSON.stringify({ type: 'move', x: (Math.random() - 0.5) * 0.3, z: (Math.random() - 0.5) * 0.3, seq: one.plates + 1 }));
	}
}, 200);

await wait(PLAY_MS);
clearInterval(walking);

const before = await health();
const iWas = bots.map((one) => one.who);
const hadSecret = bots.filter((one) => one.secret).length;
if (hadSecret < HOW_MANY) { killWorld(); cannotRun(`열쇠를 받은 봇이 ${hadSecret}명뿐이다 — 같은 사람인지 못 잰다`); }

// ★ 여기가 이 관문의 핵심 — 세계를 껐다 켜고 <b>다 같이</b> 다시 붙는다.
console.log('[다시몰림] 세계를 껐다 켠다 — 그리고 모두가 동시에 다시 붙는다');
for (const one of bots) { try { one.socket.close(); } catch { /* 이미 */ } }
killWorld();
await wait(1500);

const worldAgain = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
});
const killAgain = () => {
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${worldAgain.pid} /F /T`, { stdio: 'ignore' });
		else worldAgain.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
};
{
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try { const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }); if (answer.ok) { up = true; break; } } catch { /* 아직 */ }
		await wait(200);
	}
	if (up === false) { killAgain(); cannotRun('세계가 다시 안 떴다'); }
}

// ⚠ 몰림을 <b>진짜로</b> 만든다 — 사이를 안 띄우고 한꺼번에 연다.
const backAt = Date.now();
for (const one of bots) {
	one.id = null;
	one.closed = false;
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	const mine = one;
	one.socket.onopen = () => mine.socket.send(JSON.stringify({ type: 'hello', secret: mine.secret }));
	one.socket.onerror = () => { /* 아래가 센다 */ };
	one.socket.onclose = () => { mine.closed = true; };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') { mine.id = said.id; if (said.identityId) mine.whoNow = said.identityId; }
	};
}

// ⚠ <b>「붙었다」로 세면 안 된다</b> — 세계는 인사(hello)를 읽기 <b>전에</b> 첫 환영을 보낸다(WM-301).
//   그 첫 장에는 신원이 없다. 그걸로 세면 「0.4초 만에 다 돌아왔다」가 나오는데
//   그건 <b>줄이 열린 시각</b>일 뿐이다(첫 판에 그렇게 재다가 「같은 사람 0/400」으로 봤다 — 자 탓).
//   기다릴 것은 <b>제 이름으로</b> 돌아온 순간이다(둘째 환영에 신원이 실린다).
{
	const until = Date.now() + 120000;
	while (Date.now() < until && bots.filter((one) => one.whoNow).length < HOW_MANY) await wait(200);
}
const cameBack = bots.filter((one) => one.whoNow).length;
const tookMs = Date.now() - backAt;
const samePerson = bots.filter((one, i) => one.whoNow && one.whoNow === iWas[i]).length;

const after = await health();
for (const one of bots) { try { one.socket.close(); } catch { /* 이미 */ } }
await wait(500);
killAgain();

let failures = 0;
const check = (what, ok, detail) => { if (ok === false) failures += 1; console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`); };

console.log(`  ⓘ 봇 ${HOW_MANY}명 — 껐다 켠 뒤 ${cameBack}명이 ${(tookMs / 1000).toFixed(1)}초 만에 돌아왔다`
	+ ` · 세계가 센 사람 ${after.people} · 신원 ${after.identities} · 가장 긴 판 사이 ${after.longestTickGapMs}ms`
	+ ` (껐을 때 사람 ${before.people})`);

check('다 돌아온다', cameBack === HOW_MANY, `${cameBack}/${HOW_MANY}명`);
check('같은 사람으로 돌아온다', samePerson === HOW_MANY, `${samePerson}/${HOW_MANY}명이 제 신원 번호 그대로`);
check('세계가 그 몰림에 안 멎는다 (판 하나의 열 배 안)', after.longestTickGapMs <= TICK_MS * 10,
	`가장 긴 판 사이 ${after.longestTickGapMs}ms (판 하나 ${TICK_MS}ms)`);

if (failures === 0) {
	console.log(`[다시몰림] ✅ 껐다 켜도 ${HOW_MANY}명이 제 이름으로 다 돌아온다`);
	process.exit(0);
}

console.log(`
[다시몰림] RESULT: ${failures}건`);
process.exit(1);
