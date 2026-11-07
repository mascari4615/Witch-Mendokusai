#!/usr/bin/env node
// wm-web-placed-test.mjs: 세계가 옮긴 내 인형 자리를 창이 따라오나 (2026-11-07)
//
// 이유
// - 창: 내 인형의 가려는 곳(target)을 들고 50ms 마다 그쪽 걸음 송신
// - 세계가 걸음과 무관하게 옮김 (쓰러진 뒤 원점에 다시 세움, 열쇠 인사 뒤 남긴 자리): 가려는 곳이 옛 자리에 남음
// - 옮긴 거리 4m(POSITION_TELEPORT_DISTANCE) 이상은 끌어당김이 가림. 4m 미만은 고치는 곳 없음
// - 3D 새로고침 경로는 5ceb2a889 에서 고침. 이 관문은 나머지 경로를 진짜 창으로 측정
//
// 측정 (원점에서 2.2~3.5m 지점. 3D 는 나쁜 회선 왕복 200ms, 지도는 곧은 회선. 이유는 지도 절)
// - 3D: 쓰러뜨린 뒤 화면의 내 인형이 세계 자리로 오나, 다시 걸으면 원점에서 출발하나
// - 지도(엔진 막고 새로고침): 손님 인형(원점)에서 남긴 자리로 옮겨질 때 원점으로 도로 걷지 않나
// - 지도: 쓰러진 뒤 옛 자리로 도로 걷지 않나
//
// 필요한 것: .NET 8, playwright + chromium (WM_PLAYWRIGHT_ROOT)
// exit: 0 따라옴, 1 옛 자리에 남음, 2 못 돌림
//
// [빨강-확인] 2026-11-07 창의 placed 처리 둘(3D, 지도)을 끄니 2건 빨강. 3D 화면 (1.39,1.39) 세계 (0,0) 차이 1.97m,
//   지도 쓰러진 뒤 옛 자리 (2.13,2.13) 까지 도로 걸음. 지도 새로고침 칸은 곧은 회선에서 안 빨개짐 (인사 답이 지도의 첫 걸음보다 먼저 옴).
//   나쁜 회선 지도에서는 끄면 (1.74,1.74) -> (0.65,0.65) 원점으로 걸음. 회귀 칸으로만 유지
// [초록] 같은 날 켜고 두 판 연속 초록

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5680);
const linePort = port + 1;
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-placed-')), 'world.json');

const ONE_WAY_MS = 100;
const JITTER_MS = 20;
const STRIKE_REACH = 2;        // StrikeRule.REACH
const COOLDOWN_MS = 600;       // StrikeRule.COOLDOWN_MS
const WALK_SPEED = 3;          // walk.mjs WALK_SPEED
const SAME_SPOT = 0.5;         // 화면과 세계가 같은 자리로 볼 거리
const NEAR_MIN = 2.2;          // 원점에서 이만큼 이상 NEAR_MAX 이하 지점 (4m 끌어당김에 안 가리는 거리)
const NEAR_MAX = 3.5;

function cannotRun(message) {
	console.error(`[placed] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? `: ${detail}` : ''}`);
}

