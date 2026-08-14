#!/usr/bin/env node
// wm-gate-wiring-audit.mjs — <b>관문을 만들어 놓고 CI 에 안 건 것</b>이 없나 (TASK-WM-366).
//
// ★ 왜: 관문은 <b>돌아야</b> 관문이다. 파일만 있고 워크플로에 안 걸리면 그건 아무도 안 보는 글이다 —
//   그리고 그 사실은 <b>아무 데도 안 적힌다</b>(빨간 게 아니라 실행이 없어서 초록으로 읽힌다).
//   이 저장소는 그 사고를 이미 겪었다(WM-2xx: 트리거에 입력이 빠져 게이트가 한 번도 안 돌았다).
//   여기서는 그 위층 — <b>돌리는 줄 자체가 없는</b> 관문을 잡는다.
//
// 재는 것: `.github/scripts/wm-*-test.mjs` / `*-smoke.mjs` 마다
//   워크플로 어딘가에 <b>그 파일을 실제로 돌리는 줄</b>이 있나(`node ... 그파일`).
//   ⚠ `paths:` 트리거에 이름만 적힌 것은 <b>안 친다</b> — 그건 「언제 도나」지 「도나」가 아니다.
//
// 실행: node .github/scripts/wm-gate-wiring-audit.mjs [--strict]
// exit: 0 = 다 걸려 있다 · 1 = 안 걸린 관문 있음(--strict) · 2 = 못 돌림
//
// [빨강-확인] 워크플로에서 「무리 속 창 셋」 관문의 돌리는 줄을 지워 보니
//   「안 걸린 관문 1개 — wm-web-crowd-windows-test.mjs」로 빨강 (2026-08-14).

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const workflows = join(repo, '.github', 'workflows');

function cannotRun(message) {
	console.error(`[관문배선] CANNOT-RUN: ${message}`);
	process.exit(2);
}

if (existsSync(workflows) === false) cannotRun(`워크플로 폴더를 못 찾았다 — ${workflows}`);

const gates = readdirSync(here).filter((name) =>
	name.startsWith('wm-') && name.endsWith('.mjs') && (name.includes('-test') || name.includes('-smoke')));

if (gates.length === 0) cannotRun('관문 파일이 하나도 없다 — 이 자가 헛것을 보고 있다');

const yaml = readdirSync(workflows)
	.filter((name) => name.endsWith('.yml') || name.endsWith('.yaml'))
	.map((name) => readFileSync(join(workflows, name), 'utf8'))
	.join('\n');

if (yaml.length === 0) cannotRun('워크플로 글이 비었다');

// ★ 진짜 창 관문은 <b>목록 파일</b>로 돈다 (TASK-WM-369) — 워크플로에는 갈래 한 줄만 있고,
//   무엇을 도는지는 이 목록이 정한다. 그러니 목록에 있는 것도 「돌린다」로 친다.
const webListPath = join(here, 'wm-web-gates.tsv');
const webList = existsSync(webListPath) ? readFileSync(webListPath, 'utf8') : '';

/** 그 관문을 <b>실제로 돌리는</b> 줄이 있나 — 이름만 적힌 트리거 목록은 안 친다. */
function isRun(gate) {
	// 목록에 <b>주석이 아닌 줄</b>로 적혀 있으면 갈래 러너가 돌린다.
	for (const line of webList.split(String.fromCharCode(10))) {
		const trimmed = line.trim();
		if (trimmed.startsWith('#') || trimmed.length === 0) continue;
		if (trimmed.split(String.fromCharCode(9))[0].trim() === gate) return true;
	}

	for (const line of yaml.split('\n')) {
		if (line.includes(gate) === false) continue;

		const trimmed = line.trim();
		if (trimmed.startsWith('-') && trimmed.includes('node') === false) continue;   // paths: 목록
		if (trimmed.includes('node ')) return true;
	}

	return false;
}

const sleeping = gates.filter((gate) => isRun(gate) === false);

console.log(`[관문배선] 관문 ${gates.length}개 · 돌리는 줄이 있는 관문 ${gates.length - sleeping.length}개`);

if (sleeping.length > 0) {
	console.log(`\n[관문배선] 안 걸린 관문 ${sleeping.length}개 — 만들어 놓고 아무도 안 돌린다:`);
	for (const gate of sleeping) console.log(`  · ${gate}`);
	console.log('\n        워크플로에 `node ./.github/scripts/run-gate.mjs ./.github/scripts/<그 관문>` 줄을 넣어라.');
	console.log('        (트리거 `paths:` 에 이름만 적는 것은 「언제 도나」지 「도나」가 아니다.)');
	if (process.argv.includes('--strict')) process.exit(1);
	process.exit(0);
}

console.log('[관문배선] ✅ 관문마다 돌리는 줄이 있다');
process.exit(0);
