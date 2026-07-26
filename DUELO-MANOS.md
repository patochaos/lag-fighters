# DUELO EN MANOS — exploración en papel (2026-07-25, noche) — **EN PAUSA**

> **PAUSADA esa misma noche.** Al verla desplegada, Patricio la frenó: la
> reestructura en manos + mazo compartido enredaba el juego ("nos estamos
> enredando en nosotros mismos"). **El juego es el DUELO simple de
> [DUELO.md](DUELO.md)**: el triángulo con alturas + envido (información
> y daño) + truco como multiplicador puro estilo póker, donde el no
> quiero paga chico Y SE SIGUE JUGANDO — sin fold, sin manos, sin bazas.
> Los principios del §1 (vida útil de la información, colisiones del
> tanto, sin-fold-no-hay-bluff, reparto=dispersión) siguen siendo
> aprendizajes válidos y quedan acá archivados por si esta puerta se
> reabre.

## 0. Por qué existe esta exploración

Dos datos y una intuición convergieron:

1. **El lab midió la pieza que falta.** "Cantar informado" empataba con
   "cantar al azar" (§11 de DUELO.md). Diagnóstico fino: en el truco el
   canto cobra porque el rival puede **irse de la mano** — y cuando se va
   con cartas buenas por miedo, el bluff le robó equity real. Nuestro
   no-quiero concede un chip y el juego sigue: no hay nada que abandonar.
   El fold necesita una MANO barata de abandonar porque ya viene otra.
   No era un problema de tuning: era la ausencia de la estructura.
2. **El experimento truco-fighter murió por fuerza plana** (anti-patrón 5
   de la biblia: todo ~50%, sin miedo ni respeto ni bluff). El reparto de
   manos desiguales es la dispersión de fuerza de la Ley 7 de fábrica:
   mano buena/mala medible por ambos. Aquel prototipo tenía el truco como
   CORE y no tenía fighting; acá el core es el triángulo con alturas
   (77-79% de brecha medida) y el truco es la capa de apuestas encima.
3. **La intuición de Patricio**: el envido del truco informa porque la
   mano son 3 cartas de un mundo cerrado; el nuestro informa poco porque
   la mano rota. Y Cosmic Encounter: mazo compartido = si la tengo yo, no
   la tenés vos.

## 1. Principios nuevos (candidatos a la biblia)

- **Vida útil de la información**: la información que se apuesta/revela
  tiene que seguir siendo verdad durante la ventana donde se usa. El
  truco lo logra con manos que no rotan (3 cartas, sin robo); Yomi 2 con
  información acumulativa (el descarte solo crece); una foto de una mano
  que rota (nuestro envido v0) se vence en 2-3 turnos y vale +1.8 pp.
- **El número cantado necesita COLISIONES**: "tengo 13" es un acertijo
  solo si 13 tiene varias lecturas (¿10+3 o 7+6? ¿alto o bajo?). En el
  catálogo actual casi todo tanto tiene UNA descomposición (la única
  colisión entre alturas del juego es el 10 de Jaina: C+C alto vs K+K
  bajo). El mazo compartido se diseña para que los números mientan.
- **Sin fold no hay bluff**: la amenaza cobra solo si el otro puede irse
  dejando algo en la mesa. El fold debe ser barato (viene otra mano) y
  doler exactamente lo concedido.
- **Reparto = dispersión de fuerza** (Ley 7): manos desiguales son
  combustible, no ruido — SI la mano es corta y foldeable (la mala mano
  dura 3 cartas, no una partida).

## 2. LA MANO — boceto v0 (decisiones del 2026-07-25 en negrita)

**Cuadro 0 — el reparto.** Mazo COMPARTIDO en el medio (golpes con
altura/velocidad/daño, agarres, guardias — composición pendiente, §3.2).
**Se reparten 4 cartas; se juegan 3**: una queda sin jugar y nadie sabe
cuál (protege el conteo, y elegir cuál guardarse es una decisión). **Las
guardias son cartas del mazo**: te pueden no tocar → mano indefensa =
mano foldeable. Sin robo durante la mano; nada vuelve a la mano.
*Fundamento: la propiedad del truco — todo lo que sé de tu mano sigue
siendo verdad hasta la última carta.*

**Cuadro 1 — el envido.** Antes del primer intercambio, opcional:
quiero / no quiero (paga chico al cantor). Querido: **se canta el NÚMERO,
nunca las cartas** (tanto = par de la misma altura, con las colisiones
del §1); el perdedor dice "son buenas" y no muestra nada. Con la mano
congelada, el número trabaja toda la mano: "cantó 12 y ya jugó el Gancho
→ le queda la Patada, guardia arriba".

**Cuadro 2 — los intercambios.** Carta secreta simultánea y la tabla de
siempre: GOLPE > AGARRE > GUARDIA > GOLPE, la velocidad desempata, cada
golpe ALTO o BAJO, cada guardia cubre una altura. El triángulo medido no
se toca; la mano es el contenedor, el fighting vive adentro.

**Cuadro 3 — el corazón (HÍBRIDO).** **Cada intercambio pega su daño
(chico) Y ganar la mano — 2 bazas de 3 — cobra un premio gordo. El TRUCO
multiplica el premio de mano, no los golpes sueltos.** Cada choque duele
como fighting; la MANO es lo que se apuesta como truco.

**Cuadro 4 — el truco y el fold.** Cantable en cualquier momento de la
mano, escalable (retruco, vale cuatro). **No quiero = te vas de la
mano**: se tira todo, el cantor cobra chico, se reparte de nuevo. Mano
mala = te fuiste barato; mano buena asustada = el bluff te robó.

**Cuadro 5 — entre manos, el mazo se agota.** La mano siguiente se
reparte **del mismo mazo, sin remezclar, hasta agotarlo** (remezcla al
agotarse, como Yomi). Dentro de la mano: el mundo cerrado del truco.
Entre manos: el conteo acumulativo de Yomi — "las dos guardias altas ya
salieron: en esta mano nadie tapa arriba". El LE QUEDAN elevado a mesa.

**Cuadro 6 — la sangre.** Daño a una vida global; KO termina la partida.
Es un fighting: se juega al cuerpo, no a 30 puntos. Números pendientes.

## 3. Cola de diseño (uno por uno, en este orden)

1. **La baza, celda por celda**: ¿quién gana la baza en cada casilla de
   la tabla? (¿la guardia acertada GANA la baza o solo no pierde? ¿trade
   y tech son parda? ¿la parda a quién favorece — el truco dice "al
   mano"?). Y el premio de mano: cuánto, y si arrastra estado (Ley 6).
2. **El mazo compartido**: composición, cuántas cartas, cuántas guardias,
   y el diseño deliberado de colisiones de tanto entre alturas.
3. **Los números**: daño por golpe, premio de mano, vida global, precio
   del fold y de cada nivel de canto, largo esperado de la partida.
4. **Qué pasa con las piezas del DUELO actual**: derribo, premio
   +DAÑO/DERRIBO, ESCAPE, chip — ¿cuáles viven dentro de la mano, cuáles
   mueren? (El derribo que arrastra a la baza siguiente huele a Ley 6.)
5. **Personajes como PODERES estilo Cosmic** (re-pesan reglas, fuera del
   mazo) ¿y/o 1-2 firmas propias mezcladas al mazo común (verlas salir es
   información)?
6. **El "mano"** (ventaja de dealer): ¿hace falta con reveal simultáneo?
   ¿Las pardas la piden?
7. **Relación con el DUELO actual**: ¿lo reemplaza o conviven?

## 4. Registro de decisiones

- 2026-07-25 (noche): explorar el combate en manos EN PAPEL antes de
  tocar código · showdown del envido = solo el número, como el truco ·
  corazón híbrido (daño por intercambio + premio de mano, el truco
  multiplica el premio) · reparto de 4, se juegan 3, guardias en el mazo.
