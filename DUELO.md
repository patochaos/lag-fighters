# Modo DUELO — el núcleo casual (spec, 2026-07-25)

> Decisión de Patricio (2026-07-25): **DUELO pasa a ser EL juego**. Clásico,
> YOMI discreto y CARTAS v2 quedan como modos EXPERTO detrás de "MÁS MODOS".
> Eje de adivinanza elegido: **alturas** (sin distancias).
>
> Fundamento en [YOMI-BIBLE.md](YOMI-BIBLE.md); el análisis comparativo que
> llevó acá está en la conversación del 2026-07-25 y resumido en §0.

## 0. Por qué existe

El problema de Yomi 2 (y de nuestra copia fiel en [YOMI2-CARDS.md](YOMI2-CARDS.md))
no es que sea profundo: es **dónde vive su complejidad**. Vive en el
REGLAMENTO — ~30 keywords que interactúan. El §5 de ese documento proponía
cortar 6; con 24 seguís necesitando el manual.

El principio estructural que adoptamos (Marvel Snap, Fantasy Strike, Yomi 1):
**la complejidad va en el CONTENIDO —cartas de una línea, personajes que
re-pesan números— nunca en el SISTEMA.** Hoy `dodge`, `proyectil`, `super`,
`combo` y `ability` son *categorías del reglamento* cuando deberían ser
*cartas*.

Objetivo medible: **≤10 conceptos, ≤2 decisiones por turno, 8-14 turnos por
partida, 3-5 minutos**, con la brecha de habilidad (§6) intacta.

## 1. Las reglas (todas — esto es el juego entero)

1. Los dos eligen **una carta en secreto** y se revelan a la vez. No hay
   jugador activo, no hay fases, no hay main phase.
2. **GOLPE gana a AGARRE · AGARRE gana a DEFENSA · DEFENSA gana a GOLPE.**
3. Golpe vs golpe (y agarre vs agarre): gana el número **azul**
   (velocidad). **Empate = se pegan los dos** y nadie cobra premio.
4. Cada golpe es **ALTO** o **BAJO**; cada defensa cubre **una** altura.
   Defendiste la equivocada → comés el golpe entero.
5. El que gana el intercambio cobra el **daño rojo** y elige su premio:
   **+DAÑO** (descartás una carta de golpe de tu mano y sumás su daño) o
   **DERRIBO** (el rival no puede jugar DEFENSA el turno que viene).
6. Defender un ataque **roba 2 cartas** y la defensa **vuelve a tu mano**.
   Atacar gasta cartas.
7. Robás 1 por turno, mano máxima 8. Sin mazo: se remezcla el descarte
   **una vez**; la segunda vez es **TIME OVER** y gana el que tiene más vida.

Eso es todo. Nada más se explica con palabras: el resto está en los números
de las cartas.

### Anatomía de carta

**VERBO + VELOCIDAD (azul) + DAÑO (rojo) + ALTURA.** Sin texto.
Las cartas firma de cada personaje llevan **una línea, máximo** (Ley 14:
si una carta necesita dos líneas, está mal).

### Resolución completa (la tabla, para que no queden huecos)

| | Golpe | Agarre | Defensa |
|---|---|---|---|
| **Golpe** | gana el más rápido; empate = trade | **golpe gana** (sin mirar velocidad) | gana la defensa si acertó la altura; si no, gana el golpe |
| **Agarre** | — | **TECH**: los dos agarres son la misma carta, nunca desempata | **agarre gana** |
| **Defensa** | — | — | no pasa nada (la defensa vuelve, **no roba**: solo roba si paró un ataque) |

Defender **no hace daño**: su premio es la ECONOMÍA (Ley 2 — cada opción
paga en una moneda distinta). Ganar con golpe/agarre paga en daño+estado.

## 2. El mazo (20 cartas, esqueleto común)

Como en Yomi 2, el esqueleto de normales es compartido y la identidad vive
en las 4 cartas firma.

| Carta | Copias | Verbo | Vel | Daño | Altura |
|---|---|---|---|---|---|
| A — Jab | 2 | Golpe | 8 | 3 | **BAJO** |
| B — Directo | 2 | Golpe | 7 | 4 | **BAJO** |
| C — Gancho | 2 | Golpe | 6 | 5 | **ALTO** |
| D — Patada | 2 | Golpe | 4 | 7 | **ALTO** |
| Agarre | 3 | Agarre | 5 | 6 | — |
| Defensa ALTA | 2 | Defensa | — | — | ALTO |
| Defensa BAJA | 2 | Defensa | — | — | BAJO |
| Firma ×2 tipos | 4 | (personaje) | | | |
| **ESCAPE** | 1 | — | — | — | — |

**La correlación es el juego**: rápido = bajo, lento = alto. El jugador
aprende en dos turnos que "si respeta mi velocidad, defiende bajo", y las
cartas firma existen para **romper esa correlación** (un ALTO rápido).

- **HP 46** (30 en la spec original; el lab lo subió para que la partida dé
  tiempo a que una lectura se acumule — ver §9).
- **Mano inicial 6**: Guardia alta + Guardia baja + Agarre + ESCAPE + 2 al azar.
- **ESCAPE** (la válvula, Ley 13): arranca en mano, **una por partida**, no
  vuelve al mazo ni siquiera por remezcla. **No pasa nada ese turno**: es la
  respuesta al derribo (no podés defender, así que congelás el turno). Es
  escape, no ventaja: no hace daño ni cobra premio.
  *(La spec decía además "cancela tu derribo": sobra — el derribo dura un
  solo turno, así que congelarlo YA es la cancelación. Una cláusula menos.)*

### Personajes (Ley 11: re-pesar, no sumar reglas)

