# Lag Fighters — Diseño

## Concepto vigente (2026-07-17): Footsies por turnos programados

2D de vista lateral (bloques 3D). Cada turno, **ambos jugadores arman en pausa
una cola de comandos de hasta 60 frames (1s @ 60fps)** y después las dos colas
se ejecutan **simultáneamente** en tiempo real. Turno corto = 1-2 decisiones
por turno, casi YOMIH puro (arrancó en 240f; se acortó el 2026-07-17).
Inspiración: *Footsies* (pocos botones, distancia, whiff punish, frame
advantage) + *Your Only Move Is HUSTLE* (framedata visible, ghost, replay).

### El mindgame central

- Planificás a ciegas: no ves la cola del rival hasta que se ejecuta.
- Si te pegan, tu comando actual se **cancela** y tu cola se desfasa; lo que no
  llegó a ejecutarse **se pierde al final del turno** ("perdió N órdenes").
- El hitstun/knockdown **se arrastra al turno siguiente**: arrancás -Nf y el
  prompt lo muestra ("VENTAJA +50f, rival derribado") → okizeme natural.
- Counter hit: pegarle a alguien en el startup de su ataque = +1 daño y más stun.

### Comandos (16) — `MoveCatalog` en `Sim.cs` (sabor Ryu vs Ken)

Balanceado contra la framedata real de ST Ryu (supercombo.gg, 2026-07-17):

| # | Comando | Frames (S/A/R) | Notas |
|---|---------|------------|-------|
| 1 | Caminar + | 20f | +0.55, NO bloquea |
| 2 | Caminar − | 20f | −0.38 (atrás más lento, como SF2), **bloquea** |
| 3 | Dash + | 16f | +1.0, no bloquea |
| 4 | Dash − | 16f | −1.0, el bait, no bloquea |
| 5 | Salto + | 6/28/10 | +1.9, aéreo 6..34, **patada de jump-in** (hit 20..30, ~+3 hit / −8 block) |
| 6 | Salto N | 6/28/6 | vertical, **patada al caer** (hit 18..30, corta: el wakeup que pega) |
| 7 | Salto − | 6/28/6 | −1.9 |
| 8 | Golpe A | 6/4/14 | 1 dmg, hs20/bs13 → **+2 on hit / −5 on block** (jab de ST: +4/+2) |
| 9 | Patada B | 16/6/30 | 2 dmg, soft KD 42f, bs26 → **−10 on block** (sweep ST: −9) |
| 10 | Hadouken | 14/2/44 = **60f** | ocupa el turno ENTERO; el salto lo castiga (ST: 52-54f, acá nerf extra a pedido) |
| 11 | Shoryuken | 4/8/32 = 44f | **invuln 1..10** (vulnerable subiendo, como N.Ryu), hard KD 60f, −17 block (ST jab DP: 44f, invuln 1-8, −18) |
| 12 | Esperar | 12f | neutral, **bloquea** (no meter órdenes = bloquear) |
| 13 | Tatsumaki | 12/18/16 = 46f | viaja +1.6, 2 hits, el 2° derriba; **atraviesa hadoukens** (girando 8..40); hitbox baja: los saltos la pasan |
| 14 | Agarre | 6/4/20 = 30f | **rompe guardia**, tira 1.2 + KD 45f; los saltos y caídos lo ignoran; **agarre vs agarre = TECH** |
| 15 | Agacharse (OFF) | 14f | **bloquea** con hurtbox 0.9: jab y hadouken **pasan por arriba**; sweep/baja/agarre pegan. Desactivado: `SimConfig.CrouchEnabled` |
| 16 | Patada baja (OFF) | 8/4/16 | 1 dmg, pega BAJO, **+2 hit / −3 block**, agachado todo el move. Desactivado con el agachado |

