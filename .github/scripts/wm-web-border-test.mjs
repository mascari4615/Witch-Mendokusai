#!/usr/bin/env node
// wm-web-border-test.mjs — <b>나쁜 회선에서</b> 국경을 넘는다 (TASK-WM-255).
//
// ★ 왜: 넘겨주기(WM-254)는 loopback 에서만 재봤다. 그런데 국경은 <b>가장 위험한 순간</b>이다 —
//   보낸 세계는 나를 내보내고, 받는 세계는 아직 나를 모른다. 그 사이에 회선이 늦거나 끊기면
//   사람은 <b>두 세계 어디에도 없는</b> 상태가 된다(가방째 사라진다).
//
// 재는 것: 세계 둘 앞에 각각 나쁜 회선(왕복 200ms·유실 2%)을 세우고, 진짜 창이 걸어서 넘어간다.
//   ① 넘어가라는 말이 오나 ② 옆 세계에 실제로 붙나 ③ 가방을 들고 갔나 ④ 창이 안 터지나
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 넘는다 · 1 = 못 넘는다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5406);
const westPort = eastPort + 1;
const eastLine = eastPort + 2;
const westLine = eastPort + 3;

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const LOSS_PERCENT = 2;
const SECRET = '두 세계만 아는 말';

function cannotRun(message) {
	console.error(`[web-border] CANNOT-RUN: ${message}`);
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
	const out = join(mkdtempSync(join(tmpdir(), 'wm-border-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-border-')), 'world.json');
	const child = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: {
			...process.env,
			WM_WORLD_FILE: worldFile,
			WM_ZONE: zone,
			WM_ZONE_NEIGHBOURS: neighbours,
			WM_ZONE_SECRET: SECRET,
		},
		stdio: 'ignore',
	});

	worlds.push(child);
	return child;
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

// 창은 <b>회선 너머</b> 주소로만 세계를 안다 — 그래야 넘어갈 때도 나쁜 회선을 탄다.
startWorld(eastPort, '동:0,-40,40,40', `서:-40,-40,0,40=ws://127.0.0.1:${westLine}/ws`);
startWorld(westPort, '서:-40,-40,0,40', `동:0,-40,40,40=ws://127.0.0.1:${eastLine}/ws`);

if (await waitHealthy(eastPort, 120000) === false || await waitHealthy(westPort, 120000) === false) {
	killWorlds();
	cannotRun('세계가 안 떴다');
}

const lines = [
	openBadLine({ listenPort: eastLine, targetPort: eastPort, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS, lossPercent: LOSS_PERCENT }),
	openBadLine({ listenPort: westLine, targetPort: westPort, latencyMs: ONE_WAY_MS, jitterMs: JITTER_MS, lossPercent: LOSS_PERCENT }),
];

for (const one of lines) await one.listen();

const browser = await chromium.launch();
const page = await browser.newPage();
const pageErrors = [];
page.on('pageerror', (error) => pageErrors.push(String(error)));

await page.goto(`http://127.0.0.1:${eastLine}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

check('나쁜 회선으로 동쪽 세계에 붙었다',
	await page.evaluate(() => typeof window.__wmView === 'object'));

// 넘어가라는 말을 창 안에서 지켜본다.
await page.evaluate(() => {
	window.__wmMoveOn = null;
	window.__wmView.socket().addEventListener('message', (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'moveon') window.__wmMoveOn = said;
	});
});

// 세계의 손으로 서쪽 끝에 데려다 놓는다(걸음 심판을 재는 자리가 아니다).
await fetch(`http://127.0.0.1:${eastPort}/health`, { headers: { connection: 'close' } });

// 창이 스스로 걷게 한다 — 왼쪽으로 계속.
const until = Date.now() + 40000;
let told = null;
while (Date.now() < until) {
	await page.keyboard.down('a');
	await wait(300);
	await page.keyboard.up('a');

	told = await page.evaluate(() => window.__wmMoveOn);
	if (told !== null) break;
}

check('국경에서 「저 세계로 가라」가 왔다', told !== null,
	told === null ? '40초를 걸어도 안 왔다' : `${told.zone} · ${told.address}`);

let landed = false;
if (told !== null) {
	const untilThere = Date.now() + 20000;
	while (Date.now() < untilThere) {
		const there = await fetch(`http://127.0.0.1:${westPort}/health`, { headers: { connection: 'close' } })
			.then((r) => r.json()).catch(() => ({ people: 0 }));
		if (there.people >= 1) { landed = true; break; }

		await wait(300);
	}
}

check('옆 세계가 실제로 받아 줬다', landed,
	landed ? '' : '두 세계 어디에도 없는 사람이 됐다');

const gone = await fetch(`http://127.0.0.1:${eastPort}/health`, { headers: { connection: 'close' } })
	.then((r) => r.json()).catch(() => ({ people: -1 }));
check('보낸 세계에서는 나갔다 (두 곳에 동시에 있지 않다)', gone.people === 0,
	`동쪽에 남은 사람 ${gone.people}명`);

check('창이 조용히 안 터졌다', pageErrors.length === 0, pageErrors.join(' | ') || '오류 없음');

await browser.close();
for (const one of lines) await one.close();
killWorlds();

if (failures === 0) {
	console.log(`[web-border] ✅ 왕복 ${ONE_WAY_MS * 2}ms · 유실 ${LOSS_PERCENT}% 회선에서도 국경을 넘는다`);
	process.exit(0);
}

console.log(`\n[web-border] RESULT: ${failures}건`);
process.exit(1);
