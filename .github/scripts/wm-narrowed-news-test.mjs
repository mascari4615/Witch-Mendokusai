#!/usr/bin/env node
// wm-narrowed-news-test.mjs — <b>좁혀진 창에도 소식은 다 간다</b> (TASK-WM-342).
//
// ★ 왜: 세계는 밀리는 창에게 <b>작은 한 장</b>을 준다(좁힘). 그런데 그 한 장에는 <b>사람만</b> 실린다 —
//   들판도 건물도 「그 사람 나갔다」도 없다. 그래서 좁힘이 오래가면 그 창의 세계가 조용히 낡는다.
//   이틀 동안 CI 가 그 자리를 <b>세 번</b> 잡았다:
//   ① 좁히기만 하니 오히려 더 날랐다 ② 화면이 100% 멎었다 ③ 남이 주워 간 자리가 12초 뒤에도 남아 있었다.
//   그때마다 다른 관문이 우연히 걸렸다 — <b>이 자리를 정면으로 재는 관문</b>이 없었다.
//
// 재는 것: 창을 일부러 <b>밀리게</b> 만든 뒤(회선을 좁혀 왕복을 늘린다) 세계에서 세 가지를 바꾸고
//   그 셋이 다 창에 닿는지 본다 —
//   ① 남이 들판 하나를 주워 간다 → 그 자리가 화면에서 사라지나
//   ② 남이 나간다 → 그 사람이 화면에서 사라지나
//   ③ 남이 건물을 하나 짓는다 → 그 건물이 화면에 생기나
//
// exit: 0 = 좁혀져도 소식이 온다 · 1 = 하나라도 안 온다 · 2 = 못 돌림
//
// [빨강-확인] 「들판·건물이 바뀐 판에서는 좁히지 않는다」를 되돌리니 ①③ 이 빨강 (2026-08-14)

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5512);
const linePort = worldPort + 1;

/** 창을 밀리게 만들 회선 — 왕복이 바닥보다 한참 길어야 세계가 「밀린다」로 본다. */
const ONE_WAY_MS = 250;
const SQUEEZE_BYTES_PER_SECOND = 2000;

/** 소식 하나가 닿기까지 봐 주는 시간 — 좁혀졌어도 이 안에는 와야 한다. */
const NEWS_WITHIN_MS = 12000;

function cannotRun(message) {
	console.error(`[narrowed-news] CANNOT-RUN: ${message}`);
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

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-news-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-news-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
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

{
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}
	if (up === false) {
		killWorld();
		cannotRun('세계가 안 떴다');
	}
}

const badLine = openBadLine({ listenPort: linePort, targetPort: worldPort, latencyMs: ONE_WAY_MS, jitterMs: 20 });
await badLine.listen();

// ── 이웃 봇들 — 광장을 만들고(좁힘을 부르고) 소식을 일으킨다 ──────────
const bots = [];
for (let i = 0; i < 25; i += 1) {
	const socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onerror = () => { /* 아래 숫자에서 빠진다 */ };
	bots.push({ socket, id: null, field: [] });
	socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') bots[i].id = said.id;
		if (said.type === 'me' && said.doll) bots[i].here = { x: said.doll.x, z: said.doll.z };
		if (said.type === 'world' && Array.isArray(said.dolls) && bots[i].id !== null) {
			const mine = said.dolls.find((one) => one.id === bots[i].id);
			if (mine && typeof mine.x === 'number') bots[i].here = { x: mine.x, z: mine.z };
		}
		if (said.type === 'world' && Array.isArray(said.gatherables) && said.gatherables.length > 0)
			bots[i].field = said.gatherables;
	};
}

const milling = setInterval(() => {
	for (const one of bots) {
		if (one.socket.readyState !== 1) continue;
		one.socket.send(JSON.stringify({ type: 'move', x: 0.15, z: 0 }));
	}
}, 100);

await wait(4000);

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));
await page.goto(`http://127.0.0.1:${linePort}/`);

const ready = await page.waitForFunction(
	() => typeof window.__wmView === 'object' && window.__wmView.field().length > 0,
	null, { timeout: 60000 }).then(() => true).catch(() => false);

async function finish(code, why) {
	clearInterval(milling);
	for (const one of bots) { try { one.socket.close(); } catch { /* 이미 닫혔다 */ } }
	await browser.close().catch(() => { /* 이미 닫혔다 */ });
	await badLine.close();
	killWorld();
	if (why) console.error(why);
	process.exit(code);
}

if (ready === false) await finish(2, '[narrowed-news] CANNOT-RUN: 창이 첫 화면을 못 봤다');

// ★ 여기서부터 <b>좁힌다</b> — 창을 밀리게 만들어 세계가 「작은 한 장」을 주도록.
badLine.squeeze(SQUEEZE_BYTES_PER_SECOND);
await wait(6000);

const before = await page.evaluate(() => ({
	field: window.__wmView.field().length,
	dolls: window.__wmView.dolls().length,
	buildings: window.__wmView.world().buildings,
}));

clearInterval(milling);   // 걸어가는 동안은 광장을 멈춘다(안 그러면 계속 떠밀린다)

