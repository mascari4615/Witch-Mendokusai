#!/usr/bin/env node
// wm-badline-strike-fairness-test.mjs — <b>나쁜 회선이라고 계속 헛치지는 않는다</b> (TASK-WM-303).
//
// ★ 왜: 때리기 판정은 <b>지금</b> 자리로 한다. 그런데 회선이 먼 사람이 보는 화면은 그만큼 옛것이고,
//   그 손짓이 세계에 닿기까지 또 그만큼 걸린다 — 그 사이 상대는 움직인다.
//   그래서 세계가 되감아 주지 않으면 <b>회선이 나쁜 사람만</b> 계속 헛친다.
//
// ⚠ <b>솔직히</b>: 처음엔 「손해가 회선에 비례해 자란다」고 읽었다(쓰러뜨리는 데 곧은 46번 · 250ms 70번).
//   그런데 그건 <b>마구 휘두를 때</b>의 값이었다 — 그 대부분은 「아직 팔이 안 돌아왔다」로 물린 것이라
//   재던 것은 거리 판정이 아니라 물림 비율이었다. 팔이 돌아왔을 때만 휘두르게 하고 다시 재니
//   되감기를 <b>꺼도</b> 나쁜 회선이 곧은 회선의 104% 를 맞혔다 — 이 거리(2m)·이 속도(3m/s)에서는
//   원래 거의 공평했다는 뜻이다. 되감기(WM-303)는 그대로 두되(옳고, 손해를 안 준다),
//   이 관문은 <b>공평함이 깨지는지 지키는 파수꾼</b>이지 「고쳤다」의 증거가 아니다.
//
// 재는 법: <b>같은 세계 안에</b> 짝을 둘 둔다 — 곧은 회선 짝 · 나쁜 회선 짝. 표적은 앞뒤로 오가고,
//   때리는 이는 <b>자기 화면</b>이 닿는다고 할 때만 휘두른다. 한 사람을 쓰러뜨릴 때까지(10대)
//   든 손짓을 견준다.
//
// ⚠ 이 관문이 걸려 넘어진 자리 둘 (재는 자의 고장 — domain-wm.md § 관문 규율):
//   ① 맞음을 `byDollId` 로 셌다 — 세계는 `by` 라 부른다(0대로 보였다).
//   ② 쓰러지면 <b>원점</b>에 다시 선다. 한 짝만 원점 근처에 두면 그 짝만 계속 싸운다 —
//      두 짝을 원점에서 <b>같은 거리</b>로 떼어야 진짜 수가 나온다.
//
// 문턱은 <b>이 판의 곧은 회선</b>과의 견줌이다(절대 숫자 X — 느린 기계에서 태생적 빨강이 된다).
// exit: 0 = 공평 · 1 = 나쁜 회선이 손해 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5530);
const linePort = worldPort + 1;

/** 일부러 <b>먼</b> 회선을 쓴다 — 100ms 에서는 손해가 작아 눈에 잘 안 띈다(실측). */
const ONE_WAY_MS = Number(process.env.WM_STRIKE_LATENCY_MS || 250);
const JITTER_MS = 30;
const LOSS_PERCENT = 2;

/** 되감기를 껐을 때 실측한 값 (빨강 줄에 같이 적는다) — 이 거리·속도에서는 끄고도 공평했다. */
const BEFORE_REWIND = 1.04;

/** 나쁜 회선이 곧은 회선의 최소 이만큼은 맞혀야 한다 (같은 판에서 잰 값끼리의 견줌 — 기계 속도와 무관). */
const AS_WELL_AS_LEAST = 0.8;

