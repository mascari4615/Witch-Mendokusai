using UnityEngine;

namespace WitchMendokusai
{
	// 바닥 액체 한 종류 (기름, 물, 독, 점액). 색과 성질과 피해
	[CreateAssetMenu(fileName = "GoopKind_", menuName = "WM/Remains/Goop Kind")]
	public class GoopKind : ScriptableObject
	{
		[Tooltip("웅덩이 색. 알파는 가장 진할 때 불투명도")] public Color Color = new(.2f, .2f, .2f, .8f);
		[Min(1f), Tooltip("가만히 두면 마를 때까지 초")] public float DrySeconds = 30f;

		[Header("성질")]
		[Tooltip("불이 붙고 이웃 칸으로 번짐")] public bool Flammable;
		[Tooltip("감전이 이어진 웅덩이 전체로 즉시 퍼짐")] public bool Conductive;

		[Header("밟은 것에 주는 것")]
		[Min(0), Tooltip("몬스터에게 초당 피해")] public int MonsterDamagePerSecond;
		[Min(0), Tooltip("주인공에게 초당 피해")] public int PlayerDamagePerSecond;
		[Range(0f, 1f), Tooltip("이동 속도 배율. 1 이면 느려지지 않음")] public float MoveSpeedMultiplier = 1f;
	}
}
