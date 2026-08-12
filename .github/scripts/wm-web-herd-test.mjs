#!/usr/bin/env node
// wm-web-herd-test.mjs — 배포 뒤 <b>우르르 다시 붙기</b>를 세계가 받아 내나 (TASK-WM-247).
//
// ★ 왜: prod 는 push 할 때마다 세계를 껐다 켠다. 그 순간 <b>붙어 있던 모두</b>가 동시에 끊기고
//   동시에 다시 붙는다 — 평소에는 한 명씩 오던 일이 한꺼번에 온다.
//   들어올 때가 제일 비싼 자리다(첫 전체 그림 + 낱말표). 그게 한꺼번에 몰리면
//   가장 먼저 무너지는 곳이 여기다. 그런데 이 자리는 <b>한 번도 안 재봤다</b>.
//
// 재는 것: 사람 여럿을 붙여 놓고 세계를 죽였다 살린 뒤
//   ① 모두 돌아오나 ② 얼마나 걸리나 ③ 돌아온 뒤 세계를 제대로 받나(빈 세계가 아니라)
//
// 필요한 것: .NET 8. (창은 안 띄운다 — 이 자리는 소켓 문제다.)
// exit: 0 = 받아 낸다 · 1 = 못 받아 낸다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5402);
const herd = Number(process.env.WM_HERD || 40);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-herd-')), 'world.json');

/*
 * 기준.
 * 돌아오기: 창의 다시 붙기 규칙은 0.5초에서 시작해 10초까지 늘어난다(link.mjs).
 *   여럿이 한꺼번에 와도 그 안에는 다 돌아와야 한다 — 안 그러면 사람이 새로고침을 누른다.
 * 받는 것: 돌아온 뒤 <b>전체 그림</b>을 받아야 한다. 안 그러면 그 창의 세계는 반쪽이다(WM-230).
 */
const MUST_RETURN_WITHIN_MS = 20000;

function cannotRun(message) {
	console.error(`[web-herd] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-herd-app-')), 'app');
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

/**
 * 창 하나 — 진짜 창처럼 <b>스스로 다시 붙는다</b>(0.5초에서 시작해 두 배씩, 10초 상한).
 * 세계가 준 열쇠를 들고 돌아가므로 「같은 사람」으로 돌아온다.
 */
function openWindow(number) {
	const one = {
		number,
		secret: '',
		joins: 0,
		gotWorld: 0,
		gotFullWorld: 0,
		backAt: -1,
		socket: null,
		waitMs: 500,
		alive: true,
	};

	const connect = () => {
		if (one.alive === false) return;

		const socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
		one.socket = socket;
		socket.onopen = () => {
			one.waitMs = 500;
			socket.send(JSON.stringify({ type: 'hello', secret: one.secret }));
		};

		socket.onmessage = (event) => {
			let said;
			try { said = JSON.parse(event.data); } catch { return; }

			if (said.type === 'welcome') {
				one.joins += 1;
				if (said.secret) one.secret = said.secret;
				if (one.backAt < 0) one.backAt = Date.now();
			}

			if (said.type === 'world') {
				one.gotWorld += 1;
				if (said.changed !== true) one.gotFullWorld += 1;
			}
		};

		socket.onerror = () => { /* onclose 가 다시 붙인다 */ };
		socket.onclose = () => {
			if (one.alive === false) return;

			setTimeout(connect, one.waitMs);
			one.waitMs = Math.min(10000, one.waitMs * 2);
		};
	};

	connect();
	return one;
}

startWorld();
if (await waitHealthy(120000) === false) {
	killWorld();
	cannotRun('세계가 안 떴다');
}

const windows = [];
for (let i = 0; i < herd; i += 1) windows.push(openWindow(i));
await wait(4000);

const joined = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((r) => r.json());
check(`${herd}명이 세계에 있다`, joined.people >= herd, `세계가 세는 사람 ${joined.people}명`);

// ⚠ 여기까지의 셈은 <b>전부</b> 잊는다 — 돌아온 횟수까지 지워야 한다.
//   안 지우면 붙는 도중에 한 번 튄 창이 이미 「돌아왔다」로 세어져 <b>거짓 초록</b>이 된다
//   (첫 판에 「가장 늦은 사람 0ms」가 나왔다 — 아무도 안 돌아왔는데 통과할 수 있었다).
for (const one of windows) {
	one.gotWorld = 0;
	one.gotFullWorld = 0;
	one.joins = 0;
	one.backAt = -1;
}

// ── 배포가 하는 일: 세계를 껐다 켠다 ─────────────────────────────────
killWorld();
const wentDownAt = Date.now();
await wait(1000);
startWorld();

if (await waitHealthy(60000) === false) {
	for (const one of windows) one.alive = false;
	cannotRun('다시 켠 세계가 안 떴다');
}

await wait(MUST_RETURN_WITHIN_MS);

const back = windows.filter((one) => one.joins > 0);
const gotFull = windows.filter((one) => one.gotFullWorld > 0);
const slowest = back.reduce((worst, one) => Math.max(worst, one.backAt - wentDownAt), 0);
const after = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((r) => r.json());

check('모두가 스스로 돌아왔다', back.length === herd, `${back.length}/${herd}명`);
check('세계도 그만큼 세고 있다', after.people >= herd, `세계가 세는 사람 ${after.people}명`);
check('돌아온 뒤 <b>전체 그림</b>을 받았다 (반쪽 세계가 아니다)', gotFull.length === herd,
	`${gotFull.length}/${herd}명`);
check(`가장 늦은 사람도 ${MUST_RETURN_WITHIN_MS / 1000}초 안에 돌아왔다`,
	back.length === herd && slowest <= MUST_RETURN_WITHIN_MS, `가장 늦은 사람 ${slowest}ms`);

console.log(`  ⓘ 사람 ${herd}명 · 세계가 꺼진 순간부터 가장 늦은 복귀 ${slowest}ms`
	+ ` · 돌아온 뒤 받은 판 ${windows.reduce((sum, one) => sum + one.gotWorld, 0)}장`);

for (const one of windows) {
	one.alive = false;
	try { one.socket.close(); } catch { /* 이미 닫혔다 */ }
}

await wait(500);
killWorld();

if (failures === 0) {
	console.log(`[web-herd] ✅ 세계를 껐다 켜도 ${herd}명이 스스로 다 돌아온다`);
	process.exit(0);
}

console.log(`\n[web-herd] RESULT: ${failures}건`);
process.exit(1);
