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

        public List<int> Plan(MatchSim sim, int me, int turnFrames)
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
                if (frames + move.Total > budget)
                {
                    // turno fluido: a veces compromete un move que cruza el
                    // límite (arranca dentro del turno, termina en el próximo)
                    if (SimConfig.CarryoverEnabled && _rng.NextDouble() < 0.45) plan.Add(pick);
                    break;
                }
                plan.Add(pick);
                frames += move.Total;
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
                case AIProfile.Zoner:
                    if (dist > 2.3f) return !threwFireball && r < 0.58 ? MoveCatalog.Hadouken : r < 0.78 ? MoveCatalog.WalkB : MoveCatalog.Parry;
                    if (dist > 1.35f) return r < 0.34 ? MoveCatalog.AttackB : r < 0.56 ? MoveCatalog.WalkB : r < 0.75 ? MoveCatalog.Hadouken : MoveCatalog.DashB;
                    return r < 0.34 ? MoveCatalog.AttackA : r < 0.57 ? MoveCatalog.Parry : r < 0.76 ? MoveCatalog.DashB : MoveCatalog.Shoryuken;

                case AIProfile.Aggressive:
                    if (dist > 2.2f) return r < 0.48 ? MoveCatalog.DashF : r < 0.78 ? MoveCatalog.JumpF : MoveCatalog.Tatsu;
                    if (dist > 1.3f) return r < 0.42 ? MoveCatalog.DashF : r < 0.72 ? MoveCatalog.AttackB : MoveCatalog.Tatsu;
                    return r < 0.35 ? MoveCatalog.AttackA : r < 0.60 ? MoveCatalog.Grab : r < 0.82 ? MoveCatalog.AttackB : MoveCatalog.Shoryuken;

                case AIProfile.Defensive:
                    if (dist > 2.3f) return r < 0.38 ? MoveCatalog.Hadouken : r < 0.72 ? MoveCatalog.WalkB : MoveCatalog.Parry;
                    if (dist > 1.35f) return r < 0.32 ? MoveCatalog.WalkB : r < 0.58 ? MoveCatalog.AttackB : r < 0.80 ? MoveCatalog.Parry : MoveCatalog.DashB;
                    return r < 0.34 ? MoveCatalog.Parry : r < 0.58 ? MoveCatalog.AttackA : r < 0.78 ? MoveCatalog.DashB : MoveCatalog.Shoryuken;

                case AIProfile.Trickster:
                    if (dist > 2.2f) return r < 0.25 ? MoveCatalog.Hadouken : r < 0.50 ? MoveCatalog.DashF : r < 0.75 ? MoveCatalog.JumpF : MoveCatalog.WalkB;
                    if (dist > 1.3f) return r < 0.22 ? MoveCatalog.DashB : r < 0.44 ? MoveCatalog.DashF : r < 0.65 ? MoveCatalog.Parry : r < 0.83 ? MoveCatalog.Tatsu : MoveCatalog.JumpN;
                    return r < 0.25 ? MoveCatalog.Grab : r < 0.46 ? MoveCatalog.Parry : r < 0.66 ? MoveCatalog.JumpN : r < 0.84 ? MoveCatalog.AttackA : MoveCatalog.Shoryuken;

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
                case 0: // proyectiles: saltarlos DE LEJOS, parriarlos, castigarlo de cerca
                    if (dist > 2.3f) return r < 0.50 ? MoveCatalog.JumpF : r < 0.78 ? MoveCatalog.Parry : MoveCatalog.DashF;
                    if (dist > 1.4f) return r < 0.42 ? MoveCatalog.Tatsu : r < 0.72 ? MoveCatalog.DashF : MoveCatalog.JumpF;
                    return r < 0.45 ? MoveCatalog.AttackA : r < 0.75 ? MoveCatalog.Grab : MoveCatalog.AttackB;
                case 1: // ataques: guardia/parry en rango, poke fuera de rango
                    if (dist > 2.0f) return r < 0.55 ? MoveCatalog.WalkB : MoveCatalog.Hadouken;
                    return r < 0.40 ? MoveCatalog.Parry : r < 0.72 ? MoveCatalog.WalkB : MoveCatalog.Shoryuken;
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

        // ---- Modo YOMI: planifica con las 7 cartas y el presupuesto de AP.
        // Sin perfiles finos todavía: juega el triángulo según la distancia y
        // castiga lo comprometido visible (salto rival → Golpe fuerte).
        public List<int> PlanYomi(MatchSim sim, int me, int turnFrames)
        {
            int opp = 1 - me;
            var plan = new List<int>();
            int budget = System.Math.Max(0, turnFrames - sim.StunRemaining(me) - sim.CommittedRemaining(me));
            int ap = sim.Fighters[me].Ap;
            if (Difficulty == AIDifficulty.Easy) ap = System.Math.Min(ap, SimConfig.ApPerTurn); // Easy no usa lo banqueado
            int frames = 0;
            float myX = sim.Fighters[me].X;
            float oppX = sim.Fighters[opp].X;
            int face = oppX >= myX ? 1 : -1;
            bool oppAir = sim.IsAirborne(opp); // turno fluido: el compromiso rival es info pública
            bool oppDown = sim.StunRemaining(opp) > 20;
            bool threwFireball = false;

            while (frames < budget)
            {
                float dist = System.Math.Abs(oppX - myX);
                int pick = PickYomi(dist, oppAir, oppDown, ap);
                oppAir = false; // solo la primera decisión reacciona al compromiso
                if (MoveCatalog.ApCost(pick) > ap) pick = MoveCatalog.WalkB;
                if (pick == MoveCatalog.Hadouken)
                {
                    if (threwFireball || HasProjectile(sim, me)) pick = dist > 1.5f ? MoveCatalog.DashF : MoveCatalog.WalkB;
                    else threwFireball = true;
                }

                var move = MoveCatalog.All[pick];
                plan.Add(pick);
                ap -= MoveCatalog.ApCost(pick);
                if (frames + move.Total > budget) break; // cruza el turno: comprometido y visible
                frames += move.Total;
                myX += face * move.MoveDx;
                myX = System.Math.Max(-SimConfig.StageHalfWidth, System.Math.Min(SimConfig.StageHalfWidth, myX));
                if (oppDown && plan.Count >= 2) oppDown = false;
                if (ap <= 0) break; // sin puntos: lo que quede del turno bloquea en neutral
            }
            return plan;
        }

        int PickYomi(float dist, bool oppAir, bool oppDown, int ap)
        {
            if (oppAir && dist < 2.2f) return MoveCatalog.Strong; // antiaéreo al compromiso visible
            if (oppDown && dist > 1.6f) return MoveCatalog.DashF;
            double r = _rng.NextDouble();
            if (dist > 2.4f)
                return r < 0.35 ? MoveCatalog.Hadouken : r < 0.60 ? MoveCatalog.DashF :
                       r < 0.80 ? MoveCatalog.JumpF : MoveCatalog.WalkB;
            if (dist > 1.3f)
                return r < 0.28 ? MoveCatalog.Strong : r < 0.50 ? MoveCatalog.DashF :
                       r < 0.64 ? MoveCatalog.JumpF : r < 0.86 ? MoveCatalog.WalkB : MoveCatalog.Hadouken;
            return r < 0.30 ? MoveCatalog.AttackA : r < 0.54 ? MoveCatalog.YomiGrab :
                   r < 0.68 ? MoveCatalog.Strong : r < 0.88 ? MoveCatalog.WalkB : MoveCatalog.DashB;
        }

        int PickUnfocused(float dist)
        {
            double r = _rng.NextDouble();
            if (dist > 2.4f) return r < 0.30 ? MoveCatalog.Hadouken : r < 0.56 ? MoveCatalog.DashF : r < 0.75 ? MoveCatalog.JumpF : r < 0.9 ? MoveCatalog.WalkB : MoveCatalog.Parry;
            if (dist > 1.4f) return r < 0.22 ? MoveCatalog.DashF : r < 0.43 ? MoveCatalog.AttackB : r < 0.60 ? MoveCatalog.WalkB : r < 0.75 ? MoveCatalog.Tatsu : r < 0.88 ? MoveCatalog.Parry : MoveCatalog.DashB;
            return r < 0.25 ? MoveCatalog.AttackA : r < 0.45 ? MoveCatalog.AttackB : r < 0.62 ? MoveCatalog.Grab : r < 0.76 ? MoveCatalog.Parry : r < 0.88 ? MoveCatalog.DashB : MoveCatalog.Shoryuken;
        }

        static bool HasProjectile(MatchSim sim, int owner)
        {
            foreach (var projectile in sim.Projectiles)
                if (projectile.Alive && projectile.Owner == owner) return true;
            return false;
        }
    }
}
