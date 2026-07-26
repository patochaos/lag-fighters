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

### Comandos (17 en catálogo, 12 en la grilla + super) — `MoveCatalog` en `Sim.cs` (sabor Ryu vs Ken)

Los retirados (OFF) quedan en `MoveCatalog` con sus índices intactos para no
romper replays viejos ni tests; solo salen de `PlanMenuUI.Order` y `SimpleAI`.

Balanceado contra la framedata real de ST Ryu (supercombo.gg, 2026-07-17):

| # | Comando | Frames (S/A/R) | Notas |
|---|---------|------------|-------|
| 1 | Caminar + (OFF) | 20f | +0.55, NO bloquea. **Retirado 2026-07-19**: redundante con Dash + |
| 2 | Bloquear | 20f | −0.38 (ex Caminar −): la defensa base, **bloquea** retrocediendo |
| 3 | Dash + | 12f | +1.0, no bloquea, **1 AP** (2026-07-20: 16f→12f, el move barato) |
| 4 | Dash − | 12f | −1.0, el bait, no bloquea, **2 AP** (sobreprecio anti-turtle) |
| 5 | Salto + | 6/28/10 | +1.9, aéreo 6..34, **patada de jump-in** (hit 20..28; la ventana de 10 daba hasta +11) |
| 6 | Salto N | 6/28/6 | vertical, **patada al caer** (hit 18..30, corta: el wakeup que pega) |
| 7 | Salto − (OFF) | 6/28/6 | −1.9. **Retirado 2026-07-19**: Salto N ya esquiva proyectiles y Dash − retrocede |
| 8 | Jab (ex Golpe A) | 6/4/14 | 1 dmg, hs20/bs13 → **+2 on hit / −5 on block** (jab de ST: +4/+2) |
| 9 | Barrida (ex Patada B) | 12/6/30 | 2 dmg, soft KD 42f, bs26 → **−10 on block** (2026-07-20: startup 16→12, baja a 4 AP — costaba lo mismo que un hadouken de turno entero) |
| 10 | Hadouken | 14/2/44 = **60f** | ocupa el turno ENTERO; el salto lo castiga (ST: 52-54f, acá nerf extra a pedido); **alcance 3.0** (2026-07-20: se disipa — zonear de fullscreen whiffea; la Shinku sigue fullscreen) |
| 11 | Shoryuken | 4/5/32 = 41f | **invuln 1..10**, hard KD 60f, −15 block; **anti-aéreo especializado** (Y 1.0–2.5, alcance 0.75): ya no pega OTG ni domina el suelo (nerf 2026-07-18: 76%→61% en el lab) |
| 12 | Parry (OFF clásico) | 2/5/5 = 12f | rechaza golpes/proyectiles en f3–7; **retirado del clásico 2026-07-20** (Bloquear es LA defensa y banca AP) — sigue vivo en YOMI y replays |
| 13 | Tatsumaki | 12/18/16 = 46f | viaja +1.6, 2 hits, el 2° derriba; **atraviesa hadoukens** (girando 8..34: el final es castigable con proyectil); hitbox baja: los saltos la pasan |
| 14 | Agarre | 6/4/14 = 24f | **rompe guardia**, **1.5 dmg** (2026-07-20 bis: 1→1.5 — el depredador del default tiene que pagar, Ley 3), tira 1.2 + KD 45f; los saltos y caídos lo ignoran; **agarre vs agarre = TECH** (recovery 20→14, 2 AP — el mixup anti-tortuga tiene que ser barato) |
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
- **La acción corre al 50%** (2026-07-20, `ClassicPace`): a velocidad real
  no se entendía qué pasó — afecta ejecución y replay (RÁPIDO ×2 = tiempo
  real); el teatro YOMI tiene su propio ritmo. Y durante la planificación
  la fila rival de la timeline RECAPITULA su turno anterior (chips
  atenuadas + "↩ TURNO ANTERIOR"): antes mostraba "? ? ?" — inútil — y lo
  que pasó se esfumaba al abrir el menú.
