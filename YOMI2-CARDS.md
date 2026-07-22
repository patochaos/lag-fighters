# Modo CARTAS — la copia de Yomi 2

> Objetivo (pedido de Patricio): re-imaginar el combate como cartas, copia
> de **Yomi 2** de Sirlin. La v1 (2026-07-21) cortaba combos y supers; la
> **v2 (2026-07-22) es la copia COMPLETA**: combos, super meter, supers,
> power up, abilities, pumps, edge y DOS personajes (Grave y Jaina). Lo
> único que queda afuera son los GEMS (customización aparte del core).
> Es un MODO nuevo — clásico y YOMI discreto siguen intactos.

## 0. La v2 en una pantalla (2026-07-22)

- **Mazos de 30 REALES**: 10 normales A-E, 3 agarres, 3 esquives, 2 blocks,
  X/Y/Z ×2, S1/S2 ×2 (una copia de cada super arranca en el DESCARTE,
  recuperable con Power Up) y la ability ×2. HP reales: Grave 90, Jaina 85.
- **Super meter (0-3 ★)**: se gana con Power Up (par descartado: +2, o
  super del descarte +1) y con **chain combos** (+1 por paso de letra,
  cobrado al instante — podés generar el meter de la super DENTRO del combo
  que la termina, como en el rulebook).
- **Combos completos**: combo points por personaje (Grave 4, Jaina 5),
  chains (A→B→C…), starters (agarre), linkers (Z), enders (X/Y/S1),
  can't-combo (S1 de Jaina). El KNOCKDOWN del agarre solo queda si NO
  seguís de combo; el último move del combo decide KD/edge.
- **Pumps**: Z quema el otro Z (+8/+7), la Y de Jaina quema cualquier carta
  (+5), su S1 quema hasta 2 supers (+9 c/u). No gastan combo points.
- **Abilities ongoing (2 combates)**: **Invocar Viento** (Grave): proyectil
  Nv.2 que le gana a esquives, +4/+2, supers a 2 CP (habilita Throw>S1).
  **Tiro en Arco** (Jaina): el rival que abre con ataque come 7 y no
  combea/pumpea; con bloqueo come 5 chip; la Y de Jaina pasa a ser segura.
- **Innates**: Grave cambia DOS veces por turno; Jaina (Imprudencia) si
  cierra su main phase con ambos blocks en el descarte: −2 HP y roba 1.
- **Supers**: Grave S1 Corazón de Dragón (s15, 20, unsafe, ★★) y S2 Poder
  de las Tormentas (★★★, super esquive: evita y devuelve 40 a strikes);
  Jaina S1 Dragón Rojo (★, s12, 10, sin combo, pump +9×2) y S2 Aliento de
  Dragón (★★, proyectil Nv.3, 18). El wild swing que da una super con
  meter DEBE jugarla (regla real).
- **UI nueva**: mano estilo **Slay the Spire** (`CardHandUI.cs`) — abanico
  de cartas grandes solapadas abajo, hover que agranda 1.5× y trae al
  frente, speed/daño en números grandes, estrellas de costo; botones
  CAMBIO / PODER (par → meter o super) / PUMP / TERMINAR-PASAR; pickers
  modales para el descarte y el beneficio del poder. Selector de personaje
  en el menú (GRAVE/JAINA; el rival lo sortea la casa).
- **Teatro**: el combo entero se ACTÚA en secuencia (cada carta con el move
  clásico más parecido: A/B→jab, C/D/E→golpe fuerte, X→proyectil,
  Y→shoryuken, Z→tatsu, S1/S2 de Jaina→Shinku, agarre→agarre); la
  duración escala con el largo del combo.
- **Lab v2** (4000 partidas, IA con main phase completa): KO 100%, 17
  turnos/partida, espejos 50/50, **Jaina 60/40 sobre Grave** (matchup real:
  S1 barata + Y s14 + chip 5 — vigilar con humanos), 16.7k combos, 4.6k
  pumps, 4.8k supers jugadas, 4.1k meter por chains, 1.6k imprudencias.
  Tests: **119 ok** (uno por regla, incluidos los combos del rulebook:
  Throw>D>E = 20+1★ exacto).

