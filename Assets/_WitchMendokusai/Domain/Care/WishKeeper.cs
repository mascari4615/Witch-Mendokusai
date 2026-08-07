using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Care;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-171 — 돌봄·배웅(소원)을 게임에 붙이는 첫 배선.
	///
	/// ★ 왜 이 파일이 생겼나: 소원 계산층은 만들어진 뒤로 <b>게임 어디서도 안 불렸다.</b>
	///   아무도 안 부르는 층은 지워져도 컴파일이 안 깨져 조용히 사라진다 — 2026-08-07 에 실제로 그랬다.
	///
	/// 여기는 <b>누가 무엇을 바라는지 들고 있다가, 채워지면 알려 주는 자리</b>다.
	/// 결말을 *수행* 하지 않는다 — 「떠난다/머문다」를 화면에서 어떻게 보여줄지는 듣는 쪽이 정한다.
	/// </summary>
	public class WishKeeper : MonoBehaviour
	{
		private readonly List<WishSpec> specs = new List<WishSpec>();
		private readonly Dictionary<string, WishProgress> progresses = new Dictionary<string, WishProgress>();

		/// <summary>소원이 채워진 순간 한 번 — (소원 id, 결말). 결말은 소원 데이터에 적힌 대로다.</summary>
		public event Action<string, WishOutcome> OnWishResolved = delegate { };

		/// <summary>아직 안 채워진 소원 수.</summary>
		public int PendingCount => specs.Count;

		/// <summary>소원 하나 받기. 같은 id 가 이미 있으면 무시한다.</summary>
		public void AddWish(WishSpec spec)
		{
			if (spec == null || progresses.ContainsKey(spec.Id) == true)
			{
				return;
			}

			specs.Add(spec);
			progresses[spec.Id] = new WishProgress();
		}

		/// <summary>그 소원이 지금 어디까지 왔나. 없는 소원이면 null.</summary>
		public WishProgress ProgressOf(string wishId)
		{
			return progresses.TryGetValue(wishId, out WishProgress progress) == true ? progress : null;
		}

		/// <summary>재료를 가져다 주기.</summary>
		public void Contribute(string wishId, string itemId, int count)
		{
			WishProgress progress = ProgressOf(wishId);
			if (progress == null)
			{
				return;
			}

			progress.AddMaterial(itemId, count);
			CheckResolved(wishId);
		}

		/// <summary>곁에 있어주기 같은 「채워짐」을 0~1 로 직접 박기.</summary>
		public void Satisfy(string wishId, string channel, float value)
		{
			WishProgress progress = ProgressOf(wishId);
			if (progress == null)
			{
				return;
			}

			progress.SetSatisfaction(channel, value);
			CheckResolved(wishId);
		}

		private void CheckResolved(string wishId)
		{
			for (int index = 0; index < specs.Count; index++)
			{
				WishSpec spec = specs[index];
				if (spec.Id != wishId)
				{
					continue;
				}

				if (WishResolver.TryResolve(spec, progresses[wishId], out WishOutcome outcome) == false)
				{
					return;
				}

				// 채워진 소원은 목록에서 뺀다 — 같은 소원이 두 번 배웅되면 안 된다.
				specs.RemoveAt(index);
				OnWishResolved.Invoke(wishId, outcome);
				return;
			}
		}
	}
}
