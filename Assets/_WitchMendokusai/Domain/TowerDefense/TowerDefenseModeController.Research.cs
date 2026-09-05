using System.Collections;
using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using VContainer;

namespace WitchMendokusai
{
	// TowerDefenseModeController 의 연구 화면 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseModeController.cs 를 본다.
	public partial class TowerDefenseModeController : MonoBehaviour
	{
		/// <summary>
		/// 연구 창 열기 — 코어를 골라 준다.
		///
		/// ★ 왜 버튼이 필요한가: 연구는 「코어를 클릭」해야 열리는데, 그 사실이 화면 어디에도 없었다
		///   (사용자 실증: "연구 어케 여는데"). 첫 판의 *유일한 다음 수*가 숨은 문 뒤에 있으면
		///   게임이 시작되지 않는다. 코어 클릭은 그대로 두고, 눈에 보이는 문을 하나 더 낸다.
		/// </summary>
		// 연구 성좌 — 전체화면 그래프(사용자 지시). 처음 열 때 한 번 세운다.
		private TowerDefenseResearchView researchView;

		// ★ 성좌의 *규칙*은 여기 산다 — 화면은 이걸 그릴 뿐이다.
		//   마디 목록은 순수 계산(스테이지 값만 있으면 나온다)이라 화면 없이도 세울 수 있고,
		//   찍은 마디 목록도 여기 있어야 「아직 안 열어본 성좌」의 저장·이어하기가 성립한다.
		private readonly System.Collections.Generic.List<TowerDefenseResearchGraph.Node> researchNodes = new();
		private readonly System.Collections.Generic.List<int> takenResearch = new();

		private void EnsureResearchGraph()
		{
			if (researchNodes.Count > 0)
				return;
			if (takenResearch.Contains(TowerDefenseResearchGraph.CORE_ID) == false)
				takenResearch.Add(TowerDefenseResearchGraph.CORE_ID);
			TowerDefenseResearchGraph.Build(stage.ResearchBranchCount, stage.ResearchRingCount,
				stage.ResearchMajorAmount, stage.ResearchMinorAmount, stage.ResearchNodeCost,
				stage.ResearchEssenceFromRing, stage.ResearchNodeResourceCost, researchNodes);
		}

		private bool TryFindResearchNode(int id, out TowerDefenseResearchGraph.Node node)
		{
			EnsureResearchGraph();
			foreach (TowerDefenseResearchGraph.Node candidate in researchNodes)
			{
				if (candidate.Id != id)
					continue;
				node = candidate;
				return true;
			}

			node = default;
			return false;
		}

		/// <summary>
		/// 성좌를 연다 — HUD 버튼이 부르는 그 문이다.
		/// ★ 공개인 이유: 검사기가 *사람과 같은 문*으로 들어와야 「열리긴 하는가」를 잴 수 있다.
		///   지금까지 성좌 화면은 규칙층만 두드려 검사했고, 화면 자체는 한 번도 안 열어봤다.
		/// </summary>
		public void OpenResearchPanel() => OpenResearch();

		/// <summary> 지금 성좌가 떠 있나 — 화면 상태를 밖에서 물을 수 있어야 「닫히는가」도 잰다. </summary>
		public bool IsResearchOpen => researchView != null && researchView.IsOpen;

		/// <summary> 성좌 화면이 실제로 차지한 자리 — 「전체화면인가」는 이걸로만 답할 수 있다. </summary>
		public UnityEngine.Rect ResearchScreenRect =>
			researchView != null ? researchView.ScreenRect : UnityEngine.Rect.zero;

		/// <summary> UI 뿌리가 차지한 자리 — 「전체화면인가」는 화면 픽셀이 아니라 이것과 견줘야 한다. </summary>
		public UnityEngine.Rect UiRootRect =>
			uiRoot != null && uiRoot.ModeHudLayer != null ? uiRoot.ModeHudLayer.worldBound : UnityEngine.Rect.zero;

		/// <summary> 성좌에 그려진 마디 수 — 0 이면 그래프가 아니라 빈 판이다. </summary>
		public int ResearchNodeCount => researchNodes.Count;

		private void OpenResearch()
		{
			if (match == null || match.CoreCombatant == null)
				return;

			placement.SuppressNextClick(); // 이 클릭이 지면 설치로 새지 않게.

			if (researchView == null && uiRoot != null && uiRoot.ModeHudLayer != null)
			{
				researchView = new TowerDefenseResearchView();
				// 모양은 스테이지가 정한다 — 갈래 수·길이·주는 양 전부 인스펙터에서.
				researchView.Build(uiRoot.ModeHudLayer, stage.ResearchBranchCount, stage.ResearchRingCount,
					stage.ResearchMajorAmount, stage.ResearchMinorAmount, stage.ResearchNodeCost,
					stage.ResearchEssenceFromRing, stage.ResearchNodeResourceCost,
					stage.ResearchBranchNames);
				researchView.NodeChosen += nodeId => ChooseResearchNode(nodeId);
				researchView.SetEssenceProvider(() => match.Essence);
				// 늦게 세운 화면을 지금 상태에 맞춘다 — 이어하기로 이미 찍힌 마디가 있는데
				// 빈 성좌를 띄우면 「효과는 있는데 자국이 없는」 반대쪽 갈라짐이 된다.
				researchView.RestoreTaken(takenResearch);
			}

			researchView?.SetOpen(true);
			// ★ 성좌는 화면을 통째로 덮는다 — 그 뒤에서 판이 계속 돌면 「어디로 뚫을까」를 고민하는
			//   동안 코어가 털린다. 메뉴와 같은 규칙으로 멈춘다(내가 멈춘 것만 내가 푼다 —
			//   사용자가 직접 멈춰 뒀으면 닫을 때 마음대로 풀면 안 된다).
			if (match != null && match.IsPaused == false)
			{
				pausedByResearch = true;
				match.TogglePause();
			}
		}

