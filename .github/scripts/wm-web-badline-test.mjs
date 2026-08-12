#!/usr/bin/env node
// wm-web-badline-test.mjs — <b>나쁜 회선에서도</b> 세계가 노나 (TASK-WM-224).
//
// ★ 왜: WM 이 지금까지 낸 모든 수치는 loopback 에서 나왔다 — 지연 0·흔들림 0·무한 대역폭.
//   그런 회선은 지구에 없다. 「200명에서 버틴다」「0.17m 만 뒤처진다」는 사실
//   「이 기계 안에서는」이라는 단서가 빠진 주장이었다. 그 단서를 지운다.
//
// 세우는 것: 세계 앞에 진짜 TCP 회선(왕복 200ms · 흔들림 30ms). 창도 봇도 그 회선으로만 붙는다.
// 재는 것: ① 붙나 ② 첫 화면까지 얼마나 ③ 남이 걷는 게 부드러운가 ④ 회선이 끊기면 스스로 돌아오나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 논다 · 1 = 못 논다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine, releaseAt } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5398);
const linePort = worldPort + 1;
const url = `http://127.0.0.1:${linePort}/`;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-web-badline-')), 'world.json');

/*
 * 회선 — 흔한 모바일 4G 언저리. 이보다 좋은 회선만 재는 것은 안 잰 것과 같다.
 */
const ONE_WAY_MS = 100;   // 왕복 200ms
const JITTER_MS = 30;

/*
 * 기준 — loopback 기준을 그대로 쓰면 안 된다. 회선이 늦는 만큼은 회선 값이지 고장이 아니다.
 *
 * 뒤처짐: 회선 한쪽(0.1s) × 걸음 3m/s = 0.3m 은 물리다. 여기에 메우는 값(0.17m)과 흔들림을 더해
 *   0.3 + 0.17 + 여유 = 1.2m. 이걸 넘으면 「저 사람 어디 있지」가 된다.
 * 멎은 프레임: 회선이 늦어도 메우는 쪽은 안 멎는다 — 늦는 것과 끊기는 것은 다른 일이다.
 * 첫 화면: 왕복 0.2s 짜리 회선에서 3초를 넘으면 사람이 창을 닫는다.
 */
const MAX_FROZEN_FRAME_RATIO = 0.25;
const MAX_SPEED_WOBBLE = 0.9;
const MAX_LAG_METERS = 1.2;
const MAX_FIRST_PAINT_MS = 3000;
const MAX_RECOVER_MS = 15000;