## 1. Investigación — las reglas reales de Yomi 2

Fuentes: rulebook oficial versus v7.7 (Road to Morningstar, PDF completo),
reseña GeekDad 2025, Mizuumi Wiki (página de Grave con framedata completa),
sirlin.net ("Introducing Yomi 2").

### Setup (real)

- Mazo de 30 cartas por personaje + 5 del gem elegido (no usamos gems).
- 1 copia de Super1 y Super2 arrancan en el DESCARTE (recuperables).
- Mano inicial de 8: **Low Block, High Block, un Throw normal y el Burst
  del gem** garantizados + 4 al azar. HP inicial en la carta de personaje.
- Se sortea quién empieza. **Turnos ALTERNADOS**: hay un jugador activo por
  turno, pero AMBOS pelean en el combate de cada turno.

### Estructura del turno (real)

1. **Draw**: robás 2 (solo 1 en el primer turno si sos el que empieza).
   El rival NO roba en tu turno.
2. **Main** (opcional, cada una máx. 1 vez, en cualquier orden):
   - **Exchange**: descartás una carta con ícono de exchange (todas las
     normales) y recuperás otra del descarte que también lo tenga.
   - **Ability**: jugás una carta de habilidad.
   - **Power Up**: descartás un par → +2 super meter, o super del descarte
     a la mano y +1 meter.
   - **Gem Storm** (1 por partida).
3. **Combate**: el activo juega su **opener boca abajo** → el rival juega
   el suyo **boca arriba** → se revela → resuelve la tabla → combos si
   corresponde.
4. Fin del turno: se descartan las cartas jugadas; turno del otro.

### Qué le gana a qué (real, tabla exacta)

- **Attack > Throw** · **Throw > Block y Dodge** · **Block/Dodge > Attack**
  (pero hay que bloquear la altura CORRECTA).
- **Attack vs Attack**: gana el de mayor **speed**; el empate lo gana el
  JUGADOR ACTIVO (su turno = su prioridad).
- **Proyectil vs Proyectil**: se ignoran los speeds, compara **nivel**
  (2 le gana a 1); mismo nivel = se anulan, nadie pega.
- **Proyectil vs Strike**: speed normal.
- **Throw vs Throw**: speed, empate al activo.
- **Alturas**: ataques **High / Low / Mid** (sin marca = mid). Low Block
  para lo bajo y lo mid; High Block para lo alto y lo mid. Bloquear la
  altura equivocada = comés el golpe entero.
- **Bloqueo exitoso**: robás 1 carta (salvo que el ataque tenga
  **Lockdown**) y el block es **Recurring**: vuelve a la mano al final del
  combate si abriste con él y no te pegaron. Los especiales pegan **block
  damage** (chip) aunque los bloquees.
- **Unsafe on Block**: si te bloquean un move unsafe, el que bloqueó roba
  y además te devuelve **UN ataque o throw** de su mano (queda como ender).
- **Dodge**: esquiva el ataque. Si esquivaste un **strike** (no un
  proyectil), devolvés **UN ataque o throw** de tu mano. Gasta cartas: es
  la defensa cara para el late game.
- **Knockdown**: los throws (y algunos moves) derriban. Derribado, el
  próximo combate: tus **dodges quedan deshabilitados** y los ataques y
  throws rivales más lentos que speed 10 **se aceleran a 10**. Dura UN
  combate. Ambos derribados = se cancela.
- **The Edge**: +3 speed (máx 10) por un combate. Implementado en la sim;
  ni Grave ni Jaina lo generan sin gems.
- **Wild swing**: si tu opener es inválido (dodge estando derribado, super
  sin meter…), lo descartás y jugás la carta de arriba del mazo como opener,
  repitiendo hasta que salga una válida.

### Economía (real)

- **Mano máxima 12**: lo que exceda se descarta.
- **Mazo agotado**: la PRIMERA vez remezclás el descarte (dejando afuera
  Low Block, High Block y una copia de cada super — siguen en el descarte,
  recuperables por exchange). La SEGUNDA vez: **TIME OVER**, gana el que
  tiene más HP.
