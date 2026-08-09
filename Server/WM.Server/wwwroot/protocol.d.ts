// 자동 생성물 — 손으로 고치지 마라 (TASK-WM-216).
// 정본 = WitchMendokusai/Server/WM.Server/Protocol.cs
// 서버가 계약을 소유하고, 이 파일은 거기서 뽑혀 나온다.

/** 서버 -> 창: 접속했다. 네 인형 번호는 이것이다. */
export interface Welcome {
	type: 'welcome';
	id: number;
}

/** 세계에 있는 인형 하나. */
export interface WorldDollView {
	id: number;
	x: number;
	z: number;
}

/** 세계의 시각 — 서버가 굴린다(사람이 없어도 흐른다). */
export interface WorldTime {
	year: number;
	season: number;
	day: number;
	hour: number;
	minute: number;
}

/** 서버 -> 창: 지금 세계는 이렇게 생겼다. */
export interface WorldSnapshot {
	type: 'world';
	dolls: WorldDollView[];
	time?: WorldTime;
}

/** 창 -> 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다). */
export interface MoveRequest {
	type: 'move';
	x: number;
	z: number;
}

export type ServerMessage = Welcome | WorldSnapshot;
export type ClientMessage = MoveRequest;
