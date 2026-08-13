#!/usr/bin/env node
// wm-threshold-audit.mjs — <b>절대 밀리초 문턱</b>에 사유가 붙어 있는지 본다 (TASK-WM-323).
//
// ★ 왜: 관문의 문턱이 절대 밀리초면 그건 <b>환경 주장</b>이 되기 쉽다 — 느린 기계에서는
//   제품이 멀쩡해도 빨개진다. 오늘 실제로 그랬다: 짓기 관문의 「0.5초 안에 대답」이
//   2코어 CI 에서만 빨갰다(같은 코드가 이 기계에서는 초록, TASK-WM-322).
//   규율은 이미 있다(domain-wm.md § 관문 규율 ④) — 없던 것은 <b>그 규율을 지키는지 보는 자</b>다.
//
// 규칙: `check(...)` 줄에 세 자리 이상 숫자로 자르는 문턱이 있으면, 그 위 다섯 줄 안에
//   `[문턱-사유]` 한 줄이 있어야 한다. 사유는 셋 중 하나여야 뜻이 있다 —
//   (a) 같은 판의 <b>다른 값과의 견줌</b>(세계 답의 절반 · 곧은 회선과의 차이)
//   (b) <b>사람이 느끼는 선</b>의 넉넉한 절대값(1초 안에 무엇이든 말한다)
//   (c) <b>제품 상수</b>(걸음 0.15m · 회선 300ms 같은, 기계와 무관한 값)
//
// ⚠ 이 감사는 <b>문턱을 없애라</b>가 아니다. 「왜 이 숫자인가」를 옆에 적게 할 뿐이다 —
//   적다 보면 환경 주장인 것이 스스로 드러난다.
//
// 실행: node .github/scripts/wm-threshold-audit.mjs
// exit: 0 = 다 사유가 있다 · 1 = 사유 없는 문턱이 있다 · 2 = 못 돌림

import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const scriptDir = resolve(here);

/** 이 숫자보다 작으면 「자릿수」가 아니라 개수·비율일 때가 많다 — 밀리초 문턱은 대개 세 자리다. */
const LOOKS_LIKE_MS = 100;

/** 사유를 이 태그로 적는다 — 사람이 찾기 쉽고, 기계가 세기도 쉽다. */
const REASON_TAG = '[문턱-사유]';

// 자기 자신은 뺀다 — 이 파일의 <b>설명</b>에 든 숫자를 문턱으로 셀 이유가 없다.
const files = readdirSync(scriptDir)
	.filter((one) => one.startsWith('wm-') && one.endsWith('.mjs'))
	.filter((one) => one !== 'wm-threshold-audit.mjs');
if (files.length < 5) {
	console.error(`[문턱] CANNOT-RUN: 관문을 ${files.length}개밖에 못 찾았다 — 경로 확인.`);
	process.exit(2);
}

const missing = [];
let found = 0;

for (const name of files) {
	const lines = readFileSync(join(scriptDir, name), 'utf8').split('\n');

	for (let i = 0; i < lines.length; i += 1) {
		const line = lines[i];
		if (line.includes('check(') === false) continue;

		// 이 줄(그리고 이어지는 두 줄)에서 「<= 1234」 꼴을 찾는다.
		const window = [line, lines[i + 1] ?? '', lines[i + 2] ?? ''].join(' ');
		const cuts = [...window.matchAll(/[<>]=?\s*(\d{3,})/g)].map((one) => Number(one[1]));
		const absolute = cuts.filter((one) => one >= LOOKS_LIKE_MS);
		if (absolute.length === 0) continue;

		found += 1;

		// 사유는 그 위 다섯 줄 안에 적는다(주석이 길어 여러 줄이 되므로 넉넉히 본다).
		const above = lines.slice(Math.max(0, i - 6), i).join('\n');
		if (above.includes(REASON_TAG)) continue;

		missing.push({ name, line: i + 1, cut: absolute.join(','), text: line.trim().slice(0, 90) });
	}
}

console.log(`[문턱] 관문 ${files.length}개 · 절대 숫자로 자르는 자리 ${found}곳 · 사유 없는 곳 ${missing.length}곳`);

if (missing.length === 0) {
	console.log('[문턱] ✅ 자르는 숫자마다 왜 그 숫자인지가 옆에 적혀 있다');
	process.exit(0);
}

for (const one of missing) {
	console.log(`  ❌ ${one.name}:${one.line} — ${one.cut}`);
	console.log(`     ${one.text}`);
}

console.log(`\n[문턱] 위 자리마다 <b>그 위</b>에 ${REASON_TAG} 한 줄을 적어라 — 셋 중 하나여야 한다:`);
console.log('       (a) 같은 판의 다른 값과의 견줌  (b) 사람이 느끼는 선  (c) 제품 상수');
console.log('       적을 사유가 없으면 그 문턱은 환경 주장이다 — 견줄 값을 만들어 상대값으로 바꿔라.');
process.exit(1);
