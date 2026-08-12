// bad-line.mjs — <b>나쁜 회선</b>을 세계 앞에 세운다 (TASK-WM-224).
//
// ★ 왜: 지금까지 WM 이 잰 모든 수치(20.7Hz · 0.17m 뒤처짐 · 재접속 · 몰림)는 전부
//   <b>loopback</b> 에서 나왔다 — 지연 0, 흔들림 0, 대역폭 무한인 세계다. 그런 회선은
//   지구에 없다. 「버틴다」는 주장이 사실은 「이 기계 안에서는 버틴다」였다.
//
// ★ 왜 TCP 층인가 (메시지를 골라 버리지 않는 이유): WebSocket 은 TCP 위에 있다.
//   TCP 는 잃은 조각을 <b>다시 보낸다</b> — 그래서 앱에는 「메시지가 사라지는」 일이 안 생긴다.
//   생기는 것은 <b>늦어짐</b>(재전송 대기)과, 끝내 안 되면 <b>끊김</b>이다.
//   메시지를 임의로 버리는 흉내는 실제로 안 일어나는 일을 시험하는 것이라 거짓 안심을 준다.
//   그래서 이 회선은 바이트를 <b>늦추고·좁히고·끊을</b> 뿐, 몰래 버리지 않는다.
//
// ★ 순서는 절대 안 뒤집는다: 한 TCP 흐름 안에서 나중 바이트가 앞 바이트를 앞지르는 일은 없다.
//   흔들림(jitter)을 넣되 내보내는 시각은 <b>단조 증가</b>여야 한다 — 안 그러면 세계에 없는
//   고장을 시험하게 된다.

import net from 'node:net';

/**
 * 이 조각을 언제 내보낼까 — 순수 셈(시험 대상).
 *
 * @param {number} readyAt  이 방향에서 <b>직전 조각</b>이 나간 시각 (ms)
 * @param {number} now      지금 (ms)
 * @param {number} bytes    이 조각의 크기
 * @param {{latencyMs:number, jitterMs:number, bytesPerSecond:number}} line
 * @param {number} roll     0..1 흔들림 제비 (시험에서 고정값을 넣는다)
 * @returns {number} 내보낼 시각 (ms) — 언제나 readyAt 이상 (순서 보존)
 */
export function releaseAt(readyAt, now, bytes, line, roll) {
	const jitter = line.jitterMs > 0 ? roll * line.jitterMs : 0;
	const arrival = now + line.latencyMs + jitter;

	// 좁은 회선 = 이 조각을 다 흘려보내는 데 걸리는 시간만큼 뒤 조각이 밀린다.
	const spool = line.bytesPerSecond > 0 ? (bytes / line.bytesPerSecond) * 1000 : 0;

	// 앞 조각보다 먼저 나갈 수는 없다 — TCP 는 순서를 안 바꾼다.
	const earliest = readyAt > arrival ? readyAt : arrival;
	return earliest + spool;
}

/**
 * 나쁜 회선을 하나 세운다. 창은 <c>listenPort</c> 로 붙고, 세계는 <c>targetPort</c> 에 있다.
 * HTTP·WebSocket 을 안 가린다 — 바이트만 다루기 때문이다(업그레이드도 그냥 흘러간다).
 */
export function openBadLine({ listenPort, targetPort, latencyMs = 0, jitterMs = 0, bytesPerSecond = 0, host = '127.0.0.1' }) {
	const line = { latencyMs, jitterMs, bytesPerSecond };
	const sockets = new Set();

	const pipeThrough = (from, to) => {
		// ⚠ 조각마다 setTimeout 을 따로 걸면 <b>순서가 뒤집힌다</b> (실측 2026-08-12):
		//   앞 조각의 타이머가 아직 안 돌았는데 뒤 조각의 「나갈 시각」이 이미 지났으면
		//   뒤 조각이 곧바로 쓰여 앞지른다 — HTTP 응답이 깨져 창이 아예 안 떴다.
		//   그래서 줄(FIFO) 하나에 담고 <b>맨 앞부터만</b> 내보낸다.
		const queue = [];
		let readyAt = 0;
		let timer = null;
		let ended = false;

		const drain = () => {
			timer = null;
			const now = Date.now();

			while (queue.length > 0 && queue[0].at <= now) {
				const piece = queue.shift();
				if (to.destroyed === false) to.write(piece.chunk);
			}

			if (queue.length > 0) {
				timer = setTimeout(drain, Math.max(1, queue[0].at - now));
				return;
			}

			if (ended && to.destroyed === false) to.end();
		};

		from.on('data', (chunk) => {
			const now = Date.now();
			readyAt = releaseAt(readyAt, now, chunk.length, line, Math.random());
			queue.push({ at: readyAt, chunk });
			if (timer === null) timer = setTimeout(drain, Math.max(1, readyAt - now));
		});

		// 밀린 조각이 다 나간 뒤에 닫는다 — 안 그러면 회선이 마지막 말을 먹는다.
		from.on('end', () => {
			ended = true;
			if (timer === null) drain();
		});

		from.on('error', () => { if (to.destroyed === false) to.destroy(); });
	};

	const server = net.createServer((incoming) => {
		const outgoing = net.connect(targetPort, host);
		sockets.add(incoming);
		sockets.add(outgoing);
		incoming.on('close', () => sockets.delete(incoming));
		outgoing.on('close', () => sockets.delete(outgoing));
		outgoing.on('error', () => incoming.destroy());
		incoming.on('error', () => outgoing.destroy());

		pipeThrough(incoming, outgoing);
		pipeThrough(outgoing, incoming);
	});

	return {
		listen: () => new Promise((done) => server.listen(listenPort, host, done)),
		/** 회선을 <b>끊는다</b> — 잃은 조각이 끝내 안 닿았을 때 진짜로 일어나는 일. */
		cut: () => {
			for (const socket of sockets) {
				try { socket.destroy(); } catch { /* 이미 죽었다 */ }
			}
			sockets.clear();
		},
		close: () => new Promise((done) => {
			for (const socket of sockets) {
				try { socket.destroy(); } catch { /* 이미 죽었다 */ }
			}
			sockets.clear();
			server.close(done);
		}),
	};
}
