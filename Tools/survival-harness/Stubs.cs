using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public enum FindObjectsInactive { Include }
    public sealed class GameObject { public readonly Transform transform = new Transform(); public void SetActive(bool active) { } }
    public class Transform { public object position; }
    public sealed class Coroutine
    {
        public IEnumerator Routine;
        public bool Stopped;
    }
    public class MonoBehaviour
    {
        public readonly GameObject gameObject = new GameObject();
        public readonly Transform transform = new Transform();
        public readonly List<Coroutine> Routines = new List<Coroutine>();
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            Coroutine coroutine = new Coroutine { Routine = routine };
            Routines.Add(coroutine);
            routine.MoveNext();
            return coroutine;
        }
        public void StopCoroutine(Coroutine coroutine) => coroutine.Stopped = true;
        public void StopAllCoroutines()
        {
            foreach (Coroutine coroutine in Routines) { coroutine.Stopped = true; }
        }
        public void Advance()
        {
            foreach (Coroutine coroutine in Routines.ToArray())
            {
                if (coroutine.Stopped == false && coroutine.Routine.MoveNext() == false)
                {
                    coroutine.Stopped = true;
                }
            }
        }
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive) => Array.Empty<T>();
        public static T FindAnyObjectByType<T>(FindObjectsInactive inactive) where T : new() => new T();
    }
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }
    public sealed class WaitForSecondsRealtime { public WaitForSecondsRealtime(float seconds) { } }
    public static class Debug { public static void LogWarning(string message) { } }
    public static class Random
    {
        private static readonly System.Random generator = new System.Random(7);
        public static int Range(int minimum, int maximum) => generator.Next(minimum, maximum);
    }
}
namespace FMODUnity { public static class RuntimeManager { public static void PlayOneShot(string name, object position) { } } }
namespace TMPro { }
namespace UnityEngine.UI { }
namespace DG.Tweening { }
namespace VContainer { [AttributeUsage(AttributeTargets.Method)] public sealed class InjectAttribute : Attribute { } }

namespace WitchMendokusai
{
    public static class SOHelper { }
    public enum DungeonStatType { DUNGEON_TIME, MONSTER_KILL, BOSS_KILL }
    public enum GameStatType { NYANG }
    public sealed class DungeonConstraint { }
    public sealed class DungeonStat
    {
        private readonly Dictionary<DungeonStatType, int> values = new Dictionary<DungeonStatType, int>();
        public int this[DungeonStatType key]
        {
            get => values.TryGetValue(key, out int value) ? value : 0;
            set => values[key] = value;
        }
    }
    public sealed class DungeonManager { public DungeonContext Context; }
    public class CardData { public int ID; public int MaxStack = 1; }
    public class EquipmentData { public int ID; public List<CardData> EffectCards = new List<CardData>(); }
    public class CardBuffer
    {
        public readonly List<CardData> Data = new List<CardData>();
        public void Add(CardData card) => Data.Add(card);
        public bool Remove(CardData card) => Data.Remove(card);
    }
    public class SOManager { public CardBuffer SelectedCardBuffer = new CardBuffer(); }
    public class DataManager
    {
        public int CurDollID;
        public readonly DungeonStat DungeonStat = new DungeonStat();
        public readonly Dictionary<GameStatType, int> GameStat = new Dictionary<GameStatType, int> { [GameStatType.NYANG] = 0 };
        public List<EquipmentData> Equipment = new List<EquipmentData>();
        public List<EquipmentData> GetEquipmentData(int dollId) => Equipment;
    }
    public enum GameEventType { OnLevelUp }
    public class GameEventManager
    {
        public int Raised; public void Raise(GameEventType eventType) { Raised++; } public void RegisterCallback(GameEventType eventType, Action callback) { }
        public void UnregisterCallback(GameEventType eventType, Action callback) { }
    }
    public class TimeManager
    {
        public bool Paused;
        public void Pause(object owner) => Paused = true;
        public void Resume(object owner) => Paused = false;
    }
    public class UIManager { }
    public interface IPanel { void Init(CardManager manager); void SetActive(bool active); }
    public class UIPanelGroup<T> : UnityEngine.MonoBehaviour
    {
        public virtual bool CanBeClosedByCancelInput => true;
        public virtual T DefaultPanel => default;
        public T CurPanelType;
        public readonly Dictionary<T, IPanel> Panels = new Dictionary<T, IPanel>();
        public virtual void Init() { }
        protected virtual void Start() { }
        protected void SetUIManager(UIManager manager) { }
        public void SetPanel(T panel) => CurPanelType = panel;
    }
    public sealed class UISelectDeck : IPanel
    {
        public List<EquipmentData> Offered;
        public void Init(CardManager manager) { }
        public void SetActive(bool active) { }
        public void SetDeckSelectButtons(List<EquipmentData> equipment) => Offered = equipment;
    }
    public sealed class UISelectCard : IPanel
    {
        public List<CardData> Offered;
        public void Init(CardManager manager) { }
        public void SetActive(bool active) { }
        public void SetCardSelectButtons(List<CardData> cards) => Offered = cards;
    }
    public sealed class UIDeck
    {
        public EquipmentData EquipmentData;
        public readonly UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
        public void Init() { }
        public void SetCards(List<CardData> cards) { }
        public void UpdateUI() { }
    }
}

namespace WitchMendokusai
{
    public enum UnitStatType { EXP_CUR, EXP_MAX, LEVEL_CUR }
    public sealed class UnitStat
    {
        private readonly Dictionary<UnitStatType, int> values = new Dictionary<UnitStatType, int>();
        private readonly Dictionary<UnitStatType, Action> listeners = new Dictionary<UnitStatType, Action>();
        public int this[UnitStatType key]
        {
            get => values.TryGetValue(key, out int value) ? value : 0;
            set { values[key] = value; if (listeners.TryGetValue(key, out Action listener)) listener(); }
        }
        public void AddListener(UnitStatType key, Action listener) { listeners[key] = listener; }
    }
    public sealed class PlayerProvider
    {
        public readonly HarnessPlayer Current = new HarnessPlayer();
    }
    public sealed class HarnessPlayer
    {
        public readonly UnitStat UnitStat = new UnitStat();
        public readonly UnityEngine.Transform transform = new UnityEngine.Transform();
    }
    public sealed class ObjectPoolManager
    {
        public UnityEngine.GameObject Spawn(UnityEngine.GameObject prefab) => new UnityEngine.GameObject();
    }
}