#!/usr/bin/env node
// wm-main-thread-block-audit.mjs — 메인 스레드에서 끝없이 기다리지 마라 (TASK-WM-414·415).
//
// ★ 왜 이 관문이 생겼나 (2026-08-20~21 실측):
//   VersusHostListener.AcceptLoop 의 await 은 유니티 SynchronizationContext 를 잡아
//   이어달리기를 *메인 스레드*로 예약한다. VersusP2PTests 가 바로 그 메인 스레드를
//   ConnectAsync(...).GetAwaiter().GetResult() 로 막았다 → 서로를 기다리는 데드락.
//   결과 = 에디터 통째 정지(OS Responding=False), 로그 0, 취소 로그 0, 강제 종료로만 복구.
//   게다가 유니티는 끝나지 않은 테스트 판을 저장했다가 다음 기동에 자동 재개해서, 재시작해도 다시 멎었다.
//   테스트 1개가 1898개 스위트 전체를 완주 불가로 만들고 있었다.
//   룰 문서에만 적으면 다음 사람이 또 쓴다. 그래서 기계가 잡는다.
//
// 재는 것 (Assets/_WitchMendokusai/**, 서드파티 Plugins/ 제외):
//   ① 끝없이 기다릴 수 있는 것을 블로킹으로 받기 — .GetAwaiter().GetResult() / 인자 없는 .Wait()
//      (짧게 재우는 Task.Delay(...) 는 뺀다 — ms 단위라 이 사고와 무관하다)
//   ② 그물 대기에 마감시한 없음 — 같은 줄에 Connect/Send/Receive/Accept 류가 있는데 CancellationToken.None
//
// 빠져나가기: 그 줄 또는 바로 윗줄에 // main-thread-ok: <사유>
//
// exit: 0 = 위반 0 · 1 = 위반 있음 · 2 = 못 돌림 (0 을 초록으로 적지 않는다)

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join, relative } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = process.env.WM_AUDIT_ROOT ? resolve(process.env.WM_AUDIT_ROOT) : resolve(here, '..', '..');
const root = process.env.WM_AUDIT_ROOT ? repo : join(repo, 'Assets', '_WitchMendokusai');

const EXEMPT = /\/\/\s*main-thread-ok/;
const NETWORK_WAIT = /(Connect|Send|Receive|Accept|Handshake)\w*Async/;

function collect(dir, out) {
	for (const name of readdirSync(dir)) {
		const full = join(dir, name);
		const info = statSync(full);
		if (info.isDirectory()) {
			if (name === 'Plugins' || name === 'obj' || name === 'bin') continue;
			collect(full, out);
			continue;
		}
		if (name.endsWith('.cs')) out.push(full);
	}
	return out;
}

let files;
try {
	files = collect(root, []);
} catch (error) {
	console.error(`[main-thread-block] 못 돌렸다 — ${error.message}`);
	process.exit(2);
}

if (files.length === 0) {
	console.error('[main-thread-block] .cs 를 하나도 못 찾았다 — 경로가 틀렸다. 0 을 초록으로 적지 않는다.');
	process.exit(2);
}

const findings = [];

for (const file of files) {
	const lines = readFileSync(file, 'utf8').split('\n');
	lines.forEach((line, index) => {
		// 한 문장이 여러 줄로 이어지면 표식은 문장 첫 줄 위에 있다 — 세 줄까지 거슬러 본다.
		const nearby = lines.slice(Math.max(0, index - 3), index + 1);
		if (nearby.some((near) => EXEMPT.test(near))) return;
		const shown = `${relative(repo, file).split(String.fromCharCode(92)).join('/')}:${index + 1}`;
		if (/\.GetAwaiter\(\)\.GetResult\(\)|\.Wait\(\)/.test(line) && /Task\.Delay/.test(line) === false) {
			findings.push({ shown, why: '끝없이 기다릴 수 있는 것을 블로킹으로 받는다', line: line.trim() });
		}
		if (NETWORK_WAIT.test(line) && /CancellationToken\.None/.test(line)) {
			findings.push({ shown, why: '그물 대기에 마감시한이 없다 (CancellationToken.None)', line: line.trim() });
		}
	});
}

console.log(`[main-thread-block] .cs ${files.length}개 검사`);

if (findings.length === 0) {
	console.log('[main-thread-block] 위반 0');
	process.exit(0);
}

console.error(`[main-thread-block] 위반 ${findings.length}건 — 메인 스레드가 멎으면 에디터가 통째로 죽는다`);
for (const finding of findings) {
	console.error(`  ${finding.shown}  ${finding.why}`);
	console.error(`      ${finding.line.slice(0, 140)}`);
}
console.error('');
console.error('  고치는 법:');
console.error('    · 배경 루프의 await 에는 ConfigureAwait(false) — 이어달리기를 메인 스레드로 안 돌려보낸다');
console.error('    · 그물 대기에는 마감시한 — new CancellationTokenSource(TimeSpan.FromSeconds(N)).Token');
console.error('    · 정말 막아야 하면 그 줄 위에 // main-thread-ok: <사유>');
console.error('  근거: memo/rules/unity.md § 에디터를 영구 정지시키는 것 (TASK-WM-414·415)');
process.exit(1);
