#!/usr/bin/env node
// wm-stale-sky-test.mjs — <b>앞서 있는 저장 하늘을 세계가 되돌린다</b> (TASK-WM-320).
//
// ★ 무엇이 있었나 (prod 실측 2026-08-13): 세계 둘을 나란히 띄웠더니 east 125일 · west 91일 —
//   <b>34일 어긋난 두 하늘</b>이었다. 하늘은 벽시계에서 유도되는데(WM-266),
//   달력을 앞으로만 미는 셈이라 <b>저장된 값이 앞서 있으면</b> 영영 못 따라잡는다.
//   국경을 넘는 순간 밤이 낮이 되는 그 사고가 <b>세계 파일을 통해</b> 되살아난 것이다.
//
// ★ 왜 관문이 또 필요한가: 고침은 단위 시험이 지킨다(`SkyAgreesTests`). 그런데 그 시험은
//   <b>달력 객체</b>만 본다 — 「세계 파일에 앞선 값이 적혀 있을 때 <b>세계가</b> 어떻게 뜨는지」는
//   아무도 안 봤다. 국경 관문도 못 잡는다(거기 두 세계는 새 파일로 뜬다 — 확인함).
//
// 재는 것: 세계 파일에 <b>200일 앞선</b> 달력을 적어 두고 띄운다 →
//   ① 세계가 뜨나 ② 하늘이 <b>벽시계 쪽으로</b> 돌아오나(앞선 값에 안 굳나)
//
// exit: 0 = 되돌린다 · 1 = 앞선 채로 굳는다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5620);

/** 저장 파일에 심어 둘 「앞선 하늘」 — 벽시계보다 한참 뒤여야 뜻이 있다. */
const AHEAD_DAY = 200;

function cannotRun(message) {
	console.error(`[stale-sky] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-sky-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

// ★ 앞선 하늘을 <b>손으로</b> 적어 둔다 — prod 에서 실제로 그런 파일이 있었다.
//   (한 철 28일이라 200일 = 7철 남짓 — 벽시계 하늘보다 한참 앞이다.)
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-sky-')), 'world.json');
writeFileSync(worldFile, JSON.stringify({
	buildings: [],
	year: 1 + Math.floor(AHEAD_DAY / (28 * 4)),
	season: Math.floor((AHEAD_DAY % (28 * 4)) / 28),
	day: (AHEAD_DAY % 28) + 1,
	hour: 6,
	minute: 0,
	people: [],
}), 'utf8');

const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
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

const health = () => fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

let up = false;
{
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}
}

if (up === false) {
	killWorld();
	cannotRun('앞선 하늘이 적힌 파일로는 세계가 아예 안 떴다');
}

// 방송 루프가 몇 판 돌 시간을 준다 — 맞추는 일은 그 루프에서 일어난다.
await wait(3000);
const now = await health();

// 같은 앱을 <b>빈 파일</b>로 한 번 더 띄워 「벽시계 하늘」이 몇 일인지 얻는다(견줄 자).
const freshFile = join(mkdtempSync(join(tmpdir(), 'wm-sky-fresh-')), 'world.json');
const fresh = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port + 1}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: freshFile },
	stdio: 'ignore',
});

let freshUp = false;
{
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port + 1}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { freshUp = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}
}

const wall = freshUp
	? await fetch(`http://127.0.0.1:${port + 1}/health`, { headers: { connection: 'close' } }).then((one) => one.json())
	: null;

try {
	if (process.platform === 'win32') execSync(`taskkill /PID ${fresh.pid} /F /T`, { stdio: 'ignore' });
	else fresh.kill('SIGKILL');
} catch { /* 이미 죽었다 */ }

killWorld();

if (wall === null) cannotRun('견줄 세계(빈 파일)가 안 떴다 — 이 상태로는 「돌아왔나」를 못 잰다');

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 파일에 심은 하늘 ${AHEAD_DAY}일 · 뜬 뒤 ${now.day}일 · 벽시계 하늘 ${wall.day}일`);

check('앞선 하늘이 적힌 파일로도 세계가 뜬다', now.ok === true, `하루 ${now.day}일째`);
check('하늘이 <b>벽시계 쪽으로</b> 돌아온다', Math.abs(now.day - wall.day) <= 1,
	Math.abs(now.day - wall.day) <= 1
		? `${now.day}일 ≒ ${wall.day}일`
		: `${now.day}일 — 벽시계는 ${wall.day}일인데 앞선 값에 굳었다(국경을 넘으면 밤이 낮이 된다)`);

if (failures === 0) {
	console.log('[stale-sky] ✅ 앞선 저장 하늘을 세계가 되돌린다');
	process.exit(0);
}

console.log(`\n[stale-sky] RESULT: ${failures}건`);
process.exit(1);
