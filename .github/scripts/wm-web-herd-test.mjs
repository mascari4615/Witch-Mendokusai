#!/usr/bin/env node
// wm-web-herd-test.mjs — 배포 뒤 <b>우르르 다시 붙기</b>를 세계가 받아 내나 (TASK-WM-247).
//
// ★ 왜: prod 는 push 할 때마다 세계를 껐다 켠다. 그 순간 <b>붙어 있던 모두</b>가 동시에 끊기고
//   동시에 다시 붙는다 — 평소에는 한 명씩 오던 일이 한꺼번에 온다.
//   들어올 때가 제일 비싼 자리다(첫 전체 그림 + 낱말표). 그게 한꺼번에 몰리면
//   가장 먼저 무너지는 곳이 여기다. 그런데 이 자리는 <b>한 번도 안 재봤다</b>.
//
// 재는 것: 사람 여럿을 붙여 놓고 세계를 죽였다 살린 뒤
//   ① 모두 돌아오나 ② 얼마나 걸리나 ③ 돌아온 뒤 세계를 제대로 받나(빈 세계가 아니라)
//
// 필요한 것: .NET 8. (창은 안 띄운다 — 이 자리는 소켓 문제다.)
// exit: 0 = 받아 낸다 · 1 = 못 받아 낸다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const port = Number(process.env.WM_SMOKE_PORT || 5402);
const herd = Number(process.env.WM_HERD || 40);
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-herd-')), 'world.json');

/*
 * 기준.
 * 돌아오기: 창의 다시 붙기 규칙은 0.5초에서 시작해 10초까지 늘어난다(link.mjs).
 *   여럿이 한꺼번에 와도 그 안에는 다 돌아와야 한다 — 안 그러면 사람이 새로고침을 누른다.
 * 받는 것: 돌아온 뒤 <b>전체 그림</b>을 받아야 한다. 안 그러면 그 창의 세계는 반쪽이다(WM-230).
 */
const MUST_RETURN_WITHIN_MS = 20000;

