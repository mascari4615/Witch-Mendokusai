#!/usr/bin/env bash
# wm-unplaced-component-audit.sh — 「만들었지만 어디에도 안 놓인 부품」을 센다. (TASK-WM-209)
#
# 왜 있나 (2026-08-08, 내가 직접 밟았다): 낮밤 공방을 다 붙여 놓고 보니 그 부품이 **어느 씬에도 없었다.**
# 코드는 준비됐고 한 번도 안 돌았다. 층 감사(`wm-unwired-layer-audit.sh`)는 이걸 **초록으로 통과시킨다** —
# 「층이 불리나」와 「그 부품이 어딘가에 놓여 있나」는 다른 질문이기 때문이다.
#
# 세는 법 — 부품이 게임에 닿는 길은 둘뿐이다:
#   ① 씬·프리팹에 **놓여 있다** — 유니티는 스크립트를 이름이 아니라 **guid** 로 가리킨다(.cs.meta 의 guid).
#   ② 코드가 **붙인다** — `AddComponent<이름>()` / `AddComponent(typeof(이름))`.
# 둘 다 아니면 그 부품은 실행될 길이 없다.
#
# 한계 (정직):
#   - 「놓여 있지만 그 씬을 아무도 안 연다」는 못 본다.
#   - 이름을 문자열로 만들어 붙이는 경로(리플렉션 등)는 못 본다 — 그런 게 있으면 기준선에 적어라.
#
# 사용: .github/scripts/wm-unplaced-component-audit.sh [--list]
# 종료 코드: 0 = 기준선과 일치 / 1 = 새로 안 놓인 부품 또는 기준선 축소 필요 / 2 = 환경 미비

set -u

repo_root=$(git rev-parse --show-toplevel 2>/dev/null) || {
	echo "git 저장소 안에서 실행해라" >&2
	exit 2
}
cd "$repo_root" || exit 2

list_all="${1:-}"
baseline=".github/scripts/wm-unplaced-component-baseline.tsv"

known=""
if [ -f "$baseline" ]; then
	known=$(grep -v '^#' "$baseline" | awk 'NF > 0 { print $1 }')
fi

work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT

# ① 부품 목록 — Domain 아래에서 MonoBehaviour 를 물려받는 것들. 추상 클래스는 그 자체로 안 놓이므로 뺀다.
components="$work_dir/components.tsv"   # 이름 \t 소스경로 \t guid
: > "$components"

# 파일 하나하나에 grep 을 걸면 1000개 넘게 돌아 몇 분이 걸린다(실측 3분 30초).
# 먼저 **MonoBehaviour 가 들어 있는 파일만** 한 번에 걸러 낸다.
candidates="$work_dir/candidates.txt"
git -c core.quotepath=false ls-files 'Assets/_WitchMendokusai/Domain/*.cs' > "$work_dir/domain-cs.txt"
# ⚠ 파일 목록을 `$(cat …)` 로 펼치면 **경로에 있는 공백에서 쪼개진다.**
#   이 저장소엔 `[Panel] DungeonRuntime.prefab` 처럼 공백·대괄호가 든 이름이 많고,
#   그래서 「자산에 안 박혔다」는 오탐이 무더기로 났다(실측). 줄 단위로 넘긴다.
xargs -d '\n' -a "$work_dir/domain-cs.txt" \
	grep -lE 'class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*:[[:space:]]*([A-Za-z0-9_.]+,[[:space:]]*)*MonoBehaviour' \
	2>/dev/null > "$candidates"

while IFS= read -r source; do
	[ -f "$source" ] || continue
	[ -f "$source.meta" ] || continue

	name=$(grep -oE 'class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*:[[:space:]]*([A-Za-z0-9_.]+,[[:space:]]*)*MonoBehaviour' "$source" \
		| head -1 | awk '{ print $2 }')
	[ -z "$name" ] && continue

	grep -qE "abstract[[:space:]]+class[[:space:]]+$name\b" "$source" && continue
	# partial 조각 파일 (TowerDefenseMatch.Clock.cs 처럼 클래스 이름과 다른 이름) 은 부품이 아님. 놓이는 것은 클래스 하나이고 그 guid 는 이름과 같은 파일의 것
	if grep -qE "partial[[:space:]]+class[[:space:]]+$name\b" "$source" && [ "$(basename "$source")" != "$name.cs" ]; then continue; fi

	guid=$(sed -n 's/^guid: //p' "$source.meta" | tr -d '\r' | head -1)
	[ -z "$guid" ] && continue

	printf '%s\t%s\t%s\n' "$name" "$source" "$guid" >> "$components"
