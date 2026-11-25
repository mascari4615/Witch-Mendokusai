using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace WitchMendokusai
{
	/// <summary>
	/// 타는 액체 위의 불. 불 자리 (GoopFireSpots) 마다 VFX 그래프 (GroundFire) 하나
	/// - 그래프는 폭 1 짜리 불 (몸통, 연기, 불티). 크기는 Transform 배율, 세기와 시작 장과 좌우 뒤집기는 노출 속성
	/// - 자리는 간격마다 다시 구하고 불은 매 프레임 부드럽게 따라감. 자리가 없어진 불은 꺼지고 남은 연기가 사라진 뒤 풀로
	/// - 주인공이나 몬스터가 안에 서면 옅어짐
	/// </summary>
	public sealed class GoopFire
	{
		private sealed class Flame
		{
			public VisualEffect Effect;
			public Vector2 Center;
			public float Width;
			public Vector2 TargetCenter;
			public float TargetWidth;
			public float SizeScale;
			public float Growth;
			public float Opacity;
			public float UnitFade = 1f;
			public float TargetUnitFade = 1f;
			public float LingerLeft;
			public bool Burning = true;
			public bool Claimed;
		}

		private static readonly int TINT_ID = Shader.PropertyToID("Tint");
		private static readonly int OPACITY_ID = Shader.PropertyToID("Opacity");
		private static readonly int START_FRAME_ID = Shader.PropertyToID("StartFrame");
		private static readonly int MIRROR_ID = Shader.PropertyToID("Mirror");

		private readonly Transform root;
		private readonly RemainsSettings settings;
		private readonly GoopFireSpots builder = new();
		private readonly List<GoopFireSpot> spots = new();
		private readonly List<Flame> flames = new();
		private readonly Stack<VisualEffect> pool = new();
		private float rebuildLeft;
		private bool hidden;

		public GoopFire(Transform parent, RemainsSettings settings)
		{
			this.settings = settings;
			root = new GameObject("GoopFire").transform;
			root.SetParent(parent, false);
		}

		// 노출 속성 이름은 코드와 그래프 두 곳에 있어 어긋나면 조용히 안 먹음. 무엇이든 만들기 전에 부름
		public static void RequirePrefab(VisualEffect prefab)
		{
			if (prefab == null)
				throw new System.InvalidOperationException($"{nameof(RemainsSettings)}.{nameof(RemainsSettings.FirePrefab)} 없음");
			if (prefab.HasVector4(TINT_ID) == false || prefab.HasFloat(OPACITY_ID) == false || prefab.HasFloat(START_FRAME_ID) == false || prefab.HasFloat(MIRROR_ID) == false)
				throw new System.InvalidOperationException("불 그래프에 Tint, Opacity, StartFrame, Mirror 노출 속성이 있어야 함");
		}

		public void Tick(float deltaTime, List<Vector2Int> burningCells, float groundHeight, PlayerObject player)
		{
			if (burningCells.Count == 0 && flames.Count == 0)
				return;
			rebuildLeft -= deltaTime;
			if (rebuildLeft <= 0f)
			{
				rebuildLeft = settings.FireRebuildInterval;
				Rebuild(burningCells, player.transform.position);
			}
			if (flames.Count == 0)
				return;
			Transform view = Camera.main.transform;
			for (int i = flames.Count - 1; i >= 0; i--)
			{
				Flame flame = flames[i];
				if (Advance(flame, deltaTime) == false)
				{
					Release(flame);
					flames.RemoveAt(i);
					continue;
				}
				Apply(flame, groundHeight, view);
			}
		}

		private void Rebuild(List<Vector2Int> burningCells, Vector3 playerPosition)
		{
			GoopFireRules rules = new(settings.GoopCellSize, settings.FireSplitAspect, settings.FireMinFill, settings.FireMinWidth, settings.FireMaxWidth, settings.MaxFires);
			builder.Build(burningCells, rules, new Vector2(playerPosition.x, playerPosition.z), spots);
			MatchSpots();
			foreach (Flame flame in flames)
				if (flame.Burning)
					flame.TargetUnitFade = UnitFadeOf(flame, playerPosition);
		}

		// 가장 가까운 짝부터 이음. 남은 자리는 새 불, 남은 불은 꺼짐
		private void MatchSpots()
		{
			foreach (Flame flame in flames)
				flame.Claimed = flame.Burning == false;
			while (TakeNearestPair(out Flame flame, out int spotIndex))
			{
				Aim(flame, spots[spotIndex]);
				spots.RemoveAt(spotIndex);
			}
			foreach (Flame flame in flames)
				if (flame.Claimed == false)
					Extinguish(flame);
			foreach (GoopFireSpot spot in spots)
				flames.Add(Light(spot));
		}

		private bool TakeNearestPair(out Flame nearest, out int spotIndex)
		{
			nearest = null;
			spotIndex = -1;
			float best = float.PositiveInfinity;
			foreach (Flame flame in flames)
			{
				if (flame.Claimed)
					continue;
				for (int i = 0; i < spots.Count; i++)
				{
					float reach = Mathf.Max(flame.Width, spots[i].Width) * settings.FireMatchReach;
					float distanceSquared = (flame.Center - spots[i].Center).sqrMagnitude;
					if (distanceSquared > reach * reach || distanceSquared >= best)
						continue;
					best = distanceSquared;
					nearest = flame;
					spotIndex = i;
				}
			}
			return nearest != null;
		}

		private static void Aim(Flame flame, GoopFireSpot spot)
		{
			flame.Claimed = true;
			flame.TargetCenter = spot.Center;
			flame.TargetWidth = spot.Width;
		}

		private Flame Light(GoopFireSpot spot)
		{
			VisualEffect effect = pool.Count > 0 ? pool.Pop() : Object.Instantiate(settings.FirePrefab, root);
			effect.gameObject.SetActive(hidden == false);
			effect.SetFloat(START_FRAME_ID, Random.Range(0f, settings.FireFrameCount));
			effect.SetFloat(MIRROR_ID, Random.value < .5f ? -1f : 1f);
			effect.SetFloat(OPACITY_ID, 0f);
			effect.Reinit();
			effect.Play();
			Flame flame = new() { Effect = effect, Center = spot.Center, Width = spot.Width, SizeScale = 1f + Random.Range(-settings.FireSizeJitter, settings.FireSizeJitter) };
			Aim(flame, spot);
			return flame;
		}

		// 새 연기와 불티를 멈춤. 몸통은 옅어지며 사라지고, 떠 있는 연기가 내려앉지 않게 크기와 자리는 그대로
		private void Extinguish(Flame flame)
		{
			flame.Burning = false;
			flame.LingerLeft = settings.FireExtinguishSeconds + settings.FireLingerSeconds;
			flame.TargetCenter = flame.Center;
			flame.TargetWidth = flame.Width;
			flame.Effect.Stop();
		}

		// 꺼지고 남은 연기까지 끝나면 false
		private bool Advance(Flame flame, float deltaTime)
		{
			float follow = 1f - Mathf.Exp(-settings.FireFollowRate * deltaTime);
			flame.Center = Vector2.Lerp(flame.Center, flame.TargetCenter, follow);
			flame.Width = Mathf.Lerp(flame.Width, flame.TargetWidth, follow);
			flame.UnitFade = Mathf.Lerp(flame.UnitFade, flame.TargetUnitFade, follow);
			if (flame.Burning)
			{
				flame.Growth = Mathf.MoveTowards(flame.Growth, 1f, deltaTime / settings.FireIgniteSeconds);
				flame.Opacity = Mathf.MoveTowards(flame.Opacity, 1f, deltaTime / settings.FireIgniteSeconds);
				return true;
			}
			flame.Opacity = Mathf.MoveTowards(flame.Opacity, 0f, deltaTime / settings.FireExtinguishSeconds);
			flame.LingerLeft -= deltaTime;
			return flame.LingerLeft > 0f;
		}

		private float UnitFadeOf(Flame flame, Vector3 playerPosition)
		{
			if (Contains(flame, playerPosition))
				return settings.FireOpacityWithPlayer;
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = 0; i < monsters.Count; i++)
				if (monsters[i].activeInHierarchy && Contains(flame, monsters[i].transform.position))
					return settings.FireOpacityWithMonster;
			return 1f;
		}

		private static bool Contains(Flame flame, Vector3 position)
		{
			float x = position.x - flame.Center.x;
			float z = position.z - flame.Center.y;
			float radius = flame.Width * .5f;
			return x * x + z * z < radius * radius;
		}

		// 카메라를 보는 판이라 밑동이 땅에 잘리지 않게 카메라 쪽으로 당기고, 당긴 만큼 작게 해 화면 크기를 지킴 (원근 카메라)
		private void Apply(Flame flame, float groundHeight, Transform view)
		{
			float size = flame.Width * settings.FireSizePerWidth * flame.SizeScale * Mathf.Lerp(settings.FireStartScale, 1f, flame.Growth);
			Vector3 forward = view.forward;
			forward.y = 0f;
			Vector3 position = new Vector3(flame.Center.x, groundHeight, flame.Center.y) - forward.normalized * (flame.Width * settings.FireFrontShift);
			Vector3 toCamera = view.position - position;
			float distance = toCamera.magnitude;
			float pull = size * settings.FireDepthPull;
			Transform transform = flame.Effect.transform;
			transform.position = position + toCamera * (pull / distance);
			transform.localScale = Vector3.one * (size * (distance - pull) / distance);
			float opacity = flame.Opacity * flame.UnitFade;
			flame.Effect.SetFloat(OPACITY_ID, opacity);
			flame.Effect.SetVector4(TINT_ID, settings.FireTint * opacity);
		}

		private void Release(Flame flame)
		{
			flame.Effect.gameObject.SetActive(false);
			pool.Push(flame.Effect);
		}

		public void SetVisible(bool visible)
		{
			if (hidden == (visible == false))
				return;
			hidden = visible == false;
			foreach (Flame flame in flames)
				flame.Effect.gameObject.SetActive(visible);
		}

		public void Clear()
		{
			foreach (Flame flame in flames)
				Release(flame);
			flames.Clear();
			rebuildLeft = 0f;
		}

		public void Dispose()
		{
			flames.Clear();
			pool.Clear();
			if (root != null)
				Object.Destroy(root.gameObject);
		}
	}
}
