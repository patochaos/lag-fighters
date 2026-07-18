# Changelog — Lag Fighters

Cada subida a GitHub agrega acá sus change notes.

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
