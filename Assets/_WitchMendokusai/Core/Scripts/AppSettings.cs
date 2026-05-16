using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(AppSettings), menuName = "WM/AppSettings")]
	public class AppSettings : ScriptableObject
	{
		[field: Header("_" + nameof(AppSettings))]
		[field: SerializeField] public bool UseLocalData { get; private set; } = true;
		[field: SerializeField] public bool InitDataSODict { get; private set; } = true;
		[field: SerializeField] public bool UseIntro { get; private set; } = true;
		[field: SerializeField] public bool AutoStart { get; private set; } = false;

		// TASK-WM-118 B3 — 결정적/헤드리스 부팅: 비결정 3요인 중 SO 플래그 2개
		// (UseIntro/AutoStart) + UseLocalData 를 결정 분기로 고정. 런타임 인스턴스
		// (Resources.Load) 한정 변경 — 디스크 .asset 불변. private set 이라 본 메서드만.
		public void ApplyDeterministicBoot()
		{
			UseIntro = false;     // 타이머 패널 skip
			AutoStart = true;     // 수동 버튼 없이 자동 StartGame
			UseLocalData = true;  // 로컬 세이브 (PlayFab Login 은 DataManager.Login 가드)
		}
	}

	public static class AppSetting
	{
		public static AppSettings Data { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void OnBooting()
		{
			Debug.Log("Booting AppSettings...");

			Data = Resources.Load<AppSettings>(nameof(AppSettings));
			if (Data == null)
			{
				Debug.LogError($"{nameof(AppSettings)} not found");
				return;
			}

			// TASK-WM-118 B3 — 결정적 부팅 모드면 비결정 플래그 고정 (런타임 인스턴스 한정).
			if (BootMode.IsDeterministic)
			{
				Data.ApplyDeterministicBoot();
				Debug.Log("[BOOT] AppSettings — ApplyDeterministicBoot (UseIntro=false, AutoStart=true, UseLocalData=true)");
			}
		}
	}
}