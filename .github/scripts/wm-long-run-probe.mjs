#!/usr/bin/env node
// wm-long-run-probe.mjs — <b>오래 두면 새나</b> (진단용 자, TASK-WM-391).
// 봇을 붙여 놓고 몇 분 두면서 기억·판 사이를 재 본다. <b>관문 아님</b>(3분짜리라 CI 에 안 건다).
//
// 실측 (이 기계, 봇 200명 · 3분):
//   기억 233 → 411 → 158 → 328 → 74 → 237MB (톱니 = 치우고 다시 쌓인다) · 사람 200 고정 ·
//   가장 긴 판 사이 146ms 로 <b>안 자란다</b> · 방송 348MB/3분(초당 1.9MB) · 만든 쓰레기 1082MB(초당 6MB)
//   ⇒ <b>안 샌다.</b> 들고 있는 양이 오르내리다 제자리로 돌아온다(자라기만 하면 그게 샘이다).
//
// 곁: 60초짜리 판에서는 <b>한 번도 안 치웠다</b>(gen0/1/2 = 0, 들고 있는 양 = 만든 양).
//   서버 GC 는 넉넉할 때 안 치운다 — 「기억 400MB」는 살아 있는 양이 아니라 <b>아직 안 치운 양</b>이다.

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5486);
const HOW_MANY = Number(process.env.WM_FULL_HOUSE || 200);
const PLAY_MS = Number(process.env.WM_FULL_HOUSE_MS || 180000);
const TICK_MS = 50; // 세계의 한 판 (초당 20판) — 제품 상수다

const cannotRun = (m) => { console.error(`[오래두기]  CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-long-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-long-')), 'world.json');
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

// 30초마다 재 본다 — 자라면 그게 샘이다.
const rows = [];
const started = Date.now();
while (Date.now() - started < PLAY_MS) {
	await wait(30000);
	const now = await health();
	rows.push(`${((Date.now() - started) / 1000).toFixed(0)}초 — 사람 ${now.people} · 기억 ${now.heldMegabytes}MB`
		+ ` · 만든 쓰레기 ${now.allocatedMegabytes}MB · gen0 ${now.gcGen0} · gen1 ${now.gcGen1} · gen2 ${now.gcGen2} · 가장 긴 판 사이 ${now.longestTickGapMs}ms`
		+ ` · 방송 ${(now.broadcastSnapshotBytes / 1048576).toFixed(0)}MB`);
	console.log(`[오래두기] ${rows[rows.length - 1]}`);
}
clearInterval(walking);

for (const one of bots) { try { one.socket.close(); } catch { /* 이미 */ } }
await wait(1000);
const ending = await health();
console.log(`[오래두기] 사람이 다 나간 뒤 — 사람 ${ending.people} · 기억 ${ending.heldMegabytes}MB`);
killWorld();
process.exit(0);
