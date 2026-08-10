#!/usr/bin/env node
// wm-game-client-gate.mjs — 게임 창이 세계를 실제로 쓰고 있나 (TASK-WM-217).
//
// ★ 왜: 웹 창에는 감시가 있다(wm-web-client-gate). 게임 창에는 없었다. 그런데 「게임 창과 웹 창이
//   같이 논다」가 목표라, 게임 쪽에서 조용히 빠지는 것이 더 뼈아프다 — 실제로 이런 일이 있었다:
//   세계는 마도서를 보내는데 게임은 <b>한 번도 안 읽었고</b>(자기 자산으로 목표를 그렸다),
//   짓기 바는 세계 목록 대신 자기 자산 전부를 늘어놓았다. 둘 다 컴파일은 멀쩡했다.
//
// 재는 것 둘:
//   ① 세계가 <b>보내는 것</b>(IWorldLink 의 읽기 자리)을 게임이 한 번이라도 읽는가
//   ② 세계에 <b>말하는 길</b>(IWorldLink 의 Request*)을 게임이 한 번이라도 부르는가
//
// 「읽는다/부른다」는 파수꾼(WorldSmokeSentinel)을 빼고 센다 — 파수꾼은 사람이 아니라 시험이라,
// 그것만 부르는 손잡이는 <b>사람에게는 없는 손잡이</b>다.
//
// exit: 0 = 깨끗 · 1 = 걸림 · 2 = 검사를 못 돌림(파일 없음 등)

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');

const contract = resolve(repo, 'Assets/_WitchMendokusai/DomainSDK/Net/IWorldLink.cs');
const roots = [
  'Assets/_WitchMendokusai/Domain',
  'Assets/_WitchMendokusai/Core',
  'Assets/_WitchMendokusai/Network',
];

// 파수꾼은 시험이다 — 여기서 부르는 것은 「사람이 쓸 수 있다」의 증거가 아니다.
const NOT_A_PERSON = ['WorldSmokeSentinel.cs'];

// 줄(IWorldLink) 자신과 그 구현은 「쓰는 쪽」이 아니다 — 계약을 적은 자리다.
const NOT_A_CONSUMER = ['IWorldLink.cs', 'WebWorldClient.cs', 'LocalWorldLink.cs'];

// 아직 사람 손잡이가 없어도 되는 것 — 이유를 적어 둔다(비면 안 된다).
const EXEMPT = new Map([
  ['MyIdentityId', '화면에 안 쓴다 — 「다시 와도 나」는 세계가 알아서 잇는다'],
  ['RequestConsume', '게임 인벤토리가 줄이 아니라 다리(WorldBagRelay)로 알린다'],
  ['TakeCompletedBrew', '솥 화면은 브릿지(SharedBrewChannel)를 거쳐 받는다'],
  ['TakeCraftResult', '제작 화면은 브릿지(WorldCraftBridge)를 거쳐 받는다'],
  ['RequestBrewStep', '자리 없는 옛 길 — 자리별 솥(RequestBrewStepAt)이 대신한다'],
  ['RequestBrewReset', '자리 없는 옛 길 — RequestBrewResetAt 이 대신한다'],
  ['RequestBrewComplete', '자리 없는 옛 길 — RequestBrewCompleteAt 이 대신한다'],
]);

function cannotRun(message) {
  console.error(`[game-client] CANNOT-RUN: ${message}`);
  process.exit(2);
}

function csharpFiles(root) {
  const found = [];
  const full = resolve(repo, root);
  const walk = (dir) => {
    let entries;
    try {
      entries = readdirSync(dir);
    } catch {
      return;
    }

    for (const entry of entries) {
      const path = join(dir, entry);
      if (statSync(path).isDirectory()) {
        walk(path);
        continue;
      }

      if (entry.endsWith('.cs') === false) continue;
      if (NOT_A_PERSON.includes(entry)) continue;
      if (NOT_A_CONSUMER.includes(entry)) continue;
      found.push(path);
    }
  };

  walk(full);
  return found;
}

let spec;
try {
  spec = readFileSync(contract, 'utf8');
} catch (error) {
  cannotRun(`계약을 못 읽었다 — ${error.message}`);
}

