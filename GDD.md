# LAG FIGHTERS — Game Design Document

*Última actualización: 2026-07-17. El diseño vivo y el historial de decisiones
está en [`DESIGN.md`](DESIGN.md); este documento es la foto completa y ordenada.*

---

## 1. Visión

Un fighting game donde **pensar reemplaza a los reflejos**. Todo el juego de
lectura de un fighting clásico (footsies, whiff punish, frame advantage,
okizeme) pero sin ejecución en tiempo real: planificás a ciegas, mirás cómo
se resuelve, y aprendés a leer al rival turno a turno.

El nombre es la broma fundacional: el juego abraza el lag en vez de pelearlo.
En **LAG MODE** la latencia crece hasta lo absurdo y el juego se vuelve más
psicológico, no menos jugable. La simulación 100% determinista convierte el
"lag" en una mecánica: dos jugadores con el mismo par de colas ven exactamente
la misma pelea, lo que abre la puerta a replay exacto, ghost preview y
multiplayer asincrónico sin netcode.

**Inspiraciones**: *Footsies* (pocos botones, distancia, whiff punish),
*Your Only Move Is HUSTLE* (framedata visible, ghost, turnos), SF2/ST (la
framedata de referencia del personaje base).

## 2. Loop core

1. **Planning (pausa)**: cada jugador arma una cola de comandos de hasta
   **60 frames** (1 segundo a 60fps). Cada carta muestra su framedata
   (startup/active/recovery). Un **ghost** te muestra el preview de tu plan
   contra un rival quieto ("pegaría N si no se mueve"). No ves la cola rival.
2. **Execution (tiempo real)**: las dos colas corren **simultáneas**. Acá no
   se toca nada: solo mirás cómo tu lectura fue genial o un desastre.
3. Vuelta a planificar, con las consecuencias a cuestas (ver 3.2).

Turno corto = 1-2 decisiones por turno → casi yomi puro, decisión a decisión.

### El mindgame

- Planificás a ciegas; la fila rival de la timeline se revela al ejecutar.
- Si te pegan, tu comando actual **se cancela** y lo que no llegó a ejecutarse
  **se pierde al final del turno** ("perdió N órdenes").
- El stun **se arrastra al turno siguiente**: arrancás −Nf y ambos lo saben
  (el prompt lo muestra: "VENTAJA +50f, rival derribado") → okizeme natural.

## 3. Sistemas

### 3.1 Turnos y colas

- 60 ticks/s. Turno de 60 frames (`SimConfig.TurnFrames`); en Lag Mode se
  re-escala por nivel.
- El stun arrastrado come frames del turno: solo planificás lo que entra en
  `turnFrames − stun`.
- Órdenes no ejecutadas al cerrar el turno se pierden (con aviso).

### 3.2 Estados

| Estado | Causa | Efecto |
|---|---|---|
| **HITSTUN** | golpe conectado | cancela el comando, come turno |
| **BLOCKSTUN** | golpe bloqueado | ídem, más corto |
| **KNOCKDOWN** | sweep, shoryu, golpe aéreo, agarre | derribo; hurtbox baja (0.55) |

Los frames restantes de stun son visibles en el HUD en vivo, y el feedback de
cada intercambio muestra la ventaja real ("+6f" / "−16f").

**Counter hit**: pegarle a alguien durante el startup de su ataque = +1 daño
y más stun.

**Trade**: si ambos conectan en el mismo frame, ambos comen daño y stun
(resolución en dos fases para que sea justo, sin ventaja por orden de loop).

### 3.3 Guardia

- **Automática, sin botón**: bloqueás en el piso si estás en neutral, esperando
  o caminando atrás. En el aire, en dash y caminando adelante NO.
- **Guard gauge (barra de 100)**: cada bloqueo la come según el golpe
  (ver tabla de movelist). Regenera ~6/seg SOLO cuando no estás bloqueando ni
  en blockstun. En 0 → **GUARD CRUSH**: stun de 50f sin daño (+~32f de
  ventaja → golpe garantizado) y la barra renace al 50%.
- El bloqueo queda con **dos counters**: el agarre (puntual, rompe guardia una
  vez) y el crush (estructural, castiga tortuguear).
- **Agarre vs agarre = TECH**: se separan, nadie come.

### 3.4 Movelist (personaje base, sabor Ryu vs Ken)

Balanceada contra la framedata real de ST Ryu (supercombo.gg). S/A/R =
startup/active/recovery. HP total: 6.

