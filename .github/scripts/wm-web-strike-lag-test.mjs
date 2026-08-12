#!/usr/bin/env node
// wm-web-strike-lag-test.mjs — <b>나쁜 회선에서 때리면 맞나</b> (TASK-WM-262).
//
// ★ 왜: 싸움 판정(WM-251)은 세계의 <b>지금</b> 자리로 본다. 그런데 창이 보는 자리는
//   회선만큼 <b>낡았다</b> — 왕복 200ms 면 상대는 이미 0.6m 옆에 가 있다.
//   그래서 사람은 「분명히 붙어서 쳤는데 안 맞는다」를 겪는다. 이 자리는 한 번도 안 재봤다
//   (지금까지의 싸움 측정은 전부 지연 0 의 loopback 이다).
//
// 재는 것: 같은 싸움을 <b>두 번</b> 한다 — 곧은 회선 / 나쁜 회선(왕복 200ms·흔들림 20ms).
//   때리는 쪽은 <b>제가 본 자리</b>로만 판단한다(사람이 하는 그대로).
//   ① 몇 번 휘둘렀나 ② 몇 번 맞았나 ③ 「너무 멀다」로 몇 번 잘렸나
//
// 필요한 것: .NET 8. (창은 안 띄운다 — 이 자리는 판정 문제다.)
// exit: 0 = 맞는다 · 1 = 안 맞는다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5410);
const linePort = port + 1;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-strikelag-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const REACH = 2;              // StrikeRule.REACH — 세계가 보는 손 닿는 거리
const COOLDOWN_MS = 600;      // StrikeRule.COOLDOWN_MS
const WALK_SPEED = 3;         // walk.mjs WALK_SPEED
const SWING_FOR_MS = 12000;   // 한 판을 재는 시간

/** 사람이 「분명히 맞을 자리」라고 볼 거리 — 반경보다 넉넉히 안쪽에서만 휘두른다. */
const SURE_HIT = REACH * 0.6;

