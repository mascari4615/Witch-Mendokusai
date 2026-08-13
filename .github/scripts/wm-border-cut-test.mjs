#!/usr/bin/env node
// wm-border-cut-test.mjs — <b>국경을 넘다 끊겨도 나로 도착한다</b> (TASK-WM-309).
//
// ★ 무엇이 있었나 (실측 2026-08-13): 통행증을 내밀다 <b>줄이 끊기면</b> 그 통행증은 이미 쓴 것이 된다.
//   그 사람이 다시 붙으면 통행증이 거절되고, 옆 세계는 그를 <b>처음 보는 손님</b>으로 맞았다 —
//   가방도 자리도 없이. 장부에는 신원이 하나 더 쌓였다(안 끊고 넘으면 1, 끊기면 2).
//
// ★ 고친 자리: 「통행증 한 장 = 한 번」을 <b>「짐은 한 번」</b>으로 좁혔다. 다시 들어오는 것은
//   허락하되(같은 사람이니까) 가방·자리·몸은 <b>처음 넘어올 때만</b> 준다 — 안 그러면 그게 복사다.
//
// 재는 것:
//   ① 대조군 — 안 끊고 넘으면 옆 세계 장부는 1, 가방은 그대로
//   ② 넘다 끊고 다시 붙으면 — 받아 주나 · 장부가 그대로 1인가 · 가방이 그대로인가(두 벌 아님)
//
// ⚠ 이 관문이 걸려 넘어진 자리 (재는 자의 고장, domain-wm.md § 관문 규율):
//   ① 주우러 가는 들판이 <b>국경 너머</b>라 짐을 챙기기 <b>전에</b> 국경을 넘어 버렸다 —
//      그때 나온 통행증에는 짐이 0이었고(서버 로그 carried=0), 그걸 「짐이 사라졌다」로 읽을 뻔했다.
//      → 이 세계 안쪽(x ≥ 4) 자리만 고르고, 서쪽으로 걷기 직전에 옛 통행증을 버린다.
//   ② 창이 <b>빈 열쇠</b>로 인사하면 세계가 남의 기록과 헷갈린다 — 진짜 창처럼 제 열쇠를 낸다.
//
// exit: 0 = 나로 도착한다 · 1 = 손님이 되거나 짐이 는다 · 2 = 못 돌림
//
// [빨강-확인] 시험 이음매(WM_TEST_HALT_AFTER_CLAIM + halt:1)로 「집은 뒤 놓기」를 실제로 만들었더니
//   가방 3 → 0 으로 빨강 — 그 자리에서 진짜 결함을 찾아 고쳤다 (TASK-WM-337, 2026-08-14)

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const eastPort = Number(process.env.WM_SMOKE_PORT || 5570);
const westPort = eastPort + 1;
const SECRET = '두 세계만 아는 말';

function cannotRun(message) {
	console.error(`[border-cut] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-bcut-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worlds = [];
function startWorld(port, zone, neighbours, { haltAfterClaim = false } = {}) {
	const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-bcut-')), 'world.json');
	const extra = haltAfterClaim ? { WM_TEST_HALT_AFTER_CLAIM: '1' } : {};
	worlds.push(spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${port}`], {
		cwd: dirname(dll),
		env: { ...process.env, WM_WORLD_FILE: worldFile, WM_ZONE: zone, WM_ZONE_NEIGHBOURS: neighbours, WM_ZONE_SECRET: SECRET, ...extra },
		stdio: 'ignore',
	}));
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

startWorld(eastPort, `동:0,-40,40,40`, `서:-40,-40,0,40=ws://127.0.0.1:${westPort}/ws`);
startWorld(westPort, `서:-40,-40,0,40`, `동:0,-40,40,40=ws://127.0.0.1:${eastPort}/ws`, { haltAfterClaim: true });

for (const port of [eastPort, westPort]) {
	let up = false;
	const until = Date.now() + 120000;
	while (Date.now() < until) {
		try {
			const answer = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { up = true; break; }
		} catch { /* 아직 */ }
		await wait(300);
	}

	if (up === false) {
		killWorlds();
		cannotRun(`세계가 안 떴다 (${port})`);
	}
}

function joinWorld(port, { secret = '', pass = '' } = {}) {
	const one = { id: null, secret: '', here: undefined, moveOn: null, bag: new Map(), field: [] };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify(pass ? { type: 'hello', secret, pass } : { type: 'hello', secret }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'welcome') { one.id = said.id; one.secret = said.secret; one.identityId = said.identityId; }
		if (said.type === 'moveon') one.moveOn = said;
		if (said.type === 'me') one.here = { x: said.x, z: said.z };
		if (said.type === 'world') {
			if (Array.isArray(said.gatherables) && said.gatherables.length > 0) one.field = said.gatherables;
			if (Array.isArray(said.dolls) && one.id !== null) {
				const mine = said.dolls.find((doll) => doll.id === one.id);
				if (mine !== undefined) one.here = { x: mine.x, z: mine.z };
			}
		}

		if (said.type === 'bag' && Array.isArray(said.items)) one.bag = new Map(said.items.map((item) => [item.itemId, item.amount]));
	};

	return one;
}

