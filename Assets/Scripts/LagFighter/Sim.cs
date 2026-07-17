using System;
using System.Collections.Generic;

namespace LagFighter
{
    // Simulación pura y determinista. Ryu vs Ken por turnos programados:
    //  - 60 ticks/s = frames. Cada turno ambos arman una cola de hasta 240
    //    frames y se ejecutan simultáneas.
    //  - Guardia automática: bloqueás si estás en neutral, esperando o
    //    caminando hacia atrás (y en el piso). No hay botón de bloqueo.
    //  - Estados con framedata: HITSTUN / BLOCKSTUN / KNOCKDOWN. Comen parte
    //    del turno; apenas terminan, la cola sigue ejecutando lo que quede.
    //  - Proyectiles (Hadouken), saltos que los pasan por arriba, y Shoryuken
    //    invulnerable anti-aéreo con recuperación gigante.

    public static class SimConfig
    {
        public const int TicksPerSecond = 60;
        public const float TickDuration = 1f / TicksPerSecond;
        public const int TurnFrames = 60; // 1 segundo por turno: denso, decisión a decisión
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
    }

    public enum AnimKind { Walk, Dash, Jump, AttackA, AttackB, Fireball, Dragon, Wait }
    public enum StunKind { None, Hitstun, Blockstun, Knockdown }

    public struct HitWindow
    {
        public int Start, Duration;
        public float Fwd0, Fwd1, Y0, Y1;
        public float Damage;
        public int Hitstun, Blockstun, CounterStun;
        public float Push;
        public bool Knockdown;
    }

    public struct WorldRect
    {
        public float X0, X1, Y0, Y1;
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
        public int InvulnStart = -1, InvulnEnd = -1; // ventana invulnerable (Shoryuken)
        public int SpawnFrame = -1;                  // frame en que larga el proyectil
        public int Total => Startup + Active + Recovery;
        public bool IsAttack => Hits.Length > 0 || SpawnFrame >= 0;
        public bool HasAir => AirEnd > AirStart;
        public float TotalDamage { get { float s = 0f; foreach (var h in Hits) s += h.Damage; return SpawnFrame >= 0 ? s + SimConfig.ProjDamage : s; } }
    }

    public static class MoveCatalog
    {
        public const int WalkF = 0, WalkB = 1, DashF = 2, DashB = 3,
                         JumpF = 4, JumpN = 5, JumpB = 6,
                         AttackA = 7, AttackB = 8, Hadouken = 9, Shoryuken = 10, Wait = 11;

