using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 공격이 남긴 아픈 물체들의 판정. 공격 자산은 여러 유닛이 나눠 쓰고 행동은 이미 다음으로 가서 여기서 봄. 씬마다 하나, 처음 쓸 때 만들어짐
	// 그림은 물체를 낸 이펙트 부품이 그리고 수명 (FxHold) 만 같이 봄. 주인이 죽거나 풀로 돌아가면 그 주인 것은 모두 식음
	public sealed class LingerField : MonoBehaviour
	{
		private static LingerField instance;

		public static LingerField Instance
		{
			get
			{
				if (instance == null)
					instance = new GameObject(nameof(LingerField)).AddComponent<LingerField>();
				return instance;
			}
		}

		private sealed class Piece
		{
			public UnitObject Source;
			public LingerSettings Settings;
			public Vector3 Position;
			public FxHold Hold;
			public float BurstTime = float.PositiveInfinity;
			// 금 가기 전의 수명
			public float PlainHarmlessTime;
			public float PlainEndTime;
			public Action StopTelegraph;
		}

		private readonly List<Piece> pieces = new();
		// 닿음 피해를 다시 시도할 수 있는 시각. 때릴 대상은 주인공 하나라 물체가 몇이든 같이 씀
		private float nextTouchTime;

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;
		}

		public void Add(UnitObject source, LingerSettings settings, Vector3 position, FxHold hold) =>
			pieces.Add(new Piece { Source = source, Settings = settings, Position = position, Hold = hold });

		// source 가 남긴 것 가운데 앞으로 seconds 초 뒤에도 아픈 것의 자리
		public void CollectHot(UnitObject source, float seconds, List<Vector3> results)
		{
			results.Clear();
			foreach (Piece piece in pieces)
			{
				if (piece.Source == source && piece.Hold.IsHot(Time.time) && piece.Hold.IsHot(Time.time + seconds))
					results.Add(piece.Position);
			}
		}

		// 주인이 켜진 채 다른 데로 옮겨짐 (재배치). 남긴 것을 모두 식힘
		public static void Abandon(UnitObject source)
		{
			if (instance == null)
				return;
			foreach (Piece piece in instance.pieces)
			{
				if (piece.Source != source)
					continue;
				Uncrack(piece);
				piece.Hold.Cancel(Time.time);
			}
		}

		// source 가 남긴 것 중 길 (source 앞 띠) 위의 것에 금을 냄. 지나가는 순간 터짐. 돌려받은 함수로 되돌림
		public Action Crack(UnitObject source, Vector3 direction, float length, float width, float startTime, float speed)
		{
			Vector3 origin = source.transform.position;
			return CrackWhere(source,
				piece => LineMonsterAttack.IsInStrip(origin, direction, length, width, piece.Position, piece.Settings.Radius),
				piece => startTime + Mathf.Max(0f, Vector3.Dot(piece.Position - origin, direction)) / speed);
		}

		// source 둘레에 남긴 것에 금을 냄. 가까운 것부터 speed 로 퍼지며 터짐. 돌려받은 함수로 되돌림
		public Action CrackAround(UnitObject source, float radius, float startTime, float speed)
		{
			Vector3 origin = source.transform.position;
			return CrackWhere(source,
				piece => SlimeKingPatternMath.IsWithin(origin, piece.Position, radius),
				piece => startTime + Vector3.Distance(origin, piece.Position) / speed);
		}

		private Action CrackWhere(UnitObject source, Func<Piece, bool> includes, Func<Piece, float> burstTimeOf)
		{
			List<Piece> cracked = new();
			foreach (Piece piece in pieces)
			{
				if (piece.Source != source || CanCrack(piece) == false || includes(piece) == false)
					continue;
				float burstTime = burstTimeOf(piece);
				Action stop = piece.Settings.BurstTelegraph.Play(new CombatFxContext
				{
					Position = piece.Position,
					Radius = piece.Settings.BurstRadius,
					Seconds = burstTime - Time.time,
				});
				// 예고 판 상한에 걸려 안 보이면 터지지도 않음
				if (GroundTelegraph.IsSkipped(stop))
					continue;
				piece.StopTelegraph = stop;
				piece.BurstTime = burstTime;
				piece.PlainHarmlessTime = piece.Hold.HarmlessTime;
				piece.PlainEndTime = piece.Hold.EndTime;
				piece.Hold.IsCracked = true;
				piece.Hold.EndTime = burstTime;
				if (piece.Settings.Damage > 0)
					piece.Hold.HarmlessTime = burstTime;
				cracked.Add(piece);
			}
			return () =>
			{
				foreach (Piece piece in cracked)
					Uncrack(piece);
			};
		}

		// 이미 식어 사라지는 것은 다시 살리지 않음
		private static bool CanCrack(Piece piece) =>
			piece.Settings.BurstRadius > 0f && piece.Hold.IsCracked == false && piece.Hold.IsGone == false
			&& Time.time < (piece.Settings.Damage > 0 ? piece.Hold.HarmlessTime : piece.Hold.EndTime);

		private static void Uncrack(Piece piece)
		{
			if (piece.Hold.IsGone || piece.Hold.IsCracked == false)
				return;
			piece.StopTelegraph?.Invoke();
			piece.StopTelegraph = null;
			piece.BurstTime = float.PositiveInfinity;
			piece.Hold.IsCracked = false;
			piece.Hold.HarmlessTime = piece.PlainHarmlessTime;
			piece.Hold.EndTime = piece.PlainEndTime;
		}

		private void Update()
		{
			if (pieces.Count == 0 || PlayerProvider.TryGetExistingInstance(out PlayerProvider playerProvider) == false)
				return;
			PlayerObject player = playerProvider.CurrentObject;
			// 터지며 반격으로 주인이 죽으면 목록이 안 바뀌고 다음 프레임에 치움
			for (int i = pieces.Count - 1; i >= 0; i--)
			{
				if (Tick(pieces[i], player) == false)
					pieces.RemoveAt(i);
			}
		}

		// 더 볼 것이 없으면 false
		private bool Tick(Piece piece, PlayerObject player)
		{
			if (piece.Source == null || piece.Source.gameObject.activeInHierarchy == false || piece.Source.IsAlive == false)
			{
				piece.StopTelegraph?.Invoke();
				piece.Hold.Cancel(Time.time);
				return false;
			}
			if (Time.time >= piece.BurstTime)
			{
				Burst(piece, player);
				return false;
			}
			if (piece.Settings.Damage > 0 && Time.time >= nextTouchTime && piece.Hold.IsHot(Time.time) && Touches(piece, player, piece.Settings.Radius))
			{
				nextTouchTime = Time.time + piece.Settings.TouchInterval;
				piece.Settings.HitEffect.Strike(piece.Source, piece.Position, player, MonsterAttack.MakeDamage(piece.Source, piece.Settings.Damage));
			}
			return Time.time < piece.Hold.EndTime;
		}

		private static void Burst(Piece piece, PlayerObject player)
		{
			piece.Hold.IsGone = true;
			piece.Hold.EndTime = Time.time;
			piece.Settings.BurstFx.Play(new CombatFxContext { Position = piece.Position, Origin = piece.Position, Radius = piece.Settings.BurstRadius });
			if (Touches(piece, player, piece.Settings.BurstRadius))
				piece.Settings.BurstHitEffect.Strike(piece.Source, piece.Position, player, MonsterAttack.MakeDamage(piece.Source, piece.Settings.BurstDamage));
		}

		private static bool Touches(Piece piece, PlayerObject player, float radius) =>
			player != null && player.IsAlive && SlimeKingPatternMath.IsWithin(piece.Position, player.transform.position, radius + player.UnitMovement.BodyRadius);
	}
}