- Los combos multiplican daño y generan super meter (A→B→C = +2 meter);
  cada personaje tiene un tope de combo points. **Implementado completo en
  la v2.**

### El mazo de Grave (real, Mizuumi) — HP 90, Zoning, max combo 4

| Carta | Copias | Tipo | Speed | Dmg | Block dmg | Altura | Notas |
|---|---|---|---|---|---|---|---|
| A. Quick Attack | 2 | Normal | 8 | 3 | — | **Low** | chain |
| B. Light Attack | 2 | Normal | 7 | 4 | — | **Low** | chain |
| C. Medium Attack | 2 | Normal | 6 | 5 | — | Mid | chain |
| D. Heavy Attack | 2 | Normal | 5 | 6 | — | **High** | chain |
| E. Power Attack | 2 | Normal | 4 | 7 | — | **High** | — |
| Throw | 3 | Throw | 5 | 7 | — | — | **Knockdown** |
| Dodge | 3 | Dodge | — | — | — | — | hit-back vs strikes |
| Low Block | 1 | Block | — | — | — | — | roba 1, Recurring |
| High Block | 1 | Block | — | — | — | — | roba 1, Recurring |
| X. Lightning Cloud | 2 | Especial | 7 | 8 | 4 | Mid | **Proyectil Nv.1**, Recurring, **Lockdown** |
| Y. Stormborne Sword | 2 | Especial | 11 | 10 | 2 | Mid | **Unsafe on Block** (el reversal) |
| Z. Whirlwind | 2 | Especial | 7 | 7 | 1 | **High** | el mixup rápido de altura |
| S1. Dragonheart | 2 | Super | 15 | 20 | 1 | Mid | FUERA (sin supers) |
| S2. True Power of Storms | 2 | Super dodge | — | 40 | — | — | FUERA |
| Wind Summon | 2 | Ability | — | — | — | — | FUERA (buffea combos/supers) |

### El mazo de Jaina (real, Mizuumi) — HP 85, Zoning, max combo 5

Normales idénticas a Grave (regla de Yomi 2: el esqueleto es común).
Innate **Imprudencia**: cerrar la main phase con ambos blocks en el
descarte = −2 HP y roba 1 (la agresión paga cartas con sangre).

| Carta | Speed | Dmg | Chip | Notas |
|---|---|---|---|---|
| X. Flame Arrow | 7 | 7 | **5** | Proyectil Nv.1, Recurring, Lockdown, ender 1CP |
| Y. Dragonheart | **14** | 8 | 1 | Unsafe, pump any+5, **−5 propio** (gratis con HP ≤ 35), segura con Arco, ender 3CP |
| Z. Crossfire Kick | 8 | 6 | 3 | High, linker 2CP, pump Z+7 |
| S1. Red Dragon | 12 | 10 | 2 | **★**, sin combo, unsafe, pump 2 supers +9 c/u |
| S2. Dragon's Breath | 8 | 18 | 4 | **★★**, proyectil **Nv.3**, ender 2CP |
| Tiro en Arco (ability) | — | — | — | ongoing 2 combates: ataque rival abre → 7 y sin combo/pump · bloqueo rival → 5 chip |

La lógica del personaje: A/B pegan BAJO y rápido, D/E pegan ALTO y fuerte
pero lento → el speed premia el golpe débil y el daño premia el lento: la
tabla de riesgo/recompensa está en las alturas. Z es el high a speed 7 que
caza al que bloquea bajo esperando A/B.

## 2. El plan — qué copiamos y qué queda afuera

### Se copia TAL CUAL

- Turnos alternados con opener boca abajo del activo / boca arriba del
  otro, y **empates de speed al activo**.
- Robo 2 por turno (1 el primero), mano máx 12.
- Exchange en la main phase (Grave puede DOS veces: su innate).
- Toda la tabla de combate: alturas, proyectiles por nivel, block draw +
  recurring + chip, lockdown, unsafe on block, dodge con hit-back,
  throw→knockdown, knockdown (dodges off, speeds→10), wild swing.
