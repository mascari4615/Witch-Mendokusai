#!/usr/bin/env node
// wm-ops-door-test.mjs — <b>살림살이 창구가 밖에는 안 열리나</b> (TASK-WM-371).
//
// ★ 왜: 세계가 밖으로 열리는 날(터널) <b>살림살이 창구도 같이</b> 열린다.
//   · `/lines` = 줄마다의 왕복·막힘·읽은 마디 (남의 회선 사정)
//   · `/health` 의 기억 파일 자리 (내 기계의 폴더 구조)
//   · `/health?collect=1` = <b>세계를 잠깐 멈추는</b> 큰 청소 — 아무나 부르면 그게 곧 공격이다
//   지금은 127.0.0.1 뒤라 안 보이지만, 여는 날 이 세 자리가 같이 열리면 그때는 늦다.
//   <b>안 열어 주는 편이 나중에 여는 것보다 쉽다.</b>
//
// 재는 것: 밖에서 온 척(CF-Connecting-IP) 두드려 보고, 안에서·열쇠로도 두드려 본다.
//
// 실행: node .github/scripts/wm-ops-door-test.mjs
// exit: 0 = 밖에는 안 열린다 · 1 = 열린다 · 2 = 못 돌림
//
// [빨강-확인] 창구 검사(OpsAllowed)를 늘 참으로 두니 3건 빨강 —
//   「밖에서 /lines 200」·「밖에서 기억 파일이 보인다」·「밖에서 부른 큰 청소가 돈다」 (2026-08-14).

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5420);
const OPS_KEY = 'ops-key-for-this-gate';   // 헤더 값은 아스키만 실린다(한글은 못 싣는다)

function cannotRun(message) {
	console.error(`[살림창구] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-ops-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-ops-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile, WM_OPS_TOKEN: OPS_KEY },
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

/** 밖에서 온 척 — 터널이 붙여 주는 이름표를 흉내낸다. */
const fromOutside = { 'CF-Connecting-IP': '203.0.113.9', connection: 'close' };
const withKey = { ...fromOutside, 'X-WM-Ops': OPS_KEY };

const ask = (path, headers) => fetch(`http://127.0.0.1:${worldPort}${path}`, { headers });

const insideLines = await ask('/lines', { connection: 'close' });
const outsideLines = await ask('/lines', fromOutside);
const keyedLines = await ask('/lines', withKey);

const insideHealth = await (await ask('/health', { connection: 'close' })).json();
const outsideHealth = await (await ask('/health', fromOutside)).json();

// 큰 청소는 <b>돌았나</b>로 본다 — 몇 번 돌았는지는 세계가 세고 있다.
const before = (await (await ask('/health', { connection: 'close' })).json()).gcGen2;
await (await ask('/health?collect=1', fromOutside)).json();
const afterOutside = (await (await ask('/health', { connection: 'close' })).json()).gcGen2;
await (await ask('/health?collect=1', { connection: 'close' })).json();
const afterInside = (await (await ask('/health', { connection: 'close' })).json()).gcGen2;

killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ /lines — 안 ${insideLines.status} · 밖 ${outsideLines.status} · 열쇠 ${keyedLines.status}`
	+ ` · 큰 청소 ${before} →(밖) ${afterOutside} →(안) ${afterInside}`);

check('밖에서는 줄 장부를 못 본다', outsideLines.status === 403, `${outsideLines.status}`);
check('안에서는 줄 장부를 본다', insideLines.status === 200, `${insideLines.status}`);
check('열쇠를 들면 밖에서도 본다', keyedLines.status === 200, `${keyedLines.status}`);

check('밖에서는 기억 파일 자리를 안 말한다',
	(outsideHealth.worldFile || '') === '', `「${outsideHealth.worldFile || '(빈칸)'}」`);
check('안에서는 기억 파일 자리를 말한다',
	(insideHealth.worldFile || '').length > 0, `「${(insideHealth.worldFile || '(빈칸)').slice(-24)}」`);

check('밖에서 부른 큰 청소는 안 돈다', afterOutside === before, `${before} → ${afterOutside}`);
check('안에서 부른 큰 청소는 돈다', afterInside > afterOutside, `${afterOutside} → ${afterInside}`);

if (bad === 0) {
	console.log('[살림창구] ✅ 밖에는 안 열리고, 안에서는 다 된다');
	process.exit(0);
}

console.log(`\n[살림창구] RESULT: ${bad}건`);
process.exit(1);
