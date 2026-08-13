#!/usr/bin/env node
// wm-save-failure-test.mjs — <b>기억을 못 적으면 그 사실이 밖에서 보인다</b> (TASK-WM-311).
//
// ★ 무엇이 있었나: 세계는 「그거 했다」고 답하고(WM-305) 0.3초 안에 디스크로 내린다(WM-310).
//   그런데 <b>디스크가 안 받으면</b> 어떻게 되나 — `store.TrySave(...)` 의 답을 <b>아무도 안 봤다</b>.
//   콘솔에 한 줄 찍고 세계는 계속 「했다」고 답한다. 사람도, /health 도, 장부도 모른다.
//   죽는 순간 통째로 사라져야 알게 된다 — 그게 무음 실패다.
//
// ★ 고친 자리: 저장의 결과를 세어 `/health` 에 내놓는다(savesDone · savesFailed ·
//   lastSaveAgoMs · lastSaveError) — 그리고 10분 장부에도 실패 수를 남긴다.
//
// 재는 것: 세계 파일 자리를 <b>디렉터리</b>로 막아 저장을 반드시 실패하게 만든 뒤
//   ① 세계가 그래도 도나(멈추면 그게 더 나쁘다) ② 실패가 /health 에 <b>보이나</b>
//   ③ 이유가 비어 있지 않나.
//
// exit: 0 = 실패가 보인다 · 1 = 무음 실패 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5590);

function cannotRun(message) {
	console.error(`[save-failure] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-savefail-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

// ★ 저장을 <b>반드시</b> 실패시키는 법: 세계 파일이 있어야 할 자리를 <b>디렉터리</b>로 채운다.
//   (권한을 만지면 CI 계정마다 결과가 달라진다 — 이 방법은 어디서나 똑같이 막힌다.)
const home = mkdtempSync(join(tmpdir(), 'wm-savefail-'));
const worldFile = join(home, 'world.json');
mkdirSync(worldFile);

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

{
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}

	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다 — 저장이 막혔다고 세계가 아예 안 서면 그것부터 고쳐야 한다');
	}
}

// 사람이 하나 들어가 뭔가 바꾼다 — 그래야 저장이 시도된다.
const me = new WebSocket(`ws://127.0.0.1:${port}/ws`);
me.onopen = () => me.send(JSON.stringify({ type: 'hello', secret: '' }));
me.onerror = () => { /* 아래가 잡는다 */ };

let mine = null;
let field = [];
me.onmessage = (event) => {
	try {
		const said = JSON.parse(event.data);
		if (said.type === 'welcome') mine = said.id;
		if (said.type === 'world' && Array.isArray(said.gatherables) && said.gatherables.length > 0) field = said.gatherables;
	} catch { /* 우리 말이 아니다 */ }
};

await wait(3000);
if (mine === null) {
	killWorld();
	cannotRun('세계에 못 들어갔다');
}

// 줍기를 몇 번 시도한다(닿든 안 닿든 세계는 「바뀔 일」을 겪는다) + 걸어서 dirty 를 만든다.
for (let i = 0; i < 60; i += 1) {
	if (me.readyState === 1) me.send(JSON.stringify({ type: 'move', x: 0.15, z: 0, seq: i }));
	if (i % 20 === 10 && field.length > 0 && me.readyState === 1) {
		me.send(JSON.stringify({ type: 'gather', nodeId: field[0].id, did: 500 + i }));
	}

	await wait(50);
}

// 느긋한 저장(5초)까지 한 판 지나가게 둔다.
await wait(7000);

const now = await health();
killWorld();

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 저장 막힌 채 — 성공 ${now.savesDone} · 실패 ${now.savesFailed}`
	+ ` · 마지막 성공 ${now.lastSaveAgoMs}ms 전 · 이유 「${String(now.lastSaveError || '').slice(0, 60)}」`);

check('저장이 막혀도 세계는 돈다', now.ok === true && now.people >= 1,
	`사람 ${now.people}명 · 하루 ${now.day}일째`);
check('못 적고 있다는 사실이 <b>밖에서 보인다</b>', now.savesFailed > 0,
	now.savesFailed > 0 ? `실패 ${now.savesFailed}번` : '실패가 0으로 보인다 — 무음 실패다(죽어야 알게 된다)');
check('왜 못 적는지도 말한다', String(now.lastSaveError || '').length > 0,
	`「${String(now.lastSaveError || '(빈 이유)').slice(0, 60)}」`);
check('한 번도 성공한 적 없음이 드러난다', now.lastSaveAgoMs === -1 || now.savesDone === 0,
	`성공 ${now.savesDone}번 · 마지막 성공 ${now.lastSaveAgoMs}`);

if (failures === 0) {
	console.log('[save-failure] ✅ 못 적으면 못 적는다고 말한다');
	process.exit(0);
}

console.log(`\n[save-failure] RESULT: ${failures}건`);
process.exit(1);