**GRAVE** — el que controla el espacio.
- **X — Nube eléctrica** ×2: Golpe, vel **10**, daño 4, BAJO.
  *"Aunque te la defiendan, pega 2."*
- **Z — Torbellino** ×2: Golpe, vel 7, daño 6, **ALTO**.
  (el rompe-correlación: alto y rápido — caza al que defiende bajo)

**JAINA** — la que apuesta.
- **Y — Espada del alba** ×2: Golpe, vel **11**, daño **6**, **ALTO**.
  *"Si te la defienden, el rival te pega un golpe de su mano, gratis."*
- **K — Patada cruzada** ×2: Golpe, vel **6**, daño 5, BAJO.
  *"Si conecta, el DERRIBO es gratis (igual podés elegir +DAÑO)."*

Los dos números en negrita salieron del lab: con Y a 8 de daño y K a vel 8
(que dominaba al Jab: mismo speed, más daño y derribo gratis) Jaina iba
**63/37**. Con el ajuste el matchup quedó **50/50**. Ley 11 en acción:
se tocaron PESOS, no reglas.

**GOLEM** — el grappler (agregado 2026-07-25, a pedido).
- **R — Roca Rodante** ×2: **Agarre**, vel **3**, daño 5.
  *"AGUANTE: te pegan y te agarra igual (cobran los dos)."*
- **H — Cabezazo** ×2: Golpe, vel 3, daño **9**, ALTO.
- Además: **+8 de vida**. Con 5 agarres en 20 cartas, defenderle sale
  carísimo — hay que pelearle, y pelearle es lo que castiga el Cabezazo.

El **super armor** es la única mecánica nueva desde la spec original, y entra
como corresponde: **una línea en UNA carta**, no una categoría del sistema.
En la tabla, golpe-vs-agarre deja de ser derrota limpia y pasa a ser un
CAMBIO (cobran los dos, nadie cobra premio). Y se paga caro: la Roca es el
agarre **más lento del juego** (pierde con el agarre común) y el que menos
pega. Con vel 7 / 8 de daño no perdía con nada y el Golem se iba a **65.8%**
en el lab; con vel 3 / 5 de daño quedó en 52.1%.

Ninguna de las seis agrega una categoría nueva ni una excepción a otra
regla: son números y una consecuencia de una línea.

## 3. Qué se corta de CARTAS v2 y por qué se puede

| Se va | Reemplazado por |
|---|---|
| Turno activo, empate al activo, robo asimétrico, "el rival no roba en tu turno" | **Reveal simultáneo** (Ley 8) — 4 reglas menos y ambos juegan siempre |
| Main phase entera: exchange, power up, ability, innates | Nada. Es un juego de cartas *antes* del juego de pelea |
| Combos: CP, chains, starters/linkers/enders, can't-combo, pumps, "el KD se pierde si seguís" | La binaria **+DAÑO / DERRIBO** (Leyes 6 y 12 comprimidas en un botón) |
| Super meter, supers, Power Up | Nada por ahora; si vuelven, como carta de una línea |
| Niveles de proyectil, lockdown, unsafe on block, recurring de especiales, edge | Números (velocidad 10 = "proyectil") y las líneas de las cartas firma |
| **Wild swing** | Desapareció solo: el derribo no PROHÍBE la guardia, la APAGA — así toda carta es siempre jugable y no hace falta la plomería del opener inválido |
| Distancia CERCA/LEJOS (del modo YOMI discreto) | Alturas: un solo eje de adivinanza |
| AP / cargar | **La mano ES la economía** — dos economías en paralelo es redundante |

Lo que **no** se toca (era el veredicto correcto del §5 de YOMI2-CARDS):
triángulo, alturas, velocidad, defender-roba-carta, defensa recurrente,
descarte público.

## 4. De dónde sale la profundidad

| Fuente | Ley | Costo en reglamento |
|---|---|---|
| Alturas → **defender también es adivinar** | 4 | media línea |
| Velocidad → golpe vs golpe no es lotería | 1 | un número |
| Mano oculta + **descarte público** → se lee, no se adivina | 5, 7, 9 | cero |
| Binaria daño/derribo → el premio tiene una decisión adentro | 6, 12 | una línea |
| Atacar gasta lo que necesitás para defender | 9 | cero |
| Personajes = re-pesar números | 11 | cero |
| ESCAPE: la válvula única y visible | 13 | una línea |

Siete fuentes de profundidad, siete reglas. Es la Ley 14 aplicada:
**pocas reglas que interactúan mucho**.

## 5. Arquitectura

Calca de lo que ya funciona ([CardSim.cs](Assets/Scripts/LagFighter/CardSim.cs)
y [YomiSim.cs](Assets/Scripts/LagFighter/YomiSim.cs)):

- **`DuelSim.cs`** — sim pura determinista, sin UnityEngine: catálogo, mazos,
  mano, robo, descarte, remezcla, RNG con seed, la tabla y el premio. Se
  reusa ~60% de `CardSim.cs` (toda la plomería de mazo/mano/descarte).
- **Tests** en `Tools/SimTests`: una celda de la tabla por test + alturas +
  premio + derribo + escape + remezcla/time over. Estimado: ~30 tests.
- **Lab** en `Tools/SimHarness`: `duelo N`, más las métricas de §6.
- **IA** en `SimpleAI`: `PickDuelCard` (pondera mano, vida, derribo, hábito
  rival observado) + `PickDuelPrize`.
- **Teatro**: el mismo de CARTAS — los blockman actúan el fallo que la tabla
  ya decidió. La sim de frames sigue siendo **títere mudo**.
- **UI**: el abanico de [CardHandUI.cs](Assets/Scripts/LagFighter/CardHandUI.cs)
  se reusa casi entero; se le sacan los botones CAMBIO/PODER/PUMP y queda
  **una sola acción: jugar la carta**. Aparece un único botón binario
  (+DAÑO / DERRIBO) cuando ganás.
