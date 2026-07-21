using System.Collections.Generic;

namespace LagFighter
{
    public enum AIProfile { Random, Zoner, Aggressive, Defensive, Trickster, Adaptive }
    public enum AIDifficulty { Easy, Normal, Hard }

    // Planifica a ciegas con la misma información y el mismo presupuesto que
    // el jugador. RANDOM resuelve un estilo al crear la partida y lo conserva.
    public class SimpleAI
    {
        readonly System.Random _rng;
        readonly int[] _observed = new int[5]; // proyectil, ataque, agarre, defensa, movilidad

        public AIProfile Profile { get; }
        public AIProfile ResolvedProfile { get; }
        public AIDifficulty Difficulty { get; }

        public SimpleAI(int seed, AIProfile profile = AIProfile.Random, AIDifficulty difficulty = AIDifficulty.Normal)
        {
            _rng = new System.Random(seed);
            Profile = profile;
            Difficulty = difficulty;
            ResolvedProfile = profile == AIProfile.Random
                ? (AIProfile)_rng.Next((int)AIProfile.Zoner, (int)AIProfile.Adaptive + 1)
                : profile;
        }

        public bool QuickRise()
        {
            double chance = ResolvedProfile == AIProfile.Trickster ? 0.48 :
                            ResolvedProfile == AIProfile.Defensive ? 0.78 : 0.65;
            if (Difficulty == AIDifficulty.Easy) chance = 0.82;
            return _rng.NextDouble() < chance;
        }

        // Reversal (1 por round): la IA lo guarda para cuando de verdad está
        // presionada — el controller ya verificó disponibilidad y AP.
        public bool UseReversal() => _rng.NextDouble() < (Difficulty == AIDifficulty.Hard ? 0.45 : 0.28);

        // Se llama una vez revelado el turno. Adaptive usa esto recién en el
        // turno siguiente, así que nunca lee el plan secreto que está armando.
        public void ObserveOpponentPlan(IEnumerable<int> plan)
        {
            foreach (int move in plan)
            {
                if (move == MoveCatalog.Hadouken) _observed[0]++;
                else if (move == MoveCatalog.AttackA || move == MoveCatalog.AttackB || move == MoveCatalog.Tatsu || move == MoveCatalog.Shoryuken || move == MoveCatalog.LowKick) _observed[1]++;
                else if (move == MoveCatalog.Grab) _observed[2]++;
                else if (move == MoveCatalog.WalkB || move == MoveCatalog.Parry || move == MoveCatalog.Crouch) _observed[3]++;
                else _observed[4]++;
            }
        }

        // apBudget: stock de AP disponible (economía del modo clásico). El
        // default infinito conserva el comportamiento legacy de tests viejos.
        public List<int> Plan(MatchSim sim, int me, int turnFrames, int apBudget = int.MaxValue)
        {
            int opp = 1 - me;
            var plan = new List<int>();
            int frames = 0;
            float myX = sim.Fighters[me].X;
            float oppX = sim.Fighters[opp].X;
            int face = oppX >= myX ? 1 : -1;
            bool oppDown = sim.StunRemaining(opp) > 20;
            bool threwFireball = false;
            int budget = System.Math.Max(0, turnFrames - sim.StunRemaining(me) - sim.CommittedRemaining(me));
            // el stock de AP también limita: pobre = turno corto (y se nota)
            if (SimConfig.ApActive && apBudget != int.MaxValue)
                budget = System.Math.Min(budget, System.Math.Max(0, apBudget) * SimConfig.FramesPerAp);

            // Easy deja huecos y toma más decisiones subóptimas. Hard aprovecha
            // todo el tiempo disponible y casi no se desvía de su perfil.
            if (Difficulty == AIDifficulty.Easy) budget = (int)(budget * 0.78f);

            bool usedSuper = false;
            while (frames < budget)
            {
                float dist = System.Math.Abs(oppX - myX);
                int pick = PickMove(sim, me, dist, oppDown, threwFireball);
                // barra llena: la IA tira la super a distancia de proyectil
                if (!usedSuper && sim.Fighters[me].Super >= SimConfig.SuperMax &&
                    dist > 1.6f && _rng.NextDouble() < 0.55)
                    pick = MoveCatalog.Super;
                if (pick == MoveCatalog.Super)
                {
                    if (usedSuper) pick = MoveCatalog.DashB;
                    else usedSuper = true;
                }
                double noise = Difficulty == AIDifficulty.Easy ? 0.34 : Difficulty == AIDifficulty.Normal ? 0.12 : 0.03;
                if (_rng.NextDouble() < noise) pick = PickUnfocused(dist);

                if (pick == MoveCatalog.Hadouken)
                {
                    if (threwFireball || HasProjectile(sim, me)) pick = dist > 1.5f ? MoveCatalog.DashF : MoveCatalog.WalkB;
                    else threwFireball = true;
                }
                if (!sim.MoveAllowed(me, pick)) pick = dist > 1.5f ? MoveCatalog.DashF : MoveCatalog.WalkB;

                var move = MoveCatalog.All[pick];
                // en modo AP cada move ocupa su slot entero: se presupuesta padded
                if (frames + move.PaddedTotal > budget)
                {
                    // turno fluido: a veces compromete un move que cruza el
                    // límite (arranca dentro del turno, termina en el próximo)
                    if (SimConfig.FluidTurn && _rng.NextDouble() < 0.45) plan.Add(pick);
                    break;
                }
                plan.Add(pick);
                frames += move.PaddedTotal;
                myX += face * move.MoveDx;
                myX = System.Math.Max(-SimConfig.StageHalfWidth, System.Math.Min(SimConfig.StageHalfWidth, myX));
                if (oppDown && plan.Count >= 3) oppDown = false;
            }
            return plan;
        }

