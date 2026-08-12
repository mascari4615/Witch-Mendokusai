#!/usr/bin/env node
// wm-server-soak-test.mjs — <b>오래 돌면 새는가</b> (TASK-WM-296).
//
// ★ 왜: 지금까지 잰 것은 전부 <b>몇 초짜리</b>다. 그런데 세계는 며칠을 돈다(prod 는 노트북에서
//   24시간 산다). 며칠 도는 서버에서 무너지는 것은 프레임이 아니라 <b>천천히 자라는 것</b>이다:
//   기억(메모리)·판 사이의 멎음·장부. 그건 짧은 시험으로는 절대 안 보인다.
//
// 재는 것: 사람 여럿을 붙여 계속 걷게 하고 90초 동안 세계의 속을 들여다본다.
//   ① 기억이 계속 자라나(앞 3분의 1 vs 뒤 3분의 1) ② 판 사이가 벌어지나 ③ 세계가 살아 있나
//
// ⚠ 절대 MB 로 자르지 않는다 (domain-wm.md § 관문 규율 ④) — 기계마다 다르다.
//   <b>자라는가</b>를 본다: 처음보다 뒤가 크게 늘면 그건 새는 것이다.
//
// 필요한 것: .NET 8. (창은 안 띄운다 — 이 자리는 서버 문제다.)
// exit: 0 = 안 샌다 · 1 = 샌다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5410);
const crowd = Number(process.env.WM_SOAK_CROWD || 40);
const soakMs = Number(process.env.WM_SOAK_MS || 90000);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-soak-')), 'world.json');

/** 뒤쪽 기억이 앞쪽보다 이만큼 넘게 크면 「자란다」로 본다. */
const MOST_MEMORY_GROWTH = 1.6;

/** 판 사이가 이보다 오래 벌어지면 사람은 「멎었다」로 느낀다 (ms). */
const MOST_TICK_GAP_MS = 1500;

