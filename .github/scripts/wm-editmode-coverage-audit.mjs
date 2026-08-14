#!/usr/bin/env node
// wm-editmode-coverage-audit.mjs — <b>EditMode 시험 중 몇 개가 실제로 도나</b> (TASK-WM-364).
//
// ★ 왜: EditMode 에는 시험이 1500개 넘게 있는데 CI 에서는 <b>한 개도 안 돈다</b>
//   (유니티 라이선스 미해결, WM-221). 그중 상당수는 유니티를 아예 안 쓰고 DomainSDK 만 본다 —
//   즉 <b>도는 자리로 데려올 수 있는데 안 데려온</b> 시험들이다.
//   실제로 그 틈으로 결함이 하나 나왔다(WM-363: 이름 지은 사람이 장부 청소에 지워졌다).
//
// 이 자는 그 빚을 <b>숫자로</b> 세고, 그 숫자가 <b>늘면</b> 막는다(래칫).
//   · 줄이는 것 = 자유(기준선을 낮춰 적으면 된다)
//   · 늘리는 것 = 빨강 — 유니티 없이 도는 새 시험을 만들었으면 도는 자리에도 걸어라
//
// 실행: node .github/scripts/wm-editmode-coverage-audit.mjs [--strict]
// exit: 0 = 빚이 안 늘었다 · 1 = 늘었다(--strict) · 2 = 못 돌림

import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const editMode = join(repo, 'Assets', '_WitchMendokusai', 'Tests', 'EditMode');
const project = join(repo, 'Server', 'WM.Server.Tests', 'WM.Server.Tests.csproj');
const baselinePath = join(here, 'wm-editmode-coverage-baseline.tsv');

function cannotRun(message) {
	console.error(`[EditMode] CANNOT-RUN: ${message}`);
	process.exit(2);
}

if (existsSync(editMode) === false) cannotRun(`시험 폴더를 못 찾았다 — ${editMode}`);
if (existsSync(project) === false) cannotRun(`시험 묶음 파일을 못 찾았다 — ${project}`);

function everyFileUnder(folder) {
	const found = [];
	for (const name of readdirSync(folder)) {
		const path = join(folder, name);
		if (statSync(path).isDirectory()) found.push(...everyFileUnder(path));
		else if (name.endsWith('.cs')) found.push(path);
	}

	return found;
}

const files = everyFileUnder(editMode);
if (files.length === 0) cannotRun('시험 파일이 하나도 없다 — 이 자가 헛것을 보고 있다');

const projectText = readFileSync(project, 'utf8');

/** csproj 가 그 파일을 걸었나 — 폴더째 건 것(<b>**</b>)과 한 파일씩 건 것 둘 다 본다. */
function linkedIn(path) {
	const under = path.slice(editMode.length + 1).replace(/\\/g, '/');
	if (projectText.includes(under)) return true;

	const folder = under.split('/').slice(0, -1).join('/');
	return folder.length > 0 && projectText.includes(`Tests/EditMode/${folder}/**`);
}

let unityFree = 0;
let unityFreeTests = 0;
let linkedTests = 0;
let sleepingTests = 0;
const sleepingByFolder = new Map();

for (const path of files) {
	const text = readFileSync(path, 'utf8');
	const tests = (text.match(/\[Test\]/g) || []).length;
	if (tests === 0) continue;

	if (text.includes('UnityEngine') || text.includes('UnityEditor')) continue;

	unityFree += 1;
	unityFreeTests += tests;

	if (linkedIn(path)) {
		linkedTests += tests;
		continue;
	}

	sleepingTests += tests;
	const folder = path.slice(editMode.length + 1).replace(/\\/g, '/').split('/').slice(0, -1).join('/') || '(뿌리)';
	sleepingByFolder.set(folder, (sleepingByFolder.get(folder) || 0) + tests);
}

console.log(`[EditMode] 유니티 없는 시험 파일 ${unityFree}개 · 그 안의 시험 ${unityFreeTests}개`);
console.log(`[EditMode] 도는 자리에 걸린 시험 ${linkedTests}개 · 아직 자는 시험 ${sleepingTests}개`);

for (const [folder, count] of [...sleepingByFolder.entries()].sort((left, right) => right[1] - left[1]).slice(0, 8))
	console.log(`  · ${folder}: ${count}개`);

let baseline = Number.POSITIVE_INFINITY;
if (existsSync(baselinePath)) {
	const line = readFileSync(baselinePath, 'utf8').split('\n').find((one) => /^\d+/.test(one.trim()));
	if (line) baseline = Number(line.trim().split(/\s+/)[0]);
}

if (Number.isFinite(baseline) === false) {
	console.log('[EditMode] 기준선이 없다 — 지금 수를 적어 두면 다음부터 늘어나는 것을 막는다.');
	process.exit(0);
}

console.log(`[EditMode] 기준선 ${baseline}개 · 지금 ${sleepingTests}개`);

if (sleepingTests > baseline) {
	console.log('\n[EditMode] 자는 시험이 <b>늘었다</b> — 유니티 없이 도는 시험을 새로 만들었으면');
	console.log('           WM.Server.Tests.csproj 에도 걸어라(복사 X, 링크). 정말 못 걸면 기준선을 올리고 사유를 적어라.');
	if (process.argv.includes('--strict')) process.exit(1);
}

if (sleepingTests < baseline)
	console.log('[EditMode] 빚이 줄었다 — 기준선을 이 수로 낮춰 적어라(래칫은 되돌아가지 않는다).');

process.exit(0);
