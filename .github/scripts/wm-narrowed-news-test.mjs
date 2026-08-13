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

/**
 * 회선을 <b>얼마나</b> 좁힐까 — 정상 수요의 이 배수 (TASK-WM-343 진단에서 배웠다).
 *
 * ★ 처음에는 초당 2KB 로 못 박았다. 그런데 창이 받은 들판 소식을 적어 보니 12초 동안 <b>한 장</b>뿐이었다 —
 *   좁힘이 수요보다 훨씬 좁아 창이 <b>굶고</b> 있었던 것이다. 그건 「소식이 안 온다」가 아니라
 *   「보낼 폭이 없다」이고, 제품 결함이 아니라 <b>자극이 과했던 것</b>이다.
 *   그래서 좁은 회선 관문과 같은 방식으로 <b>정상 수요를 재서</b> 그 몫으로 좁힌다.
 */
const SQUEEZE_OF_DEMAND = 0.8;

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
	stdio: process.env.WM_SEND_DEBUG === '1' ? 'inherit' : 'ignore',
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

// ★ <b>창이 받은 들판 소식만</b> 따로 적어 둔다 (진단 도구) — 안 사라졌을 때 무엇이 왔는지 봐야
//   세계 탓인지 창 탓인지 가른다. 세계에 로그를 켜면 박자가 바뀌어 증상이 숨는다(실측).
await page.evaluate(() => {
	window.__wmFieldTrail = [];
	window.__wmView.socket().addEventListener('message', (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type !== 'world') return;
		if (said.gatherables === undefined && said.fieldGone === undefined) return;

		window.__wmFieldTrail.push({
			at: Date.now(),
			seq: said.sequence,
			delta: said.fieldChanged === true,
			싣고온자리: (said.gatherables || []).length,
			없어졌다: (said.fieldGone || []).length,
		});
		if (window.__wmFieldTrail.length > 40) window.__wmFieldTrail.shift();
	});
});

// ★ 여기서부터 <b>좁힌다</b> — 창을 밀리게 만들어 세계가 「작은 한 장」을 주도록.
//   좁힐 값은 <b>선 위의 정상 수요</b>에서 뽑는다(창이 세는 바이트는 푼 뒤라 부풀어 있다).
const carriedBefore = badLine.peek().reduce((sum, one) => sum + (one.carried || 0), 0);
await wait(4000);
const carriedAfter = badLine.peek().reduce((sum, one) => sum + (one.carried || 0), 0);
const demand = Math.max(1200, Math.round((carriedAfter - carriedBefore) / 4));
const squeezeTo = Math.round(demand * SQUEEZE_OF_DEMAND);
console.log(`  ⓘ 정상 수요 초당 ${(demand / 1000).toFixed(1)}KB → 좁힘 초당 ${(squeezeTo / 1000).toFixed(1)}KB`);

badLine.squeeze(squeezeTo);
await wait(6000);

const before = await page.evaluate(() => ({
	field: window.__wmView.field().length,
	dolls: window.__wmView.dolls().length,
	buildings: window.__wmView.world().buildings,
}));

clearInterval(milling);   // 걸어가는 동안은 광장을 멈춘다(안 그러면 계속 떠밀린다)

// ① 남이 들판 하나를 줍는다
// ⚠ <b>창이 지금 보고 있는</b> 자리를 골라야 한다 (2026-08-14 실측): 봇이 아는 자리 중에 고르면
//   창의 관심 반경 밖일 수 있고, 그러면 「소식이 안 온다」가 아니라 <b>원래 안 오는 자리</b>다.
//   그 착각으로 제품을 두 번 헛고칠 뻔했다(세계 로그를 찍어 보고 알았다: 창은 좁혀지지도 않았다).
const seenByWindow = await page.evaluate(() => window.__wmView.field().map((one) => ({ id: one.id, x: one.x, z: one.z })));
if (seenByWindow.length === 0) await finish(2, '[narrowed-news] CANNOT-RUN: 창이 들판을 못 보고 있다');