function cannotRun(message) {
	console.error(`[strike-lag] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-strikelag-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

let world = null;
function startWorld() {
	world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: worldFile },
		stdio: 'ignore',
	});
}

function killWorld() {
	if (world === null) return;
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
	world = null;
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

async function waitHealthy(milliseconds) {
	const until = Date.now() + milliseconds;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) return true;
		} catch { /* 아직 */ }
		await wait(300);
	}

	return false;
}

/** 세계에 붙은 창 하나 — <b>제가 받은 그림</b>만 안다(사람과 같은 눈). */
function openWindow(where) {
	const one = { id: 0, seen: new Map(), hurts: [], socket: null, ready: false };
	const socket = new WebSocket(where);
	one.socket = socket;

	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'welcome' && said.id) { one.id = said.id; one.ready = true; }
		if (said.type === 'hurt') one.hurts.push(said);
		if (said.type === 'world' && Array.isArray(said.dolls)) {
			for (const doll of said.dolls) {
				if (typeof doll.x !== 'number' || typeof doll.z !== 'number') continue;

				one.seen.set(doll.id, { x: doll.x, z: doll.z });
			}
		}
	};

	return one;
}

const tell = (one, message) => {
	try { one.socket.send(JSON.stringify(message)); } catch { /* 끊겼다 */ }
};

/**
 * 한 판 — 상대는 좌우로 걷고, 때리는 쪽은 <b>제가 본 자리</b>가 확실히 닿을 때만 휘두른다.
 * @returns {{swings:number, hits:number}}
 */
async function fight(where, label, swingAt = SURE_HIT, fleeing = false) {
	const target = openWindow(where);
	const attacker = openWindow(where);

	const until = Date.now() + 8000;
	while (Date.now() < until && (target.ready === false || attacker.ready === false)) await wait(100);
	if (target.ready === false || attacker.ready === false) {
		cannotRun(`${label}: 창이 세계에 못 붙었다`);
	}

	let swings = 0;
	const started = Date.now();
	let lastSwing = 0;
	let walkingRight = true;
	let turnedAt = Date.now();

	while (Date.now() - started < SWING_FOR_MS) {
		// 상대는 좌우로 걷는다 — 1.2초마다 방향을 바꾼다(제자리 걸음이 아니다).
		if (Date.now() - turnedAt > 1200) { walkingRight = walkingRight === false; turnedAt = Date.now(); }
		// 도망가는 쪽은 <b>조금 느리다</b>(같은 속도면 영영 못 따라잡아 표본이 안 쌓인다) —
		// 쫓는 쪽이 가장자리에 붙어 계속 휘두르게 되는, 실제로 제일 흔한 그림이다.
		tell(target, {
			type: 'move',
			x: (fleeing ? 0.6 : (walkingRight ? 1 : -1)) * WALK_SPEED * 0.05,
			z: 0,
		});

		// 때리는 쪽은 <b>제가 본</b> 상대 자리로 붙는다.
		const mine = attacker.seen.get(attacker.id);
		const his = attacker.seen.get(target.id);
		if (mine && his) {
			const away = Math.hypot(his.x - mine.x, his.z - mine.z);
			if (away > swingAt) {
				const step = Math.min(WALK_SPEED * 0.05, Math.max(0.01, away - swingAt * 0.5));
				tell(attacker, { type: 'move', x: (his.x - mine.x) / away * step, z: (his.z - mine.z) / away * step });
			} else if (Date.now() - lastSwing >= COOLDOWN_MS + 60) {
				// 사람 눈에는 <b>확실히</b> 닿는 거리다 — 여기서 안 맞으면 그건 회선 탓이다.
				lastSwing = Date.now();
				swings += 1;
				tell(attacker, { type: 'strike', targetId: target.id });
			}
		}

		await wait(50);
	}

	// 마지막 답이 올 틈 (나쁜 회선은 왕복이 걸린다).
	await wait(ONE_WAY_MS * 4 + 400);

	const hits = attacker.hurts.filter((one) => one.dollId === target.id).length;
	target.socket.close();
	attacker.socket.close();
	await wait(300);

	console.log(`  ⓘ ${label}: 휘두름 ${swings}번 · 맞음 ${hits}번`
		+ ` · 맞은 비율 ${swings === 0 ? 0 : Math.round((hits / swings) * 100)}%`);

	return { swings, hits };
}

startWorld();
if (await waitHealthy(120000) === false) {
	killWorld();
	cannotRun('세계가 안 떴다');
}

const straight = await fight(`ws://127.0.0.1:${port}/ws`, '곧은 회선');
const chaseStraight = await fight(`ws://127.0.0.1:${port}/ws`, '곧은 회선 · 도망가는 상대를 쫓으며', REACH * 0.95, true);

const line = openBadLine({ listenPort: linePort, targetPort: port, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS });
await line.listen();
const rough = await fight(`ws://127.0.0.1:${linePort}/ws`, `나쁜 회선(왕복 ${ONE_WAY_MS * 2}ms)`);
const chaseRough = await fight(`ws://127.0.0.1:${linePort}/ws`,
	`나쁜 회선 · 도망가는 상대를 쫓으며`, REACH * 0.95, true);

await line.close();
killWorld();

check('곧은 회선에서는 휘두르면 맞는다 (재는 자가 성한지부터)', straight.swings >= 5 && straight.hits >= straight.swings * 0.9,
	`${straight.hits}/${straight.swings}`);

const roughRate = rough.swings === 0 ? 0 : rough.hits / rough.swings;
check('나쁜 회선에서도 붙어서 친 것은 맞는다 (열에 아홉)', rough.swings >= 5 && roughRate >= 0.9,
	`${rough.hits}/${rough.swings} = ${Math.round(roughRate * 100)}%`);

// ★ 진짜 물음은 <b>가장자리</b>다 — 도망가는 상대를 쫓으며 「닿는다」고 보이는 순간 치는 것.
//   낡은 눈으로 가장자리를 치면, 세계의 지금 자리는 이미 반경 밖이다.
const chaseStraightRate = chaseStraight.swings === 0 ? 0 : chaseStraight.hits / chaseStraight.swings;
const chaseRoughRate = chaseRough.swings === 0 ? 0 : chaseRough.hits / chaseRough.swings;

check('쫓아가며 가장자리를 쳐도 곧은 회선에서는 맞는다', chaseStraight.swings >= 5 && chaseStraightRate >= 0.9,
	`${chaseStraight.hits}/${chaseStraight.swings} = ${Math.round(chaseStraightRate * 100)}%`);
check('쫓아가며 가장자리를 쳐도 나쁜 회선에서 맞는다 (곧은 회선의 8할 이상)',
	chaseRough.swings >= 5 && chaseRoughRate >= chaseStraightRate * 0.8,
	`${chaseRough.hits}/${chaseRough.swings} = ${Math.round(chaseRoughRate * 100)}%`
	+ ` · 곧은 회선 ${Math.round(chaseStraightRate * 100)}%`);

if (failures === 0) {
	console.log('[strike-lag] ✅ 왕복 200ms 에서도 붙어서 친 것은 맞는다');
	process.exit(0);
}

console.log(`\n[strike-lag] RESULT: ${failures}건`);
process.exit(1);
