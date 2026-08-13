#!/usr/bin/env node
// wm-web-answer-test.mjs — <b>누르면 곧바로 대답하나</b> (TASK-WM-283).
//
// ★ 왜: 걸음은 앞질러 그린다(WM-271) — 눌렀을 때 곧바로 움직인다. 그런데 <b>줍기·짓기</b>는
//   세계의 답을 기다린다. 왕복 200ms 회선이면 누르고 나서 <b>아무 일도 안 일어나는 시간</b>이
//   생긴다. 사람은 그 시간을 「안 눌렸다」로 읽고 또 누른다(그게 「반응이 굼뜬 게임」의 정체다).
//
// 재는 것: 나쁜 회선에서 줍기·짓기를 누르고 ① 화면이 <b>무엇이든 즉시</b> 말해 주나
//   ② 세계의 답이 오기까지 얼마나 걸리나 ③ 그 사이 눌린 것이 조용히 씹히지 않나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 대답한다 · 1 = 조용하다 · 2 = 못 돌림
//
// [빨강-확인] 창의 즉시 대답(askedFor 의 상태 글씨)을 끄니 「세계 답의 절반 안쪽」이 빨강 —
//   388ms(세계 답 338ms). 그때도 「1초 안」 바닥은 초록이라, 촘촘한 주장은 위 줄이 한다 (2026-08-14)

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5880);
const linePort = port + 1;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-answer-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const REACH = 2.5;

