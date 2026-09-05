#!/usr/bin/env bash
# WM — 판정 층 이름이 게임 층 이름과 부딪히나 (TASK-WM-217).
#
# 왜 필요했나: 엔진 밖 빌드(Portable)는 **DomainSDK 만** 굽는다. 그래서 게임 층에 이미 있는
# 이름을 판정 층에 또 만들어도 초록이다 — 유니티에서만 CS0436/CS0246 으로 터진다.
# 실제로 두 번 났다: `Doll`(마녀의 인형 SO) · `DollView`(UI 요소).
#
# 둘 다 rootNamespace 가 `WitchMendokusai` 로 같아서, 같은 이름은 곧 충돌이다.
# 게임 층이 먼저 있었으므로 **판정 층이 이름을 양보한다**(WorldDoll / WorldDollView).
set -euo pipefail

SDK_DIR='DomainSDK'
GAME_DIRS=('Assets/_WitchMendokusai/Domain' 'Assets/_WitchMendokusai/Core' 'Assets/_WitchMendokusai/Network' 'Assets/_WitchMendokusai/ViewModel')

if [ ! -d "$SDK_DIR" ]; then
	echo "::error::$SDK_DIR 가 없다 — 게이트가 통과한 게 아니라 아무것도 안 본 것이다."
	exit 1
fi

# 선언된 형 이름만 뽑는다(제네릭 인자·주석은 제외). 부분 클래스는 중복 제거.
# ⚠ 파일 목록을 인자로 넘기지 X — 수천 개면 grep 이 exit 2 로 죽고, set -e 아래서 게이트가
#   「검사 실패」가 아니라 그냥 조용히 멈춘다. 폴더를 주고 grep 이 스스로 훑게 한다.
declare_names() {
	# 들여쓰기 1단(네임스페이스 바로 아래)까지만 = **top-level 형**. 중첩 형(2단 이상)은 감싸는
	# 형이 이름을 한정하므로 부딪히지 않는다 (실제로 TowerDefense 안의 `Node` 가 그렇다).
	grep -rhoE --include='*.cs' '^(	|    )?(public|internal)[[:space:]]+(sealed[[:space:]]+|abstract[[:space:]]+|static[[:space:]]+|partial[[:space:]]+|readonly[[:space:]]+)*(class|struct|interface|enum|record)[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' "$@" 2>/dev/null \
		| sed -E 's/.*(class|struct|interface|enum|record)[[:space:]]+//' \
		| sort -u
}

SDK_NAMES=$(declare_names "$SDK_DIR")
SDK_COUNT=$(printf '%s\n' "$SDK_NAMES" | grep -c . || true)
if [ "$SDK_COUNT" -lt 20 ]; then
	echo "::error::판정 층에서 뽑은 이름이 ${SDK_COUNT}개뿐이다 — 뽑는 방식이 깨졌다(통과 아님)."
	exit 1
fi

PRESENT_DIRS=()
for dir in "${GAME_DIRS[@]}"; do
	[ -d "$dir" ] && PRESENT_DIRS+=("$dir")
done

if [ "${#PRESENT_DIRS[@]}" -eq 0 ]; then
	echo "::error::게임 층 폴더를 하나도 못 찾았다 — 비교 대상이 없으면 통과가 아니라 실패다."
	exit 1
fi

GAME_NAMES=$(declare_names "${PRESENT_DIRS[@]}")
# comm 은 정렬 규칙(locale)에 예민해 조용히 어긋난다 — 같은 줄 찾기는 grep 이 확실하다.
COLLISIONS=$(printf '%s\n' "$GAME_NAMES" | grep -Fx -f <(printf '%s\n' "$SDK_NAMES") || true)

if [ -n "$COLLISIONS" ]; then
	echo "::error::판정 층 이름이 게임 층 이름과 부딪힌다 — 유니티에서만 터진다(엔진 밖 빌드는 못 본다):"
	printf '%s\n' "$COLLISIONS" | while read -r name; do
		[ -z "$name" ] && continue
		echo "::error::  $name"
	done
	echo "::error::판정 층이 이름을 양보할 것 (예: Doll -> WorldDoll, DollView -> WorldDollView)."
	exit 1
fi

echo "판정 층 이름 ${SDK_COUNT}개 — 게임 층과 부딪히는 것 0."
