#!/usr/bin/env node
// wm-web-walk-test.mjs — 걷기의 셈이 맞나 (TASK-WM-235).
//
// ★ 왜 따로 시험하나: 걷는 자리가 <b>둘</b>이 됐다 — 엔진이 오기 전의 지도(2D)와 온 뒤의 세계(3D).
//   같은 규칙을 두 벌 적으면 반드시 갈라지고, 갈라진 걸음은 「나만 다른 데 서 있는」 세계가 된다.
//   그래서 셈을 한 벌(walk.mjs)로 뽑고, 그 한 벌을 여기서 못박는다.
//   브라우저도 서버도 필요 없다 — 순수한 셈이라 눈 깜짝할 새에 돈다.
//
// exit: 0 = 맞다 · 1 = 틀렸다

import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';

const here = fileURLToPath(new URL('.', import.meta.url));
const repo = resolve(here, '..', '..');
const walk = await import(new URL('file:///' + join(repo, 'Server/WM.Server/wwwroot/walk.mjs').replace(/\\/g, '/')).href);
const { stepTowards, stepToSend, WALK_SPEED, MAX_STEP, SMALLEST_STEP } = walk;

let failures = 0;
let looked = 0;

function check(what, ok, detail) {
	looked += 1;
	if (ok === false) {
		failures += 1;
		console.log(`  ❌ ${what}${detail ? ` — ${detail}` : ''}`);
	}
}

const near = (a, b, slack = 0.001) => Math.abs(a - b) <= slack;
const from = { x: 0, z: 0 };

// ── 앞질러 그리기 ──────────────────────────────────────────────────────
{
	const ahead = stepTowards(from, 0, 1, 1);
	check('앞으로 1초면 걸음 속도만큼 간다', near(ahead.z, WALK_SPEED) && near(ahead.x, 0),
		`(${ahead.x}, ${ahead.z})`);

	const back = stepTowards(from, 0, -1, 1);
	check('뒤로도 같은 속도다', near(back.z, -WALK_SPEED), `${back.z}`);

	const side = stepTowards(from, 1, 0, 1);
	check('옆으로도 같은 속도다', near(Math.hypot(side.x, side.z), WALK_SPEED), `${Math.hypot(side.x, side.z)}`);

	const diagonal = stepTowards(from, 1, 1, 1);
	check('대각선이 더 빠르지 않다 (세계는 그걸 속임수로 본다)',
		near(Math.hypot(diagonal.x, diagonal.z), WALK_SPEED),
		`${Math.hypot(diagonal.x, diagonal.z).toFixed(3)}m/s`);

	const still = stepTowards(from, 0, 0, 1);
	check('안 누르면 안 움직인다', near(still.x, 0) && near(still.z, 0));

	const noTime = stepTowards({ x: 2, z: 3 }, 0, 1, 0);
	check('시간이 안 흘렀으면 안 움직인다', near(noTime.x, 2) && near(noTime.z, 3));

	const half = stepTowards(from, 0, 1, 0.5);
	check('반 초면 반만 간다', near(half.z, WALK_SPEED / 2), `${half.z}`);
}

// ── 화면이 보는 쪽이 앞이다 ────────────────────────────────────────────
{
	const facingRight = stepTowards(from, 0, 1, 1, { x: 1, z: 0 });
	check('앞쪽이 +X 면 앞으로 가기는 +X 다 (3D 는 눈이 돌아간다)',
		near(facingRight.x, WALK_SPEED) && near(facingRight.z, 0),
		`(${facingRight.x.toFixed(2)}, ${facingRight.z.toFixed(2)})`);

	const sideways = stepTowards(from, 1, 0, 1, { x: 1, z: 0 });
	check('그때 오른쪽은 앞쪽을 시계 방향으로 돌린 쪽이다',
		near(Math.hypot(sideways.x, sideways.z), WALK_SPEED) && near(sideways.x, 0),
		`(${sideways.x.toFixed(2)}, ${sideways.z.toFixed(2)})`);

	const broken = stepTowards(from, 0, 1, 1, { x: 0, z: 0 });
	check('앞쪽을 모르면 세계의 +Z 를 앞으로 본다 (0 으로 나누지 않는다)',
		near(broken.z, WALK_SPEED), `${broken.z}`);
}

// ── 세계에 보낼 걸음 ───────────────────────────────────────────────────
{
	check('가만히 서 있으면 아무 말도 안 한다', stepToSend(from, from) === null);
	check('아주 조금은 말할 거리가 아니다',
		stepToSend({ x: SMALLEST_STEP / 2, z: 0 }, from) === null);

	const small = stepToSend({ x: 0.15, z: 0 }, from);
	check('보통 걸음은 그대로 간다', small !== null && near(small.x, 0.15), JSON.stringify(small));

	const long = stepToSend({ x: 100, z: 0 }, from);
	check('한 번에 갈 수 있는 만큼으로 잘린다',
		long !== null && near(Math.hypot(long.x, long.z), MAX_STEP), JSON.stringify(long));

	const slanted = stepToSend({ x: 30, z: 40 }, from);
	check('잘려도 방향은 안 틀어진다',
		slanted !== null && near(slanted.x / slanted.z, 30 / 40),
		JSON.stringify(slanted));

	const behind = stepToSend({ x: 0, z: 0 }, { x: 5, z: 0 });
	check('세계가 나를 앞에 두고 있으면 뒤로 향한다', behind !== null && behind.x < 0,
		JSON.stringify(behind));
}

// ── 무엇을 집을까 (TASK-WM-249) ────────────────────────────────────────
{
	const { whatIsInReach, REACH } = walk;
	const me = { x: 0, z: 0 };

	check('손이 안 닿으면 아무것도 안 집는다',
		whatIsInReach(me, [{ id: 1, x: REACH + 0.1, z: 0 }]) === null);
	check('닿는 것 중 가장 가까운 것을 집는다',
		whatIsInReach(me, [{ id: 1, x: 2, z: 0 }, { id: 2, x: 0.5, z: 0 }]).id === 2);
	check('같은 거리면 늘 같은 것을 집는다 (누를 때마다 바뀌면 못 겨눈다)',
		whatIsInReach(me, [{ id: 7, x: 1, z: 0 }, { id: 3, x: -1, z: 0 }]).id === 3);
	check('자리가 없는 것은 건너뛴다 (이름표에는 자리가 없다)',
		whatIsInReach(me, [{ id: 1, name: '나무' }, { id: 2, x: 0.5, z: 0 }]).id === 2);
	check('아무것도 없으면 null', whatIsInReach(me, []) === null);
	check('목록이 아니면 null', whatIsInReach(me, undefined) === null);
}

console.log(`[web-walk] 걷기의 셈 ${looked}가지 확인`);

if (failures === 0) {
	console.log('[web-walk] ✅ 지도와 세계가 같은 걸음을 쓴다');
	process.exit(0);
}

console.log(`\n[web-walk] RESULT: ${failures}건`);
process.exit(1);
