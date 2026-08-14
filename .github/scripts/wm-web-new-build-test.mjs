#!/usr/bin/env node
// wm-web-new-build-test.mjs — <b>새 판이 나가면 열린 창이 스스로 새로 받나</b> (TASK-WM-367).
//
// ★ 왜: 배포가 나가면 서버는 새것인데 <b>이미 열려 있던 창</b>은 옛것이다. 그 사람만 고쳐 놓은
//   고장을 계속 겪고, 우리는 그 사실을 모른다(그 창은 아무 말도 안 한다).
//   세계는 자기가 내주는 창의 <b>도장</b>을 welcome 에 실어 주고, 창은 자기 도장과 견줘 다르면
//   스스로 새로고침한다 — 그 길이 진짜로 도는지 <b>진짜 창</b>으로 본다.
//
// 재는 것 (배포를 흉내낸다):
//   ① 창을 열어 붙인다 ② 세계를 껐다가 <b>창 파일을 고쳐서</b> 다시 켠다(= 새 판 배포)
//   ③ 창이 다시 붙은 뒤 <b>스스로 새로고침</b>한다 ④ 새로고침 뒤에도 세계가 보인다(빈 화면 X)
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 새 판을 알아본다 · 1 = 옛 창 그대로 · 2 = 못 돌림
//
// [빨강-확인] 창의 도장 견주기를 꺼 보니 빨강 — 「스스로 새로고침했다 — 아니다(1번 그림)」 (2026-08-14).

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5390);

/** 새 판이 뜨고 창이 알아채기까지 봐 주는 시간. [문턱-사유] (c) 사람이 느끼는 선 — 배포 뒤 한 판(1분) 안. */
const NOTICE_WITHIN_MS = 60000;

function cannotRun(message) {
	console.error(`[새판] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let chromium;
try {
	const root = process.env.WM_PLAYWRIGHT_ROOT;
	const req = createRequire(root ? join(resolve(root), 'package.json') : import.meta.url);
	chromium = req('playwright').chromium;
} catch (error) {
	cannotRun(`playwright 를 못 찾았다 — ${error.message}`);
}

let appFolder;
try {
	appFolder = join(mkdtempSync(join(tmpdir(), 'wm-newbuild-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${appFolder}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

const dll = join(appFolder, 'WM.Server.dll');
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-newbuild-')), 'world.json');
let world = null;

async function startWorld() {
	world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
		cwd: dirname(dll), env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore',
	});

	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } });
			if (answer.ok) return true;
		} catch { /* 아직 */ }
		await wait(300);
	}

	return false;
}

function killWorld() {
	if (world === null) return;
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
	world = null;
}

if (await startWorld() === false) { killWorld(); cannotRun('세계가 안 떴다'); }

const browser = await chromium.launch();
const page = await browser.newPage();
const errors = [];
let paints = 0;
page.on('pageerror', (error) => errors.push(String(error)));
page.on('load', () => { paints += 1; });

await page.goto(`http://127.0.0.1:${worldPort}/`);
const painted = await page.waitForFunction(
	() => typeof window.__wmView === 'object' && window.__wmView.me() !== null,
	null, { timeout: 60000 }).then(() => true).catch(() => false);

if (painted === false) { await browser.close(); killWorld(); cannotRun('창이 안 떴다'); }

const paintsBefore = paints;

// ── 배포를 흉내낸다: 세계를 껐다가 <b>창 파일을 고쳐서</b> 다시 켠다 ────────────
killWorld();
appendFileSync(join(appFolder, 'wwwroot', 'index.html'), '\n<!-- 새 판 (관문이 만든 것) -->\n');
await wait(1500);
if (await startWorld() === false) { await browser.close(); cannotRun('새 판 세계가 안 떴다'); }

// 창이 스스로 다시 붙고, 도장이 다른 것을 보고, 스스로 새로고침해야 한다.
const cameBack = await page.waitForFunction(
	() => typeof window.__wmView === 'object' && window.__wmView.me() !== null,
	null, { timeout: NOTICE_WITHIN_MS }).then(() => true).catch(() => false);

await wait(3000);

const paintsAfter = paints;
const seen = await page.evaluate(() => ({
	dolls: window.__wmView ? window.__wmView.world().dolls : -1,
	field: window.__wmView ? window.__wmView.world().gatherables : -1,
}));

await browser.close();
killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 그린 횟수 ${paintsBefore} → ${paintsAfter} · 다시 붙었나 ${cameBack ? '예' : '아니오'}`
	+ ` · 창이 아는 사람 ${seen.dolls}명 · 들판 ${seen.field}곳`);

check('스스로 새로고침했다', paintsAfter > paintsBefore,
	`${paintsBefore}번 그림 → ${paintsAfter}번 그림`);

check('새로고침 뒤에도 세계가 보인다', cameBack && seen.field > 0,
	`사람 ${seen.dolls}명 · 들판 ${seen.field}곳`);

check('창이 조용히 안 터졌다', errors.length === 0, errors.slice(0, 2).join(' | ') || '오류 없음');

if (bad === 0) {
	console.log('[새판] ✅ 새 판이 나가면 열린 창이 스스로 새로 받는다');
	process.exit(0);
}

console.log(`\n[새판] RESULT: ${bad}건`);
process.exit(1);
