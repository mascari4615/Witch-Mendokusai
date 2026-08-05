#!/usr/bin/env bash
# wm-revert-audit.sh — 「어떤 커밋이 다른 커밋의 일을 조용히 되돌렸나」를 찾는다.
#
# 왜 있나 (2026-08-06): 여러 세션이 같은 main 에 붙는데, 그중 하나가 **옛 사본을 든 작업트리**로
# 파일을 통째 다시 쓰면 앞선 커밋의 변경이 통째로 사라진다. diff 는 「그 세션이 고친 것」처럼 보이고,
# 컴파일도 대개 통과한다(되돌림이 자기완결적이라). 그래서 **아무도 모른 채로 며칠을 산다.**
# 실측: `54a3da61` 한 커밋이 .cs 57개를 이전 판으로 되돌렸고, 그중 42개가 그대로 살아 있었다.
#
# 판정 원리 (문자열 매칭 아님 — blob 동일성):
#   파일 F 에 대해, 커밋 C 직전에 F 를 만진 커밋을 P 라 하자.
#   C 의 F 내용 == P^ 의 F 내용  →  C 는 P 의 F 변경을 통째로 되돌린 것이다.
#   추가로 HEAD 의 F 내용 == P^ 의 F 내용 이면 **아직 안 고쳐진 것**.
#
# 한계 (정직):
#   - P 이후 다른 커밋이 F 를 또 만졌으면 HEAD 비교는 「안 같음」이 되어 STILL 목록에서 빠진다.
#     즉 STILL 목록은 **하한**이다 — 「최소 이만큼은 아직 되돌아가 있다」.
#   - 의도적 되돌리기(git revert)도 같은 모양이라 걸린다. 커밋 메시지로 가려낼 것.
#
# 사용:
#   .github/scripts/wm-revert-audit.sh <commit>        # 그 커밋이 되돌린 것
#   .github/scripts/wm-revert-audit.sh <commit> --all  # 되돌림 전부(이미 복구된 것 포함)
#
# 종료 코드: 0 = 아직 안 고쳐진 되돌림 0건 / 1 = 있음 / 2 = 사용법 오류

set -u

target="${1:-}"
show_all="${2:-}"

if [ -z "$target" ]; then
	echo "usage: $0 <commit> [--all]" >&2
	exit 2
fi

if ! git rev-parse --verify "$target^{commit}" >/dev/null 2>&1; then
	echo "unknown commit: $target" >&2
	exit 2
fi

still=0
repaired=0

while IFS= read -r file; do
	[ -z "$file" ] && continue

	prev=$(git log "$target^" -1 --format=%H -- "$file" 2>/dev/null)
	[ -z "$prev" ] && continue

	undone=$(git log -1 --format='%h %s' "$prev")

	had_before=0
	git cat-file -e "$prev^:$file" 2>/dev/null && had_before=1
	has_at_target=0
	git cat-file -e "$target:$file" 2>/dev/null && has_at_target=1

	if [ "$had_before" -eq 0 ]; then
		# P 가 *만든* 파일을 C 가 지웠다 = 그 파일 통째로 되돌림. 가장 아픈 경우인데
		# 사라진 파일은 diff 에서 눈에 안 띈다 (실측: MotorTuning.cs / WM-199).
		[ "$has_at_target" -eq 1 ] && continue
		if git cat-file -e "HEAD:$file" 2>/dev/null; then
			repaired=$((repaired + 1))
			[ "$show_all" = "--all" ] && echo "REPAIRED? $file  (지워졌던 것: $undone — 이후 되살아남)"
		else
			echo "STILL-GONE $file   ← 파일이 통째로 사라졌다"
			echo "          되돌아간 것: $undone"
			echo "          복구:        git checkout $prev -- '$file'"
			still=$((still + 1))
		fi
		continue
	fi

	[ "$has_at_target" -eq 0 ] && continue

	before=$(git rev-parse "$prev^:$file")
	at_target=$(git rev-parse "$target:$file")
	[ "$before" != "$at_target" ] && continue

	if git cat-file -e "HEAD:$file" 2>/dev/null && [ "$(git rev-parse "HEAD:$file")" = "$before" ]; then
		echo "STILL     $file"
		echo "          되돌아간 것: $undone"
		echo "          복구:        git checkout $prev -- '$file'"
		still=$((still + 1))
	else
		repaired=$((repaired + 1))
		if [ "$show_all" = "--all" ]; then
			echo "REPAIRED? $file  (되돌아갔던 것: $undone — 이후 누가 손댐)"
		fi
	fi
done < <(git show --name-only --format='' "$target")

echo
echo "=== $target: 아직 되돌아가 있음 $still건 / 이후 손댐 $repaired건 ==="
echo "    (STILL 은 하한이다 — 이후 다른 커밋이 만진 파일은 여기 안 잡힌다)"

[ "$still" -gt 0 ] && exit 1
exit 0
