#!/usr/bin/env node
// wm-web-beyond-test.mjs — <b>진짜 창</b>에 국경 너머 사람이 보이나 (TASK-WM-267).
//
// ★ 왜: 국경 너머 보기(WM-263)·말 건너기(WM-264)는 <b>서버 시험</b>으로만 쟀다. 그런데 사람이
//   보는 것은 창이다 — 세계가 보내 줘도 창이 안 그리면 그 사람에게는 여전히 벽이다.
//   게다가 그림자는 <b>못 때리는 사람</b>이라, 이 세계 사람과 똑같이 그리면
//   「왜 저 사람만 반응이 없지」가 된다. 그래서 비쳐 보이게 그린다 — 그것까지 여기서 잰다.
//
// 재는 것 (나쁜 회선 왕복 200ms 위에서):
//   ① 창에 저 세계 사람이 뜨나(번호가 음수) ② 그 사람만 비쳐 보이게 그렸나
//   ③ 저 세계 사람의 <b>말</b>이 들리나 ④ 창이 조용히 안 터졌나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 보인다 · 1 = 안 보인다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5470);
const westPort = eastPort + 1;
const eastLine = eastPort + 2;

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const SECRET = '두 세계만 아는 말';
const EAST_LAND = '동:0,-40,40,40';
const WEST_LAND = '서:-40,-40,0,40';

