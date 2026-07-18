# LAG FIGHTERS

**Footsies por turnos programados.** Un fighting game donde nadie aprieta botones
en tiempo real: cada turno, ambos jugadores arman en pausa una cola de comandos
de 1 segundo (60 frames) y después las dos colas se ejecutan **simultáneamente**.
Leer al rival importa más que los reflejos. Y en **LAG MODE**, cada 4 turnos el
lag se duplica hasta que estás jugando por paloma mensajera.

Hecho en Unity 6 (URP) con una simulación pura y determinista en C#.

## Cómo jugar

- **Práctica**: dummy quieto que no bloquea y revive. Para aprender la movelist.
- **VS IA**: la IA planifica en secreto, como vos.
- **1v1 local**: hotseat — planifica el jugador 1, después el 2, y se ejecuta.
- **NORMAL o LAG MODE**: en Lag Mode los turnos se van alargando (60 → 120 →
  240 → 480 → 960 frames). El wifi está llorando.

### Controles

| Tecla | Acción |
|---|---|
| Click / `1-9`,`0` | Agregar carta al plan |
| Flechas + `Enter` | Navegar y agregar |
| `Backspace` | Borrar última orden |
| `Espacio` | Confirmar turno |
| `V` | Replay del último round (re-simulación exacta) |
| `R` | Revancha |
| `M` / `Esc` | Volver al menú |
| `H` | Mostrar/ocultar hurt/hitboxes |

### La regla de oro

Si te pegan, tu comando actual **se cancela**, tu cola se desfasa, y lo que no
llegó a ejecutarse **se pierde**. El stun se arrastra al turno siguiente:
arrancás en desventaja y el rival lo sabe. Bloquear es automático (neutral,
esperar o caminar atrás) pero come **guardia**: la barra amarilla llega a 0 y
es **GUARD CRUSH** — 50 frames de regalo para el rival.

## Correr el proyecto

1. Abrir con **Unity 6000.5.3f1** (URP + Input System).
2. Dar **Play** en cualquier escena vacía: `MatchController.Boot()` construye
   arena, peleadores, HUD y menús por código. No hay nada en la escena a propósito.

**Build standalone**: menú `Lag Fighters → Build para compartir`.

## Documentación

- [`GDD.md`](GDD.md) — game design document completo: sistemas, movelist con framedata, arquitectura.
- [`DESIGN.md`](DESIGN.md) — el diseño vivo y su historial de pivots (leer antes de tocar gameplay).
- [`PLAN.md`](PLAN.md) — plan de mejoras en curso.
- [`CHANGELOG.md`](CHANGELOG.md) — qué cambió en cada push.

## Verificación sin abrir Unity

`Assets/Scripts/LagFighter/Sim.cs` no depende de UnityEngine: la simulación
compila y corre con `dotnet` a secas. Eso habilita el compile-check, los tests
de framedata y el **lab de balance** (miles de peleas IA vs IA con stats por
movimiento) desde la línea de comandos — ver `Tools/`.