// 계약에서 이름을 뽑는다: `X Dolls { get; }` 와 `void RequestMove(...)`.
const reading = [...spec.matchAll(/^\s*[\w<>\[\]?.]+\s+(\w+)\s*\{\s*get;\s*\}/gm)].map((m) => m[1]);
const talking = [...spec.matchAll(/^\s*(?:void|bool|int)\s+(\w+)\s*\(/gm)].map((m) => m[1]);

if (reading.length === 0 || talking.length === 0) {
  cannotRun('계약에서 읽는 자리·말하는 길을 못 찾았다 — 검사기가 눈먼 것과 같다');
}

let body = '';
let files = 0;
const sources = [];
for (const root of roots) {
  for (const path of csharpFiles(root)) {
    const text = readFileSync(path, 'utf8');
    body += text;
    sources.push({ path, text });
    files += 1;
  }
}

if (files === 0) {
  cannotRun('게임 쪽 소스를 한 개도 못 찾았다');
}

const problems = [];

for (const name of reading) {
  if (EXEMPT.has(name)) continue;

  // `.Dolls` 처럼 <b>읽는 자리</b>로 쓰였나.
  if (new RegExp(`\\.${name}\\b`).test(body) === false)
    problems.push(`세계가 보내는 「${name}」 를 게임이 한 번도 안 읽는다 — 그 기능은 게임 창에 없는 것이다`);
}

for (const name of talking) {
  if (EXEMPT.has(name)) continue;

  if (new RegExp(`\\.${name}\\s*\\(`).test(body) === false)
    problems.push(`세계에 말하는 「${name}」 를 게임이 한 번도 안 부른다 — 사람에게 그 손잡이가 없다`);
}

// ── 보여 주는 자리에서 세계에 <b>말을 걸지</b> 않는다 ─────────────────────────
// ★ 실측 2026-08-10: 게임 제작 화면에서 「만들겠다」 요청이 <b>툴팁 갱신 자리</b>에 들어가 있었다 —
//   마우스를 옮겨 툴팁이 갱신될 때마다 제작이 나갔다는 뜻이다. 컴파일도 시험도 초록이었고,
//   사람이 눈치채려면 재료가 저절로 줄어드는 걸 봐야 한다.
//   그리는 자리는 그리기만 한다. 요청은 사람이 누를 때만 나간다.
const DRAWING = /^\s*(?:private|public|protected|internal)?\s*(?:static\s+)?(?:void|bool|string)\s+(Update\w*|Draw\w*|Refresh\w*|\w*Tooltip\w*|OnGUI)\s*\(/;
// ⚠ 「Channel.Request(」 만 찾으면 안 된다 (자가 시험이 잡았다): 실제 코드는
//   `WorldCraftBridge.Channel.Request(...)` 처럼 <b>앞에 이름이 더 붙는다</b>.
//   앞을 묶지 말고 「무엇이든 .Request…(」 를 찾되, 계약을 적은 파일은 애초에 안 본다.
//   그리고 `Request\w+` 라고 적으면 <b>`Request(` 자체를 놓친다</b>(뒤에 글자를 요구하니까) —
//   실제 결함이 `Channel.Request(...)` 여서 그 한 글자에 검사가 눈을 감았다. `\w*` 로 연다.
const SHOUTING = /\.(Request\w*|PlaceBuilding|RemoveBuilding|Rename)\s*\(/;

for (const source of sources) {
  const lines = source.text.split('\n');
  let inDrawing = false;
  let drawingName = '';
  let depth = 0;
  let started = false;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    if (inDrawing === false) {
      const found = DRAWING.exec(line);
      if (found !== null) {
        inDrawing = true;
        drawingName = found[1];
        depth = 0;
        started = false;
      }

      continue;
    }

    // ⚠ 메서드가 <b>언제 끝났나</b>를 잘못 세면 검사가 눈을 감는다 (자가 시험이 잡았다):
    //   처음엔 「중괄호 수가 0 이하면 끝」으로 봤는데, 메서드 안 첫 if 블록이 닫히는 순간
    //   0 이 되어 그 뒤를 안 봤다 — 실제 결함이 그 뒤에 있었다. 첫 `{` 를 만난 뒤부터 센다.
    const opens = (line.match(/\{/g) || []).length;
    const closes = (line.match(/\}/g) || []).length;
    if (started === false && opens > 0)
      started = true;

    depth += opens - closes;

    if (SHOUTING.test(line) && line.trim().startsWith('//') === false) {
      problems.push(
        `${source.path.split(/[\\/]/).pop()}:${i + 1} — 보여 주는 자리(${drawingName})에서 세계에 말을 건다\n` +
        `      ${line.trim()}\n` +
        '      그리는 자리는 그리기만 한다 — 요청은 사람이 누를 때만 나가야 한다');
    }

    if (started === true && depth <= 0)
      inDrawing = false;
  }
}

// ── 파수꾼은 <b>게임이 쓰는 길</b>로만 논다 ─────────────────────────────────
// ★ 왜: 파수꾼이 줄(IWorldLink)을 직접 부르면 관문은 초록인데 <b>게임 화면의 손잡이는 죽어</b>
//   있을 수 있다 — 실제로 게임 창에 상자가 아예 없던 시절에도 관문은 초록이었다(그 길을 안 지나갔다).
//   그래서 게임이 브릿지로 쓰는 것은 파수꾼도 브릿지로 쓴다. 줍기처럼 게임도 줄을 직접 쓰는 것은 예외.
{
  const sentinel = resolve(repo, 'Assets/_WitchMendokusai/Network/WorldSmokeSentinel.cs');
  const BRIDGED = ['RequestPlace', 'RequestRemove', 'RequestRename', 'RequestCraft',
    'RequestBrewStepAt', 'RequestBrewCompleteAt', 'RequestBrewResetAt', 'TakeCraftResult'];

  let watcher;
  try {
    watcher = readFileSync(sentinel, 'utf8');
  } catch {
    watcher = '';
  }

  const lines = watcher.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line.trim().startsWith('//')) continue;

    for (const name of BRIDGED) {
      if (new RegExp(`\\blink\\??\\.${name}\\s*\\(`).test(line) === false) continue;

      problems.push(
        `WorldSmokeSentinel.cs:${i + 1} — 파수꾼이 줄을 직접 부른다 (${name})\n` +
        `      ${line.trim()}\n` +
        '      게임 화면은 브릿지를 거친다 — 파수꾼도 그 길로 가야 그 손잡이가 도는지 잰다');
    }
  }
}

console.log(`[game-client] 게임 소스 ${files}개 · 읽는 자리 ${reading.length} · 말하는 길 ${talking.length} 확인`);

if (problems.length === 0) {
  console.log('[game-client] ✅ 게임 창이 세계를 실제로 쓰고 있다');
  process.exit(0);
}

for (const problem of problems) console.log(`  ${problem}`);
console.log(`\n[game-client] RESULT: ${problems.length}건`);
process.exit(1);
