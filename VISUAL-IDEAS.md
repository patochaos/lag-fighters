# Legibilidad visual — análisis y plan (2026-07-20)

Feedback de usuarios: **a veces no se entiende qué está pasando**. Modelos de
bloques + animaciones simples = los moves se confunden. Este doc sale de una
sesión de análisis con el juego corriendo en el editor (VS IA, varios turnos
capturados) y es la lista de mejoras para atacarlo, priorizada.

## Diagnóstico (qué se ve hoy)

- **Los moves de brazo se parecen entre sí**: jab, agarre y hadouken terminan
  todos en "brazo(s) al frente". La anticipación existe (carga atrás en
  startup) pero es chica y a velocidad real no se registra.
- **Nada anuncia QUÉ tiró cada uno**: había que leer las fichas de la
  timeline, y las abreviaturas ("BL", "DP", "»") son crípticas para un
  usuario nuevo.
- **La cabeza era un cubo liso**: el facing solo se leía por los brazos.
- **Los popups se amontonan**: daño, "¡COUNTER!", frame advantage y el cartel
  de ROUND comparten la misma zona (y ≈ 2.15 del mundo, centro-arriba) y se
  pisan entre sí en cuanto pasan dos cosas juntas (verificado en captura:
  "+14F ¡COUNTER! HIT −2 22F" ilegible).
- **Jerarquía plana de eventos**: un jab y un shoryuken pesan visualmente
  casi igual (mismos sparks, mismo shake corto). Lo importante no grita.
- Lo que SÍ funciona y no hay que tocar: el contraste peleadores/fondo
  (celeste/naranja sobre gris), la pose de knockdown (se lee clarísimo), la
  timeline con recap del turno rival, la acción al 50%.

## Hecho en esta sesión (prototipos vivos en `FighterView.cs`)

1. **Ojos laterales** — un cubito oscuro por lateral de la cabeza, pegado al
   borde delantero (la cámara es lateral: en la cara frontal no se veían).
   Facing legible de un vistazo. Probado en editor: funciona, es sutil;
   si convence, agrandar un 30-50%.
2. **Cartel con el nombre del move** — al arrancar cada ataque, popup con el
   nombre ("JAB", "SHORYUKEN") en el color del lado, sobre el atacante
   (`WorldFX.Popup` reusado). Probado: es EL cambio que más rápido explica
   la acción. Pendiente: dárles su propio carril de altura para que no
   choquen con los popups de daño (ver punto 3).

## Quick wins recomendados (cero assets, mismo lenguaje de bloques)

3. **Carriles de popups**: separar por tipo — daño sobre el golpeado, nombre
   del move más abajo y a un costado, frame advantage chico y abajo,
   "¡COUNTER!" grande al centro. Hoy todo nace en el mismo y; con un offset
   por tipo + apilado incremental cuando coinciden se destraba solo.
4. **Iconos en las fichas de la timeline** en vez de abreviaturas: pictograma
   procedural de 16px por move (puño, pierna, bola, flecha). Las fichas ya
   tienen color por categoría; el icono remata la lectura sin texto.
5. **Anticipación más gorda**: subir el wind-up del jab/barrida/DP (el factor
   `atk` negativo de startup, hoy −0.45) y saturar más el tinte amarillo de
   startup. La regla de oro de legibilidad en fighting games es que el
   startup se telegrafíe; acá además ES información de juego (counter hits).
6. **Afterimages en dash**: 2-3 copias del rig que se desvanecen en ~0.2s.
   El dash de 12f a velocidad real es un teleport visual; con estela se lee
   como velocidad. (Pool de rigs fantasma, mismo truco que el ghost.)
7. **Polvo de piso**: cubitos grises al aterrizar de un salto, al arrancar un
   dash y en el wakeup. SparkFX ya tiene el pool; es un `Burst` gris con
   menos velocidad vertical.