- **Lag teatral DORMIDO en el replay** (2026-07-20): el replay arranca en
  NORMAL y solo ofrece RÁPIDO; el botón LAG y todos los `ReplayLagFX`
  quedan en el código para cuando vuelva la temática.
- **UX de lectura**: velocidad de playback ×0.5/×1/×2 (solo presentación),
  resumen post-turno en el prompt ("pegaste N · recibiste M · perdiste K
  órdenes"), log de turnos lateral colapsable (tecla L).
- **Presentación**: KO en cámara lenta, hit-sparks de cubitos, trails en
  frames activos, intro "ROUND N — ¡PELEA!", stage con skyline + público de
  bloques, announcer SOLO en KO/guard crush con toggle VOZ.

### ACTION POINTS en el modo clásico (2026-07-20, v2 el mismo día)

Pedido de Patricio: que el turno diga CLARITO qué entra y qué no, y después
(misma fecha, auditoría contra `YOMI-BIBLE.md`) que la ECONOMÍA sea el juego.
`SimConfig.ApEnabled` (ON por defecto), `FramesPerAp`=12.

- **1 AP = 12 frames → el turno de 60f banca 5 AP** (Lag Mode: 90f→7,
  135f→11, 202f→16, 303f→25). Costo por move = `ceil(frames/12)`
  (`MoveDef.ApCost`, con `ApCostExtra` para sobreprecios de diseño — solo
  puede ENCARECER, abaratar rompería la garantía slot=tiempo): **Dash + 1**
  (16f→12f: el move barato) · **Dash − 2** (mismo move, sobreprecio
  anti-turtle: huir tributa) · Bloquear/Jab/**Agarre 2** · Shoryu/Saltos/
  Tatsu/**Barrida 4** · Hadouken/Super 5. Segundo rebalance del día: lo
  ofensivo pesado bajó (agarre 3→2, barrida 5→4 vía framedata) porque
  bloquear+dashear fuera de rango dominaba.
- **Cada move OCUPA su slot entero** (`PaddedTotal`): un dash de 16f reserva
  24f; el sobrante se espera en neutral (bloqueando — el padding no es un
  hueco indefenso). Así los AP nunca mienten sobre los frames: el combate
  (hits, stun, ventaja frame a frame) sigue 100% frame-exacto, los AP solo
  cuantizan presupuesto y secuenciación de la cola propia. Un golpe cancela
  el move Y devuelve el resto del slot (el stun lo reemplaza).
- **Economía persistente** (`ApEconomy` en Sim.cs, pura — la comparten
  MatchController y el harness; biblia Leyes 2/7/9): **ingreso +4 por turno,
  lo no gastado SE GUARDA hasta la BARRA LLENA (tope = capacidad, 5)**,
  arrancás el round a full. El tope era capacidad+2 y se bajó el mismo día:
  con stock > capacidad las bolitas mentían ("tengo 7" pero el turno banca
  5). Gastar los 5 cada turno te deja en turnos de 4; administrar te banca
  turnos llenos. **Bloqueo bancado**: la CARTA Bloquear que bloquea ≥1
  golpe paga +1 AP (el bloqueo automático en neutral defiende pero NO
  banca). El stock es público: la economía ES la información. Nunca toca la
  sim → replay y online deterministas sin viajar en el protocolo.
- **Bug fix del pref fantasma (2026-07-20)**: el toggle C oculto seguía
  CARGANDO su PlayerPref — con el pref viejo prendido, FluidTurn quedaba ON
  y los moves cruzaban el turno "gratis" (tatsus fantasma sin costo, slots
  comprometidos comiendo AP). En modo AP el pref se ignora.
- **OVERFLOW/PRÉSTAMO: DORMIDO** (`SimConfig.ApOverflowEnabled = false`,
  nació y se apagó el 2026-07-20): complejizaba probar si lo BÁSICO es
  disfrutable. El código está entero detrás del flag (y un test lo ejercita
  para que no se pudra); con overflow apagado el turno es ESTRICTO y la
  super vuelve a estar ligada al toggle C legacy (hoy oculto → sin super).
- **REVERSAL** (la válvula anti-vortex, Ley 13): derribado al planificar,
  tercera opción del botón de wakeup — **1 por round, 2 AP**: te levantás YA
  y el empujón separa a 2.4 (`Sim.Reversal`; contra la pared retrocede el
  propio). Viaja en `TurnCode` **v2** (byte wake = trit 0/1/2; los códigos
  v1 se rechazan) y en el turn log → el replay lo re-aplica. La IA lo usa
  (28%/45% según dificultad).
- **El Parry SALIÓ del modo clásico** (sigue en el catálogo para YOMI y
  replays): Bloquear es LA defensa y paga en economía. Ojo anotado: el
  parry era el anti-chip (recargaba guardia) — el zoneo quedó más fuerte;
  vigilar con el lab.
- **Grilla 3×3** (teclas 1-9, anti-clutter): Bloquear · **DASH** · **SALTO**
  / Jab · Barrida · Agarre / Tatsu · Hadouken · Shoryuken. Dash y Salto son
  UNA carta que abre un **mini-picker de dirección** (Dash: adelante/atrás ·
  Salto: adelante/neutro/atrás — Salto − volvió como dirección). Click, 1-3
  o Enter (=adelante); afuera cancela; el ghost actúa la variante hovereada.
- **UI de bolitas** (sprites procedurales de 64px, disco = disponible, aro =
  vacío; también en YOMI): **el stock de AMBOS jugadores vive bajo el panel
  de vida/guardia de cada lado**, siempre visible (tu lado descuenta en vivo
  al planificar; el rival muestra su stock público). Debajo del lado rival,
  el **log de aperturas** ("ABRIÓ: DASH · JAB · —"): las primeras cartas de
  sus últimos 3 planes ya revelados (Ley 5: leer hábitos, no adivinar).
  Costo "N AP" en cada carta y en el panel de info; timeline con rayitas
  cada 12f y hueco de padding visible tras cada ficha.
- Lab post-rebalance (2000 peleas): P0/P1 clavados (832/831), stock
  promedio 4.6/5, 1.16 bloqueos bancados/pelea, dashes bien arriba (1 AP).
- **Sesión de balance 2026-07-20 (noche), 9 experimentos A/B en el lab**
  contra la biblia (detalle en `YOMI-BIBLE.md` §4): quedaron **guardia 55 +
  agarre 1.5 + hadouken con alcance 3.0 + juez por guardia + Defensive
  re-agresivizado** (agarra en corta). Descartados con datos: Bloquear 3 AP
  (no toca el bloqueo gratis en neutral: timeout igual), ingreso +3 (MÁS
  tortuga: planes cortos = más neutral, timeout 42→54%), chip de vida en
  proyectiles (aun a 0.1 regala el matchup Zoner-Defensive: 46→65%). El
  TIME OVER quedó en ~40% (era 42) pero con empates a la mitad (15→10%),
  crushes +45% y la matriz de perfiles pareja: Zoner 54.9 · Aggressive
  51.5 · Defensive 46.3 · Trickster 46.2 · Adaptive 38.7 (baseline:
  Defensive 40 y Aggressive 47 con Zoner igual arriba). El residuo de
  timeout es de espejos pasivos IA-IA (Def-Def sigue en ~20 turnos), no
  del juego humano.

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

### Super: Shinku Hadouken (2026-07-19, solo turno fluido)

Barra dorada (0..`SuperMax`=120) bajo la guardia: **carga con los frames de
overflow** que cruzan el turno (`OnTurnEnd`) — el riesgo de comprometerse es
el combustible. Botón dorado en el menú de planificación (late al llenarse),
una por plan, la barra se gasta al arrancar el move. Reset por round (sim
nueva). La IA la tira a distancia >1.6 con barra llena (55%).

**Shinku** (`MoveCatalog.Super`=16): 14/2/40 = 56f, proyectil 4 dmg, doble
velocidad, 1.8× de ancho (misma altura: **se salta**), hard KD, **arrasa
hadoukens** comunes (super vs super se anulan), el **parry no la rechaza**,
bloquearla come 40 de guardia. Con el toggle OFF la barra no existe (se
oculta en HUD y menú).

Idea anotada para una segunda super: install "SIN LAG" (tus moves −20% de
frames por 2 turnos) — temática pero invasiva en la framedata.

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
  agotarse → TIME OVER y gana el que tiene más vida; **con vida igual
  desempata la GUARDIA restante** (2026-07-20: premia al que atacó — la
  guardia solo regenera atacando — y la tortuga-a-empate pierde el juicio;
  en el lab bajó los empates de 15% a 10%). Empate real solo si vida Y
  guardia empatan. El prompt muestra TURNO X/20 y avisa en los últimos.
  Práctica no tiene límite.
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
- **Guard gauge** (2026-07-17; barra achicada 2026-07-18 y de nuevo
  2026-07-20 tras la auditoría anti-tortuga): barra de **55** por jugador
  (70→55: con 70 el crush era anécdota — 0.14/pelea; con 55 son 0.20-0.27 y
  el 16-21% de las peleas ve al menos uno). Cada bloqueo la come según el
  golpe: A −15 · B −30 · patadas aéreas −15 · hadouken −25 · shoryu −35 ·
  tatsu −15/hit. Regenera ~8/seg SOLO mientras ejecutás moves que no
  bloquean. En 0 → **GUARD CRUSH**: stun de 50f sin daño (+~32f: golpe
  garantizado), la barra renace al ~50% (27). 4 jabs o 2 sweeps bloqueados
  seguidos = crush. El bloqueo queda con dos counters: agarre (puntual) y
  crush (estructural). UI: barrita amarilla bajo los pips (rojo <25%).
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

## Modo YOMI v2 — discreto (2026-07-20)

> Ver **[YOMI-BIBLE.md](YOMI-BIBLE.md)**: las leyes de diseño del género
> (Sirlin, Yomi 1/2, Exceed) destiladas + auditoría de este modo contra ellas.

Sesión de diseño 2026-07-20: el juego muta de "lag" a **yomi** (lectura),
apuntando a un *Yomi Hustle casual*. Research: Sirlin (yomi layers,
"Designing Yomi") — piedra-papel-tijera puro es malo porque no hay
información; lo que vuelve hábil al guessing son **payoffs asimétricos +
estado visible que sesga intenciones**.

**v1 (misma fecha, retirada en horas)**: AP como presupuesto sobre la sim de
frames. Se descartó porque era una quimera: la tabla decía una cosa y los
frames decidían otra por abajo (y el agarre trade-aba con el jab en el mismo
frame). Pivot de Patricio: **dos distancias discretas y resolución por
tabla — la matriz de counters ES la ley**.

### Las reglas (todas)

- **Dos distancias**: CERCA / LEJOS (se arranca LEJOS). **Una acción por
  turno** cada uno, revelación simultánea, resuelve la matriz de `YomiSim`.
- **HP 6** · **AP: arrancás con 3, +1 automático por turno, tope 6**. Los AP
  de ambos son públicos (barrita celeste + "AP n/6" en el HUD): la economía
  ES la información — "está pobre → va a cargar → pegale".
- 8 acciones (la dirección de dash/salto la decide la distancia):

| Acción | AP | Dmg | Qué hace |
|---|---|---|---|
| Jab | 1 | 1 | solo cerca; gana a Kick/Agarre; caza el salto en el despegue |
| Kick | 2 | 2 | AMBAS distancias; caza dashes y cargadores; pierde con Jab (cerca), Salto y Parry |
| Agarre | 2 | 2 | solo cerca; rompe Parry; derriba y manda a LEJOS; pierde con golpes |
| Parry | 1 | (1) | si bloquea un golpe: **+1 AP y devuelve 1** (el rechazo); pierde con Agarre |
| Shoryuken | 3 | 3 | cerca: gana a TODO, derriba a LEJOS · lejos: SOLO lectura antiaérea · **whiff = recovery (perdés el turno siguiente)** |
| Dash | 1 | — | cerca: te vas (esquiva Jab/Agarre/Shoryu; Kick te caza) · lejos: entrás (Kick te frena) |
| Salto | 2 | 1 | cerca: escape aéreo (esquiva Kick/Agarre; Jab te baja) · lejos: entrás con patada (gana a Kick; Parry la bloquea; Shoryu-lectura te baja) |
| Cargar | 0 | — | +2 AP si no te pegan; **todo golpe al que carga es counter (+1)** y cancela la carga |

- Golpe al que está en **recovery** también es counter (+1). Espejos:
  golpes iguales = trade · Agarre vs Agarre = TECH · Shoryu vs Shoryu cerca =
  doble KD · lejos = doble whiff.
- El triángulo interno de cerca: **Jab > Agarre > Parry > Jab** (el parry
  con rechazo cierra el loop — sin el rechazo, el jab spam era gratis).
- Cada escape tiene su cazador: **Kick caza el Dash**, **Jab caza el Salto**.
  Sin esto, retirarse era defensa gratis (no hay esquina con 2 distancias).
- Fin: HP 0 (KO) o 20 turnos (TIME OVER por vida). Rounds al mejor de 3.

### Implementación

- **`YomiSim.cs`**: la matriz, pura y determinista; cada celda es un test en
  `Tools/SimTests` (~25 tests + barrido exhaustivo de celdas legales).
- **Teatro**: `MatchController.BeginYomiTheater` actúa el resultado sobre una
  `MatchSim` fresca por turno (slots X: cerca ±0.45 · lejos ±0.95) con los
  moves clásicos (Kick = Strong 17, Agarre = YomiGrab 18, etc.) y retardos
  de coreografía (`TheaterDelay`) para que los ganadores conecten, los
  perdedores se interrumpan y los whiffs se vean. **El HP real lo dicta
  YomiSim** (sync al final del turno); el teatro es solo presentación.
- UI: la grilla de PlanMenuUI en modo yomi son las 8 acciones — **click =
  jugarla ya** (sin cola, sin LISTO); la tag de cada carta canta su fila de
  la matriz en la distancia actual. IA: `SimpleAI.PickYomi` (pondera por
  distancia/economía + counter-pick del hábito rival observado; Hard lee más).
- **Presentación del turno (pedido 2026-07-20)**: fase de REVELACIÓN — las
  dos cartas GIGANTES con animación de entrada (ease-out-back) + "VS" por
  2.4s antes de que pase nada (espacio/click la apura); después se achican y
  quedan **dockeadas a los costados durante la acción** para que se lea qué
  hizo cada uno; el teatro corre al 60% de velocidad. AP como **circulitos
  GRANDES abajo a los costados** (sprite circular procedural): lleno =
  disponible, vacío = no hay.
- **La sim es un títere MUDO (fix 2026-07-20)**: los bugs de la primera
  tanda de juego venían todos de reusar la voz de la sim de frames — el
  retardo de coreografía era un hitstun falso (el parrier "parecía golpeado"),
  los popups salían de eventos de la sim ("AL AIRE" contradiciendo a la
  tabla) y los AP se actualizaban al resolver (parecía que cargar pagaba
  aunque te agarraran: era el ingreso +1 adelantado). Fixes: retardo
  invisible (`FighterState.QueueDelayTick`, espera en neutral), popups y
  números 100% desde `YomiTurnResult` al cerrar el turno ("−N HP",
  "CARGA CANCELADA", counters), AP en HUD congelados al valor de arranque
  hasta el cierre, y al golpeado se le limpia la cola (no salen moves
  fantasma tarde). **Decisión**: NO proyecto aparte — mismo proyecto,
  pero la tabla es la única voz; la sim solo pone poses, sparks y sfx.
- **Distancias legibles (fix 2026-07-20)**: CERCA = 1.0 de separación,
  LEJOS = 3.4 (antes 0.9/1.9: no se distinguían). Moves de entrada larga
  solo-teatro en el catálogo (StrongFar 19, JumpInFar 20, DashInFar 21)
  para que los golpes lleguen; en la revelación los peleadores CAMINAN a su
  marca (sin teleports); franja de piso pintada por distancia (fría =
  LEJOS, cálida = CERCA) + cartel "→ CERCA / → LEJOS" al cambiar.
- Lab discreto: `dotnet run --project Tools/SimHarness -- yomi N` (y tercera
  pasada del lab default). 8000 partidas: 100% KO, 7.6 turnos/partida,
  46% de turnos en cerca, AP promedio 2.6/6 (la economía muerde), 9k parrys,
  2.2k recoveries. Ninguna acción domina (dmg/uso: Shoryu 1.46 · Kick 1.38 ·
  Salto 0.74 · Jab 0.73 · Agarre 0.62 · Parry 0.45).
- **Qué queda fuera en este modo**: frames, guard gauge, wakeup, super,
  overflow, ghost, replay (V) — todo eso sigue intacto en los modos clásicos.
- Ideas anotadas: personajes = editar celdas/costos de la matriz ("mi agarre
  también pega de lejos", "mi parry devuelve 2"); roguelike = draftear
  modificadores de celdas entre peleas. El core discreto los hace triviales.

## Modo CARTAS — copia de Yomi 2 (2026-07-21)

> Ver **[YOMI2-CARDS.md](YOMI2-CARDS.md)**: la investigación completa del
> Yomi 2 real (rulebook v7.7 + mazo de Grave) y el plan de la copia.

Pedido de Patricio: el combate re-imaginado como cartas, copia casi exacta
de Yomi 2 **sin combos ni supers** (por ahora), un solo personaje (Grave,
mazo de 24 con sus números reales, **HP 45** = 90 original a mitad de daño).
Turnos alternados, opener boca abajo vs boca arriba, attack/throw/block/
dodge con alturas high/low/mid, speed con empates al jugador activo,
proyectiles por nivel, dodge con hit-back, throws que derriban, knockdown
que apura los speeds a 10 y apaga dodges, exchange (Grave: ×2, su innate),
mano máx 12, remezcla única y time over. Una partida ES el match.
`CardSim.cs` es la sim pura (calca estructural de `YomiSim.cs`); el teatro
y la UI solo leen. Entrada **CARTAS** en el menú (vs IA). Tests por regla
en `Tools/SimTests` (15), lab `cards N` en `Tools/SimHarness` (4000
partidas: KO 99.9%, 14.4 turnos, P0/P1 parejo, ninguna carta domina).

**Segunda pasada (mismo día, auditoría completa)**: mapeo mecánica por
mecánica contra el rulebook en YOMI2-CARDS.md §3 (qué está fiel, qué se
cortó y por qué). Fixes: el time over a mitad de combate juzgaba con el HP
viejo; la IA no recuperaba ataques por exchange; el castigo del humano
ahora muestra las cartas reveladas ANTES de elegir. UI: panel por lado con
HP reales, mano/mazo/**descarte público compacto de ambos** (regla de
Yomi 2: siempre consultable), derribo y cambios restantes; guard bar
apagada (acá no existe). Traza legible: `cardstrace [seed]`.

**v2 (2026-07-22): la copia COMPLETA + Jaina** (ver YOMI2-CARDS.md §0):
mazos de 30 reales con supers y abilities, super meter (0-3★), Power Up,
COMBOS enteros (combo points, chains con +1★ por letra, starters/linkers/
enders, KD solo sin combo), pumps, Invocar Viento / Tiro en Arco (ongoing
2 combates), innates (doble exchange / Imprudencia), edge en la sim, wild
swing que DEBE jugar la super con meter. HP reales 90/85. **Dos
personajes**: Grave y Jaina (selector en el menú, rival sorteado). La mano
es un abanico estilo **Slay the Spire** (`CardHandUI.cs`): cartas grandes
solapadas, hover 1.5× al frente, CAMBIO/PODER/PUMP/TERMINAR; el teatro
actúa el combo entero en secuencia con el move más parecido por carta.
Único corte: GEMS. Lab: KO 100%, 17 turnos, espejos 50/50, Jaina 60/40
sobre Grave (matchup real — vigilar con humanos). 119 tests.

## Modo DUELO — el núcleo casual (2026-07-25)

> Ver **[DUELO.md](DUELO.md)**: la spec completa, el análisis comparativo
> que la originó y los resultados del lab.

Decisión de Patricio tras el análisis del 2026-07-25: **DUELO pasa a ser EL
juego**; clásico, YOMI discreto y CARTAS v2 quedan como modos EXPERTO. El
diagnóstico: Yomi 2 (y nuestra copia fiel) no es confuso por profundo sino
por **dónde vive su complejidad** — en el reglamento (~30 keywords), cuando
debería vivir en el contenido (cartas de una línea + personajes que re-pesan
números). Eje de adivinanza elegido: **alturas**, sin distancias.

**El juego entero, 7 reglas**: carta secreta simultánea (sin turno activo,
sin main phase) · GOLPE > AGARRE > GUARDIA > GOLPE · la velocidad desempata
golpes, empate = trade · cada golpe es ALTO o BAJO y cada guardia cubre UNA
altura · el que gana elige **+DAÑO** (quema un golpe de la mano) o
**DERRIBO** (la guardia rival no bloquea el turno siguiente) · defender bien
roba 2 y la guardia vuelve a la mano · robás 1 por turno, mano 8, remezcla
única y después TIME OVER por vida.

- Mazo de **20** por personaje (A 8/3 bajo · B 7/4 bajo · C 6/5 alto ·
  D 4/7 alto · 3 agarres 5/6 · 2+2 guardias · 4 cartas firma · 1 ESCAPE),
  **HP 46**, mano inicial 6 con ambas guardias + agarre + escape.
  La correlación **rápido = BAJO / lento = ALTO** es lo que se aprende en dos
  turnos; las firmas existen para romperla.
- **ESCAPE** (válvula, Ley 13): una por partida, no vuelve nunca — congela
  el turno. Es la respuesta al derribo.
- **Sin wild swing**: el derribo no PROHÍBE la guardia, la APAGA → toda
  carta es siempre jugable y desaparece toda esa plomería.
- Personajes por PESOS (Ley 11): Grave (X vel 10, chip 2 = "proyectil" sin
  inventar la palabra; Z alto y rápido) · Jaina (Y vel 11 unsafe; K derribo
  gratis).
- **`DuelSim.cs`**: sim pura determinista, 27 tests (146 en total).
  Lab: `duelo N` (balance), `duelogap N` (profundidad), `duelotune N`
  (barrido de diales).
- **Medido (8000 partidas)**: KO 99.8% · 13.5 turnos · premio 60/40 (las dos
  ramas vivas) · matchup 50.6/49.4 · **brecha de habilidad 77.5%** ·
  valor de la información +1.9 pp con control en 50.1%.
- Cuatro diales los movió el lab: robo 2→1 (con mano gorda el premio se
  resolvía 95% daño: la decisión de la Ley 12 moría), vida 30→46 (a 8.5
  turnos no da tiempo a leer), defender roba 1→2 (sin eso la guardia no
  llega a ser el default barato de la Ley 3) y los nerfs de Jaina (63/37 →
  50/50).
- Aprendizajes de método anotados en DUELO.md §9: una lectura mal diseñada
  PIERDE (defender más porque "va a atacar" baja el winrate); medir el valor
  de la información contra un bot random siempre da 50% (hace falta un rival
  competente con un tic); y el sesgo de lado que apareció era `System.Random`
  correlacionando seeds, no la sim.
- **UI (2026-07-25, tarde)**: `DuelHandUI.cs` (abanico de cartas grandes,
  altura codificada por POSICIÓN de la barra, keywords en chips, detalle en
  castellano) + `DuelHudUI.cs` (vida exacta, mano/mazo/descarte de ambos, el
  strip "LE QUEDAN" con las guardias resaltadas, triángulo permanente y
  revelación dockeable). El teatro actúa cada carta con la altura correcta y
  el derribado **queda en el piso durante su planificación**. Trampa
  evitada: la guardia usa `WalkB` (único move que `IsBlockingState` acepta) —
  con `Parry` el defensor se comía el golpe contradiciendo a la tabla.
- **Tercer personaje GOLEM** (grappler): 5 agarres en 20 cartas, +8 de vida
  y **super armor** en la Roca Rodante — a pedido de Patricio, es el clásico
  del grappler y entra como UNA línea en UNA carta, no como categoría del
  sistema. Se paga con velocidad y daño (vel 3 / 5): con vel 7 / 8 el Golem
  se iba a 65.8% en el lab; ahora Grave 49.8 · Jaina 48.1 · Golem 52.1.
- **Falta**: verificación EN VIVO en el editor (no estaba abierto) y el
  onboarding guionado. Ver DUELO.md §10.
- **LOS CANTOS — envido y truco** (spec 2026-07-25 noche, sesión de diseño
  — **sim y lab HECHOS**, falta la UI): la capa de apuestas del truco sobre el core.
  Una gramática (cantar/quiero/no quiero/subir) y dos cantos: **ENVIDO**
  (solo hasta la primera sangre; tanto = tus dos golpes de la misma altura
  — el palo ES la altura; ganar cobra 3 de chip pero tu tanto se hace
  público y siembra la lectura de alturas) y **TRUCO** (el intercambio vale
  ×2, escalable a retruco ×3 y vale cuatro ×4; el no quiero siempre paga).
  Ataca el valor de la información flaco (+1.9 pp): la lectura no cambia
  qué jugás, cambia CUÁNDO y CUÁNTO apostás. Conceptos 8-10 de 10 — tope
  de la Ley 14 tocado. Segunda tanda: la guardia COBRA el truco en cartas
  (robo 1 ×mult — Ley 2 por moneda), agarre 6→7 y X 4→5 (revisada contra
  los mazos reales de Yomi 2; el 10 quedó como el número acertijo del
  envido). Valor de la información: **+3.1 pp**, máximo histórico.
  **Diseño cerrado en papel (2026-07-26, DUELO.md §12): ROUNDS al mejor
  de 3** — vida ~24-28 por round, envido y una cadena de truco POR ROUND,
  el estado muere con el round y solo la lectura persiste. Auditoría
  contra la biblia hecha: las fallas vigiladas son Ley 7 (dispersión de
  fuerza suave = techo del bluff), Ley 3 (robo 1 en observación) y Ley 13
  (truco sobre derribado). Pendiente de implementación con su orden en
  §12.

## Historial de pivots

1. **Delay queue continuo** (4s de delay, timeline deslizante) — descartado.
2. **3D libre estilo Tekken** (lanes primero, luego movimiento 2D libre,
   combos, octágono UFC) — descartado.
3. **YOMIH 3D** (un move por turno, pausa al quedar libre, movelist JACK,
   ALTO/MEDIO/BAJO, tracking, octágono) — descartado como estructura, pero
   varias mecánicas van a volver (niveles, sways, frame advantage visible).
4. **Footsies por turnos programados** ← ACTUAL.
