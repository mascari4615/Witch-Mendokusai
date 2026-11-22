using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 풀이 주인공과 가까운 몬스터 쪽에서 눕고, 싸운 자리 (처치, 폭발) 는 한동안 눌려 있다 다시 섬
	/// - 셰이더 WM/Remains/InteractiveGrass 가 전역 값을 읽음. 이 클래스는 매 프레임 전역 값만 씀
	/// - 눌림은 판 없는 캔버스 (GroundStainCanvas) 하나. 도장의 알파가 눌린 정도, 다시 서는 시간이 옅어지는 시간
	/// </summary>
	public sealed class GrassInteraction
	{
		private static readonly int BENDERS_ID = Shader.PropertyToID("_WMGrassBenders");
		private static readonly int BENDER_COUNT_ID = Shader.PropertyToID("_WMGrassBenderCount");
		private static readonly int FLATTEN_TEX_ID = Shader.PropertyToID("_WMGrassFlatten");
		private static readonly int FLATTEN_RECT_ID = Shader.PropertyToID("_WMGrassFlattenRect");
		private static readonly int PARAMS_ID = Shader.PropertyToID("_WMGrassParams");
		// 셰이더 배열 길이와 같아야 함 (InteractiveGrass.shader 의 MAX_BENDERS)
		public const int MAX_BENDERS = 16;

		private readonly Vector4[] benders = new Vector4[MAX_BENDERS];
		private readonly GroundStainCanvas flatten;

		public GrassInteraction(Transform parent, RemainsSettings settings)
		{
			// 눌림은 알파 하나만 읽음. 낮은 해상도로 충분
			flatten = new GroundStainCanvas(parent, settings.StainUtilityShader, null, settings.GrassFlattenResolution);
		}

		public void Flatten(Vector3 position, float radius) => flatten.Add(position, radius, Color.white);

		public void Tick(RemainsSettings settings, PlayerObject player, float groundHeight, float deltaTime)
		{
			Vector3 center = player.UnitMovement.Position;
			int count = 0;
			benders[count++] = new Vector4(center.x, center.y, center.z, settings.GrassPlayerBendRadius);
			float rangeSquared = settings.GrassBenderRange * settings.GrassBenderRange;
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = 0; i < monsters.Count && count < Mathf.Min(MAX_BENDERS, settings.GrassMaxBenders); i++)
			{
				GameObject monster = monsters[i];
				if (monster.activeInHierarchy == false)
					continue;
				Vector3 position = monster.transform.position;
				if ((position - center).sqrMagnitude > rangeSquared)
					continue;
				benders[count++] = new Vector4(position.x, position.y, position.z, settings.GrassMonsterBendRadius);
			}
			for (int i = count; i < MAX_BENDERS; i++)
				benders[i] = Vector4.zero;
			Shader.SetGlobalVectorArray(BENDERS_ID, benders);
			Shader.SetGlobalFloat(BENDER_COUNT_ID, count);

			flatten.Tick(settings, settings.GrassRegrowSeconds, center, groundHeight, deltaTime);
			Shader.SetGlobalTexture(FLATTEN_TEX_ID, flatten.Texture);
			Shader.SetGlobalVector(FLATTEN_RECT_ID, flatten.WorldRect);
			Shader.SetGlobalVector(PARAMS_ID, new Vector4(settings.GrassBendStrength, settings.GrassHeight, settings.GrassFlattenAmount, groundHeight));
		}

		public void Clear()
		{
			flatten.Clear();
			Suspend();
		}

		// 판 밖 (결과 화면, 로비). 같은 풀 재질을 쓰는 다른 장면이 눕거나 눌리지 않게 전역 값을 비움
		public void Suspend()
		{
			Shader.SetGlobalFloat(BENDER_COUNT_ID, 0f);
			Shader.SetGlobalVector(FLATTEN_RECT_ID, Vector4.zero);
		}

		public void Dispose()
		{
			flatten.Dispose();
			Suspend();
		}
	}
}
