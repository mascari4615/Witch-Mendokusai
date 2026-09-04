using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Contracts;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.Idle.UI;
using WitchMendokusai.Presentation;

namespace WitchMendokusai.Idle
{
	/// <summary>
	/// V2 작전 화면. 정본 <c>memo/wm/design/idle/layout.md</c> (사용자 확정 2026-08-30).
	///
	/// ★ 화면은 둘. 왼쪽 <b>전투 창</b>(1200)과 오른쪽 <b>관리 열</b>(720).
	///   HUD 는 전부 전투 창 안: 작전 코드, 웨이브, 스테퍼, 반복, 골드, 손패, 코스트, AUTO.
	///   관리 열은 탭 7 + 판 하나. 한 번에 한 판.
	/// ★ 분할 토글: 전투 창과 관리 열을 chevron 하나로 접고 펼침.
	/// ★ 상점, 연구소 탭은 전투 창의 3D 씬 자리를 그 탭의 씬으로 바꾼다 (지금은 자리 표시만).
	/// ★ 규칙은 한 줄도 없다. 사진을 그리고 의도를 보낸다. 판정은 전부 코어.
	/// ★ 설정: 배속과 전투 기록. 골드 상세: HUD 골드 아이콘.
	///
	/// 여기는 MonoBehaviour 수명주기, 에셋 검사, 세션과 저장, 소리, 무대. 화면 조립은 <see cref="BattleScreenView"/>
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(PanelRenderer))]
	public sealed class BattleScreen : MonoBehaviour, IGameView<IdleSnapshot>
	{
		[Header("수치 자산")]
		[SerializeField] private TuningSO tuningAsset;
		[SerializeField] private HeroCatalogSO heroCatalogAsset;
		[SerializeField] private UIContentSO uiContentAsset;
		[SerializeField] private GearPresentationSO gearPresentationAsset;
		[SerializeField] private RuntimeSettingsSO runtimeSettingsAsset;

		[Header("UI Builder 정본과 반복 템플릿")]
		[SerializeField] private IdleViewAssetsSO viewAssets;

		[Header("무대. 씬이 꽂아 준다")]
		[SerializeField] private BattleStage stage;

		private BattleSessionLifecycle sessionLifecycle;
		private IdleSession session => sessionLifecycle?.Session;
		private bool preview => sessionLifecycle?.IsPreview ?? false;
		private float untilUiRefresh;
		private ProceduralSfx sound;
		private bool clickSoundHooked;
		private ScreenRootController screenRootController;
		private VisualElement panelRoot;
		private BattleScreenView view;
		private HeroVisualPresenter heroVisualPresenter;
		private GearVisualPresenter gearVisualPresenter;

		// 에디트 모드 미리보기 (사용자 2026-08-30: UI 수정은 Play 없이). 저장 읽기와 쓰기 없음. 임시 판 위 시뮬만
		/// <summary>화면 에셋이 없어 못 짓는 판. 켜 두되 아무것도 안 그린다</summary>
		private bool broken;

#if UNITY_EDITOR
		private BattlePreviewDriver previewDriver;
#endif

		/// <summary>미리보기 시뮬 진행 여부. 기본은 첫 틱 뒤 정지 (정적 장면). Dev Panel 이 켠다</summary>
		public static bool PreviewRunning { get; set; }

		public PresentationKind Kind => PresentationKind.UIOnly;

		private void OnEnable()
		{
			// UXML 이 정본 (사용자 2026-08-30). 없으면 조용한 빈 화면 대신 여기서 정지
			//
			// ⚠ enabled 를 끄면 그 상태가 씬에 저장됨 (실측 2026-08-31). 에셋을 채워도 복구 불가
			//   플래그만 세우고 컴포넌트는 켠 채로
			if (MissingAsset(out string what))
			{
				Debug.LogError("[Idle] 화면 에셋이 없다: " + what + ". Dev Panel 의 씬 짓기로 다시 꽂아라");
				broken = true;
				return;
			}

			broken = false;
			IdleHeroes.Configure(heroCatalogAsset.ToDomain());
			heroVisualPresenter = new HeroVisualPresenter(heroCatalogAsset);
			gearVisualPresenter = new GearVisualPresenter(gearPresentationAsset);

			// 배치 빌드에서는 아무것도 안 세운다 (실측 2026-09-01: 20회 연속 빌드 실패).
			// 씬 검사(IdleSceneBuilder.Verify)가 씬을 열면 [ExecuteAlways] 때문에 여기가 돌고,
			// -nographics 배치에는 카메라도 패널도 없음. 빌드가 Unknown 으로 사망
			if (Application.isBatchMode)
			{
				return;
			}

			screenRootController = new ScreenRootController(
				GetComponent<PanelRenderer>(),
				viewAssets.Screen,
				OnPanelReloaded);
			screenRootController.Enable();

			IdleTuning tuning = tuningAsset.ToTuning();
			sessionLifecycle = new BattleSessionLifecycle(
				tuning,
				runtimeSettingsAsset,
				Application.isPlaying == false);

			if (sessionLifecycle.IsPreview)
			{
#if UNITY_EDITOR
				previewDriver = new BattlePreviewDriver(Tick, () => PreviewRunning);
#endif
			}
			else
			{
				EnsureSound();
			}

			if (stage != null)
			{
				stage.Build();
			}
			else
			{
				Debug.LogWarning("[Idle] 무대가 안 꽂혀 있다. HUD 만 뜬다. 씬 빌더로 다시 지어라.");
			}

			BuildView(sessionLifecycle.Away);
			Render(session.Capture());
		}

		/// <summary>안 꽂힌 화면 에셋 이름. 전부 있으면 거짓</summary>
		private bool MissingAsset(out string what)
		{
			what = string.Empty;

			if (tuningAsset == null) { what = "tuningAsset"; }
			else if (heroCatalogAsset == null) { what = "heroCatalogAsset"; }
			else if (heroCatalogAsset.TryValidate(out string heroError) == false)
			{
				what = "heroCatalogAsset: " + heroError;
			}
			else if (uiContentAsset == null) { what = "uiContentAsset"; }
			else if (gearPresentationAsset == null) { what = "gearPresentationAsset"; }
			else if (runtimeSettingsAsset == null) { what = "runtimeSettingsAsset"; }
			else if (viewAssets == null) { what = "viewAssets"; }
			else if (viewAssets.TryValidate(out string viewError) == false)
			{
				what = "viewAssets: " + viewError;
			}
			else if (uiContentAsset.TryValidate(
				System.Enum.GetValues(typeof(ManagementPage)).Length, out string uiError) == false)
			{
				what = "uiContentAsset: " + uiError;
			}
			else if (gearPresentationAsset.TryValidate(out string gearError) == false)
			{
				what = "gearPresentationAsset: " + gearError;
			}
			else if (runtimeSettingsAsset.TryValidate(out string runtimeError) == false)
			{
				what = "runtimeSettingsAsset: " + runtimeError;
			}

			return what.Length > 0;
		}

		private void OnDisable()
		{
#if UNITY_EDITOR
			previewDriver?.Dispose();
			previewDriver = null;
#endif
			screenRootController?.Dispose();
			screenRootController = null;
			panelRoot = null;
			clickSoundHooked = false;
			view?.Dispose();
			view = null;
			sessionLifecycle?.Close();
			sessionLifecycle = null;
		}

		private void OnApplicationPause(bool paused)
		{
			if (paused)
			{
				WriteDown();
			}
		}

		private void OnApplicationQuit()
		{
			WriteDown();
		}

		private void Update()
		{
			if (preview)
			{
				return;
			}

			if (view == null)
			{
				BuildView(default);
				if (view == null)
				{
					return;
				}
			}

			Tick(Time.unscaledDeltaTime);
		}

		private void Tick(float delta)
		{
			if (session == null || broken)
			{
				return;
			}

			// 보고 있는 동안은 위험 진행. 적의 공격, 쓰러짐, 부활
			session.AdvanceLive(delta);
			session.AdvanceSurge(delta);
			IdleSnapshot snapshot = session.Capture();

			if (stage != null)
			{
				stage.Render(snapshot, delta);
			}

			view?.Tick(delta);

			untilUiRefresh -= delta;
			if (untilUiRefresh <= 0f)
			{
				untilUiRefresh = runtimeSettingsAsset.UIRefreshSeconds;
				Render(snapshot);
			}

			if (preview)
			{
				return;
			}

			sessionLifecycle.TickPersistence(delta);
		}

		private void WriteDown()
		{
			sessionLifecycle?.Save();
		}

		// ── 짓기 ──────────────────────────────────────────────────────────

		/// <summary>판 위에 화면을 새로 짓는다. 판이 아직 없으면(PanelRenderer OnEnable 전) 다음 Update 에 다시</summary>
		private void BuildView(IdleAwayReport away)
		{
			ManagementPage openPage = view?.OpenedPage ?? ManagementPage.Doll;
			view?.Dispose();
			view = null;
			if (panelRoot == null)
			{
				return;
			}

			if (clickSoundHooked == false)
			{
				panelRoot.RegisterCallback<ClickEvent>(OnButtonClicked);
				clickSoundHooked = true;
			}

			BattleScreenView made = new BattleScreenView(
				panelRoot,
				session,
				stage,
				uiContentAsset,
				runtimeSettingsAsset,
				viewAssets,
				heroVisualPresenter,
				gearVisualPresenter,
				openPage,
				WriteDown,
				WipeAndRestart,
				() => sound?.Good());
			made.Build(away);
			view = made;
		}

		private void OnPanelReloaded(VisualElement rootElement)
		{
			if (panelRoot != rootElement)
			{
				clickSoundHooked = false;
			}

			panelRoot = rootElement;
			if (session == null || broken)
			{
				return;
			}

			BuildView(default);
			Render(session.Capture());
		}

		// ── 그리기 ────────────────────────────────────────────────────────

		public void Render(IdleSnapshot snapshot)
		{
			view?.Render(snapshot);
		}

		/// <summary>
		/// 저장 삭제 뒤 처음부터 재시작. 디버그 전용
		///
		/// ★ 끄면서 저장하는 길(<see cref="OnDisable"/>)이 지운 것을 되살리지 않게 차단 뒤 끔
		/// </summary>
		private void WipeAndRestart()
		{
			// 미리보기는 저장과 무관. 임시 판만 새로
			if (preview)
			{
				enabled = false;
				enabled = true;
				return;
			}

			sessionLifecycle.WipeAndSkipClose();
			enabled = false;
			enabled = true;
		}

		// ── 소리 ──────────────────────────────────────────────────────────

		private void EnsureSound()
		{
			if (sound == null && Application.isPlaying && Application.isBatchMode == false)
			{
				sound = new ProceduralSfx(gameObject, runtimeSettingsAsset.SoundVolume, runtimeSettingsAsset.SoundMinGapSeconds);
			}
		}

		private void OnButtonClicked(ClickEvent moment)
		{
			if (moment.target is Button button && button.ClassListContains("idle-stat-buy") == false)
			{
				sound?.Click();
			}
		}
	}
}
