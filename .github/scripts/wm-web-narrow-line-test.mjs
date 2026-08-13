#!/usr/bin/env node
// wm-web-narrow-line-test.mjs — 좁은 회선에서 창이 <b>과거를 보고 있지 않나</b> (TASK-WM-225).
//
// ★ 왜: 세계는 밀린 창을 건너뛴다(Connection.Sending). 그런데 「보냈다」는 판정이
//   <b>OS 송신 버퍼에 넣었다</b>는 뜻이다 — 회선이 좁으면 버퍼(수십 KB)가 다 찰 때까지
//   세계는 계속 「보냈다」고 믿는다. 그동안 창이 보는 것은 <b>몇 초 전 세계</b>다.
//   화면은 멀쩡히 부드럽다(끊기지도 않는다). 그래서 지금까지의 어떤 시험도 이걸 못 봤다.
//   사람에게는 「분명히 옆에 있었는데 갑자기 저기 있네」로 나타난다.
//
// 재는 것 = <b>정보의 나이</b>. 봇 하나가 일정 속도로 곧게 걷는다(기준 시계). 창이 받은
//   그 사람의 자리를 진짜 자리와 견주면, 그 차이가 곧 「창이 몇 초 전을 보고 있나」다.
//   ① 나이가 얼마나 되나 ② 시간이 갈수록 <b>불어나나</b>(불어나면 버퍼가 쌓이는 중 = 회복 불가)
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 현재를 본다 · 1 = 과거를 본다 · 2 = 못 돌림
//
// ⚠ 빨강 걷기 <b>못 했다</b> — 세 번 시도한 기록 (2026-08-14): 「밀린 창을 건너뛰기」를 꺼도 초록이었다(나이 0.13초).
//   이 자리는 <b>방어가 여러 겹</b>이라(건너뛰기 · 작은 한 장 · 보내기 자체가 await) 한 겹만 빼서는 안 늙는다.
//   ① 「밀린 창 건너뛰기」만 끔 → 초록(나이 0.13초)
//   ② 세 겹(건너뛰기 · 좁힘 · 보내기 await)을 <b>다</b> 끔 → 여전히 초록(0.13초)
//   ③ 회선을 초당 800바이트까지 졸라맴 → 여전히 초록(0.16초)
//   즉 <b>자극이 수요보다 크다</b>: 좁힌 4KB/s 가 이 판의 실제 수요(정상 흐름)보다 여전히 넉넉하다.
//   (32KB/s 는 <b>들어올 때</b> 낱말표·첫 그림까지 낀 값이라 정상 수요가 아니다.)
//   다음에 할 일 = <b>정상 수요를 따로 재고</b> 그 1/5 로 좁히기. 그 전까지는 빚 목록에 그대로 둔다.

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5400);
const linePort = worldPort + 1;
const url = `http://127.0.0.1:${linePort}/`;
const crowd = Number(process.env.WM_CROWD || 40);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-web-narrow-')), 'world.json');

/*
 * 회선 — 나쁜 모바일. 왕복 200ms 에 초당 32KB(256kbps).
 * 40명 광장의 알림은 실측 7KB/s 였으니 <b>회선이 모자라지는 않다</b> —
 * 모자라서 죽는지가 아니라, 여유가 적을 때 <b>버퍼가 쌓이는지</b>를 본다.
 */
const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const BYTES_PER_SECOND = 32000;

/*
 * 기준.
 * 나이: 회선 한쪽(0.1s) + 판 사이(0.05s) + 여유 = 0.5s. 이보다 늙으면 사람이 어긋남을 느낀다.
 * 불어남: 8초를 재서 뒤쪽 절반이 앞쪽 절반보다 0.3s 넘게 늙었으면 <b>쌓이는 중</b>이다 —
 *   그건 시간이 지나면 무한히 나빠진다는 뜻이라, 절대값보다 이쪽이 더 무섭다.
 */
const MAX_AGE_SECONDS = 0.5;
const MAX_AGE_GROWTH_SECONDS = 0.3;
// 회선이 보낼 것보다 좁아지면 늙는 건 물리다 — 하지만 <b>끝없이</b> 늙으면 그건 고장이다.
const MAX_SQUEEZED_AGE_SECONDS = 2;

