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

Ninguna de las cuatro agrega una categoría nueva ni una excepción a otra
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
| Matchup Grave-Jaina | **50.6 / 49.4** | parejo |
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