let chromium;
try {
	const root = process.env.WM_PLAYWRIGHT_ROOT;
	const req = createRequire(root ? join(resolve(root), 'package.json') : import.meta.url);
	chromium = req('playwright').chromium;
} catch (error) {
	cannotRun(`playwright 를 못 찾았다: ${error.message} (WM_PLAYWRIGHT_ROOT 로 알려 준다)`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-placed-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다: ${String(error.stderr || error.message).slice(-400)}`);
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
const fmt = (x, z) => `(${x.toFixed(2)}, ${z.toFixed(2)})`;
const fromOrigin = (me) => Math.hypot(me.serverX, me.serverZ);
const drawnGap = (me) => Math.hypot(me.drawnX - me.serverX, me.drawnZ - me.serverZ);

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

async function finish() {
	await browser.close();
	await line.close();
	killWorld();
	if (failures === 0) {
		console.log('[placed] ✅ 세계가 옮긴 자리를 창(3D, 지도)이 바로 따라온다');
		process.exit(0);
	}

	console.log(`\n[placed] RESULT: ${failures}건`);
	process.exit(1);
}

/** 창 하나 (제 열쇠 칸을 가진 새 사람). blockEngine 뒤로는 지도만 사용 */
async function openTab(viaPort = linePort) {
	const context = await browser.newContext();
	const page = await context.newPage();
	const tab = { page, errors: [], mapOnly: false };
	page.on('pageerror', (error) => tab.errors.push(String(error)));

	tab.blockEngine = async () => {
		await page.route('**/three.module.min.js', (route) => route.abort());
		tab.mapOnly = true;
	};

	/** 내 번호, 화면 자리, 세계가 아는 자리. 아직 모르면 null. */
	tab.mine = () => page.evaluate((onlyMap) => {
		if (onlyMap) {
			const early = window.__wmEarly;
			if (!early || typeof early.me !== 'function') return null;
			const drawn = early.drawnMe();
			const known = early.worldMe();
			if (early.me() === null || !drawn || !known) return null;
			return { id: early.me(), drawnX: drawn.x, drawnZ: drawn.z, serverX: known.x, serverZ: known.z };
		}

		const view = window.__wmView;
		if (typeof view !== 'object') return null;
		const me = (view.dolls() || []).find((one) => one.isLocal) || null;
		return me === null ? null : { id: me.id, drawnX: me.drawnX, drawnZ: me.drawnZ, serverX: me.serverX, serverZ: me.serverZ };
	}, tab.mapOnly);

	/** 접속 후 열쇠 인사 답까지 대기. reload 면 새로고침 */
	tab.enter = async (reload = false) => {
		if (reload) await page.reload();
		else await page.goto(`http://127.0.0.1:${viaPort}/`);

		if (tab.mapOnly) {
			await page.waitForFunction(() => window.__wmEarly && window.__wmEarly.socket
				&& window.__wmEarly.socket.readyState === 1, null, { timeout: 30000 }).catch(() => { /* 아래가 잡는다 */ });
			// 빨강 원인 기록: 지도가 보낸 걸음, 받은 placed, 받은 내 자리와 시각
			await page.evaluate(() => {
				const early = window.__wmEarly;
				const trace = [];
				window.__placedTrace = trace;
				const started = performance.now();
				const stamp = () => Math.round(performance.now() - started);
				const send = early.socket.send.bind(early.socket);
				early.socket.send = (text) => {
					try {
						const said = JSON.parse(text);
						if (said.type === 'move') trace.push(`${stamp()} 보냄 move ${said.x.toFixed(2)},${said.z.toFixed(2)}`);
					} catch { /* 글자가 아니면 안 적는다 */ }
					send(text);
				};
				early.socket.addEventListener('message', (event) => {
					let said;
					try { said = JSON.parse(event.data); } catch { return; }
					const me = early.me();
					if (said.type === 'placed') trace.push(`${stamp()} 받음 placed ${said.x},${said.z} seq ${said.seq}`);
					if (said.type === 'welcome') trace.push(`${stamp()} 받음 welcome id ${said.id} 신원 ${said.identityId || 0}`);
					if (said.type === 'world' && Array.isArray(said.dolls)) {
						const mineNow = said.dolls.find((one) => one.id === me);
						if (mineNow) trace.push(`${stamp()} 받음 world#${said.sequence}${said.changed ? 'd' : 'W'} 나 ${mineNow.x},${mineNow.z}`);
					}
					if (said.type === 'me' && said.doll && said.doll.id === me) trace.push(`${stamp()} 받음 me ${said.doll.x},${said.doll.z}`);
				});
			});
			const until = Date.now() + 20000;
			while (Date.now() < until && (await tab.mine()) === null) await wait(100);
			return;
		}

		await page.waitForFunction(() => {
			const view = window.__wmView;
			return typeof view === 'object' && view.bagNewsSinceWelcome() > 0
				&& (document.getElementById('me').textContent || '').includes('번 사람')
				&& (view.dolls() || []).some((one) => one.isLocal);
		}, null, { timeout: 40000 }).catch(() => { /* 아래가 잡는다 */ });
	};

	/** 원점에서 NEAR_MIN~NEAR_MAX 지점으로 이동. 원점에서 d 쪽 한 줄 위만 사용 (a 는 되돌아옴) */
	tab.standNearOrigin = async () => {
		const until = Date.now() + 30000;
		while (Date.now() < until) {
			const me = await tab.mine();
			if (me === null) { await wait(200); continue; }
			const far = fromOrigin(me);
			if (far >= NEAR_MIN && far <= NEAR_MAX) break;

			const key = far > NEAR_MAX ? 'a' : 'd';
			await page.keyboard.down(key);
			await wait(far < 1.5 ? 300 : 80);
			await page.keyboard.up(key);
			await wait(500);
		}

		// 걸음 답이 다 오고 화면이 멎을 틈
		await wait(800);
		return tab.mine();
	};

	return tab;
}