        int PickMove(MatchSim sim, int me, float dist, bool oppDown, bool threwFireball)
        {
            if (Difficulty == AIDifficulty.Hard && sim.IsAirborne(1 - me) && dist < 1.45f)
                return MoveCatalog.Shoryuken;
            if (oppDown && dist > 1.65f) return MoveCatalog.DashF;

            double r = _rng.NextDouble();
            switch (ResolvedProfile)
            {
                // (2026-07-20) el Parry salió del modo clásico: Bloquear es la
                // única defensa (y banca AP). Los picks de parry pasan a
                // WalkB/DashB según el rol.
                case AIProfile.Zoner:
                    if (dist > 2.3f) return !threwFireball && r < 0.58 ? MoveCatalog.Hadouken : r < 0.78 ? MoveCatalog.WalkB : MoveCatalog.DashB;
                    if (dist > 1.35f) return r < 0.34 ? MoveCatalog.AttackB : r < 0.56 ? MoveCatalog.WalkB : r < 0.75 ? MoveCatalog.Hadouken : MoveCatalog.DashB;
                    return r < 0.34 ? MoveCatalog.AttackA : r < 0.57 ? MoveCatalog.WalkB : r < 0.76 ? MoveCatalog.DashB : MoveCatalog.Shoryuken;

                case AIProfile.Aggressive:
                    if (dist > 2.2f) return r < 0.48 ? MoveCatalog.DashF : r < 0.78 ? MoveCatalog.JumpF : MoveCatalog.Tatsu;
                    if (dist > 1.3f) return r < 0.42 ? MoveCatalog.DashF : r < 0.72 ? MoveCatalog.AttackB : MoveCatalog.Tatsu;
                    return r < 0.35 ? MoveCatalog.AttackA : r < 0.60 ? MoveCatalog.Grab : r < 0.82 ? MoveCatalog.AttackB : MoveCatalog.Shoryuken;

                case AIProfile.Defensive:
                    // re-agresivizado 2026-07-20 bis: los reemplazos del parry
                    // habían ido TODOS a WalkB y el espejo defensivo era 100%
                    // TIME OVER; ahora poke-a más en media y agarra en corta.
                    if (dist > 2.3f) return r < 0.38 ? MoveCatalog.Hadouken : r < 0.72 ? MoveCatalog.WalkB : MoveCatalog.DashB;
                    if (dist > 1.35f) return r < 0.32 ? MoveCatalog.WalkB : r < 0.62 ? MoveCatalog.AttackB : r < 0.80 ? MoveCatalog.WalkB : MoveCatalog.DashB;
                    return r < 0.30 ? MoveCatalog.WalkB : r < 0.52 ? MoveCatalog.AttackA : r < 0.66 ? MoveCatalog.Grab : r < 0.82 ? MoveCatalog.DashB : MoveCatalog.Shoryuken;

                case AIProfile.Trickster:
                    if (dist > 2.2f) return r < 0.25 ? MoveCatalog.Hadouken : r < 0.50 ? MoveCatalog.DashF : r < 0.75 ? MoveCatalog.JumpF : MoveCatalog.WalkB;
                    if (dist > 1.3f) return r < 0.22 ? MoveCatalog.DashB : r < 0.44 ? MoveCatalog.DashF : r < 0.65 ? MoveCatalog.WalkB : r < 0.83 ? MoveCatalog.Tatsu : MoveCatalog.JumpN;
                    return r < 0.25 ? MoveCatalog.Grab : r < 0.46 ? MoveCatalog.WalkB : r < 0.66 ? MoveCatalog.JumpN : r < 0.84 ? MoveCatalog.AttackA : MoveCatalog.Shoryuken;

                case AIProfile.Adaptive:
                    return PickAdaptive(dist, r);

                default:
                    return PickUnfocused(dist);
            }
        }

