using System;
using System.Collections.Generic;

namespace LagFighter
{
    // Simulación pura y determinista. Ryu vs Ken por turnos programados:
    //  - 60 ticks/s = frames. Cada turno ambos arman una cola de hasta 240
    //    frames y se ejecutan simultáneas.
    //  - Guardia automática: bloqueás si estás en neutral o caminando hacia
    //    atrás (y en el piso). Parry es una lectura activa, no un bloqueo.
    //  - Estados con framedata: HITSTUN / BLOCKSTUN / KNOCKDOWN. Comen parte
    //    del turno; apenas terminan, la cola sigue ejecutando lo que quede.
    //  - Proyectiles (Hadouken), saltos que los pasan por arriba, y Shoryuken
    //    invulnerable anti-aéreo con recuperación gigante.

    public static class SimConfig
    {
        public const int TicksPerSecond = 60;
        public const float TickDuration = 1f / TicksPerSecond;
        public const int TurnFrames = 60; // 1 segundo por turno: denso, decisión a decisión

        // Turno fluido (toggle del menú principal, OFF por defecto): el último
        // move puede CRUZAR el límite del turno en vez de tener que entrar
        // completo. Arrancás el turno siguiente comprometido (p.ej. en el aire,
        // okizeme estilo Akuma) y el rival TE VE al planificar: info honesta.
        public static bool CarryoverEnabled = false;

        // ---- ACTION POINTS (2026-07-20): el turno clásico se divide en
        // slots de AP (1 AP = 12f → 5 AP por turno de 60f). Cada move cuesta
        // ceil(frames/12) AP y OCUPA su slot entero: el resto del slot se
        // espera en neutral (bloqueando). Así el presupuesto en AP nunca
        // miente sobre los frames — la lección de la v1 del yomi. El combate
        // (hits, stun, ventaja) sigue siendo frame-exacto. El último move
        // puede pasarse del presupuesto: cruza el turno (semántica fluida,
        // siempre ON en modo AP) y esos AP se PIDEN PRESTADOS al turno
        // siguiente, que arranca con menos.
        public static bool ApEnabled = true;
        public const int FramesPerAp = 12;
        public static bool ApActive => ApEnabled && !TableMode; // en YOMI/CARTAS los recursos son otros

        // OVERFLOW/PRÉSTAMO — DESACTIVADO a pedido (2026-07-20, mismo día que
        // nació): pasarse del presupuesto complejizaba entender si lo BÁSICO
        // es disfrutable. El código queda entero detrás de este flag y VA A
        // VOLVER; con true, el último move puede pasarse de AP y cruzar el
        // turno pidiendo prestado al siguiente.
        public static bool ApOverflowEnabled = false;

        // "el move en curso cruza el límite del turno": overflow del modo AP
        // (si está habilitado) o el toggle clásico de turno fluido.
        public static bool FluidTurn => CarryoverEnabled || (ApActive && ApOverflowEnabled);

        // Reversal (2026-07-20, la válvula anti-vortex de la biblia, Ley 13):
        // derribado al planificar, 1 vez por round y pagando AP, te levantás
        // YA y el empujón separa a esta distancia. Escape, no ventaja.
        public const float ReversalGap = 2.4f;

        // ---- Modo YOMI (v2 discreto, 2026-07-20): la lógica vive en
        // YomiSim.cs (dos distancias, una acción por turno, matriz de
        // counters). Este flag solo marca el MODO: la sim de frames pasa a
        // ser TEATRO de presentación (MatchController coreografía el
        // resultado de la tabla con los moves de acá). La v1 (AP sobre la
        // sim de frames) se retiró el mismo día: era una quimera — la tabla
        // decía una cosa y los frames decidían otra por abajo.
        public static bool YomiEnabled = false;

        // ---- Modo CARTAS (2026-07-21): copia de Yomi 2 — la lógica vive en
        // CardSim.cs (mazos, manos, combate por tabla con speeds y alturas).
        // Igual que en YOMI, la sim de frames es solo TEATRO del resultado.
        public static bool CardsEnabled = false;

        // ---- Modo DUELO (2026-07-25): EL núcleo casual — la lógica vive en
        // DuelSim.cs (7 reglas, mazo de 20, alturas y velocidad). La sim de
        // frames es TEATRO puro: actúa la carta que la tabla ya resolvió.
        public static bool DuelEnabled = false;
        // El TEATRO de DUELO (los peleadores actuando la carta) quedo APAGADO
        // el 2026-07-25 a pedido de Patricio: con las animaciones heredadas
        // del modo clasico se leia como ruido (aparecen y hacen cualquier
        // cosa). El codigo sigue entero detras del flag; la ceremonia del
        // turno ahora es la REVELACION de las cartas (DuelHudUI).
        public static bool DuelTheaterEnabled = false;
        // Los modos "de mesa" comparten esto: la sim de frames no manda.
        public static bool TableMode => YomiEnabled || CardsEnabled || DuelEnabled;
        // 20 turnos por round → TIME OVER y decide la vida. Calibrado con la
        // distribución natural del lab: mediana 13, p75 21; con 20 el 75% de
        // las peleas termina por KO y el juez solo corta la cola de stalls.
        public const int TurnsPerRound = 20;
        public const int MaxHp = 6;
        public const float StageHalfWidth = 4.2f;
        public const float MinSeparation = 0.8f;
        public const float StartX = 2.0f;
        public const float HurtHalfWidth = 0.35f;
        public const float HurtHeight = 1.75f;
        public const float AirHurtY0 = 1.35f;  // saltando, la hurtbox sube:
        public const float AirHurtY1 = 2.6f;   // los hadoukens pasan por abajo

        // proyectil (Hadouken)
        public const float ProjSpeed = 0.05f;      // por frame (3 u/s)
        public const float ProjHalfWidth = 0.22f;
        public const float ProjY0 = 0.95f, ProjY1 = 1.28f;
        public const float ProjDamage = 1f;
        public const int ProjHitstun = 22, ProjBlockstun = 16;
        public const float ProjPush = 0.3f;
        public const float ProjGuardDamage = 25f; // sube de 20: que zonear lastime la guardia
        public const float ProjRange = 3.0f;      // el hadouken se disipa tras viajar esto: zonear de fullscreen whiffea

        // guard gauge: bloquear no cuesta vida pero sí guardia; en 0 → GUARD CRUSH.
        // Barra de 70 (era 100: crushear casi no pasaba): 5 jabs o 3 sweeps
        // bloqueados seguidos y la guardia vuela.
        public const float GuardMax = 55f;
        public const float GuardRegen = 0.14f;      // ~8/seg, SOLO mientras ejecutás moves que no bloquean (guardia = stamina)
        public const int GuardCrushStun = 50;
        public const float GuardCrushRespawn = 27f; // la barra renace al ~50%

        // ---- Super (Shinku Hadouken): la barra se carga con frames de
        // overflow (turno fluido) — el riesgo de comprometerse es el combustible.
        public const int SuperMax = 120;            // ~3 overflows grandes
        public const float SuperDamage = 4f;        // el golpe más fuerte del juego
        public const int SuperHitstun = 60;         // hard KD
        public const int SuperBlockstun = 30;
        public const float SuperGuardDamage = 40f;  // bloquearla te deja al borde del crush
        public const float SuperPush = 0.9f;
        public const float SuperSpeed = 0.1f;       // el doble del hadouken común
        public const float ParryGuardRefund = 15f;  // parry exitoso RECARGA guardia: anti-chip

        // ---- features DESACTIVADAS a pedido de Patricio (2026-07-17) ----
        // El código de pérdida de miembros y agachado sigue completo abajo;
        // para reactivar: poner estos flags en true y descomentar las cartas
        // en PlanMenuUI.Order y las opciones de SimpleAI. Los tests de
        // Tools/SimTests se reactivan solos al flipear los flags.
        public static readonly bool LimbsEnabled = false;
        public static readonly bool CrouchEnabled = false;

