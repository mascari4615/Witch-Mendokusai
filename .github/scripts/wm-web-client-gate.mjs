#!/usr/bin/env node
// wm-web-client-gate.mjs — 웹 창이 계약을 따라가고 있나 (TASK-WM-217).
//
// ★ 왜: 웹 창(index.html)은 시험이 없다. 계약에 새 말이 생겨도 창이 안 다루면
//   <b>조용히 무시</b>된다 — 화면은 멀쩡한데 그 기능만 없다. 그리고 그 안의 자바스크립트는
//   문법이 깨져도 서버가 그대로 서빙한다(브라우저에서만 죽는다).
//
// 재는 것 셋:
//   ① 안의 모듈 자바스크립트가 <b>문법적으로 선다</b> (node --check)
//   ② 계약(protocol.d.ts)의 <b>서버 → 창</b> 말들을 창이 전부 다룬다
//   ③ 손잡이(가방·상자·짓기·솥·이름)가 화면에 있다
//
// exit: 0 = 깨끗 · 1 = 걸림 · 2 = 검사를 못 돌림(파일 없음 등)

import { readFileSync, writeFileSync, mkdtempSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';
import { tmpdir } from 'node:os';


// 저장소 안에서 자기 위치로 뿌리를 잡는다 — 남의 기계·CI 어디서도 돈다
// (memo 에 두었더니 리눅스 러너에서 파일을 못 찾아 게이트가 통째로 안 돌았다).
const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const web = resolve(repo, 'Server/WM.Server/wwwroot/index.html');
const contract = resolve(repo, 'Server/WM.Server/wwwroot/protocol.d.ts');

// 창이 「받기만」 하는 말 — 여기 없으면 그 기능이 조용히 죽는다.
// (요청 쪽 말은 창이 보내는 것이므로 이 검사의 대상이 아니다.)
const EXEMPT = new Set(['WorldSnapshot']); // 스냅샷은 type 이 'world' 라 아래에서 따로 본다

function cannotRun(message) {
  console.error(`[web-client] CANNOT-RUN: ${message}`);
  process.exit(2);
}

let page;
let types;
try {
  page = readFileSync(web, 'utf8');
  types = readFileSync(contract, 'utf8');
} catch (error) {
  cannotRun(`파일을 못 읽었다 — ${error.message}`);
}

const problems = [];

// ① 모듈 자바스크립트가 서는가
const script = page.match(/<script type="module">([\s\S]*?)<\/script>/);
if (script === null) {
  cannotRun('index.html 안에서 module 스크립트를 못 찾았다');
}

const work = mkdtempSync(join(tmpdir(), 'wm-web-gate-'));
const scriptPath = join(work, 'page.mjs');
writeFileSync(scriptPath, script[1], 'utf8');

try {
  execFileSync(process.execPath, ['--check', scriptPath], { stdio: 'pipe' });
} catch (error) {
  problems.push(`창 안의 자바스크립트가 안 선다:\n      ${String(error.stderr || error.message).split('\n')[0]}`);
}

// ② 서버가 창에 보내는 말을 전부 다루나
const serverLine = types.match(/export type ServerMessage = ([^;]+);/);
if (serverLine === null) {
  cannotRun('계약에서 ServerMessage 줄을 못 찾았다');
}

const names = serverLine[1].split('|').map((one) => one.trim()).filter((one) => one.length > 0);
for (const name of names) {
  if (EXEMPT.has(name)) continue;

  // 그 말의 type 문자열을 계약에서 뽑는다: `export interface Bag { type: 'bag';`
  const shape = types.match(new RegExp(`export interface ${name} \\{\\s*type: '([^']+)'`));
  if (shape === null) {
    problems.push(`계약의 ${name} 에서 type 문자열을 못 찾았다 — 계약이 이상하다`);
    continue;
  }

  if (page.includes(`'${shape[1]}'`) === false) {
    problems.push(`창이 '${shape[1]}'(${name}) 를 안 다룬다 — 그 기능은 조용히 없는 것이 된다`);
  }
}

if (page.includes("'world'") === false) {
  problems.push("창이 'world'(스냅샷) 를 안 다룬다");
}

// ③ 손잡이가 화면에 있나
const HANDLES = [
  ['id="bag"', '가방'],
  ['id="chest"', '상자'],
  ['id="buildpick"', '짓기 고르기'],
  ['id="brewpick"', '솥 재료 고르기'],
  ['id="stir"', '넣고 젓기'],
  ['id="complete"', '완성 가져가기'],
  ['id="me"', '내 이름'],
  ['id="craftpick"', '만들 것 고르기'],
  ['id="make"', '만들기'],
];

for (const [needle, what] of HANDLES) {
  if (page.includes(needle) === false)
    problems.push(`손잡이가 사라졌다: ${what} (${needle})`);
}

console.log(`[web-client] 계약의 서버 말 ${names.length}개 · 손잡이 ${HANDLES.length}개 확인`);

if (problems.length === 0) {
  console.log('[web-client] ✅ 창이 계약을 따라가고 있다');
  process.exit(0);
}

for (const problem of problems) console.log(`  ${problem}`);
console.log(`\n[web-client] RESULT: ${problems.length}건`);
process.exit(1);