Golpe aéreo = hard KD 60f (un turno entero de okizeme — vigilar si es mucho).
Shoryuken: invuln real frames 1–10 (fix 2026-07-17: el primer frame estaba
vulnerable por off-by-one; lo pescó el test de framedata).

### Sistemas agregados el 2026-07-17 (plan de mejoras)

- **Esquina real**: el pushback que aplasta al defensor contra la pared se
  transfiere al atacante (bloqueado, conectado y crush), como en SF.
- **Wakeup options**: derribado al planificar elegís RÁPIDO (−16f de KD) o
  QUEDARSE (+16f, baitea el meaty). Elección secreta hasta ejecutar, va al
  turn log (replay determinista); la IA elige 65% rápido.
- **Counter hit visible**: flash naranja largo + cartel "¡COUNTER!".
- **Pérdida de miembros** — implementada y **DESACTIVADA a pedido**
  (2026-07-17, mismo día): daño localizado por altura (bajo `LimbSplitY`=1.0
  → pierna; arriba → brazo), 3 HP por miembro; sin brazo ni A ni Hadouken;
  sin pierna ni B/Tatsu/baja, aéreas no salen, velocidad 65%; el bloque del
  rig desaparece; órdenes huérfanas degradan a Esperar. **Para reactivar:
  `SimConfig.LimbsEnabled = true`** (los tests se reactivan solos). Ídem
  agachado: `SimConfig.CrouchEnabled` + descomentar cartas en
  `PlanMenuUI.Order` y opciones en `SimpleAI`.