- **Menú**: botón grande **JUGAR** (= DUELO vs IA). PRÁCTICA / VS IA /
  ONLINE / YOMI / CARTAS / LAG pasan detrás de **MÁS MODOS**.

## 6. Cómo se mide (el lab deja de opinar y mide)

- **Brecha de habilidad** = winrate de la IA heurística completa contra una
  IA que juega legal al azar. **Objetivo ≥75%.** Si baja, el juego es
  lotería. Medir también CARTAS v2 como baseline comparativo.
- **Valor de la información** = IA completa vs la misma IA con la lectura
  del descarte/hábitos apagada. Si da ~50%, la información pública es
  decorativa y la profundidad es falsa. Es el test más filoso del set.
- **Costo de entrada** = conceptos (≤10), decisiones/turno (≤2), palabras de
  texto en pantalla por turno.
- **Ritmo** = 8-14 turnos/partida, ≥95% KO (no time over), espejos 50/50.

## 7. Riesgos anotados (los originales, revisados contra el lab en §9)

- **Puede sentirse flaco**: 3 verbos + alturas es poco si los personajes no
  diferencian de verdad. Dial: números bien distintos y una tercera carta
  firma, no reglas nuevas.
- **El derribo puede degenerar** ("siempre derribo"): es el primer A/B del
  lab. Si el oki es demasiado, el dial es que el derribo apure velocidades
  en vez de apagar la defensa.
- **El chip de la X de Grave**: el lab del clásico ya enseñó que el chip
  solo fluye hacia el zoner y regala matchups (Zoner-Defensive 46→65%).
  Vigilarlo desde la primera corrida.
- **Defensa vs defensa** = turno muerto: si dos tortugas se encuentran, el
  mazo decide. Si el lab muestra time over >10%, el dial es que la defensa
  que no paró nada tampoco vuelva a la mano.
- **Perdemos foco en CARTAS v2** (119 tests, copia fiel que funciona): no se
  borra, pasa a modo EXPERTO. El trabajo queda vivo.

## 8. Orden de implementación

1. ~~`DuelSim.cs` + tests~~ **HECHO** (2026-07-25): 974→ `DuelSim.cs`, 27
   tests nuevos (146 en total, `pwsh Tools/verify.ps1` en verde).
2. ~~Lab + métricas~~ **HECHO**: `duelo N`, `duelogap N`, `duelotune N`.
2b. ~~LOS CANTOS en sim + lab~~ **HECHO** (2026-07-25 noche, ver §11):
   `duelocantos N` para los diales. Falta la UI del canto (y el
   respondedor adaptativo de la IA).
3. UI: reuso de `CardHandUI` + botón binario + HUD (vida, mano, mazo,
   descarte público de ambos, derribo).
4. Teatro y presentación (reveal de las dos cartas + el fallo cantado).
5. Reordenar el menú: **JUGAR** grande, el resto en MÁS MODOS.
6. Onboarding: 3 primeros turnos guionados contra un dummy que hace lo que
   la UI dice que va a hacer.


## 9. Lo que dijo el lab (2026-07-25, primera pasada)

Comandos: `dotnet run --project Tools/SimHarness -- duelo 8000`
(balance) · `duelogap 8000` (profundidad) · `duelotune 4000` (barrido de
diales).

### Números finales (8000 partidas IA vs IA)

| Métrica | Valor | Objetivo |
|---|---|---|
| KO | **99.8%** (0.2% time over) | ≥95% |
| Turnos/partida | **13.5** | 8-14 |
| Mano promedio | 5.2 / 8 | que respire sin desbordar |
| Premio +DAÑO vs DERRIBO | **60 / 40** | que las dos ramas se usen |
| Winrate por personaje | **Grave 49.8 · Jaina 48.1 · Golem 52.1** | parejo (±2) |
| Brecha de habilidad (heurística vs random) | **77.5%** | ≥75% |
| Valor de la información | **+1.9 pp** | >0 |
| Control (leer vs no leer, ambos impredecibles) | **50.1%** | ~50% |
| Simetría de lados (sin alternar) | 50.8 / 50.4% | ~50% |

Acierto de altura al defender: **46%** — la adivinanza defensiva está viva
(no es ni gratis ni imposible). Con lectura sube a 53.5% contra un rival
con tic, contra 50.6% sin lectura.

### Los cuatro diales que movió el lab (y por qué)

1. **Robo por turno: 2 → 1.** Con robo 2 la mano queda gorda (6.5/8) y
   quemar una carta por +DAÑO sale gratis: el premio se resolvió en
   **95% daño / 5% derribo**, o sea la decisión de la Ley 12 murió. Con
   robo 1 vuelve a 60/40. *La escasez de mano ES lo que hace que el premio
   sea una decisión.*
2. **Vida 30 → 46.** A 30 la partida dura 8.5 turnos: no da tiempo a que
   una lectura se acumule (el valor de la información quedaba en el ruido).
3. **Defender bien roba 1 → 2.** Sin esto, defender era un 50/50 entre
   ganancia chica y pérdida grande, y el agarre encima lo castiga: la
   guardia no llegaba a ser el "default barato" de la Ley 3.
4. **Nerfs a Jaina** (Y 8→6 de daño, K vel 8→6): de 63/37 a 50/50.

### Tres cosas que el lab pescó y valen como aprendizaje

- **La lectura mal diseñada PIERDE.** La primera versión de la IA hacía
  "el rival ataca mucho → defendé": medido, eso baja el winrate (control
  47%). Saber que va a atacar no sirve si no sabés la ALTURA, y defender
  de más te expone al agarre. La lectura correcta cambia **qué** guardia
  elegís, no **cuántas veces** defendés.