        // pérdida de miembros: daño localizado por altura del golpe.
        // Bajo LimbSplitY pega a la pierna, arriba al brazo. Cada parte tiene
        // su HP; en 0 el miembro VUELA (y con él, sus movimientos).
        public const float LimbHp = 3f;
        public const float LimbSplitY = 1.0f;
        public const float LeglessSpeedFactor = 0.65f;

        // agachado: hurtbox de 0.9 — el jab (Y0 1.0) y el hadouken (Y0 0.95)
        // pasan por arriba; el sweep, la patada baja y el agarre no.
        public const float CrouchHeight = 0.9f;
    }

    public enum AnimKind { Walk, Dash, Jump, AttackA, AttackB, Fireball, Dragon, Tatsu, Grab, Parry, Crouch, LowKick }
    public enum StunKind { None, Hitstun, Blockstun, Knockdown }

    public struct HitWindow
    {
        public int Start, Duration;
        public float Fwd0, Fwd1, Y0, Y1;
        public float Damage;
        public int Hitstun, Blockstun, CounterStun;
        public float Push;
        public float GuardDamage; // cuánto come de la barra de guardia si lo bloquean
        public bool Knockdown;
        public bool IsGrab; // ignora la guardia; pierde contra aéreos/caídos; grab vs grab = tech
    }

    public struct WorldRect
    {
        public float X0, X1, Y0, Y1;
        public bool Grab; // para que la viz pinte los agarres distinto
        public bool Overlaps(WorldRect o) => X0 <= o.X1 && o.X0 <= X1 && Y0 <= o.Y1 && o.Y0 <= Y1;
    }

    public class MoveDef
    {
        public string Id, Name, Desc;
        public AnimKind Anim;
        public int Startup, Active, Recovery;
        public HitWindow[] Hits = Array.Empty<HitWindow>();
        public float MoveDx;
        public int MotionStart, MotionEnd;
        public int AirStart = -1, AirEnd = -1;       // ventana en el aire (hurtbox alta, no bloquea)
        public int CrouchStart = -1, CrouchEnd = -1; // ventana agachado (hurtbox baja: los altos pasan de largo)
        public int InvulnStart = -1, InvulnEnd = -1; // ventana invulnerable (Shoryuken)
        public int ProjImmuneStart = -1, ProjImmuneEnd = -1; // inmune SOLO a proyectiles (Tatsumaki)
        public int SpawnFrame = -1;                  // frame en que larga el proyectil
        public int Total => Startup + Active + Recovery;
        // Modo AP: costo en action points (redondeo hacia arriba: un move que
        // pisa un slot lo paga entero) y duración padded — el move ocupa sus
        // slots completos, el sobrante se espera en neutral.
        // ApCostExtra: sobreprecio de diseño (Dash − paga 2: la retirada
        // tributa). Solo puede ENCARECER — abaratar rompería la garantía de
        // que un plan validado por AP entra en los frames del turno.
        public int ApCostExtra;
        public int ApCost => (Total + SimConfig.FramesPerAp - 1) / SimConfig.FramesPerAp + ApCostExtra;
        public int PaddedTotal => SimConfig.ApActive ? ApCost * SimConfig.FramesPerAp : Total;
        public bool IsAttack => Hits.Length > 0 || SpawnFrame >= 0;
        public bool HasAir => AirEnd > AirStart;
        public float TotalDamage { get { float s = 0f; foreach (var h in Hits) s += h.Damage; return SpawnFrame >= 0 ? s + SimConfig.ProjDamage : s; } }
    }

    public static class MoveCatalog
    {
        public const int WalkF = 0, WalkB = 1, DashF = 2, DashB = 3,
                         JumpF = 4, JumpN = 5, JumpB = 6,
                         AttackA = 7, AttackB = 8, Hadouken = 9, Shoryuken = 10, Parry = 11,
                         Tatsu = 12, Grab = 13, Crouch = 14, LowKick = 15, Super = 16,
                         Strong = 17, YomiGrab = 18,
                         StrongFar = 19, JumpInFar = 20, DashInFar = 21; // teatro yomi (entradas largas)

