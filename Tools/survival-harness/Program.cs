using System;
using System.Collections.Generic;
using System.Reflection;
using WitchMendokusai;

internal static class Program
{
    private sealed class Fixture
    {
        public readonly CardManager Manager = new CardManager();
        public readonly SOManager Data = new SOManager();
        public readonly DataManager Player = new DataManager();
        public readonly TimeManager Time = new TimeManager();
        public readonly UISelectDeck Deck = new UISelectDeck();
        public readonly UISelectCard Cards = new UISelectCard();

        public Fixture(params EquipmentData[] equipment)
        {
            Player.Equipment.AddRange(equipment);
            Manager.Construct(new GameEventManager(), Time, Data, Player, new UIManager());
            Manager.Panels[CardPanelType.SelectDeck] = Deck;
            Manager.Panels[CardPanelType.SelectCard] = Cards;
            Manager.Reset();
        }

        public void Offer() { Manager.LevelUp(); Manager.Advance(); }
        public int Pending => (int)typeof(CardManager).GetField("levelUpStack", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Manager);
    }

    private static EquipmentData Equipment(int id, params CardData[] cards)
        => new EquipmentData { ID = id, EffectCards = new List<CardData>(cards) };
    private static CardData Card(int id, int maximum = 1) => new CardData { ID = id, MaxStack = maximum };
    private static void Check(bool condition, string message)
    {
        if (condition == false) { throw new InvalidOperationException(message); }
    }