/*
 * 이 자의 <b>해상도</b> — 대략 ±0.3초다. 두 시계(노드·창)가 다르고, 기준 시계에 직선을 맞추는
 * 데도 오차가 있다. 그러니 이보다 작은 음수는 「미래를 봤다」가 아니라 <b>자의 떨림</b>이다.
 * 이 칸이 잡으려는 것은 -2초·-6.9초 같은 <b>말이 안 되는</b> 값이다(그런 값이 초록으로
 * 통과한 적이 있다 — 그때 판정은 위쪽만 막고 있었다).
 */
const RULER_SLACK_SECONDS = 0.4;
const WALK_SPEED = 3;      // m/s — 봇이 내는 속도 (0.15m / 50ms)
const MEASURE_MS = 4000;

/*
 * ⚠ 재는 시간이 길면 걷는 사람이 <b>관심 반경(32m) 밖</b>으로 나간다 — 3m/s 로 16초면 48m 다.
 *   한때 「먼저 뒤로 물러나 두기」로 피했는데, 그러면 돌아서는 구간이 표본에 섞여 나이가
 *   <b>음수</b>로 나왔다(CI 실측 2026-08-12: 나이 -6.9초인데 판정은 초록 — 거짓 초록).
 *   꼼수 대신 재는 시간을 줄인다: 두 구간 합쳐 8초 × 3m/s = 24m, 반경 안이고 걸음은 한 방향이다.
 */
/*
 * 표본 = 세계가 <b>말한 횟수</b>다(그린 횟수가 아니라). 20Hz × 8초 = 160판이 나올 자리.
 * 넉넉할 때는 그 절반은 와야 하고, 회선을 조인 뒤에는 <b>적게 오는 것이 정상</b>이다 —
 * 그래도 아예 안 오면 그건 죽은 것이라, 몇 판은 와야 한다.
 */
// ⚠ 문턱은 <b>재는 시간에서 끌어낸다</b> — 손으로 박아 두면 재는 시간을 줄일 때 같이 안 줄어
//   「표본이 모자라다」로 빨개진다(실측 2026-08-12, 8초→4초로 줄이자 그렇게 됐다).
const PLATES_PER_SECOND = 20;
const LEAST_SAMPLES_ROOMY = Math.floor((MEASURE_MS / 1000) * PLATES_PER_SECOND * 0.5);
const LEAST_SAMPLES_SQUEEZED = Math.floor((MEASURE_MS / 1000) * PLATES_PER_SECOND * 0.1);

/*
 * 도중에 확 좁아진 회선 — 지하철·엘리베이터. 40명 광장의 알림(7KB/s)보다 <b>좁다</b>:
 * 세계가 보내려는 것이 회선보다 많을 때 무슨 일이 나는지가 진짜 물음이다.
 */
const SQUEEZED_BYTES_PER_SECOND = 4000;

