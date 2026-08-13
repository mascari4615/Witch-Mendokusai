#!/usr/bin/env node
// wm-web-alone-test.mjs — <b>혼자 처음 들어가도 세계가 보이나</b> (TASK-WM-302).
//
// ★ 왜: WM-301 이 잡은 고장은 <b>세 가지가 겹칠 때만</b> 났다 — ① 지연 없는 회선(같은 기계)
//   ② 세계에 <b>나 혼자</b> ③ 첫 프레임 전에 델타가 옴. 그런데 그 셋은 사람이 prod 를
//   <b>처음 여는 순간</b> 그대로다(노트북 localhost, 아직 아무도 없는 세계).
//   그동안의 창 관문은 전부 <b>나쁜 회선(프록시)</b> 아니면 <b>봇이 여럿</b>이었다 — 그래서 못 봤다.
//
// 재는 것 (지연 0 · 사람 하나):
//   ① 붙나 ② <b>내가 보이나</b> ③ 들판이 보이나 ④ 창이 안 터지나
//
// 이 관문은 일부러 <b>가장 빠른 회선</b>으로 붙는다 — 느린 쪽만 재면 빠른 쪽이 눈먼 자리가 된다.
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 보인다 · 1 = 안 보인다 · 2 = 못 돌림
//
// [빨강-확인] 세계가 들판을 안 보내게 하니 빨강 — 「들판이 통째로 비었다」 (2026-08-14)

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5520);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-alone-')), 'world.json');

/** 사람이 「안 뜨네」라고 느끼기 전에 보여야 한다. */
const SHOW_WITHIN_MS = 15000;

function cannotRun(message) {
	console.error(`[web-alone] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

let chromium;
try {
	const root = process.env.WM_PLAYWRIGHT_ROOT;
	const req = createRequire(root ? join(resolve(root), 'package.json') : import.meta.url);
	chromium = req('playwright').chromium;
} catch (error) {
	cannotRun(`playwright 를 못 찾았다 — ${error.message} (WM_PLAYWRIGHT_ROOT 로 알려 준다)`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-alone-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

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

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

{
	const until = Date.now() + 120000;
	let up = false;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}

	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다');
	}
}

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

const openedAt = Date.now();
await page.goto(`http://127.0.0.1:${port}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

// 「보인다」의 뜻: <b>내 인형</b>과 <b>들판</b>이 화면에 섰다.
let shown = { dolls: 0, gatherables: 0 };
{
	const until = Date.now() + SHOW_WITHIN_MS;
	while (Date.now() < until) {
		shown = await page.evaluate(() => window.__wmView.world());
		if (shown.dolls > 0 && shown.gatherables > 0) break;

		await wait(200);
	}
}

const showedInMs = Date.now() - openedAt;
const health = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })
	.then((one) => one.json()).catch(() => ({ people: -1 }));

console.log(`  ⓘ 혼자 · 지연 0 — ${showedInMs}ms 만에 화면: 사람 ${shown.dolls} · 들판 ${shown.gatherables}`
	+ ` (세계가 세는 사람 ${health.people}명)`);

check('세계가 나를 받았다', health.people >= 1, `${health.people}명`);
check('<b>내가</b> 화면에 보인다', shown.dolls > 0,
	shown.dolls > 0 ? `${shown.dolls}명` : '붙었는데 아무도 안 보인다 — 첫 전체 그림을 놓친 것이다(WM-301)');
check('들판이 화면에 보인다', shown.gatherables > 0,
	shown.gatherables > 0 ? `${shown.gatherables}자리` : '들판이 통째로 비었다');
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
killWorld();

if (failures === 0) {
	console.log('[web-alone] ✅ 가장 빠른 회선에서 혼자 들어가도 세계가 보인다');
	process.exit(0);
}

console.log(`\n[web-alone] RESULT: ${failures}건`);
process.exit(1);