- Mazo agotado: remezcla 1 vez (blocks quedan en el descarte) → time over.
- El mazo de Grave completo con sus números reales, menos supers/ability.

### Queda AFUERA (por ahora)

- **Combos** (combo points, chains, pumps, enders) — los hits pegan su
  daño de carta y listo. El throw SIEMPRE derriba (en el real derriba solo
  si no seguís con combo).
- **Supers + super meter + Power Up** (sin meter, el power up no paga nada).
- **Gems, Burst, Gem Storm, Abilities**.
- **The Edge** (Grave no lo genera sin gems).
- Mazo resultante: 24 cartas (30 − 2 supers ×2 − ability ×2). Mano inicial
  de 7 (sin el Burst): Low Block + High Block + Throw + 4 al azar.
- **HP 45** (no 90): sin combos el daño por hit es la mitad del original —
  45 mantiene la duración de partida de Yomi 2. Validado en el lab.
- **Rounds**: una partida de cartas ES el match (Yomi 2 no tiene rounds).
  El que empieza alterna si hay revancha; el activo gana los empates.

### Arquitectura (calca del modo YOMI discreto)

- **`CardSim.cs`** — sim pura determinista (sin UnityEngine): catálogo
  (`CardDef`), mazos, manos, RNG con seed, fases y TODA la resolución.
  Cada regla es testeable. La UI y el teatro solo LEEN.
- **Tests** en `Tools/SimTests`: una celda de la tabla por test.
- **Lab** en `Tools/SimHarness`: `cards N` corre N partidas IA vs IA.
- **IA** en `SimpleAI`: elige opener (pondera mano/estado/knockdown),
  exchange y hit-back.
- **Teatro** en `MatchController`: como `BeginYomiTheater` — revelación de
  las dos cartas gigantes + los blockman actúan con los moves clásicos
  (A/B/C→jabs, D/E→strong, X→hadouken, Y→shoryu, Z→tatsu, throw→agarre).
  El HP real lo dicta `CardSim`; la sim de frames es un títere mudo.
- **UI**: mano de cartas abajo (hasta 12, dinámica), click = detalle,
  doble click/Enter = jugar; picker de exchange y de hit-back; HUD con
  HP, mazo restante, descarte del rival visible (es info pública real).
- **Menú**: modo nuevo CARTAS junto a NORMAL/LAG/YOMI.

### Estado (2026-07-21) — implementado y verificado

- `CardSim.cs` completo + 15 tests (una regla por test) en `Tools/SimTests`
  — 107 ok en `verify.ps1`.
- IA (`PickCardOpener`/`PickCardHitBack`/`DoCardExchanges` en SimpleAI) y
  lab `dotnet run --project Tools/SimHarness -- cards N`.
- **Lab a 4000 partidas**: P0 1999 vs P1 1975 (parejo), el que EMPIEZA gana
  52/48 (leve, fiel al original — el activo gana empates), **KO 99.9%**,
  14.4 turnos/partida, mano promedio 8.9, 15.5k bloqueos correctos vs 4.1k
  de altura equivocada, 3.6k esquives, 6.9k castigos, 1.2k proyectiles
  anulados. dmg/uso: Agarre 4.69 · X 4.29 · Y 3.25 · Z 3.23 · E 3.04 —
  ninguna carta domina ni sobra. (Ojo del lab: seeds consecutivas de
  System.Random se correlacionan y sesgan el head-to-head — se
  decorrelacionaron con multiplicadores primos.)
- Modo **CARTAS** en el menú (junto a PRÁCTICA/VS IA/ONLINE), vs IA.
  UI: la grilla ES la mano (posición = índice, hasta 12), botón contextual
  CAMBIO/CANCELAR/PASAR, picker de descarte para el exchange, castigo con
  la mano filtrada (ESPACIO pasa), revelación de cartas gigantes + fallo
  cantado (misma maquinaria del modo YOMI), teatro con moves clásicos
  (A/B→jab, C/D/E→strong, X→hadouken, Y→shoryu, Z→tatsu, throw→agarre,
  dodge→backdash) y HP del HUD proporcional (los números exactos van en
  prompt y popups).

