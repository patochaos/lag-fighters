# YOMI BIBLE — qué hace funcionar un fighting game por turnos

Lecciones destiladas de David Sirlin (Yomi 1/2, Fantasy Strike, "Designing Yomi",
"Playing to Win"), Exceed, BattleCON, y experimentos propios (2026-07-20/21:
~200k manos simuladas de variantes de resolución por cartas). Referencia para el
modo YOMI de Lag Fighters y cualquier diseño futuro del género.

---

## 0. La premisa del género

Un fighting game vive en el borde de la reacción humana (~16 frames): todo lo
que resuelve más rápido es **predicción**. Un fighting por turnos elimina la
ejecución y la reacción por completo — lo único que queda es la predicción.
Eso significa que TODO el juego tiene que salir de una sola pregunta bien
diseñada: *"¿qué va a hacer el otro?"*.

Yomi = leer la intención del rival. Los "yomi layers" de Sirlin: nivel 0 (mi
mejor jugada) → nivel 1 (countereo tu mejor jugada) → nivel 2 (countereo tu
counter) → nivel 3 (que vuelve a ser el nivel 0). El ciclo cierra en ~3 capas y
por eso el metagame nunca se resuelve. **Pero el ciclo solo gira si las capas
pagan distinto** — ver Ley 1.

---

## 1. Las Leyes

### Ley 1 — Piedra-papel-tijera puro es lotería. Lo que lo vuelve juego son los payoffs asimétricos.
RPS con premios iguales no tiene decisiones: cualquier distribución es tan
buena como cualquier otra. El guessing se vuelve *hábil* cuando: (a) cada
opción paga distinto al ganar, (b) el valor de cada opción depende del estado
(vida, recursos, posición), y (c) esa valuación es difícil de computar exacta.
Sirlin: el jugador debe hacer "on-the-fly judgments about what things are
really worth" — si la matriz se puede resolver, se memoriza y muere.
**Medido en sims propias**: con triángulo que gana "automático", la fuerza de
cualquier mano converge a ~50% y ninguna decisión de riesgo tiene dientes.

### Ley 2 — Cada opción convierte la victoria en una MONEDA distinta.
El corazón de Yomi no es quién gana el reveal sino **qué cobra el ganador**:
- Ataque gana → convierte MANO en DAÑO (combos).
- Bloqueo gana → convierte defensa en ECONOMÍA (robás carta, el bloqueo vuelve).
- Esquive gana → contraataque único gordo (daño sin combo, gastando mano).
- Agarre gana → daño medio + ESTADO (knockdown), y existe para que defender no sea gratis.
Si todas las opciones pagan en la misma moneda (solo daño), el juego es plano.
Diseñá primero las monedas, después los números.

### Ley 3 — Tiene que existir una opción default barata... y su depredador.
El bloqueo es el pívot: pierde poco, estabiliza, deja pensar. Sin default el
juego es ansiedad pura; con default gratis es tortuga. El equilibrio clásico:
el agarre existe EXCLUSIVAMENTE para castigar al que abusa del default.
Regla práctica: el default debe ser la opción correcta ~40-50% del tiempo
para un jugador pasivo... y perder feo contra su counter dedicado.

### Ley 4 — La opción defensiva también tiene que ser una adivinanza.
La jugada maestra de Yomi 2: partir el bloqueo en ALTO y BAJO, y hacerlo
**escaso** (dos cartas en todo el mazo, se recuperan con esfuerzo). Resultado:
elegir "bloquear" no cierra la decisión — abre otra (¿alto o bajo? ¿gasto mi
único bloqueo alto acá?). Anti-patrón directo: el bloqueo como botón único
que siempre está disponible y siempre "funciona". Si defender es trivial,
atacar se vuelve trivial de leer.

