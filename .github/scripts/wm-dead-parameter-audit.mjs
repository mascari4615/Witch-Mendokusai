#!/usr/bin/env node
// wm-dead-parameter-audit.mjs — <b>받아 놓고 안 쓰는 인자</b>를 잡는다 (TASK-WM-231).
//
// ★ 왜: 이걸로 진짜 버그가 나갔다(TASK-WM-230). 세계는 들판을 「바뀐 자리만」 보내면서
//   그렇다는 표시(fieldChanged·fieldGone)를 <b>인자로 받아 놓고 쓰지 않았다</b>.
//   컴파일도 되고, 시험도 초록이고, 화면도 멀쩡했다 — 다만 창이 부분 목록을 전체로 알고
//   통째로 갈아 끼워 들판 67자리가 한 번에 사라졌다. 오류 한 줄 안 났다.
//
// ★ 왜 컴파일러가 안 잡나: C# 은 안 쓰는 <b>인자</b>를 오류로 안 본다(지역 변수와 다르다).
//   분석기 규칙(IDE0060)은 이 저장소에서 꺼져 있다 — 서식 규칙이 WM 스타일과 부딪히기 때문이다.
//   그래서 필요한 것만 따로 본다.
//
// 보는 곳: 서버의 <b>말을 짓는 층</b>(Protocol.cs) — 여기서 인자를 흘리면 그대로 계약이 반쪽이 된다.
// 일부러 안 쓰는 인자는 그 줄에 `// 안 쓰는 게 맞다` 를 달아 둔다.
//
// exit: 0 = 다 쓰인다 · 1 = 흘린 인자가 있다

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const watched = ['Server/WM.Server/Protocol.cs'];
const ALLOW = '안 쓰는 게 맞다';

/** 여는 중괄호부터 짝이 맞는 닫는 중괄호까지 — 글 안의 중괄호는 안 센다. */
function bodyFrom(text, openAt) {
	let depth = 0;
	let inText = false;
	let inChar = false;
	for (let at = openAt; at < text.length; at += 1) {
		const now = text[at];
		const before = text[at - 1];
		if (inText) {
			if (now === '"' && before !== '\\') inText = false;
			continue;
		}

		if (inChar) {
			if (now === "'" && before !== '\\') inChar = false;
			continue;
		}

		if (now === '"') { inText = true; continue; }
		if (now === "'") { inChar = true; continue; }
		if (now === '{') depth += 1;
		if (now === '}') {
			depth -= 1;
			if (depth === 0) return text.slice(openAt + 1, at);
		}
	}

	return '';
}

/** `int foo = 3` · `IEnumerable<int> gone = null` 에서 이름만. */
function nameOf(one) {
	const clean = one.split('=')[0].trim();
	const bits = clean.split(/\s+/);
	return bits.length >= 2 ? bits[bits.length - 1] : null;
}

let dropped = 0;
let looked = 0;

for (const relative of watched) {
	const path = join(repo, relative);
	const text = readFileSync(path, 'utf8');
	const heads = [...text.matchAll(/^\t*(?:public|private|internal|protected)[^\n=;]*?\s(\w+)\(([^)]*)\)\s*$/gm)];

	for (const head of heads) {
		const [whole, method, args] = head;
		if (args.trim() === '') continue;

		const openAt = text.indexOf('{', head.index + whole.length);
		if (openAt < 0) continue;

		const body = bodyFrom(text, openAt);
		if (body === '') continue;

		looked += 1;
		for (const one of args.split(/,(?![^<]*>)/)) {
			const name = nameOf(one);
			if (name === null) continue;

			const used = new RegExp(`\\b${name}\\b`).test(body);
			if (used) continue;
			if (whole.includes(ALLOW)) continue;

			dropped += 1;
			const line = text.slice(0, head.index).split('\n').length;
			console.log(`  ❌ ${relative}:${line} ${method}( … ${name} … ) — 받아 놓고 안 쓴다`);
		}
	}
}

console.log(`[dead-param] 말을 짓는 함수 ${looked}개를 봤다`);

if (dropped === 0) {
	console.log('[dead-param] ✅ 받아 놓고 흘리는 인자가 없다');
	process.exit(0);
}

console.log(`\n[dead-param] RESULT: ${dropped}건 — 받은 것을 안 쓰면 그 말은 안 나간 것이다.`);
console.log('  일부러 안 쓰는 인자면 그 함수 머리 줄에 `// 안 쓰는 게 맞다` 를 달아 둔다.');
process.exit(1);