done < "$candidates"

if [ ! -s "$components" ]; then
	echo "부품이 하나도 안 잡혔다 — 경로 가정이 틀렸다" >&2
	exit 2
fi

# ② 씬·프리팹에 박힌 guid 를 한 번에 훑는다.
guids="$work_dir/guids.txt"
cut -f3 "$components" | sort -u > "$guids"

placed="$work_dir/placed.txt"
git -c core.quotepath=false ls-files '*.unity' '*.prefab' > "$work_dir/assets.txt"
if [ -s "$work_dir/assets.txt" ]; then
	xargs -d '
' -a "$work_dir/assets.txt" grep -ohF -f "$guids" 2>/dev/null | sort -u > "$placed"
else
	: > "$placed"
fi

# ③ 코드가 붙이는 것들.
sources="$work_dir/sources.txt"
git -c core.quotepath=false ls-files 'Assets/_WitchMendokusai/*.cs' > "$sources"
added="$work_dir/added.txt"
# ⚠ 붙이는 길이 하나 더 있다 — **DI 등록**이다(`RegisterComponentOnNewGameObject<T>` 등).
#   컨테이너가 게임오브젝트를 만들어 부품을 달아 준다. 이걸 빼먹고 처음 돌렸더니 23건 중
#   여럿이 오탐이었다(대화 러너 등 — 코드에서 15곳이 쓰는데 「닿을 길 없음」으로 잡혔다).
xargs -d '
' -a "$sources" grep -ohE 'AddComponent[[:space:]]*<[[:space:]]*[A-Za-z_][A-Za-z0-9_]*|AddComponent[[:space:]]*\([[:space:]]*typeof[[:space:]]*\([[:space:]]*[A-Za-z_][A-Za-z0-9_]*|Register[A-Za-z]*[[:space:]]*<[[:space:]]*[A-Za-z_][A-Za-z0-9_]*' 2>/dev/null \
	| grep -oE '[A-Za-z_][A-Za-z0-9_]*$' | sort -u > "$added"

unplaced="$work_dir/unplaced.txt"
: > "$unplaced"

total=0
while IFS=$'\t' read -r name source guid; do
	total=$((total + 1))

	grep -qxF "$guid" "$placed" && continue
	grep -qxF "$name" "$added" && continue

	printf '%s\t%s\n' "$name" "$source" >> "$unplaced"
done < "$components"

placed_count=$((total - $(wc -l < "$unplaced")))
echo "부품 $total 개 — 놓였거나 코드가 붙임 $placed_count · 닿을 길 없음 $(wc -l < "$unplaced")"
echo

if [ "$list_all" = "--list" ]; then
	sort "$unplaced" | awk -F'\t' '{ printf "  %-40s %s\n", $1, $2 }'
	echo
fi

new_debt=""
while IFS=$'\t' read -r name source; do
	is_known=0
	for entry in $known; do
		[ "$entry" = "$name" ] && is_known=1
	done
	[ "$is_known" -eq 0 ] && new_debt="$new_debt $name"
done < "$unplaced"

repaid=""
for entry in $known; do
	grep -qP "^\Q$entry\E\t" "$unplaced" 2>/dev/null || grep -q "^$entry	" "$unplaced" || repaid="$repaid $entry"
done

status=0

if [ -n "$new_debt" ]; then
	echo "★ 닿을 길이 없는 부품 —$new_debt"
	echo "  씬·프리팹 어디에도 없고, 코드가 붙이지도 않는다. 지금 지워도 아무 일도 안 일어난다."
	echo "  ① 놓든지 붙이든지, ② 아직 아니면 $baseline 에 이름과 사유를 적어라."
	echo
	status=1
fi

if [ -n "$repaid" ]; then
	echo "✅ 이제 닿는 부품 —$repaid"
	echo "  $baseline 에서 빼라. 기준선은 줄어들기만 해야 한다."
	echo
	status=1
fi

[ "$status" -eq 0 ] && echo "=== 기준선과 일치 ==="

exit "$status"