        public static readonly MoveDef[] All =
        {
            new MoveDef { Id = "walkF", Name = "Caminar +", Anim = AnimKind.Walk, Startup = 2, Active = 16, Recovery = 2,
                Desc = "Avanza un paso corto. Caminando hacia adelante NO bloqueás.",
                MoveDx = 0.55f, MotionStart = 0, MotionEnd = 20 },

            new MoveDef { Id = "walkB", Name = "Caminar −", Anim = AnimKind.Walk, Startup = 2, Active = 16, Recovery = 2,
                Desc = "Retrocede bloqueando (más lento que avanzar, como en SF2).",
                MoveDx = -0.38f, MotionStart = 0, MotionEnd = 20 },

            new MoveDef { Id = "dashF", Name = "Dash +", Anim = AnimKind.Dash, Startup = 2, Active = 10, Recovery = 4,
                Desc = "Arremetida hacia adelante. NO bloquea: es puro compromiso.",
                MoveDx = 1.0f, MotionStart = 2, MotionEnd = 12 },

            new MoveDef { Id = "dashB", Name = "Dash −", Anim = AnimKind.Dash, Startup = 2, Active = 10, Recovery = 4,
                Desc = "Salto atrás rápido. Tampoco bloquea, pero te saca del rango.",
                MoveDx = -1.0f, MotionStart = 2, MotionEnd = 12 },

            new MoveDef { Id = "jumpF", Name = "Salto + (patada)", Anim = AnimKind.Jump, Startup = 6, Active = 28, Recovery = 6,
                Desc = "Salto adelante con patada en la bajada: EL jump-in. Pasa hadoukens; en el aire no bloqueás.",
                MoveDx = 1.9f, MotionStart = 6, MotionEnd = 34, AirStart = 6, AirEnd = 34,
                Hits = new[] { new HitWindow { Start = 20, Duration = 10, Fwd0 = 0.2f, Fwd1 = 0.95f, Y0 = 0.85f, Y1 = 1.65f,
                    Damage = 1f, Hitstun = 26, Blockstun = 15, CounterStun = 36, Push = 0.2f } } },

            new MoveDef { Id = "jumpN", Name = "Salto N", Anim = AnimKind.Jump, Startup = 6, Active = 28, Recovery = 6,
                Desc = "Salto vertical. Esquiva proyectiles sin regalar posición.",
                AirStart = 6, AirEnd = 34 },

            new MoveDef { Id = "jumpB", Name = "Salto −", Anim = AnimKind.Jump, Startup = 6, Active = 28, Recovery = 6,
                Desc = "Salto atrás. La retirada elegante sobre el hadouken.",
                MoveDx = -1.9f, MotionStart = 6, MotionEnd = 34, AirStart = 6, AirEnd = 34 },

            new MoveDef { Id = "atkA", Name = "Golpe A", Anim = AnimKind.AttackA, Startup = 6, Active = 4, Recovery = 14,
                Desc = "El jab: rápido y corto (+2 on hit, −5 on block). Atrapa avances y saltos cercanos.",
                Hits = new[] { new HitWindow { Start = 6, Duration = 4, Fwd0 = 0.45f, Fwd1 = 1.1f, Y0 = 1.0f, Y1 = 1.6f,
                    Damage = 1f, Hitstun = 20, Blockstun = 13, CounterStun = 32, Push = 0.35f } } },

            new MoveDef { Id = "atkB", Name = "Patada B", Anim = AnimKind.AttackB, Startup = 16, Active = 6, Recovery = 30,
                Desc = "El sweep: lenta, larga, 2 de daño, DERRIBA (soft). −10 si la bloquean.",
                Hits = new[] { new HitWindow { Start = 16, Duration = 6, Fwd0 = 0.5f, Fwd1 = 1.6f, Y0 = 0.5f, Y1 = 1.2f,
                    Damage = 2f, Hitstun = 42, Blockstun = 26, CounterStun = 55, Push = 0.55f, Knockdown = true } } },

            new MoveDef { Id = "hadouken", Name = "Hadouken", Anim = AnimKind.Fireball, Startup = 14, Active = 2, Recovery = 44,
                Desc = "Proyectil. 60f totales: tirarlo es comprometer EL TURNO ENTERO. Saltable y castigable.",
                SpawnFrame = 14 },

            new MoveDef { Id = "shoryu", Name = "Shoryuken", Anim = AnimKind.Dragon, Startup = 4, Active = 8, Recovery = 32,
                Desc = "Invuln frames 1-10 (después, vulnerable subiendo). Anti-aéreo, hard KD, −17 en block.",
                InvulnStart = 1, InvulnEnd = 10, AirStart = 6, AirEnd = 30, MoveDx = 0.4f, MotionStart = 2, MotionEnd = 12,
                Hits = new[] { new HitWindow { Start = 4, Duration = 8, Fwd0 = 0.15f, Fwd1 = 0.95f, Y0 = 0.7f, Y1 = 2.5f,
                    Damage = 2f, Hitstun = 60, Blockstun = 22, CounterStun = 70, Push = 0.4f, Knockdown = true } } },

            new MoveDef { Id = "wait", Name = "Esperar", Anim = AnimKind.Wait, Startup = 0, Active = 12, Recovery = 0,
                Desc = "12 frames quieto, bloqueando. El neutral también es una decisión." },
        };
    }

    public struct Projectile
    {
        public int Owner;
        public float X;
        public int Dir;
        public bool Alive;

        public WorldRect Rect => new WorldRect
        {
            X0 = X - SimConfig.ProjHalfWidth, X1 = X + SimConfig.ProjHalfWidth,
            Y0 = SimConfig.ProjY0, Y1 = SimConfig.ProjY1
        };
    }

    public class FighterState
    {
        public float Hp = SimConfig.MaxHp;
        public float X;
        public int Face = 1;
        public List<int> Queue = new List<int>();
        public int QueueIndex;
        public int MoveIndex = -1;
        public int MoveStartTick;
        public bool[] WindowHit = Array.Empty<bool>();
        public StunKind Stun = StunKind.None;
        public int StunEndTick;
        public bool BlockEnabled = true; // el dummy de práctica no bloquea

        public FighterState Clone()
        {
            var c = (FighterState)MemberwiseClone();
            c.Queue = new List<int>(Queue);
            c.WindowHit = (bool[])WindowHit.Clone();
            return c;
        }
    }

    public enum EvKind { Hit, Blocked, Whiff }

    public struct SimEvent
    {
        public int Attacker;
        public EvKind Kind;
        public float Damage;
        public bool Counter;
        public int MoveIndex;
        public int FrameAdv; // ventaja del atacante tras el intercambio (+ = a favor)
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

        // Guardia automática: neutral, esperar o caminar hacia atrás — en el piso.
        public bool IsBlockingState(int i)
        {
            var f = Fighters[i];
            if (!f.BlockEnabled || IsStunned(i) || IsAirborne(i)) return false;
            if (f.MoveIndex < 0) return true;
            return f.MoveIndex == MoveCatalog.WalkB || f.MoveIndex == MoveCatalog.Wait;
        }

