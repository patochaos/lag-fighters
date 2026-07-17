# Lag Fighters

Fighting game por turnos programados en Unity 6 (6000.5.3f1, URP, Input System). Responder en español rioplatense.

## Cómo trabajar acá

- **Leé `DESIGN.md` antes de tocar gameplay** — ahí está el diseño vigente y su historial de pivots.
- Todo el código vive en `Assets/Scripts/LagFighter/`. La escena no tiene nada: `MatchController.Boot()` (RuntimeInitializeOnLoadMethod) construye arena, peleadores, HUD y menús por código al dar Play. No crear assets de escena ni prefabs sin necesidad.
- `Sim.cs` es **simulación pura y determinista** (sin UnityEngine): toda la lógica de juego va ahí. Views/UI solo leen estado. Esto habilita ghost preview, replay exacto y futuro netcode lockstep — no romper esa separación.
- Verificación sin abrir el editor: compilar con `dotnet build` usando un csproj que referencie `C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data/Managed/UnityEngine/*.dll` y `Library/ScriptAssemblies/` (definir `ENABLE_INPUT_SYSTEM`, LangVersion 9). Hay uno armado en el scratchpad de Claude (`compilecheck/check.csproj`).
- Con el editor abierto, el bridge MCP for Unity permite leer la consola (`read_console`) — usarlo después de cada cambio grande.
- Un commit por bloque de trabajo terminado.
