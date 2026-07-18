# Changelog — Lag Fighters

Cada subida a GitHub agrega acá sus change notes.

## 0.4.0 — 2026-07-18 (Parry + perfiles de IA)

### Gameplay
- **Parry reemplaza Esperar** sin cambiar el índice del comando ni romper los
  códigos de turno: 2f startup, 5f activos, 5f recovery. Rechaza golpes y
  proyectiles; contra melee interrumpe 18f. Pierde contra agarre y delay.
- Las órdenes de features apagadas ahora se consumen en neutral, sin regalar
  un Parry ni invalidar replays/códigos.

### VS IA
- Selector posterior a VS IA con **RANDOM** por defecto y cinco perfiles para
  práctica repetible: Zoner, Aggressive, Defensive, Trickster y Adaptive.
- Dificultades Fácil, Normal (default) y Difícil. Adaptive aprende únicamente
  de planes ya revelados, nunca del plan secreto actual.
- Feedback visual y HUD propio para Parry; pruebas deterministas nuevas para
  ventana, counters, proyectiles, perfiles y presupuesto de turnos.

## 0.3.0 — 2026-07-17 (primer playtest + build web)

Ajustes tras el primer playtest de Patricio, y la primera build WebGL.

### Balance / features
- **Pérdida de miembros y agachado DESACTIVADOS** (a pedido): el código
  queda completo detrás de `SimConfig.LimbsEnabled` / `CrouchEnabled`
  (false) — flipear los flags y descomentar cartas/IA para reactivar.
  Los códigos async con esos movimientos se consumen en neutral (no rompen).
- **Lag Mode más suave**: sube 50% cada 3 turnos (60 → 90 → 135 → 202 →
  303) en vez de duplicarse cada 4.

### UX
- **Replay obligatorio del round**: al terminar cada round, gane quien
  gane, corre la repetición de la pelea entera de corrido antes del
  banner. V en el game over la repite; los botones de velocidad aplican.
- Cajas (hurt/hitboxes) OFF por defecto.
- La explicación de la carta seleccionada ahora es un panel legible:
  título con el color de la categoría + tag, descripción grande a la
  izquierda, contador de frames a la derecha.
- Restyle de HUD/menús con fuente pixel (PressStart2P), hover en cartas,
  scrub del ghost arrastrando la timeline, click derecho borra fichas,
  rango del movimiento dibujado en el escenario, panel OPC colapsable.
- El aviso "perdés Nf por el stun" usa los frames reales del turno con lag.

### Build
- **Primera build WebGL** (16.6 MB, gzip + decompression fallback) lista
  para itch.io; target nuevo en BuildScript (menú o batchmode).

## 0.2.0 — 2026-07-17 (el plan completo, salvo el segundo personaje)

Todo `PLAN.md` implementado en una sesión, con tests y lab verdes.

### Gameplay
- **Pérdida de miembros** (la idea fundacional): daño localizado por altura
  del golpe, 3 HP por miembro. Sin brazo: ni A ni Hadouken. Sin pierna: ni
  B/Tatsu/baja, las patadas aéreas no salen, velocidad 65%. El bloque del
  rig literalmente se cae; órdenes huérfanas se consumen en neutral.
- **Agacharse + alto/bajo posicional**: hurtbox 0.9 — el jab y el hadouken
  pasan por arriba (esquivar no gasta guardia). Nueva **Patada baja**
  (+2 hit / −3 block, pega bajo, agachado todo el move).
- **Esquina real**: el pushback contra la pared se transfiere al atacante.
- **Wakeup options**: RÁPIDO (−16f) / QUEDARSE (+16f), secreto hasta
  ejecutar, en el turn log (replay determinista intacto).
- Fix: el Shoryuken tenía el primer frame vulnerable (off-by-one, lo pescó
  el nuevo test de framedata). Invuln real 1–10.

### Modos
- **1v1 con picks secretos**: pantalla "PASÁ EL TECLADO" entre pickers.
- **POR CÓDIGO** (online asincrónico sin servidores): cada turno se
  intercambia un código corto por chat (clipboard); la sim determinista
  garantiza la misma pelea en ambas puntas.

### UX / Presentación
- Velocidad de playback ×0.5/×1/×2 · resumen post-turno · log de turnos
  colapsable (L).
- Counter hit visible (flash naranja + cartel) · KO en cámara lenta ·
  hit-sparks de cubitos · trails en frames activos · intro de round ·
  stage con skyline y público de bloques · announcer solo en KO/guard
  crush con toggle VOZ.

### Técnica
- `Tools/`: SimTests (16 tests de framedata con dotnet), SimHarness (lab
  con stats de guard crush), verify.ps1 (compile + tests + lab), y
  CompileCheck versionados en el repo.
- BuildScript: target WebGL (gzip + decompression fallback) para itch.io.
- splash.png 8.9 → 2.2 MB. `.plastic/` fuera de git.
- Documentación: README, GDD, DESIGN y PLAN al día.

## 0.1.0 — 2026-07-17 (primer push)

Primer prototipo jugable completo del concepto "footsies por turnos
programados", desarrollado íntegramente el 2026-07-17.

### Gameplay
- Simulación pura y determinista (`Sim.cs`, sin UnityEngine): 60 ticks/s,
  turnos de 60 frames con colas simultáneas.
- Movelist de 14 comandos sabor Ryu vs Ken, balanceada contra la framedata
  real de ST Ryu: caminar/dash/saltos, jab, sweep, Hadouken (proyectil real),
  Shoryuken con invuln, Tatsumaki que atraviesa fireballs, Agarre con tech.
- Estados HITSTUN / BLOCKSTUN / KNOCKDOWN con framedata visible; el stun se
  arrastra al turno siguiente (okizeme natural) y las órdenes interrumpidas
  se pierden.
- Guardia automática sin botón + counter hits (+1 daño en startup) + trades
  justos con resolución en dos fases.
- **Guard gauge**: bloquear come guardia (A −15 · B −30 · aéreas −15 ·
  hadouken −20 · shoryu −35 · tatsu −15/hit); regen 6/seg fuera de
  guardia/blockstun; en 0 → GUARD CRUSH (50f de stun, la barra renace al 50%).
- **LAG MODE**: cada 4 turnos los frames del turno se duplican (60 → 960),
  con carteles y wifi agonizante.
- Rounds al mejor de 3 con replay determinista del round (V) y revancha (R).

### Modos y UI
- Práctica (dummy que no bloquea y revive) / VS IA (planifica en secreto) /
  1v1 local hotseat. Menú en dos pasos: modo → NORMAL o LAG MODE.
- Menú de planificación con cartas (grilla 7x2, framedata y mini-barra S/A/R
  por carta), ghost de preview del plan, timelines del turno con fichas y
  stun arrastrado, pips de vida + barra de guardia, feedback con frame
  advantage real, indicador de distancia, toggle de hurt/hitboxes (H).
- Todo clickeable con mouse y operable por teclado.

### Técnica
- Cero contenido en escena: todo se construye por código al dar Play.
- Blockman procedural con rig de bloques separados (preparado para pérdida
  de miembros). Ghost animado real.
- Juice cosmético que no toca la sim: hitstop, screen shake, sonidos
  sintetizados en runtime (sin assets de audio).
- Lab de balance headless (miles de peleas IA vs IA con `dotnet`).
- BuildScript con menú "Lag Fighters → Build para compartir"; fix de
  materiales en build (Resources); splash screen.
- Documentación: README, GDD completo, DESIGN.md (diseño vivo + pivots),
  PLAN.md (roadmap).
