#!/usr/bin/env node
// wm-web-long-outage-test.mjs — <b>세계가 한참 없어도 창은 기다리나</b> (TASK-WM-300).
//
// ★ 왜: 지금까지의 「다시 붙기」는 전부 <b>짧은</b> 끊김이었다(회선 몇 초, 세계 1초). 그런데 실제로는
//   길다: 배포는 40초 남짓 걸리고, 노트북은 잠들었다 깨고, 회선은 몇 분씩 나간다.
//   창의 다시 붙기는 0.5초에서 시작해 <b>10초까지</b> 늘어난다(link.mjs) — 그러면 한참 뒤에도
//   스스로 돌아와야 한다. 안 돌아오면 사람은 새로고침을 눌러야 하고, 그건 「고장」으로 기억된다.
//
// 재는 것: 세계를 <b>60초</b> 동안 죽였다가 살린 뒤
//   ① 창이 스스로 돌아오나 ② 얼마나 걸리나 ③ 돌아온 뒤 세계를 통째로 받나 ④ 창이 안 터지나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 기다린다 · 1 = 안 돌아온다 · 2 = 못 돌림
//
// ⚠ 지금 이 관문은 <b>다른 고장</b>에 먼저 걸린다 (TASK-WM-300 진단 중):
//   지연 <b>없는</b> 회선(같은 기계)으로 붙으면 창이 「붙었다」라고 하면서도 세계가 <b>텅 비어</b> 보인다
//   (사람 0 · 들판 0). 세계는 그 사람을 세고 있고 판도 110장 보냈는데 화면만 비어 있다.
//   실측: 첫 판들이 <b>순서가 뒤바뀌어</b> 온다 — seq 10 → 9 → 11 → 12.
//   그래서 아직 CI 에 안 걸었다. 그 고장을 고친 뒤에 건다.

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5510);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-outage-')), 'world.json');

/** 세계가 없는 시간 — 창의 다시 붙기 간격(최대 10초)보다 한참 길어야 뜻이 있다. */
const OUTAGE_MS = Number(process.env.WM_OUTAGE_MS || 60000);

/** 돌아오기까지 봐 주는 시간 — 간격이 10초까지 늘어나므로 그 몇 배는 준다. */
const COME_BACK_WITHIN_MS = 40000;

function cannotRun(message) {
	console.error(`[long-outage] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-outage-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

let world = null;
function startWorld() {
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

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

async function waitHealthy(milliseconds) {
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

startWorld();
if (await waitHealthy(120000) === false) {
	killWorld();
	cannotRun('세계가 안 떴다');
}

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(`http://127.0.0.1:${port}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

// ⚠ 재기 전에 <b>잴 것이 있는지</b>부터 (domain-wm.md § 관문 규율).
await page.waitForFunction(() => (window.__wmView.world().gatherables || 0) > 0, null, { timeout: 30000 })
	.catch(() => { /* 아래에서 잡는다 */ });

const before = await page.evaluate(() => window.__wmView.world());
check('창이 세계에 들어갔다', before.gatherables > 0, JSON.stringify(before));

if (before.gatherables === 0) {
	await browser.close();
	killWorld();
	cannotRun('창이 세계를 못 받았다 — 이 상태로는 「돌아오나」를 잴 수 없다');
}

// 돌아오는 순간을 창 안에서 지켜본다 — 다시 붙을 때마다 새 줄이 열린다.
await page.evaluate(() => {
	window.__wmBack = { at: -1, plates: 0 };
	const listen = () => {
		const now = window.__wmView.socket();
		if (!now || now === window.__wmHeard) return;

		window.__wmHeard = now;
		now.addEventListener('message', (event) => {
			let said;
			try { said = JSON.parse(event.data); } catch { return; }

			if (said.type !== 'world') return;
			if (window.__wmBack.at < 0) window.__wmBack.at = Date.now();

			window.__wmBack.plates += 1;
		});
	};

	listen();
	window.__wmWatch = setInterval(listen, 200);
});

// ── 세계를 한참 죽여 둔다 ─────────────────────────────────────────────
killWorld();
const wentDownAt = Date.now();
await page.evaluate(() => { window.__wmBack = { at: -1, plates: 0 }; });
await wait(OUTAGE_MS);

const whileGone = await page.evaluate(() => ({
	status: (document.getElementById('status') || {}).textContent,
	plates: window.__wmBack.plates,
}));

check('세계가 없는 동안 창이 그 사실을 말한다', /끊|다시|기다/.test(whileGone.status || ''),
	`화면: "${whileGone.status}"`);
check('세계가 없는 동안에는 소식도 없다 (있으면 재는 자가 이상한 것)', whileGone.plates === 0,
	`${whileGone.plates}장`);

startWorld();
const cameUpAt = Date.now();
if (await waitHealthy(60000) === false) {
	await browser.close();
	cannotRun('다시 켠 세계가 안 떴다');
}

// ── 창이 스스로 돌아오나 ──────────────────────────────────────────────
let back = { at: -1, plates: 0 };
{
	const until = Date.now() + COME_BACK_WITHIN_MS;
	while (Date.now() < until) {
		back = await page.evaluate(() => window.__wmBack);
		if (back.at > 0) break;

		await wait(300);
	}
}

const cameBackInMs = back.at < 0 ? -1 : back.at - cameUpAt;
const after = await page.evaluate(() => window.__wmView.world());

console.log(`  ⓘ 세계가 없던 시간 ${Math.round((cameUpAt - wentDownAt) / 1000)}초`
	+ ` · 살아난 뒤 창이 돌아오기까지 ${cameBackInMs < 0 ? '안 돌아옴' : cameBackInMs + 'ms'}`
	+ ` · 돌아온 뒤 받은 판 ${back.plates}장`);

check('한참 없어도 창은 스스로 돌아온다', cameBackInMs >= 0,
	cameBackInMs < 0 ? `${COME_BACK_WITHIN_MS / 1000}초를 기다려도 안 돌아왔다 — 사람은 새로고침을 눌러야 한다` : `${cameBackInMs}ms`);
check('돌아온 뒤 세계가 통째로 다시 보인다', after.gatherables >= before.gatherables,
	`들판 ${before.gatherables} → ${after.gatherables}`);
check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
killWorld();

if (failures === 0) {
	console.log(`[long-outage] ✅ 세계가 ${Math.round(OUTAGE_MS / 1000)}초 없어도 창은 스스로 돌아온다`);
	process.exit(0);
}

console.log(`\n[long-outage] RESULT: ${failures}건`);
process.exit(1);