// ⚠ 걸음은 <b>한 걸음씩</b>이다(절대 자리가 아니다) — 예전에 이걸 절대 자리로 보내다가
//   봇이 그 자리에 못 가서 줍기가 거절됐고, 관문은 「소식이 안 온다」로 읽었다(재는 자의 고장).
// ⚠ 새 봇을 <b>지금</b> 부른다 (2026-08-14): 광장 봇들은 4초 동안 밀려 다녀 자기 자리를 잃었고,
//   그 상태로 걸어가게 했더니 줍기가 계속 거절됐다 — 관문은 그걸 「소식이 안 온다」로 잘못 읽었다.
//   갓 들어온 봇은 원점 근처에 서므로 창이 보는 자리로 걸어가기 쉽다.
const picker = { socket: new WebSocket(`ws://127.0.0.1:${worldPort}/ws`), id: null, here: null, field: [] };
picker.socket.onopen = () => picker.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
picker.socket.onerror = () => { /* 아래가 잡는다 */ };
picker.socket.onmessage = (event) => {
	let said;
	try { said = JSON.parse(String(event.data)); } catch { return; }
	if (said.type === 'welcome') picker.id = said.id;
	if (said.type === 'me' && said.doll) picker.here = { x: said.doll.x, z: said.doll.z };
	if (said.type === 'world' && Array.isArray(said.dolls) && picker.id !== null) {
		const mine = said.dolls.find((one) => one.id === picker.id);
		if (mine && typeof mine.x === 'number') picker.here = { x: mine.x, z: mine.z };
	}
	if (said.type === 'world' && Array.isArray(said.gatherables) && said.gatherables.length > 0)
		picker.field = said.gatherables;
	if (said.type === 'world' && said.fieldChanged && Array.isArray(said.fieldGone))
		picker.field = picker.field.filter((one) => said.fieldGone.includes(one.id) === false);
};

{
	const until = Date.now() + 15000;
	while (Date.now() < until && (picker.here === null || picker.field.length === 0)) await wait(200);
}
if (picker.here === null) await finish(2, '[narrowed-news] CANNOT-RUN: 새 봇이 제 자리를 못 받았다');
bots.push(picker);

// 창이 보는 자리 중 <b>봇에게 가장 가까운</b> 것 — 걸어가는 시간을 아낀다.
const goal = seenByWindow
	.map((one) => ({ one, away: Math.hypot(one.x - picker.here.x, one.z - picker.here.z) }))
	.sort((left, right) => left.away - right.away)[0].one;

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
// 줍기가 <b>정말</b> 됐나 — 봇(곧은 회선)의 눈으로 확인한다. 이걸 안 보면 「소식이 안 왔다」와
// 「애초에 안 없어졌다」를 못 가른다(그 착각으로 제품을 헛고칠 뻔했다).
const pickerStillSees = picker.field.some((one) => one.id === goal.id);
console.log(`  ⓘ 세계의 들판 ${worldNow === null ? '못 읽음' : worldNow.gatherables}자리`
	+ ` · 주운 자리 ${goal.id} — 봇(곧은 회선)에게 ${pickerStillSees ? '아직 보인다(줍기 실패?)' : '사라졌다(줍기 성공)'}`);

if (pickerStillSees)
	await finish(2, `[narrowed-news] CANNOT-RUN: 줍기가 안 됐다 (자리 ${goal.id} 가 봇에게도 남아 있다) — 잴 것이 없다`);

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

