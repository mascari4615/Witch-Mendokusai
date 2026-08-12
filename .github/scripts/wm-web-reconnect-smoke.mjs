#!/usr/bin/env node
// wm-web-reconnect-smoke.mjs — 진짜 브라우저로 「세계가 죽었다 살아나면 창이 스스로 다시 붙나」 (TASK-WM-217).
//
// ★ 왜 브라우저인가: 게임 창에는 「놀 수 있나」를 재는 관문이 둘 있는데(혼자·둘이),
//   웹 창에는 <b>문법·계약·글자</b>를 보는 눈만 있었다. 그래서 창이 실제로 붙는지,
//   붙었다 끊긴 뒤 다시 붙는지는 아무도 안 봤다 — 그 자리에서 사람이 새로고침해 왔다.
//   여기서는 진짜 세계를 띄우고, 진짜 창을 열고, <b>세계를 죽였다 살린다</b>.
//
// 재는 것 넷: ① 창이 붙는다 ② 끊기면 스스로 다시 붙으려 한다
//             ③ 세계가 돌아오면 사람 손 없이 다시 붙는다 ④ 그 뒤 세계가 다시 그려진다
//
// 필요한 것: .NET 8 (dotnet) · playwright + chromium.
//   playwright 가 이 저장소에 없으므로 `WM_PLAYWRIGHT_ROOT`(playwright 를 깐 폴더)로 알려 준다.
//   CI 는 임시 폴더에 깔아서 넘긴다. 없으면 <b>거짓 초록이 아니라 CANNOT-RUN(exit 2)</b> 이다.
//
// exit: 0 = 다 맞음 · 1 = 틀린 것 있음 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5391);
const url = `http://127.0.0.1:${port}/`;

// 시험이 남긴 세계로 다음 판이 걸려 넘어지지 않게 — 매번 새 자리에서 논다.
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-web-reconnect-')), 'world.json');

function cannotRun(message) {
	console.error(`[web-reconnect] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let chromium;
try {
	const root = process.env.WM_PLAYWRIGHT_ROOT;
	const req = createRequire(root ? join(resolve(root), 'package.json') : import.meta.url);
	chromium = req('playwright').chromium;
} catch (error) {
	cannotRun(`playwright 를 못 찾았다 — ${error.message} (WM_PLAYWRIGHT_ROOT 로 알려 준다)`);
}

function startWorld() {
	spawn('dotnet', ['run', '--project', `${repo}/Server/WM.Server/WM.Server.csproj`,
		'--', '--urls', `http://127.0.0.1:${port}`],
		{ cwd: repo, env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore', shell: true });
}

async function waitHealthy(milliseconds) {
	const until = Date.now() + milliseconds;
	while (Date.now() < until) {
		try {
			const response = await fetch(`${url}health`);
			if (response.ok) return true;
		} catch { /* 아직 안 떴다 */ }
		await new Promise((done) => setTimeout(done, 400));
	}
	return false;
}

function killWorld() {
	// `dotnet run` 은 자식을 낳는다 — 포트를 쥔 놈을 직접 죽여야 세계가 진짜로 내려간다.
	try {
		if (process.platform === 'win32') {
			const found = execSync(`netstat -ano | findstr :${port} | findstr LISTENING`, { shell: 'cmd.exe' })
				.toString().trim().split('\n');
			for (const line of found) {
				const pid = line.trim().split(/\s+/).pop();
				if (pid && pid !== '0') execSync(`taskkill /PID ${pid} /F /T`, { stdio: 'ignore', shell: 'cmd.exe' });
			}
			return;
		}

		const pid = execSync(`lsof -ti tcp:${port}`).toString().trim().split('\n')[0];
		if (pid) execSync(`kill -9 ${pid}`);
	} catch { /* 이미 죽었다 */ }
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

startWorld();
if (await waitHealthy(120000) === false) {
	killWorld();
	cannotRun('세계가 안 떴다 (dotnet 이 없거나 포트가 막혔다)');
}

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

async function statusHas(text, milliseconds) {
	try {
		await page.waitForFunction(
			(want) => (document.getElementById('status')?.textContent || '').includes(want),
			text, { timeout: milliseconds });
	} catch { /* 아래에서 실제 글자를 보여 준다 */ }

	return await page.textContent('#status');
}

await page.goto(url);

const opened = await statusHas('붙었다', 30000);
check('창이 세계에 붙는다', opened === '붙었다', opened);

// 죽이기 전의 시각 — 다시 붙은 뒤 이게 움직여야 <b>새 소식이 오는 것</b>이다.
//   (화면에 남은 옛 글자는 끊긴 채로도 그대로라서, 그것만 보면 거짓 초록이 난다.)
const clockBefore = await page.textContent('#clock');

killWorld();
const dropped = await statusHas('다시 붙는 중', 25000);
check('끊기면 스스로 다시 붙으려 한다', dropped.includes('다시 붙는 중'), dropped);

startWorld();
const back = await statusHas('붙었다', 90000);
check('세계가 돌아오면 사람 손 없이 다시 붙는다', back === '붙었다', back);

let clockAfter = clockBefore;
try {
	await page.waitForFunction(
		(was) => (document.getElementById('clock')?.textContent || '') !== was,
		clockBefore, { timeout: 20000 });
} catch { /* 아래에서 실제 글자를 보여 준다 */ }
clockAfter = await page.textContent('#clock');
check('다시 붙은 뒤 세계 소식이 다시 흐른다', clockAfter !== clockBefore, `${clockBefore} → ${clockAfter}`);
check('창이 조용히 터지지 않았다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
killWorld();

if (failures === 0) {
	console.log('[web-reconnect] ✅ 웹 창은 세계가 내려갔다 와도 사람 손 없이 다시 논다');
	process.exit(0);
}

console.log(`\n[web-reconnect] RESULT: ${failures}건`);
process.exit(1);
