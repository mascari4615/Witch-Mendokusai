#!/usr/bin/env node
// wm-buddy-next-to-me-test.mjs — <b>바로 옆 사람</b>이 무리 때문에 얼어붙나 (TASK-WM-397·398).
//
// ★ 왜: 판은 <b>칸 한복판</b> 기준으로 「가까운 48명」을 골라 그 칸 사람들이 나눠 쓴다.
//   칸을 24m 로 키운 뒤(WM-396) 한복판과 <b>칸 구석</b>의 차이가 최대 17m 다.
//   구석에 선 사람 옆에 친구가 서 있어도, 무리가 한복판에 몰려 있으면 그 친구가 <b>48위 밖</b>으로
//   밀릴 수 있다 — 「옆에 있는데 안 보인다」. 그건 MMO 에서 가장 나쁜 꼴이다.
//
// 무대: 무리 60명은 한복판 언저리에서 <b>움직이고</b>, 나와 친구는 칸 구석에 <b>가만히</b> 선다.
//   (움직이는 사람에게 떼어 둔 자리가 있어서, 가만히 선 친구가 가장 불리하다.)
//
// 잰 것 (2026-08-14): 친구가 <b>0.9m 옆에서 움찔거리는데 그 움직임이 8초 동안 0번</b> 왔다.
//   ⇒ 옆 사람이 <b>얼어붙는다</b>(안 보이는 게 아니라 그 자리에 멈춰 있다 — 그게 진짜 증상이다).
//
// 고침 (TASK-WM-398): <b>몰린 칸을 쪼갠다</b> — 24m → 12 → 6 → 3m. 칸이 작아지면 한복판이 곧 내 자리다.
//   고친 뒤 움직임 <b>40번</b>. 광장(400명 한자리) 비용은 그대로다(바이트 82MB · 지은 그림 1998 — 안 늘었다).
//
// [빨강-확인] 쪼개기를 끄면(옛 동작) 이 자가 곧바로 빨강 — 「움직임 8초 동안 0번」 (2026-08-14).
//
// 먼저 시도했다 되돌린 것: 「사람마다 제 옆 넷을 얹기」 — 움직임은 40번이 됐지만
//   광장 바이트가 83MB → 530MB(6.4배)로 터졌다. 반쪽 고침은 안 남긴다.
//
// ⚠ 이 자를 만들며 두 번 틀렸다: ① 「보이나」로 쟀다 — 진짜 창은 한 번 받은 사람을 계속 그리므로
//   가만히 선 친구는 늘 「보인다」다. 잴 것은 <b>움직임이 오나</b>다.
//   ② 장부를 비우고 다시 셌다 — 「나갔다」를 안 들은 사람은 창에 그대로 남는다.

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5578);
const CROWD = Number(process.env.WM_CROWD || 60);
const cannotRun = (m) => { console.error(`[옆사람] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));
if (!globalThis.WebSocket) cannotRun('이 node 에는 WebSocket 이 없다');

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-buddy-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-buddy-')), 'world.json');
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
	const one = { id: null, x: 0, z: 0, sees: new Set() };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') one.id = said.id;
		// ★ 자리는 `world` 에서만 읽는다 (2026-08-20). `names` 도 `dolls` 배열을 갖는데
		//   거기엔 x/z 가 없다 — 이름은 따로 나른다(TASK-WM-220). 종류를 안 가리고 읽으면
		//   `names` 한 통에 one.x 가 undefined 로 덮여, 시험이 `toFixed` 에서 터진다.
		//   자가 틀리면 관문은 헛것을 지킨다.
		if (said.type !== 'world') {
			if (said.beat !== undefined && one.socket.readyState === 1) one.socket.send(JSON.stringify({ type: 'beat', beat: said.beat }));
			return;
		}
		for (const d of (said.dolls || [])) {
			if (d.id === one.id) { one.x = d.x; one.z = d.z; }
			one.sees.add(d.id);
			// 그 사람의 <b>움직임</b>이 나에게 오나 — 자리가 바뀌어 온 횟수를 센다.
			if (one.watch === d.id) {
				const spot = `${d.x},${d.z}`;
				if (spot !== one.lastSpot) { one.lastSpot = spot; one.moves = (one.moves || 0) + 1; }
			}
		}
		if (said.beat !== undefined && one.socket.readyState === 1) one.socket.send(JSON.stringify({ type: 'beat', beat: said.beat }));
	};
	return one;
}
const send = (o, m) => { if (o.socket.readyState === 1) o.socket.send(JSON.stringify(m)); };

const me = joinOne();
const buddy = joinOne();
const crowd = [];
for (let i = 0; i < CROWD; i += 1) { crowd.push(joinOne()); if (i % 20 === 19) await wait(50); }

{
	const until = Date.now() + 60000;
	const all = [me, buddy, ...crowd];
	while (Date.now() < until && all.some((one) => one.id === null)) await wait(200);
	if (all.some((one) => one.id === null)) { killWorld(); cannotRun('다 못 들어갔다'); }
}

// 나와 친구는 칸 구석으로 걸어간다 (칸 24m — 한복판은 12,12 · 구석은 23,23 언저리).
{
	const until = Date.now() + 90000;
	let step = 0;
	while (Date.now() < until && (me.x < 22.5 || me.z < 22.5 || buddy.x < 21.5 || buddy.z < 22.5)) {
		step += 1;
		if (me.x < 22.5 || me.z < 22.5) send(me, { type: 'move', x: me.x < 22.5 ? 0.15 : 0, z: me.z < 22.5 ? 0.15 : 0, seq: step });
		if (buddy.x < 21.5 || buddy.z < 22.5) send(buddy, { type: 'move', x: buddy.x < 21.5 ? 0.15 : 0, z: buddy.z < 22.5 ? 0.15 : 0, seq: step });
		await wait(60);
	}
}
console.log(`[옆사람] 나 (${me.x.toFixed(1)}, ${me.z.toFixed(1)}) · 친구 (${buddy.x.toFixed(1)}, ${buddy.z.toFixed(1)})`
	+ ` — 사이 ${Math.hypot(me.x - buddy.x, me.z - buddy.z).toFixed(1)}m`);

// 무리는 한복판(12,12) 언저리에서 <b>움직인다</b>.
const milling = setInterval(() => {
	for (const one of crowd) {
		if (one.socket.readyState !== 1) continue;
		const toX = 12 - one.x;
		const toZ = 12 - one.z;
		const far = Math.hypot(toX, toZ) > 4;
		const x = far ? Math.sign(toX) * 0.15 : (Math.random() - 0.5) * 0.3;
		const z = far ? Math.sign(toZ) * 0.15 : (Math.random() - 0.5) * 0.3;
		one.socket.send(JSON.stringify({ type: 'move', x, z, seq: Date.now() % 100000 }));
	}
}, 150);

// ★ 친구도 <b>제자리에서 움찔거린다</b> — 「보이나」가 아니라 <b>움직임이 나에게 오나</b>를 잰다.
//   진짜 창은 한 번 받은 사람을 계속 그리므로, 「안 보인다」가 아니라 <b>얼어붙는다</b>가 진짜 증상이다.
me.watch = buddy.id;
const buddyFidget = setInterval(() => {
	if (buddy.socket.readyState === 1)
		buddy.socket.send(JSON.stringify({ type: 'move', x: (Math.random() - 0.5) * 0.2, z: (Math.random() - 0.5) * 0.2, seq: Date.now() % 100000 }));
}, 200);

await wait(20000);
// ⚠ <b>지웠다 다시 세면 안 된다</b> — 진짜 창은 「나갔다」는 말을 듣기 전까지 그 사람을 계속 그린다.
//   가만히 선 사람은 델타에 안 실리므로, 장부를 비우고 8초만 세면 <b>안 보인다</b>로 잘못 읽는다
//   (이 자의 첫 판이 그랬다 — 자 탓, 오늘 여섯 번째).
//   그래서 「세계가 한 번이라도 말해 줬나」로 본다. 대신 <b>나갔다</b>는 말은 지운다.
me.socket.addEventListener('message', (event) => {
	let said;
	try { said = JSON.parse(String(event.data)); } catch { return; }
	for (const goneId of (said.gone || [])) me.sees.delete(goneId);
});
me.moves = 0;
await wait(8000);
clearInterval(milling);
clearInterval(buddyFidget);

const sawBuddy = me.sees.has(buddy.id);
const howManySeen = me.sees.size;
const crowdAt = crowd.filter((one) => Math.hypot(one.x - 12, one.z - 12) < 6).length;

console.log(`[옆사람] 무리 ${crowdAt}/${CROWD} 명이 한복판에 모였다 · 내가 8초 동안 본 사람 ${howManySeen}명`);
console.log(`[옆사람] <b>바로 옆 친구의 움직임이 8초 동안 ${me.moves ?? 0}번 왔다</b> (보이나: ${sawBuddy ? '보인다' : '안 보인다'})`);

for (const one of [me, buddy, ...crowd]) { try { one.socket.close(); } catch { /* 이미 */ } }
await wait(300);
killWorld();
// 초당 20판이 도는 8초다 — 옆 사람이 움찔거리면 수십 번은 와야 한다.
process.exit((me.moves ?? 0) >= 20 ? 0 : 1);
