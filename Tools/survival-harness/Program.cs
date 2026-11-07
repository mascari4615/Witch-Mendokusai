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
    private static CardData Card(int id, int maximum = 1)
    {
        CardData card = new CardData { ID = id };
        typeof(CardData).GetProperty(nameof(CardData.MaxStack)).SetValue(card, maximum);
        return card;
    }
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
        cases.Add("boss overtime keeps countdown at zero and total time advancing", () =>
        {
            DataManager data = new DataManager();
            DungeonContext context = new DungeonContext(TimeSpan.FromMinutes(10), new List<DungeonConstraint>(), data.DungeonStat);
            for (int i = 0; i < 6020; i++) context.UpdateTime();
            Check(context.DungeonCurTime == TimeSpan.Zero, "boss countdown became negative");
            Check(data.DungeonStat[DungeonStatType.DUNGEON_TIME] == 602, "boss overtime disappeared from run time");
        });
        cases.Add("late survival difficulty stays within supported hard tier", () =>
        {
            DataManager data = new DataManager();
            DungeonContext context = new DungeonContext(TimeSpan.FromMinutes(10), new List<DungeonConstraint>(), data.DungeonStat);
            for (int i = 0; i < 6000; i++) context.UpdateTime();
            context.UpdateDifficulty();
            Check(context.CurDifficulty == DungeonDifficulty.Hard, "ten minute difficulty is outside enum tiers");
        });
        cases.Add("result includes time spent fighting after survival countdown", () =>
        {
            DataManager data = new DataManager();
            DungeonContext context = new DungeonContext(TimeSpan.FromMinutes(10), new List<DungeonConstraint>(), data.DungeonStat);
            DungeonRecorder recorder = new DungeonRecorder(new DungeonManager { Context = context }, data);
            for (int i = 0; i < 6050; i++) context.UpdateTime();
            Check(recorder.CaptureResultRecord(5).PlayTime == TimeSpan.FromSeconds(605), "boss fight time missing from result");
        });
        cases.Add("selection cancellation tolerates a released deck view", () =>
        {
            Fixture fixture = new Fixture(Equipment(0, Card(10)));
            fixture.Manager.LevelUp();
            Dictionary<int, UIDeck> views = (Dictionary<int, UIDeck>)typeof(CardManager).GetField("deckUIDic", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture.Manager);
            views[0] = null;
            fixture.Manager.CancelSelection();
            Check(fixture.Pending == 0 && fixture.Time.Paused == false, "released view blocked cleanup");
        });
        cases.Add("advanced card unlocks only after every prerequisite", () =>
        {
            CardData count = Card(400, 10);
            CardData damage = Card(401, 100);
            CardData advanced = Card(410);
            advanced.Requirements.Add(new CardData.Requirement { Card = count, Stack = 3 });
            advanced.Requirements.Add(new CardData.Requirement { Card = damage, Stack = 2 });
            Fixture fixture = new Fixture(Equipment(0, count, damage, advanced));
            fixture.Data.SelectedCardBuffer.Add(count);
            fixture.Data.SelectedCardBuffer.Add(count);
            fixture.Data.SelectedCardBuffer.Add(count);
            fixture.Data.SelectedCardBuffer.Add(damage);
            fixture.Offer();
            fixture.Manager.SelectDeck(0);
            Check(fixture.Cards.Offered.Contains(advanced) == false, "missing prerequisite ignored");
            fixture.Manager.CancelSelection();
            fixture.Data.SelectedCardBuffer.Add(damage);
            fixture.Offer();
            fixture.Manager.SelectDeck(0);
            Check(fixture.Cards.Offered.Contains(advanced), "eligible advanced card not offered");
            fixture.Manager.SelectCard(advanced);
            fixture.Offer();
            fixture.Manager.SelectDeck(0);
            Check(fixture.Cards.Offered.Contains(advanced) == false, "one-time card offered twice");
            fixture.Manager.Reset();
            fixture.Offer();
            fixture.Manager.SelectDeck(0);
            Check(fixture.Cards.Offered.Contains(advanced) == false, "previous run unlock leaked");
        });
        cases.Add("locked-only decks release selection and invalid requirements stay locked", () =>
        {
            CardData locked = Card(410);
            locked.Requirements.Add(new CardData.Requirement { Card = Card(400), Stack = 1 });
            Fixture fixture = new Fixture(Equipment(0, locked));
            fixture.Offer();
            Check(fixture.Pending == 0 && fixture.Time.Paused == false, "locked deck trapped level selection");
            locked.Requirements.Clear();
            locked.Requirements.Add(new CardData.Requirement { Card = null, Stack = 1 });
            Check(locked.MeetsRequirements(fixture.Data.SelectedCardBuffer.Data) == false, "broken asset unlocked");
        });
        cases.Add("choice preview tracks each prerequisite without spending the choice", () =>
        {
            CardData count = Card(400, 10);
            count.Name = "검 수";
            count.Description = "검 수 +1";
            CardData damage = Card(401, 100);
            damage.Name = "검 피해";
            CardData advanced = Card(410);
            advanced.Name = "검 진화";
            advanced.Requirements.Add(new CardData.Requirement { Card = count, Stack = 3 });
            advanced.Requirements.Add(new CardData.Requirement { Card = damage, Stack = 2 });
            List<CardData> selected = new List<CardData> { count, count, damage };
            List<CardData> deck = new List<CardData> { count, damage, advanced };
            string preview = CardChoiceText.Description(count, selected, deck, true);
            Check(preview.Contains("강화 2 → 3 / 10회"), "next stack missing");
            Check(preview.Contains("검 진화 조건 3 → 4 / 5회"), "unlock projection missing");
            Check(preview.Contains("검 피해: 현재 1/2회"), "other prerequisite missing");
            Check(selected.Count == 3 && advanced.MeetsRequirements(selected) == false, "preview spent a choice");
            selected.Add(count);
            Check(CardChoiceText.Description(count, selected, deck).Contains("조건 4 → 4 / 5회"), "surplus count bypassed damage requirement");
            Check(CardChoiceText.Description(damage, selected, deck).Contains("조건 4 → 5 / 5회"), "finishing choice not explained");
            selected.Add(damage);
            selected.Add(advanced);
            Check(CardChoiceText.Description(count, selected, deck).Contains("검 진화") == false, "spent advanced hint remained");
            selected.Clear();
            Check(CardChoiceText.Description(count, selected, deck).Contains("조건 0 → 1 / 5회"), "new run retained old progress");
        });
        cases.Add("choice preview handles unrelated and invalid unlocks", () =>
        {
            CardData card = Card(10, 5);
            CardData invalid = Card(410);
            invalid.Name = "invalid";
            invalid.Requirements.Add(new CardData.Requirement { Card = card, Stack = 1 });
            invalid.Requirements.Add(new CardData.Requirement { Card = null, Stack = 1 });
            string preview = CardChoiceText.Description(card, null, new List<CardData> { null, invalid });
            Check(preview.Contains("강화 0 → 1 / 5회") && preview.Contains("invalid") == false, "invalid unlock advertised");
            Check(CardChoiceText.Description(null, null, null) == string.Empty, "empty slot retained text");
        });
        foreach (KeyValuePair<string, Action> testCase in cases)
        {
            try { testCase.Value(); Console.WriteLine("PASS " + testCase.Key); }
            catch (Exception exception) { failures++; Console.WriteLine("FAIL " + testCase.Key + ": " + exception.Message); }
        }
        Console.WriteLine("cases=" + cases.Count + " failed=" + failures);
        return failures == 0 ? 0 : 1;
    }
}
