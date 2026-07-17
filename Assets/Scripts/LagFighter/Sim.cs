using System;
using System.Collections.Generic;

namespace LagFighter
{
    // Simulación pura y determinista. Footsies por turnos programados:
    //  - 60 ticks/s = frames. Cada turno, ambos jugadores arman en pausa una
    //    cola de comandos de hasta TurnFrames (240 = 4s) y se ejecutan juntos.
    //  - 2D: posición en X sobre una línea (la puerta al 3D queda abierta:
    //    todo pasa por rects y no por la geometría del escenario).
    //  - Si te pegan, tu comando actual se cancela y la cola se desfasa;
    //    lo que no llegó a ejecutarse se pierde al final del turno, y el
    //    stun/knockdown arrastra desventaja de frames al turno siguiente.

    public static class SimConfig
    {
        public const int TicksPerSecond = 60;   // 1 tick = 1 frame
        public const float TickDuration = 1f / TicksPerSecond;
        public const int TurnFrames = 240;      // 4 segundos por turno
        public const int MaxHp = 6;
        public const float StageHalfWidth = 4.2f;
        public const float MinSeparation = 0.8f;
        public const float StartX = 2.0f;
        public const float HurtHalfWidth = 0.35f;
        public const float HurtHeight = 1.75f;
    }

    public enum AnimKind { Walk, Dash, AttackA, AttackB, Guard, Wait }

    public struct HitBoxDef
    {
        public int Start, Duration;      // frames desde el inicio del comando
        public float Fwd0, Fwd1, Y0, Y1; // rect local: hacia adelante y altura
        public float Damage;
        public int Hitstun, CounterStun; // frames de stun (counter: pegado en startup)
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
        public int Startup, Active, Recovery;   // framedata para mostrar
        public HitBoxDef[] Hits = Array.Empty<HitBoxDef>();
        public float MoveDx;                    // desplazamiento local (positivo = hacia el rival)
        public int MotionStart, MotionEnd;
        public int GuardStart, GuardEnd;        // ventana de bloqueo
        public int Total => Startup + Active + Recovery;
        public bool IsAttack => Hits.Length > 0;
        public bool IsGuard => GuardEnd > GuardStart;
        public float TotalDamage { get { float s = 0f; foreach (var h in Hits) s += h.Damage; return s; } }
    }

    // Arsenal mínimo estilo Footsies: el juego es distancia, whiff punish y
    // frame advantage, no la lista de movimientos.
    public static class MoveCatalog
    {
        public const int WalkF = 0, WalkB = 1, DashF = 2, DashB = 3,
                         AttackA = 4, AttackB = 5, Guard = 6, Wait = 7;

        public static readonly MoveDef[] All =
        {
            new MoveDef { Id = "walkF", Name = "Caminar +", Anim = AnimKind.Walk, Startup = 2, Active = 16, Recovery = 2,
                Desc = "Avanza un paso corto. Controlá la distancia.",
                MoveDx = 0.55f, MotionStart = 0, MotionEnd = 20 },

            new MoveDef { Id = "walkB", Name = "Caminar −", Anim = AnimKind.Walk, Startup = 2, Active = 16, Recovery = 2,
                Desc = "Retrocede un paso corto. Hacé whiffear y castigá.",
                MoveDx = -0.5f, MotionStart = 0, MotionEnd = 20 },

            new MoveDef { Id = "dashF", Name = "Dash +", Anim = AnimKind.Dash, Startup = 2, Active = 10, Recovery = 4,
                Desc = "Arremetida rápida hacia adelante. Para cerrar distancia de golpe.",
                MoveDx = 1.0f, MotionStart = 2, MotionEnd = 12 },

            new MoveDef { Id = "dashB", Name = "Dash −", Anim = AnimKind.Dash, Startup = 2, Active = 10, Recovery = 4,
                Desc = "Salto atrás rápido. El corazón del bait.",
                MoveDx = -1.0f, MotionStart = 2, MotionEnd = 12 },

            new MoveDef { Id = "atkA", Name = "Golpe A", Anim = AnimKind.AttackA, Startup = 8, Active = 4, Recovery = 18,
                Desc = "Rápido y corto. 1 de daño. Counter si lo pegás en el startup ajeno.",
                Hits = new[] { new HitBoxDef { Start = 8, Duration = 4, Fwd0 = 0.45f, Fwd1 = 1.1f, Y0 = 1.0f, Y1 = 1.5f,
                    Damage = 1f, Hitstun = 24, CounterStun = 36, Push = 0.35f } } },

            new MoveDef { Id = "atkB", Name = "Patada B", Anim = AnimKind.AttackB, Startup = 16, Active = 6, Recovery = 30,
                Desc = "Lenta, larga, 2 de daño y DERRIBA. Castigo grande, riesgo grande.",
                Hits = new[] { new HitBoxDef { Start = 16, Duration = 6, Fwd0 = 0.5f, Fwd1 = 1.6f, Y0 = 0.5f, Y1 = 1.2f,
                    Damage = 2f, Hitstun = 50, CounterStun = 65, Push = 0.55f, Knockdown = true } } },

            new MoveDef { Id = "guard", Name = "Guardia", Anim = AnimKind.Guard, Startup = 2, Active = 28, Recovery = 6,
                Desc = "Bloquea todo durante la ventana. La cola final es punisheable.",
                GuardStart = 2, GuardEnd = 30 },

            new MoveDef { Id = "wait", Name = "Esperar", Anim = AnimKind.Wait, Startup = 0, Active = 12, Recovery = 0,
                Desc = "12 frames de nada. El timing lo es todo." },
        };
    }

