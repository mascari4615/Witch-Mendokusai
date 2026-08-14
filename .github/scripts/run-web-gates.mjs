#!/usr/bin/env node
// run-web-gates.mjs — <b>진짜 창 관문을 갈래로 나눠 돌린다</b> (TASK-WM-369).
//
// ★ 왜: 브라우저 관문이 서른 개를 넘었고 한 러너에서 <b>차례로</b> 돈다 —
//   한 판이 8~14분이다(실측 2026-08-14: 503·648·830초). 되먹임이 느리면 고치는 속도가 느려진다.
//   목록을 갈래로 나눠 여러 러너가 <b>같이</b> 돌면 그 시간은 갈래 수만큼 줄어든다.
//
// ★ 왜 목록 파일인가: 워크플로에 관문마다 한 단(step)을 적으면 새 관문마다 워크플로를 고쳐야 하고,
//   그 손질을 잊으면 <b>만들어 놓고 안 도는 관문</b>이 된다(그 사고를 잡는 자가 WM-366 이다).
//   목록에 한 줄 더하는 것으로 끝나게 한다.
//
// 실행: node .github/scripts/run-web-gates.mjs --shard 1 --of 3 [--list]
// exit: 0 = 그 갈래의 관문이 다 통과(또는 CANNOT-RUN) · 1 = 하나라도 빨강 · 2 = 못 돌림

import { readFileSync, existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const listPath = join(here, 'wm-web-gates.tsv');

function cannotRun(message) {
	console.error(`[갈래] CANNOT-RUN: ${message}`);
	process.exit(2);
}

if (existsSync(listPath) === false) cannotRun(`관문 목록을 못 찾았다 — ${listPath}`);

const asked = (name, fallback) => {
	const at = process.argv.indexOf(name);
	return at >= 0 && process.argv[at + 1] ? Number(process.argv[at + 1]) : fallback;
};

const shard = asked('--shard', 1);
const of = asked('--of', 1);
if (Number.isFinite(shard) === false || Number.isFinite(of) === false || shard < 1 || of < 1 || shard > of)
	cannotRun(`갈래가 이상하다 — --shard ${shard} --of ${of}`);

const gates = readFileSync(listPath, 'utf8').split('\n')
	.map((line) => line.trim())
	.filter((line) => line.length > 0 && line.startsWith('#') === false)
	.map((line) => {
		const [file, env = ''] = line.split('\t');
		return { file: file.trim(), env: env.trim() };
	});

if (gates.length === 0) cannotRun('목록에 관문이 하나도 없다 — 이 자가 헛것을 보고 있다');

// 갈래 나누기는 <b>돌아가며</b>(1,4,7… / 2,5,8…) — 무거운 관문이 한 갈래에 몰리지 않게.
const mine = gates.filter((one, at) => at % of === (shard - 1));

console.log(`[갈래] ${shard}/${of} — 관문 ${mine.length}개 (전체 ${gates.length}개)`);
for (const one of mine) console.log(`  · ${one.file}${one.env ? ` (${one.env})` : ''}`);

if (process.argv.includes('--list')) process.exit(0);

let bad = 0;
for (const one of mine) {
	const extra = {};
	for (const pair of one.env.split(',')) {
		if (pair.includes('=') === false) continue;
		const [key, value] = pair.split('=');
		extra[key.trim()] = value.trim();
	}

	console.log(`\n[갈래] ── ${one.file} ──`);
	const ran = spawnSync(process.execPath,
		[join(here, 'run-gate.mjs'), join(here, one.file)],
		{ stdio: 'inherit', env: { ...process.env, ...extra } });

	if (ran.status !== 0) {
		bad += 1;
		console.log(`[갈래] ❌ ${one.file} (exit ${ran.status})`);
	}
}

if (bad > 0) {
	console.log(`\n[갈래] ${shard}/${of} RESULT: 빨간 관문 ${bad}개`);
	process.exit(1);
}

console.log(`\n[갈래] ✅ ${shard}/${of} — ${mine.length}개 다 통과`);
process.exit(0);