8. **Impact frame en counter/KO**: 2-3 frames de flash blanco de pantalla
   completa (overlay UI, WebGL-safe) + el hitstop que ya existe. El counter
   hoy tinta al muñeco; con el flash de pantalla se vuelve un EVENTO.
9. **Sparks proporcionales al daño**: jab = 6 cubitos, barrida = 12, DP/super
   = 20 + burst dorado. Hoy casi todo tira lo mismo.
10. **Micro-zoom de cámara**: punch-in suave (~5%) en hits fuertes y KO,
    y un pelín de zoom out cuando están a más de ~3 de distancia. Ojo: la
    cámara fija ayuda a leer distancias — que sea sutil y siempre vuelva.
11. **Outline en los peleadores** (inverted hull, un material unlit negro con
    normales invertidas): los despega del fondo en cualquier stage y banca
    WebGL porque es geometría, no post.

## UI / frontend

12. **Mini-reveal al arrancar la ejecución** (clásico): medio segundo
    mostrando la primera carta de cada lado, estilo la revelación del modo
    YOMI (la infra de cartas gigantes ya existe en `BeginYomiTheater`/HUD).
    Responde "¿con qué abrió?" antes de que pase todo junto.
13. **Recap post-turno con iconos**: el texto "pegaste 2 · recibiste 0 ·
    perdiste 1 órdenes" está bien pero es homogéneo; con los mismos iconos
    del punto 4 se escanea en un golpe de vista.
14. **Label/icono en la barra de guardia**: la barrita amarilla no se
    auto-explica (varios usuarios no saben que existe el guard crush hasta
    que les pasa). Un escudito de 12px al lado alcanza.
15. **Tooltip en las fichas de la timeline** (hover = framedata del move),
    si no existe ya — cierra el loop con el panel de info de las cartas.

## Assets gratis (si se quiere subir un escalón estético)

- **[Kenney — Blocky Characters](https://kenney.nl/assets/blocky-characters)**
  (CC0): el match natural — mismo lenguaje de bloques pero con caras, skins
  y proporciones más simpáticas. Se puede robar SOLO la textura de cara/skin
  y aplicarla al rig procedural actual sin tocar la animación.
- **[Quaternius](https://quaternius.com/)** (CC0): low-poly riggeados +
  [Universal Animation Library](https://quaternius.itch.io/universal-animation-library)
  con combate. Compatible rig Mixamo.
- **Mixamo** (Adobe, gratis, uso comercial OK): humanoides + decenas de
  anims de pelea (jab, hook, sweep, uppercut). Útil como REFERENCIA de poses
  clave para mejorar las procedurales, aunque no se importe nada.
- **[Fighter Pack Bundle FREE](https://assetstore.unity.com/packages/3d/animations/fighter-pack-bundle-free-36286)**
  (Asset Store): anims de boxeo gratis.
- **[OpenGameArt — Blocky Characters](https://opengameart.org/content/blocky-characters)**
  (espejo CC0 del pack de Kenney).

### Por qué NO migrar a modelos riggeados ahora

La animación es 100% procedural **desde el estado de la sim**: cada pose
refleja frames reales (startup/active/recovery, la ventana de hit viva). Un
clip enlatado de Mixamo dura lo que dura; para que no MIENTA framedata habría
que retimearlo por fases (Playables/sampleo manual) para CADA move — proyecto
mediano, y las hurtboxes rectangulares quedarían más desalineadas del cuerpo
que hoy. El feedback no pide "más lindo", pide "entender": la plata está en
anticipación, carteles, popups e iconos. Los bloques son identidad, no deuda.

## Prioridad sugerida

1. **3 + 4** (carriles de popups, iconos en fichas) — puro layout, máximo
   efecto en "entender qué pasó".
2. **1 + 2 pulidos** (ojos más grandes si convencen, carril propio para el
   cartel del move).
3. **5 + 6 + 9** (anticipación, afterimages, sparks proporcionales) — que el
   cuerpo cuente la historia.
4. **8 + 12** (impact frames, mini-reveal) — drama y contexto.
5. Resto según feedback.
