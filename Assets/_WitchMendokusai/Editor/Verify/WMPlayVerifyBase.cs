using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// WM PlayMode 자율검증 하네스 공통 lifecycle (TASK-WM — 하네스 DRY 추출).
	///
	/// 패턴 [[wm-playmode-autoverify-bootready-gate]]: arm(메뉴) → EnterPlaymode → ready 게이트 → settle →
	/// RunVerify 1회 → 자동 ExitPlaymode. 외부 명령 상태와 무관하게 하네스가 *에디터 안에서* 스스로 구동하고
	/// Editor.log 가 ground-truth. HARD_TIMEOUT 안전망으로 공유 에디터 보호(절대 Play 에 안 물림).
	///
	/// 파생 = 변하는 3축만 구현: <see cref="Tag"/>(유니크 prefix) / <see cref="ArmPref"/>(EditorPrefs 키) /
	/// <see cref="IsReady"/>(World+매니저 준비) / <see cref="RunVerify"/>(검증 본문). static 끼리 상속 불가라
	/// instance abstract + 파생별 [InitializeOnLoad] static bootstrap(Instance new → ctor 가 playModeStateChanged 구독).
	/// 검증 본문이 spawn 후 *프레임 경과 관찰*을 요구하면(예: 온실 demoTick) 이 단발-RunVerify 모델과 안 맞음 — 그 경우 별도.
	/// </summary>
	public abstract class WMPlayVerifyBase
	{
		protected abstract string ArmPref { get; }
		protected abstract string Tag { get; }

		/// <summary> ready 조건 — <see cref="SceneIsWorld"/> + 하네스 고유 매니저 준비. </summary>
		protected abstract bool IsReady();

		/// <summary> 검증 본문 — ready+settle 후 1회 동기 실행. LOOP OK/FAIL 로그 + 스크린샷까지 책임. 예외는 base 가 잡아 EXCEPTION 로그. </summary>
		protected abstract void RunVerify();

		protected virtual double SettleSeconds => 2.0;
		protected virtual double HardTimeout => 45.0;

		private double playStart;
		private double readyAt = -1.0;
		private bool ran;

		// 파생 static bootstrap 이 `new()` 하면 구독. Instance 가 static readonly 라 구독은 1회.
		protected WMPlayVerifyBase()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		// 파생 [MenuItem] static 래퍼가 Instance.Arm() 호출.
		public void Arm()
		{
			EditorPrefs.SetBool(ArmPref, true);
			Debug.Log(Tag + " armed — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		private void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(ArmPref, false))
			{
				EditorPrefs.SetBool(ArmPref, false);
				playStart = EditorApplication.timeSinceStartup;
				readyAt = -1.0;
				ran = false;
				EditorApplication.update += Tick;
				Debug.Log(Tag + " EnteredPlayMode — ready 대기 시작");
			}
		}

		private void Tick()
		{
			double now = EditorApplication.timeSinceStartup;

			// 안전망: 무슨 일이 있어도 HARD_TIMEOUT 넘으면 Play 탈출(공유 에디터 보호).
			if (now - playStart > HardTimeout)
			{
				Debug.LogError(Tag + " TIMEOUT — ready 미충족 또는 행. Play 강제 종료.");
				Finish();
				return;
			}
			if (ran)
				return;
			if (IsReady() == false)
				return;

			// settle 2단 게이트: ready 도달 후 SettleSeconds 안정화 대기 → 1회 실행.
			if (readyAt < 0.0)
			{
				readyAt = now;
				return;
			}
			if (now - readyAt < SettleSeconds)
				return;

			ran = true;
			try
			{
				RunVerify();
			}
			catch (Exception exception)
			{
				Debug.LogError(Tag + " EXCEPTION — " + exception.GetType().Name + ": " + exception.Message);
			}
			finally
			{
				Finish();
			}
		}

		/// <summary> 활성 씬이 World(로드 완료)인가 — 야외 검증 공통 게이트. </summary>
		protected static bool SceneIsWorld()
		{
			Scene active = SceneManager.GetActiveScene();
			return active.IsValid() && active.name == "World" && active.isLoaded;
		}

		/// <summary> TAG 자동 prefix 로그. </summary>
		protected void Log(string message) => Debug.Log(Tag + " " + message);

		private void Finish()
		{
			EditorApplication.update -= Tick;
			if (EditorApplication.isPlaying)
				EditorApplication.ExitPlaymode();
		}
	}
}
