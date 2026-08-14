#!/usr/bin/env node
// wm-cell-size-probe.mjs — <b>칸 크기를 재서 고른다</b> (진단용 자, TASK-WM-396).
//
// ★ 왜: 세계는 판을 <b>칸 단위로 한 장</b> 지어 그 칸 사람들이 나눠 쓴다(WM-220).
//   칸이 크면 한 장을 여럿이 나눠 써서 세계가 덜 일하고, 대신 그 장이 칸 언저리 사람까지 덮어야 해서
//   <b>넉넉히 보낸다</b>(바이트 ↑). 지금 값 16m 은 재 보고 고른 값이 아니었다.
//
// ⚠ 무대가 <b>가를 수 있어야</b> 한다: 봇이 다 한자리에 모여 있으면 칸을 어떻게 나눠도 같다
//   (첫 판에 그렇게 재다가 8·16·24m 이 전부 83MB 로 똑같이 나왔다 — 자가 못 가른 것).
//   그래서 이 자는 봇을 <b>펼친다</b>.

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5560);
const HOW_MANY = Number(process.env.WM_FULL_HOUSE || 200);
const PLAY_MS = Number(process.env.WM_FULL_HOUSE_MS || 20000);
const SPREAD_MS = Number(process.env.WM_SPREAD_MS || 40000);
const TICK_MS = 50; // 세계의 한 판 (초당 20판) — 제품 상수다

const cannotRun = (m) => { console.error(`[칸크기]  CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-cell-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-cell-')), 'world.json');
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

// ★ 먼저 <b>펼친다</b> — 저마다 제 방향으로 걸어 나간다(속도는 세계가 잰다).
{
	const until = Date.now() + SPREAD_MS;
	let step = 0;
	while (Date.now() < until) {
		step += 1;
		for (let i = 0; i < bots.length; i += 1) {
			const one = bots[i];
			if (one.socket.readyState !== 1) continue;

			const turn = (i / bots.length) * Math.PI * 2;
			one.socket.send(JSON.stringify({ type: 'move', x: Math.cos(turn) * 0.15, z: Math.sin(turn) * 0.15, seq: step }));
		}
		await wait(80);
	}
}
console.log('[칸크기] 펼치기 끝');

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
	console.log(`[칸크기] ✅ ${HOW_MANY}명이 붙어도 세계가 버틴다`);
	process.exit(0);
}

console.log(`\n[칸크기] RESULT: ${failures}건`);
process.exit(1);