/** 곧은 회선의 맨 창이 그 사람에게 붙어 쓰러질 때까지 타격. 반환은 쓰러뜨린 횟수 */
async function knockDown(victimId) {
	const attacker = { id: 0, seen: new Map(), downs: 0 };
	const socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	socket.onopen = () => socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }
		if (said.type === 'welcome' && said.id) attacker.id = said.id;
		if (said.type === 'hurt' && said.dollId === victimId && said.down) attacker.downs += 1;
		if (said.type === 'world' && Array.isArray(said.dolls)) {
			for (const doll of said.dolls) {
				if (typeof doll.x === 'number' && typeof doll.z === 'number') attacker.seen.set(doll.id, { x: doll.x, z: doll.z });
			}
		}
	};
	const tell = (message) => {
		try { socket.send(JSON.stringify(message)); } catch { /* 끊겼다 */ }
	};

	const until = Date.now() + 40000;
	let lastSwing = 0;
	while (Date.now() < until && attacker.downs === 0) {
		const me = attacker.seen.get(attacker.id);
		const him = attacker.seen.get(victimId);
		if (attacker.id !== 0 && me && him) {
			const gap = Math.hypot(him.x - me.x, him.z - me.z);
			if (gap > STRIKE_REACH * 0.6) {
				const step = Math.min(WALK_SPEED * 0.05, gap - STRIKE_REACH * 0.3);
				tell({ type: 'move', x: (him.x - me.x) / gap * step, z: (him.z - me.z) / gap * step });
			} else if (Date.now() - lastSwing >= COOLDOWN_MS + 60) {
				lastSwing = Date.now();
				tell({ type: 'strike', targetId: victimId });
			}
		}

		await wait(50);
	}

	try { socket.close(); } catch { /* 이미 닫혔다 */ }
	return attacker.downs;
}

/** 세계가 원점에 다시 세운 자리가 창에 도착할 때까지 대기 */
async function waitWorldPutsAtOrigin(tab) {
	const until = Date.now() + 10000;
	while (Date.now() < until) {
		const me = await tab.mine();
		if (me !== null && fromOrigin(me) < SAME_SPOT) return me;
		await wait(100);
	}

	return tab.mine();
}

// ── 1. 3D: 쓰러진 뒤 원점에 다시 세움 ────────────────────────────────────
console.log('[placed] 3D 창, 쓰러진 뒤 원점에 다시 세움');
{
	const tab = await openTab();
	await tab.enter();
	const standing = await tab.standNearOrigin();
	check('3D: 세계에 붙어 사람이 됐다', standing !== null);
	if (standing === null) await finish();

	const standingAway = fromOrigin(standing);
	check('3D: 원점에서 4m 안쪽에 섰다 (끌어당김이 가리지 않는 거리)', standingAway >= NEAR_MIN && standingAway <= NEAR_MAX,
		`${standingAway.toFixed(2)}m`);
	check('3D: 선 자리에서 화면과 세계가 같다 (재는 자가 성한지부터)', drawnGap(standing) <= SAME_SPOT,
		`화면 ${fmt(standing.drawnX, standing.drawnZ)} 세계 ${fmt(standing.serverX, standing.serverZ)}`);

	const downs = await knockDown(standing.id);
	check('3D: 쓰러져 세계가 다시 세웠다', downs > 0, `쓰러짐 ${downs}번`);
	if (downs === 0) await finish();

	const placedAt = await waitWorldPutsAtOrigin(tab);
	check('3D: 창이 세계가 다시 세운 자리를 받았다', fromOrigin(placedAt) < SAME_SPOT,
		`세계 ${fmt(placedAt.serverX, placedAt.serverZ)}`);

	// 왕복 두 번 넘게 대기
	await wait(1200);
	const settled = await tab.mine();
	check('3D: 다시 선 뒤 화면의 내 인형이 세계의 내 자리에 있다', drawnGap(settled) <= SAME_SPOT,
		`화면 ${fmt(settled.drawnX, settled.drawnZ)} 세계 ${fmt(settled.serverX, settled.serverZ)} 차이 ${drawnGap(settled).toFixed(2)}m`);
	check('3D: 가만히 있는 동안 옛 자리로 도로 걷지 않는다', fromOrigin(settled) < SAME_SPOT,
		`세계 ${fmt(settled.serverX, settled.serverZ)}`);

	// 다시 걸으면 원점에서 출발 (옛 자리 + 걸음으로 튀지 않음)
	await tab.page.keyboard.down('w');
	await wait(150);
	await tab.page.keyboard.up('w');
	await wait(1200);
	const walked = await tab.mine();
	check('3D: 다시 걸으면 원점에서 출발하고 화면도 같은 자리다', fromOrigin(walked) < 1.5 && drawnGap(walked) <= SAME_SPOT,
		`세계 ${fmt(walked.serverX, walked.serverZ)} 화면 ${fmt(walked.drawnX, walked.drawnZ)}, 쓰러지기 전 ${standingAway.toFixed(2)}m`);
	check('3D: 창이 조용히 안 터졌다', tab.errors.length === 0, tab.errors.join(' | ') || '오류 없음');
	await tab.page.context().close();
}