function cannotRun(message) {
	console.error(`[strike-fairness] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-fair-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-fair-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
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

	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다');
	}
}

const badLine = openBadLine({
	listenPort: linePort, targetPort: worldPort,
	latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS, lossPercent: LOSS_PERCENT,
});
await badLine.listen();

function joinWorld(port, label) {
	const one = { label, id: null, x: 0, z: 0, seen: new Map(), hits: 0, swings: 0, ack: 0 };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래 칸이 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'welcome') one.id = said.id;
		if (said.type === 'world') {
			// 세계의 도장을 되돌려 준다 — 세계는 이걸로 이 사람의 회선을 잰다 (TASK-WM-303).
			if (typeof said.at === 'number') one.ack = said.at;
			if (Array.isArray(said.dolls)) {
				for (const doll of said.dolls) {
					one.seen.set(doll.id, doll);
					if (doll.id === one.id) { one.x = doll.x; one.z = doll.z; }
				}
			}
		}

		if (said.type === 'hurt' && said.by === one.id) {
			one.hits += 1;
			if (said.down && one.downAt === undefined) {
				one.downAt = Date.now();
				one.swingsToDown = one.swings;
			}
		}
	};

	return one;
}

const send = (one, message) => {
	if (one.socket.readyState === 1)
		one.socket.send(JSON.stringify(one.ack ? { ...message, ack: one.ack } : message));
};

const straightHitter = joinWorld(worldPort, '곧은 회선');
const straightTarget = joinWorld(worldPort, '표적(곧은)');
const badHitter = joinWorld(linePort, '나쁜 회선');
const badTarget = joinWorld(linePort, '표적(나쁜)');
await wait(3000);

if (straightHitter.id === null || badHitter.id === null) {
	badLine.close();
	killWorld();
	cannotRun('네 사람이 다 못 들어갔다');
}

// 두 짝을 <b>원점에서 같은 거리</b>로 떼어 둔다 (쓰러지면 원점에 다시 서기 때문이다).
for (let i = 0; i < 260; i += 1) {
	send(badHitter, { type: 'move', x: 0.3, z: 0, seq: i });
	send(badTarget, { type: 'move', x: 0.3, z: 0, seq: i });
	send(straightHitter, { type: 'move', x: -0.3, z: 0, seq: i });
	send(straightTarget, { type: 'move', x: -0.3, z: 0, seq: i });
	await wait(50);
}

await wait(2000);
const apart = Math.hypot(badHitter.x - straightHitter.x, badHitter.z - straightHitter.z);
if (apart < 40) {
	badLine.close();
	killWorld();
	cannotRun(`두 짝이 안 떨어졌다 (${apart.toFixed(1)}m) — 서로의 싸움에 끼어든다`);
}

const pairs = [[straightHitter, straightTarget], [badHitter, badTarget]];
let step = 1000;
const walking = setInterval(() => {
	step += 1;
	const way = Math.floor(step / 20) % 2 === 0 ? 0.15 : -0.15;
	send(straightTarget, { type: 'move', x: way, z: 0, seq: step });
	send(badTarget, { type: 'move', x: way, z: 0, seq: step });
}, 50);

// ★ <b>팔이 돌아왔을 때만</b> 휘두른다 (650ms > 세계의 600ms). 마구 휘두르면 거의 모든 손짓이
//   「아직 팔이 안 돌아왔다」로 물려서, 재는 값이 <b>거리 판정</b>이 아니라 물림 비율이 된다
//   (실측: 그렇게 쟀더니 판마다 1.21~1.52배로 널뛰어 되감기 유무를 못 갈랐다).
const swinging = setInterval(() => {
	for (const [hitter, target] of pairs) {
		const asSeen = hitter.seen.get(target.id);
		if (asSeen === undefined) continue;

		const far = Math.hypot(asSeen.x - hitter.x, asSeen.z - hitter.z);
		if (far > 2) continue;

		hitter.swings += 1;
		send(hitter, { type: 'strike', targetId: target.id });
	}
}, 650);

// ⚠ <b>표본이 찰 때까지</b> 싸운다 (실측 2026-08-13): 2코어 CI 에서 40초로는 나쁜 회선 쪽이
//   14번밖에 못 휘둘러 CANNOT-RUN 이 났다(내 기계에서는 56번). 시간을 못 박으면 느린 기계에서
//   태생적으로 못 재는 관문이 된다 — 그래서 <b>휘두른 횟수</b>로 끝을 정하고, 위로만 시간을 막는다.
const FIGHT_UNTIL_SWINGS = 25;
const FIGHT_MOST_MS = 150000;
{
	const until = Date.now() + FIGHT_MOST_MS;
	while (Date.now() < until) {
		if (straightHitter.swings >= FIGHT_UNTIL_SWINGS && badHitter.swings >= FIGHT_UNTIL_SWINGS) break;

		await wait(500);
	}
}

clearInterval(walking);
clearInterval(swinging);
await wait(1500);

badLine.close();
killWorld();

for (const [hitter] of pairs) {
	hitter.landed = hitter.swings === 0 ? 0 : hitter.hits / hitter.swings;
	console.log(`  ⓘ ${hitter.label} — 휘두름 ${hitter.swings} · 맞음 ${hitter.hits}`
		+ ` (${(hitter.landed * 100).toFixed(0)}%)`);
}

if (straightHitter.swings < 20 || badHitter.swings < 20)
	cannotRun(`휘두른 횟수가 너무 적다 (곧은 ${straightHitter.swings} · 나쁜 ${badHitter.swings}) — 이 표본으로는 못 가른다`);

if (straightHitter.landed <= 0)
	cannotRun('곧은 회선조차 한 대도 못 맞혔다 — 재는 자가 고장 난 것이다');

// ★ 견줌에는 <b>바닥</b>이 있어야 한다 (2026-08-14, 무리 관문에서 배웠다): 둘이 <b>같이</b> 나빠지면
//   비율은 그대로라 초록이 된다. 「나쁜 회선이 곧은 회선만큼 맞힌다」가 뜻을 가지려면
//   곧은 회선이 <b>실제로 맞히고</b> 있어야 한다.
//   [문턱-사유] (c) 제품 상수 — <b>붕괴만</b> 잡는 바닥이다. 이 기계에서는 38~40% 라 20% 로 뒀더니
//     2코어 CI 에서 <b>4%</b> 로 빨갰다(같은 코드) — 맞는 비율은 봇이 얼마나 빨리 붙느냐라 <b>기계 몫</b>이다.
//     그래서 「거의 0」만 잡는 2% 로 낮춘다. 더 센 바닥은 <b>사거리 안에 있던 시간</b>을 같이 재야 한다(다음).
//   ⚠ 밟아 보려 했으나 <b>그 띠가 좁다</b>(2026-08-14): 사거리를 0.05m 로 줄이면 0% → 위의
//     CANNOT-RUN 이 먼저 나고, 0.7m 로 줄여도 봇이 워낙 붙어 있어 37% 가 나온다.
//     그래서 이 바닥은 「0% 는 아닌데 2% 도 안 되는」 좁은 띠를 지킨다 — 못 밟아 본 채로 둔다(정직하게 적는다).
const LANDS_AT_LEAST = 0.02;
if (straightHitter.landed < LANDS_AT_LEAST) {
	console.log(`  ❌ 곧은 회선이 ${(straightHitter.landed * 100).toFixed(0)}% 밖에 못 맞혔다`
		+ ` (바닥 ${(LANDS_AT_LEAST * 100).toFixed(0)}%) — 둘이 같이 나빠지면 비율은 그대로다`);
	console.log('[strike-fairness] RESULT: 붙어서 휘둘러도 안 맞는다 — 비율보다 이게 먼저다');
	process.exit(1);
}

const asWellAs = badHitter.landed / straightHitter.landed;
console.log(`  ⓘ 회선 ${ONE_WAY_MS}ms · 유실 ${LOSS_PERCENT}% — 나쁜 회선이 곧은 회선의 ${(asWellAs * 100).toFixed(0)}% 만큼 맞혔다`
	+ ` (되감기 없으면 실측 ${(BEFORE_REWIND * 100).toFixed(0)}% · 한도 ${(AS_WELL_AS_LEAST * 100).toFixed(0)}%)`);

if (asWellAs >= AS_WELL_AS_LEAST) {
	console.log('[strike-fairness] ✅ 나쁜 회선이라고 계속 헛치지 않는다');
	process.exit(0);
}

console.log(`
[strike-fairness] RESULT: 나쁜 회선이 곧은 회선의 ${(asWellAs * 100).toFixed(0)}% 밖에 못 맞혔다 —`
	+ ' 세계가 <b>때린 사람이 보고 있던 순간</b>으로 되감아 판정하는지 보라 (LineTime · PastPlaces).');
process.exit(1);