        // Counter-picks CONSCIENTES DE DISTANCIA: la versión anterior elegía
        // la respuesta correcta a la distancia equivocada (saltaba fireballs
        // desde el cuerpo a cuerpo) y perdía 89-15 contra Zoner en el lab.
        int PickAdaptive(float dist, double r)
        {
            int best = 0;
            for (int i = 1; i < _observed.Length; i++) if (_observed[i] > _observed[best]) best = i;
            if (_observed[best] == 0) return PickUnfocused(dist);
            switch (best)
            {
                case 0: // proyectiles: saltarlos DE LEJOS, bloquearlos, castigarlo de cerca
                    if (dist > 2.3f) return r < 0.50 ? MoveCatalog.JumpF : r < 0.78 ? MoveCatalog.WalkB : MoveCatalog.DashF;
                    if (dist > 1.4f) return r < 0.42 ? MoveCatalog.Tatsu : r < 0.72 ? MoveCatalog.DashF : MoveCatalog.JumpF;
                    return r < 0.45 ? MoveCatalog.AttackA : r < 0.75 ? MoveCatalog.Grab : MoveCatalog.AttackB;
                case 1: // ataques: guardia en rango (banca AP), poke fuera de rango
                    if (dist > 2.0f) return r < 0.55 ? MoveCatalog.WalkB : MoveCatalog.Hadouken;
                    return r < 0.40 ? MoveCatalog.WalkB : r < 0.72 ? MoveCatalog.WalkB : MoveCatalog.Shoryuken;
                case 2: // agarres: los saltos y el jab (más rápido) ganan
                    if (dist > 1.6f) return r < 0.55 ? MoveCatalog.AttackB : MoveCatalog.WalkB;
                    return r < 0.45 ? MoveCatalog.AttackA : r < 0.75 ? MoveCatalog.JumpN : MoveCatalog.DashB;
                case 3: // defensa: agarre, presión que chipea y avance
                    if (dist > 1.6f) return r < 0.60 ? MoveCatalog.DashF : MoveCatalog.AttackB;
                    return r < 0.50 ? MoveCatalog.Grab : r < 0.80 ? MoveCatalog.AttackB : MoveCatalog.DashF;
                default: // movilidad: pokes largos y control de espacio
                    if (dist > 2.2f) return r < 0.50 ? MoveCatalog.Hadouken : MoveCatalog.DashF;
                    return r < 0.55 ? MoveCatalog.AttackB : MoveCatalog.AttackA;
            }
        }

        // ---- Modo YOMI v2 (discreto): elige UNA acción por turno sobre
        // YomiSim. Base aleatoria ponderada por distancia y economía, más una
        // capa de counter-pick contra la acción más frecuente del rival en
        // esa distancia (los picks son públicos al revelarse). La agresividad
        // del counter-pick escala con la dificultad.
        readonly int[] _seenClose = new int[9];
        readonly int[] _seenFar = new int[9];

        public void ObserveYomi(YomiAction act, bool wasClose)
        {
            var seen = wasClose ? _seenClose : _seenFar;
            seen[(int)act]++;
        }

