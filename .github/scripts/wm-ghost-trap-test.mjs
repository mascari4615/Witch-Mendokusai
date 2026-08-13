#!/usr/bin/env node
// wm-ghost-trap-test.mjs — <b>재현 안 되는 유령을 잡는 덫이 실제로 작동하나</b> (TASK-WM-329).
//
// ★ 왜: 「그 사람 나갔다」(gone)는 한 번밖에 안 간다. 그 한 번을 놓친 창은 그 사람을 영영
//   유령으로 그린다 — 오류도 없고 아무도 모른다. 실제로 한 번 20초 넘게 그랬는데 26판을 돌려도
//   재현이 안 됐다(WM-314). 원인을 못 잡으면 <b>물어보게 하면 된다</b>: 창이 이따금
//   「내가 이 사람들을 그리고 있다」(roster)를 보내고, 세계가 「그건 없다」(ghosts)고 답한다.
//
// ⚠ 이 관문은 유령을 <b>재현하지 않는다</b>(못 한다). 대신 덫 자체를 시험한다 —
//   덫이 조용히 고장 나면 다음에 유령이 와도 또 무(無)로 지나간다. 덫은 잡히는지 확인해야 덫이다.
//
// 재는 것: ① 있는 사람만 대면 조용하다 ② 없는 번호를 대면 그 번호를 되돌려 준다
//          ③ 세계가 그 일을 센다(`/health` 의 ghostsFound)
//
// exit: 0 = 덫이 작동한다 · 1 = 덫이 고장났다 · 2 = 못 돌림
//
// [빨강-확인] 없는 번호를 대면 되돌려주는 길을 실제로 태웠다 — ghostsFound 0 → 1

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5372);

/** 세계에 있을 리 없는 번호 — 사람 번호는 1부터 차례로 붙는다. */
const NEVER_EXISTED = 999001;

function cannotRun(message) {
	console.error(`[유령덫] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let WebSocket;
try {
	WebSocket = (await import('node:worker_threads'), globalThis.WebSocket);
	if (!WebSocket) throw new Error('이 node 에는 WebSocket 이 없다');
} catch (error) {
	cannotRun(`WebSocket 이 없다 — ${error.message}`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-ghost-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-ghost-')), 'world.json');
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

const socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
const said = [];
let myId = -1;

socket.addEventListener('message', (event) => {
	let message;
	try { message = JSON.parse(String(event.data)); } catch { return; }
	said.push(message);
	if (message.type === 'welcome' && typeof message.id === 'number') myId = message.id;
});

const opened = await new Promise((done) => {
	socket.addEventListener('open', () => done(true));
	socket.addEventListener('error', () => done(false));
	setTimeout(() => done(false), 20000);
});

if (opened === false) {
	killWorld();
	cannotRun('세계에 못 붙었다');
}

socket.send(JSON.stringify({ type: 'hello' }));

// ⚠ 재기 전에 <b>잴 것이 왔는지</b>부터 (domain-wm.md § 관문 규율 ②) — 내 번호를 모르면
//   「있는 사람만 댔다」를 만들 수 없어서, 조용한 것이 초록인지 못 붙은 것인지 구분이 안 된다.
{
	const until = Date.now() + 20000;
	while (Date.now() < until && myId < 0) await wait(100);
	if (myId < 0) {
		socket.close();
		killWorld();
		cannotRun('세계가 내 번호를 안 알려줬다 — 이 상태로는 덫을 못 시험한다');
	}
}

// ① 있는 사람만 대면 조용해야 한다.
said.length = 0;
socket.send(JSON.stringify({ type: 'roster', ids: [myId] }));
await wait(2000);
const quiet = said.some((one) => one.type === 'ghosts') === false;

// ② 없는 번호를 대면 그 번호가 돌아와야 한다.
said.length = 0;
socket.send(JSON.stringify({ type: 'roster', ids: [myId, NEVER_EXISTED] }));

let answer = null;
{
	const until = Date.now() + 10000;
	while (Date.now() < until && answer === null) {
		answer = said.find((one) => one.type === 'ghosts') ?? null;
		await wait(100);
	}
}

const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } })
	.then((one) => one.json());

socket.close();
killWorld();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

check('있는 사람만 대면 아무 말도 안 한다', quiet,
	quiet ? '조용했다 (할 말 없으면 안 보낸다)' : '없는 사람이 없는데도 뭔가 말했다');
check('없는 사람을 대면 그 번호를 돌려준다', answer !== null && (answer.ids || []).includes(NEVER_EXISTED),
	answer === null ? '10초를 기다려도 답이 없었다' : `돌아온 번호 ${JSON.stringify(answer.ids)}`);
check('내 번호는 안 돌려준다 — 산 사람을 지우면 안 된다', answer !== null && (answer.ids || []).includes(myId) === false,
	answer === null ? '답이 없었다' : `내 번호 ${myId} 는 ${((answer.ids || []).includes(myId)) ? '섞여 있었다' : '안 섞였다'}`);
check('세계가 그 일을 센다', health.ghostsFound >= 1, `ghostsFound = ${health.ghostsFound}`);

if (failures === 0) {
	console.log('[유령덫] ✅ 덫이 살아 있다 — 유령이 다시 오면 지워지고, 그 사실이 숫자로 남는다');
	process.exit(0);
}

console.log(`\n[유령덫] RESULT: ${failures}건`);
process.exit(1);
