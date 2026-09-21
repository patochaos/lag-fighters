# Lag Fighters

Fighting game por turnos programados en Unity 6 (URP, Input System). Responder en español rioplatense. `DESIGN.md` gobierna el gameplay.

- Código: `Assets/Scripts/LagFighter/`. `MatchController.Boot()` construye arena, peleadores y UI al dar Play; no depende de escenas/prefabs diseñados manualmente.
- `Sim.cs` contiene toda la lógica pura y determinista, sin UnityEngine. Views/UI solo leen estado; preservar esta separación para preview/replay.
- Verificación de gameplay sin editor: `pwsh Tools/verify.ps1` (compilación y framedata); `-Lab` agrega balance IA vs IA. Tests: `Tools/SimTests`; lab: `Tools/SimHarness`.
- Un commit por bloque de trabajo terminado.
