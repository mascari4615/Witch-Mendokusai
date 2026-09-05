using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class JRPG_UnitInstance : MonoBehaviour
	{
		public static int GOAL = 10000;

		public Unit UnitData => unitData;

		// TODO : Unit.Speed
		public int Speed => speed;
		public bool IsAlly => isAlly;
		public bool IsReady => isReady;
		private bool isReady = false;

		[SerializeField] private int speed;
		[SerializeField] private Unit unitData;
		[SerializeField] private bool isAlly;
		[SerializeField] private int positionIndex;
		[SerializeField] private GameObject spriteMesh;
		[SerializeField] private int unitInstanceID = 0;
		public int UnitInstanceID => unitInstanceID;
		public Sprite UnitSpirte => unitSprite;
		[SerializeField] private Sprite unitSprite;
		public float curActPoint = 0;
		public int CurHP => curHP;
		private int curHP = 0;
		public bool IsAlive => curHP > 0;

		public void NextTick()
		{
			Debug.Log($"{name}, {nameof(NextTick)}");

			curActPoint += speed;

			if (curActPoint >= GOAL)
			{
				Debug.Log($"{name}, {nameof(NextTick)}, IsReady");
				curActPoint %= GOAL;
				isReady = true;
			}
		}

		public void StartTurn(bool isMyTurn)
		{
			if (isMyTurn)
			{
				isReady = false;
			}

			spriteMesh.transform.localScale = Vector3.one * (isMyTurn ? 1.2f : 1f);

			// TODO : 자기 턴 이펙트?
		}
	}
}