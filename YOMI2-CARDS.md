# Modo CARTAS — la copia de Yomi 2 (2026-07-21)

> Objetivo (pedido de Patricio): re-imaginar el combate como cartas. Copia
> casi exacta de **Yomi 2** de Sirlin, simplificada: **sin combos ni supers
> por ahora**, el resto igual. Un solo personaje (Grave, el shoto). Es un
> MODO nuevo — los modos clásico y YOMI discreto siguen intactos.

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
- **The Edge**: +3 speed (máx 10) por un combate. Grave no lo genera sin
  gems → queda anotado, no implementado.
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
  cada personaje tiene un tope de combo points. **Todo esto queda AFUERA
  en esta primera versión.**

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

### Después (anotado, no ahora)

- Combos con combo points (la razón de ser de las letras A-E).
- Super meter + Power Up + los dos supers de Grave.
- Más personajes = otros mazos (el catálogo ya queda por-personaje).
- Edge cuando haya moves que lo den.
