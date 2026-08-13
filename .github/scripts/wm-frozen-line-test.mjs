#!/usr/bin/env node
// wm-frozen-line-test.mjs — <b>얼어붙은 창을 세계가 놓아주나</b> (TASK-WM-355).
//
// ★ 왜: 노트북 덮개를 닫거나 지하철에 들어가면 줄은 <b>끊기지 않은 채</b> 아무것도 안 흐른다.
//   끊긴 것과 다르다 — 끊기면 양쪽이 곧 알지만, 얼면 아무도 모른 채 서로를 기다린다.
//   그동안 세계는 그 사람을 「접속 중」으로 들고 있다: 인형이 그 자리에 서 있고(<b>진짜 유령</b>),
//   정원 한 자리를 계속 먹는다. TCP 가 스스로 포기하는 데는 몇 시간이 걸린다.
//   실측(2026-08-14, 고치기 전): 얼린 뒤 <b>6분이 지나도</b> 세계가 세는 사람이 1 이었다.
//
// 재는 것 (얼린 회선 너머 봇 하나 · 놓아주는 시간을 짧게 낮춘 세계):
//   ① 얼자마자는 <b>아직</b> 들고 있다(그냥 끊긴 것과 헷갈리지 않게)
//   ② 정한 시간이 지나면 놓아준다(사람 수가 준다) ③ 놓아준 것을 <b>숫자로 적는다</b>
//
// 실행: node .github/scripts/wm-frozen-line-test.mjs
// exit: 0 = 놓아준다 · 1 = 영영 들고 있다 · 2 = 못 돌림
//
// [빨강-확인] 놓아주는 시간을 아주 크게(9999초) 두니 빨강 — 「얼린 지 한참인데 세계가 세는 사람 1」.

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5570);
const linePort = worldPort + 1;

/** 이 시험에서만 쓰는 짧은 기다림 — 제품은 90초다(규칙은 같고 수만 작다). */
const LET_GO_SECONDS = 10;

function cannotRun(message) {
	console.error(`[얼어붙은줄] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-froze-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-froze-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile, WM_LET_GO_SECONDS: String(LET_GO_SECONDS) },
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
	if (up === false) { killWorld(); cannotRun('세계가 안 떴다'); }
}

const health = () => fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

const badLine = openBadLine({ listenPort: linePort, targetPort: worldPort });
await badLine.listen();

// 얼릴 봇 하나 + <b>성한 봇 하나</b>(곧은 회선) — 성한 사람까지 쓸어내면 그게 더 나쁘다.
const frozen = new WebSocket(`ws://127.0.0.1:${linePort}/ws`);
frozen.onopen = () => frozen.send(JSON.stringify({ type: 'hello', secret: '' }));
frozen.onerror = () => { /* 아래에서 수로 본다 */ };

const healthy = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
healthy.onopen = () => healthy.send(JSON.stringify({ type: 'hello', secret: '' }));
healthy.onerror = () => { /* 아래에서 수로 본다 */ };
const beating = setInterval(() => {
	if (healthy.readyState === 1) healthy.send(JSON.stringify({ type: 'beat' }));
}, 250);

await wait(3000);
const joined = await health();
if (joined.people < 2) { clearInterval(beating); killWorld(); await badLine.close(); cannotRun(`봇 둘이 다 못 들어갔다 — ${joined.people}명`); }

badLine.freeze();

// ① 얼자마자는 아직 들고 있어야 한다 — 그래야 「그냥 끊긴 것」과 안 헷갈린다.
await wait(2000);
const rightAfter = await health();

// ② 정한 시간이 지나면 놓아준다.
await wait((LET_GO_SECONDS + 6) * 1000);
const later = await health();

clearInterval(beating);
try { healthy.close(); } catch { /* 이미 */ }
await badLine.close();
killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 놓아주는 시간 ${LET_GO_SECONDS}초 · 사람 ${joined.people}명 → 언 직후 ${rightAfter.people}명`
	+ ` → ${LET_GO_SECONDS + 6}초 뒤 ${later.people}명 · 놓아준 창 ${later.letGoOfFrozen}개`);

check('언 직후에는 아직 들고 있다', rightAfter.people === joined.people,
	`${rightAfter.people}명 (얼기 전 ${joined.people}명)`);

check('정한 시간이 지나면 놓아준다', later.people < joined.people,
	`${later.people}명 남았다`);

check('놓아준 것을 숫자로 적는다', later.letGoOfFrozen >= 1, `${later.letGoOfFrozen}개`);

// ★ 성한 사람까지 쓸어내면 그게 더 나쁘다 — 숨소리를 보내는 창은 남아 있어야 한다.
check('숨쉬는 창은 안 건드린다', later.people >= 1, `남은 사람 ${later.people}명`);

if (bad === 0) {
	console.log('[얼어붙은줄] ✅ 얼어붙은 창은 놓아주고, 숨쉬는 창은 그대로 둔다');
	process.exit(0);
}

console.log(`\n[얼어붙은줄] RESULT: ${bad}건`);
process.exit(1);
