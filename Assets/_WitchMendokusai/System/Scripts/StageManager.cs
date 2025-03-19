using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	public class StageManager : Singleton<StageManager>
	{
		public static event Action<Stage, StageObject> OnStageChanged = delegate { };

		[SerializeField] private StageObject homeStageObject;
		[SerializeField] private Stage homeStage;

		public Stage LastStage { get; private set; } = null;
		public StageObject CurStageObject { get; private set; } = null;
		public Stage CurStage { get; private set; } = null;

		private Vector3 lastPosDiff;

		private void Start()
		{
			CurStageObject = homeStageObject;
			CurStage = homeStage;
			OnStageChanged(CurStage, CurStageObject);
		}

		public async UniTask LoadStage(Stage stage, int spawnPortalIndex = -1, bool isBackToLastStage = false)
	{
			// 플레이어의 위치는 그대로, 이동할 스테이지가 플레이어 위치에 생성됨

			// isBackToLastStage: 어떠한 이유로 포탈 타기 전 마지막 위치로 돌아간다는 뜻.
			// i.e. 던전에서 나갈 때 던전을 진입했던 위치로 돌아감 (이렇게 포탈을 타는 것이 아닌, 특정한 이유로 원래 스테이지로 돌아가는 경우)
			// i.e. 포탈을 타고 A -> B 스테이지로 왔다가 다시 B -> A로 돌아가는 경우는 해당 사항이 아님.

			await UIManager.Instance.Transition.Transition(LoadStageTask, aWhenEnd: () => UIManager.Instance.StagePopup(stage));

			void LoadStageTask()
			{
				// 주석 참고:
				// 함수 시작: 이전 스테이지 Z -> 현재 스테이지 A -> 이동할 스테이지 B
				// 함수 종료: 이전 스테이지 A -> 현재 스테이지 B (스테이지 Z는 사용되지 않음.)

				Vector3 newLastPosDiff = Player.Instance.transform.position - CurStageObject.gameObject.transform.position;

				// LastStage를 A로 갱신하고, 비활성화
				CurStageObject.gameObject.SetActive(false);
				LastStage = CurStage;

				// 새로운 스테이지 B 생성 (비활성화 상태)
				GameObject targetStage = stage.Prefab.gameObject;
				CurStageObject = ObjectPoolManager.Instance.Spawn(targetStage).GetComponent<StageObject>();

				// 새로운 스테이지 B 위치 변환
				// TODO:
				// Vector3 portalTPPos = stage.Prefab.Portals[spawnPortalIndex].TpPos.position;
				Vector3 newStagePos;
				{
					if (isBackToLastStage == true)
					{
						newStagePos = Player.Instance.transform.position - lastPosDiff;
					}
					else
					{
						Portal[] portals = stage.Prefab.Portals;
						if (portals.Length == 0)
						{
							newStagePos = Player.Instance.transform.position;
						}
						else
						{
							Vector3 portalTPPos = stage.Prefab.Portals.Where(p => p.TargetStage == LastStage).First().TpPos.position;
							newStagePos = Player.Instance.transform.position - portalTPPos;
						}
					}
				}
				CurStageObject.transform.position = newStagePos;

				// 새로운 스테이지 B 활성화, CurStage를 B로 갱신
				CurStageObject.gameObject.SetActive(true);
				CurStage = stage;
				OnStageChanged(CurStage, CurStageObject);

				// 마지막 이동 위치 갱신 (B -> A로 다시 되돌아가는 경우)
				lastPosDiff = newLastPosDiff;
			}
		}
	}
}