// ── 2. 지도: 엔진 막고 새로고침, 그 뒤 쓰러짐 ─────────────────────────────
// 지도는 열쇠를 안 적음. 3D 로 먼저 사람이 되어 열쇠를 남긴 뒤 엔진을 막고 새로고침
// 지도는 곧은 회선으로 측정 (2026-11-07 실측)
// - 지도는 되감기 없이 "가려는 곳 - 받은 세계 자리" 를 50ms 마다 송신. 왕복 200ms 면 걸음 4개가 늘 떠 있음
//   가려는 곳을 지나쳤다 돌아오기 반복 (원점 둘레 0.6m 를 1.5초 넘게 돎). placed 와 무관한 별도 결함
// - 그 흔들림이 이 관문의 물음(옮긴 자리를 따라오나)을 가림. 곧은 회선에서 판정, 곧은 회선에서도 placed 를 끄면 빨강
console.log('[placed] 지도 창 (3D 로 열쇠를 남긴 뒤 엔진 막고 새로고침, 곧은 회선)');
{
	const tab = await openTab(port);
	await tab.enter();
	const left = await tab.standNearOrigin();
	check('지도: 3D 로 사람이 되어 원점에서 4m 안쪽에 섰다', left !== null && fromOrigin(left) >= NEAR_MIN && fromOrigin(left) <= NEAR_MAX,
		left === null ? '자리를 모른다' : `${fromOrigin(left).toFixed(2)}m`);
	if (left === null) await finish();

	await tab.blockEngine();
	await tab.enter(true);
	check('지도: 엔진 없이 세계에 붙었다', (await tab.page.evaluate(() => typeof window.__wmView !== 'object'))
		&& (await tab.mine()) !== null);

	// 열쇠 인사 답(남긴 자리로 옮김) 도착과 왕복 몇 번의 여유
	await wait(2500);
	const back = await tab.mine();
	const moved = back === null ? null : Math.hypot(back.serverX - left.serverX, back.serverZ - left.serverZ);
	check('지도: 새로고침 뒤 남긴 자리에 있다 (원점으로 도로 걷지 않는다)', moved !== null && moved <= 1.5,
		back === null ? '자리를 모른다' : `${fmt(left.serverX, left.serverZ)} -> ${fmt(back.serverX, back.serverZ)}`);
	check('지도: 그린 내 자리도 세계 자리다', back !== null && drawnGap(back) <= SAME_SPOT,
		back === null ? '자리를 모른다' : `화면 ${fmt(back.drawnX, back.drawnZ)} 세계 ${fmt(back.serverX, back.serverZ)}`);

	const standing = await tab.standNearOrigin();
	const standingAway = standing === null ? 0 : fromOrigin(standing);
	check('지도: 원점에서 4m 안쪽에 섰다', standingAway >= NEAR_MIN && standingAway <= NEAR_MAX, `${standingAway.toFixed(2)}m`);
	if (standing === null) await finish();

	const downs = await knockDown(standing.id);
	check('지도: 쓰러져 세계가 다시 세웠다', downs > 0, `쓰러짐 ${downs}번`);
	if (downs === 0) await finish();

	await waitWorldPutsAtOrigin(tab);
	await wait(1500);
	const settled = await tab.mine();
	check('지도: 쓰러진 뒤 옛 자리로 도로 걷지 않는다', fromOrigin(settled) < SAME_SPOT,
		`세계 ${fmt(settled.serverX, settled.serverZ)}, 쓰러지기 전 ${standingAway.toFixed(2)}m`);
	check('지도: 쓰러진 뒤 그린 내 자리도 세계 자리다', drawnGap(settled) <= SAME_SPOT,
		`화면 ${fmt(settled.drawnX, settled.drawnZ)} 세계 ${fmt(settled.serverX, settled.serverZ)}`);
	check('지도: 창이 조용히 안 터졌다', tab.errors.length === 0, tab.errors.join(' | ') || '오류 없음');
	if (failures > 0) {
		const trace = await tab.page.evaluate(() => window.__placedTrace || []);
		console.log('  ⓘ 지도 기록 (끝 60줄, ms 는 이 창 열림부터):');
		for (const one of trace.slice(-60)) console.log(`    ${one}`);
	}
	await tab.page.context().close();
}

await finish();
