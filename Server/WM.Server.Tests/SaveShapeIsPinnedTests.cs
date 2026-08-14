using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WitchMendokusai;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 기억에 적히는 <b>칸 이름</b>을 못에 박아 둔다 (TASK-WM-361).
	///
	/// ★ 왜: 칸 이름을 바꾸면 옛 기억의 그 값은 <b>조용히 사라진다</b> — JSON 은 모르는 이름을 그냥 버리고,
	///   5초 뒤 저장 루프가 그 빈 값을 원본 위에 덮는다. 컴파일도 되고 시험도 다 초록인 채로
	///   상자·가방·솥이 비는 길이다. 판 번호(WM-360)는 <b>되돌리기</b>를 막지 이름 바꾸기를 막지 못한다.
	///
	/// 그래서 규약을 둔다: <b>여기 적힌 이름은 못이다.</b>
	///   · 칸을 <b>더하는</b> 것 = 자유(옛 세계가 모르는 칸을 버려도 잃는 게 없다) — 이 시험은 안 막는다.
	///   · 칸을 <b>지우거나 이름을 바꾸는</b> 것 = 이 시험이 빨개진다. 그때 사람이 판단한다:
	///     정말 버릴 값이면 여기 목록에서 지우고 <b>판 번호를 올린다</b>(WorldStore.KNOWN_SAVE_VERSION).
	/// </summary>
	/// <remarks>
	/// [빨강-확인] `GatherTakenSaveEntry.remaining` 을 코드 전체에서 <b>한꺼번에</b> 이름만 바꿔 보니
	/// 컴파일은 멀쩡하고 다른 시험도 다 초록인데 이 시험만 빨갰다 —
	/// 「사라진 칸: GatherTakenSaveEntry.remaining」 (2026-08-14).
	/// (한 곳만 바꾸면 컴파일러가 잡는다. 무서운 것은 <b>깔끔하게</b> 바꾼 경우다.)
	/// </remarks>
	public sealed class SaveShapeIsPinnedTests
	{
		/// <summary>못 — 「꾸러미 이름 : 칸 이름들」. 지우거나 바꾸려면 판 번호를 올려라.</summary>
		private static readonly Dictionary<Type, string[]> PINNED = new Dictionary<Type, string[]>
		{
			[typeof(WorldSaveData)] = new[]
			{
				"saveVersion", "buildings", "year", "season", "day", "hour", "minute",
				"people", "identities", "gathered", "storages", "cauldrons",
			},
			[typeof(BuildingSaveData)] = new[] { "x", "y", "z", "w", "l", "buildingId" },
			[typeof(PersonSaveData)] = new[] { "identityId", "x", "z", "bag" },
			[typeof(BagSaveEntry)] = new[] { "itemId", "amount" },
			[typeof(StorageSaveEntry)] = new[] { "x", "y", "z", "items" },
			[typeof(GatherTakenSaveEntry)] = new[] { "nodeId", "regrowAtMinute", "remaining" },
			[typeof(CauldronSaveEntry)] = new[] { "x", "y", "z", "path" },
			[typeof(BrewStepSaveEntry)] = new[] { "dx", "dy", "grind" },

			// 신원 장부 — 여기 이름이 바뀌면 <b>사람이 남이 된다</b>(가방·이름·집을 잃는다).
			[typeof(Identity.WorldIdentityBook)] = new[] { "people", "nextId", "invites" },
			[typeof(Identity.WorldIdentityRecord)] = new[] { "id", "secret", "secretHash", "lastSeenDay", "name", "externalId" },
			[typeof(Identity.WorldLinkInvite)] = new[] { "code", "codeHash", "identityId" },
		};

		private static string[] FieldNamesOf(Type shape)
		{
			return shape.GetFields(BindingFlags.Public | BindingFlags.Instance)
				.Select((one) => one.Name)
				.ToArray();
		}

		[Test]
		public void 박아_둔_칸이_하나도_안_사라졌다()
		{
			List<string> gone = new List<string>();
			foreach (KeyValuePair<Type, string[]> pinned in PINNED)
			{
				string[] now = FieldNamesOf(pinned.Key);
				foreach (string name in pinned.Value)
				{
					if (now.Contains(name) == false)
						gone.Add($"{pinned.Key.Name}.{name}");
				}
			}

			Assert.That(gone, Is.Empty,
				"이 칸이 사라지거나 이름이 바뀌면 옛 기억의 그 값은 조용히 버려지고 곧 덮인다. "
				+ "정말 버릴 값이면 이 목록에서 지우고 WorldStore.KNOWN_SAVE_VERSION 을 올려라 — 사라진 칸: "
				+ string.Join(", ", gone));
		}

		/// <summary>
		/// 새로 생긴 칸은 <b>막지 않는다</b> — 다만 목록에 없다는 사실은 보이게 적는다.
		/// (「대상 0건 = 통과」를 막는 자리이기도 하다: 못이 실제로 그 꾸러미를 보고 있는지 확인한다.)
		/// </summary>
		[Test]
		public void 못이_실제로_그_꾸러미를_보고_있다()
		{
			foreach (KeyValuePair<Type, string[]> pinned in PINNED)
			{
				string[] now = FieldNamesOf(pinned.Key);
				Assert.That(now, Is.Not.Empty, $"{pinned.Key.Name} 에 공개 칸이 하나도 없다 — 못이 헛것을 보고 있다");

				string[] fresh = now.Where((one) => pinned.Value.Contains(one) == false).ToArray();
				if (fresh.Length > 0)
					TestContext.Out.WriteLine($"[못] {pinned.Key.Name} 에 새 칸: {string.Join(", ", fresh)} (더하는 것은 자유다)");
			}
		}
	}
}