    private static int Main()
    {
        int failures = 0;
        Dictionary<string, Action> cases = new Dictionary<string, Action>
        {
            ["empty first deck still offers another deck"] = () =>
            {
                Fixture fixture = new Fixture(Equipment(0), Equipment(1, Card(10)), null, null);
                fixture.Offer();
                Check(fixture.Manager.CurPanelType == CardPanelType.SelectDeck, "other usable equipment never offered");
                Check(fixture.Deck.Offered.Count == 1 && fixture.Deck.Offered[0].ID == 1, "empty or missing equipment offered");
                fixture.Manager.SelectDeck(0);
                Check(fixture.Cards.Offered.Count == 1, "single remaining card missing");
            },
            ["reset clears queued levels and stale animation"] = () =>
            {
                Fixture fixture = new Fixture(Equipment(0, Card(10)), null, null, null);
                fixture.Manager.LevelUp();
                fixture.Manager.LevelUp();
                fixture.Manager.Reset();
                fixture.Manager.Advance();
                Check(fixture.Pending == 0 && fixture.Time.Paused == false, "queued levels survived reset");
                Check(fixture.Manager.CurPanelType == CardPanelType.None, "old animation reopened panel");
                fixture.Offer();
                Check(fixture.Manager.CurPanelType == CardPanelType.SelectDeck, "new run cannot level up");
            },
            ["exhausted cards release all queued levels"] = () =>
            {
                Fixture fixture = new Fixture(Equipment(0), null, null, null);
                fixture.Manager.LevelUp();
                fixture.Manager.LevelUp();
                fixture.Manager.Advance();
                Check(fixture.Pending == 0 && fixture.Time.Paused == false, "exhausted draft left pending levels");
                Check(fixture.Manager.CurPanelType == CardPanelType.None, "empty picker left open");
            },
            ["duplicate ids and capped cards cannot enter offers"] = () =>
            {
                CardData capped = Card(10);
                Fixture fixture = new Fixture(Equipment(0, capped, Card(11), Card(11), null, Card(12, 0)), null, null, null);
                fixture.Data.SelectedCardBuffer.Add(capped);
                fixture.Offer();
                fixture.Manager.SelectDeck(0);
                Check(fixture.Cards.Offered.Count == 1 && fixture.Cards.Offered[0].ID == 11, "invalid or duplicate candidates offered");
            },
            ["queued levels finish when final upgrade is selected"] = () =>
            {
                CardData card = Card(10);
                Fixture fixture = new Fixture(Equipment(0, card), null, null, null);
                fixture.Manager.LevelUp();
                fixture.Manager.LevelUp();
                fixture.Manager.Advance();
                fixture.Manager.SelectDeck(0);
                fixture.Manager.SelectCard(card);
                fixture.Manager.Advance();
                Check(fixture.Pending == 0 && fixture.Time.Paused == false, "last available upgrade trapped next level");
                Check(fixture.Data.SelectedCardBuffer.Data.Count == 1, "upgrade applied incorrect count");
            },
            ["repeated click cannot grant an upgrade twice"] = () =>
            {
                CardData card = Card(10, 5);
                Fixture fixture = new Fixture(Equipment(0, card), null, null, null);
                fixture.Offer();
                fixture.Manager.SelectDeck(0);
                fixture.Manager.SelectCard(card);
                fixture.Manager.SelectCard(card);
                Check(fixture.Data.SelectedCardBuffer.Data.Count == 1 && fixture.Pending == 0, "closed picker accepted second click");
            }
        };
        cases.Add("result elapsed time is positive", () =>
        {
            DataManager data = new DataManager();
            DungeonContext context = new DungeonContext(TimeSpan.FromMinutes(10), new List<DungeonConstraint>(), data.DungeonStat);
            DungeonManager dungeon = new DungeonManager { Context = context };
            DungeonRecorder recorder = new DungeonRecorder(dungeon, data);
            for (int i = 0; i < 20; i++) { context.UpdateTime(); }
            data.DungeonStat[DungeonStatType.MONSTER_KILL] = 5;
            DungeonRecord result = recorder.CaptureResultRecord();
            Check(result.PlayTime == TimeSpan.FromSeconds(2), "countdown produced negative elapsed time");
            Check(result.KillCount == 5, "run kill difference incorrect");
        });
        cases.Add("a new run measures only its own elapsed time", () =>
        {
            DataManager data = new DataManager();
            DungeonContext context = new DungeonContext(TimeSpan.FromMinutes(10), new List<DungeonConstraint>(), data.DungeonStat);
            DungeonManager dungeon = new DungeonManager { Context = context };
            for (int i = 0; i < 20; i++) { context.UpdateTime(); }
            DungeonRecorder recorder = new DungeonRecorder(dungeon, data);
            for (int i = 0; i < 10; i++) { context.UpdateTime(); }
            Check(recorder.CaptureResultRecord().PlayTime == TimeSpan.FromSeconds(1), "new recording inherited elapsed time");
        });
        cases.Add("result captures the reached level", () =>
        {
            DataManager data = new DataManager();
            DungeonManager dungeon = new DungeonManager { Context = new DungeonContext(TimeSpan.FromMinutes(3), new List<DungeonConstraint>(), data.DungeonStat) };
            DungeonRecorder recorder = new DungeonRecorder(dungeon, data);
            Check(recorder.CaptureResultRecord(7).Level == 7, "result lost reached level");
        });
        cases.Add("ending a run cancels selection without erasing its cards", () =>
        {
            Fixture fixture = new Fixture(Equipment(0, Card(10, 3)));
            fixture.Data.SelectedCardBuffer.Add(Card(11));
            fixture.Manager.LevelUp();
            fixture.Manager.LevelUp();
            fixture.Manager.CancelSelection();
            fixture.Manager.Advance();
            Check(fixture.Pending == 0 && fixture.Time.Paused == false, "end left queued selection or pause");
            Check(fixture.Manager.CurPanelType == CardPanelType.None, "selection reopened after end");
            Check(fixture.Data.SelectedCardBuffer.Data.Count == 1, "result lost selected cards");
        });
        foreach (int amount in new[] { 30, 90, 300, 350 })
        {
            int experience = amount;
            cases.Add("experience burst consumes increasing costs " + experience, () =>
            {
                PlayerProvider provider = new PlayerProvider();
                GameEventManager events = new GameEventManager();
                ExpManager exp = new ExpManager();
                exp.Construct(provider, events, new ObjectPoolManager());
                typeof(ExpManager).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(exp, null);
                provider.Current.UnitStat[UnitStatType.EXP_CUR] = experience;
                int remaining = experience;
                int required = 30;
                int levels = 0;
                while (remaining >= required) { remaining -= required; required += 30; levels++; }
                Check(provider.Current.UnitStat[UnitStatType.LEVEL_CUR] == levels, "burst granted wrong level count");
                Check(provider.Current.UnitStat[UnitStatType.EXP_CUR] == remaining, "burst left wrong experience");
                Check(provider.Current.UnitStat[UnitStatType.EXP_MAX] == required, "burst left wrong next threshold");
                Check(events.Raised == levels, "level notifications mismatched");
            });
        }
        foreach (KeyValuePair<string, Action> testCase in cases)
        {
            try { testCase.Value(); Console.WriteLine("PASS " + testCase.Key); }
            catch (Exception exception) { failures++; Console.WriteLine("FAIL " + testCase.Key + ": " + exception.Message); }
        }
        Console.WriteLine("cases=" + cases.Count + " failed=" + failures);
        return failures == 0 ? 0 : 1;
    }
}
