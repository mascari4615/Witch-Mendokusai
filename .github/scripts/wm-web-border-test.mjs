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

// ⚠ 세계 소식 세기는 <b>창이 열리기 전에</b> 심는다 (TASK-WM-278). 나중에 귀를 대면
//   국경을 넘느라 <b>줄이 바뀌는 순간</b>을 통째로 놓친다 — 그게 바로 재려는 그 순간이다.
await page.addInitScript(() => {
	window.__wmPlates = [];
	const RealSocket = window.WebSocket;
	window.WebSocket = function (...args) {
		const socket = new RealSocket(...args);
		socket.addEventListener('message', (event) => {
			let said;
			try { said = JSON.parse(event.data); } catch { return; }

			if (said.type === 'world') window.__wmPlates.push(Date.now());
		});

		return socket;
	};

	window.WebSocket.prototype = RealSocket.prototype;
	Object.assign(window.WebSocket, RealSocket);
});

await page.goto(`http://127.0.0.1:${eastLine}/`);
await page.waitForFunction(() => typeof window.__wmView === 'object', null, { timeout: 40000 })
	.catch(() => { /* 아래 칸이 잡는다 */ });

check('나쁜 회선으로 동쪽 세계에 붙었다',
	await page.evaluate(() => typeof window.__wmView === 'object'));

