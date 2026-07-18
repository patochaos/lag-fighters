# Lag Fighters — Plan de UI/UX (2026-07-17, solo plan, a revisar)

## 0. Diagnóstico del estado actual

Inventario de lo que hay en pantalla hoy (todo funcional, nada roto):

- **Arriba**: prompt de turno (centro), dist, resumen de turno, botón CAJAS,
  wifi+ping, pips de vida + guard bar + wins (esquinas), feedback (esquinas),
  estado HITSTUN/BLOCKSTUN (esquinas), botón LOG (derecha).
- **Abajo**: dos timelines con fichas + stun block, botones velocidad, cartas
  7x2 con framedata, BORRAR / ¡LISTO! / WAKEUP, detalle + status + ayuda.
- **Mundo**: blockmen, ghost animado, hurt/hitboxes, bolas de hadouken,
  escenario pasillo plano.

Los problemas NO son de información (sobra información, está todo) sino de:

1. **Jerarquía plana**: todo pesa igual. La vida (lo único que importa
   siempre) ocupa lo mismo que el botón CAJAS. En SF6 la vida es un 40% del
   ancho; acá son 6 cuadraditos perdidos en una esquina.
2. **Información lejos de la acción**: el daño/COUNTER/BLOQUEADO aparece en
   las esquinas superiores mientras la acción pasa en el centro. El ojo tiene
   que viajar. (Into the Breach: TODO pasa sobre el tablero.)
3. **Texto flotante sin contenedor**: labels sueltos sobre fondo variable =
   look de debug overlay. Falta "cuerpo" de UI (paneles, bordes, fondo).
4. **Tipografía**: Arial legacy en 12 tamaños distintos. La splash ya definió
   una identidad (pixel font, estética netplay) que la UI in-game no sigue.
5. **Dos fases, un solo look**: planificar y ejecutar se ven casi iguales;
   el cambio de fase (el corazón del juego) es un texto que cambia.
6. **El mundo está desaprovechado**: cámara fija, piso vacío, cero lectura
   espacial de rangos/distancias desde el mundo (todo por texto "dist 4.00").
7. **Redundancias**: estado en 3 lados (label esquina + bloque en timeline +
   prompt), ayuda de teclas siempre visible aunque ya la sabés.

## 1. Referencias y qué robarle a cada una

| Juego | Qué hace bien | Qué robamos |
|---|---|---|
| **Your Only Move Is HUSTLE** | El juego ES el editor: menú pegado al personaje, scrub de tiempo libre, cámara libre, outcome en el mundo. UI mínima, casi sin HUD. | **Scrub del turno** (arrastrar el playhead y ver el ghost en ese frame), preview en el mundo, sensación de "editor de pelea". |
| **Into the Breach** | Planificación con información perfecta: intenciones EN el tablero, hover = preview del resultado exactamente donde va a pasar, undo total, cero texto lejos de la acción. | **Hover de carta = rango dibujado en el piso** frente al personaje; badges/números world-space; borrar cualquier orden, no solo la última. |
| **Street Fighter 6** | Jerarquía brutal (vida enorme arriba, drive gauge debajo, lo demás mínimo), lenguaje de color estricto, daño y mensajes en el punto de impacto, KO cinematográfico. | **HP protagonista** con guard gauge debajo, color = significado fijo, feedback en el personaje golpeado, KO con cámara. |
| **Footsies** | Minimalismo: si no decide la ronda, no está en pantalla. | Podar: ayudas colapsables, CAJAS/LOG/VELOCIDAD a un menú de esquina discreto. |
| **Frozen Synapse** | Turnos simultáneos: fase de planificación con look propio (paleta fría, overlays) vs ejecución "en vivo". | **Cambio de modo visual fuerte** entre planning (frío, con grid/scanlines) y ejecución (limpio, saturado). |
| **Toribash** | Turn-based fighter: replays como espectáculo compartible. | El replay (V) como "highlight" — cámara automática, sin HUD de planificación. |

## 2. Dirección de arte: "NETPLAY ROTO"

La splash ya la definió: cartel pixelado + wifi + ping. La UI entera debería
ser **el overlay de un netplay de los 2000 en decadencia**:

- **Una sola fuente pixel** (Press Start 2P o VT323, licencia OFL, se importa
  el TTF a Resources y listo). 3 tamaños: título / normal / mini.
- **Paleta con significado fijo** (documentar en DESIGN.md):
  - Celeste = P1 · Naranja = P2 (ya está)
  - Amarillo = startup/atención · Rojo = activo/daño · Azul = recovery/block
  - Magenta = agarre · Verde = vida/OK · Gris = neutral (ya casi está)
- **Marco de "stream"**: nombres estilo lobby ("VOS", "RIVAL" como tags de
  conexión), ping siempre visible (en Normal: "ping 0ms · LAN"), y al subir
  el lag: glitch/scanline sutil en la UI (no en el juego — la sim es sagrada).
- Paneles con fondo negro translúcido + borde de 1px de color de sección:
  TODO texto vive dentro de un panel. Chau labels flotando.

## 3. Propuestas por pantalla/fase

### 3.1 Planificación (el corazón — mayor inversión)

- **La timeline propia sube de jerarquía**: más alta (60px), pegada arriba de
  las cartas, con el stun block y las fichas con **pictogramas** (puño, pie,
  bola, flechas) además de la letra — legible de un vistazo.
