// TASK-WM-211 — 게이트 자기 검사 표본.
// 기대: Start 는 메서드 위 마커로 면제, OnEnable 은 마커 밖이라 [ORDER-RISK] 1건.
using UnityEngine;

public class Good : MonoBehaviour
{
	/// <summary>씬 정적 배치라 Start 시점 존재 보장.</summary>
	// init-order-ok: 씬에 미리 놓인 것만 훑는다 — 이유를 메서드 위에 적었다.
	private void Start()
	{
		Thing[] things = FindObjectsByType<Thing>();
	}

	// 마커가 파일 전체로 새면 안 된다 — 이 메서드는 그대로 잡혀야 한다.
	private void OnEnable()
	{
		Thing other = FindAnyObjectByType<Thing>();
	}
}
