// 자동 생성물 — 손으로 고치지 마라 (TASK-WM-216).
// 정본 = WitchMendokusai/Server/WM.Server/Protocol.cs
// 서버가 계약을 소유하고, 이 파일은 거기서 뽑혀 나온다.

/** 창 -> 서버: 나 왔다(열쇠가 있으면 같이). 첫 말이다. */
export interface Hello {
	type: 'hello';
	secret: string;
	klCode?: string;
	klSession?: string;
	/** 이미 들고 있는 낱말표·제작표의 도장 — 같으면 세계가 그것들을 다시 안 보낸다. */
	knownCatalogs?: string;
}

/** 서버 -> 창: 접속했다. secret 이 비어있지 않으면 새로 받은 열쇠(적어 둘 것). */
export interface Welcome {
	type: 'welcome';
	id: number;
	identityId: number;
	secret: string;
	/** 이 서버 판의 낱말표·제작표 도장 — hello 에 되돌려 주면 그것들을 안 보낸다. */
	catalogStamp: string;
	/** 창(웹 화면)의 판 도장 — 달라졌으면 새 판이 나간 것이다 (TASK-WM-367). */
	windowStamp: string;
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
	hoursPerDay: number;
}

/** 서버 -> 창: 지금 세계는 이렇게 생겼다. */
export interface WorldBuildingView {
	x: number;
	y: number;
	z: number;
	w: number;
	l: number;
	buildingId: number;
}

export interface BrewStepView {
	dx: number;
	dy: number;
	grind: number;
}

export interface BrewView {
	x: number;
	y: number;
	steps: number;
	side: number;
	path: BrewStepView[];
}

export interface GatherableView {
	id: number;
	x: number;
	z: number;
	itemId: number;
	amount: number;
}

/** buildings·gatherables 는 바뀐 프레임에만 실린다 — 없으면 지난 것을 그대로 쓸 것. */
/** 지은 자리마다의 솥 — 여럿이 각자 젓는다. */
export interface CauldronView {
	x: number;
	y: number;
	z: number;
	px: number;
	py: number;
	steps: number;
	side: number;
}

export interface WorldSnapshot {
	type: 'world';
	sequence: number;
	/** 세계의 시계 도장 — 창은 자기 말에 ack 로 얹는다 (TASK-WM-303). */
	at: number;
	changed?: boolean;
	gone?: number[];
	dolls: WorldDollView[];
	buildings?: WorldBuildingView[];
	fieldChanged?: boolean;
	fieldGone?: number[];
	gatherables?: GatherableView[];
	cauldrons?: CauldronView[];
	time?: WorldTime;
	brew?: BrewView;
}

/** 서버 -> 창: 여기부터는 저 세계다. 그 주소로 옮겨 붙고 pass 를 hello 에 낸다. */
export interface MoveOn {
	type: 'moveon';
	zone: string;
	address: string;
	x: number;
	z: number;
	pass: string;
}

/** 창 -> 서버: 저 사람을 때린다. 거리·간격·대상은 세계가 본다. */
export interface Did {
	type: 'did';
	did: number;
}

export interface StrikeRequest {
	type: 'strike';
	targetId: number;
	ack?: number;
}

/** 서버 -> 창: 누가 맞았다. down 이면 그 자리에서 다시 세워졌다. */
export interface Hurt {
	type: 'hurt';
	dollId: number;
	by: number;
	health: number;
	down: boolean;
}

/** 창 -> 서버: 이렇게 말했다. 빈 줄·너무 긴 줄은 세계가 다듬거나 버린다. */
export interface SayRequest {
	type: 'say';
	text: string;
	ack?: number;
	did?: number;
}

/** 서버 -> 창: 누가 이렇게 말했다 — 그 사람이 보이는 사람에게만 온다. */
export interface Said {
	type: 'said';
	dollId: number;
	name: string;
	text: string;
}

/** 창 -> 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다). */
export interface MoveRequest {
	type: 'move';
	x: number;
	z: number;
	seq?: number;
	/** 마지막으로 본 세계 도장 (TASK-WM-303). */
	ack?: number;
}

/** 창 -> 서버: 이 칸의 건물을 부수고 싶다. */
export interface RemoveRequest {
	type: 'remove';
	x: number;
	y: number;
	z: number;
}

/** 창 -> 서버: 저기 있는 저것을 줍겠다. 손이 닿는지는 세계가 본다. */
export interface GatherRequest {
	type: 'gather';
	nodeId: number;
}

/** 창 -> 서버: 이 재료를 솥에 넣는다(가방에서 실제로 빠진다). 어디로 밀지는 세계가 안다. */
/** x·y·z 를 주면 그 자리의 솥, 안 주면 세계에 하나뿐인 옛 솥(회귀 0). 손이 닿아야 한다. */
export interface BrewRequest {
	type: 'brew';
	itemId: number;
	x?: number;
	y?: number;
	z?: number;
}

/** 서버 -> 창: 마도서 — 무엇을 만들 수 있고 어디를 겨냥하나(들어올 때 한 번). */
export interface Spellbook {
	type: 'spellbook';
	pages: { id: number; name: string; x: number; y: number; radius: number; itemId: number; amount: number }[];
}

