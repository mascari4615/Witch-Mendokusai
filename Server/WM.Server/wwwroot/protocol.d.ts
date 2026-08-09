// 자동 생성물 — 손으로 고치지 마라 (TASK-WM-216).
// 정본 = WitchMendokusai/Server/WM.Server/Protocol.cs
// 서버가 계약을 소유하고, 이 파일은 거기서 뽑혀 나온다.

/** 창 -> 서버: 나 왔다(열쇠가 있으면 같이). 첫 말이다. */
export interface Hello {
	type: 'hello';
	secret: string;
}

/** 서버 -> 창: 접속했다. secret 이 비어있지 않으면 새로 받은 열쇠(적어 둘 것). */
export interface Welcome {
	type: 'welcome';
	id: number;
	identityId: number;
	secret: string;
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

/** 창 -> 서버: 이 칸의 건물을 부수고 싶다. */
export interface RemoveRequest {
	type: 'remove';
	x: number;
	y: number;
	z: number;
}

/** 창 -> 서버: 솥을 한 번 젓는다(모두가 같은 솥). */
export interface BrewRequest {
	type: 'brew';
	dx: number;
	dy: number;
	grind: number;
}

/** 창 -> 서버: 솥을 비운다. */
export interface BrewResetRequest {
	type: 'brewreset';
}

/** 창 -> 서버: 이 솥을 완성으로 가져가겠다(선착순 한 번). */
export interface BrewCompleteRequest {
	type: 'brewcomplete';
}

/** 서버 -> 그 창에게만: 완성은 네 것이다. */
export interface BrewTaken {
	type: 'brewtaken';
	x: number;
	y: number;
	steps: number;
	side: number;
}

/** 창 -> 서버: 내 가방 좀 알려줘. */
export interface BagAsk {
	type: 'bagask';
}

/** 창 -> 서버: 이걸 썼다(제작 재료 등). 안 알리면 쓴 게 다시 생긴다. */
export interface ConsumeRequest {
	type: 'consume';
	itemId: number;
	amount: number;
}

/** 서버 -> 그 창에게만: 네 가방은 이렇다. */
export interface Bag {
	type: 'bag';
	items: { itemId: number; amount: number }[];
}

export type ServerMessage = Welcome | WorldSnapshot | BrewTaken | Bag;
export type ClientMessage = MoveRequest | RemoveRequest | BrewRequest | BrewResetRequest | BrewCompleteRequest | Hello | BagAsk | ConsumeRequest;