// 넘어가라는 말을 창 안에서 지켜본다.
// ★ 그리고 <b>세계 소식이 몇 초 멎나</b>도 같이 잰다 (TASK-WM-278): 국경을 넘는 동안 창은
//   이 세계에서 떨어져 저 세계에 붙는다 — 그 사이는 사람 눈에 <b>멈춘 화면</b>이다.
//   넘어간다는 사실만 재고 <b>얼마나 멎나</b>는 한 번도 안 쟀다.
await page.evaluate(() => {
	window.__wmMoveOn = null;
	window.__wmHeardOn = null;

	const listen = () => {
		const now = window.__wmView.socket();
		if (!now || now === window.__wmHeardOn) return;

		window.__wmHeardOn = now;
		now.addEventListener('message', (event) => {
			let said;
			try { said = JSON.parse(event.data); } catch { return; }

			if (said.type === 'moveon') window.__wmMoveOn = said;
		});
	};

	listen();
	window.__wmWatch = setInterval(listen, 100);
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

// ★ 이제 넘어가는 순간에는 <b>잠깐 두 세계에 다 있다</b> (TASK-WM-279): 창이 저 세계에
//   먼저 붙어 보고 첫 그림이 온 뒤에 옛 줄을 놓기 때문이다. 그 겹침이 <b>곧 풀리는지</b>를 본다
//   — 안 풀리면 그 사람은 정말로 두 세계에 산다(가방이 복사된다).
let gone = { people: -1 };
{
	const untilGone = Date.now() + 5000;
	while (Date.now() < untilGone) {
		gone = await fetch(`http://127.0.0.1:${eastPort}/health`, { headers: { connection: 'close' } })
			.then((r) => r.json()).catch(() => ({ people: -1 }));
		if (gone.people === 0) break;

		await wait(200);
	}
}

check('보낸 세계에서는 곧 나간다 (두 세계에 눌러앉지 않는다)', gone.people === 0,
	`동쪽에 남은 사람 ${gone.people}명`);

// ── 국경을 넘는 동안 화면이 <b>몇 초 멎었나</b> (TASK-WM-278) ─────────────
//   왕복 200ms 회선에서 「끊고 · 붙고 · 첫 그림」이 얼마나 걸리는지가 그 값이다.
{
	const plates = await page.evaluate(() => window.__wmPlates || []);
	let worstGapMs = 0;
	for (let i = 1; i < plates.length; i += 1)
		worstGapMs = Math.max(worstGapMs, plates[i] - plates[i - 1]);

	console.log(`  ⓘ 국경을 넘는 동안 세계 소식이 가장 오래 멎은 시간 ${worstGapMs}ms`
		+ ` (받은 판 ${plates.length}장)`);

	// 사람이 「끊겼다」로 읽기 시작하는 선 — 넘겨주기(200ms 대기) + 붙기 + 첫 그림.
	// 판이 몇 장 없으면 그건 「안 멎었다」가 아니라 <b>못 잰 것</b>이다(20Hz 라 25장 ≈ 1.3초).
	if (plates.length < 25) {
		await browser.close();
		for (const one of lines) await one.close();
		killWorlds();
		cannotRun(`세계 소식을 ${plates.length}장밖에 못 받았다 — 이 상태로 잰 멎음은 뜻이 없다`);
	}

	check('국경을 넘어도 화면이 3초 넘게 멎지 않는다', worstGapMs <= 3000,
		`${worstGapMs}ms · 받은 판 ${plates.length}장`);
}

// ── ② 저 세계가 <b>안 열려 있으면</b> 어떻게 되나 (TASK-WM-256) ─────────
//   보낸 세계는 이미 나를 내보냈다. 저쪽이 꺼져 있으면 나는 어디에도 없는 사람이 된다.
//   그러면 <b>왔던 곳으로 돌아와야</b> 한다 — 통행증에 신원과 가방이 들어 있으니 그대로 선다.
{
	// 동쪽으로 다시 넘어가게 두고, 이번엔 동쪽 세계를 죽여 둔다.
	const east = worlds[0];
	try {
		if (process.platform === 'win32') execSync(`taskkill /PID ${east.pid} /F /T`, { stdio: 'ignore' });
		else east.kill('SIGKILL');
	} catch { /* 이미 죽었다 */ }

	// ⚠ 넘어가면 <b>줄이 바뀐다</b> — 옛 줄에 귀를 대고 있으면 아무 말도 안 들린다.
	//   창이 새로 연 줄에 다시 붙인다(그리고 다시 붙을 때마다 또 붙게 둔다).
	await page.evaluate(() => {
		window.__wmMoveOn = null;
		const listen = () => {
			const now = window.__wmView.socket();
			if (!now || now === window.__wmHeard) return;

			window.__wmHeard = now;
			now.addEventListener('message', (event) => {
				let said;
				try { said = JSON.parse(event.data); } catch { return; }

				if (said.type === 'moveon') window.__wmMoveOn = said;
			});
		};

		listen();
		if (window.__wmListenTimer) clearInterval(window.__wmListenTimer);
		window.__wmListenTimer = setInterval(listen, 200);
	});

	const untilTold = Date.now() + 40000;
	let toldAgain = null;
	while (Date.now() < untilTold) {
		await page.keyboard.down('d');
		await wait(300);
		await page.keyboard.up('d');

		toldAgain = await page.evaluate(() => window.__wmMoveOn);
		if (toldAgain !== null) break;
	}

	check('죽은 세계로도 「가라」는 온다 (세계는 이웃이 살았는지 모른다)', toldAgain !== null);

	// ★ 이제는 <b>떠나기 전에 붙어 본다</b> (TASK-WM-279) — 저 세계가 안 열리면 아예 안 떠난다.
	//   보낸 세계도 창이 실제로 줄을 놓을 때까지 데리고 있으므로, 「어디에도 없는 사람」이 안 생긴다.
	let stayed = false;
	if (toldAgain !== null) {
		const untilBack = Date.now() + 25000;
		while (Date.now() < untilBack) {
			const here = await fetch(`http://127.0.0.1:${westPort}/health`, { headers: { connection: 'close' } })
				.then((r) => r.json()).catch(() => ({ people: 0 }));
			if (here.people >= 1) { stayed = true; break; }

			await wait(400);
		}
	}

	check('저 세계가 안 열리면 원래 세계에 그대로 있다', stayed,
		stayed ? '' : '두 세계 어디에도 없는 사람이 됐다 — 가방째 사라졌다');
}

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
