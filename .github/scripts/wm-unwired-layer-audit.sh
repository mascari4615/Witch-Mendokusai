#!/usr/bin/env bash
# wm-unwired-layer-audit.sh — 「만들었지만 게임이 한 번도 안 부르는 층」을 센다. (TASK-WM-209)
#
# 왜 있나 (2026-08-07 사고 실측): 커밋 하나가 파일 105개를 지웠는데 **컴파일이 안 깨졌다.**
# 지워진 층을 부르는 코드가 0곳이었기 때문이다. 아무도 모른 채 하루가 지났다.
# 아무도 안 부르는 층은 ① 기능이 없고 ② 지워져도 안 울리는 **두 배로 나쁜 상태**다.
#
# 어떻게: 게이트가 아니라 **래칫**이다. 지금 있는 빚은 기준선에 적어 두고 통과시키되,
#   - 기준선에 **없는** 층이 호출처 0 이면 → 실패 (새 빚 유입 차단)
#   - 기준선에 있는 층이 **배선되면** → 기준선에서 빼라고 요구 (줄어들기만 한다)
#
# 세는 법: `DomainSDK/<층>/*.cs` 의 타입 이름들이 **그 층 밖 게임 코드**에서 몇 번 언급되나.
#   시험(`Tests/`)은 호출처로 안 센다 — 시험만 지키는 상태가 바로 이 사고의 모양이었다.
#
# 한계 (정직):
#   - 이름 문자열 매칭이다. 같은 이름의 다른 타입이 있으면 부풀 수 있다(0 판정엔 영향 적음).
#   - 「불리기는 하는데 죽은 경로」는 못 본다. 0 이냐 아니냐만 본다.
#
# 사용: .github/scripts/wm-unwired-layer-audit.sh
# 종료 코드: 0 = 기준선과 일치 / 1 = 새 미배선 층 또는 기준선 축소 필요 / 2 = 환경 미비

set -u

repo_root=$(git rev-parse --show-toplevel 2>/dev/null) || {
	echo "git 저장소 안에서 실행해라" >&2
	exit 2
}
cd "$repo_root" || exit 2

sdk_root="DomainSDK"
baseline=".github/scripts/wm-unwired-layer-baseline.tsv"

if [ ! -d "$sdk_root" ]; then
	echo "$sdk_root 가 없다" >&2
	exit 2
fi

known=""
if [ -f "$baseline" ]; then
	known=$(grep -v '^#' "$baseline" | awk 'NF > 0 { print $1 }')
fi

new_debt=""
repaid=""

# 타입 하나마다 저장소를 훑으면 몇 분이 걸린다(실측: 2분 넘게 안 끝남).
# 훑을 파일 목록을 **한 번만** 만들고, 층마다 이름 목록을 통째로 넘겨 grep 을 한 번씩만 부른다.
work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT

haystack="$work_dir/haystack.txt"
git -c core.quotepath=false ls-files 'Assets/_WitchMendokusai/*.cs' \
	| grep -v "^$sdk_root/" \
	| grep -v "/Tests/" > "$haystack"

# 「부르는 것이 코드가 아니라 데이터」인 타입이 있다 — `[SerializeReference]` 로 자산 안에서 태어나는 것들
# (규칙 조건, 노드 그래프 노드 등). 코드 호출처만 세면 그 부류가 통째로 오탐이 된다.
# 다른 슬롯이 짚어 준 지적이다(2026-08-08 session-bus). 자산 쪽도 같이 센다.
asset_haystack="$work_dir/assets.txt"
git -c core.quotepath=false ls-files 'Assets/_WitchMendokusai/*.asset' 'Assets/_WitchMendokusai/*.prefab' 'Assets/_WitchMendokusai/*.unity' > "$asset_haystack"

if [ ! -s "$haystack" ]; then
	echo "훑을 게임 코드가 하나도 안 잡혔다 — 경로 가정이 틀렸다" >&2
	exit 2
fi

printf '%-16s %8s %10s %10s\n' "층" "타입수" "코드호출" "자산참조"
printf -- '---------------- -------- ---------- ----------\n'

for layer_dir in "$sdk_root"/*/; do
	[ -d "$layer_dir" ] || continue
	layer=$(basename "$layer_dir")

	names="$work_dir/names.txt"
	: > "$names"
	type_count=0

	# 파일 이름이 아니라 **파일이 실제로 선언하는 타입 이름**을 뽑는다.
	# (실측 오탐: `Events/QuestEvents.cs` 는 `QuestAddedEvent` 등을 담는다 — 파일 이름으로 세면
	#  「아무도 안 부른다」가 나오지만 실제로는 퀘스트 전체가 쓴다.)
	for source in "$layer_dir"*.cs; do
		[ -f "$source" ] || continue
		grep -oE '^[[:space:]]*public[[:space:]]+([a-z]+[[:space:]]+)*(class|struct|interface|enum|record)[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' "$source" \
			| awk '{ print $NF }' >> "$names"
	done

	sort -u "$names" -o "$names"
	type_count=$(wc -l < "$names")

	[ "$type_count" -eq 0 ] && continue

	# -w = 이름 전체가 맞을 때만(부분 일치로 부풀지 않게), -F = 정규식 아님, -f = 이름 목록 파일.
	# 경로 공백에서 쪼개지지 않게 줄 단위로 넘긴다(실측 오탐 원인).
	call_sites=$(xargs -d '
' -a "$haystack" grep -lwF -f "$names" 2>/dev/null | wc -l)

	asset_refs=0
	if [ -s "$asset_haystack" ]; then
		asset_refs=$(xargs -d '
' -a "$asset_haystack" grep -lwF -f "$names" 2>/dev/null | wc -l)
	fi

	# 코드가 부르든 자산이 부르든, 어느 쪽이든 「불린다」로 본다.
	used=$((call_sites + asset_refs))

	printf '%-16s %8s %10s %10s\n' "$layer" "$type_count" "$call_sites" "$asset_refs"

	is_known=0
	for entry in $known; do
		[ "$entry" = "$layer" ] && is_known=1
	done

	if [ "$used" -eq 0 ] && [ "$is_known" -eq 0 ]; then
		new_debt="$new_debt $layer"
	fi

	if [ "$used" -gt 0 ] && [ "$is_known" -eq 1 ]; then
		repaid="$repaid $layer"
	fi
done

echo

status=0

if [ -n "$new_debt" ]; then
	echo "★ 새로 생긴 미배선 층 —$new_debt"
	echo "  게임 코드도, 자산도 한 번도 안 부른다. 지금 지워도 컴파일이 안 깨진다는 뜻이다."
	echo "  ① 배선을 넣든지, ② 정말 나중에 할 거면 $baseline 에 층 이름을 적고 사유를 남겨라."
	echo
	status=1
fi

if [ -n "$repaid" ]; then
	echo "✅ 배선된 층 —$repaid"
	echo "  $baseline 에서 이 줄들을 빼라. 기준선은 줄어들기만 해야 한다."
	echo
	status=1
fi

if [ "$status" -eq 0 ]; then
	echo "=== 기준선과 일치 — 새 미배선 층 0 ==="
	if [ -n "$known" ]; then
		echo "    (아직 갚을 빚이 남아 있다: $(printf '%s' "$known" | tr '\n' ' '))"
	fi
fi

exit "$status"
