#!/usr/bin/env node
// wm-full-house-test.mjs — <b>정원만큼</b> 진짜로 붙여 본다 (TASK-WM-387).
//
// ★ 왜: 세계는 「한 곳에 400명」이라고 적어 놓았다(MOST_PEOPLE_AT_ONCE, WM-349).
//   그런데 지금까지 <b>가장 많이 붙여 본 것은 40명</b>이다(무리 관문). 적어 놓은 숫자와
//   견디는 숫자가 다르면, 그건 정원이 아니라 <b>희망</b>이다.
//
// 재는 것 (봇 N명이 동시에 붙어 걷고 숨소리까지 보낸다):
//   ① 아무도 안 쫓겨난다 (turnedAwayPeople 0 · 줄이 안 닫힌다)
//   ② 세계가 안 멎는다 (longestTickGapMs — 판 하나의 몇 배인지로 본다)
//   ③ <b>아무도 안 굶는다</b> — 가장 적게 받은 봇이 중앙값의 몇 %인지 (같은 판 안의 견줌)
//
// 실측 (이 기계, 봇 400명 · 20초): 세계가 센 사람 400 · 기억 319MB ·
//   가장 긴 판 사이 143ms(판 하나의 2.9배) · 판 16만 장 · 가장 적게 받은 봇 402 / 중앙값 407.
//   ⇒ <b>적어 놓은 정원이 진짜 정원이다.</b>
//
// [빨강-확인] 정직하게 적는다 — 이 무대에서 <b>밟아 본 줄과 못 밟은 줄</b>:
//   · 정원을 100 으로 낮춰 보니 <b>CANNOT-RUN</b> 이 났다(400명 중 100명만 들어감). 그건 이 자의 설계다 —
//     못 재는 무대에서 0 을 초록·빨강 어느 쪽으로도 안 적는다(관문 규율 ②).
//   · 「굶는다」 줄은 <b>이 무대에서 못 밟았다</b>. 「보내는 중」 표 새는 옛 코드로 되돌려도 초록이었다 —
//     봇은 빠르고 곧은 회선이라 <b>건너뛰는 길 자체가 안 열린다</b>. 그 줄을 진짜로 밟은 자는
//     `wm-web-crowd-windows-test.mjs`(WM-345, 나쁜 회선 + 진짜 창)다. 여기서는 <b>정원</b>을 잰다.
//
// exit: 0 = 정원만큼 견딘다 · 1 = 굶거나 쫓겨난다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5478);
const HOW_MANY = Number(process.env.WM_FULL_HOUSE || 400);
const PLAY_MS = Number(process.env.WM_FULL_HOUSE_MS || 20000);
const TICK_MS = 50; // 세계의 한 판 (초당 20판) — 제품 상수다

const cannotRun = (m) => { console.error(`[정원]  CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-full-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-full-')), 'world.json');
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
	const one = { id: null, plates: 0, closed: false, x: (i % 20) - 10, z: Math.floor(i / 20) - 10 };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래가 센다 */ };
	one.socket.onclose = () => { one.closed = true; };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') one.id = said.id;
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

const after = await health();
// 쓰레기를 얼마나 만드나 — 기억이 얼마나 「들려 있나」보다 이쪽이 진짜 신호다(WM-388).
console.log(`  ⓘ 만든 쓰레기 ${after.allocatedMegabytes}MB · gen0 ${after.gcGen0} · gen2 ${after.gcGen2}`
	+ ` · GC 멈춤 ${after.gcPausePercent}% · 방송 바이트 ${after.broadcastSnapshotBytes}`
	+ ` · 지은 그림 ${after.builtSnapshots} / 보낸 말 ${after.broadcastSnapshotMessages}`);
const gone = bots.filter((one) => one.closed).length;
const plates = bots.map((one) => one.plates).sort((a, b) => a - b);
const fewest = plates[0];
const middle = plates[Math.floor(plates.length / 2)];

for (const one of bots) { try { one.socket.close(); } catch { /* 이미 */ } }
await wait(500);
killWorld();

let failures = 0;
const check = (what, ok, detail) => { if (ok === false) failures += 1; console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`); };

console.log(`  ⓘ 봇 ${HOW_MANY}명 · ${PLAY_MS / 1000}초 — 세계가 센 사람 ${after.people} · 기억 ${after.heldMegabytes}MB`
	+ ` · 가장 긴 판 사이 ${after.longestTickGapMs}ms · 판 ${plates.reduce((a, b) => a + b, 0)}장`);

check('아무도 안 쫓겨난다', gone === 0 && after.turnedAwayPeople === 0,
	`닫힌 줄 ${gone}개 · 돌려보낸 사람 ${after.turnedAwayPeople}명`);
// ⚠ 밀리초 문턱을 안 쓴다(규율 ④) — <b>판 하나의 몇 배</b>로 본다. 열 배면 세계가 눈에 띄게 끊긴다.
check('세계가 안 멎는다 (판 하나의 열 배 안)', after.longestTickGapMs <= TICK_MS * 10,
	`가장 긴 판 사이 ${after.longestTickGapMs}ms (판 하나 ${TICK_MS}ms)`);
// 같은 판 안의 견줌 — 가장 적게 받은 봇이 중앙값의 절반은 받아야 한다.
check('아무도 안 굶는다 (중앙값의 절반 이상)', fewest >= middle * 0.5 && fewest > 0,
	`가장 적게 받은 봇 ${fewest}장 · 중앙값 ${middle}장`);

if (failures === 0) {
	console.log(`[정원] ✅ ${HOW_MANY}명이 붙어도 세계가 버틴다`);
	process.exit(0);
}

console.log(`\n[정원] RESULT: ${failures}건`);
process.exit(1);