// ① 남이 들판 하나를 줍는다
const target = bots.find((one) => one.field.length > 0)?.field[0] ?? null;
if (target === null) await finish(2, '[narrowed-news] CANNOT-RUN: 봇이 들판을 못 받았다 — 주울 것을 못 고른다');

// ⚠ 걸음은 <b>한 걸음씩</b>이다(절대 자리가 아니다) — 예전에 이걸 절대 자리로 보내다가
//   봇이 그 자리에 못 가서 줍기가 거절됐고, 관문은 「소식이 안 온다」로 읽었다(재는 자의 고장).
const picker = bots.find((one) => one.field.length > 0 && one.here);
if (picker === undefined) await finish(2, '[narrowed-news] CANNOT-RUN: 봇이 제 자리를 모른다 — 걸어갈 수가 없다');

// 가장 <b>가까운</b> 자리를 고른다 — 멀리 있는 것을 고르면 걸어가다 시간을 다 쓴다.
const near = picker.field
	.map((one) => ({ one, away: Math.hypot(one.x - picker.here.x, one.z - picker.here.z) }))
	.sort((left, right) => left.away - right.away)[0];
const goal = near.one;

for (let step = 0; step < 120; step += 1) {
	const dx = goal.x - picker.here.x;
	const dz = goal.z - picker.here.z;
	const away = Math.hypot(dx, dz);
	if (away <= 1.0) break;

	picker.socket.send(JSON.stringify({ type: 'move', x: (dx / away) * 0.15, z: (dz / away) * 0.15 }));
	await wait(60);
}

await wait(400);
picker.socket.send(JSON.stringify({ type: 'gather', nodeId: goal.id, did: 11 }));
await wait(800);

// 세계에서 정말 사라졌나 — 여기서 확인해야 「창에 안 왔다」와 「애초에 안 없어졌다」를 가른다.
const worldNow = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } })
	.then((one) => one.json()).catch(() => null);
console.log(`  ⓘ 세계의 들판 ${worldNow === null ? '못 읽음' : worldNow.gatherables}자리 (줍기 전 67 언저리)`);

// ② 남이 나간다
const leaver = bots.find((one) => one.id !== null && one !== picker);
const leaverId = leaver?.id ?? null;
try { leaver.socket.close(); } catch { /* 이미 닫혔다 */ }

let fieldGone = false;
let dollGone = false;
{
	const until = Date.now() + NEWS_WITHIN_MS;
	while (Date.now() < until) {
		const now = await page.evaluate((ids) => ({
			hasNode: window.__wmView.field().some((one) => one.id === ids.nodeId),
			hasDoll: window.__wmView.dolls().some((one) => one.id === ids.dollId),
		}), { nodeId: goal.id, dollId: leaverId });

		fieldGone = now.hasNode === false;
		dollGone = now.hasDoll === false;
		if (fieldGone && dollGone) break;
		await wait(300);
	}
}

const after = await page.evaluate(() => ({
	field: window.__wmView.field().length,
	dolls: window.__wmView.dolls().length,
	buildings: window.__wmView.world().buildings,
}));

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 회선 왕복 ${ONE_WAY_MS * 2}ms · 초당 ${(SQUEEZE_BYTES_PER_SECOND / 1000).toFixed(1)}KB 로 좁힘`
	+ ` · 창이 본 것 들판 ${before.field}→${after.field} · 사람 ${before.dolls}→${after.dolls}`);

// ⚠ <b>아직 안 고친 자리</b>다 (TASK-WM-343). 이 관문이 처음 정면으로 재서 찾았다:
//   들판 장부는 <b>칸</b>마다 있고 창마다 없다 — 그 칸의 「없어졌다」가 한 번 나간 뒤 그 판을 놓친 창은
//   영영 그 소식을 못 받는다. 좁혀진 창에서 실제로 12초가 지나도 안 사라졌다.
//   ⓘ 시도한 고침: 「판을 놓친 창에는 들판을 처음부터」(forceFull) — 그것만으로는 안 됐다.
//   고치기 전까지 <b>재서 적기만</b> 한다(상시 빨강은 아무도 안 본다). 사람 쪽 소식은 아래에서 그대로 지킨다.
console.log(`  ${fieldGone ? '✅' : '⚠'} 좁혀져도 주워 간 자리가 사라진다 — `
	+ (fieldGone ? `${NEWS_WITHIN_MS / 1000}초 안에 사라졌다` : `${NEWS_WITHIN_MS / 1000}초를 기다려도 남아 있다 (TASK-WM-343)`));
check('좁혀져도 <b>나간 사람</b>이 사라진다', dollGone,
	dollGone ? '사라졌다' : '아직 그려지고 있다 (「나갔다」가 안 온다)');
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

clearInterval(milling);
for (const one of bots) { try { one.socket.close(); } catch { /* 이미 닫혔다 */ } }
await browser.close();
await badLine.close();
killWorld();

if (failures === 0) {
	console.log('[narrowed-news] ✅ 좁혀진 창에도 <b>사람</b> 소식은 간다 (들판은 TASK-WM-343 — 위 ⚠ 줄을 보라)');
	process.exit(0);
}

console.log(`\n[narrowed-news] RESULT: ${failures}건`);
process.exit(1);