function cannotRun(message) {
	console.error(`[web-answer] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-answer-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

let world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
});

function killWorld() {
	if (world === null) return;
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
		else world.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }
	world = null;
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

const line = openBadLine({ listenPort: linePort, targetPort: port, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS });
await line.listen();

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(`http://127.0.0.1:${linePort}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });
await page.waitForFunction(
	() => (window.__wmView.dolls() || []).some((one) => one.isLocal), null, { timeout: 30000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

check('나쁜 회선으로 세계에 붙었다', await page.evaluate(() => typeof window.__wmView === 'object'));

// ⚠ 재기 전에 <b>잴 것이 있는지</b>부터 (domain-wm.md § 관문 규율).
await page.waitForFunction(() => (window.__wmView.field() || []).length > 0, null, { timeout: 30000 })
	.catch(() => { /* 아래에서 잡는다 */ });

const field = await page.evaluate(() => window.__wmView.field());
if (field.length === 0) {
	await browser.close();
	await line.close();
	killWorld();
	cannotRun('창이 들판을 못 받았다 — 누를 것이 없으면 「대답하나」를 잴 수 없다');
}

await page.bringToFront();
// ⚠ 자판을 쥐려고 <b>세계를 누르면 안 된다</b> (2026-08-13): 그 자리는 땅이라 「짓기」가 나간다
//   (CI 진단에 「재료가 모자란다」가 찍혔다 — 관문이 세계를 건드리고 있었다).
//   위쪽 띠(머리말)를 눌러 자판만 가져온다.
await page.click('header', { position: { x: 5, y: 5 } }).catch(() => { /* 없으면 그냥 둔다 */ });

// 손이 닿는 데까지 걸어간다 — 멀리서 누르면 세계는 「손이 안 닿는다」로 답한다(그건 다른 얘기다).
const target = field.slice().sort((a, b) => Math.hypot(a.x, a.z) - Math.hypot(b.x, b.z))[0];
{
	const until = Date.now() + 30000;
	while (Date.now() < until) {
		const me = await page.evaluate(() => (window.__wmView.dolls() || []).find((one) => one.isLocal) || null);
		if (me === null) { await wait(200); continue; }

		if (Math.hypot(target.x - me.drawnX, target.z - me.drawnZ) <= REACH * 0.6) break;

		const keyX = target.x > me.drawnX + 0.2 ? 'd' : (target.x < me.drawnX - 0.2 ? 'a' : null);
		const keyZ = target.z > me.drawnZ + 0.2 ? 'w' : (target.z < me.drawnZ - 0.2 ? 's' : null);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.down(key);
		}

		await wait(120);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.up(key);
		}
	}
}

const me = await page.evaluate(() => (window.__wmView.dolls() || []).find((one) => one.isLocal) || null);
check('주울 것 앞까지 걸어갔다', me !== null && Math.hypot(target.x - me.drawnX, target.z - me.drawnZ) <= REACH,
	me === null ? '나를 못 찾았다' : `${Math.hypot(target.x - me.drawnX, target.z - me.drawnZ).toFixed(2)}m`);

// ── 누른다 — 그리고 <b>화면이 언제 무엇을 말하나</b>를 본다 ────────────
const pressed = await page.evaluate((nodeId) => {
	const started = Date.now();
	const before = (document.getElementById('status') || {}).textContent;
	window.__wmAnswer = { started, before, saidAt: -1, said: '', bagAt: -1 };

	window.__wmView.socket().addEventListener('message', (event) => {
		let heard;
		try { heard = JSON.parse(event.data); } catch { return; }

		if (heard.type === 'bag' && window.__wmAnswer.bagAt < 0) window.__wmAnswer.bagAt = Date.now();
	});

	// 화면이 바뀌는 순간을 잡는다 — 창이 <b>스스로</b> 말하는지가 이 판의 물음이다.
	const watch = setInterval(() => {
		const now = (document.getElementById('status') || {}).textContent;
		if (now !== window.__wmAnswer.before && window.__wmAnswer.saidAt < 0) {
			window.__wmAnswer.saidAt = Date.now();
			window.__wmAnswer.said = now;
		}
	}, 10);

	setTimeout(() => clearInterval(watch), 5000);
	return started;
}, target.id);
void pressed;

// ★ 사람처럼 <b>진짜로 누른다</b> — 줄로 바로 보내면 창이 스스로 대답하는지 못 잰다.
{
	const where = await page.evaluate((spot) => window.__wmView.screenOf(spot.x, spot.z), target);
	await page.mouse.click(Math.round(where.x), Math.round(where.y));
}

await wait(3000);
const answer = await page.evaluate(() => window.__wmAnswer);

const saidInMs = answer.saidAt < 0 ? -1 : answer.saidAt - answer.started;
const bagInMs = answer.bagAt < 0 ? -1 : answer.bagAt - answer.started;

console.log(`  ⓘ 누른 뒤 — 화면이 말하기까지 ${saidInMs < 0 ? '안 말함' : saidInMs + 'ms'}`
	+ ` · 세계의 답(가방)까지 ${bagInMs < 0 ? '안 옴' : bagInMs + 'ms'}`);

check('세계는 답한다 (가방이 온다)', bagInMs >= 0, bagInMs < 0 ? '가방이 안 왔다' : `${bagInMs}ms`);
// ⚠ 절대 밀리초로 자르면 안 된다 (domain-wm.md § 관문 규율 ④): 느린 기계에서는 누르는 것도
//   재는 것도 느려 144ms 가 나온다(CI 실측) — 그건 창이 굼뜬 게 아니다.
//   제품 주장은 <b>「세계의 답보다 훨씬 먼저 말한다」</b>이다.
check('창이 세계의 답보다 훨씬 먼저 말한다 (절반 안쪽)',
	saidInMs >= 0 && bagInMs > 0 && saidInMs <= bagInMs / 2,
	saidInMs < 0 ? '아무 말도 안 했다 — 사람은 「안 눌렸다」로 읽고 또 누른다'
		: `${saidInMs}ms · 세계의 답 ${bagInMs}ms · "${answer.said}"`);
// [문턱-사유] (b) 사람이 느끼는 선 — 위 줄이 이미 「세계 답의 절반」으로 제품을 주장한다.
//   이 줄은 그 위에 얹는 <b>바닥</b>이다(세계가 아주 느릴 때 절반도 느릴 수 있으니).
// ⚠ 0.5초가 아니라 <b>1초</b>다 (2026-08-14, 미리 훑기): 같은 0.5초 문턱이 짓기 관문에서
//   2코어 CI 만 빨갛게 만들었다(WM-322). 여기 바닥의 뜻은 「사람이 기다린다고 느끼기 전」이고,
//   그 선은 넉넉해야 한다 — 촘촘한 주장은 이미 위 줄(절반)이 한다.
check('그래도 사람이 기다린다고 느낄 만큼 늦지는 않다 (1초 안)', saidInMs >= 0 && saidInMs <= 1000,
	`${saidInMs}ms`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
await line.close();
killWorld();

if (failures === 0) {
	console.log('[web-answer] ✅ 나쁜 회선에서도 누르면 곧바로 대답한다');
	process.exit(0);
}

console.log(`\n[web-answer] RESULT: ${failures}건`);
process.exit(1);
