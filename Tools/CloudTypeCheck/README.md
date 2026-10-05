# CloudTypeCheck

The cloud environment has no Unity, so new C# cannot be compiled there. This folder lets a cloud session catch the cheap mistakes (typos, wrong
signatures, bad syntax, missing members) before handing code to Local Claude:

- `UnityStubs.cs` and `EditorStubs.cs` are hand-written, behaviour-free stand-ins for the parts of `UnityEngine`/`UnityEditor` (and the rig classes
  the scene builder configures) that the proving-ground code uses. The *project's own* classes (`GolfBall`, `HoleController`, `Cup`, `CourseGeometry`...)
  are compiled for real, so calls into them are checked against their true signatures.
- `RuntimeAndTests.csproj` checks the obstacle runtime code and `ObstacleProvingGroundTests.cs`. `Editor.csproj` checks `ProvingGroundSceneBuilder.cs`.
- Run (needs the .NET 8 SDK, `apt-get install dotnet-sdk-8.0`; NUnit comes from NuGet):
  `dotnet build Tools/CloudTypeCheck/RuntimeAndTests.csproj` and `dotnet build Tools/CloudTypeCheck/Editor.csproj`.

It proves nothing about behaviour or about how real Unity APIs behave; a stub can be laxer than Unity. When you add a Unity API call, add it to the stubs
with the real signature. Unity itself, run by Local Claude, remains the only compile and test that counts.
