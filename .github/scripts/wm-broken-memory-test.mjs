#!/usr/bin/env node
// wm-broken-memory-test.mjs — <b>못 읽는 기억을 만나면 세계는 안 뜬다</b> (TASK-WM-333).
//
// ★ 왜: 예전에는 못 읽은 것을 「기억이 없다」와 똑같이 다뤘다 — 세계가 <b>빈 세계로</b> 뜨고,
//   5초 뒤 저장 루프가 그 빈 세계를 <b>원본 위에 덮었다</b>. 읽기 실패 한 번이 기억 파괴다:
//   상자도 사람도 통째로 사라지고, 화면에는 아무 이상도 안 보인다(그냥 「새 세계」로 보인다).
//   실제로 이 자리를 상자 관문을 만들다 밟았다 — 필드 하나를 잘못 적었더니 심어 둔 상자가
//   조용히 사라졌다(`identities` 는 목록이 아니라 꾸러미다).
//
// 재는 것: 깨진 world.json 을 놓고 세계를 켠다 —
//   ① 안 뜬다(건강검사에 대답 없음) ② <b>원본이 그대로다</b>(덮이지 않았다) ③ 깨진 파일을 옆에 치워 뒀다
//
// ⚠ 「안 뜬다」가 나쁜 게 아니다 — 안 뜬 것은 곧바로 보인다(배포가 막히고 지킴이가 말한다).
//   조용히 빈 세계로 뜨는 것만이 안 보인다.
//
// exit: 0 = 안 뜨고 기억을 지켰다 · 1 = 떴거나 원본을 덮었다 · 2 = 못 돌림

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join, dirname } from 'node:path';
import { mkdtempSync, writeFileSync, readFileSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const worldPort = Number(process.env.WM_SMOKE_PORT || 5392);

function cannotRun(message) {
	console.error(`[깨진기억] CANNOT-RUN: ${message}`);
	process.exit(2);
}

const wait = (ms) => new Promise((done) => setTimeout(done, ms));

let dll;
try {
	const out = join(mkdtempSync(join(tmpdir(), 'wm-broken-app-')), 'app');
	execSync(`dotnet publish "${repo}/Server/WM.Server/WM.Server.csproj" -c Release -o "${out}" --nologo`,
		{ cwd: repo, stdio: 'pipe' });
	dll = join(out, 'WM.Server.dll');
} catch (error) {
	cannotRun(`세계를 못 지었다 — ${String(error.stderr || error.message).slice(-400)}`);
}

// 사람이 실제로 겪는 꼴로 깨뜨린다 — 아무 말이나 넣는 게 아니라 <b>거의 맞는</b> 파일이다.
// (`identities` 가 꾸러미인데 목록으로 적힌 판 — 이 관문을 만들게 한 실제 사고다.)
const folder = mkdtempSync(join(tmpdir(), 'wm-broken-'));
const worldFile = join(folder, 'world.json');
const brokenText = JSON.stringify({
	buildings: [{ x: 0, y: 0, z: 0, w: 1, l: 1, buildingId: 4005 }],
	storages: [{ x: 0, y: 0, z: 0, items: [{ itemId: 1, amount: 20 }] }],
	identities: [],
	year: 1, season: 0, day: 1, hour: 8, minute: 0,
});
writeFileSync(worldFile, brokenText, 'utf8');

const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll),
	env: { ...process.env, WM_WORLD_FILE: worldFile },
	stdio: ['ignore', 'pipe', 'pipe'],
});

let said = '';
world.stdout.on('data', (chunk) => { said += String(chunk); });
world.stderr.on('data', (chunk) => { said += String(chunk); });

let exitCode = null;
world.on('close', (code) => { exitCode = code; });

// 뜨는지 본다 — 뜨면(건강검사에 답하면) 그것이 빨강이다.
let answered = false;
{
	const until = Date.now() + 20000;
	while (Date.now() < until && exitCode === null) {
		try {
			const answer = await fetch(`http://127.0.0.1:${worldPort}/health`, { headers: { connection: 'close' } });
			if (answer.ok) { answered = true; break; }
		} catch { /* 아직/영영 */ }
		await wait(300);
	}
}

// 저장 루프가 덮을 시간을 <b>넉넉히</b> 준다 — 5초 주기라 그 이상 기다려야 「안 덮었다」가 뜻이 있다.
if (answered) await wait(9000);

try {
	if (process.platform === 'win32') execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' });
	else world.kill('SIGKILL');
} catch { /* 이미 죽었다 */ }
await wait(1000);

const nowText = readFileSync(worldFile, 'utf8');
const aside = readdirSync(folder).filter((one) => one.includes('.broken-'));

let failures = 0;
function check(what, ok, detail) {
	if (ok === false) failures += 1;
	console.log(`  ${ok ? '✅' : '❌'} ${what} — ${detail}`);
}

console.log(`  ⓘ 세계 끝난 코드 ${exitCode} · 건강검사 ${answered ? '대답함' : '대답 없음'} · 옆에 둔 파일 ${aside.length}개`);

check('깨진 기억으로는 안 뜬다', answered === false, answered ? '떴다 — 빈 세계로 뜨면 원본을 덮는다' : '안 떴다');
check('원본을 안 덮었다', nowText === brokenText,
	nowText === brokenText ? `그대로다 (${nowText.length}자)` : `바뀌었다 (${brokenText.length}자 → ${nowText.length}자)`);
check('깨진 파일을 옆에 치워 뒀다', aside.length >= 1, aside.join(', ') || '없다');
// ⚠ 한글로 견주지 않는다 (WM-326 에서 배운 것): 이 판의 콘솔 코드페이지에 따라 세계가 찍은
//   한글은 물음표가 되기도 한다 — 그걸로 자르면 <b>기계 사정이 제품 빨강</b>이 된다.
//   코드페이지와 무관한 표식으로 본다: 남긴 TASK 번호와 옆에 둔 파일 이름.
check('왜 안 떴는지 말한다', said.includes('TASK-WM-333') && said.includes('.broken-'),
	said.split(String.fromCharCode(10)).filter((one) => one.includes('.broken-') || one.includes('TASK-WM-333')).length + '줄에 사유가 있다');

if (failures === 0) {
	console.log('[깨진기억] ✅ 못 읽으면 안 뜬다 — 그리고 원본은 그대로 남는다');
	process.exit(0);
}

console.log(`\n[깨진기억] RESULT: ${failures}건`);
process.exit(1);