| # | Comando | Frames | Daño | Guardia | Notas |
|---|---------|--------|------|---------|-------|
| 1 | Caminar + | 20 | — | — | +0.55, NO bloquea |
| 2 | Caminar − | 20 | — | — | −0.38 (atrás más lento, como SF2), **bloquea** |
| 3 | Dash + | 16 | — | — | +1.0, no bloquea: puro compromiso |
| 4 | Dash − | 16 | — | — | −1.0, el bait |
| 5 | Salto + | 6/28/10 | 1 | −15 | +1.9, patada de jump-in (hit 20..30, ~+3/−8) |
| 6 | Salto N | 6/28/6 | 1 | −15 | vertical, patada al caer — el wakeup que pega |
| 7 | Salto − | 6/28/6 | — | — | −1.9, la retirada sobre el hadouken |
| 8 | Golpe A | 6/4/14 | 1 | −15 | jab: **+2 on hit / −5 on block** |
| 9 | Patada B | 16/6/30 | 2 | −30 | sweep, soft KD 42f, **−10 on block** |
| 10 | Hadouken | 14/2/44 | 1 | −20 | 60f: ocupa el turno ENTERO; saltable |
| 11 | Shoryuken | 4/8/32 | 2 | −35 | invuln 1..10, hard KD 60f, −17 block |
| 12 | Esperar | 12 | — | — | neutral, **bloquea** |
| 13 | Tatsumaki | 12/18/16 | 1+1 | −15/hit | viaja +1.6, atraviesa hadoukens (8..40), 2° hit derriba, hitbox baja |
| 14 | Agarre | 6/4/20 | 1 | — | rompe guardia, KD; pierde vs aéreos/caídos; tech espejo |
| 15 | Agacharse (OFF) | 14 | — | — | **bloquea** con hurtbox 0.9: jab y hadouken pasan por arriba |
| 16 | Patada baja (OFF) | 8/4/16 | 1 | −15 | pega BAJO, **+2 hit / −3 block**, agachado todo el move; es patada |

Reglas extra: golpe a alguien en el aire = hard KD 60f. Proyectil: 1 por vez,
3 u/s, choca con el proyectil rival.

### 3.4b Alto/bajo posicional (agacharse) — DESACTIVADO

*Implementado y apagado por flag (`SimConfig.CrouchEnabled = false`) hasta
nuevo aviso; el diseño queda documentado para cuando vuelva.*

No hay flags de nivel: es geometría. Agacharse baja la hurtbox a 0.9, así
que el jab (Y desde 1.0) y el hadouken (Y desde 0.95) **pasan por arriba**
— esquivar no gasta guardia, bloquear sí. El counter del agachado: sweep,
patada baja, tatsu y agarre le pegan igual. El mixup queda: contra parado →
jab/hadouken; contra agachado → sweep/agarre/baja; contra los dos → salto.

### 3.4c Pérdida de miembros — DESACTIVADA

*Implementada y apagada por flag (`SimConfig.LimbsEnabled = false`) hasta
nuevo aviso; el diseño queda documentado para cuando vuelva.*

La idea fundacional. Cada golpe conectado hace daño localizado según su
altura: bajo 1.0 come **pierna**, arriba come **brazo** (3 HP por miembro).

- **Brazo en 0**: el bloque del rig vuela; ni Golpe A ni Hadouken.
- **Pierna en 0**: ni Patada B, ni Tatsumaki, ni Patada baja; las patadas
  aéreas no salen (saltás igual); caminar y dash rinden 65%.

Las órdenes ya planificadas con un miembro perdido degradan a Esperar
(determinista: el replay y el online por código no se rompen). Los miembros
vuelven al empezar cada round.

### 3.4d Wakeup options

Derribado al planificar, elegís **RÁPIDO** (−16f de knockdown) o **QUEDARSE**
(+16f, para que el meaty del rival pegue al aire). La elección es secreta
hasta que el turno se ejecuta y viaja en el turn log (replay exacto). La IA
se levanta rápido el 65% de las veces.

### 3.4e Esquina

El pushback que aplasta al defensor contra la pared se transfiere al
atacante (como en SF): la esquina aprieta pero no es una trituradora infinita.

### 3.5 Escenario

Línea de ±4.2, separación mínima 0.8 en el piso (en el aire se cruzan).
Hurtbox de pie 0.7×1.75, aérea 1.35–2.6 (los hadoukens pasan por abajo),
caído 0.55.

### 3.6 LAG MODE

