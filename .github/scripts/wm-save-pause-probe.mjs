#!/usr/bin/env node
// wm-save-pause-probe.mjs — <b>기억을 적는 동안 세계가 멎지 않나</b> (TASK-WM-353).
//
// ⚠ 이건 <b>관문이 아니라 자</b>다: 세계를 일부러 괴롭혀도(6.9MB 기억을 초당 13번 적기)
//   초당 17.8판·최대 230ms 로 안 멎었다 — <b>빨개지지 않는 관문은 증거가 아니다</b>(규율 ⑥).
//   대신 이 자로 재다가 진짜 결함이 나왔다: 되살리기가 제곱이라 큰 세계가 <b>안 뜬다</b>(고쳤다).
//
// ★ 왜: 세계는 이따금 자기 기억을 통째로 파일에 적는다. 세계가 커질수록(집·상자·장부)
//   그 한 번이 길어진다 — 그 사이 판이 멈추면 <b>모두의 화면이 같이</b> 멎는다.
//   여태 잰 것은 「적었나·실패했나」뿐이고, <b>적는 동안 사람들은 어땠나</b>는 안 쟀다.
//
// 재는 것 (집 2000채짜리 큰 세계 · 봇 여덟이 걷는 동안):
//   ① 기억을 실제로 여러 번 적었나(안 적었으면 이 시험은 아무것도 안 본 것이다)
//   ② 그 사이 봇들이 받는 판이 <b>초당 여덟 장</b> 아래로 안 떨어지나 (제품이 약속한 바닥)
//   ③ 세계가 한 번도 오래 멎지 않았나
//
// 실행: node .github/scripts/wm-save-pause-probe.mjs
// exit: 0 = 안 멎는다 · 1 = 멎는다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5530);

/** 큰 세계 — 집 2000채. 「크면 어떻게 되나」를 보는 것이 이 시험의 전부다. */
const HOUSES = Number(process.env.WM_HOUSES || 2000);
const BOTS = Number(process.env.WM_BOTS || 8);
const WATCH_SECONDS = 20;

/**
 * 사람이 「끊긴다」로 넘어가는 선 — 세계는 초당 20판을 말한다(제품 상수).
 * [문턱-사유] (c) 제품 상수 — 초당 여덟 장은 좁은 회선 관문(WM-343)이 쓰는 것과 같은 바닥이다.
 */
const LEAST_PLATES_PER_SECOND = 8;

/**
 * 세계가 한 번에 멎어도 되는 최대.
 * [문턱-사유] (c) 사람이 느끼는 선 — 0.5초 넘게 멎으면 걷던 사람이 그 자리에서 튄다.
 */
const MOST_TICK_GAP_MS = 500;

function cannotRun(message) {
	console.error(`[저장멈춤·자] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-save-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

// 집 2000채를 심는다 — 「지어서 채우는」 절차를 거치면 시험이 길고 흔들린다.
const buildings = [];
for (let i = 0; i < HOUSES; i += 1) {
	buildings.push({ x: (i % 100) - 150, y: 0, z: Math.floor(i / 100) - 150, w: 1, l: 1, buildingId: 4001 });
}

const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-save-')), 'world.json');
writeFileSync(worldFile, JSON.stringify({
	buildings,
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
	people: [], gathered: [], cauldrons: [], storages: [],
}), 'utf8');

const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll), env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore',
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
	if (up === false) { killWorld(); cannotRun('세계가 안 떴다'); }
}

{
	const health = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());
	if (health.buildings < HOUSES) {
		killWorld();
		cannotRun(`큰 세계가 안 실렸다 — 집 ${health.buildings}채 (${HOUSES}채를 심었다)`);
	}
}

function join_() {
	const one = { plates: 0 };
	one.socket = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	one.socket.onopen = () => one.socket.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.socket.onerror = () => { /* 수로 본다 */ };
	one.socket.onmessage = (event) => {
		try { if (JSON.parse(String(event.data)).type === 'world') one.plates += 1; } catch { /* 딴 소식 */ }
	};

	return one;
}

const bots = [];
for (let i = 0; i < BOTS; i += 1) bots.push(join_());
await wait(3000);

// 걷는다 — 걸어야 세계가 「적을 것이 있다」로 보고 기억을 적는다(안 걸으면 저장이 안 돈다).
const walking = setInterval(() => {
	for (const one of bots) {
		if (one.socket.readyState !== 1) continue;
		one.socket.send(JSON.stringify({ type: 'move', x: Math.random() < 0.5 ? 0.15 : -0.15, z: 0 }));
	}
}, 100);

const before = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());
for (const one of bots) one.plates = 0;

await wait(WATCH_SECONDS * 1000);

const after = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } }).then((one) => one.json());
clearInterval(walking);

const platesPerSecond = bots.map((one) => one.plates / WATCH_SECONDS);
const saves = after.savesDone - before.savesDone;
const fileMegabytes = statSync(worldFile).size / 1024 / 1024;

for (const one of bots) { try { one.socket.close(); } catch { /* 이미 */ } }
killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 집 ${HOUSES}채 · 기억 파일 ${fileMegabytes.toFixed(1)}MB · ${WATCH_SECONDS}초에 ${saves}번 적었다`
	+ ` · 봇이 받은 판 초당 ${platesPerSecond.map((rate) => rate.toFixed(1)).join('/')}`);

// ★ 「안 적었는데 초록」이 제일 나쁘다 — 그건 아무것도 안 본 것이다 (관문 규율 ②).
if (saves === 0) cannotRun('그 사이 기억을 한 번도 안 적었다 — 잴 것이 없었다');

check(`적는 동안에도 초당 ${LEAST_PLATES_PER_SECOND}판은 간다`,
	platesPerSecond.every((rate) => rate >= LEAST_PLATES_PER_SECOND),
	`가장 적게 받은 봇 초당 ${Math.min(...platesPerSecond).toFixed(1)}판`);

check(`세계가 한 번도 ${MOST_TICK_GAP_MS}ms 넘게 안 멎는다`,
	after.longestTickGapMs <= MOST_TICK_GAP_MS, `가장 벌어진 판 ${after.longestTickGapMs}ms`);

check('적다가 실패하지 않았다', after.savesFailed === 0, `실패 ${after.savesFailed}번`);

if (bad === 0) {
	console.log('[저장멈춤·자] ✅ 큰 세계를 적는 동안에도 사람들의 세계는 흐른다');
	process.exit(0);
}

console.log(`\n[저장멈춤·자] RESULT: ${bad}건`);
process.exit(1);
