using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace WitchMendokusai
{
	// 한 프레임 안의 구간 비용을 Player.log 에 한 줄로 남긴다 (데모 조사 2026-11-27)
	// 멈춤 기록기 (HitchLog) 는 "어느 프레임이 길었나" 만 앎. 이 기록은 그 프레임 안에서 "어느 단계가 길었나" 를 가름
	//   첫 입장 694ms (전환 중 스테이지 적재와 판 초기화), 패배 순간 334ms (판 끝 저장과 결과창 열기) 가 대상
	// 쓰는 법: using (CostLog.Measure("EndDungeon.Save")) { ... }. 문턱을 넘은 구간만 찍어 평소 로그를 늘리지 않음
	// 읽는 법
	//   라벨은 점으로 부모와 자식. "EndDungeon" 은 "EndDungeon.Save" 를 품으므로 줄을 그냥 더하면 두 번 셈. 부모와 자식 합의 차가 재지 않은 단계
	//   frame 칸은 비용이 생긴 프레임. HitchLog 는 그 프레임의 길이를 다음 프레임에 찍고 frame 칸이 없음. 같은 멈춤의 두 줄은 시각 t 로 이음
	//   스크립트 구간만 잼. 렌더, 셰이더 첫 사용, UI Toolkit 레이아웃처럼 스크립트 밖 비용은 안 잡힘. [Hitch] 는 있는데 [Cost] 가 없으면 그쪽
	// 메인 스레드 전용 (Time 을 읽음). 스레드풀 안에서 쓰지 않음
	// 면제 조건은 HitchLog 와 같음 (에디터, batchmode, 결정 부팅, 무음 실행). 판정은 HitchLog 처럼 첫 씬 적재 뒤 한 번. 그 전 구간은 안 잼
	public readonly struct CostLog : IDisposable
	{
		private const string THRESHOLD_ENVIRONMENT_VARIABLE = "WM_COST_MS";
		// 기본 문턱. 144FPS 한 프레임 (약 7ms) 을 넘는 단계만 보려는 값. 구조체라 인스펙터가 없어 실행 때 환경 변수 WM_COST_MS 로 바꿈
		private const float DEFAULT_THRESHOLD_MILLISECONDS = 10f;

		private static bool isEnabled;
		private static float thresholdMilliseconds = DEFAULT_THRESHOLD_MILLISECONDS;

		private readonly string label;
		private readonly long startTimestamp;

		private CostLog(string label, long startTimestamp)
		{
			this.label = label;
			this.startTimestamp = startTimestamp;
		}

		public static CostLog Measure(string label)
		{
			if (label == null)
			{
				throw new ArgumentNullException(nameof(label));
			}
			return new CostLog(label, isEnabled ? Stopwatch.GetTimestamp() : 0);
		}

		public static bool ShouldLog(double milliseconds, float thresholdMilliseconds)
			=> milliseconds > thresholdMilliseconds;

		// 숫자가 아니거나 음수인 값은 버리고 기본 문턱. NaN 은 모든 구간을 찍음
		public static float ParseThreshold(string text)
			=> float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && float.IsNaN(parsed) == false && parsed >= 0f
				? parsed
				: DEFAULT_THRESHOLD_MILLISECONDS;

		public static string Format(string label, double milliseconds, float realtimeSeconds, int frameCount)
			=> string.Format(CultureInfo.InvariantCulture, "[Cost] {0} {1:F1}ms t={2:F1}s frame={3}", label, milliseconds, realtimeSeconds, frameCount);

		public static double ElapsedMilliseconds(long startTimestamp, long endTimestamp)
			=> (endTimestamp - startTimestamp) * 1000.0 / Stopwatch.Frequency;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Install()
		{
			isEnabled = HitchLog.IsExempt(Application.isEditor, Application.isBatchMode, BootMode.IsDeterministic, HeadlessAudioMute.IsSilentRun) == false;
			thresholdMilliseconds = ParseThreshold(Environment.GetEnvironmentVariable(THRESHOLD_ENVIRONMENT_VARIABLE));
		}

		public void Dispose()
		{
			// 면제 실행이거나 판정 전에 시작한 구간 (startTimestamp 0) 은 재지 않음
			if (isEnabled == false || startTimestamp == 0)
			{
				return;
			}
			double milliseconds = ElapsedMilliseconds(startTimestamp, Stopwatch.GetTimestamp());
			if (ShouldLog(milliseconds, thresholdMilliseconds) == false)
			{
				return;
			}
			string line = Format(label, milliseconds, Time.realtimeSinceStartup, Time.frameCount);
			Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", line);
		}
	}
}