Modo espejo de cualquier modo base. Cada 3 turnos, los frames del turno
suben **50%**: 60 → 90 → 135 → 202 → 303 (cap). Cada salto trae su cartel
("IT GETS LAGGIER…", "EL WIFI ESTÁ LLORANDO", "MODO DIAL-UP", "PALOMA
MENSAJERA") y un indicador de wifi que pierde barras hasta parpadear en rojo
con ping falso. Más lag = colas más largas = más plan y menos reacción.
Se resetea por round.

### 3.7 Rounds

Al mejor de 3 (marcadores dorados). `V` re-simula el último round completo de
corrido (replay determinista desde el log de turnos); `R` es revancha.

## 4. Modos

- **Práctica**: dummy quieto, no bloquea, revive al morir.
- **VS IA**: `SimpleAI` planifica en secreto — zonea, castiga knockdowns,
  mezcla footsies y cada tanto apuesta un shoryuken.
- **1v1 local**: hotseat con **picks secretos** — entre pickers hay una
  pantalla "PASÁ EL TECLADO" que oculta filas y ghost.
- **POR CÓDIGO** (online asincrónico, sin servidores): elegís lado, planificás
  y el juego copia tu código de turno al portapapeles (`TurnCode`: LF +
  base64 de lado/turno/wakeup/cola). Se lo mandás al rival por WhatsApp/
  Discord, pegás el suyo con ESPACIO y la sim determinista garantiza que
  ambos ven exactamente la misma pelea. Valida lado, turno y movimientos.

Menú inicial: NORMAL o LAG MODE → modo (→ lado, si es POR CÓDIGO).

## 5. Presentación

- **Blockman procedural**: peleadores de bloques 3D armados por código
  (`FighterView`), rig de piezas separadas — y las piezas efectivamente se
  caen (pérdida de miembros).
- **Juice cosmético que nunca toca la sim**: hitstop, screen shake, flashes,
  **hit-sparks de cubitos**, **trails** en mano/pie durante frames activos,
  **KO en cámara lenta** (timeScale 0.3 por 1.5s).
- **Sonido sintetizado en runtime** (`SfxLib`): cero assets de audio.
  Announcer (mp3) SOLO en KO y guard crush, toggle VOZ en el HUD.
- **HUD**: pips de vida, barra de guardia (parpadea en rojo <25%), estado de
  miembros, timelines del turno con fichas y stun arrastrado, feedback con
  framedata, resumen post-turno, log de turnos colapsable (L), velocidad de
  playback ×0.5/×1/×2, indicador de distancia, toggle de cajas (H).
- Intro de round ("ROUND N — ¡PELEA!"), stage con líneas de piso, skyline
  determinista y público de bloques saltando; splash screen y wifi
  agonizante en Lag Mode.

## 6. Arquitectura técnica

```
Assets/Scripts/LagFighter/
├── Sim.cs          ← simulación PURA y determinista (sin UnityEngine)
├── SimpleAI.cs     ← IA de planificación (también pura)
├── MatchController ← flujo: ModeSelect → Planning → Executing → (KO | EndTurn)
├── FighterView     ← blockman procedural (solo LEE la sim)
├── HudUI / PlanMenuUI / ModeMenuUI ← UI por código, cero prefabs
├── Viz.cs          ← hurt/hitboxes + ghost del plan
├── ArenaBuilder    ← piso, pared, cámara, luz
└── Juice.cs        ← CameraFX + SfxLib
```

**La regla sagrada**: `Sim.cs` no importa UnityEngine y las views solo leen
estado. Esto habilita:

- **Ghost preview** (`PlanPreview`): clona la sim y simula tu plan contra un
  rival quieto.
- **Replay exacto**: re-simular el log de turnos reproduce la pelea idéntica.
- **Lab de balance headless** (`Tools/SimHarness`): miles de peleas IA vs IA
  con `dotnet`, sin Unity (stats por movimiento: usos, conecta%, dmg/uso,
  crushes, techs) + **tests de framedata** (`Tools/SimTests`, 16 tests) +
  `Tools/verify.ps1` que corre todo.
- **Online asincrónico ya andando**: el modo POR CÓDIGO intercambia colas
  serializadas (`TurnCode`) — cero netcode real.

La escena no tiene nada: `MatchController.Boot()` (RuntimeInitializeOnLoadMethod)
construye todo por código al dar Play.

## 7. Roadmap

Ver [`PLAN.md`](PLAN.md). Resumen: profundidad de pelea (esquina real, wakeup
options, counter visible), UX de lectura del turno (velocidad de playback,
resumen post-turno, log), compartir (WebGL/itch.io, picks secretos, online
asincrónico por código), contenido (pérdida de miembros — la idea fundacional
—, niveles ALTO/MEDIO/BAJO), presentación (KO slow-mo, sparks, intro de round,
stage, announcer puntual).

## 8. Historial de pivots

1. **Delay queue continuo** (4s de delay, timeline deslizante) — descartado.
2. **3D libre estilo Tekken** — descartado.
3. **YOMIH 3D** (un move por turno, ALTO/MEDIO/BAJO, tracking) — descartado
   como estructura; varias mecánicas van a volver.
4. **Footsies por turnos programados** ← ACTUAL (arrancó con turnos de 240f,
   se acortó a 60f el 2026-07-17).