function cannotRun(message) {
	console.error(`[soak] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-soak-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

let world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
});

function killWorld() {
	if (world === null) return;
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
	world = null;
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));
const look = () => fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })
	.then((answer) => answer.json())
	.catch(() => null);

{
	const until = Date.now() + 120000;
	let up = false;
	while (Date.now() < until) {
		const health = await look();
		if (health && health.ok) { up = true; break; }

		await wait(300);
	}

	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다');
	}
}

// ── 사람을 붙이고 계속 걷게 둔다 ───────────────────────────────────────
const bots = [];
for (let i = 0; i < crowd; i += 1) {
	const socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onerror = () => { /* 못 붙은 놈은 아래 숫자에서 빠진다 */ };
	bots.push(socket);
}

await wait(3000);

const joined = await look();
check(`${crowd}명이 세계에 있다`, joined !== null && joined.people >= crowd,
	joined === null ? '세계가 대답을 안 한다' : `세계가 세는 사람 ${joined.people}명`);

if (joined === null || joined.people < crowd) {
	for (const one of bots) { try { one.close(); } catch { /* 닫혔다 */ } }
	killWorld();
	cannotRun('사람이 다 안 붙었다 — 이 상태로 오래 돌려 봐야 뜻이 없다');
}

const walking = setInterval(() => {
	for (const socket of bots) {
		if (socket.readyState !== 1) continue;

		socket.send(JSON.stringify({ type: 'move', x: (Math.random() - 0.5) * 2, z: (Math.random() - 0.5) * 2 }));
	}
}, 200);

// ── 90초 동안 세계의 속을 들여다본다 ───────────────────────────────────
const seen = [];
{
	const until = Date.now() + soakMs;
	while (Date.now() < until) {
		const health = await look();
		if (health && health.ok) {
			seen.push({
				at: Date.now(),
				// ⚠ allocatedMegabytes 는 <b>누적</b>이라 늘 자란다(첫 판이 그걸로 3.48배 빨강이었다).
				//   「지금 들고 있는 양」으로 본다 (TASK-WM-296).
				memory: Number(health.heldMegabytes || 0),
				allocated: Number(health.allocatedMegabytes || 0),
				gap: Number(health.longestTickGapMs || 0),
				gen2: Number(health.gcGen2 || 0),
				pause: Number(health.gcPausePercent || 0),
				people: Number(health.people || 0),
			});
		}

		await wait(2000);
	}
}

clearInterval(walking);
for (const one of bots) { try { one.close(); } catch { /* 닫혔다 */ } }

if (seen.length < 10) {
	killWorld();
	cannotRun(`세계를 ${seen.length}번밖에 못 들여다봤다 — 이 상태로 잰 값은 뜻이 없다`);
}

const third = Math.floor(seen.length / 3);
const early = seen.slice(0, third);
const late = seen.slice(-third);
const mean = (rows, pick) => rows.reduce((sum, one) => sum + pick(one), 0) / rows.length;

// ★ 「들고 있는 양」은 <b>톱니</b>다 (실측: 15 → 74 → 150 → 245 → 346 → 33MB). 큰 청소가 돌면
//   뚝 떨어진다 — 그러니 <b>평균</b>을 견주면 톱니의 어느 자리를 봤느냐를 재게 된다(뜻 없는 값).
//   새는지는 <b>골짜기</b>(가장 적을 때)로 본다: 청소하고도 안 내려가면 그게 사는 것이다.
const floorOf = (rows) => Math.min(...rows.map((one) => one.memory));
const memoryEarly = floorOf(early);
const memoryLate = floorOf(late);
const grew = memoryEarly === 0 ? 0 : memoryLate / memoryEarly;
const worstGap = seen.reduce((worst, one) => Math.max(worst, one.gap), 0);
const stayed = seen.every((one) => one.people >= crowd);

const allocatedPerSecond = (seen[seen.length - 1].allocated - seen[0].allocated)
	/ ((seen[seen.length - 1].at - seen[0].at) / 1000);

console.log(`  ⓘ ${Math.round(soakMs / 1000)}초 · 사람 ${crowd}명 — 골짜기 ${memoryEarly.toFixed(0)}MB → ${memoryLate.toFixed(0)}MB`
	+ ` (골짜기 ${grew.toFixed(2)}배) · 새로 담는 속도 ${allocatedPerSecond.toFixed(1)}MB/s`
	+ ` · 가장 벌어진 판 ${worstGap}ms · 들여다본 횟수 ${seen.length}`);

// 「자란다」와 「아직 안 치웠다」는 다른 일이다 — <b>큰 청소(gen2)가 돌았는데도</b> 자라면 그건 새는 것이다.
const gen2Ran = seen[seen.length - 1].gen2 - seen[0].gen2;
console.log(`  ⓘ 큰 청소(gen2) ${gen2Ran}번 · 멈춤 비율 ${seen[seen.length - 1].pause}%`
	+ ` · 들고 있는 양 흐름: ${seen.filter((one, at) => at % Math.ceil(seen.length / 6) === 0).map((one) => one.memory + 'MB').join(' → ')}`);

check('청소하고 나서도 안 자란다 (골짜기가 1.6배 안)', grew > 0 && grew <= MOST_MEMORY_GROWTH,
	`골짜기 ${memoryEarly.toFixed(0)}MB → ${memoryLate.toFixed(0)}MB (${grew.toFixed(2)}배)`);
check(`판 사이가 ${MOST_TICK_GAP_MS}ms 넘게 안 벌어진다`, worstGap > 0 && worstGap <= MOST_TICK_GAP_MS,
	`${worstGap}ms`);
check('도는 내내 사람이 안 떨어졌다', stayed, `가장 적을 때 ${Math.min(...seen.map((one) => one.people))}명`);

killWorld();

if (failures === 0) {
	console.log(`[soak] ✅ ${Math.round(soakMs / 1000)}초를 도는 동안 세계가 안 샌다`);
	process.exit(0);
}

console.log(`\n[soak] RESULT: ${failures}건`);
process.exit(1);
