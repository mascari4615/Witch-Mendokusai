# 대화 하네스 — 유니티 없이 대화 로직 돌리기

한 슬롯이 유니티를 잡고 있으면 나머지는 `-batchmode` 컴파일조차 못 한다.
그 사이에도 **대화 시스템의 순수 로직만은 돌려 볼 수 있게** 만든 것이다.

```bash
dotnet run --project Tools/dialogue-harness
```

## 무엇이 도는가

- `Assets/_WitchMendokusai/Tests/EditMode/Dialogue*Test.cs` — **원본 그대로** 컴파일·실행한다.
  (사본을 두지 않는다. 사본은 낡아서 거짓 초록을 낸다.)
- 게임에 들어간 원고(`Domain/Narrative/Demo/오프닝.txt`)를 실제로 읽어 끝까지 재생한다.
- 형제 저장소 `memo/wm/design/narrative/` 원고가 있으면 같이 훑고 **왕복**(읽기→쓰기→읽기)까지 잰다.

## 무엇이 가짜인가

`stubs/` 셋뿐이다 — `UnityEngine` 최소 대역(파괴된 객체가 `== null` 로 보이는 규칙 포함),
`NUnit` 최소 대역, 게임 쪽 인터페이스 몇 개. **게임 소스는 진짜 파일을 그대로 문다.**

## 무엇을 보장하지 *않는가* — 이게 제일 중요하다

- **직렬화**(`[SerializeReference]` 가 실제로 저장되는지) · **에셋 임포트** · **MonoBehaviour 수명주기** · **PlayMode**.
- 따라서 이건 `run_tests` 의 **대체가 아니라 그 앞의 체**다. 정본은 유니티다.
- MonoBehaviour 인 파일(러너·화자 태그 등)은 아예 컴파일 대상에서 빼 뒀다(csproj 참조).

## 왜 이걸 저장소에 두나

이 하네스가 없던 동안, 에디터를 못 쓰는 세션은 「시험을 *썼다*」까지만 하고 초록을 본 적이 없었다.
지금은 같은 파일이 실제로 돈다 — 누구든 위 한 줄로 확인할 수 있다.