        public static readonly MoveDef[] All =
        {
            // Caminar + retirado del menú (2026-07-19, redundante con Dash +).
            // Queda en el catálogo: los índices no se mueven y los replays viejos siguen andando.
            new MoveDef { Id = "walkF", Name = "Caminar +", Anim = AnimKind.Walk, Startup = 2, Active = 16, Recovery = 2,
                Desc = "Avanza un paso corto. Caminando hacia adelante NO bloqueás.",
                MoveDx = 0.55f, MotionStart = 0, MotionEnd = 20 },

            new MoveDef { Id = "walkB", Name = "Bloquear", Anim = AnimKind.Walk, Startup = 2, Active = 16, Recovery = 2,
                Desc = "La defensa base: bloquea retrocediendo despacio. Come guardia, no vida. El agarre la rompe.",
                MoveDx = -0.38f, MotionStart = 0, MotionEnd = 20 },

            // Dash a 12f = 1 AP (2026-07-20, rebalance): EL move barato del
            // modo AP — un slot justo, movimiento puro. Antes 16f/2 AP y el
            // costo mínimo del juego era 2: el turno se sentía de a ladrillos.
            new MoveDef { Id = "dashF", Name = "Dash +", Anim = AnimKind.Dash, Startup = 2, Active = 8, Recovery = 2,
                Desc = "Arremetida hacia adelante. NO bloquea: es puro compromiso.",
                MoveDx = 1.0f, MotionStart = 2, MotionEnd = 10 },

            // Dash − paga 2 AP (sobreprecio anti-turtle, 2026-07-20): huir
            // cuesta el doble que entrar — sin el tributo, dashear fuera de
            // rango era la defensa gratis que la biblia prohíbe (Ley 3).
            new MoveDef { Id = "dashB", Name = "Dash −", Anim = AnimKind.Dash, Startup = 2, Active = 8, Recovery = 2,
                Desc = "Salto atrás rápido. No bloquea, y huir tributa: cuesta 2 AP.",
                MoveDx = -1.0f, MotionStart = 2, MotionEnd = 10, ApCostExtra = 1 },

            new MoveDef { Id = "jumpF", Name = "Salto + (patada)", Anim = AnimKind.Jump, Startup = 6, Active = 28, Recovery = 10,
                Desc = "Jump-in con patada en la bajada (hit 20..28) + 10f de recovery al caer. Pasa hadoukens; en el aire no bloqueás. Guardia −15.",
                MoveDx = 1.9f, MotionStart = 6, MotionEnd = 34, AirStart = 6, AirEnd = 34,
                Hits = new[] { new HitWindow { Start = 20, Duration = 8, Fwd0 = 0.2f, Fwd1 = 0.95f, Y0 = 0.85f, Y1 = 1.65f,
                    Damage = 1f, Hitstun = 26, Blockstun = 15, CounterStun = 36, Push = 0.2f, GuardDamage = 15f } } },

            new MoveDef { Id = "jumpN", Name = "Salto N (patada)", Anim = AnimKind.Jump, Startup = 6, Active = 28, Recovery = 6,
                Desc = "Salto vertical con patada en la bajada: el wakeup que igual pega. Esquiva proyectiles. Guardia −15.",
                AirStart = 6, AirEnd = 34,
                Hits = new[] { new HitWindow { Start = 18, Duration = 12, Fwd0 = 0.05f, Fwd1 = 0.7f, Y0 = 0.8f, Y1 = 1.6f,
                    Damage = 1f, Hitstun = 24, Blockstun = 14, CounterStun = 34, Push = 0.15f, GuardDamage = 15f } } },

            // Salto − retirado del menú (2026-07-19: Salto N esquiva proyectiles y Dash − retrocede).
            new MoveDef { Id = "jumpB", Name = "Salto −", Anim = AnimKind.Jump, Startup = 6, Active = 28, Recovery = 6,
                Desc = "Salto atrás. La retirada elegante sobre el hadouken.",
                MoveDx = -1.9f, MotionStart = 6, MotionEnd = 34, AirStart = 6, AirEnd = 34 },

            new MoveDef { Id = "atkA", Name = "Jab", Anim = AnimKind.AttackA, Startup = 6, Active = 4, Recovery = 14,
                Desc = "El jab: rápido y corto (+2 on hit, −5 on block). Atrapa avances y saltos cercanos. Guardia −15.",
                Hits = new[] { new HitWindow { Start = 6, Duration = 4, Fwd0 = 0.45f, Fwd1 = 1.1f, Y0 = 1.0f, Y1 = 1.6f,
                    Damage = 1f, Hitstun = 20, Blockstun = 13, CounterStun = 32, Push = 0.35f, GuardDamage = 15f } } },

            // Barrida a 48f = 4 AP (rebalance 2026-07-20: costaba 5, lo mismo
            // que un hadouken de turno entero — startup 16→12, el −10 on
            // block se preserva: bs26 − (48−12) = −10).
            new MoveDef { Id = "atkB", Name = "Barrida", Anim = AnimKind.AttackB, Startup = 12, Active = 6, Recovery = 30,
                Desc = "El sweep: larga, 2 de daño, DERRIBA (soft). −10 si la bloquean. Guardia −30.",
                Hits = new[] { new HitWindow { Start = 12, Duration = 6, Fwd0 = 0.5f, Fwd1 = 1.6f, Y0 = 0.5f, Y1 = 1.2f,
                    Damage = 2f, Hitstun = 42, Blockstun = 26, CounterStun = 55, Push = 0.55f, Knockdown = true, GuardDamage = 30f } } },

            new MoveDef { Id = "hadouken", Name = "Hadouken", Anim = AnimKind.Fireball, Startup = 14, Active = 2, Recovery = 44,
                Desc = "Proyectil. 60f totales: tirarlo es comprometer EL TURNO ENTERO. Saltable y castigable. Guardia −25.",
                SpawnFrame = 14 },

            new MoveDef { Id = "shoryu", Name = "Shoryuken", Anim = AnimKind.Dragon, Startup = 4, Active = 5, Recovery = 32,
                Desc = "Invuln frames 1-10 (después, vulnerable subiendo). ANTI-AÉREO (no pega abajo de Y 1.0), hard KD, −15 en block. Guardia −35.",
                InvulnStart = 0, InvulnEnd = 10, AirStart = 6, AirEnd = 30, MoveDx = 0.4f, MotionStart = 2, MotionEnd = 12,
                Hits = new[] { new HitWindow { Start = 4, Duration = 5, Fwd0 = 0.15f, Fwd1 = 0.75f, Y0 = 1.0f, Y1 = 2.5f,
                    Damage = 2f, Hitstun = 60, Blockstun = 22, CounterStun = 70, Push = 0.4f, Knockdown = true, GuardDamage = 35f } } },

            new MoveDef { Id = "parry", Name = "Parry", Anim = AnimKind.Parry, Startup = 2, Active = 5, Recovery = 5,
                Desc = "Lectura de 12f: rechaza golpes y proyectiles entre f3-7 y RECARGA 15 de guardia. Pierde contra agarres y ataques demorados." },

            new MoveDef { Id = "tatsu", Name = "Tatsumaki", Anim = AnimKind.Tatsu, Startup = 12, Active = 18, Recovery = 16,
                Desc = "Giratoria que viaja lejos y ATRAVIESA hadoukens (girando, 8..34: el final es castigable). Dos hits; el segundo derriba. Guardia −15 por hit.",
                MoveDx = 1.6f, MotionStart = 10, MotionEnd = 30, ProjImmuneStart = 8, ProjImmuneEnd = 34,
                Hits = new[] {
                    new HitWindow { Start = 14, Duration = 5, Fwd0 = 0.3f, Fwd1 = 0.95f, Y0 = 0.9f, Y1 = 1.3f,
                        Damage = 1f, Hitstun = 22, Blockstun = 14, CounterStun = 32, Push = 0.3f, GuardDamage = 15f },
                    new HitWindow { Start = 24, Duration = 5, Fwd0 = 0.3f, Fwd1 = 0.95f, Y0 = 0.9f, Y1 = 1.3f,
                        Damage = 1f, Hitstun = 45, Blockstun = 16, CounterStun = 55, Push = 0.5f, Knockdown = true, GuardDamage = 15f } } },

            // Agarre a 24f = 2 AP (rebalance 2026-07-20: costaba 3 y el
            // mixup contra la tortuga salía caro — recovery 20→14).
            new MoveDef { Id = "grab", Name = "Agarre", Anim = AnimKind.Grab, Startup = 6, Active = 4, Recovery = 14,
                Desc = "Rompe la guardia y tira corto (KD). Los saltos lo ignoran; agarre vs agarre = TECH.",
                Hits = new[] { new HitWindow { Start = 6, Duration = 4, Fwd0 = 0.15f, Fwd1 = 0.9f, Y0 = 0.5f, Y1 = 1.6f,
                    Damage = 1.5f, Hitstun = 45, Blockstun = 0, CounterStun = 45, Push = 1.2f, Knockdown = true, IsGrab = true } } },

            new MoveDef { Id = "crouch", Name = "Agacharse", Anim = AnimKind.Crouch, Startup = 0, Active = 14, Recovery = 0,
                Desc = "Bloquea agachado: los golpes ALTOS y los hadoukens pasan por arriba. El sweep, la patada baja y el agarre no.",
                CrouchStart = 0, CrouchEnd = 14 },

            new MoveDef { Id = "lowKick", Name = "Patada baja", Anim = AnimKind.LowKick, Startup = 8, Active = 4, Recovery = 16,
                Desc = "Rastrera desde abajo: pega BAJO (+2 hit / −7 block), agachado mientras dura. Guardia −15.",
                CrouchStart = 0, CrouchEnd = 28,
                Hits = new[] { new HitWindow { Start = 8, Duration = 4, Fwd0 = 0.4f, Fwd1 = 1.15f, Y0 = 0.25f, Y1 = 0.8f,
                    Damage = 1f, Hitstun = 22, Blockstun = 13, CounterStun = 32, Push = 0.3f, GuardDamage = 15f } } },

            new MoveDef { Id = "shinku", Name = "Shinku Hadouken", Anim = AnimKind.Fireball, Startup = 14, Active = 2, Recovery = 40,
                Desc = "LA SUPER: proyectil gigante de 4, veloz, arrasa hadoukens y el parry no lo rechaza. Bloquearla come 40 de guardia. Se salta. Cuesta la barra entera.",
                SpawnFrame = 14 },

            // ---- Cartas exclusivas del modo YOMI (2026-07-20) ----
            // Golpe fuerte: poke largo Y antiaéreo (hitbox hasta 2.4 de alto:
            // alcanza la hurtbox aérea 1.35-2.6). Derriba; bloqueado es −12.
            // De cerca pierde con el agarre yomi (startup 14 vs 9): su casa es
            // la media distancia y el cielo.
            new MoveDef { Id = "strong", Name = "Golpe fuerte", Anim = AnimKind.AttackA, Startup = 14, Active = 4, Recovery = 26,
                Desc = "Largo y ALTO: el antiaéreo del modo. 2 de daño, DERRIBA. Bloqueado es −12 y de cerca el agarre le gana.",
                Hits = new[] { new HitWindow { Start = 14, Duration = 4, Fwd0 = 0.4f, Fwd1 = 1.5f, Y0 = 0.6f, Y1 = 2.4f,
                    Damage = 2f, Hitstun = 45, Blockstun = 18, CounterStun = 58, Push = 0.5f, Knockdown = true, GuardDamage = 30f } } },

            // Agarre yomi: igual al clásico pero startup 9 (vs 6): el jab (6)
            // SIEMPRE lo countereá — el triángulo Golpe > Agarre queda limpio,
            // sin trades turbios en el mismo frame. El clásico no se toca.
            new MoveDef { Id = "grabY", Name = "Agarre", Anim = AnimKind.Grab, Startup = 9, Active = 4, Recovery = 17,
                Desc = "Rompe la guardia y tira (KD). Pierde contra golpes (más rápidos) y whiffea contra saltos y a distancia.",
                Hits = new[] { new HitWindow { Start = 9, Duration = 4, Fwd0 = 0.15f, Fwd1 = 0.9f, Y0 = 0.5f, Y1 = 1.6f,
                    Damage = 1f, Hitstun = 45, Blockstun = 0, CounterStun = 45, Push = 1.2f, Knockdown = true, IsGrab = true } } },

            // ---- Versiones "de entrada" para el TEATRO del modo YOMI v2 ----
            // Con LEJOS a 3.4 de separación (que se VEA), los moves clásicos no
            // llegan: estos clones cubren la distancia. Solo presentación:
            // la tabla ya decidió, acá el golpe tiene que llegar hasta el rival.
            new MoveDef { Id = "strongFar", Name = "Kick", Anim = AnimKind.AttackA, Startup = 14, Active = 4, Recovery = 26,
                Desc = "(teatro yomi) Kick con embestida: cruza el escenario para pegar de lejos.",
                MoveDx = 1.7f, MotionStart = 0, MotionEnd = 14,
                Hits = new[] { new HitWindow { Start = 14, Duration = 4, Fwd0 = 0.4f, Fwd1 = 1.5f, Y0 = 0.6f, Y1 = 2.4f,
                    Damage = 2f, Hitstun = 45, Blockstun = 18, CounterStun = 58, Push = 0.5f, Knockdown = true, GuardDamage = 30f } } },

            new MoveDef { Id = "jumpInFar", Name = "Salto", Anim = AnimKind.Jump, Startup = 6, Active = 28, Recovery = 10,
                Desc = "(teatro yomi) Salto de entrada largo: cruza desde LEJOS con la patada al caer.",
                MoveDx = 3.0f, MotionStart = 6, MotionEnd = 34, AirStart = 6, AirEnd = 34,
                Hits = new[] { new HitWindow { Start = 20, Duration = 10, Fwd0 = 0.2f, Fwd1 = 0.95f, Y0 = 0.85f, Y1 = 1.65f,
                    Damage = 1f, Hitstun = 26, Blockstun = 15, CounterStun = 36, Push = 0.2f, GuardDamage = 15f } } },

            new MoveDef { Id = "dashInFar", Name = "Dash", Anim = AnimKind.Dash, Startup = 2, Active = 10, Recovery = 4,
                Desc = "(teatro yomi) Dash de entrada largo: de LEJOS a CERCA de una.",
                MoveDx = 2.4f, MotionStart = 2, MotionEnd = 12 },
        };
    }

