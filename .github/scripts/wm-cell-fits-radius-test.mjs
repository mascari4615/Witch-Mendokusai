#!/usr/bin/env node
// wm-cell-fits-radius-test.mjs — <b>칸이 반경보다 크면 세계가 안 뜬다</b> (TASK-WM-402).
//
// ★ 왜: 한 벌은 <b>칸 한복판</b> 기준으로 고른다. 칸이 반경보다 크면 둘 사이가 같아도
//   한쪽만 보는 판이 생긴다 — 「나는 안 보이는데 상대는 나를 본다」(WM-401 이 그 대칭을 박았다).
//   칸 크기는 손으로 바꾸는 값이라 한 글자로 그 성질이 조용히 깨진다.
//   그러니 <b>안 뜨는 것</b>이 낫다(땅 겹침 WM-368 · 기억 깨짐 WM-333 과 같은 규칙).
//
// 재는 것: ① 칸을 반경보다 크게 주면 세계가 <b>안 뜬다</b> ② 알맞게 주면 <b>뜬다</b>
//   (②가 없으면 「무엇을 줘도 안 뜬다」와 못 가른다).
//
// [빨강-확인] 막는 줄을 지우니 「칸 64m 인데도 떴다」로 빨강 (2026-08-14).
//
// exit: 0 = 크면 막고 알맞으면 뜬다 · 1 = 안 막거나 못 뜬다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5478);
const cannotRun = (m) => { console.error(`[칸반경] CANNOT-RUN: ${m}`); process.exit(2); };
const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-fits-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`, { cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

async function tryWorld(cell, patienceMs) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-fits-')), 'world.json');
	const child = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: worldFile, WM_INTEREST_CELL: String(cell) },
		stdio: 'ignore',
	});

	let up = false;
	const until = Date.now() + patienceMs;
	while (Date.now() < until) {
		try { const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }); if (answer.ok) { up = true; break; } } catch { /* 아직 */ }
		await wait(300);
	}

	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${child.pid} /F /T`, { stdio: 'ignore' });
		else child.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }

	await wait(800);
	return up;
}

let failures = 0;
const check = (what, ok, detail) => { if (ok === false) failures += 1; console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`); };

// ⚠ 알맞은 값부터 본다 — 이게 안 뜨면 아래 결과는 뜻이 없다(관문 규율 ②).
const smallOk = await tryWorld(24, 60000);
if (smallOk === false)
	cannotRun('알맞은 칸(24m)으로도 세계가 안 떴다 — 이 기계 사정이라 「막나」를 못 잰다');

const bigUp = await tryWorld(64, 20000);

check('알맞은 칸(24m)이면 뜬다', smallOk, '떴다');
check('칸이 반경(32m)보다 크면(64m) 안 뜬다', bigUp === false, bigUp ? '떴다 — 안 막았다' : '안 떴다');

if (failures === 0) {
	console.log('[칸반경] ✅ 칸이 반경보다 크면 세계가 스스로 안 뜬다');
	process.exit(0);
}
console.log(`\n[칸반경] RESULT: ${failures}건`);
process.exit(1);
