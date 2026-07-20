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
- Counter hit: pegarle a alguien en el startup de su ataque = más stun, y +1
  daño SOLO en golpes de 1 (un DP counter de 3/6 HP decidía medio round).

### Comandos (16 en catálogo, 12 en el menú) — `MoveCatalog` en `Sim.cs` (sabor Ryu vs Ken)

Los retirados (OFF) quedan en `MoveCatalog` con sus índices intactos para no
romper replays viejos ni tests; solo salen de `PlanMenuUI.Order` y `SimpleAI`.

Balanceado contra la framedata real de ST Ryu (supercombo.gg, 2026-07-17):

| # | Comando | Frames (S/A/R) | Notas |
|---|---------|------------|-------|
| 1 | Caminar + (OFF) | 20f | +0.55, NO bloquea. **Retirado 2026-07-19**: redundante con Dash + |
| 2 | Bloquear | 20f | −0.38 (ex Caminar −): la defensa base, **bloquea** retrocediendo |
| 3 | Dash + | 16f | +1.0, no bloquea |
| 4 | Dash − | 16f | −1.0, el bait, no bloquea |
| 5 | Salto + | 6/28/10 | +1.9, aéreo 6..34, **patada de jump-in** (hit 20..28; la ventana de 10 daba hasta +11) |
| 6 | Salto N | 6/28/6 | vertical, **patada al caer** (hit 18..30, corta: el wakeup que pega) |
| 7 | Salto − (OFF) | 6/28/6 | −1.9. **Retirado 2026-07-19**: Salto N ya esquiva proyectiles y Dash − retrocede |
| 8 | Jab (ex Golpe A) | 6/4/14 | 1 dmg, hs20/bs13 → **+2 on hit / −5 on block** (jab de ST: +4/+2) |
| 9 | Barrida (ex Patada B) | 16/6/30 | 2 dmg, soft KD 42f, bs26 → **−10 on block** (sweep ST: −9) |
| 10 | Hadouken | 14/2/44 = **60f** | ocupa el turno ENTERO; el salto lo castiga (ST: 52-54f, acá nerf extra a pedido) |
| 11 | Shoryuken | 4/5/32 = 41f | **invuln 1..10**, hard KD 60f, −15 block; **anti-aéreo especializado** (Y 1.0–2.5, alcance 0.75): ya no pega OTG ni domina el suelo (nerf 2026-07-18: 76%→61% en el lab) |
| 12 | Parry | 2/5/5 = 12f | rechaza golpes/proyectiles en f3–7 e interrumpe ataques cuerpo a cuerpo; pierde vs agarre y delay; **no bloquea** |
| 13 | Tatsumaki | 12/18/16 = 46f | viaja +1.6, 2 hits, el 2° derriba; **atraviesa hadoukens** (girando 8..34: el final es castigable con proyectil); hitbox baja: los saltos la pasan |
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
  rig desaparece; órdenes huérfanas se consumen en neutral. **Para reactivar:
  `SimConfig.LimbsEnabled = true`** (los tests se reactivan solos). Ídem
  agachado: `SimConfig.CrouchEnabled` + descomentar cartas en
  `PlanMenuUI.Order` y opciones en `SimpleAI`.
