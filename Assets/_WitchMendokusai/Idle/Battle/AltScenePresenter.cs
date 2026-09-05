using UnityEngine;

namespace WitchMendokusai.Idle
{
	/// <summary>전투 창이 보여 주는 장면. 상점과 연구소 탭은 전투 대신 그 장면 (layout.md 2)</summary>
	internal enum StageScene
	{
		Battle = 0,
		Shop = 1,
		Lab = 2,
	}

	/// <summary>
	/// 상점과 연구실 임시 장면. 바닥, 가구, 인형 하나, 소품 몇 개 (사용자 2026-09-05 "임시로라도")
	///
	/// ★ 전투 시뮬은 뒤에서 계속 돎. 여기는 보이는 것만 바꿈
	/// ★ 인형은 전투와 같은 프리팹. 대기 동작만
	/// </summary>
	internal sealed class AltScenePresenter
	{
		internal sealed class Settings
		{
			public Color ShopGroundColor { get; set; }
			public Color LabGroundColor { get; set; }
			public Color ShopPropColor { get; set; }
			public Color LabPropColor { get; set; }
			public Vector3 GroundScale { get; set; }
			public Vector3 CounterSize { get; set; }
			public Vector3 CounterPosition { get; set; }
			public Vector3 CrateSize { get; set; }
			public Vector3 CrateStart { get; set; }
			public float CrateSpacing { get; set; }
			public int CrateCount { get; set; }
			public Vector3 DeskSize { get; set; }
			public Vector3 DeskPosition { get; set; }
			public float FlaskRadius { get; set; }
			public Vector3 FlaskStart { get; set; }
			public float FlaskSpacing { get; set; }
			public int FlaskCount { get; set; }
			public Vector3 DollPosition { get; set; }
			public float DollYaw { get; set; }
		}

		private readonly Transform parent;
		private readonly BattleEntityPresenter.Settings entitySettings;
		private readonly Settings settings;
		private Transform shopRoot;
		private Transform labRoot;
		private IdleDollAnimator shopDoll;
		private IdleDollAnimator labDoll;
		private StageScene shown = StageScene.Battle;
		private Vector3 shopRoom;
		private Vector3 labRoom;

		public AltScenePresenter(Transform parent, BattleEntityPresenter.Settings entitySettings, Settings settings)
		{
			this.parent = parent;
			this.entitySettings = entitySettings;
			this.settings = settings;
		}

		public StageScene Shown => shown;

		/// <summary>방이 설 자리. 전투 마당과 겹치지 않게 떨어뜨린다 (카메라가 옮겨 감)</summary>
		public void PlaceRooms(Vector3 shop, Vector3 lab)
		{
			shopRoom = shop;
			labRoom = lab;

			if (shopRoot != null) { shopRoot.localPosition = shopRoom; }
			if (labRoot != null) { labRoot.localPosition = labRoom; }
		}

		/// <summary>
		/// 보여 줄 장면. 방은 <b>끄지 않는다</b> (카메라가 그 자리로 가므로)
		///
		/// ★ 처음 볼 때만 지음. 안 볼 때도 켜 두는 편이 도로 켤 때 한 프레임 늦는 것보다 나음
		/// </summary>
		public void Show(StageScene scene)
		{
			shown = scene;

			if (scene == StageScene.Shop && shopRoot == null)
			{
				shopRoot = BuildShop();
				shopRoot.localPosition = shopRoom;
			}

			if (scene == StageScene.Lab && labRoot == null)
			{
				labRoot = BuildLab();
				labRoot.localPosition = labRoom;
			}
		}

		/// <summary>에디트 모드 미리보기용. Play 에서는 Animator 가 스스로 돎</summary>
		public void Tick(float delta)
		{
			if (shown == StageScene.Shop) { shopDoll?.Tick(delta); }
			if (shown == StageScene.Lab) { labDoll?.Tick(delta); }
		}

		private Transform BuildShop()
		{
			Transform root = MakeRoot("Shop", settings.ShopGroundColor);
			MakeBox(root, "Counter", settings.CounterPosition, settings.CounterSize, settings.ShopPropColor);

			for (int at = 0; at < settings.CrateCount; at++)
			{
				Vector3 position = settings.CrateStart + Vector3.right * (settings.CrateSpacing * at);
				MakeBox(root, "Crate" + at, position, settings.CrateSize, settings.ShopPropColor);
			}

			shopDoll = MakeDoll(root);
			return root;
		}

		private Transform BuildLab()
		{
			Transform root = MakeRoot("Lab", settings.LabGroundColor);
			MakeBox(root, "Desk", settings.DeskPosition, settings.DeskSize, settings.LabPropColor);

			for (int at = 0; at < settings.FlaskCount; at++)
			{
				GameObject flask = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				flask.name = "Flask" + at;
				flask.transform.SetParent(root, false);
				flask.transform.localPosition = settings.FlaskStart + Vector3.right * (settings.FlaskSpacing * at);
				flask.transform.localScale = Vector3.one * (settings.FlaskRadius * 2f);
				BattleVisualFactory.Paint(flask, settings.LabPropColor);
			}

			labDoll = MakeDoll(root);
			return root;
		}

		private Transform MakeRoot(string name, Color groundColor)
		{
			GameObject root = new GameObject(name);
			root.transform.SetParent(parent, false);

			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(root.transform, false);
			ground.transform.localScale = settings.GroundScale;
			BattleVisualFactory.Paint(ground, groundColor);
			return root.transform;
		}

		private static void MakeBox(Transform root, string name, Vector3 position, Vector3 size, Color color)
		{
			GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
			box.name = name;
			box.transform.SetParent(root, false);
			box.transform.localPosition = position;
			box.transform.localScale = size;
			BattleVisualFactory.Paint(box, color);
		}

		private IdleDollAnimator MakeDoll(Transform root)
		{
			if (entitySettings.DollPrefab == null)
			{
				return null;
			}

			GameObject doll = Object.Instantiate(entitySettings.DollPrefab, root, false);
			doll.name = "Doll";
			doll.transform.localPosition = settings.DollPosition;
			doll.transform.localRotation = Quaternion.Euler(0f, settings.DollYaw, 0f);
			doll.transform.localScale = Vector3.one * entitySettings.DollModelScale;
			return doll.GetComponentInChildren<IdleDollAnimator>();
		}
	}
}
