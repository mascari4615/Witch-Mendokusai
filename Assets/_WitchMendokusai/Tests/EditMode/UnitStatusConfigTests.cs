using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	// 배포되는 상태이상 설정 (UnitStatusConfig 에셋) 과 진열장이 코드의 종류 목록과 맞는지 잠금
	// - 종류 추가는 enum, 규칙 목록, 설정 줄, 진열 칸 네 곳. 하나라도 빠지면 런타임에 줄 없음 예외나 안 보이는 아이콘
	public sealed class UnitStatusConfigTests
	{
		private static UnitStatusConfig LoadConfig()
		{
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(UnitStatusConfig));
			Assert.AreEqual(1, guids.Length, "UnitStatusConfig 에셋은 하나");
			return AssetDatabase.LoadAssetAtPath<UnitStatusConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
		}

		[Test]
		public void EveryStatusHasOneStyleWithIcon()
		{
			UnitStatusConfig config = LoadConfig();
			foreach (UnitStatusType type in UnitStatusRules.ALL)
			{
				int rows = 0;
				foreach (UnitStatusStyle style in config.Styles)
				{
					if (style.Type != type)
						continue;
					rows++;
					Assert.IsNotNull(style.Icon, $"{type} 아이콘 없음");
					Assert.Greater(style.Seconds, 0f, $"{type} 기본 지속 0");
				}
				Assert.AreEqual(1, rows, $"{type} 줄 수");
			}
			Assert.AreEqual(UnitStatusRules.Count, config.Styles.Count, "설정에 종류 목록에 없는 줄이 있음");
		}

		[Test]
		public void EveryStatusHasDistinctIconAndColor()
		{
			UnitStatusConfig config = LoadConfig();
			HashSet<UnityEngine.Sprite> icons = new();
			List<UnityEngine.Color> colors = new();
			foreach (UnitStatusStyle style in config.Styles)
			{
				Assert.IsTrue(icons.Add(style.Icon), $"{style.Type} 아이콘이 다른 종류와 같음");
				foreach (UnityEngine.Color other in colors)
				{
					float distance = UnityEngine.Mathf.Abs(style.IconColor.r - other.r) + UnityEngine.Mathf.Abs(style.IconColor.g - other.g) + UnityEngine.Mathf.Abs(style.IconColor.b - other.b);
					Assert.Greater(distance, 0.25f, $"{style.Type} 색이 다른 종류와 거의 같음");
				}
				colors.Add(style.IconColor);
			}
		}

		[Test]
		public void MechanicsDifferByType()
		{
			UnitStatusConfig config = LoadConfig();
			Assert.Greater(config.Style(UnitStatusType.Bleed).MaxStacks, 1, "출혈은 쌓임");
			Assert.Greater(config.Style(UnitStatusType.Bleed).TickSeconds, 0f);
			Assert.Greater(config.Style(UnitStatusType.Shock).ChainCount, 0, "감전은 튐");
			Assert.Greater(config.Style(UnitStatusType.Shock).TickSeconds, 0f);
			Assert.Greater(config.Style(UnitStatusType.Mark).DamageTakenPercent, 0f, "표식은 받는 피해 증가");
			Assert.IsTrue(config.Style(UnitStatusType.Fear).FleeFromPlayer, "공포는 도주");
			Assert.IsTrue(config.Style(UnitStatusType.Silence).BlockSkills, "침묵은 스킬 봉쇄");
			Assert.IsTrue(config.Style(UnitStatusType.Freeze).StopAnimation);
			Assert.Greater(config.Style(UnitStatusType.Slow).SlowRatio, 0f);
			Assert.Greater(config.Style(UnitStatusType.Stun).OrbitStars, 0, "기절은 머리 위 3D 별 궤도");
			Assert.IsNotNull(config.OrbitMaterial, "3D 궤도 재질");
		}

		// 사용자 판정 2026-11-20: 감전은 노랑, 기절 대표색은 연보라, 독은 보라
		[Test]
		public void StatusColorsFollowUserDecision()
		{
			UnitStatusConfig config = LoadConfig();
			UnityEngine.Color shock = config.Style(UnitStatusType.Shock).IconColor;
			Assert.IsTrue(shock.r > 0.9f && shock.g > 0.85f && shock.b < 0.4f, "감전은 노랑");
			UnityEngine.Color stun = config.Style(UnitStatusType.Stun).IconColor;
			Assert.IsTrue(stun.b > stun.r && stun.r > stun.g && stun.g > 0.5f, "기절은 연보라");
			UnityEngine.Color poison = config.Style(UnitStatusType.Poison).IconColor;
			Assert.IsTrue(poison.b > poison.g && poison.r > poison.g && poison.g < 0.5f, "독은 보라");
		}

		[Test]
		public void CellSheetsMatchFrameCounts()
		{
			UnitStatusConfig config = LoadConfig();
			Assert.IsNotNull(config.CellBack, "칸 그림");
			Assert.IsNotNull(config.RingSheet, "테두리 묶음");
			Assert.IsNotNull(config.DigitSheet, "숫자 묶음");
			int rows = (config.RingFrames + config.RingColumns - 1) / config.RingColumns;
			Assert.AreEqual(config.RingSheet.width / config.RingColumns * rows, config.RingSheet.height, "테두리 묶음 행 수");
			Assert.AreEqual(config.DigitSheet.height * 10, config.DigitSheet.width, "숫자는 0부터 9, 가로 한 줄");
		}

		[Test]
		public void ShowcaseHasCellForEveryStatus()
		{
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(DevShowcaseSO));
			Assert.IsNotEmpty(guids);
			foreach (string guid in guids)
			{
				DevShowcaseSO showcase = AssetDatabase.LoadAssetAtPath<DevShowcaseSO>(AssetDatabase.GUIDToAssetPath(guid));
				if (showcase.StatusCells.Count == 0)
					continue;
				foreach (UnitStatusType type in UnitStatusRules.ALL)
				{
					bool single = false;
					foreach (DevShowcaseStatusCell cell in showcase.StatusCells)
						single |= cell.Statuses.Count == 1 && cell.Statuses[0] == type;
					Assert.IsTrue(single, $"{showcase.name} 에 {type} 한 종류 칸 없음");
				}
			}
		}
	}
}
