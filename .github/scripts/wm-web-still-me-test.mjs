#!/usr/bin/env node
// wm-web-still-me-test.mjs — <b>새로고침해도 나인가</b> (TASK-WM-277).
//
// ★ 왜: 세계가 사람을 알아보는 규칙은 서버 시험이 지킨다(WM-218·259). 그런데 사람이 실제로
//   겪는 것은 <b>창을 새로 여는 일</b>이다 — 새로고침·탭 닫았다 열기·컴퓨터 잠들었다 깨기.
//   그때 열쇠를 못 들고 가면 그 사람은 <b>남</b>이 된다: 가방도 자리도 남의 것이 된다.
//   이 자리는 진짜 창으로 한 번도 안 쟀다(서버 시험은 열쇠를 손으로 들고 다닌다).
//
// 재는 것 (나쁜 회선 왕복 200ms 위에서):
//   ① 주운 뒤 새로고침하면 <b>같은 사람</b>인가 ② 가방이 그대로인가 ③ 서 있던 자리가 그대로인가
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 나다 · 1 = 남이 된다 · 2 = 못 돌림
//
// [빨강-확인] 창이 열쇠를 안 적게 하니 3건 빨강 — 가방 석탄1 → 빈손 · 자리 (0.25,5.08) → (0,0)

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5670);
const linePort = port + 1;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-stillme-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const REACH = 2.5;

function cannotRun(message) {
	console.error(`[still-me] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-stillme-app-')), 'app');
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

/** 이 창이 지금 아는 「나」 — 신원 번호 · 가방 · 세계가 아는 내 자리. */
async function whoAmI() {
	return page.evaluate(() => {
		const me = (window.__wmView.dolls() || []).find((one) => one.isLocal) || null;
		const shown = (document.getElementById('bag').textContent || '').trim();
		const bag = shown === '비었다' ? '' : shown;

		return {
			identity: (document.getElementById('me').textContent || '').trim(),
			bag,
			x: me ? me.serverX : null,
			z: me ? me.serverZ : null,
		};
	});
}

async function openWorld() {
	await page.goto(`http://127.0.0.1:${linePort}/`);
	await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
		.catch(() => { /* 아래 칸이 잡는다 */ });
	await page.waitForFunction(
		() => (window.__wmView.dolls() || []).some((one) => one.isLocal), null, { timeout: 30000 })
		.catch(() => { /* 아래 칸이 잡는다 */ });
}

await openWorld();
check('나쁜 회선으로 세계에 붙었다', await page.evaluate(() => typeof window.__wmView === 'object'));

// ── 무언가 <b>남길 것</b>을 만든다: 걸어가서 줍는다 ────────────────────
{
	const until = Date.now() + 30000;
	let took = false;
	while (Date.now() < until && took === false) {
		const near = await page.evaluate((reach) => {
			const me = (window.__wmView.dolls() || []).find((one) => one.isLocal);
			if (!me) return null;

			const field = window.__wmView.field() || [];
			let best = null;
			for (const one of field) {
				const away = Math.hypot(one.x - me.drawnX, one.z - me.drawnZ);
				if (best === null || away < best.away) best = { ...one, away, meX: me.drawnX, meZ: me.drawnZ };
			}

			if (best === null) return null;
			return { ...best, close: best.away <= reach * 0.6 };
		}, REACH);

		if (near === null) { await wait(300); continue; }

		if (near.close) {
			// ⚠ 3D 창에서 줍기는 <b>눌러 고르는 것</b>이라(지도의 E 키가 아니다) 화면 좌표가 필요하다.
			//   이 관문이 재려는 건 「새로고침해도 나인가」지 겨냥이 아니므로, 세계에 바로 청한다.
			await page.evaluate((nodeId) => {
				window.__wmView.socket().send(JSON.stringify({ type: 'gather', nodeId }));
			}, near.id);

			await wait(800);
			const bag = (await whoAmI()).bag;
			took = bag.length > 0;
			continue;
		}

		const keyX = near.x > near.meX + 0.2 ? 'd' : (near.x < near.meX - 0.2 ? 'a' : null);
		const keyZ = near.z > near.meZ + 0.2 ? 'w' : (near.z < near.meZ - 0.2 ? 's' : null);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.down(key);
		}

		await wait(120);
		for (const key of [keyX, keyZ]) {
			if (key !== null) await page.keyboard.up(key);
		}
	}
}

const before = await whoAmI();
check('무언가를 주웠다 (남길 것이 있어야 잰다)', before.bag.length > 0, before.bag || '가방이 비었다');
check('세계가 나를 사람으로 안다', before.identity.includes('번 사람'), before.identity);

if (before.bag.length === 0 || before.identity.includes('번 사람') === false) {
	await browser.close();
	await line.close();
	killWorld();
	console.log(`\n[still-me] RESULT: ${failures}건`);
	process.exit(1);
}

// ── 새로고침 — 사람이 늘 하는 그것 ─────────────────────────────────────
await page.reload();
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });
await page.waitForFunction(
	() => (window.__wmView.dolls() || []).some((one) => one.isLocal), null, { timeout: 30000 })
	.catch(() => { /* 아래에서 다시 본다 */ });

// ★ <b>잴 것이 올 때까지 기다린다</b> (관문 규율 ②·④-2, TASK-WM-392).
//   CI 에서 「(0.54, 4.76) → (null, null)」로 빨갰다 — 자리가 <b>틀린</b> 것이 아니라
//   그 자리를 아직 <b>못 받은</b> 것이다(나쁜 회선 + 느린 러너 + 새로고침 직후).
//   시간을 박지 말고 <b>왔나</b>를 보면서 기다린다. 그래도 안 오면 못 잰 것이다(0 을 빨강으로 안 적는다).
let after = await whoAmI();
{
	const until = Date.now() + 60000;
	while (Date.now() < until && after.x === null) {
		await wait(500);
		after = await whoAmI();
	}
}

if (after.x === null) {
	await browser.close();
	await line.close();
	killWorld();
	cannotRun('새로고침 뒤 내 인형이 안 왔다 — 이 판에서는 자리를 못 쟀다');
}

await wait(600);
after = await whoAmI();

check('새로고침해도 같은 사람이다', after.identity === before.identity,
	`${before.identity} → ${after.identity}`);
check('가방이 그대로다', after.bag === before.bag, `${before.bag} → ${after.bag || '(비었다)'}`);
check('서 있던 자리가 그대로다',
	after.x !== null && Math.hypot(after.x - before.x, after.z - before.z) <= 1.5,
	`(${before.x}, ${before.z}) → (${after.x}, ${after.z})`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
await line.close();
killWorld();

if (failures === 0) {
	console.log('[still-me] ✅ 나쁜 회선에서 새로고침해도 나이고, 가방도 자리도 그대로다');
	process.exit(0);
}

console.log(`\n[still-me] RESULT: ${failures}건`);
process.exit(1);
