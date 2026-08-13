#!/usr/bin/env node
// wm-action-once-audit.mjs — <b>세계를 바꾸는 말은 모두 「한 번만」 문을 지난다</b> (TASK-WM-308).
//
// ★ 왜 관문이 아니라 <b>감사</b>인가: 끊기는 순간 사라지는 것을 고친 길(WM-305·307)은
//   말마다 <b>손으로</b> 문을 세운다. 새 말이 하나 생기면 그때 잊으면 그만이다 —
//   그리고 그 사실은 <b>사람이 그 말을 하다 끊길 때</b>에야 드러난다(그때는 이미 물건이 샜다).
//   그래서 「빠뜨릴 수 있음」 자체를 없앤다: 여기 이름이 없는 말은 <b>빨강</b>이다.
//
// 재는 것: `WorldHost.HandleMessage` 안의 `if (kind == Protocol.X)` 를 전부 찾아
//   ① 세계를 바꾸는 말이면 그 블록 안에서 `ShouldDo` 를 지나는지
//   ② 어느 쪽인지 아직 안 정한 새 말이 있는지.
//
// exit: 0 = 다 지킨다 · 1 = 빠진 말이 있다 · 2 = 못 돌림

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const hostFile = join(repo, 'Server', 'WM.Server', 'WorldHost.cs');

/** 세계를 바꾸는 말 — 두 번 하면 물건·집·몸이 달라진다. 「한 번만」 문을 반드시 지나야 한다. */
const CHANGES_WORLD = new Set([
	'GATHER', 'PLACE', 'REMOVE', 'CONSUME', 'SAY',
	'CHEST_PUT', 'CHEST_TAKE', 'CRAFT', 'BREW', 'BREW_COMPLETE', 'BREW_RESET',
	'RENAME', 'STRIKE',
]);

/** 세계를 안 바꾸는 말 — 물어보기·인사·걸음. 문을 안 지나도 된다(사유를 여기 적는다). */
const HARMLESS = new Map([
	['HELLO', '인사 — 신원을 정하는 자리라 번호가 없다(통행증·열쇠가 그 몫을 한다)'],
	['BAG_ASK', '물어보기 — 두 번 물어도 가방은 그대로다'],
	['CHEST_ASK', '물어보기 — 상자를 열어 보는 것뿐이다'],
	['INVITE_ASK', '물어보기 — 초대장을 한 장 더 받을 뿐이다'],
	['LINK', '계정 잇기 — 같은 계정을 두 번 이어도 결과가 같다(멱등)'],
	['MOVE', '걸음 — 다음 걸음이 곧 고친다. 다시 보내면 오히려 두 걸음이 된다'],
	['HEARD', '옆 세계에서 건너온 말 — 사람이 보내는 말이 아니다(세계끼리 쓴다)'],
	['NEARBY', '옆 세계가 보내는 그림자 — 사람이 보내는 말이 아니다(세계끼리 쓴다)'],
	['ROSTER', '물어보기 — 「내가 이 사람들을 그리고 있다」(TASK-WM-329). 두 번 물어도 세계는 안 바뀐다(답만 온다)'],
	['BEAT', '도장만 되돌리는 숨소리 (TASK-WM-339) — 세계는 이걸로 그 창이 얼마나 밀렸는지만 안다'],
]);

function fail(message) {
	console.error(`[action-once] CANNOT-RUN: ${message}`);
	process.exit(2);
}

let source;
try {
	source = readFileSync(hostFile, 'utf8');
} catch (error) {
	fail(`WorldHost.cs 를 못 읽었다 — ${error.message}`);
}

const handleAt = source.indexOf('private void HandleMessage');
if (handleAt < 0) fail('HandleMessage 를 못 찾았다 — 이 감사는 그 안만 본다');

// 사람이 보내는 말을 다루는 곳만 본다(그 뒤의 세계끼리 쓰는 자리는 뺀다).
const peerAt = source.indexOf('private void HandlePeer', handleAt);
const body = source.slice(handleAt, peerAt > 0 ? peerAt : source.length);

// ⚠ 블록은 <b>여는 줄</b>로 가른다 — 한 줄에 말이 둘이면(상자처럼) 같은 블록이다.
//   자리(index)만으로 자르면 「CHEST_PUT || CHEST_TAKE」의 앞엣것이 빈 토막을 갖게 되어
//   문을 지나는데도 빨강이 된다(첫 판이 그랬다).
const blocks = [];
const opener = /\n\t*if \((kind[^)]*)\)/g;
let opened;
while ((opened = opener.exec(body)) !== null) {
	blocks.push({ says: opened[1], at: opened.index });
}

if (blocks.length === 0) fail('다루는 말을 하나도 못 찾았다 — 이 감사가 낡은 것이다');

for (let i = 0; i < blocks.length; i += 1) {
	const until = i + 1 < blocks.length ? blocks[i + 1].at : body.length;
	blocks[i].text = body.slice(blocks[i].at, until);
	blocks[i].kinds = [...blocks[i].says.matchAll(/Protocol\.([A-Z_]+)/g)].map((one) => one[1]);
	blocks[i].guarded = blocks[i].text.includes('ShouldDo(');
}

const kinds = new Set(blocks.flatMap((one) => one.kinds));
const missing = [];
const unclassified = [];

for (const kind of kinds) {
	if (HARMLESS.has(kind)) continue;

	if (CHANGES_WORLD.has(kind) === false) {
		unclassified.push(kind);
		continue;
	}

	// 그 말이 나오는 블록 중 <b>하나라도</b> 문을 지나면 그 말은 지켜진다.
	if (blocks.some((one) => one.kinds.includes(kind) && one.guarded) === false) missing.push(kind);
}

console.log(`  ⓘ 사람이 보내는 말 ${kinds.size}가지 — 세계를 바꾸는 것 ${CHANGES_WORLD.size}가지 · 안 바꾸는 것 ${HARMLESS.size}가지`);

if (unclassified.length > 0) {
	console.log(`\n❌ 어느 쪽인지 안 정한 말: ${unclassified.join(', ')}`);
	console.log('   → 세계를 바꾸면 CHANGES_WORLD 에 넣고 ShouldDo 를 세워라.');
	console.log('   → 안 바꾸면 HARMLESS 에 <b>사유와 함께</b> 적어라 (사유 없이 넘기지 마라).');
}

if (missing.length > 0) {
	console.log(`\n❌ 「한 번만」 문을 안 지나는 말: ${missing.join(', ')}`);
	console.log('   → 끊기는 순간 그 말을 한 사람은 다시 보낸다. 문이 없으면 두 번 일어난다.');
}

if (unclassified.length === 0 && missing.length === 0) {
	console.log('[action-once] ✅ 세계를 바꾸는 말은 모두 「한 번만」 문을 지난다');
	process.exit(0);
}

process.exit(1);