    public class FighterState
    {
        public float Hp = SimConfig.MaxHp;
        public float X;
        public int Face = 1; // +1 mira a la derecha
        public List<int> Queue = new List<int>();
        public int QueueIndex;
        public int MoveIndex = -1;
        public int MoveStartTick;
        public bool[] WindowHit = Array.Empty<bool>();
        public int StunEndTick;
        public bool Down; // knockdown (visual + dura más)

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
    }

    public class MatchSim
    {
        public FighterState[] Fighters = { new FighterState(), new FighterState() };
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
                Tick = Tick,
                Over = Over,
                Winner = Winner,
            };
        }

        public MoveDef CurrentMove(int i) => Fighters[i].MoveIndex < 0 ? null : MoveCatalog.All[Fighters[i].MoveIndex];
        public int Phase(int i) => Tick - Fighters[i].MoveStartTick;
        public bool IsStunned(int i) => Tick < Fighters[i].StunEndTick;
        public int StunRemaining(int i) => Math.Max(0, Fighters[i].StunEndTick - Tick);

        public void SetQueue(int i, IEnumerable<int> moves)
        {
            Fighters[i].Queue = new List<int>(moves);
            Fighters[i].QueueIndex = 0;
        }

        // Fin de turno: lo no ejecutado se pierde; el stun se arrastra.
        // Devuelve cuántas órdenes perdió cada uno (para feedback).
        public int OnTurnEnd(int i)
        {
            var f = Fighters[i];
            int lost = f.Queue.Count - f.QueueIndex + (f.MoveIndex >= 0 ? 1 : 0);
            f.MoveIndex = -1;
            f.Queue.Clear();
            f.QueueIndex = 0;
            return lost;
        }

        public bool IsGuarding(int i)
        {
            var m = CurrentMove(i);
            if (m == null || !m.IsGuard) return false;
            int p = Phase(i);
            return p >= m.GuardStart && p < m.GuardEnd;
        }

        public WorldRect HurtRect(int i)
        {
            var f = Fighters[i];
            float h = f.Down ? 0.55f : SimConfig.HurtHeight; // caído: hurtbox baja (para el futuro okizeme)
            return new WorldRect { X0 = f.X - SimConfig.HurtHalfWidth, X1 = f.X + SimConfig.HurtHalfWidth, Y0 = 0f, Y1 = h };
        }

        public WorldRect HitRectWorld(int i, HitBoxDef h)
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

            // fin de comandos (+ whiff), fin de knockdown, arranque del siguiente
            for (int i = 0; i < 2; i++)
            {
                var f = Fighters[i];
                if (f.MoveIndex >= 0)
                {
                    var m = MoveCatalog.All[f.MoveIndex];
                    if (Tick - f.MoveStartTick >= m.Total)
                    {
                        if (m.IsAttack)
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
                    f.Down = false;
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

            // golpes
            for (int i = 0; i < 2; i++)
            {
                var atk = Fighters[i];
                var m = CurrentMove(i);
                if (m == null) continue;
                int phase = Phase(i);
                var def = Fighters[1 - i];

                for (int wi = 0; wi < m.Hits.Length; wi++)
                {
                    var h = m.Hits[wi];
                    if (phase < h.Start || phase >= h.Start + h.Duration || atk.WindowHit[wi]) continue;
                    if (!HitRectWorld(i, h).Overlaps(HurtRect(1 - i))) continue;

                    atk.WindowHit[wi] = true;

                    if (IsGuarding(1 - i))
                    {
                        def.X += atk.Face * h.Push * 0.6f;
                        LastEvents.Add(new SimEvent { Attacker = i, Kind = EvKind.Blocked, MoveIndex = atk.MoveIndex });
                        continue;
                    }

                    var defMove = CurrentMove(1 - i);
                    bool counter = defMove != null && defMove.IsAttack && Phase(1 - i) < defMove.Startup;
                    float dmg = h.Damage + (counter ? 1f : 0f);

                    def.Hp = Math.Max(0f, def.Hp - dmg);
                    def.X += atk.Face * h.Push;
                    def.MoveIndex = -1; // el comando actual se cancela; la cola queda desfasada
                    def.StunEndTick = Tick + (counter ? h.CounterStun : h.Hitstun);
                    def.Down = h.Knockdown;

                    LastEvents.Add(new SimEvent { Attacker = i, Kind = EvKind.Hit, Damage = dmg, Counter = counter, MoveIndex = atk.MoveIndex });
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

        void SeparateAndClamp()
        {
            float d = Fighters[1].X - Fighters[0].X;
            float abs = Math.Abs(d);
            if (abs < SimConfig.MinSeparation)
            {
                float push = (SimConfig.MinSeparation - abs) * 0.5f * (d >= 0f ? 1f : -1f);
                Fighters[0].X -= push;
                Fighters[1].X += push;
            }
            for (int i = 0; i < 2; i++)
                Fighters[i].X = Math.Max(-SimConfig.StageHalfWidth, Math.Min(SimConfig.StageHalfWidth, Fighters[i].X));
        }
    }

    // Preview del plan: simula TU cola contra un rival que no mete inputs
    // nuevos (pero conserva su stun/knockdown arrastrado). Muestra trayectoria,
    // hitboxes y qué pasaría si el rival se queda quieto.
    public class PlanPreview
    {
        public struct Snap
        {
            public float X;
            public List<WorldRect> HitRects;
        }

        public List<Snap> Snaps = new List<Snap>();
        public float DamageIfStill;

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
                g.Snaps.Add(snap);
                sim.Step();
                foreach (var ev in sim.LastEvents)
                    if (ev.Attacker == fighter && ev.Kind == EvKind.Hit) g.DamageIfStill += ev.Damage;
                if (sim.Over) break;
            }
            return g;
        }
    }
}