if (fieldGone === false) {
	const trail = await page.evaluate(() => window.__wmFieldTrail.slice(-8));
	console.log('  ⓘ 창이 받은 마지막 들판 소식들:');
	for (const one of trail)
		console.log(`     seq=${one.seq} ${one.delta ? '델타' : '통째'} 싣고온자리=${one.싣고온자리} 없어졌다=${one.없어졌다}`);
	console.log(`     (찾던 자리 ${goal.id})`);
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

console.log(`  ⓘ 회선 왕복 ${ONE_WAY_MS * 2}ms · 초당 ${(squeezeTo / 1000).toFixed(1)}KB 로 좁힘`
	+ ` · 창이 본 것 들판 ${before.field}→${after.field} · 사람 ${before.dolls}→${after.dolls}`);

// ⓘ <b>고친 자리</b>다 (TASK-WM-343 — 아래 기록은 어떻게 좁혀 갔는지의 자취다):
//   들판 장부는 <b>칸</b>마다 있고 창마다 없다 — 그 칸의 「없어졌다」가 한 번 나간 뒤 그 판을 놓친 창은
//   영영 그 소식을 못 받는다. 좁혀진 창에서 실제로 12초가 지나도 안 사라졌다.
//   ⓘ 재현율이 <b>판마다 흔들린다</b>: 같은 코드로 3/3 · 2/3 · 1/3 을 다 봤다 (2026-08-14).
//     그래서 <b>한 판으로는 판단하지 마라</b> — 고쳤는지 보려면 같은 코드로 여러 판을 돌려 비율을 견줘야 한다.
//     (오늘 그 함정에 두 번 빠졌다: 한 판이 초록이라 「고쳐졌다」로 읽었고, 한 판이 빨강이라 「나빠졌다」로 읽었다.)
//   ⓘ <b>자극이 과했다</b>: 처음엔 초당 2KB 로 못 박았는데, 창이 받은 들판 소식을 적어 보니
//     12초 동안 <b>한 장</b>뿐이었다 — 좁힘이 수요보다 훨씬 좁아 창이 굶고 있었다.
//     그건 「소식이 안 온다」가 아니라 「보낼 폭이 없다」다. 이제 <b>정상 수요의 0.8배</b>로 좁힌다.
//     (그 사실은 세계 로그가 아니라 <b>창이 받은 것을 적어</b> 알았다 — 세계 로그는 박자를 바꿔 증상을 숨긴다.)
//   ⓘ 지금까지 고친 것(각각 3판씩 재서): 판 놓친 창엔 들판 통째 · FieldNews 로 셈 분리(칸마다 지금+바로 앞) ·
//     자극까지 고친 지금 <b>3판 중 2판 통과</b>. 남은 1판의 자취는 <b>늘 같은 모양</b>이다:
//     창이 받은 들판 소식이 「통째 67자리」 <b>하나뿐</b>이고 그 뒤 12초 동안 아무것도 없다.
//     ⇒ 남은 것은 <b>차례</b> 문제다: 회선이 수요의 0.8배면 사람 소식이 폭을 다 쓰고,
//       한 번짜리 들판 소식은 줄을 서다 못 나간다.
//     그 길은 넣었다(작은 한 장에 들판을 같이 싣는다). 그런데도 이 관문은 3판 중 1판 남는다.
//   ⓘ <b>봇 자와 갈리는 지점</b>을 찾았다 (2026-08-14): 같은 조건을 봇으로 재면 8판 중 7·6·6 으로 <b>온다</b>.
//     차이는 <b>숨소리(beat)</b>다 — 진짜 창은 0.25초마다 도장을 돌려주므로 세계가 「밀린다」로 보고
//     좁힘·절반 박자 길로 보내지만, 봇은 도장을 안 보내 왕복이 0 이라 <b>늘 보통 길</b>로 받는다.
//     ⇒ 남은 것은 <b>밀린 길 위에서</b> 소식이 폭에 밀려 못 나가는 자리다.
//   ✅ <b>고쳤다</b> (2026-08-14): 회선이 계속 막히면 세계가 그 창을 <b>영영 건너뛰고</b> 있었다 —
//     자취가 아예 비어 있었다(들판 소식 0장). 이제 오래 밀린 창에는 20판(1초)마다 한 번
//     <b>가장 작은 한 장</b>(사람 6명 + 들판)을 밀어 넣는다.
//     실측: 봇 자 8판 중 <b>0판 → 7판</b> · 이 관문 3판 중 <b>3판</b> 통과.
//     다음 후보 = 창이 좁힘에서 돌아오는 <b>그 판</b>의 순서(작은 한 장 → 통째 판 사이에 델타가 끼는지)를
//     세계 쪽 로그로 한 판만 따라가 보기. 통과한 한 판은 서버에 로그를 켠 판이었다 —
//     로그가 박자를 바꾸면 안 나는 것으로 보아 <b>때가 맞아야</b> 나는 자리다.
//   ⓘ 지금까지 밝혀진 것: 들판 델타는 <b>칸 장부</b>(lastCellField)에서 만들어진다. 그 칸의 「없어졌다」가
//     한 판에 한 번 나가고 장부가 갱신되면, <b>그 판을 건너뛴 창</b>은 그 소식을 영영 못 받는다
//     (다음 판에는 이미 없어진 것으로 쳐서 아무것도 안 실린다).
//   ⓘ 시도한 고침: 「판을 놓친 창에는 들판을 처음부터」(forceFull) · 「들판이 바뀐 판에서는 좁히지 않기」
//     — 둘 다 넣었는데도 2/3 으로 난다. 다음 = 창마다 <b>마지막으로 받은 들판 판번호</b>를 두고
//     그보다 뒤처지면 통째로 다시 주는 길(칸 장부에 기대지 않는다).
//   고치기 전까지 <b>재서 적기만</b> 한다(상시 빨강은 아무도 안 본다). 사람 쪽 소식은 아래에서 그대로 지킨다.
check('좁혀져도 <b>주워 간 자리</b>가 사라진다', fieldGone,
	fieldGone ? `${NEWS_WITHIN_MS / 1000}초 안에 사라졌다` : `${NEWS_WITHIN_MS / 1000}초를 기다려도 남아 있다`);
check('좁혀져도 <b>나간 사람</b>이 사라진다', dollGone,
	dollGone ? '사라졌다' : '아직 그려지고 있다 (「나갔다」가 안 온다)');
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

clearInterval(milling);
for (const one of bots) { try { one.socket.close(); } catch { /* 이미 닫혔다 */ } }
await browser.close();
await badLine.close();
killWorld();

if (failures === 0) {
	console.log('[narrowed-news] ✅ 좁혀진 창에도 소식은 다 간다');
	process.exit(0);
}

console.log(`\n[narrowed-news] RESULT: ${failures}건`);
process.exit(1);
