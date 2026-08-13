#!/usr/bin/env node
// wm-red-walk-audit.mjs — <b>관문마다 「빨개지는 것을 봤다」가 적혀 있나</b> (TASK-WM-336).
//
// ★ 왜 (2026-08-14, 하루에 세 번): 관문이 초록인데 <b>아무것도 안 지키고</b> 있었다.
//   · 우르르(WM-318): 통행증을 아무도 못 쓰게 막았는데 도착 6/6 으로 통과 — 「수」만 셌다.
//   · 창 여럿(WM-328): 남의 인형이 <b>서 있는 채로</b> 보이기만 해도 통과 — 「보인다」만 봤다.
//   · 통행증 재사용(WM-335): <b>자물쇠를 꺼도 초록</b> — 통행증이 가방을 덮어쓰는 줄 몰랐다.
//   셋 다 「제품이 고장 났을 때 이 관문이 정말 빨개지나」를 <b>안 밟아 봤기</b> 때문이다.
//   초록은 증거가 아니다 — <b>빨개지는 것을 본 초록</b>만 증거다.
//
// 규칙: 관문 파일마다 `[빨강-확인]` 한 줄. 무엇을 부러뜨렸고 그때 무엇이 빨개졌는지 적는다.
//   예) `// [빨강-확인] firstCrossing 자물쇠를 끄니 40개로 빨강 (20 → 40)`
//
// ⚠ 이 감사는 <b>빚 목록(baseline)</b>을 갖는다. 이미 있는 관문 26개를 오늘 다 밟을 수는 없다 —
//   그러나 <b>새로 만드는 관문</b>은 처음부터 밟게 한다. 빚은 줄이면 그 줄을 지운다
//   (zero-target-audit·enum 기준선과 같은 꼴).
//
// 실행: node .github/scripts/wm-red-walk-audit.mjs [--write-baseline]
// exit: 0 = 새 관문에 다 적혀 있다 · 1 = 안 적힌 새 관문이 있다 · 2 = 못 돌림
//
// [빨강-확인] 태그 없는 가짜 관문을 하나 놓으니 빨강(exit 1) — 지우니 초록 (2026-08-14)

import { readdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const scriptDir = resolve(here);
const baselinePath = join(scriptDir, 'wm-red-walk-baseline.tsv');

const TAG = '[빨강-확인]';

const gates = readdirSync(scriptDir)
	.filter((one) => one.startsWith('wm-') && one.endsWith('.mjs'))
	.filter((one) => one.includes('test') || one.includes('smoke'));

if (gates.length < 10) {
	console.error(`[빨강걷기] CANNOT-RUN: 관문을 ${gates.length}개밖에 못 찾았다 — 경로 확인.`);
	process.exit(2);
}

const missing = gates.filter((one) => readFileSync(join(scriptDir, one), 'utf8').includes(TAG) === false);

if (process.argv.includes('--write-baseline')) {
	const head = [
		'# wm-red-walk 기준선 — 아직 「빨개지는 것을 본 적 없는」 관문들 (이미 진 빚).',
		'# 여기 없는 <새> 관문은 처음부터 [빨강-확인] 을 적어야 한다.',
		'# 빚을 갚으면(밟아 보고 적으면) 그 줄을 지운다. 지운 줄이 다시 나타나면 그때부터 빨강이다.',
		'# 갱신: node .github/scripts/wm-red-walk-audit.mjs --write-baseline',
	];
	writeFileSync(baselinePath, [...head, ...missing].join('\n') + '\n', 'utf8');
	console.log(`[빨강걷기] 기준선을 적었다 — 빚 ${missing.length}건 / 관문 ${gates.length}개`);
	process.exit(0);
}

const debt = existsSync(baselinePath)
	? new Set(readFileSync(baselinePath, 'utf8').split('\n').map((one) => one.trim()).filter((one) => one && one.startsWith('#') === false))
	: new Set();

const fresh = missing.filter((one) => debt.has(one) === false);
const paid = [...debt].filter((one) => missing.includes(one) === false);

console.log(`[빨강걷기] 관문 ${gates.length}개 · 빨강을 본 관문 ${gates.length - missing.length}개`
	+ ` · 아직 안 본 관문 ${missing.length}개(빚 ${debt.size}) · 새로 빠진 것 ${fresh.length}개`);

if (paid.length > 0) {
	console.log(`[빨강걷기] 빚을 갚은 관문 ${paid.length}개 — 기준선에서 지워라: ${paid.join(', ')}`);
}

if (fresh.length === 0) {
	console.log('[빨강걷기] ✅ 새 관문은 전부 「빨개지는 것을 봤다」를 적었다');
	process.exit(0);
}

for (const one of fresh) console.log(`  ❌ ${one} — ${TAG} 이 없다`);
console.log(`\n[빨강걷기] 초록은 증거가 아니다. 제품을 <b>일부러 부러뜨려</b> 이 관문이 빨개지는 것을 보고,`);
console.log(`           무엇을 껐고 그때 무엇이 어떻게 나왔는지 ${TAG} 한 줄로 남겨라.`);
process.exit(1);