const send = (one, message) => {
	if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message));
};

const bagSize = (one) => [...one.bag.values()].reduce((sum, amount) => sum + amount, 0);
const health = (port) => fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

/** 동쪽에서 물건을 하나 줍고, 서쪽 국경까지 걸어가 통행증을 받는다. */
async function walkToBorderWithLuggage() {
	const me = joinWorld(eastPort);
	await wait(3000);
	if (me.id === null) return null;

	// 가까운 들판까지 걸어가 줍는다 — 짐이 있어야 「짐이 두 벌 왔나」를 잴 수 있다.
	//   ⚠ 들판 목록이 <b>오기 전에</b> 고르면 아무 데도 못 간다(첫 판이 그랬다: 짐 0으로 CANNOT-RUN).
	for (let i = 0; i < 40 && me.field.length === 0; i += 1) await wait(250);

	const mineNow = me.here || { x: 0, z: 0 };
	// ⚠ 들판 자리가 <b>국경 너머</b>면, 주우러 가다가 먼저 국경을 넘어 버린다 —
	//   그때 나온 통행증에는 <b>짐이 0</b>이다(실측: carried=0). 그걸 나중에 쓰면 「짐이 사라졌다」로 보인다.
	//   그래서 이 세계 <b>안쪽</b> 자리만 고른다.
	const nearby = [...me.field].filter((one) => one.x >= 4).sort((one, other) =>
		Math.hypot(one.x - mineNow.x, one.z - mineNow.z) - Math.hypot(other.x - mineNow.x, other.z - mineNow.z));

	// ⚠ 가장 가까운 자리 하나만 노리면 <b>앞사람이 방금 주운 자리</b>일 수 있다(다시 자라는 중이라 거절).
	//   그러면 짐이 0 이 되어 검사가 빈 검사가 된다 — 그래서 몇 자리를 차례로 시도한다.
	for (const spot of nearby.slice(0, 5)) {
		if (bagSize(me) > 0) break;

		for (let i = 0; i < 500; i += 1) {
			const mine = me.here;
			if (mine !== undefined && Math.hypot(spot.x - mine.x, spot.z - mine.z) < 1.2) break;

			if (mine !== undefined) {
				send(me, {
					type: 'move', seq: i,
					x: Math.max(-0.15, Math.min(0.15, spot.x - mine.x)),
					z: Math.max(-0.15, Math.min(0.15, spot.z - mine.z)),
				});
			}

			await wait(50);
		}

		for (let tries = 1; tries <= 2 && bagSize(me) === 0; tries += 1) {
			send(me, { type: 'gather', nodeId: spot.id, did: spot.id + tries });
			await wait(1000);
			send(me, { type: 'bagask' });
			await wait(1000);
		}
	}

	// 서쪽 국경으로 — <b>여기서부터</b>의 통행증만 센다(앞서 받은 것은 짐 없는 옛것이다).
	me.moveOn = null;
	for (let i = 0; i < 600 && me.moveOn === null; i += 1) {
		send(me, { type: 'move', x: -0.15, z: 0, seq: 1000 + i });
		await wait(50);
	}

	return me;
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

// ── ① 대조군: 안 끊고 넘는다 ──────────────────────────────────────────
const cleanOne = await walkToBorderWithLuggage();
if (cleanOne === null || cleanOne.moveOn === null) {
	killWorlds();
	cannotRun('국경까지 못 갔다 (넘어가라는 말이 안 왔다)');
}

const luggage = bagSize(cleanOne);
if (luggage <= 0) {
	killWorlds();
	cannotRun('짐이 없다 — 「짐이 두 벌 왔나」를 잴 수 없다');
}

const overClean = joinWorld(westPort, { secret: cleanOne.secret, pass: cleanOne.moveOn.pass });
await wait(3500);
cleanOne.socket.close();
await wait(2000);
send(overClean, { type: 'bagask' });
await wait(1500);

const westClean = await health(westPort);
check('안 끊고 넘으면 나로 도착한다 (대조군)', overClean.id !== null && bagSize(overClean) === luggage,
	`장부 ${westClean.identities} · 가방 ${luggage} → ${bagSize(overClean)}`);

if (overClean.id === null) {
	killWorlds();
	cannotRun('대조군조차 못 넘었다 — 재는 자가 고장 난 것이다');
}

overClean.socket.close();
await wait(1500);

// ── ② 넘다 끊고 다시 붙는다 ───────────────────────────────────────────
const cutOne = await walkToBorderWithLuggage();
if (cutOne === null || cutOne.moveOn === null) {
	killWorlds();
	cannotRun('두 번째 사람이 국경까지 못 갔다');
}

const cutLuggage = bagSize(cutOne);
if (cutLuggage <= 0) {
	killWorlds();
	cannotRun('두 번째 사람이 짐을 못 챙겼다 — 「짐이 두 벌 왔나」가 빈 검사가 된다');
}

const beforeLedger = (await health(westPort)).identities;

// ★ 통행증을 <b>집어 든 뒤 짐을 건네기 전에</b> 끊긴다 (TASK-WM-337).
//   밖에서 30ms 뒤에 끊는 것으로는 그 순간을 못 만든다 — 인사 처리가 그보다 빨리 끝나기 때문이다.
//   실제로 이 관문은 <b>고침을 되돌려도 초록</b>이었다(거짓 초록). 그래서 세계에 시험용 이음매를 두고
//   (`WM_TEST_HALT_AFTER_CLAIM=1`, 서쪽 세계에만) 그 자리에서 <b>한 번</b> 줄을 놓게 한다.
// 인사만 보내고 곧바로 죽는다 — 세계가 받아들이는 중에 줄이 끊긴 셈이다.
const halfWay = new WebSocket(`ws://127.0.0.1:${westPort}/ws`);
halfWay.onopen = () => {
	// ⚠ 진짜 창은 <b>제 기기 열쇠</b>를 함께 낸다. 빈 열쇠로 내밀면 세계가 남의 기록과 헷갈려
	//   통행증을 물리고 손님으로 맞는다 — 그건 재는 자가 만든 고장이다(첫 판이 그랬다).
	// `halt: '1'` = 세계에게 「이번 인사는 통행증을 집은 자리에서 놓아라」 (WM_TEST_HALT_AFTER_CLAIM 과 짝).
	halfWay.send(JSON.stringify({ type: 'hello', secret: cutOne.secret, pass: cutOne.moveOn.pass, halt: '1' }));
	setTimeout(() => halfWay.close(), Number(process.env.WM_HALF_MS || 30));
};

halfWay.onerror = () => { /* 아래가 잡는다 */ };
await wait(3000);
cutOne.socket.close();
await wait(5000);

// 창이 하는 그대로 — 같은 통행증으로 다시 들어간다.
const overCut = joinWorld(westPort, { secret: cutOne.secret, pass: cutOne.moveOn.pass });
await wait(4000);
send(overCut, { type: 'bagask' });
await wait(2000);

const westCut = await health(westPort);
const grew = westCut.identities - beforeLedger;

check('넘다 끊겨도 받아 준다', overCut.id !== null,
	overCut.id === null ? '못 들어갔다 — 그 사람은 어디에도 없는 사람이 된다' : `사람 ${overCut.id}`);
check('손님이 아니라 나로 도착한다 (장부가 안 는다)', grew <= 1,
	`서쪽 장부 ${beforeLedger} → ${westCut.identities} (2 늘면 반쪽 시도가 남긴 유령 신원이다)`);
check('짐이 그대로다 (두 벌도, 빈손도 아니다)', bagSize(overCut) === cutLuggage,
	`가방 ${cutLuggage} → ${bagSize(overCut)}`);

// ── ③ 두 세계가 <b>같은 하늘</b>을 보나 (TASK-WM-315) ─────────────────
//
// ★ 왜 여기서 재나: prod 에 세계 둘을 세우자마자 east 125일 · west 91일이 나왔다 —
//   34일 어긋난 하늘이다(국경을 넘으면 밤이 낮이 된다). 하늘은 벽시계에서 유도되므로
//   <b>같아야 정상</b>이다. 두 세계를 띄우는 관문이 여기뿐이라 이 자리에서 지킨다.
//
// ⚠ 이 검사만으로는 <b>그때 그 고장</b>을 못 만든다: 여기 두 세계는 <b>새 파일</b>로 뜨므로
//   저장된 달력이 앞설 일이 없다(고침을 꺼도 초록이다 — 실패 경로를 밟아 확인했다).
//   그 자리는 단위 시험이 지킨다(`SkyAgreesTests`). 여기서는 <b>둘이 어긋나지 않는다</b>는
//   불변식을 지킨다 — 앞으로 어떤 이유로든 갈리면 이 줄이 빨개진다.
const eastSky = await health(eastPort);
const westSky = await health(westPort);
const dayGap = Math.abs(eastSky.day - westSky.day);
const minuteGap = Math.abs((eastSky.hour * 60 + eastSky.minute) - (westSky.hour * 60 + westSky.minute));

check('두 세계가 같은 하늘을 본다', dayGap === 0 && minuteGap <= 2,
	`east ${eastSky.day}일 ${eastSky.hour}:${eastSky.minute} · west ${westSky.day}일 ${westSky.hour}:${westSky.minute}`
		+ ` (하루 차이 ${dayGap} · 분 차이 ${minuteGap})`);

killWorlds();

if (failures === 0) {
	console.log('[border-cut] ✅ 국경을 넘다 끊겨도 나로, 짐 그대로 도착한다');
	process.exit(0);
}

console.log(`\n[border-cut] RESULT: ${failures}건`);
process.exit(1);
