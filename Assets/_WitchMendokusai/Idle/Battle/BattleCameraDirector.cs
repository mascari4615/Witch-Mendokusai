using Unity.Cinemachine;
using UnityEngine;

namespace WitchMendokusai.Idle
{
	/// <summary>
	/// 전투 창 카메라 (사용자 2026-09-05: 시네머신으로, 화면을 덮지 말고 카메라가 옮겨 다니게).
	///
	/// ★ 카메라 셋. 전투는 편성을 따라다니고, 가게와 연구실은 각자 방에 고정.
	///   탭을 바꾸면 우선순위만 바꾼다. 사이는 시네머신이 섞어 주므로 그것이 곧 전환 연출
	/// ★ 판이 좌표를 앞으로 밀면 (<see cref="DomainSDK.Idle.IdleSnapshot.OriginX"/>) 따라갈 대상도 같이
	///   뒤로 밀린다. 그대로 두면 카메라가 그 거리를 부드럽게 쫓아가 세상이 뚝 미끄러져 보임.
	///   <see cref="Warp"/> 가 시네머신에 대상이 순간이동했다고 알려 화면을 그대로 붙잡음
	/// </summary>
	internal sealed class BattleCameraDirector
	{
		internal sealed class Settings
		{
			/// <summary>따라갈 대상에서 카메라까지의 거리 (전투)</summary>
			public Vector3 BattleOffset { get; set; }

			public Vector3 BattleEuler { get; set; }

			/// <summary>따라가기 굼뜸 (x, y, z). 클수록 느리게 따라붙음</summary>
			public Vector3 FollowDamping { get; set; }

			public float FieldOfView { get; set; }

			/// <summary>탭을 바꿀 때 카메라가 옮겨 가는 시간 (초)</summary>
			public float BlendSeconds { get; set; }

			/// <summary>가게 방과 연구실 방의 자리. 전투 마당에서 이만큼 떨어져 있다</summary>
			public Vector3 ShopRoom { get; set; }

			public Vector3 LabRoom { get; set; }

			/// <summary>방을 볼 때 카메라 자리 (방 기준)</summary>
			public Vector3 RoomOffset { get; set; }

			public Vector3 RoomEuler { get; set; }
		}

		private const int SHOWN_PRIORITY = 20;
		private const int HIDDEN_PRIORITY = 0;

		private readonly Settings settings;
		private Transform target;
		private CinemachineCamera battleCamera;
		private CinemachineCamera shopCamera;
		private CinemachineCamera labCamera;
		private CinemachineBrain brain;

		public BattleCameraDirector(Settings settings)
		{
			this.settings = settings;
		}

		/// <summary>따라갈 대상. 무대가 편성 한가운데를 여기에 넣는다</summary>
		public Transform Target => target;

		public void Build(Transform parent)
		{
			GameObject anchor = new GameObject("CameraTarget");
			anchor.hideFlags = HideFlags.DontSave;
			anchor.transform.SetParent(parent, false);
			target = anchor.transform;

			battleCamera = MakeCamera(parent, "BattleCamera", settings.BattleEuler);
			CinemachineFollow follow = battleCamera.gameObject.AddComponent<CinemachineFollow>();
			follow.FollowOffset = settings.BattleOffset;
			follow.TrackerSettings.PositionDamping = settings.FollowDamping;
			battleCamera.Follow = target;

			shopCamera = MakeRoomCamera(parent, "ShopCamera", settings.ShopRoom);
			labCamera = MakeRoomCamera(parent, "LabCamera", settings.LabRoom);

			EnsureBrain();
			Show(StageScene.Battle);
		}

		/// <summary>편성 한가운데. 카메라는 이 점을 따라간다</summary>
		public void Aim(Vector3 worldPosition)
		{
			if (target != null)
			{
				target.position = worldPosition;
			}
		}

		/// <summary>
		/// 판이 세상을 <paramref name="shift"/> 만큼 앞으로 밂. 카메라도 같은 만큼 즉시 옮김
		///
		/// ★ 이것이 이음새의 전부. 안 하면 웨이브마다 화면이 뒤로 미끄러짐
		/// </summary>
		public void Warp(float shift)
		{
			if (target == null || Mathf.Approximately(shift, 0f))
			{
				return;
			}

			Vector3 delta = new Vector3(-shift, 0f, 0f);
			target.position += delta;
			CinemachineCore.OnTargetObjectWarped(target, delta);
		}

		/// <summary>보여 줄 장면. 우선순위만 바꾸고 사이는 시네머신이 섞는다</summary>
		public void Show(StageScene scene)
		{
			SetPriority(battleCamera, scene == StageScene.Battle);
			SetPriority(shopCamera, scene == StageScene.Shop);
			SetPriority(labCamera, scene == StageScene.Lab);
		}

		/// <summary>지금 카메라가 옮겨 가는 중인가. 화면이 그 사이 소리를 줄이는 데 씀</summary>
		public bool Blending => brain != null && brain.IsBlending;

		private static void SetPriority(CinemachineCamera camera, bool shown)
		{
			if (camera != null)
			{
				camera.Priority = shown ? SHOWN_PRIORITY : HIDDEN_PRIORITY;
			}
		}

		private CinemachineCamera MakeCamera(Transform parent, string name, Vector3 euler)
		{
			GameObject made = new GameObject(name);
			made.hideFlags = HideFlags.DontSave;
			made.transform.SetParent(parent, false);
			made.transform.rotation = Quaternion.Euler(euler);

			CinemachineCamera camera = made.AddComponent<CinemachineCamera>();
			camera.Lens.FieldOfView = settings.FieldOfView;
			camera.Priority = HIDDEN_PRIORITY;
			return camera;
		}

		private CinemachineCamera MakeRoomCamera(Transform parent, string name, Vector3 room)
		{
			CinemachineCamera camera = MakeCamera(parent, name, settings.RoomEuler);
			camera.transform.position = room + settings.RoomOffset;
			return camera;
		}

		/// <summary>메인 카메라에 두뇌를 단다. 이미 있으면 섞는 시간만 맞춘다</summary>
		private void EnsureBrain()
		{
			Camera main = Camera.main;
			if (main == null)
			{
				return;
			}

			brain = main.GetComponent<CinemachineBrain>();
			if (brain == null)
			{
				brain = main.gameObject.AddComponent<CinemachineBrain>();
			}

			brain.DefaultBlend = new CinemachineBlendDefinition(
				CinemachineBlendDefinition.Styles.EaseInOut, settings.BlendSeconds);
		}
	}
}
