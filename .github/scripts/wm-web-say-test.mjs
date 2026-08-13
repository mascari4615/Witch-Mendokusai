#!/usr/bin/env node
// wm-web-say-test.mjs — <b>창에서 말을 할 수 있나</b> (TASK-WM-268).
//
// ★ 왜: 세계는 말을 나를 줄 알고(WM-250) 남의 말을 이름표에 띄울 줄도 알았다. 그런데 창에
//   <b>말할 자리</b>가 없어서 사람은 한 마디도 못 했다 — 혼자 걷는 화면과 같이 노는 세계를
//   가르는 것이 그것이다. 게다가 이 자리는 나쁜 회선에서 특히 티가 난다(왕복 200ms).
//
// 재는 것: 진짜 창에서 Enter 로 열고 치고 Enter — ① 세계가 받았나(옆 사람이 들었나)
//   ② 내 창에도 돌아오나 ③ 말하는 중에 <b>걷지 않나</b>(글자가 걸음으로 새면 창이 고장 난 것)
//   ④ 창이 조용히 안 터졌나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 말한다 · 1 = 못 한다 · 2 = 못 돌림
//
// [빨강-확인] 세계가 남에게 말을 안 나르게 하니 2건 빨강 — 옆 사람이 그 말을 못 듣는다 (2026-08-14)

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5476);
const linePort = port + 1;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-say-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const WORD = '거기 잠깐';

function cannotRun(message) {
	console.error(`[web-say] CANNOT-RUN: ${message}`);
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

let world = null;
function killWorld() {
	if (world === null) return;
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
	world = null;
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-say-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
});

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

// 창은 나쁜 회선 너머로만 세계를 안다.
const line = openBadLine({ listenPort: linePort, targetPort: port, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS });
await line.listen();

// 옆 사람(봇) — 세계가 정말로 받았는지는 <b>남이 들었나</b>로 본다.
const neighbour = new WebSocket(`ws://127.0.0.1:${port}/ws`);
const heardByNeighbour = [];
let neighbourReady = false;
neighbour.onopen = () => { neighbour.send(JSON.stringify({ type: 'hello', secret: '' })); neighbourReady = true; };
neighbour.onmessage = (event) => {
	let said;
	try { said = JSON.parse(event.data); } catch { return; }

	if (said.type === 'said') heardByNeighbour.push(said);
};

{
	const until = Date.now() + 8000;
	while (Date.now() < until && neighbourReady === false) await wait(100);
}

check('옆 사람이 세계에 있다', neighbourReady);

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(`http://127.0.0.1:${linePort}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

check('나쁜 회선으로 세계에 붙었다', await page.evaluate(() => typeof window.__wmView === 'object'));

// 내 창에 돌아오는 말도 본다.
await page.evaluate(() => {
	window.__wmHeard = [];
	window.__wmView.socket().addEventListener('message', (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'said') window.__wmHeard.push(said);
	});
});

const whereAmI = async () => page.evaluate(() => {
	const me = (window.__wmView.dolls() || []).find((one) => one.isLocal);
	return me ? { x: me.serverX, z: me.serverZ } : null;
});

const before = await whereAmI();

// ── 사람이 하는 그대로: Enter 로 열고, 치고, Enter ─────────────────────
await page.keyboard.press('Enter');
check('Enter 를 누르면 말할 자리가 열린다',
	await page.evaluate(() => document.activeElement && document.activeElement.id === 'saybox'));

// 「w」·「a」 가 섞여 있다 — 말하는 중에 이 글자가 걸음으로 새면 안 된다.
await page.keyboard.type(WORD);
await page.keyboard.press('Enter');

let mine = [];
{
	const until = Date.now() + 10000;
	while (Date.now() < until) {
		mine = await page.evaluate(() => window.__wmHeard || []);
		if (mine.length > 0) break;

		await wait(150);
	}
}

check('내가 한 말이 내 창에 돌아온다', mine.some((one) => one.text === WORD),
	mine.length === 0 ? '아무것도 안 돌아왔다' : JSON.stringify(mine[0]));
check('옆 사람이 그 말을 들었다', heardByNeighbour.some((one) => one.text === WORD),
	heardByNeighbour.length === 0 ? '옆 사람은 못 들었다' : JSON.stringify(heardByNeighbour[0]));

const after = await whereAmI();
check('말하는 동안 걷지 않았다', before !== null && after !== null
	&& Math.abs(before.x - after.x) < 0.05 && Math.abs(before.z - after.z) < 0.05,
	`${JSON.stringify(before)} → ${JSON.stringify(after)}`);

// 말한 뒤에는 손이 다시 세계로 돌아와야 한다 — 안 그러면 걷기가 죽는다.
check('말한 뒤에는 말할 자리가 닫힌다',
	await page.evaluate(() => document.activeElement === null
		|| document.activeElement.id !== 'saybox'));

// 빈 말은 세계에 안 보낸다(줄만 먹는다).
const saidCount = mine.length;
await page.keyboard.press('Enter');
await page.keyboard.press('Enter');
await wait(800);
check('빈 말은 안 보낸다',
	(await page.evaluate(() => (window.__wmHeard || []).length)) === saidCount);

check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

try { neighbour.close(); } catch { /* 이미 닫혔다 */ }
await browser.close();
await line.close();
killWorld();

if (failures === 0) {
	console.log('[web-say] ✅ 나쁜 회선에서도 창에서 말하고, 그 말이 남에게 간다');
	process.exit(0);
}

console.log(`\n[web-say] RESULT: ${failures}건`);
process.exit(1);
