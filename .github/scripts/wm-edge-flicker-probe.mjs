#!/usr/bin/env node
// wm-edge-flicker-probe.mjs — 보이는 <b>끝자락</b>에서 사람이 깜빡이나 (진단용 자, TASK-WM-394).
//
// ★ 왜: 세계는 32m 안의 사람만 보여 준다(PLAYER_INTEREST_RADIUS). 그 선은 <b>딱 한 줄</b>이라,
//   그 언저리에 선 사람은 한 걸음마다 「보였다 안 보였다」를 되풀이할 수 있다.
//   MMO 에서 이건 눈에 <b>깜빡임</b>으로 보인다(그리고 지웠다 그리는 값도 든다).
//
// 잰 것 (2026-08-14): 끝자락(32m)을 여섯 번 넘나들어도 <b>뒤집힘 0</b>.
//   까닭은 세계가 <b>넉넉히 보내기</b> 때문이다 — 판은 「칸 한복판 + 반경 + 칸 하나」(≈48m)까지 담고,
//   32m 로 자르는 것은 <b>창</b>이다. 그래서 <b>세계 쪽 깜빡임(그렸다 지웠다 말하기)은 없다.</b>
//   ⇒ 남은 물음은 <b>창 쪽</b>이다: 32m 선에서 그림이 깜빡이나 — 그건 진짜 창으로 재야 한다(다음 판).
//
// ⚠ 이 자를 만들며 <b>세 번</b> 틀렸다(전부 자 탓, 세계 탓 아님):
//   ① 「몇 번 눌렀나」로 거리를 어림했다 — 걸음은 세계가 속도를 재서 깎는다(39m 눌렀는데 20m).
//   ② 지켜보는 창이 <b>원점에 있다고</b> 믿었다 — 그 사람 자리를 안 읽으면 32m 를 못 잰다.
//   ③ 왕복 <b>끝</b>에서 x 를 찍고 「안 움직인다」고 봤다 — 왕복은 제자리로 돌아온다(당연히 같다).

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5506);
const cannotRun = (m) => { console.error(`[깜빡임] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-flick-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-flick-')), 'world.json');
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

function join1(watch) {
	const one = { id: null, sees: new Set(), flips: 0, watchId: null };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') one.id = said.id;
		// 제 자리는 판 안의 제 인형에서 읽는다 — 걸음은 세계가 <b>속도를 재서</b> 깎으므로
		// 「몇 번 눌렀나」로 거리를 어림하면 틀린다(첫 판에 그렇게 재다 끝자락에 닿지도 못했다).
		for (const d of (said.dolls || [])) if (d.id === one.id) one.x = d.x;
		if (said.beat !== undefined && one.socket.readyState === 1) one.socket.send(JSON.stringify({ type: 'beat', beat: said.beat }));
		if (watch === false || one.watchId === null) return;

		for (const d of (said.dolls || [])) if (d.id === one.watchId) one.sawAt = d.x;
		const drawn = (said.dolls || []).some((d) => d.id === one.watchId);
		const dropped = (said.gone || []).includes(one.watchId);
		const was = one.sees.has(one.watchId);
		if (drawn && was === false) { one.sees.add(one.watchId); one.flips += 1; }
		if (dropped && was) { one.sees.delete(one.watchId); one.flips += 1; }
	};
	return one;
}
const send = (o, m) => { if (o.socket.readyState === 1) o.socket.send(JSON.stringify(m)); };

const watcher = join1(true);
const walker = join1(false);
{
	const until = Date.now() + 30000;
	while (Date.now() < until && (watcher.id === null || walker.id === null)) await wait(100);
	if (watcher.id === null || walker.id === null) { killWorld(); cannotRun('둘이 다 못 들어갔다'); }
}
watcher.watchId = walker.id;

// 걸어서 32m 언저리로 간다 — <b>도착했나</b>를 보면서 간다(시간·횟수를 안 박는다).
{
	// ⚠ 지켜보는 창도 <b>제자리에서 시작하지 않는다</b> — 그 사람 자리를 알아야 32m 를 잰다
	//   (첫 판에 그걸 몰라 「33.6m 인데도 보인다」를 이상하게 봤다. 둘 사이 거리는 28m 였다.)
	const watchX = watcher.x ?? 0;
	const edgeX = watchX + 32;
	console.log(`[깜빡임] 지켜보는 창 x ${watchX.toFixed(2)} — 끝자락은 x ${edgeX.toFixed(2)} 언저리다`);
	const until = Date.now() + 180000;
	let step = 0;
	while (Date.now() < until && (walker.x ?? 0) < edgeX + 1.5) {
		send(walker, { type: 'move', x: 0.15, z: 0, seq: step += 1 });
		await wait(60);
	}
	if ((walker.x ?? 0) < edgeX + 1.5) { killWorld(); cannotRun(`끝자락까지 못 갔다 (x ${walker.x} · 끝자락 ${edgeX.toFixed(2)})`); }
}
await wait(1500);
console.log(`[깜빡임] 왕복 시작 — 지금 보이나: ${watcher.sees.has(walker.id)} · 내가 아는 내 자리 x ${walker.x?.toFixed(2)}`
	+ ` · <b>지켜보는 창이 본 그 사람 x</b> ${watcher.sawAt?.toFixed(2)}`);
watcher.flips = 0;

// ★ 그 선을 <b>넘나든다</b> — ±1m 를 왕복한다.
for (let lap = 0; lap < 6; lap += 1) {
	// 끝자락을 <b>안쪽으로</b> 넘었다가 다시 바깥으로 — 3m 를 오간다.
	for (let step = 0; step < 20; step += 1) { send(walker, { type: 'move', x: -0.15, z: 0, seq: 10000 + lap * 100 + step }); await wait(60); }
	for (let step = 0; step < 20; step += 1) { send(walker, { type: 'move', x: 0.15, z: 0, seq: 15000 + lap * 100 + step }); await wait(60); }
}
await wait(1500);

const done = await (await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })).json();
console.log(`[깜빡임] 세계 장부 — 물린 걸음 ${done.refusedSteps} · 사람 ${done.people} · 방송 ${done.broadcastSnapshotMessages}장`);
console.log(`[깜빡임] 왕복 끝 — 지금 보이나: ${watcher.sees.has(walker.id)} · 내가 아는 내 자리 x ${walker.x?.toFixed(2)}`
	+ ` · 지켜보는 창이 본 그 사람 x ${watcher.sawAt?.toFixed(2)}`);
console.log(`[깜빡임] 끝자락에서 여섯 번 오간 동안 <b>보였다 안 보였다</b> 뒤집힌 횟수: ${watcher.flips}`);
watcher.socket.close();
walker.socket.close();
await wait(300);
killWorld();
process.exit(0);
