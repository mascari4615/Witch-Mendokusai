#!/usr/bin/env node
// wm-zero-id-guard.mjs — 「번호 0을 없음으로」 쓰는 자리를 찾는다 (TASK-WM-217).
//
// ★ 왜: WM 의 **나무가 아이템 0번**이다. 그래서 `itemId == 0` 을 「없음/미설정」으로 쓰면
//   그 종류만 세계에서 조용히 빠진다 — 예외도 로그도 없다. 실제로 2026-08-10 하루에
//   ① 들판에 나무가 안 자람 ② 결과가 나무인 레시피가 「빈 결과」 ③ 관문이 나무를 주우면
//   「못 주웠다」로 판정, 셋을 밟았다. 사람이 기억으로 막을 부류가 아니라 기계가 막아야 한다.
//
// 통과 방법(둘 중 하나):
//   ① 「없음」을 null·bool 플래그·1부터 시작하는 다른 키로 표현한다 (권장)
//   ② 그 줄 끝이나 바로 윗줄에 `// zero-id-ok: <사유>` 를 단다
//
// exit: 0 = 깨끗 · 1 = 걸린 자리 있음 · 2 = 검사를 못 돌림(대상 0개 등)

import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const ROOTS = [
  'WitchMendokusai/Assets/_WitchMendokusai',
  'WitchMendokusai/Server',
];

// 「아이템 번호를 0과 비교」하는 모양. 변수 이름에 item 이 들어간 것만 본다
// (건물·레시피 번호는 1부터라 0 비교가 안전하다).
const SUSPECT = /\b\w*[Ii]tem\w*Id\s*(==|!=)\s*0\b/;
const EXEMPT = /zero-id-ok/;

// 저장소 안에서 자기 위치로 뿌리를 잡는다 — 남의 기계·CI 어디서도 돈다
// (memo 에 두었더니 리눅스 러너에서 파일을 못 찾아 게이트가 통째로 안 돌았다).
const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');

function listFiles() {
  const out = [];
  for (const root of ROOTS) {
    try {
      const listed = execFileSync('git', ['-C', repo, 'ls-files', root.replace('WitchMendokusai/', '')], {
        encoding: 'utf8',
      });
      for (const line of listed.split('\n')) {
        if (line.trim().endsWith('.cs')) out.push(resolve(repo, line.trim()));
      }
    } catch {
      // 목록을 못 얻으면 아래에서 「못 돌림」으로 끝난다.
    }
  }

  return out;
}

const files = listFiles();
if (files.length < 20) {
  console.error(`[zero-id] CANNOT-RUN: 검사 대상이 ${files.length}개뿐이다 — 경로를 확인할 것.`);
  process.exit(2);
}

const hits = [];
for (const file of files) {
  let text;
  try {
    text = readFileSync(file, 'utf8');
  } catch {
    continue;
  }

  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i++) {
    if (SUSPECT.test(lines[i]) === false) continue;
    if (EXEMPT.test(lines[i]) || (i > 0 && EXEMPT.test(lines[i - 1]))) continue;

    hits.push(`${file.replace(`${umbrella}/`, '')}:${i + 1}\n      ${lines[i].trim()}`);
  }
}

console.log(`[zero-id] 검사 ${files.length}개 파일`);
if (hits.length === 0) {
  console.log('[zero-id] ✅ 「번호 0 = 없음」으로 쓰는 자리 없음');
  process.exit(0);
}

for (const hit of hits) console.log(`  ${hit}`);
console.log(`\n[zero-id] RESULT: ${hits.length}건 — 나무가 0번이라 이 자리는 그 종류를 조용히 지운다.`);
console.log('        고치는 법: null·bool 플래그·1부터 시작하는 키로. 정말 안전하면 `// zero-id-ok: 사유`.');
process.exit(1);