## 3. Auditoría mecánica por mecánica (2026-07-21, segunda pasada)

Mapeo contra el rulebook v7.7. ✔ = implementado fiel · ✂ = cortado a
propósito (con el porqué) · ≈ = adaptado.

| Mecánica de Yomi 2 | Estado | Dónde / por qué |
|---|---|---|
| Mazo de 30 por personaje | ≈ | 24 (sin supers ×2 ni ability ×2) — `CardCatalog.DeckCounts` |
| Mano inicial: blocks + throw + burst + 4 | ≈ | 7 cartas (sin Burst: no hay gems) — test lo clava |
| Supers al descarte en el setup | ✔ | v2: una copia de cada una, recuperables |
| Turnos alternados, sorteo inicial | ✔ | `Active`, alterna por revancha |
| Draw 2 (1 el primero) · rival no roba en tu turno | ✔ | `StartTurn` + test |
| Mano máxima 12 (exceso al descarte) | ✔ | `AddToHand` + test |
| Exchange (1/turno · Grave: 2, su innate) | ✔ | `Exchange`, solo normales, solo el activo + test |
| Ability (Wind Summon / Arc Shot) | ✔ | v2: ongoing 2 combates, con tests |
| Power Up (par → meter/fetch super) | ✔ | v2: ambas ramas, con test |
| Gem Storm / Burst / gem specials | ✂ | sin gems (nota: el Burst era la válvula del derribado) |
| Opener boca abajo (activo) → boca arriba → reveal | ≈ | picks simultáneos en secreto — equivalente en información |
| Attack > Throw (sin importar speed) | ✔ | test: E (s4) le gana al Agarre |
| Throw > Block y Dodge · derriba | ✔ | tests |
| Block/Dodge > Attack con altura correcta | ✔ | tests de las 6 combinaciones de altura |
| Strike vs strike: speed, empate al ACTIVO | ✔ | test con Active=1 |
| Throw vs throw: speed, empate al activo | ✔ | test |
| Proyectil vs proyectil: SOLO nivel; igual = se anulan | ✔ | test X vs X |
| Proyectil vs strike: speed normal | ✔ | rama general |
| Bloqueo exitoso: roba 1 | ✔ | test |
| Lockdown (X): sin robo al bloquearlo | ✔ | test |
| Block damage (chip) de los especiales | ✔ | X 4 · Y 2 · Z 1 — el chip NO es "hit" |
| Recurring: vuelve si abriste y no te pegaron | ✔ | blocks y X + límite de mano |
| Unsafe on block (Y): robás y devolvés UN golpe | ✔ | test, con el orden oficial (robo → castigo → recurring) |
| Dodge: castiga strikes, NO proyectiles | ✔ | test |
| Castigo = ender (sin combo después) | ✔ | no hay combos: un solo golpe |
| Castigo con pump | ✔ | v2 |
| Knockdown: sin dodges + speeds rivales a 10, UN combate | ✔ | test (D s5→10 le gana al A s8 del caído) |
| Ambos derribados = se cancela | ✔ | `FinishCombat` |
| The Edge (+3 speed, máx 10) | ≈ | v2: el efecto está implementado; ni Grave ni Jaina lo generan sin gems |
| Wild swing (opener inválido → mazo) | ✔ | test (dodge derribado) |
| Remezcla ÚNICA dejando blocks (y supers) afuera | ✔ | test; sin supers quedan los 2 blocks |
| Segunda vez sin mazo = TIME OVER por vida | ✔ | test + fix: si salta a mitad de combate, se juzga DESPUÉS de aplicar el daño del turno |
| Descarte público y consultable | ✔ | HUD: descarte compacto de AMBOS lados siempre visible |
| Combos (chains, linkers, enders, combo points) | ✔ | v2 completo, con los combos del rulebook como tests |
| Super meter por chains | ✔ | v2: +1 por paso de letra, al instante |

### Qué asegura que una pelea completa funcione

- **Ataques**: 8 cartas de golpe + 3 agarres por mazo; la IA recupera
  golpes por exchange si se queda sin ataques en mano.