    public struct Projectile
    {
        public int Owner;
        public float X;
        public float SpawnX; // de dónde salió: el hadouken común se disipa a ProjRange de acá
        public int Dir;
        public bool Alive;
        public bool Super; // Shinku: más ancho y rápido, arrasa hadoukens, imparryable

        // la super es más ANCHA pero no más alta: saltarla sigue siendo el counter
        public WorldRect Rect => new WorldRect
        {
            X0 = X - SimConfig.ProjHalfWidth * (Super ? 1.8f : 1f),
            X1 = X + SimConfig.ProjHalfWidth * (Super ? 1.8f : 1f),
            Y0 = SimConfig.ProjY0, Y1 = SimConfig.ProjY1
        };
    }

    public class FighterState
    {
        public float Hp = SimConfig.MaxHp;
        public float Guard = SimConfig.GuardMax;
        public float ArmHp = SimConfig.LimbHp;
        public float LegHp = SimConfig.LimbHp;
        public float X;
        public float PrevX; // X del tick anterior: la view interpola entre ambos (sin smear mentiroso)
        public int Face = 1;
        public List<int> Queue = new List<int>();
        public int QueueIndex;
        public int MoveIndex = -1;
        public int MoveStartTick;
        public bool Crushed; // guard crush en curso (distingue su stun del hitstun común)
        public int Super;    // barra de super (0..SuperMax): carga con frames de overflow
        public uint WindowHit; // bitmask: 1 = esa ventana de hit ya conectó
        public StunKind Stun = StunKind.None;
        public int StunEndTick;
        public bool BlockEnabled = true; // el dummy de práctica no bloquea
        // Bloqueo bancado (economía AP): la carta Bloquear que bloquea al
        // menos un golpe este turno paga +1 AP. Solo la CARTA — el bloqueo
        // automático en neutral defiende igual pero no banca (Ley 2/9 de la
        // biblia: defender con intención alimenta la economía).
        public bool BankedBlock;
        // Retardo de coreografía (teatro YOMI): la cola no arranca antes de
        // este tick, SIN stun falso — el peleador espera en neutral, sin
        // badge ni pose de golpeado. 0 = sin efecto (modos clásicos).
        public int QueueDelayTick;

        public FighterState Clone()
        {
            var c = (FighterState)MemberwiseClone();
            c.Queue = new List<int>(Queue);
            return c;
        }
    }

    public enum EvKind { Hit, Blocked, Parry, Whiff, Tech, GuardCrush, LimbLost }

    public enum Limb { Arm, Leg }

    public struct SimEvent
    {
        public int Attacker;
        public EvKind Kind;
        public float Damage;
        public bool Counter;
        public int MoveIndex;
        public int FrameAdv; // ventaja del atacante tras el intercambio (+ = a favor)
        public Limb Limb;    // solo para LimbLost
    }

    public class MatchSim
    {
        public FighterState[] Fighters = { new FighterState(), new FighterState() };
        public List<Projectile> Projectiles = new List<Projectile>();
        public int Tick;
        public bool Over;
        public int Winner = -1;
        public List<SimEvent> LastEvents = new List<SimEvent>();

        public MatchSim()
        {
            Fighters[0].X = -SimConfig.StartX;
            Fighters[1].X = SimConfig.StartX;
            Fighters[0].PrevX = Fighters[0].X;
            Fighters[1].PrevX = Fighters[1].X;
            Fighters[0].Face = 1;
            Fighters[1].Face = -1;
        }

        public MatchSim Clone()
        {
            return new MatchSim
            {
                Fighters = new[] { Fighters[0].Clone(), Fighters[1].Clone() },
                Projectiles = new List<Projectile>(Projectiles),
                Tick = Tick,
                Over = Over,
                Winner = Winner,
            };
        }

        public MoveDef CurrentMove(int i) => Fighters[i].MoveIndex < 0 ? null : MoveCatalog.All[Fighters[i].MoveIndex];
        public int Phase(int i) => Tick - Fighters[i].MoveStartTick;
        public bool IsStunned(int i) => Tick < Fighters[i].StunEndTick && Fighters[i].Stun != StunKind.None;
        public int StunRemaining(int i) => IsStunned(i) ? Fighters[i].StunEndTick - Tick : 0;

        public bool IsAirborne(int i)
        {
            var m = CurrentMove(i);
            if (m == null || !m.HasAir) return false;
            int p = Phase(i);
            return p >= m.AirStart && p < m.AirEnd;
        }