function cannotRun(message) {
	console.error(`[web-badline] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

// ── ① 회선 자체의 규칙 — 살아 있는 것을 세우기 전에, 셈이 맞나 ──────────
{
	const line = { latencyMs: 100, jitterMs: 30, bytesPerSecond: 0 };
	check('회선이 늦춘다', releaseAt(0, 1000, 50, line, 0) === 1100);
	check('흔들림은 늦추는 쪽으로만 붙는다', releaseAt(0, 1000, 50, line, 1) === 1130);
	check('앞 조각을 앞지르지 않는다 (TCP 는 순서를 안 바꾼다)',
		releaseAt(1200, 1000, 50, line, 0) === 1200);
	check('좁은 회선은 큰 조각만큼 뒤를 민다',
		releaseAt(0, 1000, 10000, { latencyMs: 0, jitterMs: 0, bytesPerSecond: 10000 }, 0) === 2000);
}

/*
 * ①-b 회선이 <b>정말로</b> 그 속도인가 — 자를 재는 자 (TASK-WM-229).
 *
 * ★ 왜: 셈이 맞아도 회선이 맞다는 뜻은 아니다. 실제로 밟았다(2026-08-12): 받기를 멈춰 둔 사이
 *   노드가 버퍼를 64KB 한 덩어리로 합쳐 주는 바람에, 「초당 4KB」라 해 놓고 회선은 몰아치고
 *   멎기를 반복했다(실측 8~16KB/s, 어떤 구간은 0). 그 위에서 난 초록은 전부 거짓이었다.
 *   그래서 살아 있는 것을 재기 전에 <b>자부터 잰다</b>.
 */
{
	const net = await import('node:net');
	const source = net.createServer((one) => {
		const blob = Buffer.alloc(4096, 65);
		const push = setInterval(() => { if (one.destroyed === false) one.write(blob); }, 20);
		one.on('close', () => clearInterval(push));
		one.on('error', () => clearInterval(push));
	});
	await new Promise((done) => source.listen(0, '127.0.0.1', done));

	const want = 4000;
	const ruler = openBadLine({ listenPort: 0, targetPort: source.address().port, latencyMs: 20, jitterMs: 0, bytesPerSecond: want });
	await ruler.listen();
	const sink = net.connect(ruler.port(), '127.0.0.1');
	let came = 0;
	sink.on('data', (piece) => { came += piece.length; });
	sink.on('error', () => { /* 아래 칸이 잡는다 */ });

	await new Promise((done) => setTimeout(done, 6000));   // 줄이 찰 때까지
	came = 0;
	const from = Date.now();
	await new Promise((done) => setTimeout(done, 6000));
	const rate = came / ((Date.now() - from) / 1000);

	sink.destroy();
	await ruler.close();
	source.close();

	check('회선이 적어 놓은 속도로 흐른다 (자를 먼저 잰다)',
		rate > want * 0.75 && rate < want * 1.25,
		`${(rate / 1000).toFixed(2)} KB/s (건 ${(want / 1000).toFixed(0)} KB/s)`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-web-badline-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: 'ignore',
});

{
	const until = Date.now() + 120000;
	let up = false;
	while (Date.now() < until) {
		try {
			const response = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } });
			if (response.ok) { up = true; break; }
		} catch { /* 아직 안 떴다 */ }
		await new Promise((done) => setTimeout(done, 400));
	}

	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다');
	}
}

// ── ② 나쁜 회선을 세계 앞에 세운다 ────────────────────────────────────
const badLine = openBadLine({ listenPort: linePort, targetPort: worldPort, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS });
await badLine.listen();

// 가만히 선 사람 몇 — 끊겼다 돌아왔을 때 <b>안 움직인 사람</b>이 다시 보이는지가 핵심이라
// (움직이는 사람은 어차피 다음 판에 실린다) 일부러 아무것도 안 하는 사람을 둔다.
const idlers = [];
for (let i = 0; i < 8; i += 1) {
	const one = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	one.onopen = () => one.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.onerror = () => { /* 아래 칸이 잡는다 */ };
	idlers.push(one);
}

// 걷는 상대 — 이 사람도 나쁜 회선으로 붙는다.
const walker = new WebSocket(`ws://127.0.0.1:${linePort}/ws`);
let walkerDollId = null;
walker.onopen = () => walker.send(JSON.stringify({ type: 'hello', secret: '' }));
walker.onerror = () => { /* 아래 칸이 잡는다 */ };
walker.onmessage = (event) => {
	try {
		const said = JSON.parse(event.data);
		if (said.type === 'welcome') walkerDollId = said.id;
	} catch { /* 우리 말이 아니다 */ }
};
await new Promise((done) => setTimeout(done, 2500));

const walking = setInterval(() => {
	if (walker.readyState === 1) walker.send(JSON.stringify({ type: 'move', x: 0.15, z: 0 }));
}, 50);

// ── ③ 창을 연다 — 첫 화면까지 걸린 시간을 잰다 ────────────────────────
const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

const openedAt = Date.now();
await page.goto(url);
let firstPaint = -1;
try {
	await page.waitForFunction(
		() => (document.getElementById('status')?.textContent || '').includes('붙었다'),
		null, { timeout: 30000 });
	firstPaint = Date.now() - openedAt;
} catch { /* 아래 칸이 잡는다 */ }

check('나쁜 회선으로도 창이 붙는다', firstPaint >= 0, firstPaint >= 0 ? `${firstPaint}ms` : '안 붙었다');
check(`첫 화면이 ${MAX_FIRST_PAINT_MS}ms 안에 뜬다`, firstPaint >= 0 && firstPaint <= MAX_FIRST_PAINT_MS,
	`${firstPaint}ms (왕복 ${ONE_WAY_MS * 2}ms 회선)`);

// ── ④ 남이 걷는 게 부드러운가 ─────────────────────────────────────────
// ⚠ 「남 중 첫 번째」를 잡으면 안 된다 (실측 2026-08-12): 가만히 선 사람이 잡히면
//   멎은 프레임 100% 가 나온다 — 재려던 것은 <b>걷는 사람</b>이 부드러운가다.
await page.evaluate((who) => {
	window.__wmTrail = [];
	const write = () => {
		const one = window.__wmView.dolls().find((doll) => doll.id === who);
		if (one) window.__wmTrail.push({ at: performance.now(), drawnX: one.drawnX, serverX: one.serverX });
		requestAnimationFrame(write);
	};
	requestAnimationFrame(write);
}, walkerDollId);

await new Promise((done) => setTimeout(done, 4000));
const trail = await page.evaluate(() => window.__wmTrail.slice());

check('나쁜 회선으로도 걷는 상대가 보인다', trail.length > 30, `프레임 ${trail.length}장`);

if (trail.length > 30) {
	let frozen = 0;
	let moving = 0;
	const speeds = [];
	let lagSum = 0;

	for (let i = 1; i < trail.length; i += 1) {
		const spent = trail[i].at - trail[i - 1].at;
		if (spent <= 0) continue;

		const went = Math.abs(trail[i].drawnX - trail[i - 1].drawnX);
		moving += 1;
		if (went < 0.0005) frozen += 1;
		speeds.push(went / (spent / 1000));
		lagSum += Math.abs(trail[i].serverX - trail[i].drawnX);
	}

	const frozenRatio = frozen / Math.max(1, moving);
	const mean = speeds.reduce((sum, one) => sum + one, 0) / Math.max(1, speeds.length);
	const variance = speeds.reduce((sum, one) => sum + ((one - mean) ** 2), 0) / Math.max(1, speeds.length);
	const wobble = mean > 0 ? Math.sqrt(variance) / mean : 999;
	const lag = lagSum / Math.max(1, speeds.length);

	check('늦는 회선인데도 화면이 안 멎는다', frozenRatio <= MAX_FROZEN_FRAME_RATIO,
		`${(frozenRatio * 100).toFixed(1)}% (기준 ${MAX_FROZEN_FRAME_RATIO * 100}%)`);
	check('그려진 속도가 고르다', wobble <= MAX_SPEED_WOBBLE, `흔들림 ${wobble.toFixed(2)} (기준 ${MAX_SPEED_WOBBLE})`);
	// ⚠ 이 뒤처짐은 <b>메우기 뒤처짐</b>이다 — 창이 마지막으로 받은 자리와 그려진 자리의 차.
	//   회선이 늦은 만큼(0.1s × 3m/s = 0.3m)은 여기 안 들어간다(창은 그걸 모른다).
	//   진짜 끝-끝 뒤처짐을 재려면 봇이 보낸 시각과 맞춰야 한다 — 아직 안 함(TASK-WM-224 § 남은 것).
	check('메우기 뒤처짐이 작다 (회선 지연은 별개)', lag <= MAX_LAG_METERS, `${lag.toFixed(2)}m (기준 ${MAX_LAG_METERS}m)`);
	console.log(`  ⓘ 평균 그려진 속도 ${mean.toFixed(2)}m/s (걷는 속도 3m/s)`);
}

// ── ⑤ 회선이 끊기면 — 잃은 조각이 끝내 안 닿았을 때 진짜로 일어나는 일 ──
//
// ★ 끊김은 「다시 붙나」로 안 끝난다 (TASK-WM-230). 세계는 <b>바뀐 것만</b> 보낸다 —
//   다시 붙은 창에 델타를 주면 그 창의 세계는 <b>영영 반쪽</b>이다(안 움직인 사람·안 바뀐 건물이
//   통째로 빈다). 화면은 멀쩡해 보이고 오류도 없다. 그래서 눈으로는 절대 안 잡힌다.
// ★ 들판이 <b>줄지 않는가</b> — 세계는 「바뀐 자리만」 보낸다. 창이 그걸 전체로 알고 갈아 끼우면
//   안 바뀐 자리 수십 개가 한 번에 사라진다(오류 없이 조용히, TASK-WM-230).
//   그래서 한 번 본 들판의 <b>가장 많았던 수</b>를 적어 두고, 그 아래로 안 떨어지는지 본다.
const fieldWatch = await page.evaluate(() => {
	window.__wmField = { most: 0, least: 1e9 };
	window.__wmFieldTimer = setInterval(() => {
		const now = window.__wmView.world().gatherables;
		if (now > window.__wmField.most) window.__wmField.most = now;
		if (window.__wmField.most > 0 && now < window.__wmField.least) window.__wmField.least = now;
	}, 100);
	return true;
});
void fieldWatch;
await new Promise((done) => setTimeout(done, 3000));
const field = await page.evaluate(() => window.__wmField);

check('본 들판이 도중에 사라지지 않는다', field.most > 0 && field.least >= field.most,
	`가장 많을 때 ${field.most}자리 · 가장 적을 때 ${field.least === 1e9 ? '-' : field.least}자리`);

const beforeCut = await page.evaluate(() => window.__wmView.world());
badLine.cut();
await page.waitForFunction(
	() => (document.getElementById('status')?.textContent || '').includes('붙었다') === false,
	null, { timeout: 15000 }).catch(() => { /* 아래 칸이 잡는다 */ });

// ★ 끊긴 동안 눌러 보면 — 창이 터지지도, 조용히 무시하지도 않아야 한다 (TASK-WM-232).
//   보내는 자리 열다섯 중 하나에 「줄이 열려 있나」 검사가 빠져 있었다: 끊긴 동안 그 줄을
//   누르면 창이 터진다. 나머지는 안 터지는 대신 <b>아무 말도 없이</b> 무시했다 —
//   사람에게는 둘 다 「눌렀는데 반응이 없다」 = 고장이다.
const errorsBeforeClick = pageErrors.length;
await page.click('#complete').catch(() => { /* 손잡이가 안 보이면 아래 칸이 잡는다 */ });
const saidWhy = await page.textContent('#status').catch(() => '');

check('끊긴 동안 눌러도 창이 안 터진다', pageErrors.length === errorsBeforeClick,
	pageErrors.slice(errorsBeforeClick).join(' | ') || '오류 없음');
check('끊긴 동안 눌렀을 때 왜 안 되는지 말해 준다', (saidWhy || '').includes('끊'),
	`화면: "${saidWhy}"`);

const cutAt = Date.now();
let recovered = -1;
try {
	await page.waitForFunction(
		() => (document.getElementById('status')?.textContent || '').includes('붙었다'),
		null, { timeout: MAX_RECOVER_MS });
	recovered = Date.now() - cutAt;
} catch { /* 아래 칸이 잡는다 */ }

check('회선이 끊겨도 사람 손 없이 돌아온다', recovered >= 0,
	recovered >= 0 ? `${recovered}ms 만에` : `${MAX_RECOVER_MS}ms 안에 못 돌아왔다`);

// 돌아온 뒤 세계가 <b>통째로</b> 돌아오나 — 델타만 받으면 여기서 빈다.
const afterCut = await page.waitForFunction(
	(was) => {
		const now = window.__wmView.world();
		return now.dolls >= was.dolls && now.buildings >= was.buildings && now.gatherables >= was.gatherables
			? now : false;
	},
	beforeCut, { timeout: 10000, polling: 200 })
	.then((handle) => handle.jsonValue())
	.catch(() => page.evaluate(() => window.__wmView.world()));

check('돌아온 뒤 세계가 통째로 다시 보인다 (반쪽 델타가 아니다)',
	afterCut.dolls >= beforeCut.dolls && afterCut.buildings >= beforeCut.buildings
		&& afterCut.gatherables >= beforeCut.gatherables,
	`끊기기 전 사람 ${beforeCut.dolls}·건물 ${beforeCut.buildings}·들판 ${beforeCut.gatherables}`
	+ ` → 돌아온 뒤 사람 ${afterCut.dolls}·건물 ${afterCut.buildings}·들판 ${afterCut.gatherables}`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

clearInterval(walking);
for (const one of idlers) { try { one.close(); } catch { /* 이미 닫혔다 */ } }
try { walker.close(); } catch { /* 이미 닫혔다 */ }
await browser.close();
await badLine.close();
killWorld();

if (failures === 0) {
	console.log(`[web-badline] ✅ 왕복 ${ONE_WAY_MS * 2}ms · 흔들림 ${JITTER_MS}ms 회선에서도 세계가 논다`);
	process.exit(0);
}

console.log(`\n[web-badline] RESULT: ${failures}건`);
process.exit(1);