- **UX de lectura**: velocidad de playback ×0.5/×1/×2 (solo presentación),
  resumen post-turno en el prompt ("pegaste N · recibiste M · perdiste K
  órdenes"), log de turnos lateral colapsable (tecla L).
- **Presentación**: KO en cámara lenta, hit-sparks de cubitos, trails en
  frames activos, intro "ROUND N — ¡PELEA!", stage con skyline + público de
  bloques, announcer SOLO en KO/guard crush con toggle VOZ.

### Lag Mode

Menú inicial en dos pasos: primero NORMAL o **LAG MODE**. En Lag Mode, cada 3
turnos los frames del turno suben 50%: 60 → 90 → 135 → 202 → 303 (cap; se
suavizó el 2026-07-17: duplicar cada 4 era demasiado brusco).
Al subir aparece el cartel ("IT GETS LAGGIER…", "EL WIFI ESTÁ LLORANDO",
"MODO DIAL-UP", "PALOMA MENSAJERA") y un indicador de wifi arriba que pierde
barras y termina parpadeando en rojo (con "ping" falso). La timeline re-escala
(RowW / CurrentTurnFrames). El lag level se resetea por round.

### Presentación / testeo

- Rounds al mejor de 3 (marcadores dorados junto a los pips; V repite el
  último round, R es revancha).
- Juice: hitstop cosmético (pausa el avance de ticks, NO toca la sim),
  screen shake (`CameraFX`) y sonidos sintetizados en runtime (`SfxLib`,
  cero assets).
- Botón CAJAS ON/OFF (o tecla H) para hurt/hitboxes; indicador de
  distancia bajo el prompt.
- Menú de planificación: grilla 7x2 (movimiento arriba, acción abajo) con
  franja de color por categoría y mini-barra S/A/R por carta.
- **Lab de balance**: harness AI vs AI headless en `Tools/SimHarness` —
  compila Sim.cs+SimpleAI.cs sin Unity y corre miles de peleas con stats por
  movimiento (usos, conecta%, dmg/uso, crushes, techs). Usarlo tras cada
  cambio de framedata. `Tools/SimTests` tiene los tests de framedata y
  `Tools/verify.ps1` corre todo junto.

- **Guardia automática (sin botón)**: bloqueás en neutral, esperando o caminando
  atrás, en el piso. Bloquear = BLOCKSTUN (te come turno). En el aire y en
  dash/walk-forward NO se bloquea.
- **Guard gauge** (2026-07-17): barra de 100 por jugador. Cada bloqueo la come
  según el golpe: A −15 · B −30 · patadas aéreas −15 · hadouken −20 ·
  shoryu −35 · tatsu −15/hit. Regenera ~6/seg SOLO cuando no estás bloqueando
  ni en blockstun. En 0 → **GUARD CRUSH**: stun de 50f sin daño (+~32f de
  ventaja: garantiza un golpe), la barra renace al 50%. El bloqueo queda con
  dos counters: agarre (puntual) y crush (estructural). Verificado en el lab:
  7 jabs bloqueados seguidos = crush; el turtle absoluto pierde ~1 HP por
  ciclo. UI: barrita amarilla bajo los pips (parpadea en rojo <25%) +
  "¡GUARDIA ROTA!" en grande.
- **Estados**: HITSTUN / BLOCKSTUN / KNOCKDOWN con frames visibles en el HUD.
  Cancelan el comando actual, comen turno, y apenas terminan la cola sigue con
  lo que quedaba. Golpe aéreo = knockdown. Counter (pegar en startup) = +1 dmg.
- Feedback muestra frame advantage real de cada intercambio ("+6f"/"−16f").
- HP: 6. Hurtbox 0.7×1.75 (aérea 1.35–2.6; caído 0.55). Proyectil y 0.95–1.28.
- Escenario: línea de ±4.2, separación mínima 0.8 (en el aire se pueden cruzar).
- Menús clickeables con mouse (cartas, botones BORRAR/LISTO, modos).

### Estructura técnica

- `Sim.cs`: simulación pura determinista, 60 ticks/s. `PlanPreview` simula tu
  plan contra rival quieto (ghost + "pegaría N si no se mueve").
- `MatchController.cs`: Planning → Executing → (KO | EndTurn → Planning).
  Log de turnos (`_turnLog`) → **replay completo con V** re-simulando.
- Modos: Práctica (dummy quieto, revive) / VS IA (planifica en secreto) / 1v1
  local hotseat con **picks secretos** (pantalla "pasá el teclado") / **POR
  CÓDIGO**: online asincrónico sin servidores — cada turno se intercambia un
  código corto (`TurnCode`: LF+base64 de lado/turno/wakeup/cola) por chat y
  la sim determinista garantiza que ambos ven la misma pelea.
- UI: cartas con framedata (PlanMenuUI), timelines de 60f con fichas por
  comando y bloque de stun arrastrado al inicio (HudUI) — la fila rival se
  revela al ejecutar. Hurt/hitboxes con toggle (Viz.cs). Blockman procedural
  (FighterView.cs).
- Controles: click o 1-9/0 agrega · flechas+Enter agrega · Backspace borra ·
  Espacio cierra turno · V replay · R reinicia · M menú · H cajas.

### Puerta al 3D (después)

La sim usa rects y posición X; el escenario no está acoplado. Para volver al
3D: X→Vec2, sways laterales, niveles ALTO/MEDIO/BAJO + agacharse, tracking
(ya estuvo implementado en el pivot anterior — ver historial git).

### Futuro anotado

- Pérdida de miembros (hurtboxes por parte del blockman → el rig ya está hecho
  de bloques separados a propósito).
- Picks secretos reales en 1v1 (ocultar ghost del picker 2, o online lockstep
  — la sim determinista ya lo permite).

## Historial de pivots

1. **Delay queue continuo** (4s de delay, timeline deslizante) — descartado.
2. **3D libre estilo Tekken** (lanes primero, luego movimiento 2D libre,
   combos, octágono UFC) — descartado.
3. **YOMIH 3D** (un move por turno, pausa al quedar libre, movelist JACK,
   ALTO/MEDIO/BAJO, tracking, octágono) — descartado como estructura, pero
   varias mecánicas van a volver (niveles, sways, frame advantage visible).
4. **Footsies por turnos programados** ← ACTUAL.
