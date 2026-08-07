using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Care;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-171 — <b>시연용 더미</b>. 소원 하나를 걸어 두고 날마다 조금씩 채워, 채워지는 순간이
	/// 콘솔에 뜨게 한다. 재료만도 아니고 곁에 있어주기만도 아니라 <b>둘 다 채워야 끝난다</b>는 걸 보여준다.
	///
	/// ★ 로어 아니다. 누가 무엇을 바라는지·떠날지 머물지는 <b>사용자가 정할 문제</b>다.
	///   여기 「꽃 3송이 + 곁에 있어주기」는 메커니즘 자리표고, 토글 하나로 통째로 꺼진다.
	/// </summary>
	public class CareDemoWish : MonoBehaviour
	{
		[SerializeField] private string wishId = "더미-소원";
		[SerializeField] private string materialId = "꽃";
		[SerializeField] private int materialNeeded = 3;
		[SerializeField] private string satisfactionChannel = "곁에 있어주기";

		[Tooltip("하루가 지날 때마다 채워지는 정도(0~1). 0.34 면 사흘이면 찬다.")]
		[SerializeField] private float satisfactionPerDay = 0.34f;

		[Tooltip("날마다 콘솔에 남길지. 꺼 두면 돌아도 눈에 안 보인다.")]
		[SerializeField] private bool logEachDay = true;

		private WishKeeper keeper;
		private WorldClock worldClock;
		private float satisfaction;
		private bool finished;

		// init-order-ok: 세계 시계는 씬 정적 배치라 Start 시점 존재. 없으면 조용히 쉰다(다른 감독들과 같은 폴백).
		private void Start()
		{
			keeper = GetComponent<WishKeeper>();
			if (keeper == null)
			{
				return;
			}

			keeper.AddWish(new WishSpec(
				wishId,
				WishKind.Companionship,
				new List<WishMaterialReq> { new WishMaterialReq(materialId, materialNeeded) },
				new Dictionary<string, float> { { satisfactionChannel, 1f } },
				WishOutcome.Settle));

			keeper.OnWishResolved += OnWishResolved;

			if (WorldClock.TryGetExistingInstance(out worldClock) == false)
			{
				return;
			}

			worldClock.OnDayChanged += OnDayChanged;
		}

		private void OnDestroy()
		{
			if (worldClock != null)
			{
				worldClock.OnDayChanged -= OnDayChanged;
			}

			if (keeper != null)
			{
				keeper.OnWishResolved -= OnWishResolved;
			}
		}

		private void OnDayChanged(int day)
		{
			if (finished == true)
			{
				return;
			}

			keeper.Contribute(wishId, materialId, 1);

			satisfaction = Mathf.Clamp01(satisfaction + satisfactionPerDay);
			keeper.Satisfy(wishId, satisfactionChannel, satisfaction);

			if (logEachDay == true && finished == false)
			{
				WishProgress progress = keeper.ProgressOf(wishId);
				int collected = progress == null ? materialNeeded : Collected(progress);
				Debug.Log($"[돌봄] {wishId} — {materialId} {collected}/{materialNeeded},"
					+ $" {satisfactionChannel} {satisfaction:0.00}/1.00");
			}
		}

		private int Collected(WishProgress progress)
		{
			return progress.CollectedMaterials.TryGetValue(materialId, out int count) == true ? count : 0;
		}

		private void OnWishResolved(string id, WishOutcome outcome)
		{
			if (id != wishId)
			{
				return;
			}

			finished = true;
			string ending = outcome == WishOutcome.Settle ? "마을에 자리잡았다" : "평온히 떠났다";
			Debug.Log($"[돌봄] {wishId} 이 채워졌다 — {ending}. (재료도 곁에 있어주기도 둘 다 찼을 때만 끝난다)");
		}
	}
}
