using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-5d — 한 활동의 *장소*(식당·침대·공방·수다터). 캐릭터가 그 활동을 고르면 여기로 걸어와 머문다.
	/// 위치(transform)만 의미 — 패드 메시·색·라벨은 LifeWorldBootstrap 가 입힌다. LifeDirector 가 모아 LifeAgent 에 주입.
	/// 미래(INC-7): 실제 가구·방(부엌·침실)이 이 마커를 대체.
	/// </summary>
	public class LifeZone : MonoBehaviour
	{
		[SerializeField] private ActivityKind activity;

		public ActivityKind Activity => activity;
		public Vector3 Position => transform.position;

		/// <summary>런타임 스폰용 — 어느 활동의 장소인지 지정.</summary>
		public void SetActivity(ActivityKind value) => activity = value;
	}
}
