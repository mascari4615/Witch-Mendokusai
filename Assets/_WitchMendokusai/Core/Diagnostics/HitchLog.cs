using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	// 긴 프레임 한 장마다 Player.log 에 한 줄을 남긴다 (데모 조사 2026-11-23, 판 도중 100ms 넘는 멈춤의 원인 미확인)
	// 가설 가르기용 칸
	//   timeScale: 걸린 프레임 동안의 값 (앞 프레임 맨 앞에서 읽어 둔 것) 과 지금 값. 0 이면 카드 창 같은 정지 창이 떠 있던 프레임
	//   gc: 걸린 구간에 끝난 GC 주기 수. 증분 GC 가 켜져 있어 (gcIncremental) 조각만 돈 프레임은 +0 일 수 있음. 앞뒤 줄과 같이 읽음
	//   heap: 관리 힙 사용량 순변화 (GC.GetTotalMemory, 성능 시나리오와 같은 지표). 할당량이 아님. GC 가 돌면 회수분이 빠짐
	//   loads: 걸린 구간에 끝난 씬 적재 수. 1 이상이면 씬 적재 멈춤
	// 로그 줄 순서도 단서. 바로 앞의 "[TimeManager] Paused by", "Resumed by" 줄과 같이 읽음
	// 사람이 하는 판만 잼. 에디터 (도메인 리로드, 가져오기가 섞임), batchmode, 결정 부팅 (성능 시나리오, 부팅 스모크), 무음 실행은 면제
	// 로그 비용이 측정 대상 프레임에 얹히지 않게 스택 추적 없이 찍음
	[DefaultExecutionOrder(EXECUTION_ORDER)]
	public class HitchLog : MonoBehaviour
	{
		// 다른 스크립트보다 먼저 돌아 프레임 맨 앞에서 재고 기준점을 다시 잡음
		private const int EXECUTION_ORDER = -32000;
		private const string THRESHOLD_ENVIRONMENT_VARIABLE = "WM_HITCH_MS";

		// 기본 문턱. 실행 때 환경 변수 WM_HITCH_MS 로 바꿈 (코드로 붙는 컴포넌트라 인스펙터가 없음)
		[SerializeField] private float thresholdMilliseconds = 100f;

		private int lastCollectionCount;
		private long lastHeapBytes;
		private float lastTimeScale;
		private int sceneLoadCount;
		private int lastSceneLoadCount;
		private int hitchCount;

		public static bool IsHitch(float frameMilliseconds, float thresholdMilliseconds)
			=> frameMilliseconds > thresholdMilliseconds;

		public static bool IsExempt(bool isEditor, bool isBatchMode, bool isDeterministic, bool isSilentRun)
			=> isEditor || isBatchMode || isDeterministic || isSilentRun;

		public static string Format(int index, float frameMilliseconds, float realtimeSeconds, string sceneName, float frameTimeScale, float currentTimeScale, int collections, long heapDeltaBytes, int sceneLoads)
			=> string.Format(
				CultureInfo.InvariantCulture,
				"[Hitch] #{0} {1:F0}ms t={2:F1}s scene={3} timeScale={4:F2}>{5:F2} gc=+{6} heap={7:+0;-0;0}KB loads=+{8}",
				index, frameMilliseconds, realtimeSeconds, sceneName, frameTimeScale, currentTimeScale, collections, heapDeltaBytes / 1024, sceneLoads);

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Install()
		{
			if (IsExempt(Application.isEditor, Application.isBatchMode, BootMode.IsDeterministic, HeadlessAudioMute.IsSilentRun))
			{
				return;
			}
			GameObject host = new(nameof(HitchLog));
			DontDestroyOnLoad(host);
			host.AddComponent<HitchLog>();
		}

		private void Awake()
		{
			string overrideText = Environment.GetEnvironmentVariable(THRESHOLD_ENVIRONMENT_VARIABLE);
			if (float.TryParse(overrideText, NumberStyles.Float, CultureInfo.InvariantCulture, out float overrideMilliseconds))
			{
				thresholdMilliseconds = overrideMilliseconds;
			}
			SceneManager.sceneLoaded += OnSceneLoaded;
			Sample();
		}

		private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => sceneLoadCount++;

		// unscaledDeltaTime 은 앞 프레임 맨 앞부터 이번 프레임 맨 앞까지. 기준점도 같은 자리에서 잡아 두 구간을 맞춤
		private void Update()
		{
			float frameMilliseconds = Time.unscaledDeltaTime * 1000f;
			if (IsHitch(frameMilliseconds, thresholdMilliseconds))
			{
				hitchCount++;
				string line = Format(
					hitchCount,
					frameMilliseconds,
					Time.realtimeSinceStartup,
					SceneManager.GetActiveScene().name,
					lastTimeScale,
					Time.timeScale,
					GC.CollectionCount(0) - lastCollectionCount,
					GC.GetTotalMemory(false) - lastHeapBytes,
					sceneLoadCount - lastSceneLoadCount);
				Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", line);
			}
			Sample();
		}

		private void Sample()
		{
			// Unity GC 는 세대가 없어 0 세대 횟수가 곧 전체 주기 수
			lastCollectionCount = GC.CollectionCount(0);
			lastHeapBytes = GC.GetTotalMemory(false);
			lastTimeScale = Time.timeScale;
			lastSceneLoadCount = sceneLoadCount;
		}
	}
}