- **Una métrica puede mentir por el sparring.** Medir "¿sirve la
  información?" contra un bot aleatorio siempre da ~50%: no hay hábito que
  leer. Hace falta un rival competente **con un tic legible** — recién ahí
  la lectura muestra su valor.
- **Sesgo de lado que no era del juego.** P0 ganaba 52% con bots random y
  48% con la heurística (el signo se daba vuelta con la política: la firma
  de un artefacto). No era la sim: era `System.Random` correlacionando
  seeds que difieren en un offset constante — la misma trampa que ya había
  mordido el lab de cartas. Con seeds hasheadas: 50.8 / 50.4%.

### Riesgos originales, revisados

- ~~"El derribo puede degenerar en siempre-derribo"~~: pasó **al revés**
  (el +DAÑO domina si la mano sobra). El dial es el robo por turno.
- **El chip de Grave**: 1579 chips en 8000 partidas, matchup parejo. Sin
  señal de alarma por ahora.
- **Guardia vs guardia**: 0.2% de time over. No se estanca.
- **Pendiente de vigilar**: el valor de la información es positivo pero
  chico (+1.9 pp). La hipótesis es que la respuesta a una lectura es ella
  misma un 50/50 (elegir altura), así que el techo de la lectura es bajo.
  Si con humanos se siente "todo suerte", el dial a probar es que acertar
  la guardia pague MÁS (castigo del defensor, como la Y de Jaina pero
  universal) antes que tocar la tabla.


## 10. Segunda tanda (2026-07-25, tarde): la UI y el tercer personaje

- **`DuelHandUI.cs`** — la mano en abanico. Regla de la pantalla: *la altura
  y el verbo se leen SIN leer*. Cada carta lleva tres códigos redundantes:
  la **barra de altura vive arriba si el golpe es alto y abajo si es bajo**
  (posición = significado), el **color por verbo** (golpe naranja · agarre
  violeta · guardia celeste · escape verde) y las **keywords en chips** con
  fondo, nunca sueltas en un párrafo. Velocidad y daño en números enormes.
  Hover: agranda 1.5×, trae al frente y llena el panel de detalle, que
  explica la carta en castellano y sin jerga.
- **`DuelHudUI.cs`** — todo lo público, de los dos lados y siempre visible:
  vida exacta con barra y número, mano (como dorsos), mazo, descarte, y el
  strip **"LE QUEDAN"** por tipo de carta, con las guardias resaltadas. Ese
  strip es la Ley 5 hecha interfaz: *"ya gastó sus dos guardias altas →
  pegale arriba"*. Arriba al centro, el triángulo permanente (todo el
  reglamento en una línea) y la revelación de las dos cartas con el fallo
  cantado, que después se dockean a los costados durante la acción.
> **Actualizado el 2026-07-25 (noche):** el teatro de esta sección se rehízo
> entero con vocabulario propio — ver [DUELO-LOOK.md](DUELO-LOOK.md) §5 y
> `FighterViewDuel.cs`. Ya no se actúan moves del catálogo clásico: son 12
> poses de DUELO en tres tiempos, y **la consecuencia queda en pantalla**
> hasta la revelación siguiente. Lo que sigue es el registro de cómo era.

- **Teatro**: cada carta tiene su move y **la altura del golpe en pantalla
  coincide con la altura de la carta** — el mixup se aprende mirando.
  A→barrida · B→giro bajo · C→jab a la cabeza · D→golpe fuerte ·
  X→hadouken · Z→patada aérea · Y→shoryuken · agarre→agarre.
  El **derribo se ve**: el derribado queda en el piso durante TODA su
  planificación (la sim del teatro no avanza ahí), que es exactamente el
  turno en que su guardia no bloquea.
- **Trampa evitada** (el pecado del "títere mudo" de YOMI): la guardia usa
  `WalkB` para las dos alturas porque es el único move que `IsBlockingState`
  cuenta como bloqueo. Con `Parry` el defensor se comía el golpe EN PANTALLA
  contradiciendo a la tabla. Pendiente: reactivar `SimConfig.CrouchEnabled`
  para que la guardia baja tenga su pose agachada de verdad.
- **Menú**: DUELO es la primera tarjeta ("JUGAR — DUELO") con selector de
  los tres personajes; el resto quedó rotulado EXPERTO.

### Pendiente de verificación

El editor de Unity no estaba abierto en esta sesión, así que **la UI está
compile-checked y con los tests verdes, pero NO vista en vivo**. Falta la
pasada de layout real (que los paneles no se pisen con el HUD clásico, los
tamaños de fuente y el ritmo del teatro).

## 11. LOS CANTOS — envido y truco (spec 2026-07-25, noche — pendiente de sim y lab)

