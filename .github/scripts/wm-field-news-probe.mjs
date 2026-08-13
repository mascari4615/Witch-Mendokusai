#!/usr/bin/env node
// wm-field-news-probe.mjs — <b>좁은 회선 너머로 들판 소식이 오나</b>를 빠르게 여러 판 잰다 (TASK-WM-343).
//
// ★ 왜 관문이 아니라 이것을 또 만드나: 같은 것을 브라우저 관문으로 재면 한 판에 2~3분이고,
//   결과가 판마다 흔들려 <b>3판으로는 A/B 가 안 갈린다</b>(2/3 대 2/3 을 놓고 하루를 썼다).
//   창(브라우저)이 없어도 <b>세계가 소식을 내보냈나</b>는 봇으로 잴 수 있다 — 그러면 20초면 한 판이다.
//   이건 관문이 아니라 <b>자</b>다: 고칠 때 이걸로 먼저 재고, 갈린 뒤에 관문으로 확인한다.
//
// 재는 것: 광장(봇 여럿) + 좁은 회선 너머 <b>지켜보는 봇</b> 하나. 남이 들판 하나를 주우면
//   그 봇의 들판에서도 그 자리가 사라지나 — 판마다 O/X 로 세어 비율을 낸다.
//
// ⓘ 실측 (2026-08-14, 8판씩 세 번): 7/8 · 6/8 · 6/8. <b>안 오는 판이 늘 5판째</b>다 —
//   판마다 줍는 봇이 목표까지 걸어가므로 회를 거듭할수록 <b>멀어진다</b>. 그러다 그 자리가
//   지켜보는 봇의 <b>관심 반경 밖</b>으로 나가면 세계는 그 자리 소식을 더 안 보낸다(설계대로다).
//   그때 지켜보는 봇에는 <b>예전에 받아 둔 자리</b>가 남는다 — 이건 「소식이 안 온다」가 아니라
//   <b>반경 밖으로 나간 것을 창이 안 지운다</b>는 다른 문제다(다음 판에서 그걸 따로 잰다).
//
// 실행: node .github/scripts/wm-field-news-probe.mjs [판수]
// exit: 0 = 다 왔다 · 1 = 한 판이라도 안 왔다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { openBadLine } from './lib/bad-line.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5602);
const linePort = worldPort + 1;
const rounds = Number(process.argv[2] || process.env.WM_ROUNDS || 5);

const ONE_WAY_MS = 250;
const SQUEEZE_OF_DEMAND = 0.8;
const NEWS_WITHIN_MS = 8000;

