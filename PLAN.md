# Lag Fighters — Plan de mejoras (2026-07-17)

Estado al final del día: **todo el plan ejecutado salvo el segundo personaje**
(excluido a pedido). Detalle de cada punto abajo; el diseño fino quedó en
`DESIGN.md` y `GDD.md`, los change notes en `CHANGELOG.md`.

## 1. Guard gauge — ✅ HECHO

Barra 100, A −15 · B −30 · aéreas −15 · hadouken −20 · shoryu −35 ·
tatsu −15/hit · baja −15. Regen 6/seg fuera de guardia/blockstun. En 0 →
GUARD CRUSH (50f, renace al 50%). Lab: contra bloqueo permanente, 7 jabs =
crush (~1 HP por ciclo); la IA casi no cruje porque no tortuguea.

## 2. Profundidad de pelea — ✅ HECHO

- Esquina real: pushback transferido al atacante contra la pared.
- Wakeup options: RÁPIDO (−16f) / QUEDARSE (+16f), secreto, en el turn log.
- Counter-hit visible: flash naranja + "¡COUNTER!".

## 3. UX de lectura del turno — ✅ HECHO

- Velocidad ×0.5 / ×1 / ×2 (solo presentación).
- Resumen post-turno desde la silla del picker.
- Log de turnos lateral colapsable (L).

## 4. Compartir — ✅ HECHO (WebGL: build lista para correr)

- Picks secretos en 1v1: pantalla "PASÁ EL TECLADO".
- **Online asincrónico POR CÓDIGO**: `TurnCode` (LF+base64), clipboard,
  validación de lado/turno. Sin servidores; la sim determinista sincroniza.
- WebGL: target agregado al BuildScript (`Lag Fighters → Build WebGL
  (itch.io)`, gzip + decompression fallback). Nota: en WebGL el modo POR
  CÓDIGO puede requerir pegar a mano (los browsers capan el clipboard).

## 5. Contenido — implementado, luego DESACTIVADO a pedido

- **Pérdida de miembros** y **Agacharse + Patada baja**: implementados
  completos con tests, y **apagados el mismo día a pedido de Patricio**.
  El código quedó entero detrás de `SimConfig.LimbsEnabled` /
  `SimConfig.CrouchEnabled` (false); para volver a probarlos, flipear los
  flags y descomentar las cartas en `PlanMenuUI.Order` + opciones de
  `SimpleAI`. Los tests se reactivan solos.
- ~~Segundo personaje~~ — EXCLUIDO por ahora (requiere MoveCatalog por
  peleador; refactor chico, anotado para cuando haya feedback de testers).

## 6. Presentación — ✅ HECHO

KO slow-mo · hit-sparks de cubitos · trails en frames activos · intro de
round · stage con skyline y público · announcer solo KO/crush con toggle.

## 7. Técnica / deuda — ✅ HECHO

- `Tools/SimTests`: 16 tests de framedata (pescaron el off-by-one de invuln
  del shoryu). `Tools/SimHarness`: lab con stats de crush. `Tools/verify.ps1`.
- splash.png 8.9 → 2.2 MB.

## Próximo (cuando haya feedback de testers)

1. Subir la build WebGL a itch.io (butler o web) y compartir link.
2. Segundo personaje (grappler o zoner) — MoveCatalog por peleador.
3. Tunear guard gauge / miembros / agachado con datos de gente real.
