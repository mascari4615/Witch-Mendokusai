// 고르개가 보여 주는 글 — 창의 <b>순수한 계산</b>만 여기 둔다 (TASK-WM-217).
//
// ★ 왜 떼어냈나: 창(index.html)에는 시험이 없었다. 그래서 「나무 0/2」 같은 표시가 조용히
//   틀려도 아무도 모른다 — 실제로 이 근처에서 <b>나무(0번)를 솥에 못 넣는</b> 함정을 넷 밟았다.
//   화면에 붙는 부분(DOM)은 시험하기 어렵지만, <b>무슨 글을 보여 줄지</b>는 순수 계산이라
//   진짜 데이터로 그대로 잴 수 있다. 그래서 그 부분만 떼어 놓는다.
//
// 규칙 하나: 여기 있는 함수는 화면을 모른다. 받은 것으로 글만 만든다.

/** 가방에서 그 물건을 몇 개 들고 있나 — 없으면 0. */
export function carrying(bag, itemId) {
	if (Array.isArray(bag) === false) return 0;

	// ⚠ itemId 0(나무)도 진짜 물건이다 — 「없음」으로 거르면 나무가 늘 0개로 보인다.
	const row = bag.find((one) => one && one.itemId === itemId);
	return row ? row.amount : 0;
}

/**
 * 지을 것 한 칸의 글 — 「· 」가 앞에 붙으면 <b>지금은 못 짓는다</b>는 뜻이다.
 * 크기는 여러 칸일 때만 붙인다(1×1 은 굳이 말하지 않는다).
 */
export function buildLabel(kind, bag, names) {
	const size = kind.w > 1 || kind.l > 1 ? ` ${kind.w}×${kind.l}` : '';
	const have = carrying(bag, kind.costItemId);
	const enough = kind.costAmount === 0 || have >= kind.costAmount;
	const material = (names && names[kind.costItemId]) || `#${kind.costItemId}`;
	const cost = kind.costAmount > 0 ? ` — ${material} ${have}/${kind.costAmount}` : '';

	return `${enough ? '' : '· '}${kind.name}${size}${cost}`;
}

/** 지금 그것을 지을 수 있나 — 공짜(비용 0)면 언제나 된다. */
export function canBuild(kind, bag) {
	return kind.costAmount === 0 || carrying(bag, kind.costItemId) >= kind.costAmount;
}

/**
 * 만들 것 한 줄의 글 — 재료를 「나무 1/3」처럼, 확실하지 않으면 성공률도 같이.
 * 「· 」가 앞에 붙으면 재료가 모자란다는 뜻이다.
 */
export function craftLabel(recipe, bag, names) {
	const needs = [];
	let enough = true;

	const itemIds = recipe.itemIds || [];
	const amounts = recipe.amounts || [];
	for (let i = 0; i < itemIds.length; i++) {
		const itemId = itemIds[i];
		const need = amounts[i];
		const have = carrying(bag, itemId);
		if (have < need) enough = false;

		needs.push(`${(names && names[itemId]) || `#${itemId}`} ${have}/${need}`);
	}

	const luck = recipe.percentage >= 100 ? '' : ` (${recipe.percentage}%)`;
	return `${enough ? '' : '· '}${recipe.name}${luck} — ${needs.join(', ')}`;
}

/** 지금 그 줄대로 만들 수 있나 — 재료만 본다(주사위는 세계가 굴린다). */
export function canCraft(recipe, bag) {
	const itemIds = recipe.itemIds || [];
	const amounts = recipe.amounts || [];
	for (let i = 0; i < itemIds.length; i++) {
		if (carrying(bag, itemIds[i]) < amounts[i]) return false;
	}

	return true;
}
