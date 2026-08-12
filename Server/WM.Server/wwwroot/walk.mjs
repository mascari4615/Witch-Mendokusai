// walk.mjs — <b>걷기의 순수한 셈</b> (TASK-WM-235).
//
// ★ 왜 따로 두나: 이제 걷는 자리가 둘이다 — 엔진이 오기 전의 지도(2D)와, 온 뒤의 세계(3D).
//   같은 규칙을 두 벌 적으면 반드시 갈라진다. 갈라진 걸음은 「나만 다른 데 서 있는」 세계가 된다.
//   여기엔 그리기도, 소켓도, 엔진도 없다 — 그래서 시험이 붙는다(wm-web-client-gate 가 본다).
//
// 세계와의 약속: 창이 보내는 것은 「어디로 가고 싶다」뿐이고, 얼마나 갈 수 있는지는 세계가 정한다
// (WorldSim.MAX_STEP 로 자르고, 시계로 속도를 심판한다 — MoveAllowance, TASK-WM-222).

/** 창이 내는 걸음 속도 (m/s) — 세계가 허락하는 값과 같다. */
export const WALK_SPEED = 3;

/** 한 번에 보내는 걸음의 최대 길이 (m) — 세계가 어차피 여기서 자른다. */
export const MAX_STEP = 1.5;

/** 걸음을 보내는 간격 (ms). */
export const SEND_EVERY_MS = 50;

/** 이 안쪽 차이는 안 보낸다 — 가만히 서 있는데 초당 20번 말할 이유가 없다. */
export const SMALLEST_STEP = 0.01;

/**
 * 손이 닿는 거리 (m) — 세계의 `WorldGatherables.REACH` 와 같은 값이다.
 * 창이 우겨도 세계가 다시 본다. 여기 두는 것은 <b>겨냥</b>을 위해서다(무엇을 집을까).
 */
export const REACH = 2.5;

/**
 * 지금 집을 수 있는 것 — 손이 닿는 것 중 <b>가장 가까운 하나</b> (TASK-WM-249).
 * 없으면 <c>null</c>. 순수 셈이라 지도(2D)와 세계(3D)가 같이 쓴다.
 */
export function whatIsInReach(me, things) {
	if (Array.isArray(things) === false)
		return null;

	let best = null;
	let bestAway = REACH * REACH;
	for (const one of things) {
		if (typeof one.x !== 'number' || typeof one.z !== 'number') continue;

		const away = ((one.x - me.x) ** 2) + ((one.z - me.z) ** 2);
		if (away > bestAway) continue;

		// 같은 거리면 번호 순 — 누를 때마다 다른 게 집히면 사람이 못 겨눈다.
		if (best !== null && away === bestAway && one.id >= best.id) continue;

		best = one;
		bestAway = away;
	}

	return best;
}

/**
 * 누르고 있는 방향으로 <b>내 화면의 나</b>를 옮긴다 (앞질러 그리기).
 *
 * ★ 왜 앞질러 그리나: 회선이 왕복 200ms 면, 눌러서 세계가 답할 때까지 내가 안 움직인다.
 *   그건 곧바로 「반응이 굼뜬 게임」이다. 그래서 내 화면의 나는 먼저 간다 —
 *   어긋나면 세계 쪽으로 슬쩍 당긴다(PositionCorrection 과 같은 생각).
 *
 * @param {{x:number, z:number}} target 지금 내가 있다고 <b>그리고 있는</b> 자리
 * @param {number} right  -1 왼쪽 · +1 오른쪽
 * @param {number} forward -1 뒤 · +1 앞
 * @param {number} dt 지난 시간 (초)
 * @param {{x:number, z:number}} [facing] 화면이 보는 앞쪽 (3D 는 카메라, 지도는 세계의 +Z)
 * @returns {{x:number, z:number}} 다음에 그릴 자리
 */
export function stepTowards(target, right, forward, dt, facing = { x: 0, z: 1 }) {
	const length = Math.hypot(right, forward);
	if (length === 0 || dt <= 0)
		return { x: target.x, z: target.z };

	// 대각선이 더 빠르면 안 된다 — 세계는 그걸 「빨리 걷기」로 본다.
	const nx = right / length;
	const nz = forward / length;

	const ahead = Math.hypot(facing.x, facing.z) === 0 ? { x: 0, z: 1 } : facing;
	const aheadLength = Math.hypot(ahead.x, ahead.z);
	const fx = ahead.x / aheadLength;
	const fz = ahead.z / aheadLength;

	// 화면의 오른쪽 = 앞쪽을 시계 방향으로 90도 돌린 것.
	const rx = fz;
	const rz = -fx;

	return {
		x: target.x + ((fx * nz) + (rx * nx)) * WALK_SPEED * dt,
		z: target.z + ((fz * nz) + (rz * nx)) * WALK_SPEED * dt,
	};
}

/**
 * 세계에 보낼 한 걸음 — 「내가 그리는 자리」와 「세계가 아는 자리」의 차이다.
 * 보낼 것이 없으면 <c>null</c>.
 */
export function stepToSend(target, world) {
	let x = target.x - world.x;
	let z = target.z - world.z;
	const length = Math.hypot(x, z);
	if (length <= SMALLEST_STEP)
		return null;

	if (length > MAX_STEP) {
		const scale = MAX_STEP / length;
		x *= scale;
		z *= scale;
	}

	return { x, z };
}
