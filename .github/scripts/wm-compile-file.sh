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
# ⚠ 아직 유니티가 한 번도 안 컴파일한 **새 파일**은 참조 dll 에 없다. 새 파일끼리 서로 부르면
#   그 파일들을 **같이** 넘겨라 (안 그러면 「그런 이름 없다」가 뜬다 — 실측).
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

for source in "$@"; do
	if [ ! -f "$source" ]; then
		echo "그런 파일이 없다: $source" >&2
		exit 2
	fi
done

# 경로에 공백이 들어간다("Program Files") — 문자열로 이어붙이면 거기서 쪼개져
# 참조가 소스 파일로 오인된다(실측: CS2001 85건). 배열로 들고 다닌다.
refs=("-r:$netstandard")

# NUnit 은 .NET Framework 용으로 빌드돼 있어 `mscorlib` 라는 이름의 어셈블리를 요구한다.
# 진짜 mscorlib 를 넣으면 netstandard 와 같은 타입이 두 벌이 된다(CS0433) — 유니티가 같이 넣어주는
# **껍데기(shim)** 를 쓴다. 이름만 mscorlib 이고 내용은 netstandard 로 넘긴다.
netfx_shim="$unity_data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
[ -f "$netfx_shim" ] && refs+=("-r:$netfx_shim")

# 자기 어셈블리도 **넣는다.** 안 넣으면 같은 폴더의 이웃 타입들이 전부 「없는 이름」이 돼
# 오탐이 쏟아진다(실측). 넣으면 같은 타입이 두 벌 보이지만 컴파일러는 *소스 쪽을 쓴다* 고
# 알려줄 뿐이라(CS0436) 판정에 지장이 없다 — 그 경고만 아래에서 끈다.
for dll in "$script_assemblies"/*.dll; do
	refs+=("-r:$dll")
done

# `UnityEngine*` 만 넣으면 모자란다 — 같은 폴더에 `Unity.Scripting.dll` 같은 것들이 섞여 있고,
# `[RuntimeInitializeOnLoadMethod]` 하나 쓰면 바로 「참조 없다」가 뜬다(실측). 폴더째 넣는다.
for dll in "$unity_data"/Managed/UnityEngine/*.dll; do
	[ -f "$dll" ] && refs+=("-r:$dll")
done

# 시험 파일이 쓰는 NUnit — 패키지 캐시 안에 있어 경로가 버전마다 다르다.
nunit=$(find Library/PackageCache -name 'nunit.framework.dll' 2>/dev/null | head -1)
[ -n "$nunit" ] && refs+=("-r:$nunit")

# 언어 버전 — 유니티가 쓰는 것과 맞춘다. 안 맞추면 csc 기본값(C# 7.3)으로 재서
# `new()` 같은 멀쩡한 문법이 전부 "Preview 기능이라 안 된다"(CS8652)로 나온다.
# 실측(2026-08-08): 저장소의 기존 파일(NodeGraph.cs 등)조차 이 오탐으로 빨개졌다 = 도구 쪽 문제였다.
# Unity 2021.2+ = C# 9. csproj 가 있으면 거기 적힌 값을 그대로 쓰고, 없으면 9.0.
lang_version=$(sed -n 's/.*<LangVersion>\(.*\)<\/LangVersion>.*/\1/p' Assembly-CSharp.csproj 2>/dev/null | head -1)
[ -z "$lang_version" ] && lang_version="9.0"

out=$(mktemp -u)".dll"
# 0649(값이 한 번도 대입 안 됨) · 0169(안 쓰는 필드) 는 끈다 — 유니티도 끈다.
# 0436(같은 타입이 소스와 dll 양쪽에) 도 끈다 — 위에서 자기 어셈블리를 일부러 넣었기 때문이다.
# `[SerializeField] private int foo;` 는 코드가 아니라 **인스펙터가** 채우므로 컴파일러 눈엔 안 채워진
# 것처럼 보인다. 이 저장소 곳곳이 그 모양이라, 안 끄면 멀쩡한 코드가 전부 빨강이 된다(실측).
# 참조가 200개를 넘으면 명령줄 길이 한계에 걸린다("Argument list too long" — 실측).
# 컴파일러가 읽는 응답 파일로 넘긴다. 경로에 공백이 있으니 한 줄씩 따옴표로 싼다.
work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT
rsp="$work_dir/refs.rsp"
: > "$rsp"
# ⚠ 응답 파일 안의 경로는 셸이 안 고쳐 준다. 명령줄로 넘길 땐 `/c/...` 가 자동으로 윈도우 경로로
# 바뀌지만 파일 안에 적으면 그대로 넘어가 「그런 파일 없다」가 된다(실측 175건).
# 경로마다 `cygpath` 를 부르면 200번 프로세스를 띄워 느려진다 — 다 적고 한 번에 바꾼다.
for ref in "${refs[@]}"; do
	printf '"%s"\n' "$ref" >> "$rsp"
done
sed -i 's|"-r:/\([a-zA-Z]\)/|"-r:\1:/|' "$rsp"

output=$("$mono" "$csc" -target:library -nostdlib+ -noconfig "-langversion:$lang_version" -nowarn:0649,0169,0436 "@$rsp" -out:"$out" "$@" 2>&1)
status=$?
rm -f "$out"

# 경고도 실패로 센다 — 이 프로젝트는 자기 어셈블리마다 `csc.rsp` 에 `-warnaserror+` 를 걸어 뒀다.
# 즉 유니티에서는 경고 하나가 곧 컴파일 에러다. 여기서만 통과시키면 초록을 잘못 본 것이 된다.
findings=$(printf '%s\n' "$output" | grep -cE "error CS|warning CS")

if [ "$findings" -gt 0 ]; then
	printf '%s\n' "$output" | grep -E "error CS|warning CS"
	echo
	echo "=== 에러·경고 합쳐 $findings 건 (이 프로젝트는 경고도 에러다) ==="
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
