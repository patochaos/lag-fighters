# Changelog — Lag Fighters

Cada subida a GitHub agrega acá sus change notes.

## 0.4.4 — 2026-07-19 (turno fluido + SUPER + animación legible)

### Turno fluido (toggle experimental)
- **Tecla C en el menú principal** (OFF por defecto, persiste): el último
  move puede **cruzar el límite del turno** en vez de tener que entrar
  completo. Arrancás el turno siguiente comprometido — p.ej. en el aire,
  okizeme estilo Akuma — y el rival TE VE al planificar (info honesta).
- Feedback de overflow en todas las capas: chip cortado en el borde con
  identidad naranja + pestaña «Nf, badge OVERFLOW sobre la cabeza, cartas
  con franja naranja + » cuando cruzarían, aviso en el status del menú.
- Forzado OFF en Online (el toggle no viaja en el protocolo → desync).

### SUPER: Shinku Hadouken (solo turno fluido)
- Barra dorada bajo la guardia: **se carga con los frames de overflow** —
  el riesgo de comprometerse es el combustible (120 puntos ≈ 3 cruces
  grandes). Botón dorado en el menú que late al llenarse; una por plan.
- El Shinku: proyectil gigante de **4 de daño**, doble velocidad, arrasa
  hadoukens, el parry no lo rechaza, bloquearlo come 40 de guardia,
  hard KD. Se salta: sigue siendo un compromiso de turno entero.
- La IA también la carga y la tira; lab con **doble pasada** (estricto +
  fluido) para calibrarla: 64% hit, ~1 super por pelea, peleas más cortas.

### Guardia = stamina (anti-tortuga)
- La guardia **regenera solo mientras ejecutás moves que no bloquean**
  (0.1 → 0.14/f). Quieto o bloqueando no cura: en turnos largos esperar
  deja de ser gratis, sin castigo directo al defensor.

### Menú de cartas simplificado
- **Fuera Caminar +** (redundante con Dash +) **y Salto −** (Salto N ya
  esquiva proyectiles, Dash − retrocede). Grilla 6×2.
- **Caminar − pasa a ser BLOQUEAR** — el nombre dice lo que hace.
- **Golpe A → Jab, Patada B → Barrida**; fichas JAB/BAR en la timeline.
- Cartas con estado en vivo: gris = no entra, franja naranja + » = cruza.
  Se recalcula en cada cambio del plan (agregar, borrar, wakeup).

### Animación procedural legible
- **Anticipación + snap**: los ataques se cargan hacia atrás en startup,
  snapean a extensión en activos y vuelven lento en recovery (se LEE el
  castigo). Jab, Barrida, Hadouken (manos a la cadera) y Shoryuken.
- **Tinte de fase en el limb que pega**: amarillo/rojo/azul (por pegar /
  pegando / recovery), el lenguaje de la framedata llevado al muñeco.
- **Puños y pies** como bloques brillantes; los trails salen del punto de
  contacto real. Torso y cabeza acompañan el golpe; shake en hitstun;
  squash & stretch en saltos.

### Legibilidad y estados
- **Hover de carta = ghost actuando** tu plan + esa carta (chau puntos).
- **Badges de estado** sobre las cabezas como lugar único: KD / BLOCK /
  HIT / GUARD / **GUARD CRUSH** (nuevo, rosa) / **OVERFLOW «Nf**.
- Menú de planificación en dos columnas (regla anti-overlap: nada por
  encima de las timelines).

## 0.4.3 — 2026-07-18 (menú simplificado + replay con lag teatral)

### Menú
- **Fix**: ONLINE mostraba FÁCIL/NORMAL en vez de CREAR SALA/UNIRSE (el paso
  caía en el caso equivocado del selector de labels).
- **VS IA entra directo** a pelear contra Adaptive en Normal; **IA CUSTOM**
  abre el submenú de perfil + dificultad para quien quiera tunear.
- **Retirados 1v1 LOCAL y POR CÓDIGO** (injugables). Quedan PRÁCTICA /
  VS IA / IA CUSTOM / ONLINE. La maquinaria interna sigue por si vuelven.
- **ESC vuelve atrás** en todos los pasos del menú.

### Replay
- **Lag teatral**: la repetición se comporta como un stream con mala
  conexión — se traba ("|| LAG...", glitch, estática), acumula deuda y corre
  a ×2.6 hasta alcanzarse; ratos "a los saltos" a ~5 fps; ping falso en rojo
  con wifi en pánico; mini-rewind al descongelar (el teleport de netplay);
  el audio se ahoga durante el tirón. En Lag Mode escala con el nivel
  alcanzado. Todo con flags independientes (`ReplayLagFX`) y sin tocar la
  re-simulación determinista.
- **Botones LAG / NORMAL / RÁPIDO** arriba al medio durante el replay:
  con lag teatral, limpio, o limpio a ×2 — conmutables en vivo.

### Varios
- El velo azul de planificación bajó a una insinuación (alfa 0.08 → 0.03).
- Carta CAMINAR +: tag "PASO CORTO · ajuste fino" (aclara su rol vs dash).