- **Defensas**: 2 bloqueos (recurring: casi siempre disponibles), 3
  esquives, y el exchange para recuperarlos del descarte.
- **Recupero de cartas**: robo 2/turno + robo por bloqueo + recurring
  (blocks y X vuelven solos) + exchange ×2 — la mano promedio del lab es
  8.9/12: la economía respira.
- **Cierre**: KO en el 99.9% de 4000 partidas (14.4 turnos promedio);
  el 0.1% restante termina por time over con juez por vida.


## 5. Análisis de CASUALIZACIÓN (2026-07-22) — qué cortar sin romper el balance

> Pedido de Patricio: análisis puro, sin código. Qué se puede simplificar o
> remover del modo CARTAS para hacerlo más casual. Los números salen del
> lab (4000 partidas IA vs IA, ~68.000 combates).

### El criterio

**"Casual" acá significa tres cosas medibles**: (1) menos REGLAS
simultáneas que recordar, (2) menos DECISIONES por turno (hoy un turno
propio puede encadenar: exchange ×2 → power up con 2 elecciones → ability
→ opener → combo carta a carta → pump → parar-o-seguir), y (3) menos
EXCEPCIONES ("...salvo que", el veneno de la curva de entrada).

**"Sin romper el balance" tiene una regla de oro**: los cortes SIMÉTRICOS
(le sacan lo mismo a los dos jugadores) son seguros por construcción; los
ASIMÉTRICOS (tocan una carta o habilidad de UN personaje) mueven el
matchup y necesitan re-medición. Hoy el matchup es Jaina 60/40 — un corte
asimétrico bien elegido puede incluso EMPAREJAR.

### La tabla: costo cognitivo vs valor real