function cannotRun(message) {
	console.error(`[web-herd] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what}${detail ? ` — ${detail}` : ''}`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-herd-app-')), 'app');
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

/**
 * 창 하나 — 진짜 창처럼 <b>스스로 다시 붙는다</b>(0.5초에서 시작해 두 배씩, 10초 상한).
 * 세계가 준 열쇠를 들고 돌아가므로 「같은 사람」으로 돌아온다.
 */
function openWindow(number, where = `ws://127.0.0.1:${port}/ws`) {
	const one = {
		number,
		secret: '',
		joins: 0,
		gotWorld: 0,
		gotFullWorld: 0,
		backAt: -1,
		socket: null,
		waitMs: 500,
		alive: true,
		tries: 0,
		errors: 0,
		rearmed: false,
		id: 0,
		identity: 0,
		at: null,
		bag: null,
		field: null,
	};

	// ⚠ 다시 붙기를 <b>닫힘에만</b> 걸면 안 된다 (2026-08-13). node 22 는 <b>못 붙은</b> 소켓에
	//   error 만 주고 close 를 안 준다(node 24 는 둘 다 준다 — 실측). 그러면 세계가 내려가 있는
	//   동안의 첫 재시도에서 줄이 끊겨 <b>영영 안 붙는다</b>. 그때 이 관문은 「아무도 안 돌아왔다」
	//   라고 적었다 — 세계 탓이 아니라 <b>재는 자</b>가 고장 난 것이었다(CI 15판 빨강).
	const retryLater = () => {
		if (one.alive === false || one.rearmed) return;

		one.rearmed = true;
		setTimeout(connect, one.waitMs);
		one.waitMs = Math.min(10000, one.waitMs * 2);
	};

	const connect = () => {
		if (one.alive === false) return;

		one.tries += 1;
		one.rearmed = false;
		const socket = new WebSocket(where);
		one.socket = socket;
		socket.onopen = () => {
			one.waitMs = 500;
			socket.send(JSON.stringify({ type: 'hello', secret: one.secret }));

			// ⚠ 가방을 다시 묻는다 — 안 물으면 옛 값이 남아 <b>자기와 자기를</b> 견주게 된다(거짓 초록).
			socket.send(JSON.stringify({ type: 'bagask' }));
		};

		socket.onmessage = (event) => {
			let said;
			try { said = JSON.parse(event.data); } catch { return; }

			if (said.type === 'welcome') {
				one.joins += 1;
				one.id = said.id || 0;
				one.identity = said.identityId || one.identity;
				if (said.secret) one.secret = said.secret;
				if (one.backAt < 0) one.backAt = Date.now();
			}

			if (said.type === 'world') {
				one.gotWorld += 1;
				if (said.changed !== true) one.gotFullWorld += 1;

				// 내 자리를 적어 둔다 — 「배포 뒤에도 제자리에 서 있나」를 보는 자리 (TASK-WM-281).
				if (Array.isArray(said.dolls)) {
					const mine = said.dolls.find((doll) => doll.id === one.id);
					if (mine && typeof mine.x === 'number') one.at = { x: mine.x, z: mine.z };
				}
			}

			// 세계가 준 가방 — 배포 뒤에도 그대로여야 한다.
			if (said.type === 'bag') one.bag = JSON.stringify(said.items || []);

			// 들판도 적어 둔다 — 무언가 주워 둬야 「가방이 그대로다」가 뜻을 가진다.
			if (said.type === 'world' && Array.isArray(said.gatherables) && said.gatherables.length > 0)
				one.field = said.gatherables;
		};

		socket.onerror = () => { one.errors += 1; retryLater(); };
		socket.onclose = () => retryLater();
	};

	connect();
	return one;
}

// ── 자를 먼저 잰다 — <b>재는 자</b>가 다시 붙을 줄 아나 ─────────────────
//   이 관문이 재는 것은 「세계가 내려갔다 와도 다들 돌아오나」다. 그런데 돌아오는 쪽(창 대역)이
//   못 돌아오면, 세계가 멀쩡해도 빨강이 뜬다 — 실제로 그렇게 15판을 세계 탓으로 적었다.
//   그러니 아무도 안 듣는 포트에 대고 <b>일부러</b> 실패시켜, 스스로 다시 시도하는지부터 본다.
{
	const nobody = openWindow(-1, 'ws://127.0.0.1:59997/ws');
	await wait(2500);
	nobody.alive = false;

	if (nobody.tries < 2) {
		cannotRun(`재는 자가 다시 안 붙는다 — 2.5초에 시도 ${nobody.tries}번 (node ${process.version}).`
			+ ' 이 상태로는 무엇을 재도 「아무도 안 돌아왔다」가 나온다.');
	}
}

startWorld();
if (await waitHealthy(120000) === false) {
	killWorld();
	cannotRun('세계가 안 떴다');
}

const windows = [];
for (let i = 0; i < herd; i += 1) windows.push(openWindow(i));
await wait(4000);

const joined = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((r) => r.json());
check(`${herd}명이 세계에 있다`, joined.people >= herd, `세계가 세는 사람 ${joined.people}명`);

// ★ 저마다 <b>다른 자리</b>로 걸어가고 무언가를 줍는다 (TASK-WM-281).
//   배포는 「돌아오나」로 끝나는 일이 아니다 — 돌아온 사람이 <b>제자리에 제 가방으로</b> 서야 한다.
//   다 원점에 서 있으면 자리가 지켜졌는지 알 수 없다.
{
	for (const one of windows) {
		const angle = (one.number / herd) * Math.PI * 2;
		for (let step = 0; step < 6; step += 1) {
			if (one.socket.readyState !== 1) continue;

			one.socket.send(JSON.stringify({ type: 'move', x: Math.cos(angle) * 1.2, z: Math.sin(angle) * 1.2 }));
		}

		if (one.socket.readyState === 1) one.socket.send(JSON.stringify({ type: 'bagask' }));
	}

	await wait(1500);

	// 저마다 <b>제 옆의 것</b>까지 걸어가서 줍는다 — 빈 가방끼리 견주면 「그대로다」가 아무 말도 안 한다.
	// ⚠ 멀리서 누르면 세계가 「손이 안 닿는다」로 물린다. 그리고 걸음은 한꺼번에 몰아 보내면
	//   시계가 물린다(MoveAllowance) — 사람처럼 <b>띄엄띄엄</b> 보낸다.
	for (const one of windows) {
		one.goingTo = null;
		if (one.field === null || one.at === null) continue;

		for (const node of one.field) {
			const away = Math.hypot(node.x - one.at.x, node.z - one.at.z);
			if (one.goingTo === null || away < one.goingTo.away) one.goingTo = { id: node.id, x: node.x, z: node.z, away };
		}
	}

	{
		const until = Date.now() + 8000;
		while (Date.now() < until) {
			let walking = 0;
			for (const one of windows) {
				if (one.socket.readyState !== 1 || one.goingTo === null || one.at === null) continue;

				const away = Math.hypot(one.goingTo.x - one.at.x, one.goingTo.z - one.at.z);
				if (away <= 1.5) continue;

				walking += 1;
				const step = Math.min(1.2, away);
				one.socket.send(JSON.stringify({
					type: 'move',
					x: (one.goingTo.x - one.at.x) / away * step,
					z: (one.goingTo.z - one.at.z) / away * step,
				}));
			}

			if (walking === 0) break;
			await wait(120);
		}
	}

	for (const one of windows) {
		if (one.socket.readyState === 1 && one.goingTo !== null)
			one.socket.send(JSON.stringify({ type: 'gather', nodeId: one.goingTo.id }));
	}

	await wait(1200);
	for (const one of windows) {
		if (one.socket.readyState === 1) one.socket.send(JSON.stringify({ type: 'bagask' }));
	}

	await wait(800);
}

// ⚠ 세계는 <b>5초마다</b> 적는다. 적기 전에 죽이면 그 몇 초는 원래 잃는 것이다(크래시).
//   이 관문이 재려는 건 「적힌 뒤에 껐다 켜도 제자리로 오나」이므로, 한 번 적힐 틈을 준다.
await wait(6500);

const stoodAt = new Map(windows.map((one) => [one.number, one.at]));
const carried = new Map(windows.map((one) => [one.number, one.bag]));
const knownAs = new Map(windows.map((one) => [one.number, one.identity]));

check('죽이기 전에 무언가를 든 사람이 있다',
	[...carried.values()].filter((bag) => bag !== null && bag !== '[]').length > 0,
	`${[...carried.values()].filter((bag) => bag !== null && bag !== '[]').length}명이 뭔가 들었다`);
check('죽이기 전에 저마다 다른 자리에 섰다',
	new Set([...stoodAt.values()].filter((at) => at !== null).map((at) => `${at.x},${at.z}`)).size >= herd / 2,
	`서로 다른 자리 ${new Set([...stoodAt.values()].filter((at) => at !== null).map((at) => `${at.x},${at.z}`)).size}곳`);

// ⚠ 여기까지의 셈은 <b>전부</b> 잊는다 — 돌아온 횟수까지 지워야 한다.
//   안 지우면 붙는 도중에 한 번 튄 창이 이미 「돌아왔다」로 세어져 <b>거짓 초록</b>이 된다
//   (첫 판에 「가장 늦은 사람 0ms」가 나왔다 — 아무도 안 돌아왔는데 통과할 수 있었다).
for (const one of windows) {
	one.gotWorld = 0;
	one.gotFullWorld = 0;
	one.joins = 0;
	one.backAt = -1;

	// ⚠ 자리·가방도 지운다 — 안 지우면 죽기 전 값이 남아 <b>자기와 자기를</b> 견주게 된다.
	one.at = null;
	one.bag = null;
}

// ── 배포가 하는 일: 세계를 껐다 켠다 ─────────────────────────────────
killWorld();
const wentDownAt = Date.now();
await wait(1000);
startWorld();

if (await waitHealthy(60000) === false) {
	for (const one of windows) one.alive = false;
	cannotRun('다시 켠 세계가 안 떴다');
}

// ⚠ 무조건 20초를 자면 안 된다 — 관문이 느려지면 사람이 끈다.
//   <b>다 돌아올 때까지</b> 기다리고, 다 왔으면 바로 잰다(늦으면 그때 상한까지 기다린다).
{
	const until = Date.now() + MUST_RETURN_WITHIN_MS;
	while (Date.now() < until) {
		if (windows.every((one) => one.joins > 0 && one.gotFullWorld > 0)) break;

		await wait(200);
	}

	// 마지막 판이 오갈 틈은 준다 — 「돌아온 뒤 받은 판」을 세는 칸이 있다.
	await wait(500);
}

const back = windows.filter((one) => one.joins > 0);
const gotFull = windows.filter((one) => one.gotFullWorld > 0);
const slowest = back.reduce((worst, one) => Math.max(worst, one.backAt - wentDownAt), 0);
const after = await fetch(`http://127.0.0.1:${port}/health`, { headers: { connection: 'close' } }).then((r) => r.json());

// 빨강 줄에는 <b>왜인지</b>를 같이 적는다 — 「0/40」만 보면 세계 탓으로 읽힌다.
const tried = windows.reduce((sum, one) => sum + one.tries, 0);
const errored = windows.reduce((sum, one) => sum + one.errors, 0);

check('모두가 스스로 돌아왔다', back.length === herd,
	`${back.length}/${herd}명 · 다시 붙어 본 횟수 ${tried}번 · 붙다 만 횟수 ${errored}번`);
check('세계도 그만큼 세고 있다', after.people >= herd, `세계가 세는 사람 ${after.people}명`);
check('돌아온 뒤 <b>전체 그림</b>을 받았다 (반쪽 세계가 아니다)', gotFull.length === herd,
	`${gotFull.length}/${herd}명`);
check(`가장 늦은 사람도 ${MUST_RETURN_WITHIN_MS / 1000}초 안에 돌아왔다`,
	back.length === herd && slowest <= MUST_RETURN_WITHIN_MS, `가장 늦은 사람 ${slowest}ms`);

// ── 돌아온 뒤 <b>제자리에 제 가방으로</b> 섰나 (TASK-WM-281) ────────────
{
	let samePlace = 0;
	let sameBag = 0;
	let samePerson = 0;
	for (const one of windows) {
		const was = stoodAt.get(one.number);
		if (was !== null && one.at !== null && Math.hypot(one.at.x - was.x, one.at.z - was.z) <= 1.5)
			samePlace += 1;

		if (carried.get(one.number) !== null && one.bag === carried.get(one.number)) sameBag += 1;
		if (knownAs.get(one.number) !== 0 && one.identity === knownAs.get(one.number)) samePerson += 1;
	}

	check('돌아온 사람이 <b>같은 사람</b>이다', samePerson === herd, `${samePerson}/${herd}명`);
	check('돌아온 사람이 <b>제자리</b>에 섰다', samePlace === herd, `${samePlace}/${herd}명`);
	check('돌아온 사람의 <b>가방</b>이 그대로다', sameBag === herd, `${sameBag}/${herd}명`);
}

console.log(`  ⓘ 사람 ${herd}명 · 세계가 꺼진 순간부터 가장 늦은 복귀 ${slowest}ms`
	+ ` · 돌아온 뒤 받은 판 ${windows.reduce((sum, one) => sum + one.gotWorld, 0)}장`);

for (const one of windows) {
	one.alive = false;
	try { one.socket.close(); } catch { /* 이미 닫혔다 */ }
}

await wait(500);
killWorld();

if (failures === 0) {
	console.log(`[web-herd] ✅ 세계를 껐다 켜도 ${herd}명이 스스로 다 돌아온다`);
	process.exit(0);
}

console.log(`\n[web-herd] RESULT: ${failures}건`);
process.exit(1);