        public bool IsInvulnerable(int i)
        {
            var m = CurrentMove(i);
            if (m == null || m.InvulnEnd <= m.InvulnStart) return false;
            int p = Phase(i);
            return p >= m.InvulnStart && p < m.InvulnEnd;
        }

        public bool IsCrouching(int i)
        {
            var m = CurrentMove(i);
            if (m == null || m.CrouchEnd <= m.CrouchStart) return false;
            int p = Phase(i);
            return p >= m.CrouchStart && p < m.CrouchEnd;
        }

        public bool IsProjImmune(int i)
        {
            var m = CurrentMove(i);
            if (m == null || m.ProjImmuneEnd <= m.ProjImmuneStart) return false;
            int p = Phase(i);
            return p >= m.ProjImmuneStart && p < m.ProjImmuneEnd;
        }

        public bool IsParrying(int i)
        {
            var f = Fighters[i];
            if (f.MoveIndex != MoveCatalog.Parry || IsStunned(i) || IsAirborne(i)) return false;
            var m = MoveCatalog.All[MoveCatalog.Parry];
            int p = Phase(i);
            return p >= m.Startup && p < m.Startup + m.Active;
        }

        // Pérdida de miembros: sin brazo no hay A ni Hadouken; sin pierna no
        // hay B ni Tatsumaki (y las patadas aéreas no salen: saltás igual).
        // Con el agachado desactivado, sus movimientos se consumen y dejan al
        // jugador neutral (protege códigos async de builds con el flag prendido).
        public bool MoveAllowed(int i, int moveIndex)
        {
            if (!SimConfig.CrouchEnabled && (moveIndex == MoveCatalog.Crouch || moveIndex == MoveCatalog.LowKick))
                return false;
            // la super pide la barra LLENA (solo carga en turno fluido)
            if (moveIndex == MoveCatalog.Super && Fighters[i].Super < SimConfig.SuperMax)
                return false;
            if (!SimConfig.LimbsEnabled) return true;
            var f = Fighters[i];
            if (f.ArmHp <= 0f && (moveIndex == MoveCatalog.AttackA || moveIndex == MoveCatalog.Hadouken))
                return false;
            if (f.LegHp <= 0f && (moveIndex == MoveCatalog.AttackB || moveIndex == MoveCatalog.Tatsu || moveIndex == MoveCatalog.LowKick))
                return false;
            return true;
        }

        // Guardia automática: neutral o caminar hacia atrás — en el piso.
        public bool IsBlockingState(int i)
        {
            var f = Fighters[i];
            if (!f.BlockEnabled || IsStunned(i) || IsAirborne(i)) return false;
            if (f.MoveIndex < 0) return true;
            return f.MoveIndex == MoveCatalog.WalkB || f.MoveIndex == MoveCatalog.Crouch;
        }

        public void SetQueue(int i, IEnumerable<int> moves)
        {
            Fighters[i].Queue = new List<int>(moves);
            Fighters[i].QueueIndex = 0;
        }

        // Wakeup option: ajusta el knockdown arrastrado (rápido = delta negativo,
        // quedarse = positivo). Se aplica al arrancar la ejecución del turno.
        public void AdjustKnockdown(int i, int delta)
        {
            var f = Fighters[i];
            if (delta == 0 || f.Stun != StunKind.Knockdown || !IsStunned(i)) return;
            f.StunEndTick = Math.Max(Tick, f.StunEndTick + delta);
        }

        public int OnTurnEnd(int i)
        {
            var f = Fighters[i];
            int lost = f.Queue.Count - f.QueueIndex;
            // Turno fluido (y modo AP: el préstamo ES un cruce): el move en
            // curso NO se corta — sigue ejecutando y el turno siguiente
            // arranca con esos frames ya comprometidos. Cada frame que cruza
            // CARGA la barra de super: el riesgo paga.
            if (f.MoveIndex >= 0 && !SimConfig.FluidTurn) { lost++; f.MoveIndex = -1; }
            else if (f.MoveIndex >= 0)
                f.Super = Math.Min(SimConfig.SuperMax, f.Super + CommittedRemaining(i));
            // turno estricto: el slot cortado no deja deuda de pad colgando
            if (!SimConfig.FluidTurn && f.QueueDelayTick > Tick) f.QueueDelayTick = Tick;
            f.Queue.Clear();
            f.QueueIndex = 0;
            f.BankedBlock = false; // el bancado se cobra por turno (lo lee el controller ANTES)
            return lost;
        }

        // Reversal: derribado, te levantás YA y el empujón manda al rival a
        // ReversalGap (lo que la pared no deje, lo retrocede el propio). El
        // costo en AP y el "1 por round" los administra el controller — acá
        // solo la física, determinista.
        public void Reversal(int i)
        {
            var f = Fighters[i];
            if (f.Stun != StunKind.Knockdown || !IsStunned(i)) return;
            f.StunEndTick = Tick;
            f.Stun = StunKind.None;
            var o = Fighters[1 - i];
            float dir = o.X >= f.X ? 1f : -1f;
            float target = f.X + dir * SimConfig.ReversalGap;
            float clamped = Math.Max(-SimConfig.StageHalfWidth, Math.Min(SimConfig.StageHalfWidth, target));
            o.X = clamped;
            float shortfall = Math.Abs(target - clamped);
            if (shortfall > 0f) // rival contra la pared: el que se levanta retrocede
                f.X = Math.Max(-SimConfig.StageHalfWidth, Math.Min(SimConfig.StageHalfWidth, f.X - dir * shortfall));
        }

        // Frames del move en curso que quedan por ejecutar (turno fluido):
        // al planificar, esto es lo que ya está comprometido del turno nuevo.
        // En modo AP cuenta el slot COMPLETO (QueueDelayTick): los AP
        // prestados son slots, no solo los frames crudos del move.
        public int CommittedRemaining(int i)
        {
            var f = Fighters[i];
            int frames = f.MoveIndex < 0 ? 0
                : Math.Max(0, MoveCatalog.All[f.MoveIndex].Total - (Tick - f.MoveStartTick));
            if (SimConfig.ApActive)
                frames = Math.Max(frames, f.QueueDelayTick - Tick);
            return Math.Max(0, frames);
        }

        public WorldRect HurtRect(int i)
        {
            var f = Fighters[i];
            if (IsAirborne(i))
                return new WorldRect { X0 = f.X - SimConfig.HurtHalfWidth, X1 = f.X + SimConfig.HurtHalfWidth, Y0 = SimConfig.AirHurtY0, Y1 = SimConfig.AirHurtY1 };
            float h = f.Stun == StunKind.Knockdown && IsStunned(i) ? 0.55f
                    : IsCrouching(i) ? SimConfig.CrouchHeight
                    : SimConfig.HurtHeight;
            return new WorldRect { X0 = f.X - SimConfig.HurtHalfWidth, X1 = f.X + SimConfig.HurtHalfWidth, Y0 = 0f, Y1 = h };
        }

        public WorldRect HitRectWorld(int i, HitWindow h)
        {
            var f = Fighters[i];
            float a = f.X + h.Fwd0 * f.Face, b = f.X + h.Fwd1 * f.Face;
            return new WorldRect { X0 = Math.Min(a, b), X1 = Math.Max(a, b), Y0 = h.Y0, Y1 = h.Y1 };
        }

        public void GetActiveHitRects(int i, List<WorldRect> result)
        {
            var f = Fighters[i];
            var m = CurrentMove(i);
            if (m == null) return;
            int phase = Phase(i);
            for (int wi = 0; wi < m.Hits.Length; wi++)
            {
                var h = m.Hits[wi];
                if (phase >= h.Start && phase < h.Start + h.Duration && (f.WindowHit & (1u << wi)) == 0)
                {
                    var r = HitRectWorld(i, h);
                    r.Grab = h.IsGrab;
                    result.Add(r);
                }
            }
        }

