# Lag Fighters — Diseño

## Concepto vigente (2026-07-17): Footsies por turnos programados

2D de vista lateral (bloques 3D). Cada turno, **ambos jugadores arman en pausa
una cola de comandos de hasta 240 frames (4s @ 60fps)** y después las dos colas
se ejecutan **simultáneamente** en tiempo real. Inspiración: *Footsies* (pocos
botones, todo es distancia, whiff punish y frame advantage) + *Your Only Move
Is HUSTLE* (planificar viendo framedata, ghost, replay final).

### El mindgame central

- Planificás a ciegas: no ves la cola del rival hasta que se ejecuta.
- Si te pegan, tu comando actual se **cancela** y tu cola se desfasa; lo que no
  llegó a ejecutarse **se pierde al final del turno** ("perdió N órdenes").
- El hitstun/knockdown **se arrastra al turno siguiente**: arrancás -Nf y el
  prompt lo muestra ("VENTAJA +50f, rival derribado") → okizeme natural.
- Counter hit: pegarle a alguien en el startup de su ataque = +1 daño y más stun.

### Comandos (8) — `MoveCatalog` en `Sim.cs`

| # | Comando | Frames (S/A/R) | Notas |
|---|---------|------------|-------|
| 1 | Caminar + | 20f | +0.55 |
| 2 | Caminar − | 20f | −0.5 |
| 3 | Dash + | 16f | +1.0, rápido |
| 4 | Dash − | 16f | −1.0, el bait |
| 5 | Golpe A | 8/4/18 | 1 dmg, corto, hitstun 24 |
| 6 | Patada B | 16/6/30 | 2 dmg, larga, **derriba** (50f) |
| 7 | Guardia | 2..30 bloquea | cola final punisheable |
| 8 | Esperar | 12f | timing |

- HP: 6. KO al llegar a 0. Sin niveles alto/bajo por ahora (ver "Puerta al 3D").
- Hurtbox: rect 0.7×1.75 (0.55 de alto caído). Hitboxes: rects con framedata.
- Escenario: línea de ±4.2, separación mínima 0.8, sin cruzarse.

### Estructura técnica

- `Sim.cs`: simulación pura determinista, 60 ticks/s. `PlanPreview` simula tu
  plan contra rival quieto (ghost + "pegaría N si no se mueve").
- `MatchController.cs`: Planning → Executing → (KO | EndTurn → Planning).
  Log de turnos (`_turnLog`) → **replay completo con V** re-simulando.
- Modos: Práctica (dummy quieto, revive) / VS IA (planifica en secreto) / 1v1
  local hotseat.
- UI: cartas con framedata (PlanMenuUI), timelines de 240f con fichas por
  comando (HudUI) — la fila rival se revela al ejecutar. Hurt/hitboxes siempre
  visibles (Viz.cs). Blockman procedural (FighterView.cs).
- Controles: 1-8 agrega comando · ←/→+Enter agrega · Backspace borra ·
  Espacio cierra turno · V replay · R reinicia · M menú.

### Puerta al 3D (después)

La sim usa rects y posición X; el escenario no está acoplado. Para volver al
3D: X→Vec2, sways laterales, niveles ALTO/MEDIO/BAJO + agacharse, tracking
(ya estuvo implementado en el pivot anterior — ver historial git).

### Futuro anotado

- Pérdida de miembros (hurtboxes por parte del blockman → el rig ya está hecho
  de bloques separados a propósito).
- Picks secretos reales en 1v1 (ocultar ghost del picker 2, o online lockstep
  — la sim determinista ya lo permite).
- Blockstun (hoy bloquear no frena la cola del que bloquea).

## Historial de pivots

1. **Delay queue continuo** (4s de delay, timeline deslizante) — descartado.
2. **3D libre estilo Tekken** (lanes primero, luego movimiento 2D libre,
   combos, octágono UFC) — descartado.
3. **YOMIH 3D** (un move por turno, pausa al quedar libre, movelist JACK,
   ALTO/MEDIO/BAJO, tracking, octágono) — descartado como estructura, pero
   varias mecánicas van a volver (niveles, sways, frame advantage visible).
4. **Footsies por turnos programados** ← ACTUAL.