### Ley 5 — Estado visible que sesga intenciones: la lectura necesita materia.
Adivinar sin información es moneda; adivinar CON información es yomi. Todo lo
público empuja probabilidades: vida, barra de super, tamaño de mano, descarte
consultable, knockdown, posición. El jugador fuerte lee "está sin bloqueos en
mano → agarre no, ataque sí"; el diseñador tiene que asegurarse de que ese
razonamiento sea posible. Yomi imprime la distribución en el frame de poker
(sabés que hay exactamente 4 ases); Yomi 2 hace el descarte público y
consultable. **Cuanta más información pública, MÁS profundo el mindgame — no
menos** (la información oculta de la mano es suficiente misterio).

### Ley 6 — Ganar tiene que comprar el próximo intercambio (pero no el juego).
Sin arrastre de estado, cada reveal es un RPS aislado y el juego no se siente
pelea. El arrastre canónico: knockdown (te deshabilita opciones y acelera al
rival un turno = okizeme), the edge (+velocidad un turno = frame advantage
hecho token), esquina, counter-hit. Y el freno anti-bola-de-nieve de Yomi 2:
**solo el ÚLTIMO move del combo otorga estado** — cada combo termina eligiendo
entre más daño o mejor situación. Ese tradeoff (daño vs oki) es LA decisión
fighting por excelencia; si tu diseño no la tiene, le falta el alma.

### Ley 7 — La fuerza tiene que ser desigual y medible, o el riesgo no existe.
Para que haya bluff, miedo, respeto y decisiones de riesgo, tiene que haber
posiciones objetivamente fuertes y débiles QUE AMBOS puedan estimar. En Yomi
la "fuerza" es el tamaño y composición de la mano (más cartas = más opciones
+ más impredecible). Hallazgo de sims propias: si la resolución aplasta la
varianza de fuerza (todo ~50%), foldear/apostar/respetar nunca es correcto y
las mecánicas de presión mueren. Dispersión de fuerza = combustible de la
tensión.

### Ley 8 — Doble-ciego sí, pero con sustancia por elección.
Simultáneo vs alternado, medido:
- Respuesta viendo la carta del rival = decisión localmente obvia (+13pp de
  winrate en nuestras sims): el que responde optimiza, no lee.
- Simultáneo con opciones pobres = lotería (la mejor heurística le ganaba a
  random por +2.5pp): eliges a ciegas y da casi igual qué.
Todos los referentes (Yomi, Exceed, BattleCON) son simultáneos, y compensan
la lotería con las Leyes 2, 5, 6 y 7: conversiones distintas, información
pública, estado que arrastra y manos grandes (7-12 cartas). **El doble-ciego
solo funciona si cada opción tiene identidad y contexto.** Nota fina de
Yomi 2: el reveal se ESCENIFICA secuencial (uno boca abajo, el otro boca
arriba, después se da vuelta) — teatro sin información: la decisión sigue
siendo ciega, pero el momento tiene drama.

### Ley 9 — La mano es vida, opciones y munición A LA VEZ.
El recurso central de un fighting de cartas: cada carta gastada en pegar es
una opción defensiva menos. Comprometer recursos al ataque te desnuda — eso
ES el riesgo/recompensa del género trasladado a cartas. Derivadas:
- Defender debe alimentar la economía (bloquear roba carta) para que el juego
  respire en ciclos de acumular → gastar.
- El descarte con recuperación (Exchange de Yomi 2) crea decisiones de
  "sculpting": qué recuperás dice qué planeás.
- Mano grande también es INFORMACIÓN negativa para el rival (menos predecible).

### Ley 10 — Payoffs inciertos > payoffs calculables.
Si el valor de cada jugada se puede computar, el juego se resuelve. El valor
tiene que depender de contexto móvil: cuánta mano tiene cada uno, cuánta vida,
qué se descartó, qué estado hay. Sirlin evita el "solved game" haciendo que
la valuación sea intuición entrenable, no aritmética. Números chicos y pocas
categorías ayudan: 6 HP se siente y se razona; 347 HP es una planilla.