		/// <summary>
		/// 이어하기 — 적힌 마디를 화면에 되돌리고 효과도 같이 다시 쌓는다.
		/// ★ 값은 다시 안 받는다(이미 치른 것) — 여기서 또 받으면 이어할 때마다 정수가 빠진다.
		/// </summary>
		private void RestoreResearchNodes(System.Collections.Generic.List<int> ids)
		{
			if (match == null || ids == null)
				return;

			takenResearch.Clear();
			takenResearch.Add(TowerDefenseResearchGraph.CORE_ID); // 코어는 이미 가진 것 — 길이 여기서 시작한다.
			takenResearch.AddRange(ids);
			researchView?.RestoreTaken(ids); // 화면은 있으면 맞추고, 없으면 열릴 때 맞춘다.
			foreach (int id in ids)
			{
				if (TryFindResearchNode(id, out TowerDefenseResearchGraph.Node node) == false)
					continue;
				// 되살리는 길이라 값은 0 — 어느 지갑인지도 물어볼 필요가 없다(이미 치른 것).
				match.TryTakeResearchNode(node.Effect, node.Amount, cost: 0, usesEssence: node.UsesEssence);
				// 단계는 저장이 따로 들고 있다 — 여기서 또 올리면 이어할 때마다 해금이 앞서 나간다.
			}
		}

		/// <summary> 새 판 — 찍은 것도 자국도 처음으로. 둘 중 하나만 지우면 화면과 규칙이 갈라진다. </summary>
		private void ResetResearchNodes()
		{
			takenResearch.Clear();
			takenResearch.Add(TowerDefenseResearchGraph.CORE_ID);
			researchView?.ResetTaken();
		}

		/// <summary> 저장 — 화면이 아니라 여기 적힌 것을 넘긴다(한 번도 안 연 성좌도 저장돼야 한다). </summary>
		private void CollectResearchNodes(System.Collections.Generic.List<int> into)
		{
			if (into == null)
				return;
			foreach (int id in takenResearch)
			{
				if (id != TowerDefenseResearchGraph.CORE_ID)
					into.Add(id);
			}
		}

		/// <summary>
		/// 성좌에서 마디를 찍었다 — 값·효과는 규칙층이 정한다(화면은 고르기만 한다).
		/// ★ 공개인 이유: 검사기가 「사람이 찍는 것과 같은 문」으로 들어와야 한다. 검사 전용 뒷문을
		///   따로 내면 그 문만 멀쩡하고 진짜 경로가 썩어도 아무도 모른다.
		/// </summary>
		public bool ChooseResearchNode(int nodeId)
		{
			if (match == null)
				return false;
			if (TryFindResearchNode(nodeId, out TowerDefenseResearchGraph.Node node) == false)
				return false;

			// 값을 못 치르면 화면에서도 도로 지운다 — 「찍힌 척」이 남으면 다음 마디가 잘못 열린다.
			if (match.TryTakeResearchNode(node.Effect, node.Amount, node.Cost, node.UsesEssence) == false)
			{
				researchView?.Undo(nodeId);
				return false;
			}

			takenResearch.Add(nodeId); // 저장이 읽는 정본 — 화면 표시와 따로 적어둔다.

			// 길 끝의 큰 마디 = 연구 한 단계 = 새 칸 해금. 성좌가 판을 바꾸는 자리다.
			if (node.IsMajor)
				match.GrantResearchLevel();
			return true;
		}

		/// <summary> 코어에서 곧장 이어지는 첫 마디 — 검사기가 「사람이 맨 처음 찍는 것」을 재현할 때 쓴다. </summary>
		public bool TryGetFirstResearchNodeId(out int nodeId)
		{
			EnsureResearchGraph();
			foreach (TowerDefenseResearchGraph.Node candidate in researchNodes)
			{
				if (candidate.Id == TowerDefenseResearchGraph.CORE_ID)
					continue;
				if (TowerDefenseResearchGraph.IsReachable(candidate, takenResearch) == false)
					continue;
				nodeId = candidate.Id;
				return true;
			}

			nodeId = -1;
			return false;
		}

		// 성좌가 멈춘 판인지 — 위와 같은 이유로 따로 센다(둘이 한 깃발을 쓰면 하나를 닫을 때 둘 다 풀린다).
		private bool pausedByResearch;

		/// <summary> 성좌를 닫는다 — 성좌 때문에 멈춘 판이면 다시 굴린다. </summary>
		private void CloseResearch()
		{
			researchView?.SetOpen(false);
			if (pausedByResearch == false)
				return;
			pausedByResearch = false;
			if (match != null && match.IsPaused)
				match.TogglePause();
		}
	}
}
