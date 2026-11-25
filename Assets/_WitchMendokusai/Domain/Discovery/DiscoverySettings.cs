using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 화면의 모양과 몬스터 기록의 단계 목표. 색, 간격, 글자 크기는 USS.
	/// 무대 크기, 칸 이동 수치, 판과 키 칸은 ESC 메뉴 (Menu), 카드와 분류 판과 달성 띠는 업적 (Cards) 과 같은 것
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_Discovery", menuName = "WM/UI/" + nameof(DiscoverySettings))]
	public class DiscoverySettings : ScriptableObject
	{
		[field: Header("모양")]
		[field: SerializeField] public StyleSheet Style { get; private set; }
		[field: SerializeField, Tooltip("업적 USS. 카드, 분류 판, 거르기, 단계 마름모, 막대, 달성 띠를 같이 씀")] public StyleSheet Cards { get; private set; }
		[field: SerializeField, Tooltip("ESC 메뉴 설정. 무대 기준 크기, 칸 이동 수치, FullMenu.uss 를 같이 씀")] public FullMenuSettings Menu { get; private set; }
		[field: SerializeField, Tooltip("몬스터 분류 아이콘")] public Sprite MonsterIcon { get; private set; }
		[field: SerializeField, Tooltip("아이콘이 없는 분류")] public Sprite OtherIcon { get; private set; }

		[field: Header("몬스터 기록 단계")]
		[SerializeField, Tooltip("일반 몬스터. 칸마다 필요한 누적 처치 수. 칸 순서는 첫 만남, 사는 곳, 몸과 버릇, 전리품, 이야기. 임시 값")] private int[] normalTargets = { 1, 20, 100, 300, 1000 };
		[SerializeField, Tooltip("보스. 임시 값")] private int[] bossTargets = { 1, 2, 4, 7, 10 };

		public IReadOnlyList<int> NormalTargets => normalTargets;
		public IReadOnlyList<int> BossTargets => bossTargets;

		/// <summary>목표 표가 칸 수와 맞고 오름차순인지. 틀리면 던짐</summary>
		public void ValidateTargets()
		{
			ValidateTargets(nameof(normalTargets), normalTargets);
			ValidateTargets(nameof(bossTargets), bossTargets);
		}

		private void ValidateTargets(string label, int[] targets)
		{
			DiscoveryStages.Validate($"{name} {label}", targets);
			if (targets.Length != MonsterLogPage.SLOT_COUNT)
			{
				throw new ArgumentException($"{name} {label}: 칸은 {MonsterLogPage.SLOT_COUNT}개, 목표는 {targets.Length}개");
			}
		}
	}
}