### Ley 11 — Personajes = re-pesar la matriz, no agregar reglas.
El grappler no tiene mecánicas nuevas: su agarre PAGA MÁS, y eso re-pesa todos
los mixups del juego. Yomi 2 con gems: customización eligiendo énfasis, no
sumando subsistemas. Advertencia empírica (review de Yomi 2): Lum, el
personaje con mini-juego propio de bookkeeping, es señalado como el que rompe
el flow. Y de nuestras sims: **agregar aristas al triángulo desbalancea mucho
más que duplicar un vértice o ajustar un peso** — probamos un ciclo de 4 y el
spread de balance explotó de ±6pp a ±16pp; el arreglo ganador fue un bonus
puntual dentro de una clase. Tocá pesos, no topología.

### Ley 12 — Combos: la lectura correcta merece un premio escalable con decisión adentro.
Ganar el reveal abre la puerta; el combo decide cuánto pasás por ella. Chains
con límite (combo points / secuencia A→B→C) convierten "acerté" en un segundo
minijuego: ¿gasto la mano en daño ahora o guardo opciones? ¿daño máximo o
ender que derriba (Ley 6)? El premio por leer bien debe ser grande (payoff
asimétrico), pero SIEMPRE con costo de oportunidad visible.

### Ley 13 — Válvulas de escape y anti-vortex.
Todo sistema de presión necesita su excepción cara: el Burst de Yomi 2 (una
sola carta rapidísima, empieza en mano, te saca del rincón), el Joker de
Yomi 1 (combo breaker + fetch de ases). Sin válvula, el que cae en el loop de
presión deja de jugar (se levanta y se va). La válvula debe ser: única o
carísima, visible como amenaza (el rival juega alrededor de ella), y peor que
jugar bien (escape, no ventaja).

### Ley 14 — Onboarding: la complejidad va en la matriz, no en el reglamento.
Yomi 1 fracasó en enseñarse (keywords, reglas quirky); Yomi 2 recortó a 30
cartas por mazo y limpió texto — misma profundidad, mitad de manual. Fantasy
Strike llevó el principio al extremo (specials de un botón). La profundidad
correcta emerge de pocas reglas interactuando (Leyes 1-13), nunca de muchas
reglas. Test: si una carta necesita más de dos líneas de texto, está mal.

---

## 2. Anti-patrones (los pecados)

1. **RPS plano**: tres opciones que pagan igual. Es una moneda con teatro.
2. **El counter no-brainer**: dar información y una herramienta que la explota
   gratis. Si ves la jugada rival y tenés SIEMPRE la respuesta, no hay juego.
3. **Defensa gratis e infinita**: bloqueo siempre disponible sin costo ni
   adivinanza interna → tortuga, y el atacante juega a la lotería.
4. **Victoria sin arrastre**: ganás el intercambio y... nada cambia para el
   siguiente. Se siente trick-taking, no pelea.
5. **Fuerza plana**: toda posición vale ~50% → no hay miedo, ni respeto, ni
   bluff. (El pecado que matamos a sims en el experimento truco.)
6. **Amenazas sin dientes**: cualquier señal/canto/taunt que no cuesta nada si
   te lo aceptan es cheap talk y los jugadores lo ignoran en una semana.
7. **Bookkeeping en el loop**: contadores, tablitas y minijuegos por turno
   rompen el ritmo del mindgame (el pecado de Lum).
8. **Topología barroca**: ciclos de 4+, dobles counters condicionales,
   excepciones a la excepción. Cada arista nueva desbalancea el todo.
9. **Snowball sin freno**: momentum que compone sin la válvula de la Ley 13
   ni el tradeoff de la Ley 6.
10. **Profundidad por manual**: agregar reglas para agregar decisiones. La
    decisión nueva tiene que salir de las reglas viejas.

---

## 3. Checklist de auditoría (para cualquier prototipo del género)

- [ ] ¿Cada opción del reveal paga en una moneda distinta? (Ley 2)
- [ ] ¿Existe el default barato Y su depredador dedicado? (Ley 3)
- [ ] ¿Defenderse implica al menos una adivinanza propia? (Ley 4)
- [ ] ¿Qué información pública tiene el jugador para LEER (no adivinar)? (Ley 5)
- [ ] ¿Ganar un intercambio modifica el siguiente? ¿Con qué freno? (Ley 6)
- [ ] ¿Hay posiciones fuertes/débiles medibles por ambos? (Ley 7)
- [ ] Si es simultáneo: ¿la elección a ciegas tiene sustancia? Si es
      alternado: ¿la respuesta es realmente una decisión? (Ley 8)
