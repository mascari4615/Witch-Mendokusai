# Dungeon card flow harness

Run `dotnet run --project Tools/survival-harness/SurvivalHarness.csproj` from the repository root.

Compiles the shipped CardManager, DungeonContext, DungeonRecorder and ExpManager sources directly. Exercises empty equipment, exhausted upgrades, queued levels, reset during the selection delay, duplicate card IDs and repeated selection.

Unity scheduling, UI services, game data and audio use explicit substitutes in Stubs.cs. The scheduler advances each routine through its initial yield and a requested next step. Eighteen checks cover card flow, selection cancellation without erasing result cards, reached level, positive elapsed time and synchronous experience bursts with increasing level costs, boss overtime, supported difficulty tiers and released UI cleanup. Unity import, serialization, UI focus, actual time ownership, scene transitions and a complete dungeon run still require the editor.
