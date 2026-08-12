#!/usr/bin/env node
// wm-web-reconnect-smoke.mjs — 진짜 브라우저로 「세계가 죽었다 살아나면 창이 스스로 다시 붙나」 (TASK-WM-217).
//
// ★ 왜 브라우저인가: 게임 창에는 「놀 수 있나」를 재는 관문이 둘 있는데(혼자·둘이),
//   웹 창에는 <b>문법·계약·글자</b>를 보는 눈만 있었다. 그래서 창이 실제로 붙는지,
//   붙었다 끊긴 뒤 다시 붙는지는 아무도 안 봤다 — 그 자리에서 사람이 새로고침해 왔다.
//   여기서는 진짜 세계를 띄우고, 진짜 창을 열고, <b>세계를 죽였다 살린다</b>.
//
// 재는 것 다섯: ① 창이 붙는다 ② 세계가 진짜로 내려갔다 ③ 끊기면 스스로 다시 붙으려 한다
//               ④ 세계가 돌아오면 사람 손 없이 다시 붙는다 ⑤ 그 뒤 세계 소식이 다시 흐른다
//
// 필요한 것: .NET 8 (dotnet) · playwright + chromium.
//   playwright 가 이 저장소에 없으므로 `WM_PLAYWRIGHT_ROOT`(playwright 를 깐 폴더)로 알려 준다.
//   CI 는 임시 폴더에 깔아서 넘긴다. 없으면 <b>거짓 초록이 아니라 CANNOT-RUN(exit 2)</b> 이다.
//
// exit: 0 = 다 맞음 · 1 = 틀린 것 있음 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
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

// 세계를 <b>한 번 짓고</b> 그 결과물을 직접 띄운다.
//
// ⚠ `dotnet run` 을 쓰면 안 된다: 그건 자식(진짜 서버)을 낳는 껍데기라 손잡이가 안 잡히고,
//   그래서 「포트를 쥔 놈을 찾아 죽이는」 우회로를 쓰게 된다. 그 우회로가 리눅스에서
//   <b>자기 자신을 죽였다</b>(node 의 fetch 가 같은 포트로 살아 있는 줄을 붙들고 있어서
//   같이 잡혔다 — CI 가 exit 137 로 죽었다). 자식 하나를 직접 들고 있으면 그런 일이 없다.
let world = null;

function buildWorld() {
	// ★ publish 다 (build 아님) — 배포가 쓰는 그 모양이어야 창(wwwroot)이 같이 실린다.
	//   그냥 build 하면 산출물에 창이 없어 화면이 통째로 안 뜬다(실측: 시험이 #status 를 못 찾았다).
	const out = join(mkdtempSync(join(tmpdir(), 'wm-web-publish-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	return join(out, 'WM.Server.dll');
}

function startWorld(dll) {
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

async function waitHealthy(milliseconds) {
	const until = Date.now() + milliseconds;
	while (Date.now() < until) {
		try {
			// ★ 줄을 남기지 않는다 — 남겨 두면 「포트를 쥔 놈」에 이 시험 자신이 낀다.
			const response = await fetch(`${url}health`, { headers: { connection: 'close' } });
			if (response.ok) return true;
		} catch { /* 아직 안 떴다 */ }
		await new Promise((done) => setTimeout(done, 400));
	}
	return false;
}

async function waitGone(milliseconds) {
	const until = Date.now() + milliseconds;
	while (Date.now() < until) {
		try {
			await fetch(`${url}health`, { headers: { connection: 'close' } });
		} catch {
			return true;
		}
		await new Promise((done) => setTimeout(done, 200));
	}
	return false;
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

let dll;
try {
	dll = buildWorld();
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

startWorld(dll);
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
check('세계가 진짜로 내려갔다', await waitGone(15000), `${url}health 가 안 받는다`);

const dropped = await statusHas('다시 붙는 중', 25000);
check('끊기면 스스로 다시 붙으려 한다', dropped.includes('다시 붙는 중'), dropped);

startWorld(dll);
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
