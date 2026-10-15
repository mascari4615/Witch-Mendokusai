# Appearance

Independent surface appearance documents. The core has no engine, editor, storage or game references. The application validates a document before invoking `IAppearanceTarget`; JSON, Unity runtime and editor consumers live in separate assemblies.

Version 1 describes named surface/channel RGBA values with explicit color space (`srgb`, `linear`, or `unspecified`). Names are semantic bindings, not mesh or shader indices. Consumers must resolve their own surface bindings. This initial version is a palette, not a complete character asset; geometry, rigging, deformation and material resources are outside its scope.

Open `WM/Authoring/Appearance` to inspect a document. `AppearancePalette.Load` uses the same validated document in play mode. It stores a snapshot and does not modify rendering or instantiate materials. Documents with uncalibrated color space can be inspected without guessing their visual output.

Future document versions require explicit migration; unknown fields and unsupported versions are rejected. No opaque extension payload is carried by this format.

`ParameterProfile` is a separate engine-independent contract for named numerical controls. Properties declare calibrated baselines and bounds; controls declare their default, current value, range and weighted property influences. `ParameterProfiles.Evaluate` calculates each property from its baseline plus the weighted differences from control defaults. Invalid or out-of-range results are rejected. `WithValues` evaluates an independent copy, so repeated edits do not compound previous results. Scene bindings belong to Scene Content rather than this core.

To run the package's editor tests, add `com.mascari4615.appearance` to the host manifest's `testables` array and select the `Appearance.Tests` assembly. The tests verify validation before side effects, rejected contract changes, serialization and isolated runtime snapshots.