/** 서버 -> 창: 솥에 넣을 수 있는 재료 목록(들어올 때 한 번). */
export interface BrewShelf {
	type: 'brewshelf';
	items: { itemId: number; name: string }[];
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
	itemId: number;
	amount: number;
	grade: number;
	recipe: string;
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
/** 창 -> 서버: 나를 이렇게 불러 달라. 짧거나 길거나 남과 겹치면 세계가 거절한다. */
export interface RenameRequest {
	type: 'rename';
	name: string;
}

/** 창 -> 서버: 이 줄대로 만들겠다. 재료도 주사위도 세계가 본다. */
export interface CraftRequest {
	type: 'craft';
	recipeId: number;
}

/** 세계가 아는 제작 한 줄 — 재료는 itemIds·amounts 짝. */
export interface CraftBookEntry {
	recipeId: number;
	name: string;
	resultItemId: number;
	resultAmount: number;
	percentage: number;
	itemIds: number[];
	amounts: number[];
}

/** 서버 -> 창: 세계가 아는 제작표(들어올 때 한 번). */
export interface CraftBook {
	type: 'craftbook';
	recipes: CraftBookEntry[];
}

/** 서버 -> 그 창에게만: 만든 결과. 재료가 없어 못 한 것과 주사위를 진 것은 다른 일이다. */
export interface Crafted {
	type: 'crafted';
	recipeId: number;
	attempted: boolean;
	succeeded: boolean;
	itemId: number;
	amount: number;
	denied: string;
}

export interface Bag {
	type: 'bag';
	items: { itemId: number; amount: number }[];
}

/** 서버 -> 창: 아이템 낱말표(들어올 때 한 번). 그 뒤로는 번호만 나른다. */
export interface Catalog {
	type: 'catalog';
	items: { itemId: number; name: string }[];
}

/** 서버 -> 창: 지을 수 있는 것 목록(들어올 때 한 번). 크기의 정본은 세계다. */
export interface BuildCatalog {
	type: 'buildcatalog';
	buildings: { buildingId: number; name: string; w: number; l: number }[];
}

/** 창 -> 서버: 여기에 이걸 짓고 싶다. 크기는 세계가 안다(창이 못 우긴다). */
export interface PlaceRequest {
	type: 'place';
	x: number;
	y: number;
	z: number;
	buildingId: number;
}

/** 창 -> 서버: 그 상자 안을 보여 줘. 손이 닿는지는 세계가 본다. */
export interface ChestAsk {
	type: 'chestask';
	x: number;
	y: number;
	z: number;
}

/** 서버 -> 그 창에게만: 그 상자 안은 이렇다(없는 상자면 items 가 빈다). */
export interface Chest {
	type: 'chest';
	x: number;
	y: number;
	z: number;
	items: { itemId: number; amount: number }[];
}

/** 창 -> 서버: 이걸 상자에 넣겠다 / 꺼내겠다. 되는지는 세계가 본다. */
export interface ChestPut {
	type: 'chestput';
	x: number;
	y: number;
	z: number;
	itemId: number;
	amount: number;
}

export interface ChestTake {
	type: 'chesttake';
	x: number;
	y: number;
	z: number;
	itemId: number;
	amount: number;
}

/** 창 -> 서버: 다른 기기를 이을 초대 열쇠를 만들어 줘. */
export interface InviteAsk {
	type: 'inviteask';
}

/** 서버 -> 그 창에게만: 그건 안 된다(무엇을·왜). 거절도 대답이다. */
export interface Denied {
	type: 'denied';
	what: string;
	why: string;
}

/** 서버 -> 그 창에게만: 초대 열쇠(한 번만 쓴다). */
export interface Invite {
	type: 'invite';
	code: string;
}

/** 창 -> 서버: 이 초대 열쇠로 나를 그 사람에 이어 줘. */
export interface LinkRequest {
	type: 'link';
	code: string;
}

/** 서버 -> 그 창에게만: 이었나(이었으면 다시 들어와야 그 사람으로 논다). */
export interface Linked {
	type: 'linked';
	ok: boolean;
	identityId: number;
}

/** 서버 -> 그 창에게만: 다른 곳에서 같은 사람이 들어왔다(여기서는 나간다). */
export interface Kicked {
	type: 'kicked';
	reason: string;
}

export interface Full {
	type: 'full';
	reason: string;
	most: number;
}

/** 서버 -> 그 창에게만: 네 인형은 여기 있다(몰린 칸에서 공유 소식에 자기가 빠졌을 때). */
export interface Me {
	type: 'me';
	doll: WorldDollView;
}

/** 서버 -> 창: 누가 무슨 이름인가(바뀔 때만). 창이 들고 있다가 인형에 붙인다. */
export interface DollNameView {
	id: number;
	name: string;
}

export interface Names {
	type: 'names';
	dolls: DollNameView[];
}

export type ServerMessage = Welcome | Me | Names | WorldSnapshot | BrewTaken | Bag | Catalog | BuildCatalog | BrewShelf | Spellbook | CraftBook | Crafted | Chest | Denied | Invite | Linked | Kicked | Said | Hurt | MoveOn | Did;
export type ClientMessage = MoveRequest | PlaceRequest | RemoveRequest | GatherRequest | ChestAsk | ChestPut | ChestTake | BrewRequest | BrewResetRequest | BrewCompleteRequest | Hello | BagAsk | ConsumeRequest | InviteAsk | LinkRequest | SayRequest | StrikeRequest;