        public void SetQueue(int i, IEnumerable<int> moves)
        {
            Fighters[i].Queue = new List<int>(moves);
            Fighters[i].QueueIndex = 0;
        }

        public int OnTurnEnd(int i)
        {
            var f = Fighters[i];
            int lost = f.Queue.Count - f.QueueIndex + (f.MoveIndex >= 0 ? 1 : 0);
            f.MoveIndex = -1;
            f.Queue.Clear();
            f.QueueIndex = 0;
            return lost;
        }

        public WorldRect HurtRect(int i)
        {
            var f = Fighters[i];
            if (IsAirborne(i))
                return new WorldRect { X0 = f.X - SimConfig.HurtHalfWidth, X1 = f.X + SimConfig.HurtHalfWidth, Y0 = SimConfig.AirHurtY0, Y1 = SimConfig.AirHurtY1 };
            float h = f.Stun == StunKind.Knockdown && IsStunned(i) ? 0.55f : SimConfig.HurtHeight;
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
                if (phase >= h.Start && phase < h.Start + h.Duration && !f.WindowHit[wi])
                    result.Add(HitRectWorld(i, h));
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
            f.MoveIndex = f.Queue[f.QueueIndex++];
            f.MoveStartTick = Tick;
            f.WindowHit = new bool[MoveCatalog.All[f.MoveIndex].Hits.Length];
            f.Face = Fighters[1 - i].X >= f.X ? 1 : -1;
        }

        public void Step()
        {
            if (Over) return;
            LastEvents.Clear();

            // fin de comandos (+ whiff), fin de stun, arranque del siguiente
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                if (f.MoveIndex >= 0)
                {
                    var m = MoveCatalog.All[f.MoveIndex];
                    if (Tick - f.MoveStartTick >= m.Total)
                    {
                        if (m.Hits.Length > 0)
                        {
                            bool any = false;
                            foreach (var h in f.WindowHit) any |= h;
                            if (!any) LastEvents.Add(new SimEvent { Attacker = i, Kind = EvKind.Whiff, MoveIndex = f.MoveIndex });
                        }
                        f.MoveIndex = -1;
                    }
                }
                if (!IsStunned(i))
                {
                    f.Stun = StunKind.None;
                    // apenas puede, sigue ejecutando lo que le quedaba
                    if (f.MoveIndex < 0 && f.QueueIndex < f.Queue.Count)
                        StartQueuedMove(i);
                }
                if (f.MoveIndex < 0)
                    f.Face = Fighters[1 - i].X >= f.X ? 1 : -1;
            }

            // desplazamiento
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                var m = CurrentMove(i);
                if (m == null || m.MotionEnd <= m.MotionStart) continue;
                int phase = Phase(i);
                if (phase < m.MotionStart || phase >= m.MotionEnd) continue;
                f.X += f.Face * m.MoveDx / (m.MotionEnd - m.MotionStart);
            }

            SeparateAndClamp();

            // spawn de proyectiles
            for (int i = 0; i < 2; i++)
            {
                var m = CurrentMove(i);
                if (m == null || m.SpawnFrame < 0 || Phase(i) != m.SpawnFrame) continue;
                if (HasProjectile(i)) continue; // uno por vez
                var f = Fighters[i];
                Projectiles.Add(new Projectile { Owner = i, X = f.X + f.Face * 0.7f, Dir = f.Face, Alive = true });
            }

            // proyectiles: mover, chocar entre sí, pegar
            for (int pi = 0; pi < Projectiles.Count; pi++)
            {
                var p = Projectiles[pi];
                if (!p.Alive) continue;
                p.X += p.Dir * SimConfig.ProjSpeed;
                if (Math.Abs(p.X) > SimConfig.StageHalfWidth + 1.2f) p.Alive = false;
                Projectiles[pi] = p;
            }
            for (int a = 0; a < Projectiles.Count; a++)
                for (int b = a + 1; b < Projectiles.Count; b++)
                {
                    if (!Projectiles[a].Alive || !Projectiles[b].Alive) continue;
                    if (Projectiles[a].Owner == Projectiles[b].Owner) continue;
                    if (!Projectiles[a].Rect.Overlaps(Projectiles[b].Rect)) continue;
                    var pa = Projectiles[a]; pa.Alive = false; Projectiles[a] = pa;
                    var pb = Projectiles[b]; pb.Alive = false; Projectiles[b] = pb;
                }
            for (int pi = 0; pi < Projectiles.Count; pi++)
            {
                var p = Projectiles[pi];
                if (!p.Alive) continue;
                int def = 1 - p.Owner;
                if (IsInvulnerable(def) || !p.Rect.Overlaps(HurtRect(def))) continue;
                ResolveContact(p.Owner, MoveCatalog.Hadouken,
                    SimConfig.ProjDamage, SimConfig.ProjHitstun, SimConfig.ProjBlockstun, SimConfig.ProjHitstun + 8,
                    SimConfig.ProjPush, knockdown: false, attackerFreeTick: AttackerFreeTick(p.Owner));
                p.Alive = false;
                Projectiles[pi] = p;
            }
            Projectiles.RemoveAll(x => !x.Alive);