        public YomiAction PickYomi(YomiSim y, int me)
        {
            if (y.Recovery[me]) return YomiAction.Recovery;
            int opp = 1 - me;

            // counter-pick del hábito rival (Hard lee más seguido)
            double counterChance = Difficulty == AIDifficulty.Hard ? 0.45 :
                                   Difficulty == AIDifficulty.Easy ? 0.0 : 0.25;
            if (_rng.NextDouble() < counterChance)
            {
                var counter = CounterOfHabit(y);
                if (counter.HasValue && y.Legal(me, counter.Value)) return counter.Value;
            }

            // el rival en recovery es un regalo: pegale lo más caro que tengas
            if (y.Recovery[opp])
            {
                if (y.Close)
                {
                    if (y.Legal(me, YomiAction.Shoryu)) return YomiAction.Shoryu;
                    if (y.Legal(me, YomiAction.Kick)) return YomiAction.Kick;
                    if (y.Legal(me, YomiAction.Jab)) return YomiAction.Jab;
                }
                else
                {
                    if (y.Legal(me, YomiAction.Kick)) return YomiAction.Kick;
                    if (y.Legal(me, YomiAction.Dash)) return YomiAction.Dash;
                }
            }

            // base ponderada; pobre de AP → cargar más seguido
            double r = _rng.NextDouble();
            bool poor = y.Ap[me] <= 1;
            YomiAction pick;
            if (y.Close)
                pick = poor
                    ? (r < 0.40 ? YomiAction.Jab : r < 0.60 ? YomiAction.Parry : r < 0.80 ? YomiAction.Charge : YomiAction.Dash)
                    : r < 0.26 ? YomiAction.Jab
                    : r < 0.42 ? YomiAction.Grab
                    : r < 0.56 ? YomiAction.Parry
                    : r < 0.68 ? YomiAction.Kick
                    : r < 0.78 ? (y.Ap[me] >= 3 ? YomiAction.Shoryu : YomiAction.Jab)
                    : r < 0.86 ? YomiAction.Dash
                    : r < 0.93 ? YomiAction.Jump : YomiAction.Charge;
            else
                pick = poor
                    ? (r < 0.45 ? YomiAction.Charge : r < 0.75 ? YomiAction.Dash : YomiAction.Parry)
                    : r < 0.30 ? YomiAction.Kick
                    : r < 0.48 ? YomiAction.Jump
                    : r < 0.63 ? YomiAction.Dash
                    : r < 0.76 ? YomiAction.Charge
                    : r < 0.90 ? YomiAction.Parry
                    : (y.Ap[me] >= 3 ? YomiAction.Shoryu : YomiAction.Kick); // la lectura antiaérea
            if (!y.Legal(me, pick)) pick = y.Legal(me, YomiAction.Dash) ? YomiAction.Dash : YomiAction.Charge;
            return pick;
        }

        // La respuesta de la matriz a lo que el rival más repite en la
        // distancia actual (ver YomiSim: cada counter sale de una celda G).
        YomiAction? CounterOfHabit(YomiSim y)
        {
            var seen = y.Close ? _seenClose : _seenFar;
            int best = -1, bestN = 0;
            for (int i = 0; i < seen.Length; i++)
                if (seen[i] > bestN) { bestN = seen[i]; best = i; }
            if (best < 0 || bestN < 2) return null; // sin muestra no hay lectura
            var habit = (YomiAction)best;
            if (y.Close)
                switch (habit)
                {
                    case YomiAction.Jab: return YomiAction.Parry;
                    case YomiAction.Kick: return YomiAction.Jab;
                    case YomiAction.Grab: return YomiAction.Jab;
                    case YomiAction.Parry: return YomiAction.Grab;
                    case YomiAction.Shoryu: return YomiAction.Dash;
                    case YomiAction.Dash: return YomiAction.Kick;
                    case YomiAction.Jump: return YomiAction.Jab;
                    case YomiAction.Charge: return YomiAction.Kick;
                }
            else
                switch (habit)
                {
                    case YomiAction.Kick: return YomiAction.Jump;
                    case YomiAction.Jump: return YomiAction.Shoryu;   // la lectura antiaérea
                    case YomiAction.Dash: return YomiAction.Kick;
                    case YomiAction.Parry: return YomiAction.Dash;
                    case YomiAction.Charge: return YomiAction.Kick;
                    case YomiAction.Shoryu: return YomiAction.Charge; // dejalo whiffear
                }
            return null;
        }

        int PickUnfocused(float dist)
        {
            double r = _rng.NextDouble();
            if (dist > 2.4f) return r < 0.30 ? MoveCatalog.Hadouken : r < 0.56 ? MoveCatalog.DashF : r < 0.75 ? MoveCatalog.JumpF : MoveCatalog.WalkB;
            if (dist > 1.4f) return r < 0.22 ? MoveCatalog.DashF : r < 0.43 ? MoveCatalog.AttackB : r < 0.60 ? MoveCatalog.WalkB : r < 0.75 ? MoveCatalog.Tatsu : r < 0.88 ? MoveCatalog.WalkB : MoveCatalog.DashB;
            return r < 0.25 ? MoveCatalog.AttackA : r < 0.45 ? MoveCatalog.AttackB : r < 0.62 ? MoveCatalog.Grab : r < 0.76 ? MoveCatalog.WalkB : r < 0.88 ? MoveCatalog.DashB : MoveCatalog.Shoryuken;
        }

        static bool HasProjectile(MatchSim sim, int owner)
        {
            foreach (var projectile in sim.Projectiles)
                if (projectile.Alive && projectile.Owner == owner) return true;
            return false;
        }
    }
}