function cannotRun(message) {
	console.error(`[web-narrow] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-web-narrow-app-')), 'app');
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

// ── 광장 — 다른 사람들은 좋은 회선으로 붙는다(그들의 회선은 이 시험의 관심사가 아니다) ──
const bots = [];
for (let i = 0; i < crowd; i += 1) {
	const socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onerror = () => { /* 못 붙은 놈은 아래 숫자에서 빠진다 */ };
	bots.push(socket);
}

// ★ <b>광장이 실제로 움직여야</b> 회선이 좁아지는 뜻이 있다 (2026-08-14 실측).
//   전에는 봇 40명이 <b>가만히</b> 서 있었다 — 그러면 세계가 보낼 것이 거의 없어(바뀐 것만 나른다)
//   회선을 초당 800바이트로 졸라매도 나이가 0.16초였다. 즉 이 관문의 <b>자극이 안 먹고</b> 있었다:
//   세계의 방어(건너뛰기·작은 한 장·await)를 <b>셋 다 꺼도</b> 초록이었다.
//   그래서 모두 걷게 한다 — 그래야 좁은 회선이 진짜 병목이 된다.
const milling = setInterval(() => {
	for (let i = 0; i < bots.length; i += 1) {
		if (bots[i].readyState !== 1) continue;
		const turn = ((Date.now() / 500) + i) % 4;
		const step = 0.15;
		bots[i].send(JSON.stringify({
			type: 'move',
			x: turn < 2 ? step : -step,
			z: turn % 2 === 0 ? step : -step,
		}));
	}
}, 100);

// ── 기준 시계 — 곧게 일정 속도로 걷는 한 사람. 이 사람도 좋은 회선이다.
//    (창이 늦는 것만 보려면 기준은 안 늦어야 한다.)
//
// ⚠ 「보낸 걸음 수」로 진짜 자리를 셈하면 안 된다 (실측 2026-08-12): 영점을 두 시각에 따로 찍는
//   순간 그 사이에 걸은 만큼이 통째로 편향이 된다 — 나이가 <b>음수</b>로 나왔다(-0.5초).
//   그래서 봇이 <b>세계가 말해 주는 자기 자리</b>를 직접 적는다. 봇의 회선은 빠르니 이게 곧 진짜다.
const walker = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
let walkerDollId = null;
const truth = []; // { at, x } — 세계가 말한 걷는 사람의 자리
walker.onopen = () => walker.send(JSON.stringify({ type: 'hello', secret: '' }));
walker.onerror = () => { /* 아래 칸이 잡는다 */ };
walker.onmessage = (event) => {
	let said;
	try { said = JSON.parse(event.data); } catch { return; }

	// 세계가 맞아들이며 「네 인형은 이 번호다」를 준다 (welcome 의 id).
	if (said.type === 'welcome' && typeof said.id === 'number') walkerDollId = said.id;
	if (walkerDollId === null) return;

	// 몰린 칸에서 공유 소식에 내가 빠지면 세계가 따로 말해 준다(me).
	if (said.type === 'me' && said.doll && said.doll.id === walkerDollId) {
		truth.push({ at: Date.now(), x: said.doll.x });
		return;
	}

	if (Array.isArray(said.dolls) === false) return;

	for (const one of said.dolls) {
		if (one.id !== walkerDollId) continue;

		// ⚠ 이름표(names)에도 dolls 가 있고 거기엔 자리가 없다 — 창 쪽에서 한 번 밟은 함정을
		//   기준 시계 쪽에서 <b>그대로 다시</b> 밟았다(맞춘 속도가 NaN, 실측 2026-08-12).
		if (typeof one.x !== 'number') continue;

		truth.push({ at: Date.now(), x: one.x });
	}
};
await new Promise((done) => setTimeout(done, 3000));

// ⚠ 걷기는 <b>창이 다 붙고 갈고리를 건 뒤에</b> 시작한다 (실측 2026-08-12): 먼저 걷게 두면
//   창이 뜨는 동안 걷는 사람이 관심 반경(32m) 밖으로 나가 버린다 — 사람이 많을수록 창이 늦게
//   뜨므로 200명에서 「걷는 사람이 한 판도 안 실린다」로 나왔다(서버는 멀쩡했다).
//
// ⚠ 그리고 <b>곧게 앞으로만</b> 걸리면 안 된다 (실측 2026-08-12): 두 구간을 재는 데 16초가 걸리고,
//   3m/s 로 16초면 48m — 관심 반경(32m)을 넘어 <b>정상적으로</b> 안 보이게 된다.
//   그걸 「소식이 안 온다」로 읽어 이틀치 엉뚱한 추적을 했다(회선은 멀쩡했고, 창은 322개 말을 받고 있었다).
//   그래서 먼저 뒤로 걸어 두고, 재는 동안 앞으로 걷는다 — 늘 반경 안에 있으면서 걸음은 한 방향이다.
let walking = null;

const startWalking = () => {
	walking = setInterval(() => {
		if (walker.readyState !== 1) return;
		walker.send(JSON.stringify({ type: 'move', x: 0.15, z: 0 }));
	}, 50);
};

// ── 좁은 회선을 창 앞에만 세운다 ───────────────────────────────────────
const badLine = openBadLine({
	listenPort: linePort,
	targetPort: worldPort,
	latencyMs: ONE_WAY_MS,
	jitterMs: JITTER_MS,
	bytesPerSecond: BYTES_PER_SECOND,
});
await badLine.listen();

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(url);
await page.waitForFunction(
	() => (document.getElementById('status')?.textContent || '').includes('붙었다'),
	null, { timeout: 30000 }).catch(() => { /* 아래 칸이 잡는다 */ });

const joined = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((r) => r.json());
check(`광장에 ${crowd}명이 있다`, joined.people >= crowd, `세계가 세는 사람 ${joined.people}명`);

// 창이 받은 「걷는 사람의 자리」를 <b>소식이 올 때</b> 적는다.
//
// ⚠ 프레임(rAF)으로도, 시계(setInterval)로도 적으면 안 된다 (CI 실측 2026-08-12):
//   둘 다 창의 본 줄(main thread)에 매달려 있어 표본 수가 <b>기계 성능</b>을 따라간다 —
//   2코어 러너에서 6개까지 떨어져 게이트가 태어날 때부터 흔들렸다(내 기계 63개).
//   재는 것은 「소식이 얼마나 늙었나」다. 그러니 <b>소식이 도착한 그 자리</b>에서 적는다 —
//   그리기와 아무 상관이 없고, 표본 수는 세계가 말한 횟수와 같아진다(= 뜻이 있는 숫자다).
await page.evaluate((who) => {
	window.__wmAges = [];
	window.__wmWalker = who;
	const write = (text) => {
		let said;
		try { said = JSON.parse(text); } catch { return; }

		const from = said.type === 'me' && said.doll ? [said.doll] : said.dolls;
		if (Array.isArray(from) === false) return;

		for (const one of from) {
			if (one.id !== window.__wmWalker) continue;

			// ⚠ 이름표(names)에도 dolls 가 있다 — 거기엔 <b>자리가 없다</b>(id·name 뿐).
			//   그걸 자리로 읽으면 나이가 NaN 이 되고, 판정이 조용히 무의미해진다(CI 실측 2026-08-12).
			if (typeof one.x !== 'number') continue;

			window.__wmAges.push({ at: Date.now(), id: one.id, sawX: one.x });
		}
	};

	window.__wmView.socket().addEventListener('message', (event) => write(event.data));
}, walkerDollId);

startWalking();

// ⚠ 방향을 바꾼 <b>직후</b>는 재지 않는다 (CI 실측 2026-08-12): 창이 보는 것은 회선만큼 늦으므로
//   돌아선 순간 창은 아직 <b>뒷걸음</b>을 보고 있다. 그 구간을 섞으면 나이가 음수로 나오고
//   「시간이 갈수록 늙는다」로 잘못 읽힌다(앞 절반 -0.15초 → 뒤 절반 0.17초로 빨갰다).
//   걸음이 한 방향으로 자리 잡은 뒤부터 적는다.
await new Promise((done) => setTimeout(done, 1500));
await page.evaluate(() => { window.__wmAges = []; });
const roomyFrom = Date.now();
await new Promise((done) => setTimeout(done, MEASURE_MS));
const roomyTo = Date.now();
const ages = await page.evaluate(() => window.__wmAges.slice());

/*
 * ② 회선이 <b>도중에</b> 확 좁아진다 — 지하철에 들어간 순간. 창은 이미 붙어 있으므로
 *    새로 받을 것은 세계 소식뿐이다. 여기서 세계가 계속 밀어 넣으면 버퍼가 쌓이고,
 *    창은 점점 <b>과거</b>를 본다(화면은 여전히 부드럽다 — 그래서 안 보인다).
 */
badLine.squeeze(SQUEEZED_BYTES_PER_SECOND);
await page.evaluate(() => { window.__wmAges = []; });
const tightFrom = Date.now();
await new Promise((done) => setTimeout(done, MEASURE_MS));
const tightTo = Date.now();
const squeezedAges = await page.evaluate(() => window.__wmAges.slice());

clearInterval(walking);
clearInterval(milling);
for (const socket of bots) { try { socket.close(); } catch { /* 이미 닫혔다 */ } }
try { walker.close(); } catch { /* 이미 닫혔다 */ }
await browser.close();
await badLine.close();
killWorld();

check('창이 걷는 사람을 봤다', ages.length >= LEAST_SAMPLES_ROOMY, `세계가 말한 판 ${ages.length}개`);
check('봇이 자기 자리를 세계에서 읽었다', truth.length >= LEAST_SAMPLES_SQUEEZED, `표본 ${truth.length}개 (적어도 ${LEAST_SAMPLES_SQUEEZED}개)`);
if (ages.length < LEAST_SAMPLES_ROOMY || truth.length < LEAST_SAMPLES_SQUEEZED) {
	console.log('\n[web-narrow] RESULT: 잴 것이 없다');
	process.exit(1);
}

/*
 * 나이 = (그 순간 진짜 자리 − 창이 본 자리) ÷ 걸음 속도.
 * 봇은 곧게 일정 속도로만 걸으므로 「거리 차」가 곧 「시간 차」다.
 * 진짜 자리는 세계가 봇에게 말해 준 값이다 — 창이 본 값과 <b>같은 뜻</b>의 숫자라 편향이 없다.
 */
/*
 * 진짜 자리 = <b>직선</b>이다 (걷는 사람은 등속으로 곧게 간다). 그러니 표본에 직선을 맞춰
 * 아무 시각에서나 읽는다.
 *
 * ⚠ 「그 시각 이전의 마지막 표본」으로 읽으면 안 된다 (실측 2026-08-12): 기준 시계의 표본이
 *   조금만 늦게 도착해도 계단이 한 칸 뒤로 밀려, 창이 <b>미래를 보는</b> 것처럼 음수 나이가 나온다
 *   (좁힌 구간에서 -2.1초). 직선은 그 흔들림을 안 탄다.
 */
const fitOver = (fromAt, toAt) => {
	const inside = truth.filter((one) => one.at >= fromAt - 500 && one.at <= toAt + 500);
	const used = inside.length >= 5 ? inside : truth;
	const t0 = used[0].at;
	let n = 0;
	let sumT = 0;
	let sumX = 0;
	let sumTT = 0;
	let sumTX = 0;
	for (const one of used) {
		const t = (one.at - t0) / 1000;
		n += 1;
		sumT += t;
		sumX += one.x;
		sumTT += t * t;
		sumTX += t * one.x;
	}

	const bottom = (n * sumTT) - (sumT * sumT);
	const slope = bottom === 0 ? 0 : ((n * sumTX) - (sumT * sumX)) / bottom;
	return { t0, slope, start: (sumX - (slope * sumT)) / n };
};

const roomyLine = fitOver(roomyFrom, roomyTo);
const tightLine = fitOver(tightFrom, tightTo);

check('기준 시계가 걷는 속도로 곧게 갔다 (안 그러면 잰 값이 뜻이 없다)',
	Math.abs(roomyLine.slope - WALK_SPEED) <= WALK_SPEED * 0.4,
	`맞춘 속도 ${roomyLine.slope.toFixed(2)}m/s (걸음 ${WALK_SPEED}m/s)`);

const agesOf = (list, fit) => {
	const trulyAt = (when) => fit.start + (fit.slope * ((when - fit.t0) / 1000));
	const out = [];
	for (const one of list) {
		if (one.id !== walkerDollId) continue;
		if (one.at < truth[0].at || one.at > truth[truth.length - 1].at) continue;

		const age = (trulyAt(one.at) - one.sawX) / WALK_SPEED;
		if (Number.isFinite(age) === false) continue;

		out.push(age);
	}

	return out;
};

const mean = (list) => list.reduce((sum, one) => sum + one, 0) / Math.max(1, list.length);

const roomy = agesOf(ages, roomyLine);
check('나이를 잴 표본이 있다', roomy.length >= LEAST_SAMPLES_ROOMY, `${roomy.length}개 (적어도 ${LEAST_SAMPLES_ROOMY}개 · 걷는 사람 번호 ${walkerDollId})`);
if (roomy.length < LEAST_SAMPLES_ROOMY) process.exit(1);

const half = Math.floor(roomy.length / 2);
const early = mean(roomy.slice(0, half));
const late = mean(roomy.slice(half));
const worst = Math.max(...roomy);

// ⚠ 나이는 <b>음수가 될 수 없다</b> — 창이 미래를 볼 수는 없다. 음수가 나오면 그건 세계가
//   빠른 게 아니라 <b>재는 자가 틀린 것</b>이다. 이 칸이 없어서 나이 -6.9초가 초록으로 통과했다
//   (CI 실측 2026-08-12). 판정은 위쪽만 막으면 반쪽이다.
const youngest = Math.min(...roomy);
check('나이가 음수가 아니다 (재는 자가 성한가)', youngest >= -RULER_SLACK_SECONDS, `가장 어린 순간 ${youngest.toFixed(2)}초 (자의 떨림 ±${RULER_SLACK_SECONDS}초)`);

check(`창이 보는 세계가 ${MAX_AGE_SECONDS}초 넘게 늙지 않았다`, late <= MAX_AGE_SECONDS,
	`나이 ${late.toFixed(2)}초 (가장 늙은 순간 ${worst.toFixed(2)}초)`);
check('시간이 가도 나이가 안 불어난다 (버퍼가 안 쌓인다)', late - early <= MAX_AGE_GROWTH_SECONDS,
	`앞 절반 ${early.toFixed(2)}초 → 뒤 절반 ${late.toFixed(2)}초`);

// ── 좁아진 뒤 ─────────────────────────────────────────────────────────
const squeezed = agesOf(squeezedAges, tightLine);
check('회선이 좁아져도 창은 계속 소식을 받는다', squeezed.length >= LEAST_SAMPLES_SQUEEZED, `${squeezed.length}판 (적어도 ${LEAST_SAMPLES_SQUEEZED}판)`);

if (squeezed.length >= LEAST_SAMPLES_SQUEEZED) {
	const tightHalf = Math.floor(squeezed.length / 2);
	const tightEarly = mean(squeezed.slice(0, tightHalf));
	const tightLate = mean(squeezed.slice(tightHalf));
	const tightWorst = Math.max(...squeezed);

	const tightYoungest = Math.min(...squeezed);
	check('좁아진 뒤에도 나이가 음수가 아니다', tightYoungest >= -RULER_SLACK_SECONDS, `가장 어린 순간 ${tightYoungest.toFixed(2)}초`);

	check(`회선이 초당 ${(SQUEEZED_BYTES_PER_SECOND / 1000).toFixed(0)}KB 로 좁아져도 ${MAX_SQUEEZED_AGE_SECONDS}초 넘게 안 늙는다`,
		tightLate <= MAX_SQUEEZED_AGE_SECONDS,
		`나이 ${tightLate.toFixed(2)}초 (가장 늙은 순간 ${tightWorst.toFixed(2)}초)`);
	check('좁아진 회선에서도 나이가 안 불어난다 (세계가 밀어 넣지 않고 건너뛴다)',
		tightLate - tightEarly <= MAX_AGE_GROWTH_SECONDS,
		`앞 절반 ${tightEarly.toFixed(2)}초 → 뒤 절반 ${tightLate.toFixed(2)}초`);
}

check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

console.log(`  ⓘ 사람 ${crowd}명 · 왕복 ${ONE_WAY_MS * 2}ms · 넓을 때 초당 ${(BYTES_PER_SECOND / 1000).toFixed(0)}KB → 좁을 때 ${(SQUEEZED_BYTES_PER_SECOND / 1000).toFixed(0)}KB`);

if (failures === 0) {
	console.log('[web-narrow] ✅ 좁은 회선에서도 창은 현재를 본다');
	process.exit(0);
}

console.log(`
[web-narrow] RESULT: ${failures}건`);
process.exit(1);
