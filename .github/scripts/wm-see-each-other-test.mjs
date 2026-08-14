#!/usr/bin/env node
// wm-see-each-other-test.mjs — <b>약속한 거리 안에서는 서로가 서로를 본다</b> (TASK-WM-401).
//
// ★ 왜: 세계가 약속한 것은 32m 다. 그런데 판은 <b>칸 한복판 + 반경 + 칸</b>(56m)까지 담는다 —
//   그래서 실제로 보이는 거리는 <b>내가 칸 어디에 서 있나</b>에 달린다. 잘못하면 둘 사이가 같아도
//   한쪽만 보는 판이 생긴다 — 싸움에서 가장 나쁜 꼴이다(「나는 안 보이는데 상대는 나를 본다」).
//
// 재는 것: 30m 떨어진 두 사람을 <b>칸 경계를 여러 곳</b>에 걸쳐 세우고, 서로의 <b>움직임</b>이
//   양쪽에 다 오는지 본다. 「보이나」가 아니라 「움직임이 오나」다(WM-397 에서 배웠다).
//
// 문턱: 약속(32m)보다 가까운 30m — 여기서는 <b>어느 칸에 서 있든</b> 둘 다 봐야 한다.
//
// [빨강-확인] 반경을 절반(16m)으로 줄이니 <b>바로 그 꼴</b>이 나왔다 (2026-08-14):
//   「x 22.7 와 x 53.0 — 서쪽→동쪽 <b>못 본다</b> · 동쪽→서쪽 본다」 = 한쪽만 보는 판.
//
// exit: 0 = 어디에 서든 서로 본다 · 1 = 한쪽만 본다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5478);
const cannotRun = (m) => { console.error(`[서로보기] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-see-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-see-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll), env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore',
});
const killWorld = () => {
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 */ }
};
{
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try { const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }); if (answer.ok) { up = true; break; } } catch { /* 아직 */ }
		await wait(300);
	}
	if (up === false) { killWorld(); cannotRun('세계가 안 떴다'); }
}

function joinOne() {
	const one = { id: null, x: 0, z: 0, fresh: new Set() };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') one.id = said.id;
		for (const d of (said.dolls || [])) {
			if (d.id === one.id) { one.x = d.x; one.z = d.z; }
			else one.fresh.add(d.id);
		}
		if (said.beat !== undefined && one.socket.readyState === 1) one.socket.send(JSON.stringify({ type: 'beat', beat: said.beat }));
	};
	return one;
}
const send = (o, m) => { if (o.socket.readyState === 1) o.socket.send(JSON.stringify(m)); };

const west = joinOne();
const east = joinOne();
{
	const until = Date.now() + 30000;
	while (Date.now() < until && (west.id === null || east.id === null)) await wait(100);
	if (west.id === null || east.id === null) { killWorld(); cannotRun('둘이 다 못 들어갔다'); }
}

// 둘을 <b>칸 경계를 사이에 두고</b> 세운다 — 칸 24m 기준: 한쪽은 칸 한복판(12), 한쪽은 그 밖(45).
const goTo = async (who, wantX) => {
	const until = Date.now() + 120000;
	let step = 0;
	while (Date.now() < until && Math.abs(who.x - wantX) > 0.3) {
		send(who, { type: 'move', x: who.x < wantX ? 0.15 : -0.15, z: 0, seq: step += 1 });
		await wait(60);
	}
};
// ★ 칸 구석과 칸 한복판을 <b>일부러</b> 고른다 (칸 24m): 서쪽은 제 칸 구석(23.5 · 한복판 12),
//   동쪽은 제 칸 한복판(60). 사이는 37m 로 <b>둘 다 32m 밖</b>이다 —
//   그런데 동쪽 칸의 한복판(60)에서 보면 서쪽은 36.5m, 서쪽 칸 한복판(12)에서 보면 동쪽은 48m 다.
const SPOTS = [1.0, 11.5, 23.0];   // 칸(24m) 안쪽·한복판·구석
const APART = 30;                   // 약속(32m)보다 가깝다

let failures = 0;
const check = (what, ok, detail) => { if (ok === false) failures += 1; console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`); };

for (const spot of SPOTS) {
	await Promise.all([goTo(west, spot), goTo(east, spot + APART)]);
	await wait(1200);

	west.fresh.clear();
	east.fresh.clear();
	const fidget = setInterval(() => {
		for (const one of [west, east])
			send(one, { type: 'move', x: (Math.random() - 0.5) * 0.1, z: (Math.random() - 0.5) * 0.1, seq: Date.now() % 100000 });
	}, 200);
	await wait(5000);
	clearInterval(fidget);

	const westSeesEast = west.fresh.has(east.id);
	const eastSeesWest = east.fresh.has(west.id);
	check(`x ${west.x.toFixed(1)} 와 x ${east.x.toFixed(1)} 가 서로 본다 (사이 ${Math.abs(east.x - west.x).toFixed(1)}m)`,
		westSeesEast && eastSeesWest,
		`서쪽→동쪽 ${westSeesEast ? '본다' : '못 본다'} · 동쪽→서쪽 ${eastSeesWest ? '본다' : '못 본다'}`);
}

west.socket.close();
east.socket.close();
await wait(300);
killWorld();

if (failures === 0) {
	console.log('[서로보기] ✅ 약속한 거리 안에서는 어디에 서든 서로를 본다');
	process.exit(0);
}
console.log(`
[서로보기] RESULT: ${failures}건`);
process.exit(1);
