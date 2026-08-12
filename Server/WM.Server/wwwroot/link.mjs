// 끊긴 줄을 <b>스스로 다시 잇는 규칙</b> — 웹 창 몫 (TASK-WM-217 § 재접속).
//
// ★ 왜 떼어냈나: 게임 창에는 `ReconnectBackoff` 가 있어 서버가 잠깐 죽어도 알아서 다시 붙는다.
//   웹 창에는 그게 없어서, 배포로 서버가 1초 내려가면 화면이 「끊겼다」로 굳고 사람이 직접
//   새로고침해야 했다. 다시 붙는 시점은 눈으로 못 보는 규칙이라 시험할 수 있는 자리에 둔다.
//
// ★ 왜 「무조건 다시 붙기」가 아닌가: 세계는 <b>나중에 온 창이 이긴다</b>(중복 로그인 금지).
//   쫓겨난 창이 다시 붙으면 방금 들어간 창을 도로 쫓아낸다 — 두 창이 서로를 영원히 밀어낸다.
//   그래서 「쫓겨남」은 다시 붙지 않는 유일한 끝이다(게임 창도 같은 규칙).
//
// 규칙 하나: 여기 있는 것은 소켓도 화면도 모른다. 언제 · 붙을까 말까 만 정한다.

/** 첫 헛걸음은 0.5초 뒤 — 게임 창(ReconnectBackoff)과 같은 숫자다. */
export const FIRST_DELAY_MS = 500;

/** 아무리 늘어도 10초 — 사람이 멈춘 화면을 그보다 오래 보게 두지 않는다. */
export const MAX_DELAY_MS = 10000;

/**
 * 다시 붙기 계획 — 끊길 때마다 물어보면 「언제 · 붙을까」를 답한다.
 *
 * 쓰는 쪽:
 *   const plan = createReconnectPlan();
 *   socket.onopen  = () => plan.opened();
 *   socket.onclose = () => { const next = plan.closed(); if (next.retry) setTimeout(connect, next.delayMs); };
 *   // 'kicked' 를 받으면 plan.kicked() — 그 뒤의 closed() 는 retry:false 다.
 */
export function createReconnectPlan() {
	let delay = FIRST_DELAY_MS;
	let attempts = 0;
	let evicted = false;

	return {
		/** 몇 번째 헛걸음인가 — 화면에 「다시 붙는 중…」을 적을 때 쓴다. */
		get attempts() { return attempts; },

		/** 쫓겨났나 — 그렇다면 이 창은 다시 붙지 않는다. */
		get evicted() { return evicted; },

		/** 붙었다 — 다음에 끊기면 다시 빠르게 시도한다. */
		opened() {
			delay = FIRST_DELAY_MS;
			attempts = 0;
		},

		/** 세계가 「다른 곳에서 접속했다」고 했다 — 이 창은 여기서 끝이다. */
		kicked() {
			evicted = true;
		},

		/**
		 * 끊겼다 — 다시 붙을까, 붙는다면 얼마 뒤에.
		 * @returns {{retry: boolean, delayMs: number, attempts: number}}
		 */
		closed() {
			if (evicted) return { retry: false, delayMs: 0, attempts };

			const wait = delay;
			attempts += 1;
			delay = Math.min(delay * 2, MAX_DELAY_MS);

			return { retry: true, delayMs: wait, attempts };
		},
	};
}

/**
 * 지금 줄이 어떤 상태인지 사람 말로 — 「끊겼다」로만 두면 사람은 고장으로 읽는다.
 *
 * @param {'connecting'|'open'|'retrying'|'evicted'|'error'} phase
 * @param {number} attempts 몇 번째 헛걸음인가 (retrying 일 때만 쓴다)
 */
export function linkStatusText(phase, attempts) {
	if (phase === 'open') return '붙었다';
	if (phase === 'connecting') return '붙는 중…';
	if (phase === 'evicted') return '다른 곳에서 접속했다 — 여기서는 나간다';
	if (phase === 'error') return '연결에 문제가 있다';

	// 다시 붙는 중 — 몇 번째인지 보여 준다(서버가 오래 죽어 있으면 사람이 알아야 한다).
	const count = Number(attempts) > 0 ? Number(attempts) : 1;
	return `끊겼다 — 다시 붙는 중… (${count}번째)`;
}