- **Scrub**: arrastrar el playhead de la timeline mueve el ghost a ese frame
  exacto (el loop automático sigue cuando soltás). YOMIH-style. Es LA feature
  de UX que falta: hoy el ghost va a su ritmo y no podés inspeccionar "¿dónde
  estoy en el frame 32?".
- **Selección de fichas**: click en una ficha de la timeline la marca; DEL la
  borra (no solo la última); las siguientes se re-encadenan solas.
- **Hover de carta = rango en el mundo**: al pasar por Golpe A, se dibuja el
  rect de alcance frente a tu personaje en el piso/aire; con Hadouken, la
  línea de viaje; con salto, el arco. Into the Breach puro. Convierte la
  framedata en intuición espacial y mata al texto "dist 4.00".
- **Cartas agrupadas** con micro-headers: MOVIMIENTO · ATAQUE · ESPECIAL ·
  DEFENSA. Panel de detalle fijo (no línea suelta): framedata + qué le gana
  y qué le pierde ("pierde vs: salto, shoryu").
- **Marcas de rango en el piso** permanentes: tics cada 0.5 entre los dos
  peleadores, con highlight del alcance del último ataque hovereado.

### 3.2 Ejecución

- **Transición de fase fuerte**: al confirmar, flash corto "¡EJECUTANDO!" +
  la UI de planificación se pliega hacia abajo + saturación del mundo sube.
  Al volver a planning: overlay frío + grid sutil. Siempre sabés dónde estás.
- **Feedback en el punto de impacto**: daño ("-2"), COUNTER, BLOQUEADO,
  TECH, GUARD CRUSH como texto world-space sobre el golpeado, con física
  mínima (sube y se desvanece). Las esquinas quedan solo para el log.
- **Badges de estado world-space**: "HITSTUN 12f" flotando sobre la cabeza
  del aturdido (contador vivo), en lugar del label de esquina.
- **Cámara viva**: framing dinámico según distancia de los peleadores
  (lerp de posición/FOV), micro punch-in en cada hit (ya hay shake), y el
  KO en cámara lenta + zoom (ya está en PLAN.md). Es EL cambio que saca el
  look de "pasillo de debug".
- Barra fina de progreso del turno arriba (1px alto, ancho pantalla).

### 3.3 HUD de combate

- **Vida protagonista**: pips el doble de grandes, arriba enfrentados hacia
  el centro (estilo SF), nombre + wins integrados en la misma tira, guard
  gauge inmediatamente debajo con su color amarillo. Un solo bloque visual
  por jugador en vez de 5 elementos sueltos.
- **Menú de esquina** (colapsado por defecto): CAJAS · VELOCIDAD · LOG en un
  solo botoncito "⚙" — hoy son 3 controles sueltos en 3 lugares.
- Wifi/ping: integrado a la tira de "conexión" arriba del todo (tema netplay).

### 3.4 Menús y pantallas

- **Mode select**: mantener la splash, pero las cartas con la misma familia
  visual nueva; LAG MODE con las barras de wifi en la carta (iconografía).
- **Pantalla de resultados** post-match: HP por turno (mini gráfico), daño
  total, órdenes perdidas, guard crushes, counters — todo ya está en el log.
  Con botones grandes: REVANCHA · REPLAY · MENÚ (hoy son teclas invisibles).
- **Onboarding mínimo**: primera partida, 3 tooltips contextuales de una
  frase (cartas → ghost → ESPACIO). Nada de tutorial largo.
- La ayuda de teclas colapsa después del turno 3 (tecla ? la trae de vuelta).

## 4. Fases de ejecución propuestas (cada una shippeable)

| Fase | Contenido | Costo |
|---|---|---|
| **UI-1 Identidad** | Fuente pixel única, paleta documentada, paneles con fondo/borde en todo texto, HP+guard+wins como bloque grande, menú ⚙ de esquina, tira de conexión (ping/wifi) | 1 sesión |
| **UI-2 El mundo habla** | Feedback y badges world-space, marcas de piso, hover-de-carta = rango dibujado, transición de fase fuerte | 1 sesión |
| **UI-3 Timeline editor** | Scrub del ghost, borrar cualquier ficha, pictogramas en fichas y cartas, panel de detalle fijo | 1-2 sesiones |
| **UI-4 Cámara y pantallas** | Cámara dinámica + KO lento, pantalla de resultados con gráfico, onboarding tooltips, ayuda colapsable | 1 sesión |

Orden recomendado: 1 → 2 → 4 → 3 (el scrub es lo más caro; identidad y
world-space feedback son el 80% de la percepción con el 40% del esfuerzo).

## 5. Notas técnicas

- Seguir con uGUI por código está bien para todo esto; no migrar a UI Toolkit
  (costo alto, ganancia nula a esta escala). TextMeshPro tampoco es necesario
  si la fuente pixel entra como TTF legacy (Press Start 2P funciona).
- World-space text: TextMesh clásico o quads con la misma fuente — barato.
- La cámara dinámica es presentación pura: no toca sim ni replay.
- Riesgo a evitar: gold-plating antes de que el gameplay asiente. Las fases
  UI se intercalan con balance/features, no las reemplazan.