        public void GetProjectileRects(int owner, List<WorldRect> result)
        {
            foreach (var p in Projectiles)
                if (p.Alive && p.Owner == owner) result.Add(p.Rect);
        }

        bool HasProjectile(int owner)
        {
            foreach (var p in Projectiles)
                if (p.Alive && p.Owner == owner) return true;
            return false;
        }

        void StartQueuedMove(int i)
        {
            var f = Fighters[i];
            int mi = f.Queue[f.QueueIndex++];
            // Si quedó inválida después de planificar, se consume en neutral.
            if (!MoveAllowed(i, mi))
            {
                f.MoveIndex = -1;
                f.WindowHit = 0;
                return;
            }
            f.MoveIndex = mi;
            f.MoveStartTick = Tick;
            f.WindowHit = 0;
            f.Face = Fighters[1 - i].X >= f.X ? 1 : -1;
            if (mi == MoveCatalog.Super) f.Super = 0; // la barra se gasta al arrancar
            // Modo AP: el move ocupa su slot ENTERO — la próxima orden no
            // arranca hasta que el slot termine (el resto se espera en neutral).
            if (SimConfig.ApActive)
                f.QueueDelayTick = Tick + MoveCatalog.All[mi].PaddedTotal;
        }

        public void Step()
        {
            if (Over) return;
            LastEvents.Clear();
            Fighters[0].PrevX = Fighters[0].X;
            Fighters[1].PrevX = Fighters[1].X;

            // fin de comandos (+ whiff), fin de stun, arranque del siguiente
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                if (f.MoveIndex >= 0)
                {
                    var m = MoveCatalog.All[f.MoveIndex];
                    if (Tick - f.MoveStartTick >= m.Total)
                    {
                        if (m.Hits.Length > 0 && f.WindowHit == 0)
                            LastEvents.Add(new SimEvent { Attacker = i, Kind = EvKind.Whiff, MoveIndex = f.MoveIndex });
                        f.MoveIndex = -1;
                    }
                }
                if (!IsStunned(i))
                {
                    f.Stun = StunKind.None;
                    f.Crushed = false;
                    // apenas puede, sigue ejecutando lo que le quedaba
                    if (f.MoveIndex < 0 && f.QueueIndex < f.Queue.Count && Tick >= f.QueueDelayTick)
                        StartQueuedMove(i);
                }
                if (f.MoveIndex < 0)
                    f.Face = Fighters[1 - i].X >= f.X ? 1 : -1;

                // Guardia = stamina (2026-07-19): regenera SOLO mientras
                // ejecutás un move que no bloquea. Quieto o bloqueando no
                // cura — la zanahoria es del que juega: llenar la barra de
                // órdenes (y cruzarla en turno fluido) recupera la guardia.
                if (f.MoveIndex >= 0 && !IsBlockingState(i) && !IsStunned(i))
                    f.Guard = Math.Min(SimConfig.GuardMax, f.Guard + SimConfig.GuardRegen);
            }

            // desplazamiento (sin pierna: caminar y dashear rinde menos)
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                var m = CurrentMove(i);
                if (m == null || m.MotionEnd <= m.MotionStart) continue;
                int phase = Phase(i);
                if (phase < m.MotionStart || phase >= m.MotionEnd) continue;
                float speed = f.LegHp <= 0f && (m.Anim == AnimKind.Walk || m.Anim == AnimKind.Dash)
                    ? SimConfig.LeglessSpeedFactor : 1f;
                f.X += f.Face * m.MoveDx * speed / (m.MotionEnd - m.MotionStart);
            }

            SeparateAndClamp();

            // spawn de proyectiles
            for (int i = 0; i < 2; i++)
            {
                var m = CurrentMove(i);
                if (m == null || m.SpawnFrame < 0 || Phase(i) != m.SpawnFrame) continue;
                if (HasProjectile(i)) continue; // uno por vez
                var f = Fighters[i];
                Projectiles.Add(new Projectile { Owner = i, X = f.X + f.Face * 0.7f, SpawnX = f.X + f.Face * 0.7f, Dir = f.Face, Alive = true,
                    Super = f.MoveIndex == MoveCatalog.Super });
            }

            // proyectiles: mover, chocar entre sí, pegar
            for (int pi = 0; pi < Projectiles.Count; pi++)
            {
                var p = Projectiles[pi];
                if (!p.Alive) continue;
                p.X += p.Dir * (p.Super ? SimConfig.SuperSpeed : SimConfig.ProjSpeed);
                if (Math.Abs(p.X) > SimConfig.StageHalfWidth + 1.2f) p.Alive = false;
                // el hadouken común tiene alcance finito: zonear de fullscreen
                // whiffea y la casa del zoneo pasa a ser la media distancia.
                // La super sigue siendo fullscreen (para eso pagás la barra).
                if (!p.Super && Math.Abs(p.X - p.SpawnX) > SimConfig.ProjRange) p.Alive = false;
                Projectiles[pi] = p;
            }
            for (int a = 0; a < Projectiles.Count; a++)
                for (int b = a + 1; b < Projectiles.Count; b++)
                {
                    if (!Projectiles[a].Alive || !Projectiles[b].Alive) continue;
                    if (Projectiles[a].Owner == Projectiles[b].Owner) continue;
                    if (!Projectiles[a].Rect.Overlaps(Projectiles[b].Rect)) continue;
                    // la super ARRASA hadoukens comunes y sigue de largo
                    bool superA = Projectiles[a].Super, superB = Projectiles[b].Super;
                    if (!superB) { var pb0 = Projectiles[b]; pb0.Alive = false; Projectiles[b] = pb0; }
                    if (!superA) { var pa0 = Projectiles[a]; pa0.Alive = false; Projectiles[a] = pa0; }
                    if (superA && superB) // super vs super: se anulan
                    {
                        var pa = Projectiles[a]; pa.Alive = false; Projectiles[a] = pa;
                        var pb = Projectiles[b]; pb.Alive = false; Projectiles[b] = pb;
                    }
                }
            for (int pi = 0; pi < Projectiles.Count; pi++)
            {
                var p = Projectiles[pi];
                if (!p.Alive) continue;
                int def = 1 - p.Owner;
                if (IsInvulnerable(def) || IsProjImmune(def)) continue; // el tatsu los atraviesa: el proyectil sigue viajando
                if (!p.Rect.Overlaps(HurtRect(def))) continue;
                var pend = BuildPending(p.Owner, p.Super ? MoveCatalog.Super : MoveCatalog.Hadouken,
                    p.Super ? SimConfig.SuperDamage : SimConfig.ProjDamage,
                    p.Super ? SimConfig.SuperHitstun : SimConfig.ProjHitstun,
                    p.Super ? SimConfig.SuperBlockstun : SimConfig.ProjBlockstun,
                    p.Super ? SimConfig.SuperHitstun + 10 : SimConfig.ProjHitstun + 8,
                    p.Super ? SimConfig.SuperPush : SimConfig.ProjPush,
                    knockdown: p.Super, attackerFreeTick: AttackerFreeTick(p.Owner),
                    guardDamage: p.Super ? SimConfig.SuperGuardDamage : SimConfig.ProjGuardDamage,
                    hitY: (SimConfig.ProjY0 + SimConfig.ProjY1) * 0.5f,
                    isProjectile: true);
                if (p.Super) pend.Parried = false; // la super no se parrea
                ApplyContact(pend);
                p.Alive = false;
                Projectiles[pi] = p;
            }
            Projectiles.RemoveAll(x => !x.Alive);