function cannotRun(message) {
	console.error(`[field-news] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-fn-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-fn-')), 'world.json');
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

/** 봇 하나 — 자기 자리와 들판을 세계가 말해 준 대로 들고 있는다. */
function join_(url) {
	const one = { socket: new WebSocket(url), id: null, here: null, field: new Map(), trail: [] };
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 아래가 잡는다 */ };
	one.socket.onmessage = (event) => {
		let said;
		try { said = JSON.parse(String(event.data)); } catch { return; }
		if (said.type === 'welcome') one.id = said.id;
		if (said.type === 'me' && said.doll) one.here = { x: said.doll.x, z: said.doll.z };
		if (said.type !== 'world') return;
		if (typeof said.at === 'number') one.lastAt = said.at;

		if (Array.isArray(said.dolls) && one.id !== null) {
			const mine = said.dolls.find((doll) => doll.id === one.id);
			if (mine && typeof mine.x === 'number') one.here = { x: mine.x, z: mine.z };
		}

		if (Array.isArray(said.gatherables) || Array.isArray(said.fieldGone)) {
			one.trail.push({
				seq: said.sequence,
				통째: said.fieldChanged !== true,
				실림: (said.gatherables || []).length,
				없어짐: (said.fieldGone || []).length,
			});
			if (one.trail.length > 12) one.trail.shift();
		}

		if (Array.isArray(said.gatherables)) {
			if (said.fieldChanged !== true) one.field = new Map(said.gatherables.map((node) => [node.id, node]));
			else for (const node of said.gatherables) one.field.set(node.id, node);
		}

		for (const goneId of said.fieldGone || []) one.field.delete(goneId);
	};

	return one;
}

const crowd = [];
for (let i = 0; i < 25; i += 1) crowd.push(join_(`ws://127.0.0.1:${worldPort}/ws`));
const watcher = join_(`ws://127.0.0.1:${linePort}/ws`);

// ★ <b>진짜 창처럼 숨소리를 보낸다</b> (TASK-WM-343): 이걸 안 보내면 왕복이 0 으로 보여
//   세계가 「안 밀린다」로 읽고 <b>보통 길</b>로 보낸다 — 그러면 이 자는 밀린 길을 못 잰다.
//   (그 차이 때문에 봇 자는 7/8 인데 브라우저 관문은 1/3 이었다.)
const beating = setInterval(() => {
	if (watcher.socket.readyState !== 1 || watcher.lastAt === undefined) return;
	watcher.socket.send(JSON.stringify({ type: 'beat', ack: watcher.lastAt }));
}, 250);

const milling = setInterval(() => {
	for (const one of crowd) {
		if (one.socket.readyState !== 1) continue;
		one.socket.send(JSON.stringify({ type: 'move', x: 0.15, z: 0 }));
	}
}, 100);

await wait(6000);

if (watcher.field.size === 0) {
	clearInterval(milling);
	killWorld();
	cannotRun('지켜보는 봇이 들판을 못 받았다');
}

// 정상 수요를 재서 그 몫으로 좁힌다 (자극이 수요보다 좁으면 굶는 것이지 결함이 아니다).
const carriedBefore = badLine.peek().reduce((sum, one) => sum + (one.carried || 0), 0);
await wait(4000);
const carriedAfter = badLine.peek().reduce((sum, one) => sum + (one.carried || 0), 0);
const demand = Math.max(1200, Math.round((carriedAfter - carriedBefore) / 4));
const squeezeTo = Math.round(demand * SQUEEZE_OF_DEMAND);
badLine.squeeze(squeezeTo);
console.log(`[field-news] 정상 수요 초당 ${(demand / 1000).toFixed(1)}KB → 좁힘 초당 ${(squeezeTo / 1000).toFixed(1)}KB · ${rounds}판`);

await wait(3000);

let came = 0;
for (let round = 1; round <= rounds; round += 1) {
	// 지켜보는 봇이 보고 있는 자리 중 하나를 광장의 누가 줍는다.
	const seen = [...watcher.field.values()];
	if (seen.length === 0) {
		console.log(`  ${round}판 — 지켜보는 봇의 들판이 비었다 (셈에서 뺀다)`);
		continue;
	}

	const picker = crowd.find((one) => one.here);
	if (picker === undefined) break;

	const goal = seen
		.map((node) => ({ node, away: Math.hypot(node.x - picker.here.x, node.z - picker.here.z) }))
		.sort((left, right) => left.away - right.away)[0].node;

	for (let step = 0; step < 120; step += 1) {
		const dx = goal.x - picker.here.x;
		const dz = goal.z - picker.here.z;
		const away = Math.hypot(dx, dz);
		if (away <= 1.0) break;

		picker.socket.send(JSON.stringify({ type: 'move', x: (dx / away) * 0.15, z: (dz / away) * 0.15 }));
		await wait(60);
	}

	picker.socket.send(JSON.stringify({ type: 'gather', nodeId: goal.id, did: 1000 + round }));
	await wait(1200);

	if (picker.field.has(goal.id)) {
		console.log(`  ${round}판 — 줍기가 안 됐다 (셈에서 뺀다)`);
		continue;
	}

	let gone = false;
	const until = Date.now() + NEWS_WITHIN_MS;
	while (Date.now() < until) {
		if (watcher.field.has(goal.id) === false) { gone = true; break; }
		await wait(200);
	}

	if (gone) came += 1;
	console.log(`  ${round}판 — ${gone ? '✅ 왔다' : '❌ 안 왔다'} (자리 ${goal.id})`);

	if (gone === false) {
		console.log('     지켜보는 봇이 받은 마지막 들판 소식:');
		for (const one of watcher.trail.slice(-6))
			console.log(`       seq=${one.seq} ${one.통째 ? '통째' : '델타'} 실림=${one.실림} 없어짐=${one.없어짐}`);
	}

	watcher.trail.length = 0;
}

clearInterval(milling);
clearInterval(beating);
for (const one of crowd) { try { one.socket.close(); } catch { /* 이미 닫혔다 */ } }
try { watcher.socket.close(); } catch { /* 이미 닫혔다 */ }
await badLine.close();
killWorld();

console.log(`[field-news] ${rounds}판 중 <b>${came}판</b>에 소식이 왔다`);
process.exit(came === rounds ? 0 : 1);