Sesión de diseño con Patricio sobre el hallazgo pendiente del §9: el valor
de la información es positivo pero chico (+1.9 pp), porque leer bien no
cambia lo que jugás (la respuesta sigue siendo un 50/50 de altura). Las dos
fantasías que el juego tiene que servir, en sus palabras: **la lectura dura**
("sabía exactamente qué ibas a hacer") y **el bluff cantado** ("te hice
creer una cosa e hice otra en tu cara"). Referencias elegidas: el truco, el
snap de Marvel Snap, Cosmic Encounter (poderes alien + reveal boca abajo),
los fighting reales.

> Evolución dentro de la misma sesión: la primera versión de este § era
> **EL DOBLO** solo (snap sin contenido). Patricio trajo su idea original
> — el envido como fase de información y el truco con su escalada — y el
> doblo quedó absorbido como el canto de TRUCO. La identidad completa:
> **el fighting del truco**. El experimento truco-fighter archivado murió
> porque el truco era el CORE y la fuerza quedaba plana (anti-patrón 5);
> acá es al revés — el core es el triángulo con alturas que ya midió 77.5%
> de brecha, y el truco entra como capa de apuestas ENCIMA.

### La gramática (un concepto, dos cantos)

Todo canto es **público, al empezar a planificar, antes de que nadie elija
carta**, y el rival responde antes de seguir: **QUIERO** (se juega lo
apostado) · **NO QUIERO** (concedés: el cantor cobra chico sin jugarse
nada) · **SUBIR** (escalás la apuesta, y la pregunta vuelve). El canto
rechazado SIEMPRE cobra — anti-patrón 6 (cheap talk) resuelto de raíz,
igual que en el truco de verdad.

### ENVIDO — la apuesta de INFORMACIÓN

- **Ventana**: solo mientras **nadie cobró daño** (la fase de estudio — el
  "neutral" del fighting, que es el rol que el envido tiene en el truco:
  antes de la primera carta). Primera sangre = ventana cerrada. Una vez
  por partida.
- **El tanto**: la suma de VELOCIDAD de **tus dos golpes de la MISMA
  altura** (el palo ES la altura). Con un solo golpe, esa velocidad; sin
  golpes, 0. *(Era daño; Patricio lo cambió a velocidad el 2026-07-26
  jugándolo: como rápido=débil, ganar el envido NO te hace favorito del
  combate — igual que el 33 del truco real, que no son las cartas que
  ganan la mano. Desacopla las dos apuestas y mata la bola de nieve. La
  siembra MEJORÓ: +10.7 pp.)*
- **QUIERO** → se comparan los tantos en secreto y el juego anuncia:
  el ganador cobra **3 de chip** y su tanto se hace PÚBLICO (número
  verificado por el juego, no declarable en falso); del perdedor solo se
  sabe que es menor — "son buenas": confesó un techo, no su mano.
- **NO QUIERO** → el cantor cobra 1 de chip, nadie muestra nada.
- **Por qué es LA pieza**: el pozo es asimétrico en dos monedas — el
  ganador cobra vida pero PAGA EN INFORMACIÓN. Y por la correlación
  rápido=BAJO/lento=ALTO del mazo, un tanto de 12 grita "tiene los ALTOS
  pesados" y uno de 7 dice "anda con los bajitos": **la declaración
  siembra la lectura de alturas de toda la partida**. El envido fabrica
  la materia de lectura (Ley 5) que al +1.9 pp le faltaba, voluntariamente
  y con precio.
- **Subidas** (REAL ENVIDO / FALTA): anotadas, NO en v0.

### TRUCO — la apuesta de SANGRE (el ex-DOBLO, ahora con respuesta)

- Cualquier turno, en la planificación: **TRUCO** → el próximo intercambio
  ganado vale **×2** (daño y premio para quien lo gane, sea quien sea —
  el riesgo es simétrico: la apuesta es tu confianza en tu lectura).
- Respuesta: QUIERO · NO QUIERO (el cantor cobra 2 de chip y el turno se
  juega normal) · **RETRUCO** (×3) → QUIERO · NO QUIERO (cobra 3) ·
  **VALE CUATRO** (×4) → QUIERO · NO QUIERO (cobra 4).
- El multiplicador queda **ARMADO hasta que alguien gane un intercambio**
  (guardia vs guardia o trade no lo disipan: la apuesta queda en el aire,
  como el truco pendiente de la última carta). *Dial: si la persistencia
  degenera, la alternativa es que valga solo ese turno.*
- **Por qué ataca el +1.9 pp**: la lectura no puede cambiar QUÉ jugás
  (techo medido) — el truco hace que cambie **cuánto vale el turno donde
  tenés razón**. Es un multiplicador sobre la calidad de lectura: cantás
  cuando LE QUEDAN y los hábitos dicen que este intercambio es tuyo. La
  habilidad del snap: no la carta, el CUÁNDO. Y siendo público, miente —
  cantar con mano mala para comprar respeto es el bluff cantado.

### Presupuesto Ley 14

Gramática de cantos (1) + envido (1) + truco (1) = **conceptos 8, 9 y 10
de 10**. El tope queda TOCADO: no entra nada más al sistema sin sacar
algo. Cero keywords nuevas en cartas, la tabla no se toca.

### Cómo se mide

- **Valor de cada canto** = IA que canta informada (tanto propio, LE
  QUEDAN, hábitos) vs la misma IA cantando al azar con igual frecuencia
  vs IA que nunca canta. Si informada ≈ azar, ese canto es moneda y se
  mata con datos.
- **La siembra del envido**: % de acierto de altura al defender POST
  envido querido vs partidas sin envido. Si no sube, el tanto no está
  filtrando lo que creemos.
- **Objetivo global**: el valor de la información del §6 sube de +1.9 pp
  a **>+5 pp** con los cantos en juego. Ese número justifica las 3 reglas.
- **Vigilar**: (a) bola de nieve — el envido paga vida al que ya ganó el
  sorteo de mano; el contrapeso es que queda cantado (la info filtrada es
  el impuesto al ganador). Si no alcanza, el dial de respaldo es premio
  económico (robar 2) en vez de chip. (b) TRUCO sobre rival derribado =
  oki doblado, puede ser demasiado gratis. (c) % de turnos con canto
  ≤ ~25% — si se canta siempre, no es un momento. (d) Que el flujo
  canto→respuesta no duela en el ritmo del turno (UI) ni en el protocolo
  online (agrega un sub-round-trip al lockstep).
- **Ley 11 / Cosmic Encounter**: personajes futuros re-pesan los cantos
  con una carta firma (el Apostador, el Mentiroso). Anotado, no ahora.

### Lo que dijo el lab (2026-07-25, noche — primera pasada de cantos)

**IMPLEMENTADO** en `DuelSim.cs` (sim pura: `Tanto`, `ResolveEnvido`,
`ResolveTruco`, multiplicador armado en `Land`) + 10 tests (170 en total) +
heurísticas en `SimpleAI` + negociación y métricas en el harness. Comandos:
`duelo N` (bloque de cantos en el reporte) y `duelocantos N` (barrido de
diales). **La UI del canto NO existe todavía** — contra humano aún no se
canta; es el próximo bloque.

Números con los defaults elegidos (envido 6 · fold sin bonus · premio
normal, 8000 partidas): KO 100% · 12.2 turnos · **cantos en el 21.8% de los
turnos** (bajo el techo del 25%) · personajes siguen parejos (±1) ·
brecha 78.6%.

- **Cantar PAGA**: la IA que canta le gana **53.2%** (con fold+2: 56.6%) a
  la misma IA que nunca canta. La agresión del canto es +EV por sí misma —
  muy truco — porque el que foldea correctamente igual sangra chips.
- **El chip del envido, calibrado** (la intuición de Patricio era exacta:
  "3 es poco, 15 define demasiado"): con 3 el ganador del envido gana el
  54% de las partidas (no pesa), con 15 el **75.6%** (la define). Quedó
  **6** → 58.8%: pesa sin definir.
- **La siembra funciona, chiquita**: acierto de guardia contra el CANTADO
  48.6% vs 46.8% global (+1.8 pp). El tanto público filtra de verdad.
- **Lo que FALTA — el timing del canto aún no es habilidad**: cantar
  informado empata (~50%) con cantar al azar a la misma frecuencia. Causa
  raíz medida: la respuesta del rival (foldear débil, aceptar fuerte) es
  correcta mire lo que mire, y el intercambio sigue siendo ~50/50 de
  altura, así que "cantar cuando estoy confiado" apenas predice. **PERO**:
  el respondedor de la IA no explota al spammer (no traquea la frecuencia
  de cantos del rival). Contra humanos el spam SE LEE y se castiga
  aceptando más. Próxima iteración de IA: respondedor adaptativo que
  traquea el hábito de canto rival — recién ahí sabremos si el timing
  puede ser skill o si hace falta un dial de diseño.
- **Descartado con datos**: multiplicar también el premio (`TrucoPrizeToo`)
  solo agrega varianza — la brecha de habilidad baja de 78.5 a 77.1 y no
  mejora nada. El `TrucoFoldBonus` (+2: no quiero paga 4/5/6) premia más
  al cantor (56.6%) sin volverlo más hábil: queda en 0, anotado como dial
  si el canto se siente débil contra humanos.
- **La escalada casi no aparece** entre IAs (×3: 38 veces en 8000, ×4: 3).
  El RETRUCO necesita al respondedor adaptativo (o humanos) para vivir.
- **Trampa de método** (para la colección del §9): el primer barrido dio
  números idénticos en las 4 celdas — `st?.Truco(d.ResolveTruco(...))`
  con `st` null **se saltea el argumento** (null-condicional), así que el
  truco no se resolvía en NINGUNA corrida 1v1. Media sesión de conclusiones
  sobre datos con el truco apagado. Resultado en variable propia, siempre.

> **Continuación (2026-07-25, más tarde):** el hallazgo de "el timing del
> canto no es habilidad" disparó una exploración en papel de DUELO
> reestructurado en manos cortas con mazo compartido
> ([DUELO-MANOS.md](DUELO-MANOS.md)) que quedó **EN PAUSA la misma
> noche**: al verla desplegada enredaba el juego. Veredicto de Patricio:
> **esta sección tal como está ES el diseño** — el truco es un
> multiplicador puro estilo póker, el no quiero paga chico y se sigue
> jugando, el envido da información y daño. Lo que sigue del §11 es
> pulir esto (respondedor adaptativo de la IA, la UI del canto), no
> reestructurarlo.

### Segunda tanda (2026-07-25, madrugada): la guardia cobra el truco + la revisada Sirlin

**Idea de Patricio**: el truco paga en la moneda de cada opción (Ley 2
aplicada al canto). Ganás el intercambio con golpe/agarre → daño
multiplicado; **lo ganás BLOQUEANDO → cartas multiplicadas** — robo base
**1** (era 2; con el truco potenciándolo, 2 era mucho), ×2 roba 2, ×3
roba 3, ×4 roba 4. El loop de tercer orden queda armado: gané el envido →
me leíste → bloqueo y robo (lo leído se vence) → pero vos lo sabés →
agarre rompe-guardia. Implementado en `StrikeVsGuard` + test.

**La revisada de mazos contra el Yomi 2 real** (fuente: la investigación
de [YOMI2-CARDS.md](YOMI2-CARDS.md) §1, rulebook v7.7 + Mizuumi; la wiki
da 403 al fetch automático pero los números ya estaban capturados y los
119 tests de CARTAS reproducen los combos del rulebook):

- **La curva `velocidad + daño = 11`** de las normales de Sirlin (8/3,
  7/4, 6/5, 5/6, 4/7) — las nuestras la cumplen, salteando el escalón
  5/6 a propósito.
- **Las firmas de Sirlin rompen la curva con miedo** (X 7/8 chip 4,
  Espada 11/10 unsafe, Y de Jaina velocidad 14 con daño propio); las
  nuestras estaban +1 a +3 sobre curva: tímidas.
- **Ajustes**: Agarre **6→7** (el throw real pega 5/7 — el depredador del
  bloqueo con dientes, clave ahora que bloquear cobra trucos) · X de
  Grave **4→5** (precio de firma, y crea LA colisión: X+X bajo = 10 =
  C+C alto). **El 10 es el número acertijo del envido por diseño** (X+X
  y K+K bajos vs C+C alto); la IA lee ≥11 = alto seguro, ≤9 = bajo
  seguro, 10 = sin señal.

**Lab (8000 balance / 6000 profundidad)**: KO 100% · 12.0 turnos ·
Grave 50.9 · Jaina 47.9 · Golem 51.2 (el agarre a 7 no disparó al Golem)
· cantos 20.5% de turnos · trucos cobrados bloqueando: 1244 de 8853
(14%) · siembra 48.8% vs 46.7% · **valor de la información +3.1 pp** (el
máximo histórico: baseline +1.9, primera pasada de cantos +0.8) · cantar
bien vs cantar al azar 51.2% (positivo por primera vez) · brecha 78%.
Pendiente hacia el objetivo >+5: el respondedor adaptativo y probar
contra humanos (la UI del canto).

## 12. El diseño cerrado en papel: ROUNDS y límites (2026-07-26, madrugada)

Última iteración solo-diseño antes de implementar (pedido de Patricio:
"primero tener la teoría correcta"). Decisiones tomadas:

### Rounds al mejor de 3

- **Vida por round ~24-28** (dial de lab: barrer 24/26/28 y re-chequear
  el chip de envido, que a menos vida pesa más relativo). Round = 6-8
  turnos; la partida entera sigue en 3-5 minutos.
- **Cada round arranca de cero**: mazo remezclado, mano inicial nueva,
  su propio ENVIDO (ventana hasta la primera sangre DEL ROUND) y **una
  sola cadena de truco por round** (como la mano del truco real: el que
  la abre, la abre; el otro sube adentro de la negociación; cerrada, no
  hay más). ESCAPE por round. Derribo y truco armado NO cruzan rounds:
  el estado muere con el round.
- **Lo único que persiste entre rounds es la lectura**: los hábitos
  observados del rival (y los de la IA). La información trabaja en dos
  escalas — la mano se renueva (el envido nunca está vencido), el
  JUGADOR se acumula. Es la respuesta al principio de "vida útil de la
  información" de DUELO-MANOS.md sin nada de aquella reestructura: el
  round ES la mano del truco, en idioma fighting, y cuesta CERO
  conceptos (los fighting tienen rounds de fábrica).
- Por qué la vida corta ahora sí (el lab la había subido 30→46): esa
  medición era pre-cantos — hoy el envido adelanta la información al
  turno 1, no hace falta una partida larga para construir la lectura.
- Bonus estructurales: freno anti-bola-de-nieve (la vida resetea),
  comeback real (perdiste un round, no la partida), y "ya se gastó el
  truco de este round" como información pública nueva (Ley 5 gratis).

### Auditoría contra YOMI-BIBLE.md (el estado de la teoría)

Cumplen bien: Leyes 1, 2 (reforzada: el truco paga en la moneda de cada
opción), 4, 5, 6 (con rounds), 8, 9, 10, 11, 12. Presupuesto Ley 14:
10/10 usado — los rounds no suman conceptos; NADA más entra sin sacar.

**Las tres fallas señaladas (en orden de gravedad):**

1. **Ley 7 / anti-patrón 5 — dispersión de fuerza suave.** Las manos
   salen del mismo esqueleto y se emparejan rápido; sin posiciones
   fuertes/débiles claras el bluff tiene techo (medido: cantar bien vs
   al azar 51.2%, no 55). El tanto público y los rounds ayudan. **Si el
   canto se siente flojo en vivo, la causa es esta ley, no el tuning**
   — el dial anotado: más varianza en la mano inicial (menos cartas
   garantizadas).
2. **Ley 3 en observación.** Robo por defender 2→1: el lab viejo decía
   que con 1 la guardia no era el default barato; hoy la compensa
   cobrar trucos bloqueando, pero es condicional. Medir si el pasivo
   que defiende ~45% sigue viable; si la tortuga muere, el agarre
   pierde presa.
3. **Ley 13 — truco sobre derribado.** La jugada más fuerte del juego
   (la guardia apagada no puede cobrar el canto); el ESCAPE la
   neutraliza una vez por round. Vigilar en vivo si se siente
   inescapable.

**Lo ignorado (pendiente, no roto)**: el onboarding guionado (ahora
también debe enseñar quiero/no-quiero) y la medición contra humanos —
todo el lab es IA-contra-IA, que acota por abajo (el spam de cantos que
la IA no castiga, un humano lo castiga solo). Miedo y respeto no se
miden en el lab. **La UI del canto es el instrumento de medición que
falta, no un lujo.**

### Orden de implementación (cuando Patricio dé el OK)

1. ~~Rounds en `DuelSim` + límites de cantos + tests~~ **HECHO**
   (2026-07-26): vida por round 26, marcador 2 de 3, envido y UNA cadena
   de truco por round, escape por round, el estado muere con el round.
   163 tests en verde.
2. ~~Lab~~ **HECHO**: barridos `duelotune` (vida 24/26/28 × robo 1/2 +
   mano suelta). Resultados abajo.
3. ~~La UI del canto~~ **HECHO**: `DuelCantoUI.cs` — botones ¡ENVIDO! /
   ¡TRUCO!, modal QUIERO / NO QUIERO / SUBIR (con retruco y vale cuatro
   negociados de verdad), banners de resultado, línea de estado (round ·
   marcador · ×N EN JUEGO · tanto cantado) y la ceremonia de round en
   `MatchController`. **Compile-check y tests OK, NO visto en vivo** —
   Patricio lo prueba.
4. Onboarding guionado — despriorizado a pedido (es un prototipo).

### Tercera tanda (2026-07-26): rounds implementados + lo que midió el lab

- **La siembra EXPLOTÓ con los rounds: 57.8% vs 49.1% (+8.7 pp)** de
  acierto de guardia contra el que cantó. Con la mano fresca por round
  el tanto nunca está vencido — la teoría de la vida útil, confirmada
  con el número más claro que dio este proyecto.
- **La brecha de habilidad subió a 87%** (era 78 sin rounds).
- Ritmo: 14.3 turnos y 2.6 rounds por partida, KO 100%, cantos en el
  17.7% de los turnos, 1.45 envidos/partida. envido→round 59% (pesa sin
  definir, con chip 4). Personajes 52.2 / 49.5 / 48.3.
- **El escenario Ley 3, adoptado con datos… y REVERTIDO jugando**
  (2026-07-26, más tarde): robo 2 midió mejor en el lab, pero Patricio
  lo probó en vivo y defender se SIENTE OP — robo por defender vuelve
  a **1** (con truco: 2 · retruco 3 · vale cuatro 4, sus números
  originales). Lección de método: el lab es IA vs IA y acota por abajo;
  cuando el humano juega, su sensación manda sobre el barrido. El
  balance no se movió (52/48/50, siembra +8.4 pp intacta). Anotado
  además del playtest: el "pegar gratis" que se sintió fuerte era el
  castigo de la Espada de Jaina (fiel al Unsafe on Block de Yomi 2);
  si la defensa sigue rindiendo mucho, los diales Sirlin pendientes
  son chip en las firmas (la Flame Arrow real pega 5 de chip) y
  recién después el Lockdown. La mano suelta (Ley 7) sigue descartada:
  +0.8 pp de canto a cambio de −2.5 de brecha.
- **El tanto pasó a VELOCIDAD** (mismo playtest): con daño, el ganador
  del envido tenía además las mejores cartas — apuestas acopladas, bola
  de nieve. Con velocidad se desacoplan (rápido=débil) y como
  rápido=BAJO, el tanto grande filtra "tiene los bajitos" — con la Y de
  Jaina (vel 11, ALTA) como la mentirosa del sistema. Medido: siembra
  58.9% vs 48.2% (**+10.7 pp**, el mejor número del proyecto), envido en
  el 70% de las partidas, tanto ganador promedio 17. A VIGILAR: Grave
  subió a 53.0 (su X vel 10 lo hace favorito del envido) — si en vivo
  se nota, el dial es el umbral de canto de la IA o el chip.
  La colisión "engineered" del 10 por daño quedó obsoleta: con
  velocidad las colisiones salen solas en la franja 13-15.
- Rebalances de la escala nueva: Golem **+8→+4** de vida (por round, +8
  era +31% y lo mandaba a 63%), envido **6→4** (proporcional a vida 26).
- **Flag Ley 12 pendiente**: el premio quedó 83/17 (+DAÑO domina — el
  derribo vale menos en rounds cortos). Dial a revisar tras jugarlo.
- Métrica "valor de la información" (vs el bot con tic): ruidosa entre
  corridas (+0.8 a +3.1) — con rounds, la SIEMBRA es el termómetro
  bueno de la información, no ese head-to-head.

### DUELO ONLINE (2026-07-26): sala con código, para probarlo con gente

Pedido de Patricio: el mismo esquema del ONLINE clásico (invitación de 4
letras sobre el relay Supabase de `NetLobby`), para aprobar el modo
jugando contra personas. Implementado:

- **Lockstep ESPEJADO**: cada cliente construye su `DuelSim` con él mismo
  como lado 0, y los streams de RNG viajan con el JUGADOR (`streamTag`),
  no con el índice. Como toda la resolución es simétrica por lado, las
  dos sims espejadas quedan idénticas sin tocar una línea de la UI (que
  asume "vos = lado 0"). **Garantizado por test**: partidas completas
  espejadas con cantos, premios, rounds y remezclas coinciden exactas.
- **Protocolo por FASES** (mensajes `PushTurn/PollTurn`, frame local del
  emisor): `H{char}` hola (la seed sale del hash del código de sala) →
  por turno: `D{N|E|T}` DECLARO (cantar o pasar — elegir carta declara
  "paso" y la carta queda provisoria) → `R{Q|N|S}` la negociación
  (quiero / no quiero / subo, con retruco y vale cuatro) → `C{idx}` la
  carta (si hubo canto, se re-elige DESPUÉS de resolverlo: información
  pareja) → `PK/PD{idx}/U{idx}` el premio o castigo del ganador. La cola
  procesa en orden y los mensajes de fases futuras esperan.
- **Menú**: DUELO → personaje → ¿CONTRA QUIÉN? (VS IA · ONLINE crear ·
  ONLINE unirse). El host es lado 0; sin revancha local (sala nueva).
- **Limitaciones anotadas**: los DOS tienen que entrar por DUELO→ONLINE
  (unirse a una sala de duelo desde el ONLINE clásico cuelga — el relay
  no distingue modos) · online hay UN canto por turno (contra la IA se
  puede envido+truco el mismo turno) · sin timer de planificación · sin
  reconexión. **Compilado y con el lockstep testeado, NO probado con dos
  clientes reales todavía.**

### Escenarios anotados por Patricio (2026-07-26, a revisar jugando)

1. **Escape vs truco armado**: hoy el ESCAPE congela el turno pero el
   multiplicador SIGUE armado. La lectura de Patricio: derribado+truco →
   el escape es la respuesta obvia → "te quemo el escape, pero quema el
   truco". ¿Debería el escape desarmar el truco? Y la capa que falta:
   **si el cantor ADIVINA que vas a escapar, debería poder castigarlo**
   — hoy no existe respuesta al escape (es la válvula por diseño, Ley
   13). Sin solución todavía; pensarlo después de jugarlo.
2. El derribo/premio en rounds cortos (el 83/17 de arriba).