            // Golpes cuerpo a cuerpo, en DOS FASES: primero se evalúan todos
            // contra el estado previo del frame, y recién después se aplican.
            // Si los dos conectan en el mismo frame es un TRADE: ambos comen
            // daño y stun (sin esto, el jugador 0 ganaría por orden de loop).
            _pending.Clear();
            for (int i = 0; i < 2; i++)
            {
                var atk = Fighters[i];
                var m = CurrentMove(i);
                if (m == null) continue;
                int phase = Phase(i);

                for (int wi = 0; wi < m.Hits.Length; wi++)
                {
                    var h = m.Hits[wi];
                    if (phase < h.Start || phase >= h.Start + h.Duration || (atk.WindowHit & (1u << wi)) != 0) continue;
                    if (m.Anim == AnimKind.Jump && atk.LegHp <= 0f) continue; // sin pierna la patada aérea no sale
                    if (IsInvulnerable(1 - i)) continue; // pasa de largo, la ventana sigue viva
                    if (h.IsGrab && (IsAirborne(1 - i) || (Fighters[1 - i].Stun == StunKind.Knockdown && IsStunned(1 - i))))
                        continue; // no se agarra al que salta ni al caído; la ventana sigue viva
                    if (!HitRectWorld(i, h).Overlaps(HurtRect(1 - i))) continue;

                    atk.WindowHit |= 1u << wi;
                    var pending = BuildPending(i, atk.MoveIndex, h.Damage, h.Hitstun, h.Blockstun, h.CounterStun,
                        h.Push, h.Knockdown, attackerFreeTick: atk.MoveStartTick + m.Total, guardDamage: h.GuardDamage,
                        hitY: (h.Y0 + h.Y1) * 0.5f);
                    pending.IsGrab = h.IsGrab;
                    pending.Parried = !h.IsGrab && IsParrying(1 - i);
                    _pending.Add(pending);
                }
            }

            // agarre vs agarre en el mismo frame = TECH: se separan, nadie come
            bool grab0 = false, grab1 = false;
            foreach (var pending in _pending)
                if (pending.IsGrab) { if (pending.Attacker == 0) grab0 = true; else grab1 = true; }
            if (grab0 && grab1)
            {
                _pending.RemoveAll(x => x.IsGrab);
                for (int i = 0; i < 2; i++)
                {
                    Fighters[i].MoveIndex = -1;
                    ClearSlotPad(i);
                    Fighters[i].X -= Fighters[i].Face * 0.55f;
                }
                LastEvents.Add(new SimEvent { Attacker = 0, Kind = EvKind.Tech, MoveIndex = MoveCatalog.Grab });
            }

            foreach (var pending in _pending)
                ApplyContact(pending);

            SeparateAndClamp();

            if (Fighters[0].Hp <= 0f || Fighters[1].Hp <= 0f)
            {
                Over = true;
                bool dead0 = Fighters[0].Hp <= 0f, dead1 = Fighters[1].Hp <= 0f;
                Winner = dead0 && dead1 ? -1 : dead0 ? 1 : 0;
            }

