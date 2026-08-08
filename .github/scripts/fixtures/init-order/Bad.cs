// TASK-WM-211 — 게이트 자기 검사 표본. 유니티 밖(.github/)이라 게임 컴파일 대상이 아니다.
// 기대: Awake 의 Find = [BLOCK] 1건, Start 의 Find = [ORDER-RISK] 1건.
using UnityEngine;

public class Bad : MonoBehaviour
{
	// 이유 없는 Awake-Find — 반드시 [BLOCK] 으로 잡혀야 한다.
	private void Awake()
	{
		Thing thing = FindAnyObjectByType<Thing>();
		Setup();
	}

	// 이름은 Awake 가 아니지만 Awake 가 부른다 — 도는 시점이 같으므로 [AWAKE-CHAIN] 1건이어야 한다.
	private void Setup()
	{
		Thing[] all = FindObjectsByType<Thing>();
	}

	// 마커 없는 Start-Find — 반드시 [ORDER-RISK] 로 잡혀야 한다.
	private void Start()
	{
		Thing[] things = FindObjectsByType<Thing>();
	}

	// 주석 안의 FindAnyObjectByType 은 호출이 아니다 — 이 줄은 잡히면 안 된다.
	private void OnDisable()
	{
	}
}
