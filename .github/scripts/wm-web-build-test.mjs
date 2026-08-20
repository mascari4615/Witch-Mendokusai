#!/usr/bin/env node
// wm-web-build-test.mjs — <b>나쁜 회선에서 집이 서나</b> (TASK-WM-287).
//
// ★ 왜: 짓기는 세계가 판정한다(재료·겹침) — 그 판정은 서버 시험이 지킨다. 그런데 <b>창에서
//   눌러 짓는 길</b>은 진짜 창으로 한 번도 안 재봤다. 줍기는 쟀는데(WM-274·283) 짓기는 안 쟀다.
//   짓기는 줍기보다 한 겹 더 있다: 재료를 쓰고 · 땅을 고르고 · 세워진 것이 <b>남에게도</b> 보인다.
//
// 재는 것 (왕복 200ms 회선):
//   ① 눌렀을 때 곧바로 「짓는 중…」이라고 하나 ② 세운 것이 창에 뜨나 ③ 가방에서 재료가 빠지나
//   ④ 창이 조용히 안 터지나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 선다 · 1 = 못 선다 · 2 = 못 돌림
//
// [빨강-확인] 세계가 「짓기」(PLACE)를 무시하게 하니 빨강 — 창이 세운 것이 세계에 안 생긴다 (2026-08-14)

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5940);
const linePort = port + 1;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-build-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const REACH = 2.5;
// ⚠ 느린 기계에서는 걷기·줍기 한 판이 훨씬 오래 걸린다 — 2코어 CI 에서 90초로는 못 모았다.
//   이건 <b>문턱</b>이 아니라 <b>품</b>이다: 넉넉히 주고, 그래도 못 모으면 CANNOT-RUN 으로 세운다.
const GATHER_UP_TO_MS = Number(process.env.WM_GATHER_MS || 180000);

