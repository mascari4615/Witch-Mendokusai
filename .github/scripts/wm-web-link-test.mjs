#!/usr/bin/env node
// wm-web-link-test.mjs — 웹 창이 <b>끊긴 뒤 다시 붙는 규칙</b>을 진짜로 돌려 본다 (TASK-WM-217).
//
// ★ 왜: 게임 창에는 `ReconnectBackoff` 시험이 있는데 웹 창에는 없었다. 그래서 배포 한 번에
//   서버가 1초 내려가면 웹 화면이 「끊겼다」로 굳고 사람이 새로고침해야 했다.
//   특히 위험한 자리 = <b>쫓겨난 창이 다시 붙는 것</b>. 세계는 나중에 온 창이 이기므로,
//   쫓겨난 창이 되붙으면 두 창이 서로를 영원히 밀어낸다. 그 규칙은 눈으로 못 본다.
//
// exit: 0 = 다 맞음 · 1 = 틀린 것 있음 · 2 = 못 돌림

import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const modulePath = resolve(repo, 'Server/WM.Server/wwwroot/link.mjs');

let link;
try {
  link = await import(pathToFileURL(modulePath).href);
} catch (error) {
  console.error(`[web-link] CANNOT-RUN: 다시 붙기 규칙을 못 읽었다 — ${error.message}`);
  process.exit(2);
}

const failures = [];

function check(what, actual, expected) {
  if (actual === expected) return;

  failures.push(`${what}\n      받은 것: ${actual}\n      바란 것: ${expected}`);
}

// ── 헛걸음 간격 ────────────────────────────────────────────────────────
{
  const plan = link.createReconnectPlan();

  check('첫 헛걸음은 0.5초 뒤', plan.closed().delayMs, 500);
  check('그 다음은 두 배', plan.closed().delayMs, 1000);
  check('또 두 배', plan.closed().delayMs, 2000);
  check('또 두 배', plan.closed().delayMs, 4000);
  check('또 두 배', plan.closed().delayMs, 8000);
  check('10초에서 멎는다', plan.closed().delayMs, 10000);
  check('아무리 오래 죽어 있어도 10초를 안 넘는다', plan.closed().delayMs, 10000);
  check('헛걸음 수를 센다', plan.attempts, 7);
}

// ── 붙으면 처음으로 ────────────────────────────────────────────────────
{
  const plan = link.createReconnectPlan();
  plan.closed();
  plan.closed();
  plan.opened();

  check('붙었다 지나면 다시 0.5초부터', plan.closed().delayMs, 500);
  check('붙었다 지나면 헛걸음 수도 처음부터', plan.attempts, 1);
}

// ── 쫓겨나면 끝 (두 창이 서로 밀어내는 것을 막는 자리) ─────────────────
{
  const plan = link.createReconnectPlan();
  plan.opened();
  plan.kicked();
  const next = plan.closed();

  check('쫓겨난 창은 다시 안 붙는다', next.retry, false);
  check('쫓겨난 창은 기다릴 것도 없다', next.delayMs, 0);
  check('쫓겨난 것을 기억한다', plan.evicted, true);
  check('다시 물어도 안 붙는다', plan.closed().retry, false);
}

// ── 한 번도 못 붙어 봤어도 다시 시도한다 (켤 때 서버가 죽어 있는 경우) ─
{
  const plan = link.createReconnectPlan();
  const first = plan.closed();

  check('처음부터 못 붙었어도 다시 붙어 본다', first.retry, true);
  check('그때도 0.5초 뒤', first.delayMs, 500);
}

// ── 사람에게 보여 줄 글 ────────────────────────────────────────────────
check('붙었을 때', link.linkStatusText('open', 0), '붙었다');
check('붙는 중일 때', link.linkStatusText('connecting', 0), '붙는 중…');
check('다시 붙는 중일 때는 몇 번째인지 보여 준다',
  link.linkStatusText('retrying', 3), '끊겼다 — 다시 붙는 중… (3번째)');
check('헛걸음 수가 없어도 1번째로 읽는다',
  link.linkStatusText('retrying', 0), '끊겼다 — 다시 붙는 중… (1번째)');
check('쫓겨났을 때는 다시 붙는다는 말을 하지 않는다',
  link.linkStatusText('evicted', 0), '다른 곳에서 접속했다 — 여기서는 나간다');
check('문제가 있을 때', link.linkStatusText('error', 0), '연결에 문제가 있다');

console.log('[web-link] 웹 창의 다시 붙기 규칙 20가지 확인');

if (failures.length === 0) {
  console.log('[web-link] ✅ 끊겨도 스스로 다시 붙고, 쫓겨나면 물러난다');
  process.exit(0);
}

for (const failure of failures) console.log(`  ${failure}`);
console.log(`\n[web-link] RESULT: ${failures.length}건`);
process.exit(1);
