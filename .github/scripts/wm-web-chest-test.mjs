#!/usr/bin/env node
// wm-web-chest-test.mjs — <b>웹 창에서 상자를 쓸 수 있나</b> (TASK-WM-359).
//
// ★ 왜: 상자는 봇으로만 쟀다(겨루기·개수 보존, WM-330). 그런데 사람은 <b>눌러서</b> 쓴다 —
//   그 길은 한 번도 안 밟아 봤고, 실제로 <b>통째로 죽어 있었다</b>:
//   상자 소식이 오면 `drawChest is not defined` 로 창이 조용히 터졌고(칸도 안 열렸다),
//   가방 줄을 눌러 넣는 길도 `HowMany` 가 없어 같이 죽어 있었다. 봇은 마디로만 겨루니 못 봤다.
//
// 재는 것 (진짜 창 하나 · 물건이 든 상자 한 채):
//   ① 상자를 열면 칸이 뜨고 그 안이 보인다 ② 줄을 누르면 가방으로 온다
//   ③ 가방 줄을 누르면 상자로 돌아간다 ④ 그 사이 창이 조용히 안 터진다
//
// 필요한 것: .NET 8 · playwright + chromium (`WM_PLAYWRIGHT_ROOT`).
// exit: 0 = 쓸 수 있다 · 1 = 못 쓴다 · 2 = 못 돌림
//
// [빨강-확인] `drawChest` 를 지우고 돌리니 빨강 — 「상자 칸이 안 열렸다」 + 창 오류
//   `drawChest is not defined` (2026-08-14). 고치기 전 제품이 바로 그 상태였다.

import { spawn, execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5754);

const ITEM_ID = 10;
const STOCK = 7;

function cannotRun(message) {
	console.error(`[상자창] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let chromium;
try {
	const root = process.env.WM_PLAYWRIGHT_ROOT;
	const req = createRequire(root ? join(resolve(root), 'package.json') : import.meta.url);
	chromium = req('playwright').chromium;
} catch (error) {
	cannotRun(`playwright 를 못 찾았다 — ${error.message}`);
}

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-chestui-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-300)}`);
}

// 사람이 서는 자리에 물건이 든 상자 하나 — 「지어서 채우는」 절차를 건너뛴다.
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-chestui-')), 'world.json');
writeFileSync(worldFile, JSON.stringify({
	buildings: [{ x: 0, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: 0, y: 0, z: 0, items: [{ itemId: ITEM_ID, amount: STOCK }] }],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
	people: [], gathered: [], cauldrons: [],
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

const browser = await chromium.launch();
const page = await browser.newPage();
const errors = [];
page.on('pageerror', (error) => errors.push(String(error)));
await page.goto(`http://127.0.0.1:${worldPort}/`);

const painted = await page.waitForFunction(
	() => typeof window.__wmView === 'object' && window.__wmView.me() !== null,
	null, { timeout: 60000 }).then(() => true).catch(() => false);

if (painted === false) { await browser.close(); killWorld(); cannotRun('창이 안 떴다'); }

// 상자를 연다 — 사람은 가까이 가서 누르지만, 여기서는 그 마디 하나로 대신한다(그 뒤는 전부 화면이다).
await page.evaluate(() => window.__wmView.socket().send(JSON.stringify({ type: 'chestask', x: 0, y: 0, z: 0, did: 501 })));
await wait(2000);

const opened = await page.evaluate(() => ({
	shown: document.getElementById('chestbox').style.display !== 'none',
	rows: document.getElementById('chest').querySelectorAll('.row').length,
	where: document.getElementById('chestwhere').textContent,
}));

let tookOut = { bagRows: 0 };
if (opened.rows > 0) {
	await page.click('#chest .row');
	await wait(1500);
	tookOut = await page.evaluate(() => ({
		bagRows: document.getElementById('bag').querySelectorAll('.row').length,
		bagText: document.getElementById('bag').textContent.trim(),
		chestText: document.getElementById('chest').textContent.trim(),
	}));
}

let putBack = { chestText: '' };
if (tookOut.bagRows > 0) {
	await page.click('#bag .row');
	await wait(1500);
	putBack = await page.evaluate(() => ({
		bagText: document.getElementById('bag').textContent.trim(),
		chestText: document.getElementById('chest').textContent.trim(),
	}));
}

await browser.close();
killWorld();

let bad = 0;
function check(what, ok, detail) {
	if (ok === false) bad += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 상자 칸 ${opened.shown ? '열림' : '안 열림'} · 줄 ${opened.rows}개 · ${opened.where}`
	+ ` · 꺼낸 뒤 가방 「${tookOut.bagText || '—'}」 · 도로 넣은 뒤 상자 「${putBack.chestText || '—'}」`);

check('상자를 열면 칸이 뜨고 그 안이 보인다', opened.shown && opened.rows > 0,
	`${opened.shown ? '열림' : '안 열림'} · 줄 ${opened.rows}개`);

check('줄을 누르면 가방으로 온다', tookOut.bagRows > 0, `가방 줄 ${tookOut.bagRows}개`);

check('가방 줄을 누르면 상자로 돌아간다', String(putBack.chestText || '').includes(String(STOCK)),
	`상자 「${putBack.chestText || '—'}」 (${STOCK}개로 돌아와야 한다)`);

check('창이 조용히 안 터졌다', errors.length === 0, errors.slice(0, 2).join(' | ') || '오류 없음');

if (bad === 0) {
	console.log('[상자창] ✅ 사람이 눌러서 상자를 쓴다');
	process.exit(0);
}

console.log(`\n[상자창] RESULT: ${bad}건`);
process.exit(1);
