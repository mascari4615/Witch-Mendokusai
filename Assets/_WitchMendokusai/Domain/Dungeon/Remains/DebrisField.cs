using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 싸움 뒤 바닥에 남는 조각들. 날다가 바닥에 닿으면 눕고, 유지 시간 동안 남았다가 작아지며 사라짐
	/// - 누운 조각은 주인공 발밑과 폭발 (Push) 에 다시 날아감
	/// - 개체는 고정 상한의 풀. 날 자리가 없으면 가장 오래 누운 것부터 다시 씀
	/// - 누운 조각 수가 상한을 넘으면 오래된 것부터 사라짐
	/// </summary>
	public sealed class DebrisField
	{
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

		private readonly List<DebrisPiece> pieces = new();
		private readonly List<DebrisPiece> resting = new();
		private readonly Stack<DebrisPiece> idle = new();
		private readonly Transform parent;
		private readonly MaterialPropertyBlock propertyBlock = new();
		private int flyingCount;

		public event Action<Vector3, float, Color> OnStain = delegate { };

		public int FlyingCount => flyingCount;
		public int RestingCount => resting.Count;

		public DebrisField(Transform parent)
		{
			this.parent = parent;
		}

		public void Spawn(RemainsSettings settings, in DebrisSpawn spawn)
		{
			DebrisPiece piece = Rent(settings);
			if (piece == null)
				return;
			piece.Spawn = spawn;
			piece.Velocity = spawn.Velocity;
			piece.StateTime = 0f;
			piece.Filter.sharedMesh = spawn.Mesh;
			piece.Renderer.sharedMaterial = spawn.Material;
			if (spawn.HasTint)
			{
				propertyBlock.SetColor(BASE_COLOR_ID, spawn.Tint);
				piece.Renderer.SetPropertyBlock(propertyBlock);
			}
			else
			{
				piece.Renderer.SetPropertyBlock(null);
			}
			piece.Transform.SetPositionAndRotation(spawn.Position, spawn.Rotation);
			piece.Transform.localScale = Vector3.one * spawn.Size;
			piece.Transform.gameObject.SetActive(true);
			SetState(piece, DebrisState.Flying);
		}

		// 날 자리 하나. 상한이면 가장 오래 누운 것을 빼서 씀, 그것도 없으면 버림
		private DebrisPiece Rent(RemainsSettings settings)
		{
			if (flyingCount >= settings.MaxFlying)
				return null;
			if (idle.Count > 0)
				return idle.Pop();
			if (pieces.Count < settings.MaxFlying + settings.MaxResting)
				return Create();
			if (resting.Count > 0)
			{
				DebrisPiece oldest = resting[0];
				RemoveResting(oldest);
				return oldest;
			}
			return null;
		}

		private DebrisPiece Create()
		{
			GameObject pieceObject = new("Debris");
			pieceObject.transform.SetParent(parent, false);
			MeshFilter filter = pieceObject.AddComponent<MeshFilter>();
			MeshRenderer renderer = pieceObject.AddComponent<MeshRenderer>();
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			DebrisPiece created = new() { Transform = pieceObject.transform, Filter = filter, Renderer = renderer };
			pieces.Add(created);
			return created;
		}

		private void SetState(DebrisPiece piece, DebrisState next)
		{
			if (piece.State == DebrisState.Flying)
				flyingCount--;
			if (next == DebrisState.Flying)
				flyingCount++;
			piece.State = next;
			piece.StateTime = 0f;
		}

		public void Tick(RemainsSettings settings, float lingerSeconds, float deltaTime, Vector3 pusherPosition, Vector3 pusherVelocity, bool hasPusher)
		{
			bool pushing = hasPusher && new Vector2(pusherVelocity.x, pusherVelocity.z).magnitude >= settings.PlayerPushMinSpeed;
			for (int i = 0; i < pieces.Count; i++)
			{
				DebrisPiece piece = pieces[i];
				switch (piece.State)
				{
					case DebrisState.Flying:
						TickFlying(settings, piece, deltaTime);
						break;
					case DebrisState.Settling:
						TickSettling(settings, piece, deltaTime);
						break;
					case DebrisState.Resting:
						piece.StateTime += deltaTime;
						if (pushing && TryPush(piece, pusherPosition, settings.PlayerPushRadius, pusherVelocity, settings.PlayerPushSpeed, settings.PlayerPushLift))
							break;
						if (piece.StateTime >= lingerSeconds)
							BeginVanish(piece);
						break;
					case DebrisState.Vanishing:
						TickVanishing(settings, piece, deltaTime);
						break;
				}
			}
			// 상한 넘친 누운 조각은 오래된 것부터 사라짐
			while (resting.Count > settings.MaxResting)
				BeginVanish(resting[0]);
		}

		private void TickFlying(RemainsSettings settings, DebrisPiece piece, float deltaTime)
		{
			DebrisSpawn spawn = piece.Spawn;
			piece.StateTime += deltaTime;
			piece.Velocity += Vector3.down * spawn.Gravity * deltaTime;
			Vector3 position = piece.Transform.position + piece.Velocity * deltaTime;
			float floor = spawn.GroundHeight + spawn.Size * spawn.FlyClearance;
			bool landed = false;
			if (position.y < floor)
			{
				position.y = floor;
				piece.Velocity = new Vector3(piece.Velocity.x * spawn.Bounce, -piece.Velocity.y * spawn.Bounce, piece.Velocity.z * spawn.Bounce);
				landed = piece.Velocity.magnitude < settings.RestSpeed;
			}
			Quaternion rotation = Quaternion.AngleAxis(spawn.SpinDegrees * deltaTime, spawn.SpinAxis) * piece.Transform.rotation;
			piece.Transform.SetPositionAndRotation(position, rotation);
			if (landed || piece.StateTime >= settings.MaxFlightSeconds)
				BeginSettle(piece);
		}

		// 지금 각도에서 바닥에 붙는 각도로. 방향 (yaw) 은 남기고 눕힘
		private void BeginSettle(DebrisPiece piece)
		{
			Quaternion current = piece.Transform.rotation;
			Vector3 forward = current * Vector3.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < .0001f)
				forward = current * Vector3.up;
			forward.y = 0f;
			piece.SettleFrom = current;
			piece.SettleTo = forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
			piece.SettleFromPosition = piece.Transform.position;
			piece.Velocity = Vector3.zero;
			SetState(piece, DebrisState.Settling);
		}

		private void TickSettling(RemainsSettings settings, DebrisPiece piece, float deltaTime)
		{
			piece.StateTime += deltaTime;
			float progress = Mathf.Clamp01(piece.StateTime / settings.SettleSeconds);
			DebrisSpawn spawn = piece.Spawn;
			Vector3 target = piece.SettleFromPosition;
			target.y = spawn.GroundHeight + spawn.Size * spawn.RestClearance;
			piece.Transform.SetPositionAndRotation(Vector3.Lerp(piece.SettleFromPosition, target, progress), Quaternion.Slerp(piece.SettleFrom, piece.SettleTo, progress));
			if (progress < 1f)
				return;
			SetState(piece, DebrisState.Resting);
			piece.RestingIndex = resting.Count;
			resting.Add(piece);
			if (spawn.StainRadius > 0f)
				OnStain(target, spawn.StainRadius * spawn.Size, spawn.StainColor);
		}

		private bool TryPush(DebrisPiece piece, Vector3 center, float radius, Vector3 pusherVelocity, float speed, float lift)
		{
			Vector3 offset = piece.Transform.position - center;
			offset.y = 0f;
			if (offset.sqrMagnitude > radius * radius)
				return false;
			Vector3 away = offset.sqrMagnitude > .0001f ? offset.normalized : new Vector3(pusherVelocity.x, 0f, pusherVelocity.z).normalized;
			Vector3 along = new Vector3(pusherVelocity.x, 0f, pusherVelocity.z).normalized;
			Launch(piece, (away + along).normalized * speed + Vector3.up * lift);
			return true;
		}

		// 폭발이나 돌진. 반경 안 누운 조각을 바깥으로 날림, 가까울수록 셈
		public void Push(Vector3 center, float radius, float speed, float lift)
		{
			for (int i = resting.Count - 1; i >= 0; i--)
			{
				DebrisPiece piece = resting[i];
				Vector3 offset = piece.Transform.position - center;
				offset.y = 0f;
				float distance = offset.magnitude;
				if (distance > radius)
					continue;
				float strength = 1f - distance / radius;
				Vector3 away = distance > .0001f ? offset / distance : Vector3.forward;
				Launch(piece, away * speed * strength + Vector3.up * lift * strength);
			}
		}

		private void Launch(DebrisPiece piece, Vector3 velocity)
		{
			RemoveResting(piece);
			piece.Velocity = velocity;
			piece.Spawn.SpinAxis = UnityEngine.Random.onUnitSphere;
			SetState(piece, DebrisState.Flying);
		}

		private void BeginVanish(DebrisPiece piece)
		{
			RemoveResting(piece);
			SetState(piece, DebrisState.Vanishing);
		}

		private void TickVanishing(RemainsSettings settings, DebrisPiece piece, float deltaTime)
		{
			piece.StateTime += deltaTime;
			float remaining = 1f - piece.StateTime / settings.VanishSeconds;
			if (remaining <= 0f)
			{
				Release(piece);
				return;
			}
			piece.Transform.localScale = Vector3.one * piece.Spawn.Size * remaining;
		}

		private void RemoveResting(DebrisPiece piece)
		{
			if (piece.RestingIndex < 0)
				return;
			resting.RemoveAt(piece.RestingIndex);
			for (int i = piece.RestingIndex; i < resting.Count; i++)
				resting[i].RestingIndex = i;
			piece.RestingIndex = -1;
		}

		private void Release(DebrisPiece piece)
		{
			RemoveResting(piece);
			SetState(piece, DebrisState.Idle);
			piece.Transform.gameObject.SetActive(false);
			idle.Push(piece);
		}

		public void Clear()
		{
			foreach (DebrisPiece piece in pieces)
			{
				if (piece.State == DebrisState.Idle)
					continue;
				piece.RestingIndex = -1;
				SetState(piece, DebrisState.Idle);
				piece.Transform.gameObject.SetActive(false);
				idle.Push(piece);
			}
			resting.Clear();
		}
	}
}