- **UX de lectura**: velocidad de playback ×0.5/×1/×2 (solo presentación),
  resumen post-turno en el prompt ("pegaste N · recibiste M · perdiste K
  órdenes"), log de turnos lateral colapsable (tecla L).
- **Presentación**: KO en cámara lenta, hit-sparks de cubitos, trails en
  frames activos, intro "ROUND N — ¡PELEA!", stage con skyline + público de
  bloques, announcer SOLO en KO/guard crush con toggle VOZ.

### Turno fluido (toggle experimental, 2026-07-19)

`SimConfig.CarryoverEnabled` — tecla **C** en el menú principal (pasos lag/modo),
persiste en PlayerPrefs (`lf_carryover`), **OFF por defecto**. Forzado OFF en
Online/Async (el toggle no viaja en el protocolo → desync).

- ON: el último move planificado puede **cruzar el límite del turno** (basta
  con que arranque adentro). El turno siguiente arranca con esos frames
  **comprometidos**: p.ej. terminás en el aire (okizeme estilo Akuma).
- La info es **honesta y pública**: el rival ve tu move comprometido al
  planificar (prompt "RIVAL comprometido: …", segmento verde-agua en la
  timeline, el ghost lo reproduce). Move largo tarde en el turno =
  telegrafiado; temprano = secreto. Esa es la decisión nueva.
- Un golpe cancela el move como siempre; solo se pierden las órdenes que no
  arrancaron. La IA compromete moves largos un 45% de las veces que su
  presupuesto se queda corto.
- El replay usa el mismo `OnTurnEnd` con el mismo flag → determinista.
- Pendiente si el toggle convence: turnos cortos que se alargan con el lag
  (el horizonte de compromiso COMO mecánica de lag). Ver conversación
  2026-07-19: riesgo de matar al zoner; A/B con el lab antes.

### Guardia = stamina (2026-07-19, anti-tortuga)

La guardia regenera **solo mientras ejecutás un move que no bloquea**
(`Sim.Step`; GuardRegen 0.1 → 0.14 para compensar el uptime menor). Quieto
o bloqueando no cura: en turnos largos "esperar al otro" deja de ser gratis,
pero sin castigo directo — la recompensa va al que llena la barra de órdenes.
El overflow del turno fluido sigue regenerando durante los frames
comprometidos. Alternativas anotadas si no alcanza: medidor de IMPULSO
(stacks por barra llena → +daño en el primer hit), prioridad en trades para
el que más frames comprometió.

### Lag Mode

Menú inicial: primero NORMAL o **LAG MODE**. En Lag Mode, cada 3
turnos los frames del turno suben 50%: 60 → 90 → 135 → 202 → 303 (cap; se
suavizó el 2026-07-17: duplicar cada 4 era demasiado brusco).
Al subir aparece el cartel ("IT GETS LAGGIER…", "EL WIFI ESTÁ LLORANDO",
"MODO DIAL-UP", "PALOMA MENSAJERA") y un indicador de wifi arriba que pierde
barras y termina parpadeando en rojo (con "ping" falso). La timeline re-escala
(RowW / CurrentTurnFrames). El lag level se resetea por round.

**VS IA** entra directo a pelear contra ADAPTIVE en NORMAL (pedido 2026-07-18:
cero fricción para el caso común). **IA CUSTOM** abre el submenú de perfil
(RANDOM, Zoner, Aggressive, Defensive, Trickster o Adaptive) y dificultad
(Fácil, Normal por defecto o Difícil). RANDOM fija un perfil para toda la
partida; Adaptive aprende solo de planes ya revelados y aplica lo observado a
partir del turno siguiente.

### Presentación / testeo

- **Lag teatral del replay** (2026-07-18): la repetición se comporta como un
  stream con mala conexión. Solo maquilla el reloj de playback; la
  re-simulación es idéntica. Flags independientes en `ReplayLagFX`
  (MatchController.cs) para apagar lo que no convenza:
  `Stutter` (congela 0.15–0.55s con "|| LAG...", glitch bars y estática, y
  después corre a ×2.6 hasta recuperar la deuda) · `Choppy` (ratos a ~5 fps,
  a los saltos) · `PingSpike` (ping falso 1800–4800ms en rojo + wifi en
  pánico) · `Rewind` (al descongelar retrocede hasta 6f del mismo turno y los
  re-simula sin re-disparar juice: el teleport de netplay) · `AudioDrop` (el
  audio se ahoga al 30% durante el tirón) · `ScaleWithLag` (en Lag Mode la
  frecuencia/duración escala con el nivel alcanzado en el round) ·
  `Enabled` (master). Durante el replay hay **tres botones grandes arriba al
  medio — LAG / NORMAL / RÁPIDO** (`ReplayViewMode`): con lag teatral, limpio,
  o limpio a ×2; conmutables en vivo y recordados dentro de la sesión.
- Rounds al mejor de 3 (marcadores dorados junto a los pips; V repite el
  último round, R es revancha). **20 turnos por round** (2026-07-18): al
  agotarse → TIME OVER y gana el que tiene más vida (empate posible). El
  prompt muestra TURNO X/20 y avisa en los últimos. Práctica no tiene límite.
- **Parry recarga guardia** (+15 por parry exitoso): la respuesta activa al
  chip de proyectiles — en el lab le bajó la opresividad al zoneo.
- Juice: hitstop cosmético (pausa el avance de ticks, NO toca la sim),
  screen shake (`CameraFX`) y sonidos sintetizados en runtime (`SfxLib`,
  cero assets).
- **Pulido UI/UX/arte (2026-07-18)**: sombra de contacto bajo los peleadores,
  fases S/A/R dentro de las fichas de la timeline, la pared pulsa con el color
  del acorralado, pips de vida que "se rompen" al perderse, hover + blips
  sintetizados en toda la UI (toggle SFX en OPC, persiste), cursor pixel
  procedural, luces de acento celeste/naranja por lado, hadouken con núcleo
  rotante y estela, festejo de KO (saltitos + público eufórico + burst
  dorado), announcer con pitch aleatorio, el menú de modos recuerda la última
  elección (PlayerPrefs), botón LISTO dice "PASAR (quieto, bloquea)" con plan
  vacío, tips de primera vez SOLO en Práctica (se apagan para siempre), y
  tipografía pixel normalizada a 8/16/24/32 px.
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
- **Guard gauge** (2026-07-17, barra achicada 2026-07-18): barra de **70** por
  jugador (con 100 crushear casi no pasaba: 0 crushes en 2000 peleas del lab;
  con 70 hay 46). Cada bloqueo la come según el golpe: A −15 · B −30 ·
  patadas aéreas −15 · hadouken −25 · shoryu −35 · tatsu −15/hit. Regenera
  ~6/seg SOLO cuando no estás bloqueando ni en blockstun. En 0 →
  **GUARD CRUSH**: stun de 50f sin daño (+~32f: golpe garantizado), la barra
  renace al 50% (35). 5 jabs o 3 sweeps bloqueados seguidos = crush. El
  bloqueo queda con dos counters: agarre (puntual) y crush (estructural).
  UI: barrita amarilla bajo los pips (parpadea en rojo <25%).
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
- Modos en el menú: Práctica (dummy quieto, revive) / VS IA (directo contra
  Adaptive Normal) / IA CUSTOM (perfil + dificultad) / **ONLINE**
  (2026-07-18): sala con código de invitación de 4 letras sobre un relay
  Supabase tonto (`NetLobby.cs`, tablas `lf_rooms`/`lf_turns` en el proyecto
  compartido arrow-game) — cada turno se intercambia un `TurnCode` (LF+base64
  de lado/turno/wakeup/cola) por HTTP con polling de 1.5s y la sim
  determinista garantiza que ambos ven la misma pelea. Sin cuentas ni
  matchmaking; la sala persiste (retomable). Sin revancha local
  (desincronizaría): sala nueva.
- **Retirados del menú (2026-07-18, injugables)**: 1v1 local hotseat (picks
  secretos con "pasá el teclado") y POR CÓDIGO (intercambio manual del
  TurnCode por chat). La maquinaria sigue en `MatchController` por si
  vuelven; el `TurnCode` nació ahí y es la base de ONLINE.
- **Timer de planificación de 30s** en ONLINE: al agotarse se manda lo que
  haya (sin órdenes = quieto bloqueando). Práctica/VS IA sin timer. El HUD
  muestra la cuenta y se pone rojo a los 10s.
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
