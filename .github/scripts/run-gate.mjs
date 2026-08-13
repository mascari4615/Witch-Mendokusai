#!/usr/bin/env node
// run-gate.mjs — 관문 하나를 돌리되 <b>「못 쟀다」를 빨강으로 안 친다</b> (TASK-WM-324).
//
// ★ 왜: 관문 규율은 이미 말한다(domain-wm.md ④②) — 잴 것이 안 오면 0 을 초록·빨강 어느 쪽으로도
//   적지 말고 <b>CANNOT-RUN(exit 2)</b> 로 끝내라. 그런데 워크플로는 「0 아니면 실패」라
//   그 뜻이 CI 에서 통째로 뒤집혔다: 느린 기계에서 표본이 안 차면 <b>제품이 빨개진다</b>.
//   오늘만 두 번 그랬다(공평함 관문 표본 부족 · 매끄러움 관문 프레임 부족).
//
// ★ 왜 스텝마다가 아니라 여기인가: 스텝마다 `if ($code -eq 2)` 를 적으면 다음에 붙는 관문은
//   또 잊는다. 관문을 <b>부르는 길</b>을 하나로 두면 새 관문도 처음부터 옳게 돈다.
//
// 쓰는 법: node .github/scripts/run-gate.mjs .github/scripts/wm-web-smooth-test.mjs [인자…]
// exit: 0 = 초록 <b>또는</b> 못 쟀다(경고) · 1 = 빨강 · 2 = 부를 것을 못 찾음

import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';

const [target, ...rest] = process.argv.slice(2);

if (!target) {
	console.error('[run-gate] 부를 관문을 안 줬다: node run-gate.mjs <관문.mjs> [인자…]');
	process.exit(2);
}

if (existsSync(target) === false) {
	console.error(`[run-gate] 그런 관문이 없다: ${target}`);
	process.exit(2);
}

const child = spawn(process.execPath, [target, ...rest], { stdio: 'inherit' });

child.on('error', (error) => {
	console.error(`[run-gate] 못 돌렸다 — ${error.message}`);
	process.exit(2);
});

child.on('close', (code) => {
	if (code === 2) {
		// ⚠ 「못 쟀다」는 제품 소식이 아니다 — 그날의 기계 사정이다. 눈에 보이게 남기되 안 세운다.
		console.log(`::warning::${target} 를 이번 판에서는 못 쟀다 (CANNOT-RUN) — 빨강 아님`);
		process.exit(0);
	}

	process.exit(code ?? 1);
});
