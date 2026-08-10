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
// ★ 아이템 번호만 보던 시절 (2026-08-10): 웹 창을 같이 훑자 <b>건물·재료·제작 줄</b>에서도
//   같은 함정이 넷 나왔다(나무를 솥에 못 넣고 있었다). 게임 쪽도 같은 눈으로 본다 —
//   「아직 안 골랐다」를 0 으로 쓰는 순간, 0번인 그것만 조용히 막힌다.
//   정말 1부터 매기는 번호(마도서 쪽)는 `// zero-id-ok: 사유` 로 뜻을 남긴다.
//   ⚠ 이름 모양에 눈멀지 않게 <b>대소문자·밑줄을 무시</b>한다: 처음엔 `\w*BuildingId` 로 적었다가
//     C# 상수 `BIG_BUILDING_ID` 를 못 잡았다(자가 시험이 그 자리에서 걸렸다).
const SUSPECT = /\b(\w*item\w*_?id|\w*building\w*_?id|\w*recipe\w*_?id|selected\w*|picked\w*)\s*(==|!=)\s*0\b/i;
const EXEMPT = /zero-id-ok/;

// ★ 웹 창(자바스크립트)도 같은 함정을 밟는다 (실측 2026-08-10): 창은 `=== 0` 을 쓰고,
//   여기엔 <b>건물 번호</b>까지 걸린다 — 「아직 안 골랐다」를 0 으로 쓰면 건물 0번(가문의 나무)만
//   영영 못 짓는다. 실제로 그 상태였고, 나무가 나온 조리는 「아무것도 못 얻었다」로 표시됐다.
const SUSPECT_WEB = /\b(\w*item\w*_?id|\w*building\w*_?id|\w*recipe\w*_?id|selected\w*|picked\w*)\s*(===|!==|==|!=)\s*0\b/i;

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
        const path = line.trim();

        // 웹 창도 같은 함정을 밟는다 — 창 하나가 조용히 그 종류를 지운다.
        if (path.endsWith('.cs') || path.endsWith('.html')) out.push(resolve(repo, path));
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
  const rule = file.endsWith('.html') ? SUSPECT_WEB : SUSPECT;
  for (let i = 0; i < lines.length; i++) {
    if (rule.test(lines[i]) === false) continue;
    if (EXEMPT.test(lines[i]) || (i > 0 && EXEMPT.test(lines[i - 1]))) continue;

    // ★ 저장소 뿌리를 기준으로 짧게 적는다. 여기가 옛 이름(umbrella)을 그대로 들고 있어서,
    //   <b>위반이 처음 생긴 순간 검사기가 터졌다</b> — 위반 0 인 동안에는 아무도 몰랐다.
    //   보고하는 자리는 위반이 있을 때만 도니까, 그 자리도 한 번은 밟아 봐야 한다.
    hits.push(`${file.replace(repo + '\\', '').replace(repo + '/', '')}:${i + 1}\n      ${lines[i].trim()}`);
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
