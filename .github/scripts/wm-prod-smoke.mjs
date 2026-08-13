#!/usr/bin/env node
// wm-prod-smoke.mjs — 배포 뒤 <b>prod 가 실제로 노는지</b> 한 번 걸어 본다 (TASK-WM-319).
//
// ★ 왜: 배포는 여태 「떴다」(/health 초록)까지만 봤다. 그런데 오늘 하루만 해도 <b>떠 있는데
//   안 노는</b> 자리가 셋이었다 — 땅 이름이 서비스 env 를 타며 「?」로 뭉개져 세계가 못 서고,
//   nssm 이 읽지도 못하는 껍데기 서비스가 남아 있었고, 두 세계의 하늘이 34일 어긋나 있었다.
//   그 셋 다 /health 만 보면 안 보이거나(껍데기) 초록으로 보인다(어긋난 하늘).
//
// 재는 것 (prod 두 세계, 봇 하나):
//   ① east 에 들어가 이름을 달고 ② 서쪽으로 걸어 국경을 넘고
//   ③ <b>그 이름 그대로</b> west 에 도착하고 ④ 두 세계의 하늘이 같은가
//
// ⚠ 이 시험은 <b>prod 를 건드린다</b>: 손님 하나가 들어와 걷다 넘어간다(빈손이라 장부 청소가 지운다).
//   그래서 하는 일을 최소로 둔다 — 줍지도, 짓지도, 말하지도 않는다.
//
// 실행: node .github/scripts/wm-prod-smoke.mjs   (노트북 러너 위, 배포 스텝 끝에서)
// exit: 0 = 논다 · 1 = 안 논다 · 2 = 못 돌림
//
// [빨강-확인] prod 를 부러뜨릴 수는 없으니 <b>같은 얼개의 두 세계를 여기 띄워</b> 걸었다 (2026-08-14):
//   서쪽의 `WM_ZONE_SECRET` 만 다르게 두니 빨강 — 「건너간 사람이 그 사람 그대로다 — 이름이 「손님 1」」.
//   즉 통행증 도장이 안 맞으면 사람이 <b>손님으로 다시 태어난다</b>(가방·이름을 잃는다). 그 자리를 이 관문이 잡는다.
//   거는 법: WM_PROD_EAST/WM_PROD_WEST 로 이 관문을 아무 두 세계에나 겨눌 수 있다.

const eastPort = Number(process.env.WM_PROD_EAST || 5199);
const westPort = Number(process.env.WM_PROD_WEST || 5200);
const NAME = 'smoke-' + Math.random().toString(36).slice(2, 7);

function cannotRun(message) {
	console.error(`[prod-smoke] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));
const health = (port) => fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((one) => one.json());

function joinWorld(port, { secret = '', pass = '' } = {}) {
	const one = { id: null, secret: '', name: '', here: undefined, moveOn: null, zone: '' };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify(pass ? { type: 'hello', secret, pass } : { type: 'hello', secret }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(event.data); } catch { return; }

		if (said.type === 'welcome') { one.id = said.id; one.secret = said.secret; }
		if (said.type === 'moveon') one.moveOn = said;
		if (said.type === 'me') one.here = { x: said.x, z: said.z };
		if (said.type === 'world' && Array.isArray(said.dolls) && one.id !== null) {
			const mine = said.dolls.find((doll) => doll.id === one.id);
			if (mine !== undefined) one.here = { x: mine.x, z: mine.z };
		}

		if (said.type === 'names' && Array.isArray(said.dolls) && one.id !== null) {
			const mine = said.dolls.find((doll) => doll.id === one.id);
			if (mine !== undefined && mine.name) one.name = mine.name;
		}
	};

	return one;
}

const send = (one, message) => {
	if (one.socket.readyState === 1) one.socket.send(JSON.stringify(message));
};

let eastHealth;
let westHealth;
try {
	eastHealth = await health(eastPort);
	westHealth = await health(westPort);
} catch (error) {
	cannotRun(`세계에 못 물어봤다 — ${error.message}`);
}

if (eastHealth.ok !== true || westHealth.ok !== true) cannotRun('두 세계 중 하나가 안 떴다');

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

// ── 하늘부터 (걷기 전에 볼 수 있다) ───────────────────────────────────
const dayGap = Math.abs(eastHealth.day - westHealth.day);
const minuteGap = Math.abs((eastHealth.hour * 60 + eastHealth.minute) - (westHealth.hour * 60 + westHealth.minute));
check('두 세계가 같은 하늘을 본다', dayGap === 0 && minuteGap <= 2,
	`east ${eastHealth.day}일 ${eastHealth.hour}:${String(eastHealth.minute).padStart(2, '0')}`
	+ ` · west ${westHealth.day}일 ${westHealth.hour}:${String(westHealth.minute).padStart(2, '0')}`);

check('기억을 적고 있다', (eastHealth.savesFailed || 0) === 0 && (westHealth.savesFailed || 0) === 0,
	`east ${eastHealth.savesFailed} · west ${westHealth.savesFailed} 번 실패`);

// ── 실제로 걸어서 넘어 본다 ───────────────────────────────────────────
const me = joinWorld(eastPort);
await wait(2500);

if (me.id === null) {
	console.log('  ❌ east 에 못 들어갔다');
	process.exit(1);
}

send(me, { type: 'rename', name: NAME, did: 1 });
await wait(1200);

for (let step = 0; step < 900 && me.moveOn === null; step += 1) {
	send(me, { type: 'move', x: -0.15, z: 0, seq: step });
	await wait(50);
}

if (me.moveOn === null) {
	me.socket.close();
	console.log(`  ❌ 국경까지 못 갔다 (자리 ${JSON.stringify(me.here)})`);
	process.exit(1);
}

const over = joinWorld(westPort, { secret: me.secret, pass: me.moveOn.pass });
await wait(3500);
me.socket.close();
await wait(1500);

const arrived = over.id !== null;
const keptName = over.name === NAME;
over.socket.close();

check('국경을 넘어 옆 세계에 닿는다', arrived, arrived ? `west 사람 ${over.id}` : '아무 데도 못 갔다');
check('건너간 사람이 그 사람 그대로다', keptName,
	keptName ? `이름 「${NAME}」 그대로` : `이름이 「${over.name || '(없음)'}」 — 손님으로 다시 태어났다`);

if (failures === 0) {
	console.log('[prod-smoke] ✅ prod 두 세계가 돌고, 사람이 국경을 넘는다');
	process.exit(0);
}

console.log(`\n[prod-smoke] RESULT: ${failures}건`);
process.exit(1);