| Mecánica | Costo cognitivo | Uso real (lab) | Veredicto |
|---|---|---|---|
| **The Edge** | 1 regla muerta (nadie la genera sin gems) | **0 usos** | **CORTAR YA** — costo cero, hoy solo existe en el manual |
| **Pumps** | alto: 3 combustibles distintos, decisión extra tras cada golpe, botón propio | 1.2/partida | **CORTAR** — es LA excepción fiddly; simétrico casi todo (ver nota Jaina S1) |
| **Lockdown (X sin robo)** | 1 excepción a "bloquear roba" | frecuente pero invisible | **CORTAR** — bloquear SIEMPRE roba: una regla menos, buff parejo al defensor |
| **Power Up doble opción** | media: par + elegir beneficio + elegir cuál super | 1 de cada ~2 turnos propios | **SIMPLIFICAR**: "par → +1★ y recuperá una super si hay" (una sola opción, sin picker) |
| **Wind Summon (3 efectos)** | alto: nivel+2, gana a esquives, +4/+2, CP de super | se juega seguido | **SIMPLIFICAR a UN efecto**: "tus supers cuestan 2 CP" (el único que habilita el combo nuevo Throw>S1 — el resto es letra chica) |
| **Arc Shot (3 efectos + condición de la Y)** | alto | **8.9k procs**: omnipresente | **SIMPLIFICAR a UN efecto**: "si el rival abre con ataque, come 7". Cortar el chip al bloqueo y la cláusula de la Y — además EMPAREJA el 60/40 (es la fuente #1 de la ventaja de Jaina) |
| **Self-damage de la Y + umbral 35** | medio: excepción con condición numérica | común | **SIMPLIFICAR**: self-damage fijo SIN umbral (una cláusula menos); o subirle 1 al costo y sacarlo |
| **Imprudencia (innate Jaina)** | medio: trigger de fase que sorprende | 1.6k procs | **DEJAR** (es identidad y es opt-in: solo dispara si VOS vaciaste tus blocks) o cortar ambos innates juntos (simétrico) |
| **Combo points + tipos de combo** | EL más alto del juego | 4+ combos/partida | **NO CORTAR, ASISTIR** (ver abajo) — es el payoff que hace que ganar el opener importe |
| **Super dodge de Grave (S2)** | medio: única carta "dodge que no es dodge" | **90 usos en 4000 partidas (2.3%)** | **REEMPLAZAR**: peor ratio valor/complejidad del juego; una super ATTACK simple (mismo slot) elimina una categoría entera de carta |
| **Wild swing** | bajo (la UI ya lo esconde) | 139 procs | dejar — es plomería invisible |
| **Recurring / robo por bloqueo / exchange** | bajo y CENTRAL | constante | **NO TOCAR** — es la economía que hace respirar la mano |
| **Alturas high/low/mid** | bajo (2 bloqueos, 3 alturas) | constante | **NO TOCAR** — es el alma de fighting game del mixup |
| **Tabla attack/throw/block/dodge + speeds** | bajo | constante | **NO TOCAR** — es el juego |

### Los combos: asistir, no amputar

El sistema de combos es el 60% del costo cognitivo (tipos, letras, CP,
meter por chain, "el KD se pierde si seguís"). Pero cortarlo cambia el
juego de género (ya lo vivimos: la v1 sin combos es EXACTAMENTE eso, y
está entera en la historia de git). Para casual, tres palancas que NO
tocan el balance porque no cambian ninguna regla, solo la presentación:

1. **Auto-combo sugerido**: al conectar, la UI ofrece UN botón "mejor
   combo" (la secuencia que la IA ya sabe calcular) además de las cartas.
   El que quiere optimizar a mano, puede; el casual apila daño con un click.
2. **Cartel de consecuencia**: "si seguís, PERDÉS el derribo" ya se
   muestra — subirlo a elección binaria explícita (DERRIBAR / +DAÑO) en
   los agarres, que es donde el casual se equivoca.
3. **Esconder la teoría**: no mostrar "STARTER/LINKER/ENDER" en las
   cartas; la UI ya ilumina qué sigue — los nombres de los tipos son
   jerga que solo paga en el rulebook impreso.

### Presets propuestos (de menor a mayor cirugía)

- **CASUAL SUAVE** (cortes simétricos, balance intacto por construcción):
  sin Edge, sin pumps, sin lockdown, power up de una sola opción,
  auto-combo sugerido, jerga de combos oculta. Se pierde: ~nada del yomi.
  Nota: sin pumps, la S1 de Jaina pierde su +18 potencial — como Jaina va
  60/40 arriba, este "des-balance" corrige en la dirección correcta.
- **CASUAL MEDIO**: lo anterior + abilities de UN efecto + self-damage
  sin umbral + S2 de Grave reemplazada por una super attack simple.
  Asimétrico pero dirigido: cada corte le saca más al personaje que va
  ganando (Jaina) o elimina la carta menos usada del juego (S2 Grave).
  Re-medir con el lab (los flags de config ya existen como patrón en el
  proyecto: SimConfig.*Enabled).
- **CASUAL TOTAL**: la v1 sin combos/supers/meter/abilities (commit
  54a3afc): HP 45, un golpe = su daño y listo. Ya está probada: 14
  turnos/partida, KO 100%, parejo. Es, en la práctica, el "modo arcade"
  gratis — recuperarla como toggle sería reactivar código, no diseñar.

### Lo que NO se toca bajo ningún preset

La tabla de counters (attack>throw>block/dodge>attack), las alturas, el
speed con empate al activo, el agarre que derriba, bloquear-roba-carta,
recurring, el exchange y el descarte público. Eso ES Yomi 2: cualquier
corte ahí no da un Yomi casual, da otro juego (y para eso ya está el modo
YOMI discreto, que es exactamente esa reducción bien hecha).

### El orden si esto se implementara mañana

1º Edge (borrar 3 líneas de manual), 2º pumps + lockdown + power up
simple (una tarde, simétrico, sin lab), 3º auto-combo sugerido (el mayor
salto de accesibilidad por hora invertida), 4º abilities de un efecto
(con pasada de lab), 5º presets como toggle de menú. Todo lo demás,
dejarlo quieto.

### Después (anotado, no ahora)

- Combos con combo points (la razón de ser de las letras A-E).
- Super meter + Power Up + los dos supers de Grave.
- Más personajes = otros mazos (el catálogo ya queda por-personaje).
- Edge cuando haya moves que lo den.
