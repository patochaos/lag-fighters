# Changelog — Lag Fighters

Cada subida a GitHub agrega acá sus change notes.

## 0.2.0 — 2026-07-17 (el plan completo, salvo el segundo personaje)

Todo `PLAN.md` implementado en una sesión, con tests y lab verdes.

### Gameplay
- **Pérdida de miembros** (la idea fundacional): daño localizado por altura
  del golpe, 3 HP por miembro. Sin brazo: ni A ni Hadouken. Sin pierna: ni
  B/Tatsu/baja, las patadas aéreas no salen, velocidad 65%. El bloque del
  rig literalmente se cae; órdenes huérfanas degradan a Esperar.
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
