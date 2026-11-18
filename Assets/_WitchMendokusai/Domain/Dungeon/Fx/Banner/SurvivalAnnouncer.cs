using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 생존 모드 사건을 화면 띠로 알림. 정예와 무리의 예고와 시작, 보스 예고와 등장
	/// - 던전이 있는 씬에서 SceneLifetimeScope 가 만듦 (SurvivalFeel 과 같은 길)
	/// - 어느 띠에 어떤 글인지는 ResourceManager.SurvivalAnnounce 에셋
	/// </summary>
	public class SurvivalAnnouncer : MonoBehaviour
	{
		private ResourceManager resourceManager;
		// 띠 에셋별 마지막 재생의 중단 함수
		private readonly Dictionary<CombatFx, Action> stops = new();

		private SurvivalAnnounceSettings Settings => resourceManager.SurvivalAnnounce;

		[Inject]
		public void Construct(ResourceManager resourceManager)
		{
			this.resourceManager = resourceManager;
			EventBusBridge.Subscribe<SurvivalEventAnnouncedEvent>(OnEventAnnounced);
			EventBusBridge.Subscribe<SurvivalEventStartedEvent>(OnEventStarted);
			EventBusBridge.Subscribe<BossAnnouncedEvent>(OnBossAnnounced);
			EventBusBridge.Subscribe<BossSpawnedEvent>(OnBossSpawned);
		}

		private void OnDestroy()
		{
			EventBusBridge.Unsubscribe<SurvivalEventAnnouncedEvent>(OnEventAnnounced);
			EventBusBridge.Unsubscribe<SurvivalEventStartedEvent>(OnEventStarted);
			EventBusBridge.Unsubscribe<BossAnnouncedEvent>(OnBossAnnounced);
			EventBusBridge.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
			// 씬을 떠날 때 떠 있던 띠를 치움. 끝난 띠의 중단 함수는 불러도 무해
			foreach (Action stop in stops.Values)
				stop.Invoke();
			stops.Clear();
		}

		private void OnEventAnnounced(SurvivalEventAnnouncedEvent announced)
		{
			SurvivalAnnounceLine line = announced.Type == SurvivalEventType.Elite ? Settings.EliteAnnounced : Settings.SwarmAnnounced;
			Show(line, announced.LeadSeconds, announced.Monster, announced.Count);
		}

		private void OnEventStarted(SurvivalEventStartedEvent started)
		{
			// 적 수 상한에 막혀 한 마리도 못 나온 사건
			if (started.Count <= 0)
				return;
			SurvivalAnnounceLine line = started.Type == SurvivalEventType.Elite ? Settings.EliteStarted : Settings.SwarmStarted;
			Show(line, 0f, started.Monster, started.Count);
		}

		private void OnBossAnnounced(BossAnnouncedEvent announced) => Show(Settings.BossAnnounced, announced.LeadSeconds, announced.Monster, 1);

		private void OnBossSpawned(BossSpawnedEvent spawned) => Show(Settings.BossSpawned, 0f, spawned.Monster, 1);

		private void Show(SurvivalAnnounceLine line, float leadSeconds, Monster monster, int count)
		{
			if (line.Banner == null)
				return;

			int seconds = Mathf.CeilToInt(leadSeconds);
			string monsterName = monster.Name;
			stops[line.Banner] = line.Banner.Play(new CombatFxContext
			{
				Title = string.Format(line.Title, seconds, monsterName, count),
				Subtitle = string.Format(line.Subtitle, seconds, monsterName, count),
				Seconds = line.HoldSeconds,
			});
		}
	}
}
