using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	// TowerDefensePlayVerify 의 소리 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		private static bool noiseArmed;

		/// <summary>
		/// 소리가 자는 것을 깨우는가 — 「멀찍이서 조용히」와 「옆에서 시끄럽게」가 실제로 다른가.
		///
		/// ★ 기다리지 않는다(적응에서 다섯 사이클을 그렇게 흘렸다). 아직 자고 있는 서식지를 골라
		///   그 옆에서 *일부러 소리를 낸다*. 소리는 짓기·부서짐과 같은 문으로 들어가므로
		///   검사만 통과하는 길이 아니다.
		/// ★ 거리로 깨어난 것과 구별해야 한다 — 그래서 서식지에서 **깨우는 거리 밖**에 소리를 낸다.
		///   그 자리에서 깨어나면 그건 소리 때문이다.
		/// </summary>
		private static void ArmNoiseProbe()
		{
			if (noiseArmed || match == null || match.SurvivedSeconds < 8f)
				return;

			Vector3 target = Vector3.zero;
			bool found = false;
			foreach (TowerDefenseMatch.LairMarker marker in match.LairMarkers)
			{
				if (marker.Awake)
					continue;
				target = marker.Position;
				found = true;
				break;
			}

			if (found == false)
			{
				noiseArmed = true;
				Debug.Log(TAG + " 소리 — 못 쟀다: 아직 자는 서식지가 없다. 실패가 아니다.");
				return;
			}

			noiseArmed = true;
			int before = match.LairsAwakened;
			float heardBefore = match.NoiseHeardAt(target);

			// ★ 소리를 한 프레임에 몰아서 내면 경고 구간(문턱의 60%)을 *건너뛴다* — 미리 알림이
			//   도는지 영영 못 잰다(두 판을 그렇게 「못 쟀다」로 끝냈다). 사람이 쏘거나 짓는 것처럼
			//   조금씩 나눠 내서 소리가 *차오르게* 한다.
			// 깨우는 거리 밖에서 낸다 — 깨어나면 그건 거리가 아니라 소리 때문이다.
			noiseRampSpot = target + new Vector3(1f, 0f, 0f) * (match.LairWakeRadius + 4f);
			noiseRampTarget = target;
			noiseRampLeft = NOISE_RAMP_STEPS;
			match.EmitNoise(noiseRampSpot, NOISE_RAMP_AMOUNT);

			float heardAfter = match.NoiseHeardAt(target);
			Debug.Log($"{TAG} 소리 — 자는 서식지 옆(깨우는 거리 밖)에서 크게 냈다 · 들리는 크기 {heardBefore:F1} → {heardAfter:F1}"
				+ $" · 문턱 {match.NoiseWakeThreshold:F1} · 깨어난 곳 {before}");

			if (heardAfter <= 0f)
			{
				Debug.LogWarning(TAG + " 소리 FAIL — 바로 옆에서 냈는데 아무것도 안 들린다(소리가 판에 안 닿는다).");
				return;
			}

			// ★ 「문턱을 넘었다」는 계산일 뿐이다. 실제로 깨어나는지는 다음 틱에 판이 정한다 —
			//   같은 프레임에 물으면 늘 「안 깼다」다. 그 자리를 기억해 두고 잠시 뒤 확인한다.
			noiseTarget = target;
			noiseAwakenedBefore = before;
			noiseCheckAt = EditorApplication.timeSinceStartup + 2.0;
			noiseMatch = match;
		}

		private static bool resumeVerified;
		private static bool noiseAlertSeen;

		/// <summary> 한 번에 몰아내지 않고 조금씩 올린다 — 경고 구간을 지나가야 미리 알림을 잰다. </summary>
		private const int NOISE_RAMP_STEPS = 40;
		private const float NOISE_RAMP_AMOUNT = 1.2f;
		private static int noiseRampLeft;
		private static Vector3 noiseRampSpot;
		private static Vector3 noiseRampTarget;

		private static void TickNoiseRamp()
		{
			if (noiseRampLeft <= 0 || match == null)
				return;

			noiseRampLeft--;
			match.EmitNoise(noiseRampSpot, NOISE_RAMP_AMOUNT);

			if (match.NoiseWarnings > 0 && noiseWarnSeenAt <= 0.0)
			{
				noiseWarnSeenAt = EditorApplication.timeSinceStartup;
				Debug.Log(TAG + " 소리 — 미리 알림이 떴다(들리는 크기 "
					+ match.NoiseHeardAt(noiseRampTarget).ToString("F1")
					+ " · 문턱 " + match.NoiseWakeThreshold.ToString("F1") + ")");
			}
		}

		private static double noiseWarnSeenAt;
		private static double noiseSustainAt;
		private static float noiseLoudestFirst;

		/// <summary>
		/// 소음이 *유지되는가* — 짓는 소리는 한 번 나고 잦아들지만 쏘는 소리는 쏘는 동안 계속 난다.
		/// 이걸 안 가르면 「사격 소음이 도는지」를 영영 모른다(한 번 재서 크면 짓느라 난 것일 수 있다).
		/// </summary>
		private static void CheckNoiseSustained()
		{
			if (match == null)
			{
				Debug.Log(TAG + " 소리 유지 — 못 쟀다: 판이 사라졌다. 실패가 아니다.");
				return;
			}

			float now = match.LoudestNoise;
			Debug.Log($"{TAG} 소리 유지 — 쏜 것을 알린 횟수 {match.ShotsReported}");
			Debug.Log($"{TAG} 소리 유지 — 12초 전 {noiseLoudestFirst:F1} → 지금 {now:F1}"
				+ $" (아무것도 안 나면 이 사이에 거의 0 이 된다 · 잦아드는 비율로 계산하면 {noiseLoudestFirst * 0.0057f:F2} 쯤)");

			if (now >= 1f)
				Debug.Log(TAG + " 소리 유지 — 계속 나고 있다 = 쏘는 소리가 살아 있다.");
			else
				Debug.Log(TAG + " 소리 유지 — 못 쟀다: 이 사이에 아무도 안 쐈다(잦아든 것과 구별 불가). 실패가 아니다.");
		}


		/// <summary> 「소리를 듣고 깨어났다」가 뜨는 *순간*을 잡는다 — 나중에 물으면 밀려난 뒤다. </summary>
		private static void WatchNoiseAlert()
		{
			if (noiseAlertSeen || match == null)
				return;

			foreach (TowerDefenseAlerts.Alert alert in match.Alerts)
			{
				if (alert.Label.Contains("소리를 듣고") == false)
					continue;
				noiseAlertSeen = true;
				Debug.Log(TAG + " 소리 — 알림: 「" + alert.Label + "」");
				return;
			}
		}

		private static double noiseCheckAt;
		private static Vector3 noiseTarget;
		private static int noiseAwakenedBefore;
		private static TowerDefenseMatch noiseMatch;

		/// <summary> 그 서식지가 *소리 때문에* 깨어났는가 — 깨우는 거리 밖에서 냈으니 거리는 아니다. </summary>
		private static void CheckNoiseWake()
		{
			if (match == null || match != noiseMatch)
			{
				Debug.Log(TAG + " 소리 — 못 쟀다: 재는 중에 판이 새로 시작됐다. 실패가 아니다.");
				return;
			}

			bool awake = false;
			foreach (TowerDefenseMatch.LairMarker marker in match.LairMarkers)
			{
				if ((marker.Position - noiseTarget).sqrMagnitude > 1f)
					continue;
				awake = marker.Awake;
				break;
			}

			bool spoken = noiseAlertSeen;

			// ★ 깨어난 뒤 통보만 하면 대응할 기회가 0 이다. 문턱에 다가가는 동안 먼저 말했는지 잰다 —
			//   경고가 0 인데 소리로 깼다면, 그 규칙은 「피할 수 있는 위협」이 아니라 그냥 벌이다.
			// ★ 미리 알림은 소리가 *차오를 때* 뜻이 있다. 이 탐침은 한 프레임에 몰아서 내므로
			//   경고 구간을 건너뛴다 — 그때 0 은 「경고가 고장」이 아니라 「이 방법으론 못 잰다」다.
			if (match.NoiseWarnings == 0)
				Debug.Log(TAG + " 소리 — 미리 알림은 못 쟀다: 탐침이 한 번에 몰아서 내 경고 구간을 건너뛴다. 실패가 아니다.");
			else
				Debug.Log(TAG + " 소리 — 깨기 전 미리 알린 횟수 " + match.NoiseWarnings);

			int byNoise = match.LairsAwakenedByNoise;
			// ★ 소리의 절반은 *사격*이다(짓기·부서짐만 있으면 「둥지 옆에서 난사해도 조용하다」가 된다).
			//   쏘는 소리가 실제로 판에 쌓이는지는 따로 재야 한다 — 안 그러면 그 절반은 죽은 규칙이다.
			// ★ 한 번만 재면 「짓느라 난 소리가 아직 안 잦아든 것」과 구별이 안 된다. 짓는 소리는
			//   한 번 나고 끝이라 몇 초면 사그라들지만, 사격 소리는 쏘는 동안 계속 다시 난다.
			//   그래서 시간을 두고 두 번 재서 *유지되는지*를 본다 — 유지되면 그건 쏘는 소리다.
			noiseSustainAt = EditorApplication.timeSinceStartup + 12.0;
			noiseLoudestFirst = match.LoudestNoise;
			Debug.Log(TAG + " 소리 — 지금 판에서 가장 시끄러운 곳 " + noiseLoudestFirst.ToString("F1")
				+ " (한 발 " + match.NoiseFromShotForVerification.ToString("F2")
				+ ") · 12초 뒤 다시 재서 유지되는지 본다");

			Debug.Log($"{TAG} 소리 결과 — 그 서식지 깨어남 {awake} · 깨어난 곳 {noiseAwakenedBefore} → {match.LairsAwakened}"
				+ $" · 그중 소리만으로 {byNoise} · 「소리를 듣고」 알림 {spoken}");

			if (awake == false)
				Debug.LogWarning(TAG + " 소리 FAIL — 문턱을 넘게 들렸는데 서식지가 안 깬다(계산만 돌고 판은 안 움직인다).");
			else if (byNoise == 0)
				Debug.Log(TAG + " 소리 — 못 쟀다: 그 서식지는 거리로도 깰 자리였다(소리만의 몫을 못 가른다). 실패가 아니다.");
			else if (spoken == false && match.Alerts.Count >= TowerDefenseAlerts.MAX_ALERTS)
			{
				// ★ 알림 칸은 유한하다(가득 차면 *먼저 난 것*부터 밀려난다). 같은 순간에 여러 사건이
				//   터지면 내 알림이 밀려난 것일 뿐인데, 그걸 「화면이 말 안 한다」로 부르면 멀쩡한
				//   규칙을 고치러 간다. 자리가 남아 있었을 때만 실패로 부른다.
				Debug.Log(TAG + " 소리 — 화면은 못 쟀다: 알림 칸이 이미 꽉 차 있었다(먼저 난 것이 밀려난다). 실패가 아니다.");
			}
			else if (spoken == false)
				Debug.LogWarning(TAG + " 소리 FAIL — 자리가 남아 있는데도 소리로 깬 이유를 화면이 말하지 않는다.");
		}
	}
}