            // golpes cuerpo a cuerpo
            for (int i = 0; i < 2; i++)
            {
                var atk = Fighters[i];
                var m = CurrentMove(i);
                if (m == null) continue;
                int phase = Phase(i);

                for (int wi = 0; wi < m.Hits.Length; wi++)
                {
                    var h = m.Hits[wi];
                    if (phase < h.Start || phase >= h.Start + h.Duration || atk.WindowHit[wi]) continue;
                    if (IsInvulnerable(1 - i)) continue; // pasa de largo, la ventana sigue viva
                    if (!HitRectWorld(i, h).Overlaps(HurtRect(1 - i))) continue;

                    atk.WindowHit[wi] = true;
                    ResolveContact(i, atk.MoveIndex, h.Damage, h.Hitstun, h.Blockstun, h.CounterStun, h.Push,
                        h.Knockdown, attackerFreeTick: atk.MoveStartTick + m.Total);
                }
            }

            SeparateAndClamp();

            if (Fighters[0].Hp <= 0f || Fighters[1].Hp <= 0f)
            {
                Over = true;
                bool dead0 = Fighters[0].Hp <= 0f, dead1 = Fighters[1].Hp <= 0f;
                Winner = dead0 && dead1 ? -1 : dead0 ? 1 : 0;
            }

            Tick++;
        }

        int AttackerFreeTick(int i)
        {
            var m = CurrentMove(i);
            return m == null ? Tick : Fighters[i].MoveStartTick + m.Total;
        }

        // Resolución común de contacto (golpe o proyectil) sobre el defensor.
        void ResolveContact(int attacker, int moveIndex, float damage, int hitstun, int blockstun, int counterStun,
            float push, bool knockdown, int attackerFreeTick)
        {
            int d = 1 - attacker;
            var def = Fighters[d];
            int face = Fighters[attacker].Face;

            if (IsBlockingState(d))
            {
                def.MoveIndex = -1; // el paso atrás / espera se corta en blockstun
                def.Stun = StunKind.Blockstun;
                def.StunEndTick = Tick + blockstun;
                def.X += face * push * 0.6f;
                LastEvents.Add(new SimEvent { Attacker = attacker, Kind = EvKind.Blocked, MoveIndex = moveIndex,
                    FrameAdv = def.StunEndTick - attackerFreeTick });
                return;
            }

            var defMove = CurrentMove(d);
            bool counter = defMove != null && defMove.IsAttack && Phase(d) < defMove.Startup;
            bool airHit = IsAirborne(d);
            bool kd = knockdown || airHit; // pegarle a alguien en el aire lo derriba
            float dmg = damage + (counter ? 1f : 0f);
            int stun = counter ? counterStun : hitstun;
            if (airHit) stun = Math.Max(stun, 60); // caída del aire = hard knockdown

            def.Hp = Math.Max(0f, def.Hp - dmg);
            def.X += face * push;
            def.MoveIndex = -1;
            def.Stun = kd ? StunKind.Knockdown : StunKind.Hitstun;
            def.StunEndTick = Tick + stun;

            LastEvents.Add(new SimEvent { Attacker = attacker, Kind = EvKind.Hit, Damage = dmg, Counter = counter,
                MoveIndex = moveIndex, FrameAdv = def.StunEndTick - attackerFreeTick });
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

    // Preview del plan: tu cola contra un rival que no mete inputs nuevos
    // (conserva su stun arrastrado; en neutral bloquea, como en el juego real).
    public class PlanPreview
    {
        public struct Snap
        {
            public float X;
            public List<WorldRect> HitRects;
        }

        public List<Snap> Snaps = new List<Snap>();
        public float DamageIfStill;
        public int BlockedCount;

        public static PlanPreview Build(MatchSim src, int fighter, List<int> plan)
        {
            var sim = src.Clone();
            sim.SetQueue(fighter, plan);
            sim.SetQueue(1 - fighter, new List<int>());
            var g = new PlanPreview();

            for (int t = 0; t < SimConfig.TurnFrames; t++)
            {
                var snap = new Snap { X = sim.Fighters[fighter].X, HitRects = new List<WorldRect>() };
                sim.GetActiveHitRects(fighter, snap.HitRects);
                sim.GetProjectileRects(fighter, snap.HitRects);
                g.Snaps.Add(snap);
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
