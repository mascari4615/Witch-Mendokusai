#!/usr/bin/env bash
# wm-compile-file.sh — 유니티 에디터를 **안 열고** C# 파일 하나(또는 여럿)를 컴파일해 본다.
#
# 왜 있나 (2026-08-07, 병렬 세션 실측): 이 저장소는 여러 세션이 **같은 작업 폴더**를 쓴다.
# 유니티는 한 프로젝트를 두 번 못 연다 — 한 슬롯이 Play 를 돌리는 동안 나머지는
# `-batchmode` 컴파일조차 못 한다("another Unity instance is running"). 그렇다고 안 고쳐진 채로
# 커밋하면 「컴파일도 안 해보고 올렸다」가 된다. 그 사이를 메우는 도구다.
#
# 무엇을 보장하나 (정직하게):
#   ✅ 이 파일의 문법 · 참조하는 타입/멤버가 **실제 프로젝트 어셈블리에 존재하는지**.
#   ❌ 프로젝트 전체 컴파일이 아니다 — 내가 *바꾼 시그니처를 남이 쓰고 있던* 경우는 못 잡는다.
#      (그건 잠금이 풀린 뒤 `-batchmode` 전체 컴파일로 봐야 한다.)
#   ❌ 시험 실행이 아니다. 초록/빨강은 별개.
#
# 전제: 한 번이라도 유니티가 이 프로젝트를 컴파일한 적이 있어야 한다(`Library/ScriptAssemblies` 필요).
#
# 사용:
#   .github/scripts/wm-compile-file.sh Assets/.../Foo.cs [Bar.cs ...]
#
# 종료 코드: 0 = 에러 0 / 1 = 컴파일 에러 / 2 = 환경 미비(사용법·유니티·어셈블리 없음)

set -u

repo_root=$(git rev-parse --show-toplevel 2>/dev/null) || {
	echo "git 저장소 안에서 실행해라" >&2
	exit 2
}
cd "$repo_root" || exit 2

if [ "$#" -eq 0 ]; then
	echo "usage: $0 <file.cs> [more.cs ...]" >&2
	exit 2
fi

script_assemblies="Library/ScriptAssemblies"
if [ ! -d "$script_assemblies" ]; then
	echo "$script_assemblies 가 없다 — 유니티가 이 프로젝트를 한 번도 컴파일한 적이 없다는 뜻이다." >&2
	echo "잠금이 풀렸을 때 -batchmode 로 한 번 돌린 뒤 다시 시도해라." >&2
	exit 2
fi

# 유니티 설치 경로 — 프로젝트가 못 박아 둔 버전 그대로 쓴다(다른 버전으로 재면 의미가 없다).
editor_version=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')
unity_data="/c/Program Files/Unity/Hub/Editor/$editor_version/Editor/Data"
if [ ! -d "$unity_data" ]; then
	echo "유니티 $editor_version 을 못 찾았다: $unity_data" >&2
	exit 2
fi

mono="$unity_data/MonoBleedingEdge/bin/mono.exe"
csc="$unity_data/MonoBleedingEdge/lib/mono/msbuild/Current/bin/Roslyn/csc.exe"
netstandard="$unity_data/NetStandard/ref/2.1.0/netstandard.dll"
for required in "$mono" "$csc" "$netstandard"; do
	if [ ! -f "$required" ]; then
		echo "필요한 파일이 없다: $required" >&2
		exit 2
	fi
done

# 대상 파일이 **자기 어셈블리**에 이미 들어 있으면 같은 타입이 두 번 보여 CS0433 이 난다.
# 파일 위치에서 가장 가까운 .asmdef 를 찾아 그 dll 만 참조에서 뺀다.
own_assemblies=""
for source in "$@"; do
	if [ ! -f "$source" ]; then
		echo "그런 파일이 없다: $source" >&2
		exit 2
	fi

	dir=$(dirname "$source")
	while [ "$dir" != "." ] && [ "$dir" != "/" ]; do
		asmdef=$(find "$dir" -maxdepth 1 -name '*.asmdef' 2>/dev/null | head -1)
		if [ -n "$asmdef" ]; then
			name=$(sed -n 's/.*"name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$asmdef" | head -1)
			[ -n "$name" ] && own_assemblies="$own_assemblies $name"
			break
		fi
		dir=$(dirname "$dir")
	done
done

# 경로에 공백이 들어간다("Program Files") — 문자열로 이어붙이면 거기서 쪼개져
# 참조가 소스 파일로 오인된다(실측: CS2001 85건). 배열로 들고 다닌다.
refs=("-r:$netstandard")

# NUnit 은 .NET Framework 용으로 빌드돼 있어 `mscorlib` 라는 이름의 어셈블리를 요구한다.
# 진짜 mscorlib 를 넣으면 netstandard 와 같은 타입이 두 벌이 된다(CS0433) — 유니티가 같이 넣어주는
# **껍데기(shim)** 를 쓴다. 이름만 mscorlib 이고 내용은 netstandard 로 넘긴다.
netfx_shim="$unity_data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
[ -f "$netfx_shim" ] && refs+=("-r:$netfx_shim")

for dll in "$script_assemblies"/*.dll; do
	base=$(basename "$dll" .dll)
	skip=0
	for own in $own_assemblies; do
		[ "$base" = "$own" ] && skip=1
	done
	[ "$skip" -eq 1 ] && continue
	refs+=("-r:$dll")
done

for dll in "$unity_data"/Managed/UnityEngine/UnityEngine*.dll; do
	[ -f "$dll" ] && refs+=("-r:$dll")
done

# 시험 파일이 쓰는 NUnit — 패키지 캐시 안에 있어 경로가 버전마다 다르다.
nunit=$(find Library/PackageCache -name 'nunit.framework.dll' 2>/dev/null | head -1)
[ -n "$nunit" ] && refs+=("-r:$nunit")

out=$(mktemp -u)".dll"
output=$("$mono" "$csc" -target:library -nostdlib+ -noconfig "${refs[@]}" -out:"$out" "$@" 2>&1)
status=$?
rm -f "$out"

errors=$(printf '%s\n' "$output" | grep -c 'error CS')

if [ "$errors" -gt 0 ]; then
	printf '%s\n' "$output" | grep 'error CS'
	echo
	echo "=== 컴파일 에러 $errors 건 ==="
	echo "    (전체 컴파일이 아니다 — 남이 쓰던 시그니처를 바꿨는지는 여기서 안 보인다)"
	exit 1
fi

if [ "$status" -ne 0 ]; then
	printf '%s\n' "$output"
	echo
	echo "=== 컴파일러가 비정상 종료했다 (에러 CS 는 0건) ===" >&2
	exit 1
fi

echo "=== 에러 0 — $# 개 파일이 실제 프로젝트 어셈블리에 대고 컴파일된다 ==="
echo "    (전체 컴파일·시험 실행은 별개다. 잠금 풀리면 -batchmode 로 한 번 더 봐라)"
exit 0