function cannotRun(message) {
	console.error(`[web-beyond] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-beyond-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-beyond-')), 'world.json');
	worlds.push(spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: worldFile, WM_ZONE: zone, WM_ZONE_NEIGHBOURS: neighbours, WM_ZONE_SECRET: SECRET },
		stdio: 'ignore',
	}));
}

function killWorlds() {
	for (const one of worlds) {
		try {
			if (process.platform === 'win32') execSync(`taskkill /PID ${one.pid} /F /T`, { stdio: 'ignore' });
			else one.kill('SIGKILL');
		} catch { /* 이미 죽었다 */ }
	}

	worlds.length = 0;
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

async function waitHealthy(port, milliseconds) {
	const until = Date.now() + milliseconds;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) return true;
		} catch { /* 아직 */ }
		await wait(300);
	}

	return false;
}

startWorld(eastPort, EAST_LAND, `${WEST_LAND}=ws://127.0.0.1:${westPort}/ws`);
startWorld(westPort, WEST_LAND, `${EAST_LAND}=ws://127.0.0.1:${eastPort}/ws`);

if (await waitHealthy(eastPort, 120000) === false || await waitHealthy(westPort, 120000) === false) {
	killWorlds();
	cannotRun('세계가 안 떴다');
}

// 창은 나쁜 회선 너머로만 동쪽 세계를 안다.
const line = openBadLine({ listenPort: eastLine, targetPort: eastPort, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS });
await line.listen();

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(`http://127.0.0.1:${eastLine}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

check('나쁜 회선으로 동쪽 세계에 붙었다', await page.evaluate(() => typeof window.__wmView === 'object'));

// 창은 국경(x=0) 쪽으로 걷다가 <b>넘기 직전에 선다</b>.
// ⚠ 국경을 넘으면 세계가 「저 세계로 가라」를 보내고 창은 정말로 넘어가 버린다 —
//   그러면 이 시험이 재려던 「국경에 서서 저 너머를 본다」가 아니게 된다.
const whereAmI = async () => page.evaluate(() => {
	const me = (window.__wmView.dolls() || []).find((one) => one.isLocal);
	return me ? me.drawnX : null;
});

const walkWest = async (milliseconds) => {
	const until = Date.now() + milliseconds;
	while (Date.now() < until) {
		const x = await whereAmI();
		if (x !== null && x <= 1.2)
			return;

		await page.keyboard.down('a');
		await wait(150);
		await page.keyboard.up('a');
		await wait(30);
	}
};

// 서쪽 세계에는 <b>봇 하나</b>가 국경 바로 너머에 선다(창을 두 개 띄울 이유가 없다).
const bot = new WebSocket(`ws://127.0.0.1:${westPort}/ws`);
let botReady = false;
bot.onopen = () => { bot.send(JSON.stringify({ type: 'hello', secret: '' })); botReady = true; };
bot.onmessage = () => { /* 봇은 듣기만 한다 */ };

{
	const until = Date.now() + 8000;
	while (Date.now() < until && botReady === false) await wait(100);
}

check('저 세계에 사람이 하나 있다', botReady);

// ⚠ 봇을 국경 <b>바깥</b>으로 밀면 안 된다 — 저 세계가 「네 자리는 이쪽이 아니다」라며
//   이쪽 세계로 넘겨 버리고, 저 세계는 텅 빈다(첫 판이 그래서 그림자 0명이었다).
//   제 땅 안쪽(x 약 -1)에 세워 둔다 — 국경에서 1m 남짓, 띠 안이다.
for (let step = 0; step < 8; step += 1) {
	bot.send(JSON.stringify({ type: 'move', x: -0.14, z: 0 }));
	await wait(60);
}

await walkWest(6000);

let seen = null;
{
	const until = Date.now() + 20000;
	while (Date.now() < until) {
		seen = await page.evaluate(() => (window.__wmView.dolls() || []).filter((one) => one.id < 0));
		if (seen.length > 0) break;

		await walkWest(600);
	}
}

check('창에 국경 너머 사람이 뜬다', seen !== null && seen.length > 0,
	seen === null ? '못 물어봤다' : `그림자 ${seen.length}명`);
check('그 사람만 비쳐 보이게 그렸다 (못 건드리는 사람이라는 표시)',
	seen !== null && seen.length > 0 && seen.every((one) => one.seeThrough === true && one.beyond === true),
	JSON.stringify(seen && seen[0] || null));

// 이 세계 사람(나)은 비쳐 보이면 안 된다 — 다 비치면 「다르게 그렸다」가 아니다.
// ⚠ 재기 전에 <b>내 인형이 서 있는지</b>부터 본다 (domain-wm.md § 관문 규율): 국경 언저리에서
//   한 걸음 더 가면 저 세계로 넘어가고(WM-254), 그 순간 내 인형은 잠깐 없다 —
//   그걸 「이 세계 사람 0명」으로 읽으면 창 탓이 아닌 것을 창 탓으로 적게 된다(느린 CI 에서 빨갰다).
await page.waitForFunction(
	() => (window.__wmView.dolls() || []).some((one) => one.id > 0), null, { timeout: 15000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

const mine = await page.evaluate(() => (window.__wmView.dolls() || []).filter((one) => one.id > 0));
check('이 세계 사람은 그대로 그린다', mine.length > 0 && mine.every((one) => one.seeThrough === false),
	`이 세계 사람 ${mine.length}명`);

// ── 저 세계 사람의 말이 창에 들리나 (TASK-WM-264 를 진짜 창으로) ────────
await page.evaluate(() => {
	window.__wmHeard = [];
	window.__wmView.socket().addEventListener('message', (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'said') window.__wmHeard.push(said);
	});
});

bot.send(JSON.stringify({ type: 'say', text: '거기 누구 있소' }));

let heard = [];
{
	const until = Date.now() + 10000;
	while (Date.now() < until) {
		heard = await page.evaluate(() => window.__wmHeard || []);
		if (heard.length > 0) break;

		await wait(200);
	}
}

check('국경 너머 사람의 말이 창에 들린다', heard.some((one) => one.text === '거기 누구 있소'),
	heard.length === 0 ? '아무 말도 안 들렸다' : JSON.stringify(heard[0]));
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

try { bot.close(); } catch { /* 이미 닫혔다 */ }
await browser.close();
await line.close();
killWorlds();

if (failures === 0) {
	console.log('[web-beyond] ✅ 나쁜 회선에서도 창에 국경 너머 사람이 비쳐 보이고, 말도 들린다');
	process.exit(0);
}

console.log(`\n[web-beyond] RESULT: ${failures}건`);
process.exit(1);