## 0.4.2 — 2026-07-18 (pulido UI/UX y arte — la sim no cambia)

### Legibilidad
- **Sombra de contacto** bajo cada peleador: se achica y desvanece con la
  altura — por fin se lee dónde cae un salto.
- **Fases S/A/R dentro de las fichas** de la timeline (amarillo/rojo/azul):
  se ve en qué frame exacto pega cada orden del turno.
- **Glow de esquina**: la pared pulsa con el color del jugador acorralado.
- Pips de vida que **se rompen** (flash + pop + fade) en vez de apagarse.
- Carteles grandes en dos slots: los avisos de lag ya no pisan COUNTER/K.O.

### Juice de UI
- Hover con tinte + **blips sintetizados** en todos los botones y cartas;
  sonidos al agregar/borrar órdenes y confirmar turno.
- Toggle **SFX ON/OFF** en OPC (persiste). Announcer con pitch aleatorio.
- Cursor pixel-art procedural, a tono con la Press Start 2P.

### Arte
- Luces de acento celeste/naranja por lado: los blockmen despegan del fondo.
- Hadouken con núcleo cúbico rotante y estela.
- Festejo de KO: saltitos del ganador, burst dorado y público eufórico.

### Flujo
- El menú de modos **recuerda tu última elección** (lag/modo/perfil/dificultad).
- Con plan vacío el botón dice **PASAR (quieto, bloquea)**; SKIP aclara
  ESPACIO; la ayuda del plan quedó en dos líneas.
- **Tips de primera vez solo en Práctica** (4, avanzan con lo que hacés y no
  vuelven nunca).
- Tipografía pixel normalizada a 8/16/24/32 px.

## 0.4.1 — 2026-07-18 (ONLINE + timers + parry anti-chip)

- **Modo ONLINE**: sala con código de invitación de 4 letras (sin cuentas,
  sin matchmaking). Relay tonto sobre Supabase (`lf_rooms`/`lf_turns`):
  cada turno sube tu `TurnCode` y baja el del rival por polling; la sim
  determinista hace el resto. La sala persiste (partida retomable).
- **Timer de planificación de 30s** en ONLINE y 1v1 local: al agotarse se
  manda lo planificado; sin órdenes = quieto bloqueando.
- **20 turnos por round**: al agotarse, TIME OVER y gana el que tiene más
  vida (empate posible). Prompt con TURNO X/20 y aviso en los últimos.
  La repetición y el marcador usan el ganador efectivo (KO o por vida).
- **El parry recarga +15 de guardia**: la respuesta activa al chip de
  proyectiles. En la matriz de perfiles, Zoner bajó de 65.8% a 58.3%.
- Adaptive con counter-picks conscientes de distancia (mejora sus peores
  cruces pero sigue último, 34.7% — anotado como deuda estructural).
- El harness juega con las reglas reales (20 turnos + juez por vida).

## 0.4.0 — 2026-07-18 (Parry + perfiles de IA + balance pass 2)

### Balance (del análisis de framedata efectiva, verificado con el lab)
- **Shoryuken anti-aéreo especializado**: activa 8→5, alcance 0.75, hitbox
  desde Y 1.0 (no pega OTG ni domina el suelo). 76%→61% de conexión en el lab.
- **Tatsu**: inmunidad a proyectiles termina en f34; el final es castigable.
- **Salto +**: ventana de hit 20..28 (el contacto tardío llegaba a +11).
- **Counter**: +1 de daño solo para golpes de 1; los pesados suman solo stun.
- **Guardia crusheable**: barra 100→70 (respawn 35), hadouken −25 de guardia.
  El lab pasó de 0 a cientos de guard crushes.
- **Posición visual honesta**: el muñeco interpola entre ticks de la sim (el
  smoothing viejo lo dejaba media hurtbox atrás en un dash). Trails solo
  durante las ventanas de hit reales.

### UI
- Menú de planificación compacto: cartas de solo-nombre + panel de info a la
  derecha (hover) con framedata, mini-barra S/A/R y **rango real de ventaja**
  (HIT +2…+5 / BLOCK −5…−2). La timeline quedó protagonista (filas de 52px).
- Replay con cartel "► REPLAY" y botón **SKIP** (o ESPACIO).
- Perf para WebGL: pool de sparks, menos strings por frame, preview sin
  capturas muertas, bitmask de ventanas de hit.

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
- Harness con modo `profiles` (round-robin de perfiles): primer dato — Zoner
  es opresivo (66% global) y Adaptive el más débil (35%); anotado para tuning.

## 0.3.1 — 2026-07-18 (la web anda de verdad)

- Pantalla negra en WebGL: el stripping de IL2CPP borraba el módulo de
  física que `CreatePrimitive` necesita → `link.xml` lo preserva.
- Lentitud + clicks corridos: `devicePixelRatio` fijado en 1 (en retina
  renderizaba 4× los píxeles y el Input System recibía clicks en px CSS) y
  pipeline recortado en WebGL (sin FSR/MSAA/HDR/sombras/post).
- Primera subida con butler (`patochaos/lag-fighters:html5`).

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