            Tick++;
        }

        // Modo AP: un contacto que cancela el move devuelve el resto del slot
        // (el stun/tech lo reemplaza — sin esto el turno siguiente cobraría
        // stun Y slot comprometido a la vez). En YOMI no toca el retardo de
        // coreografía (ApActive es false ahí).
        void ClearSlotPad(int i)
        {
            if (SimConfig.ApActive && Fighters[i].QueueDelayTick > Tick)
                Fighters[i].QueueDelayTick = Tick;
        }

        int AttackerFreeTick(int i)
        {
            var m = CurrentMove(i);
            return m == null ? Tick : Fighters[i].MoveStartTick + m.Total;
        }

        // Contacto pendiente: los flags del defensor (bloqueo, counter, aéreo)
        // se capturan ANTES de aplicar nada, para que los trades sean justos.
        struct PendingHit
        {
            public int Attacker, MoveIndex;
            public float Damage, Push, GuardDamage, HitY;
            public int Hitstun, Blockstun, CounterStun, AttackerFree;
            public bool Knockdown, Guarded, Counter, AirHit, IsGrab, Parried, IsProjectile;
        }

        readonly List<PendingHit> _pending = new List<PendingHit>();

        PendingHit BuildPending(int attacker, int moveIndex, float damage, int hitstun, int blockstun, int counterStun,
            float push, bool knockdown, int attackerFreeTick, float guardDamage, float hitY = 1.2f,
            bool isProjectile = false)
        {
            int d = 1 - attacker;
            var defMove = CurrentMove(d);
            return new PendingHit
            {
                Attacker = attacker,
                MoveIndex = moveIndex,
                Damage = damage,
                Push = push,
                Hitstun = hitstun,
                Blockstun = blockstun,
                CounterStun = counterStun,
                AttackerFree = attackerFreeTick,
                GuardDamage = guardDamage,
                HitY = hitY,
                Knockdown = knockdown,
                Guarded = IsBlockingState(d),
                Counter = defMove != null && defMove.IsAttack && Phase(d) < defMove.Startup,
                AirHit = IsAirborne(d),
                Parried = IsParrying(d),
                IsProjectile = isProjectile,
            };
        }

        // Esquina real: si el empuje aplasta al defensor contra la pared, el
        // sobrante se transfiere al atacante hacia atrás (como en SF). Sin esto
        // la esquina es más letal de lo que debería: el pushback muere ahí.
        void PushDefender(int attacker, int face, float push)
        {
            var def = Fighters[1 - attacker];
            float target = def.X + face * push;
            float clamped = Math.Max(-SimConfig.StageHalfWidth, Math.Min(SimConfig.StageHalfWidth, target));
            def.X = clamped;
            float excess = Math.Abs(target - clamped);
            if (excess > 0f) Fighters[attacker].X -= face * excess;
        }

        void ApplyContact(PendingHit p)
        {
            int d = 1 - p.Attacker;
            var def = Fighters[d];
            int face = Fighters[p.Attacker].Face;

            if (p.Parried && !p.IsGrab)
            {
                // el parry recarga guardia: la respuesta activa al chip del zoner
                def.Guard = Math.Min(SimConfig.GuardMax, def.Guard + SimConfig.ParryGuardRefund);
                const int punishStun = 18;
                if (!p.IsProjectile)
                {
                    var attacker = Fighters[p.Attacker];
                    attacker.MoveIndex = -1;
                    attacker.Stun = StunKind.Hitstun;
                    attacker.StunEndTick = Tick + punishStun;
                    ClearSlotPad(p.Attacker); // el stun reemplaza el slot: no se cobra doble
                }
                int parryFree = def.MoveStartTick + MoveCatalog.All[MoveCatalog.Parry].Total;
                LastEvents.Add(new SimEvent
                {
                    Attacker = d,
                    Kind = EvKind.Parry,
                    MoveIndex = p.MoveIndex,
                    FrameAdv = p.IsProjectile ? 0 : Tick + punishStun - parryFree,
                });
                return;
            }

            if (p.Guarded && !p.IsGrab) // el agarre rompe la guardia
            {
                def.Guard -= p.GuardDamage;
                if (def.Guard <= 0f)
                {
                    // GUARD CRUSH: stun largo sin daño; la barra renace a la mitad
                    def.Guard = SimConfig.GuardCrushRespawn;
                    def.MoveIndex = -1;
                    ClearSlotPad(d);
                    def.Stun = StunKind.Hitstun;
                    def.Crushed = true;
                    def.StunEndTick = Tick + SimConfig.GuardCrushStun;
                    PushDefender(p.Attacker, face, p.Push);
                    LastEvents.Add(new SimEvent { Attacker = p.Attacker, Kind = EvKind.GuardCrush, MoveIndex = p.MoveIndex,
                        FrameAdv = def.StunEndTick - p.AttackerFree });
                    return;
                }
                if (def.MoveIndex == MoveCatalog.WalkB) def.BankedBlock = true; // bloqueo bancado: +1 AP al cerrar el turno
                def.MoveIndex = -1; // el paso atrás / espera se corta en blockstun
                ClearSlotPad(d);
                def.Stun = StunKind.Blockstun;
                def.StunEndTick = Tick + p.Blockstun;
                PushDefender(p.Attacker, face, p.Push * 0.6f);
                LastEvents.Add(new SimEvent { Attacker = p.Attacker, Kind = EvKind.Blocked, MoveIndex = p.MoveIndex,
                    FrameAdv = def.StunEndTick - p.AttackerFree });
                return;
            }

            bool kd = p.Knockdown || p.AirHit; // pegarle a alguien en el aire lo derriba
            // counter: +1 de daño SOLO para golpes de 1 (un DP counter de 3/6 HP
            // decidía medio round en una lectura); los pesados suman solo stun
            float dmg = p.Damage + (p.Counter && p.Damage < 2f ? 1f : 0f);
            int stun = p.Counter ? p.CounterStun : p.Hitstun;
            if (p.AirHit) stun = Math.Max(stun, 60); // caída del aire = hard knockdown

            def.Hp = Math.Max(0f, def.Hp - dmg);
            PushDefender(p.Attacker, face, p.Push);
            def.MoveIndex = -1;
            ClearSlotPad(d);
            def.Stun = kd ? StunKind.Knockdown : StunKind.Hitstun;
            def.StunEndTick = Tick + stun;

            LastEvents.Add(new SimEvent { Attacker = p.Attacker, Kind = EvKind.Hit, Damage = dmg, Counter = p.Counter,
                MoveIndex = p.MoveIndex, FrameAdv = def.StunEndTick - p.AttackerFree });

            // daño localizado: bajo la cintura come pierna, arriba come brazo
            if (!SimConfig.LimbsEnabled) return;
            if (p.HitY < SimConfig.LimbSplitY)
            {
                if (def.LegHp > 0f)
                {
                    def.LegHp = Math.Max(0f, def.LegHp - dmg);
                    if (def.LegHp <= 0f)
                        LastEvents.Add(new SimEvent { Attacker = p.Attacker, Kind = EvKind.LimbLost, Limb = Limb.Leg, MoveIndex = p.MoveIndex });
                }
            }
            else if (def.ArmHp > 0f)
            {
                def.ArmHp = Math.Max(0f, def.ArmHp - dmg);
                if (def.ArmHp <= 0f)
                    LastEvents.Add(new SimEvent { Attacker = p.Attacker, Kind = EvKind.LimbLost, Limb = Limb.Arm, MoveIndex = p.MoveIndex });
            }
        }

        void SeparateAndClamp()
        {
            float d = Fighters[1].X - Fighters[0].X;
            float abs = Math.Abs(d);
            // en el aire se pueden cruzar; en el piso no se atraviesan
            if (abs < SimConfig.MinSeparation && !IsAirborne(0) && !IsAirborne(1))
            {
                float push = (SimConfig.MinSeparation - abs) * 0.5f * (d >= 0f ? 1f : -1f);
                Fighters[0].X -= push;
                Fighters[1].X += push;
            }
            for (int i = 0; i < 2; i++)
                Fighters[i].X = Math.Max(-SimConfig.StageHalfWidth, Math.Min(SimConfig.StageHalfWidth, Fighters[i].X));
        }
    }

    // Economía de ACTION POINTS del modo clásico (2026-07-20): pura y
    // compartida por MatchController y el harness del lab — la regla vive en
    // UN solo lugar. Ingreso por turno menor que la capacidad física del
    // turno (Ley 7 de la biblia: la escasez crea posiciones fuertes/débiles
    // medibles); lo no gastado SE GUARDA hasta el tope; el bloqueo bancado
    // suma +1. El stock solo limita la planificación: nunca toca la sim.
    public class ApEconomy
    {
        public readonly int[] Stock = new int[2];
        public readonly bool[] ReversalUsed = new bool[2];
        public const int ReversalCost = 2;

        // Tope de ahorro = LA BARRA LLENA (rebalance 2026-07-20): antes era
        // capacidad+2 y las bolitas mentían — mostrabas 7 pero el turno
        // físico banca 5. Con tope = capacidad, las bolitas SIEMPRE son lo
        // que podés gastar (salvo stun, que se ve en la timeline).
        public static int Cap(int apPerTurn) => apPerTurn;
        public static int Income(int apPerTurn) => apPerTurn - 1; // 60f: +4 por turno

        public void ResetRound(int apPerTurn)
        {
            Stock[0] = Stock[1] = apPerTurn; // primer turno a full
            ReversalUsed[0] = ReversalUsed[1] = false;
        }

        // cierre de turno: cobra lo gastado (plan + reversal), paga el
        // ingreso y el bloqueo bancado, y aplica el tope de ahorro
        public void EndTurn(int i, int apPerTurn, int spentAp, bool banked)
        {
            int s = Stock[i] - spentAp + Income(apPerTurn) + (banked ? 1 : 0);
            Stock[i] = Math.Max(0, Math.Min(Cap(apPerTurn), s));
        }
    }

    // Código de turno para el online asincrónico: serializa (lado, turno,
    // wakeup, cola) en base64 corto. La sim determinista hace el resto:
    // ambos jugadores aplican los dos códigos y ven exactamente la misma pelea.
    // v2 (2026-07-20): el byte de wakeup pasa de bool a trit (0 = quedarse,
    // 1 = rápido, 2 = REVERSAL) — los códigos v1 se rechazan (sala nueva).
    public static class TurnCode
    {
        const byte Version = 2;
        public const int WakeStay = 0, WakeQuick = 1, WakeReversal = 2;

        public static string Encode(int side, int turn, int wake, List<int> moves)
        {
            var bytes = new byte[4 + moves.Count];
            bytes[0] = Version;
            bytes[1] = (byte)side;
            bytes[2] = (byte)turn;
            bytes[3] = (byte)wake;
            for (int i = 0; i < moves.Count; i++) bytes[4 + i] = (byte)moves[i];
            return "LF" + Convert.ToBase64String(bytes);
        }

        public static bool TryDecode(string code, out int side, out int turn, out int wake, out List<int> moves)
        {
            side = turn = 0;
            wake = WakeQuick;
            moves = null;
            if (string.IsNullOrEmpty(code) || !code.StartsWith("LF")) return false;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(code.Substring(2)); }
            catch (FormatException) { return false; }
            if (bytes.Length < 4 || bytes[0] != Version) return false;
            side = bytes[1];
            turn = bytes[2];
            wake = bytes[3];
            if (side < 0 || side > 1 || wake > WakeReversal) return false;
            moves = new List<int>(bytes.Length - 4);
            for (int i = 4; i < bytes.Length; i++)
            {
                if (bytes[i] >= MoveCatalog.All.Length) return false;
                moves.Add(bytes[i]);
            }
            return true;
        }
    }

    // Preview del plan: tu cola contra un rival que no mete inputs nuevos
    // (conserva su stun arrastrado; en neutral bloquea, como en el juego real).
    public class PlanPreview
    {
        public float DamageIfStill;
        public int BlockedCount;

        public static PlanPreview Build(MatchSim src, int fighter, List<int> plan, int turnFrames)
        {
            var sim = src.Clone();
            sim.SetQueue(fighter, plan);
            sim.SetQueue(1 - fighter, new List<int>());
            var g = new PlanPreview();

            for (int t = 0; t < turnFrames; t++)
            {
                sim.Step();
                foreach (var ev in sim.LastEvents)
                {
                    if (ev.Attacker != fighter) continue;
                    if (ev.Kind == EvKind.Hit) g.DamageIfStill += ev.Damage;
                    else if (ev.Kind == EvKind.Blocked) g.BlockedCount++;
                }
                if (sim.Over) break;
            }
            return g;
        }
    }
}
