using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 직접 누르는 큰 포화. 따로 모으는 게이지가 다 차면 키로 발동
	// - 발동 동안 등 뒤 시점 (CameraManager.BeginSweepView). 바라보는 쪽으로 포화가 나가고 마우스 좌우로 훑음
	// - 그동안 피해를 안 받음 (보호막 자리 IDamageAbsorber). 자동 발동은 쉼
	// - 끝나거나 끊기면 (무기 교체, 판 나감, 사망) 시점과 조작을 되돌림
	// - 무장, 탄, 막타 그림은 일제 사격과 같은 VolleyArsenal. 판정은 탄 수와 피해만 다름
	public partial class VolleyGauge
	{
		[Header("큰 포화 (직접 누름)")]
		[SerializeField, Tooltip("켜면 큰 포화를 씀. 몸 배치 프리팹만")] private bool barrageEnabled;
		[SerializeField, Min(1f), Tooltip("일제 사격 게이지보다 크게")] private float barrageSize = 360f;
		[SerializeField, Min(0.1f), Tooltip("포화 길이 (초)")] private float barrageSeconds = 3f;
		[SerializeField, Min(1)] private int barrageShotsPerWeapon = 6;
		[SerializeField, Min(0), Tooltip("탄 피해 배율 (퍼센트)")] private int barragePercent = 120;
		[SerializeField, Range(0f, 60f), Tooltip("포화 탄이 흩어지는 폭 (도)")] private float barrageFanDegrees = 12f;
		[SerializeField] private string barrageReadyText = "포화 준비 (R)";
		[SerializeField] private string barrageText = "일제 포화!";
		[SerializeField] private OrbitCameraProfile barrageView = new();
		[SerializeField, Tooltip("등 뒤 시점에 인물 머리 대신 그릴 뒷모습")] private Sprite barrageBackHead;

		private readonly BarrageGuard barrageGuard = new();
		private float barrageGauge;
		private Coroutine barrageRoutine;
		private Player barragePlayer;
		private Sprite frontHead;
		private bool barraging;

		/// <summary> 0 에서 1 </summary>
		public float BarrageFill01 => barrageGauge / barrageSize;

		public bool IsBarrageReady => barrageEnabled && barraging == false && barrageGauge >= barrageSize;

		// 피해를 전부 막음. 낙사처럼 무적을 뚫는 피해는 PlayerObject 가 여기로 안 보냄
		private sealed class BarrageGuard : IDamageAbsorber
		{
			public bool TryAbsorb(DamageInfo damageInfo) => true;
		}

		/// <summary> 개발자 창. 게이지를 바로 채움 </summary>
		public void FillBarrage() => ChargeBarrage(barrageSize);

		private void ChargeBarrage(float amount)
		{
			if (barrageEnabled == false || barraging || barrageGauge >= barrageSize)
				return;
			barrageGauge = VolleyGaugeRules.Add(barrageGauge, amount, barrageSize);
			if (barrageGauge >= barrageSize)
				PopText(barrageReadyText);
		}

		// 누른 때 게이지가 차 있고 다른 포화가 안 나가는 중이면 시작
		private void OnBarrageRequested(PlayerBarrageRequestedEvent evt)
		{
			if (IsBarrageReady && firing == false && DungeonManagerBridge.IsDungeon)
				barrageRoutine = StartCoroutine(Barrage());
		}

		private IEnumerator Barrage()
		{
			barraging = true;
			firing = true;
			barrageGauge = 0f;
			barragePlayer = playerProvider.Current;
			Vector3 forward = AimDirection();
			barragePlayer.Object.AddAbsorber(barrageGuard);
			CameraManager.Instance.BeginSweepView(barrageView, forward);
			SetBackHead(true);
			arsenal.SetBackView(true);
			PopText(barrageText);
			int count = VolleyGaugeRules.ShotCount(baseWeapons, PlayerStat[bonusWeaponsStat], barrageShotsPerWeapon);
			arsenal.Raise(barragePlayer.transform, AimDirection, AimPoint, count, barrageFanDegrees);
			yield return new WaitForSeconds(arsenal.WindupSeconds);
			yield return Barrage(count);
			// 막타 빛줄기가 터질 때까지 등 뒤 시점을 지킴
			yield return new WaitForSeconds(arsenal.FinaleSeconds);
			FinishBarrage();
			lastFireTime = Time.time;
		}

		private IEnumerator Barrage(int count)
		{
			for (int i = 0; i < count; i++)
			{
				arsenal.Fire(i, SpawnShot(shotPrefab, VolleyGaugeRules.FanAngle(i * VolleyGaugeRules.SpreadStride(count) % count, count, barrageFanDegrees), barragePercent));
				yield return new WaitForSeconds(barrageSeconds / count);
			}
		}

		// 시점, 무적, 인물 그림을 되돌림. 두 번 불러도 됨
		private void FinishBarrage()
		{
			if (barraging == false)
				return;
			barraging = false;
			firing = false;
			if (barrageRoutine != null)
				StopCoroutine(barrageRoutine);
			barrageRoutine = null;
			if (barragePlayer != null)
			{
				barragePlayer.Object.RemoveAbsorber(barrageGuard);
				SetBackHead(false);
			}
			arsenal.SetBackView(false);
			if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
				cameraManager.EndSweepView();
			barragePlayer = null;
		}

		private void SetBackHead(bool back)
		{
			if (barrageBackHead == null)
				return;
			if (back)
				frontHead = barragePlayer.Object.SwapHead(barrageBackHead);
			else
				barragePlayer.Object.SwapHead(frontHead);
		}

		private void PopText(string text)
		{
			if (UIManager.TryGetExistingInstance(out UIManager uiManager))
				uiManager.PopText(text, TextType.Normal, playerProvider.Current.transform.position);
		}
	}
}