- [ ] ¿Atacar consume recursos defensivos? ¿Defender alimenta la economía? (Ley 9)
- [ ] ¿Puede un jugador calcular la jugada óptima con una planilla? Si sí,
      falta incertidumbre de valuación. (Ley 10)
- [ ] ¿El premio por leer bien escala con una decisión adentro? (Ley 12)
- [ ] ¿Cómo se escapa el que está abajo? ¿Cuánto cuesta? (Ley 13)
- [ ] ¿Un jugador nuevo entiende el triángulo en una partida? (Ley 14)

---

## 4. Aplicación a Lag Fighters (actualizado 2026-07-20, tarde)

Lo que ya cumple (ambos modos):
- **Ley 5**: AP/stock públicos de los dos lados, distancia, guardia, y el
  **log de aperturas del rival** al planificar (leer hábitos, no adivinar).
- **Ley 6**: hitstun/KD arrastrado entre turnos + esquina = okizeme natural.
- **Ley 3**: bloqueo default con el agarre como depredador.
- **Ley 11**: la idea anotada de "personajes = tocar UNA arista" es
  exactamente la doctrina correcta.

Aplicado en el modo clásico tras la auditoría de esta biblia (2026-07-20):
- **Ley 7 + 9**: economía de AP persistente (`ApEconomy`): ingreso +4 <
  capacidad 5, ahorro con tope 7 → posiciones ricas/pobres medibles.
- **Ley 2 + 9**: bloqueo bancado — la carta Bloquear que bloquea paga +1 AP
  (defender con intención alimenta la economía). El parry salió: la defensa
  es UNA y económica.
- **Ley 13**: REVERSAL (1 por round, 2 AP, derribado): escape, no ventaja.
- **Ley 14**: grilla 3×3 con Dash/Salto agrupados por dirección.

Dónde seguir mirando:
- **Ley 4**: el bloqueo sigue siendo botón único siempre disponible. La
  versión alto/bajo escasa de Yomi 2 es la evolución natural si el juego
  pide más profundidad defensiva (el agachado retirado coqueteaba con esto).
- **Ley 7 (yomi)**: ~~AP promedio 5.5/6~~ **viejo**: la economía del modo
  YOMI ya se re-tuneó — el lab de hoy da 2.5/6, la escasez muerde. Cumplida.
- **Ley 12**: no hay decisión de "daño vs estado" al ganar — el premio es
  fijo (elegir Barrida vs Jab en el plan cubre parte; vigilar).
- **Anti-patrón 3 (tortuga)**: tras sacar el parry y premiar el bloqueo, el
  lab IA-IA subió a ~48% de TIME OVER. Si el juego humano se siente pasivo:
  encarecer Bloquear (2→3 AP), bajar el tope de ahorro, o darle al chip de
  proyectiles el rol anti-tortuga que tenía el parry.

---

## 5. Fuentes

- Sirlin, "Designing Yomi" — sirlin.net/articles/designing-yomi
- Sirlin, "Introducing Yomi 2" — sirlin.net/posts/introducing-yomi-2
- Yomi 2 Versus Rulebook v7.7 (Sirlin Games) — sirlingames.com/rulebooks
- Sirlin, "Playing to Win" (yomi layers, scrub theory)
- Exceed Fighting System rulebook (Level 99) · BattleCON (Level 99)
- Review Yomi 2 "Road to Morningstar" — Meeple Mountain (crítica a Lum)
- Experimentos propios: `Tools/`-style Monte Carlo sobre variantes de
  resolución por cartas, 2026-07-20/21 (sesión "truco fighter", archivada en
  `D:\Lag Fighters\truco-fighter-proto\` — fuera de este repo).
