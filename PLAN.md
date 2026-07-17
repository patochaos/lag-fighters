# Lag Fighters — Plan de mejoras (2026-07-17, a revisar por Patricio)

Estado: la build standalone funciona y el loop core está sano. Este plan es
propuesta — nada está ejecutado. Ordenado por prioridad sugerida.

## 1. Guard gauge (la pregunta de Patricio) — PRÓXIMA SESIÓN

Decisión de diseño: **NO** media vida por bloquear (mataría el bloqueo: nadie
bloquearía y el neutral se vuelve adivinanza pura). **SÍ** block meter:

- Barra de guardia 100 por jugador. Cada bloqueo la baja según el golpe:
  A −15 · B −30 · Hadouken −20 · Shoryuken −35 · patada aérea −15.
- Regenera ~6/seg SOLO cuando no estás bloqueando ni en blockstun.
- En 0 → **GUARD CRUSH**: stun largo (~50f), barra renace al 50%.
- UI: barrita amarilla bajo los pips + "¡GUARDIA ROTA!" + framedata visible.
- El bloqueo queda con dos counters: Agarre (puntual) y crush (estructural).
- Tunear valores con el lab AI vs AI (agregar guard-crush a las stats).

## 2. Profundidad de pelea (corto plazo, baratas)

- **Esquina real**: cuando el defensor está contra la pared, el pushback del
  golpe bloqueado/conectado se transfiere al atacante (como SF). Hoy el
  pushback muere contra la pared → la esquina es más letal de lo que debería.
- **Wakeup options**: al planificar derribado, elegir "levantarse rápido"
  (menos frames de KD, posición fija) vs "quedarse" (más frames, el rival
  puede whiffear el meaty). Un toggle en el menú de planificación.
- **Counter-hit más visible**: hit-spark distinto y "COUNTER" grande.

## 3. UX de lectura del turno (corto plazo)

- **Velocidad de ejecución**: botón x0.5 / x1 / x2 para ver el turno (solo
  presentación; la sim no cambia). Clave en Lag Mode con turnos de 960f.
- **Resumen post-turno**: al volver a planificar, línea con lo que pasó:
  "recibiste 2 · pegaste 1 · perdiste 3 órdenes · arrancás −28f".
- **Log de turnos** lateral colapsable (qué jugó cada uno, resultado).

## 4. Compartir (mediano)

- **Build WebGL** → itch.io: el multiplicador real para testeo (nadie instala
  un exe de un amigo, todos abren un link). La sim pura debería portar limpio;
  revisar Resources y tamaño de splash.png (9MB → comprimir).
- **Picks secretos en 1v1 local**: pantalla "pasá el teclado" entre pickers,
  ghost oculto para el que no elige.
- **Online asincrónico por código** (idea propia, único del diseño): la sim
  determinista permite serializar un turno como código corto (ej. base64 de
  la cola). Pelea por WhatsApp/Discord: me mandás tu código, te mando el mío,
  ambos vemos el mismo turno resolverse. Cero netcode real.

## 5. Contenido (mediano/largo)

- **Segundo personaje**: movelist alternativa (grappler lento con agarre
  fuerte, o zoner con mejor fireball y peor cuerpo a cuerpo). Requiere
  MoveCatalog por peleador (hoy es global) — refactor chico.
- **Pérdida de miembros** (la idea fundacional): hurtboxes por parte del
  blockman (brazos/piernas/cabeza); daño localizado; perder el brazo
  deshabilita A/Hadouken, la pierna te saca patadas y velocidad. El rig de
  bloques separados ya está pensado para esto.
- **Niveles ALTO/MEDIO/BAJO + agacharse** (volver del pivot 3): el siguiente
  eje de mixup cuando el actual se agote.

## 6. Presentación (cuando el gameplay asiente)

- KO en cámara lenta (playback cosmético, la sim no se toca).
- Hit-sparks de partículas y trails en manos/pies durante frames activos.
- Intro de round ("ROUND 1 — FIGHT!") con la estética del cartel/wifi.
- Stage con algo de vida (fondo, público de bloques, piso con líneas).
- Audio: volver al announcer solo en momentos (KO, guard crush), con toggle.

## 7. Técnica / deuda (continuo)

- Unit tests de framedata sobre la sim pura ("A on hit = +2", "tatsu
  atraviesa hadouken") — corren con dotnet, sin Unity.
- Script único de verificación: compile-check + tests + lab de balance.
- Comprimir splash.png (9MB) y revisar tamaño de build.

## Corte sugerido para la próxima sesión

1. Guard gauge completo con lab tuning (punto 1).
2. Esquina real + counter visible (punto 2, parcial).
3. Velocidad de ejecución + resumen post-turno (punto 3, parcial).
4. Si sobra: WebGL a itch.io (punto 4).