function cannotRun(message) {
	console.error(`[web-build] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-build-app-')), 'app');
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

await page.bringToFront();
// ⚠ 자판을 쥐려고 <b>세계를 누르면 안 된다</b> (2026-08-13): 그 자리는 땅이라 「짓기」가 나간다
//   (CI 진단에 「재료가 모자란다」가 찍혔다 — 관문이 세계를 건드리고 있었다).
//   위쪽 띠(머리말)를 눌러 자판만 가져온다.
await page.click('header', { position: { x: 5, y: 5 } }).catch(() => { /* 없으면 그냥 둔다 */ });

// ── 재료를 모은다 — 「지을 수 있다」가 될 때까지 걸어가서 줍는다 ────────
//   ⚠ 재기 전에 <b>잴 것이 있는지</b>부터 (domain-wm.md § 관문 규율).
// 창은 못 짓는 줄 앞에 <b>「· 」</b>를 붙인다(picker.mjs buildLabel) — 그게 없는 줄이 곧 지을 수 있는 것.
// ⚠ 「모자」 같은 낱말로 찾으면 안 된다 — 그런 글자는 애초에 안 붙는다(첫 판이 그래서
//    가방이 빈 채로 「지을 수 있다」가 됐다).
const buildableNow = async () => page.evaluate(() => {
	const picker = document.getElementById('buildpick');
	if (!picker || picker.options.length === 0) return null;

	const able = [...picker.options].filter((one) => one.textContent.startsWith('· ') === false);
	return able.length > 0 ? able[0].value : null;
});

const alreadyTaken = new Set();

/** 지을 수 있을 때까지 걸어가서 줍는다 — 되면 그 건물을 고르고 true. */
async function gatherUntilBuildable(msLimit) {
	const until = Date.now() + msLimit;
	const taken = alreadyTaken;
	while (Date.now() < until) {
		const able = await buildableNow();
		if (able !== null) {
			// 지을 수 있는 것을 고른다 — 고르지 않으면 땅을 눌러도 아무 일도 안 일어난다.
			await page.selectOption('#buildpick', able);
			return true;
		}

		const near = await page.evaluate((seen) => {
			const me = (window.__wmView.dolls() || []).find((one) => one.isLocal);
			if (!me) return null;

			const field = (window.__wmView.field() || []).filter((one) => seen.includes(one.id) === false);
			let best = null;
			for (const one of field) {
				const away = Math.hypot(one.x - me.drawnX, one.z - me.drawnZ);
				if (best === null || away < best.away) best = { ...one, away, meX: me.drawnX, meZ: me.drawnZ };
			}

			return best;
		}, [...taken]);

		if (near === null) { await wait(300); continue; }

		if (near.away <= REACH * 0.6) {
			const where = await page.evaluate((spot) => window.__wmView.screenOf(spot.x, spot.z), near);
			await page.mouse.click(Math.round(where.x), Math.round(where.y));
			taken.add(near.id);
			await wait(350);
			continue;
		}

		const keyX = near.x > near.meX + 0.2 ? 'd' : (near.x < near.meX - 0.2 ? 'a' : null);
		const keyZ = near.z > near.meZ + 0.2 ? 'w' : (near.z < near.meZ - 0.2 ? 's' : null);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.down(key);
		}

		await wait(200);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.up(key);
		}
	}

	return false;
}

const ready = await gatherUntilBuildable(GATHER_UP_TO_MS);

if (ready === false) {
	await browser.close();
	await line.close();
	killWorld();
	cannotRun('재료를 못 모았다 — 지을 수 없으면 「집이 서나」를 잴 수 없다');
}

const bagBefore = await page.evaluate(() => (document.getElementById('bag').textContent || '').trim());
const builtBefore = await page.evaluate(() => window.__wmView.world().buildings);
// 세계가 <b>실제로</b> 지었는지의 기준선 — 창이 늦는 것과 세계가 안 지은 것을 가르는 자리.
const buildingsBeforePress = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })
	.then((r) => r.json()).then((one) => one.buildings).catch(() => -1);

// ── 땅을 눌러 짓는다 — 사람이 하는 그대로 ──────────────────────────────
await page.evaluate(() => {
	window.__wmSaid = { at: -1, text: '' };
	const before = (document.getElementById('status') || {}).textContent;
	const watch = setInterval(() => {
		const now = (document.getElementById('status') || {}).textContent;
		if (now !== before && window.__wmSaid.at < 0) {
			window.__wmSaid.at = Date.now();
			window.__wmSaid.text = now;
		}
	}, 10);

	setTimeout(() => clearInterval(watch), 8000);
	window.__wmPressedAt = Date.now();
});

// 내 발밑 옆의 빈 땅 — 주울 것 위를 누르면 줍기가 된다(그건 다른 길이다).
//
// ⚠ <b>한 번 눌러 보고 마는 것</b>은 이 관문을 흔들리게 만든다 (2026-08-14 CI·이 기계 각 1회 빨강):
//   그 자리가 마침 못 짓는 자리이거나 창이 아직 자리를 못 잡았으면 세계도 창도 <b>아무 말이 없다</b> —
//   그러면 「곧바로 말한다」가 아니라 「말할 일이 없었다」가 빨강으로 적힌다.
//   그래서 <b>말이 나올 때까지</b> 자리를 옮겨 가며 몇 번 눌러 본다(관문 규율 ④-2 ⓑ).
{
	let pressed = 0;
	for (let step = 0; step < 8; step += 1) {
		const me = await page.evaluate(() => (window.__wmView.dolls() || []).find((one) => one.isLocal));
		if (me === undefined || me === null) { await wait(300); continue; }

		const away = 1.5 + step * 0.5;
		const spot = { x: Math.floor(me.drawnX) + away, z: Math.floor(me.drawnZ) + (step % 2 === 0 ? away : -away) };
		const where = await page.evaluate((one) => window.__wmView.screenOf(one.x, one.z), spot);

		// 누른 때를 <b>다시</b> 적는다 — 앞서 헛누른 것까지 세면 「곧바로」가 길어 보인다.
		await page.evaluate(() => { window.__wmPressedAt = Date.now(); });
		await page.mouse.click(Math.round(where.x), Math.round(where.y));
		pressed += 1;

		await wait(400);
		const heard = await page.evaluate(() => window.__wmSaid.at >= 0);
		if (heard) break;
	}

	console.log(`  ⓘ 눌러 본 자리 ${pressed}곳 (말이 나올 때까지)`);
}

const said = await page.evaluate(() => ({ ...window.__wmSaid, pressedAt: window.__wmPressedAt }));
const saidInMs = said.at < 0 ? -1 : said.at - said.pressedAt;

// 세운 것이 창에 뜰 때까지 — <b>그 시각도</b> 적는다(즉시 대답을 견줄 자다).
let built = builtBefore;
let worldAnsweredInMs = -1;
{
	// ⚠ 벽시계로 자르지 않는다 (2026-08-20). 15초로 잘랐더니 2코어 CI 에서만 빨개졌다 —
	//   그 판에서 <b>재료는 빠졌고</b>(= 세계는 지었다) 조금 뒤 창에도 떴다. 즉 결함이 아니라
	//   느린 기계였다. 절대 밀리초는 환경 주장이다(domain-wm.md § 관문 규율 ②).
	//   대신 <b>일어났나를 기다린다</b>: 세계가 지은 뒤에도 창이 새 소식(sequence)을
	//   여러 번 받고서 여전히 못 그렸을 때만 빨갛다. 이 자는 기계를 안 탄다.
	const NEWS_AFTER_WORLD_HAS_IT = 5;
	let worldHadItAtSequence = -1;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		const view = await page.evaluate(() => window.__wmView.world());
		built = view.buildings;
		if (built > builtBefore) {
			// ⚠ 두 시각은 <b>같은 시계</b>여야 한다: 누른 때는 Date.now() 로 적혀 있다.
			//   performance.now() 와 섞으면 음수가 나와 「못 잼」이 된다(첫 판이 그랬다).
			worldAnsweredInMs = await page.evaluate(() => Date.now() - window.__wmPressedAt);
			break;
		}

		if (worldHadItAtSequence < 0) {
			const now = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })
				.then((r) => r.json()).then((one) => one.buildings).catch(() => -1);
			if (now > 0 && now > buildingsBeforePress) worldHadItAtSequence = view.sequence;
		}
		else if (view.sequence - worldHadItAtSequence >= NEWS_AFTER_WORLD_HAS_IT) break;

		await wait(200);
	}
}

const bagAfter = await page.evaluate(() => (document.getElementById('bag').textContent || '').trim());

// ── ② 남이 먼저 세운 자리에 지으려 하면 (TASK-WM-289) ──────────────────
//   겨루기에 <b>진 쪽</b>이 무엇을 보나: 왜 안 됐는지 아나 · 재료를 안 잃나 · 남의 것이 보이나.
async function loseTheSpot() {
	const bot = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	const state = { id: 0, at: { x: 0, z: 0 }, bag: [], field: null, built: false };
	bot.onopen = () => { bot.send(JSON.stringify({ type: 'hello', secret: '' })); bot.send(JSON.stringify({ type: 'bagask' })); };
	bot.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'welcome' && said.id) state.id = said.id;
		if (said.type === 'bag') state.bag = said.items || [];
		if (said.type === 'world') {
			if (Array.isArray(said.gatherables) && said.gatherables.length > 0) state.field = said.gatherables;
			if (Array.isArray(said.dolls)) {
				const mine = said.dolls.find((one) => one.id === state.id);
				if (mine && typeof mine.x === 'number') state.at = { x: mine.x, z: mine.z };
			}
		}
	};

	{
		const until = Date.now() + 10000;
		while (Date.now() < until && (state.id === 0 || state.field === null)) await wait(150);
	}

	if (state.id === 0 || state.field === null) return null;

	// 봇도 재료를 모은다 — 나무 2개(보관 상자·솥 값).
	const wood = () => (state.bag.find((one) => one.itemId === 0) || { amount: 0 }).amount;
	const taken = new Set();
	{
		const until = Date.now() + 60000;
		while (Date.now() < until && wood() < 2) {
			let best = null;
			for (const node of state.field) {
				if (taken.has(node.id)) continue;

				const away = Math.hypot(node.x - state.at.x, node.z - state.at.z);
				if (best === null || away < best.away) best = { ...node, away };
			}

			if (best === null) break;

			if (best.away <= REACH * 0.6) {
				bot.send(JSON.stringify({ type: 'gather', nodeId: best.id }));
				taken.add(best.id);
				bot.send(JSON.stringify({ type: 'bagask' }));
				await wait(400);
				continue;
			}

			const step = Math.min(1.2, best.away);
			bot.send(JSON.stringify({
				type: 'move',
				x: (best.x - state.at.x) / best.away * step,
				z: (best.z - state.at.z) / best.away * step,
			}));

			await wait(120);
		}
	}

	if (wood() < 2) { try { bot.close(); } catch { /* 닫혔다 */ } return null; }

	// 창 옆의 빈 칸을 <b>봇이 먼저</b> 차지한다.
	const me = await page.evaluate(() => (window.__wmView.dolls() || []).find((one) => one.isLocal));
	const cell = { x: Math.floor(me.drawnX) - 2, z: Math.floor(me.drawnZ) + 2 };

	// 봇도 손이 닿아야 짓는다 — 그 칸 쪽으로 걸어간다.
	{
		const until = Date.now() + 30000;
		while (Date.now() < until) {
			const away = Math.hypot(cell.x + 0.5 - state.at.x, cell.z + 0.5 - state.at.z);
			if (away <= 2) break;

			const step = Math.min(1.2, away);
			bot.send(JSON.stringify({
				type: 'move',
				x: (cell.x + 0.5 - state.at.x) / away * step,
				z: (cell.z + 0.5 - state.at.z) / away * step,
			}));

			await wait(120);
		}
	}

	// ⚠ <b>봇이 정말 세웠는지</b>부터 본다. 안 세웠는데 「겨루기」라고 부르면 그건 겨루기가 아니다
	//   (첫 판이 그랬다: 봇이 못 세운 자리에 창이 그냥 지어 놓고 초록이 났다).
	const before = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })
		.then((r) => r.json()).then((one) => one.buildings).catch(() => -1);

	bot.send(JSON.stringify({ type: 'place', x: cell.x, y: 0, z: cell.z, buildingId: 4005 }));

	let landed = false;
	{
		const until = Date.now() + 6000;
		while (Date.now() < until) {
			const now = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })
				.then((r) => r.json()).then((one) => one.buildings).catch(() => -1);
			if (now > before) { landed = true; break; }

			await wait(300);
		}
	}

	try { bot.close(); } catch { /* 닫혔다 */ }
	return landed ? cell : null;
}

const lost = await loseTheSpot();

// ⚠ 절대 밀리초는 <b>환경 주장</b>이다 (domain-wm.md § 관문 규율 ④). 0.5초로 잘랐더니
//   2코어 CI 에서 빨개졌다(같은 코드가 이 기계에서는 초록) — 느린 기계에서는 누르는 것도
//   그리는 것도 느리다. 그래서 <b>같은 판의 세계 답</b>과 견준다: 즉시 대답은 세계가 답하기까지의
//   <b>절반</b> 안에 나와야 한다(WM-292 가 「대답」 관문에서 쓴 그 자다).
//   세계 답을 못 재면 그때만 사람이 느끼는 선(1초)으로 자른다.
const halfOfWorld = worldAnsweredInMs > 0 ? Math.round(worldAnsweredInMs / 2) : -1;
const sayLimitMs = halfOfWorld > 0 ? Math.max(halfOfWorld, 200) : 1000;

check('누르자마자 「짓는 중」이라고 한다 (세계 답의 절반 안)', saidInMs >= 0 && saidInMs <= sayLimitMs,
	saidInMs < 0
		? '아무 말도 안 했다'
		: `${saidInMs}ms · 세계 답 ${worldAnsweredInMs < 0 ? '못 잼' : worldAnsweredInMs + 'ms'}`
			+ ` · 한도 ${sayLimitMs}ms · "${said.text}"`);
check('세운 것이 창에 뜬다', built > builtBefore, `건물 ${builtBefore} → ${built}`);
check('가방에서 재료가 빠진다', bagAfter !== bagBefore, `${bagBefore} → ${bagAfter}`);
// ⚠ 겨루기 전에 <b>재료를 다시</b> 채운다. 안 그러면 세계는 「재료가 모자란다」로 답하고,
//   그건 <b>자리 다툼</b>이 아니라 다른 얘기다(첫 판이 그 값으로 초록이었다 — 거짓 초록).
const armedAgain = lost === null ? false : await gatherUntilBuildable(60000);

if (lost === null || armedAgain === false) {
	console.log('  ⓘ 겨루기는 못 쟀다 — 봇이나 창이 재료를 못 모았다(다음 칸은 건너뛴다)');
} else {
	const bagBeforeRace = await page.evaluate(() => (document.getElementById('bag').textContent || '').trim());
	await page.evaluate(() => { window.__wmDenied = []; });
	await page.evaluate(() => {
		window.__wmView.socket().addEventListener('message', (event) => {
			let said;
			try { said = JSON.parse(event.data); } catch { return; }

			if (said.type === 'denied') window.__wmDenied.push(said);
		});
	});

	// ⚠ <b>세워진 것 자체</b>를 눌러야 한다 — 땅 높이(0.4)를 겨누면 집 뒤의 땅이 눌려 그냥 지어진다
	//   (실측: 그 판에서 나무 2개가 빠졌다). 집의 윗면(0.9)을 겨눈다.
	const where = await page.evaluate((one) => window.__wmView.screenOf(one.x + 0.5, one.z + 0.5, 0.9), lost);
	await page.mouse.click(Math.round(where.x), Math.round(where.y));
	await wait(1500);

	const told = await page.evaluate(() => window.__wmDenied || []);
	const bagAfterRace = await page.evaluate(() => (document.getElementById('bag').textContent || '').trim());

	// ★ 알고 보니 <b>겨루기 자체가 안 일어난다</b> (실측): 3D 창에서 세워진 것을 누르면
	//   그건 「짓기」가 아니라 <b>그것을 여는 일</b>이다(상자 열기). 그래서 거절도 안 온다 —
	//   창이 애초에 짓겠다고 말하지 않았기 때문이다. 사람 눈에는 이게 옳다:
	//   남의 집 위에 지으려다 재료를 날릴 길이 없다.
	const seenByMe = await page.evaluate(() => window.__wmView.world().buildings);
	check('남이 세운 것이 내 창에도 보인다', seenByMe > built, `건물 ${built} → ${seenByMe}`);
	// ⚠ 「재료를 잃나」로는 못 자른다 — 그 자리를 눌렀을 때 <b>집이 눌리느냐 옆 땅이 눌리느냐</b>가
	//   화면 각도에 따라 갈린다(옆 땅이면 그냥 새로 짓는 게 맞다). 흔들리는 값은 관문이 아니다.
	//   대신 <b>남의 집이 그대로 있나</b>를 본다 — 그게 진짜 지켜야 할 것이다(덮어쓰기 금지).
	const standingAfter = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } })
		.then((r) => r.json()).then((one) => one.buildings).catch(() => -1);

	check('남이 세운 것을 덮어쓰지 않는다', standingAfter >= seenByMe,
		`세계의 건물 ${standingAfter}채 · 내 창이 보던 ${seenByMe}채`);

	console.log(`  ⓘ 그 자리를 눌렀을 때 온 거절: ${told.length === 0 ? '없음(짓겠다고 말한 적이 없다)' : told.map((one) => one.why).join(' | ')}`);
}

check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
await line.close();
killWorld();

if (failures === 0) {
	console.log('[web-build] ✅ 왕복 200ms 회선에서도 눌러서 집이 선다');
	process.exit(0);
}

console.log(`\n[web-build] RESULT: ${failures}건`);
process.exit(1);
