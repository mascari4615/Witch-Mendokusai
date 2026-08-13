#!/usr/bin/env node
// wm-web-picker-test.mjs — 웹 창의 고르개 계산을 진짜로 돌려 본다 (TASK-WM-217).
//
// ★ 왜: 창에는 시험이 없었다. 게이트(wm-web-client-gate)는 「문법이 서나 · 손잡이가 있나」만 본다 —
//   그런데 창이 조용히 틀리는 자리는 <b>보여 주는 글</b>이다. 실제로 이 근처에서 나무(0번)를
//   솥에 못 넣고, 가문의 나무(건물 0번)를 못 짓는 함정을 넷 밟았다.
//   DOM 은 여기서 못 돌리지만, 무슨 글을 보여 줄지는 순수 계산이라 그대로 잴 수 있다.
//
// exit: 0 = 다 맞음 · 1 = 틀린 것 있음 · 2 = 못 돌림
//
// [빨강-확인] 가진 것 세는 함수(carrying)를 빈손으로 만드니 29건 빨강 — 보여 줄 글이 통째로 틀린다 (2026-08-14)

import { readFileSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const modulePath = resolve(repo, 'Server/WM.Server/wwwroot/picker.mjs');

let picker;
try {
  picker = await import(pathToFileURL(modulePath).href);
} catch (error) {
  console.error(`[web-picker] CANNOT-RUN: 고르개 계산을 못 읽었다 — ${error.message}`);
  process.exit(2);
}

const WOOD = 0; // ⚠ 게임의 나무가 0번이다 — 이 시험의 절반이 그 사실을 지키기 위한 것이다.
const names = { 0: '나무', 4: '석탄', 5: '철광석' };

const failures = [];

function check(what, actual, expected) {
  if (actual === expected) return;

  failures.push(`${what}\n      받은 것: ${actual}\n      바란 것: ${expected}`);
}

// ── 가방 세기 ──────────────────────────────────────────────────────────
check('나무(0번)를 든 것을 센다',
  picker.carrying([{ itemId: WOOD, amount: 3 }], WOOD), 3);

check('없는 것은 0개',
  picker.carrying([{ itemId: 4, amount: 1 }], WOOD), 0);

check('빈 가방도 0개로 답한다',
  picker.carrying(null, WOOD), 0);

// ── 지을 것 ────────────────────────────────────────────────────────────
const chest = { buildingId: 4005, name: '보관 상자', w: 1, l: 1, costItemId: WOOD, costAmount: 2 };
const big = { buildingId: 3, name: '임시 블럭', w: 2, l: 2, costItemId: WOOD, costAmount: 2 };
const free = { buildingId: 9, name: '공짜 것', w: 1, l: 1, costItemId: WOOD, costAmount: 0 };

check('재료가 되면 앞에 점이 안 붙는다',
  picker.buildLabel(chest, [{ itemId: WOOD, amount: 5 }], names), '보관 상자 — 나무 5/2');

check('모자라면 앞에 점이 붙는다',
  picker.buildLabel(chest, [{ itemId: WOOD, amount: 1 }], names), '· 보관 상자 — 나무 1/2');

check('빈손이어도 나무 0개로 보인다(「없음」이 아니다)',
  picker.buildLabel(chest, [], names), '· 보관 상자 — 나무 0/2');

check('여러 칸 건물은 크기를 같이 보여 준다',
  picker.buildLabel(big, [{ itemId: WOOD, amount: 2 }], names), '임시 블럭 2×2 — 나무 2/2');

check('공짜인 것에는 재료를 안 붙인다',
  picker.buildLabel(free, [], names), '공짜 것');

check('이름을 모르면 번호로 버틴다',
  picker.buildLabel({ ...chest, costItemId: 77 }, [], {}), '· 보관 상자 — #77 0/2');

check('빈손으로는 못 짓는다', picker.canBuild(chest, []), false);
check('재료가 되면 지을 수 있다', picker.canBuild(chest, [{ itemId: WOOD, amount: 2 }]), true);
check('공짜인 것은 빈손으로도 짓는다', picker.canBuild(free, []), true);

// ── 만들 것 ────────────────────────────────────────────────────────────
const plank = { recipeId: 1, name: '나무 판자', percentage: 100, itemIds: [WOOD], amounts: [3] };
const brick = { recipeId: 15, name: '벽돌', percentage: 70, itemIds: [4, 5], amounts: [1, 1] };

check('반드시 되는 줄은 성공률을 안 붙인다',
  picker.craftLabel(plank, [{ itemId: WOOD, amount: 3 }], names), '나무 판자 — 나무 3/3');

check('가끔 실패하는 줄은 성공률을 붙인다',
  picker.craftLabel(brick, [{ itemId: 4, amount: 1 }, { itemId: 5, amount: 1 }], names),
  '벽돌 (70%) — 석탄 1/1, 철광석 1/1');

check('재료가 모자라면 점이 붙는다',
  picker.craftLabel(plank, [{ itemId: WOOD, amount: 1 }], names), '· 나무 판자 — 나무 1/3');

check('나무만으로 만드는 줄도 빈손이면 못 만든다', picker.canCraft(plank, []), false);
check('재료가 다 있으면 만들 수 있다', picker.canCraft(plank, [{ itemId: WOOD, amount: 3 }]), true);
check('하나라도 모자라면 못 만든다',
  picker.canCraft(brick, [{ itemId: 4, amount: 1 }]), false);


// ── 두 창이 같은 답을 내나 — 골든 표와 대조 ───────────────────────────────
// ★ 같은 규칙이 웹 JS 와 게임 C# 두 벌로 있다. 두 벌은 언젠가 갈라지고, 그러면 같은 세계에서
//   웹은 「지을 수 있다」, 게임은 「못 짓는다」가 된다. 답을 한 곳(picker-golden.json)에 적어 두고
//   양쪽이 각자 그 표와 대조한다 — 규칙을 바꾸려면 표부터 바꾸고, 안 고친 쪽이 그 자리에서 빨개진다.
const goldenPath = resolve(repo, 'Server/WM.Server/wwwroot/picker-golden.json');
let golden;
try {
  golden = JSON.parse(readFileSync(goldenPath, 'utf8'));
} catch (error) {
  console.error(`[web-picker] CANNOT-RUN: 골든 표를 못 읽었다 — ${error.message}`);
  process.exit(2);
}

for (const row of golden.build) {
  check(`골든: ${row.case} (글)`, picker.buildLabel(row.kind, row.bag, golden.itemNames), row.label);
  check(`골든: ${row.case} (지을 수 있나)`, picker.canBuild(row.kind, row.bag), row.canBuild);
}

for (const row of golden.craft) {
  check(`골든(제작): ${row.case} (글)`, picker.craftLabel(row.recipe, row.bag, golden.itemNames), row.label);
  check(`골든(제작): ${row.case} (만들 수 있나)`, picker.canCraft(row.recipe, row.bag), row.canCraft);
}

// 겨냥 — 세계의 마도서 쪽은 `targetX/targetY` 로 적힌다. 창은 그것을 x/y 로 받아 그린다.
for (const row of golden.aim) {
  const pages = row.pages.map((page) => ({
    id: page.id, name: page.name, x: page.targetX, y: page.targetY,
    radius: page.radius, amount: page.amount,
  }));

  check(`골든(겨냥): ${row.case}`, picker.aimingText(pages, row.at), row.text);
}

console.log(`[web-picker] 창의 고르개 계산 17가지 · 골든 표 `
  + `${golden.build.length + golden.craft.length + golden.aim.length}줄 확인`);

if (failures.length === 0) {
  console.log('[web-picker] ✅ 보여 주는 글이 맞다');
  process.exit(0);
}

for (const failure of failures) console.log(`  ${failure}`);
console.log(`\n[web-picker] RESULT: ${failures.length}건`);
process.exit(1);
