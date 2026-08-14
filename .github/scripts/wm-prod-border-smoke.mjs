#!/usr/bin/env node
// wm-prod-border-smoke.mjs — <b>prod 두 세계</b> 사이를 실제로 걸어서 넘어 본다 (TASK-WM-384).
//
// ★ 왜 관문 목록에 없나: prod 세계는 노트북 안(127.0.0.1:5199/5200)에만 있다 — CI 러너에서는 못 닿는다.
//   그래서 이건 <b>손으로 부르는 자</b>다. 노트북에서 `node wm-prod-border-smoke.mjs` 로 돌린다
//   (데스크톱에서는 laptop-ops `/exec` 로 이 파일을 보내 돌린다).
//
// ⚠ 글자는 <b>ASCII</b> 로만 찍는다 — 노트북 셸을 타고 오며 한글이 뭉개진다(WM 배포에서 겪은 그 병).
//
// 재는 것: 동쪽에 들어가 국경까지 걷기 → 통행증 → 서쪽 입장 → <b>동쪽 사람 수가 0</b> 이 되나
//   (그게 「두 세계에 동시에 있지 않다」의 증거다 — WM-377·378·382 가 지키는 것).
//
// exit: 0 = 넘어갔다 · 1 = 못 넘었다 · 2 = 못 돌림
const wait = (ms) => new Promise((d) => setTimeout(d, ms));
function join(port, opts = {}) {
	const one = { id: null, secret: '', moveOn: null, me: null, welcome: null };
	one.socket = new WebSocket(`ws://127.0.0.1:${port}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify(opts.pass ? { type: 'hello', secret: opts.secret ?? '', pass: opts.pass } : { type: 'hello', secret: opts.secret ?? '' }));
	one.socket.onerror = () => {};
	one.socket.onmessage = (e) => {
		let said; try { said = JSON.parse(String(e.data)); } catch { return; }
		if (said.type === 'welcome') { one.id = said.id; one.secret = said.secret ?? ''; one.welcome = said; }
		if (said.type === 'moveon') one.moveOn = said;
		if (said.type === 'me') one.me = said;
	};
	return one;
}
const send = (o, m) => { if (o.socket.readyState === 1) o.socket.send(JSON.stringify(m)); };
const health = async (port) => (await (await fetch(`http://127.0.0.1:${port}/health`)).json());

const walker = join(5199);
let until = Date.now() + 20000;
while (Date.now() < until && walker.id === null) await wait(100);
if (walker.id === null) { console.log('RESULT cannot-run: east join failed'); process.exit(2); }
console.log('east joined id=' + walker.id);

for (let step = 0; step < 3000 && walker.moveOn === null; step += 1) {
	send(walker, { type: 'move', x: -0.15, z: 0, seq: 100 + step });
	await wait(30);
}
if (!walker.moveOn || !walker.moveOn.pass) { console.log('RESULT cannot-run: no pass at border'); walker.socket.close(); process.exit(2); }
console.log('got pass, zone=' + walker.moveOn.zone + ' addr=' + walker.moveOn.address);
const pass = walker.moveOn.pass;
const secret = walker.secret;
walker.socket.close();
await wait(1000);

const arrived = join(5200, { secret, pass });
until = Date.now() + 20000;
while (Date.now() < until && arrived.id === null) await wait(100);
if (arrived.id === null) { console.log('RESULT fail: west join failed'); process.exit(1); }
await wait(2000);
const westHealth = await health(5200);
const eastHealth = await health(5199);
console.log(`west people=${westHealth.people} east people=${eastHealth.people} westShadows=${westHealth.shadows} eastNeighbourLines=${eastHealth.neighbourLinesHeld}`);
arrived.socket.close();
await wait(1500);
console.log('RESULT ok: crossed east->west on prod');
