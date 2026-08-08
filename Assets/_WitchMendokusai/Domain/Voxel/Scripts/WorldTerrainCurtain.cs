using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형이 다 찰 때까지 화면을 덮는 가림막 (TASK-WM-194).
	///
	/// ★ 사용자 실증: "복셀 월드 렌더하는데, 처음부터 다 생성이 안된채로 게임이 시작해서 처음에
	///   빈공간 보이다가 차근차근 월드가 생성되는게 보입니다 … 애초에 생성이 다 된 시점에 로딩이
	///   완료되거나, 마인크래프트처럼 월드 로딩할때 그 생성되는 UI 같은 걸 만들어야 할 것 같아요."
	///
	/// ★ 왜 「로딩 완료」 신호를 미루지 않았나: 그 신호(WorldReady)는 부팅 스모크의 판정점이라,
	///   지형을 기다리게 만들면 *검사 도구가 지형 때문에 실패*하기 시작한다. 판정과 연출은 다른 일이다.
	///   그래서 신호는 그대로 두고, 사람 눈에 보이는 가림막만 지형을 기다린다.
	///
	/// ★ 절대 판을 잠그지 않는다 — 지형이 영영 안 차는 경우(맵 끝·생성 실패)에도 정해진 시간이 지나면
	///   스스로 걷힌다. 로딩 화면에 갇히는 것은 어떤 이유로도 없어야 한다.
	/// </summary>
	public sealed class WorldTerrainCurtain : MonoBehaviour
	{
		private const float MAX_WAIT_SECONDS = 20f;
		private const string LABEL = "지형을 만드는 중…";

		private ChunkManager chunkManager;
		private UIRoot uiRoot;
		private VisualElement curtain;
		private Label label;
		private float waited;

		/// <summary>
		/// 씬이 뜬 뒤 스스로 붙는다 — 씬·프리팹을 고치지 않아도 어떤 월드에서든 동작한다.
		/// 복셀 지형이 없는 씬(로비·인트로)에서는 아무 일도 하지 않는다.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Install()
		{
			if (FindAnyObjectByType<ChunkManager>() == null)
				return;

			GameObject host = new GameObject(nameof(WorldTerrainCurtain));
			host.AddComponent<WorldTerrainCurtain>();
		}

		private void Start()
		{
			// init-order-ok: 위 Install 이 **청크 관리자가 이미 있을 때만** 이 오브젝트를 만든다 —
			// 없으면 아예 태어나지 않는다. 그래서 여기선 반드시 있다(없으면 아래에서 스스로 사라진다).
			chunkManager = FindAnyObjectByType<ChunkManager>();
			if (chunkManager == null || UIRoot.TryGetExistingInstance(out uiRoot) == false)
			{
				Destroy(gameObject);
				return;
			}

			if (chunkManager.IsInitialAreaReady)
			{
				Destroy(gameObject); // 이미 다 찼으면 굳이 가리지 않는다.
				return;
			}

			BuildCurtain();
		}

		private void BuildCurtain()
		{
			curtain = new VisualElement { name = "TerrainCurtain" };
			curtain.style.position = Position.Absolute;
			curtain.style.left = 0;
			curtain.style.right = 0;
			curtain.style.top = 0;
			curtain.style.bottom = 0;
			curtain.style.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 1f);
			curtain.style.alignItems = Align.Center;
			curtain.style.justifyContent = Justify.Center;
			// 가림막은 클릭을 삼킨다 — 안 그러면 안 보이는 땅을 눌러 판이 시작된다.
			curtain.pickingMode = PickingMode.Position;

			label = new Label(LABEL);
			label.style.fontSize = 22;
			label.style.color = new Color(0.86f, 0.9f, 0.98f, 1f);
			label.pickingMode = PickingMode.Ignore;
			curtain.Add(label);

			uiRoot.OverlayLayer.Add(curtain);
		}

		private void Update()
		{
			if (curtain == null)
				return;

			waited += Time.unscaledDeltaTime;
			if (chunkManager == null || chunkManager.IsInitialAreaReady || waited >= MAX_WAIT_SECONDS)
			{
				Lift();
				return;
			}

			// 「멈춘 게 아니라 만들고 있다」를 보여준다 — 정지 화면과 작업 화면은 달라 보여야 한다.
			int dots = Mathf.FloorToInt(waited * 2f) % 4;
			label.text = LABEL + new string('·', dots);
		}

		private void Lift()
		{
			curtain?.RemoveFromHierarchy();
			curtain = null;
			Destroy(gameObject);
		}
	}
}
