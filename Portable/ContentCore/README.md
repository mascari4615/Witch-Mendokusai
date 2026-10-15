# Portable content core example

Run `dotnet run --project Portable/ContentCore/ContentCore.csproj` from the repository root with the .NET 10 SDK. No Unity installation, engine assembly, model asset or network dependency is required by this project.

It compiles the same Appearance, Geometry, Scene Content, Motion, Rigging and Scene Sequence core sources used by the Unity packages. It executes pose/property interpolation through host ports, rejects an invalid semantic value before a partial frame, evaluates limb rotations against a forward-position check, composes independent scene nodes without modifying source documents, and validates their sequence composition.

This verifies core portability and selected behavior. Unity rendering, asset ownership and game integration are verified separately in the editor and runtime.
