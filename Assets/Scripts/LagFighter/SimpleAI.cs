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

        // ---- MODO CARTAS v2 (copia completa de Yomi 2): main phase entera,
        // opener (con supers), combos, pumps y castigo ----

        readonly int[] _seenCardKind = new int[5]; // attack/throw/block/dodge/ability revelados del rival

        public void ObserveCard(CardKind kind) => _seenCardKind[(int)kind]++;

        // Main phase completa del turno propio: ability, power up y exchanges.
        public void DoCardMainPhase(CardSim s)
        {
            int me = s.Active;

            // 1) ability: Grave espera tener con qué aprovechar el viento;
            //    Jaina tira el arco apenas puede presionar
            int abIdx = s.Hand[me].IndexOf(CardCatalog.Ability);
            if (abIdx >= 0 && s.CanPlayAbility(abIdx))
            {
                bool grave = s.CharIdx[me] == CardCatalog.GraveIdx;
                double chance = grave
                    ? (s.Meter[me] >= 2 || s.Hand[me].Contains(CardCatalog.Super1) ? 0.6 : 0.15)
                    : 0.5;
                if (_rng.NextDouble() < chance) s.PlayAbility(abIdx);
            }

            // 2) power up: descartar un par que no duela; fetch de super si
            //    hay una esperando en el descarte
            TryPowerUp(s, me);

            // 3) exchanges: blocks que falten, después agarre, después golpes
            while (s.ExchangesLeft > 0)
            {
                int want = -1;
                if (!s.Hand[me].Contains(CardCatalog.LowBlock))
                    want = s.Discard[me].IndexOf(CardCatalog.LowBlock);
                if (want < 0 && !s.Hand[me].Contains(CardCatalog.HighBlock))
                    want = s.Discard[me].IndexOf(CardCatalog.HighBlock);
                if (want < 0 && !s.Hand[me].Contains(CardCatalog.Throw))
                    want = s.Discard[me].IndexOf(CardCatalog.Throw);
                if (want < 0 && !HasAttackInHand(s, me))
                {
                    int bestDmg = 0;
                    for (int d = 0; d < s.Discard[me].Count; d++)
                    {
                        var def = s.Def(me, s.Discard[me][d]);
                        if (def.Kind == CardKind.Attack && def.IsNormal && def.Damage > bestDmg)
                        { bestDmg = def.Damage; want = d; }
                    }
                }
                if (want < 0) return;
                int give = CardToGive(s, me);
                if (give < 0 || !s.Exchange(give, want)) return;
            }
        }

        void TryPowerUp(CardSim s, int me)
        {
            if (s.PowerUpUsed || s.Meter[me] >= CardConfig.MeterCap) return;
            // buscar un par "quemable": C, esquives sobrantes, D, E
            int[] burnable = { CardCatalog.AttackC, CardCatalog.Dodge, CardCatalog.AttackD, CardCatalog.AttackE };
            foreach (int card in burnable)
            {
                int a = -1, b = -1;
                for (int i = 0; i < s.Hand[me].Count; i++)
                    if (s.Hand[me][i] == card) { if (a < 0) a = i; else { b = i; break; } }
                if (b < 0) continue;
                if (card == CardCatalog.Dodge && _rng.NextDouble() < 0.5) continue;
                bool fetch = false; int super = -1;
                int s1 = s.Discard[me].Contains(CardCatalog.Super1) ? CardCatalog.Super1 : -1;
                int s2 = s.Discard[me].Contains(CardCatalog.Super2) ? CardCatalog.Super2 : -1;
                if (!s.Hand[me].Contains(CardCatalog.Super1) && s1 >= 0) { fetch = true; super = s1; }
                else if (!s.Hand[me].Contains(CardCatalog.Super2) && s2 >= 0 && _rng.NextDouble() < 0.4) { fetch = true; super = s2; }
                s.PowerUp(a, b, fetch, super);
                return;
            }
        }

        static bool HasAttackInHand(CardSim s, int me)
        {
            foreach (int c in s.Hand[me])
                if (s.Def(me, c).Kind == CardKind.Attack) return true;
            return false;
        }

        // Elige el opener (índice de MANO). Lee el estado como un jugador:
        // rival derribado = mixup fuerte, yo derribado = bloquear o reversal,
        // supers cuando pagan, y una lectura del hábito según dificultad.
        public int PickCardOpener(CardSim s, int me)
        {
            var hand = s.Hand[me];
            int opp = 1 - me;
            int atkFast = -1, atkStrong = -1, thr = -1, dodge = -1,
                blockHigh = -1, blockLow = -1, proj = -1, reversal = -1,
                super1 = -1, super2 = -1;
            for (int i = 0; i < hand.Count; i++)
            {
                var d = s.Def(me, hand[i]);
                if (d.IsSuper)
                {
                    if (s.Meter[me] < d.SuperCost) continue; // impagable: ni mirarla
                    if (hand[i] == CardCatalog.Super1) super1 = i;
                    else if (d.Kind != CardKind.Dodge || !s.KnockedDown[me]) super2 = i;
                    continue;
                }
                switch (d.Kind)
                {
                    case CardKind.Attack:
                        if (hand[i] == CardCatalog.SpecialY) reversal = i;
                        if (d.Projectile) proj = i;
                        if (atkFast < 0 || d.Speed > s.Def(me, hand[atkFast]).Speed) atkFast = i;
                        if (atkStrong < 0 || d.Damage > s.Def(me, hand[atkStrong]).Damage) atkStrong = i;
                        break;
                    case CardKind.Throw: thr = i; break;
                    case CardKind.Dodge: if (!s.KnockedDown[me]) dodge = i; break;
                    case CardKind.Block:
                        if (hand[i] == CardCatalog.HighBlock) blockHigh = i; else blockLow = i;
                        break;
                }
            }
            int AnyBlock() => _rng.NextDouble() < 0.5
                ? (blockHigh >= 0 ? blockHigh : blockLow)
                : (blockLow >= 0 ? blockLow : blockHigh);

            double r = _rng.NextDouble();

            // el ARCO rival castiga los ataques (7 al abrir): agarre, bloqueo
            // o esquive hasta que se apague — como haría cualquier humano
            if (s.ArcActive(opp) && _rng.NextDouble() < 0.8)
            {
                if (thr >= 0 && r < 0.45) return thr;
                if (dodge >= 0 && r < 0.6) return dodge;
                int blkArc = AnyBlock();
                if (blkArc >= 0) return blkArc;
                if (thr >= 0) return thr;
                if (dodge >= 0) return dodge;
            }

            // rival derribado: mixup fuerte — el momento de la super de ataque
            if (s.KnockedDown[opp])
            {
                if (super1 >= 0 && s.Def(me, hand[super1]).Kind == CardKind.Attack && r < 0.45) return super1;
                if (r < 0.55 && atkStrong >= 0) return atkStrong;
                if (r < 0.85 && thr >= 0) return thr;
                if (atkFast >= 0) return atkFast;
            }
            // derribado yo: bloquear adivinando altura, o el reversal
            if (s.KnockedDown[me])
            {
                if (r < 0.20 && super1 >= 0 && s.Def(me, hand[super1]).Speed >= 10) return super1;
                if (r < 0.35 && reversal >= 0) return reversal;
                int blk = AnyBlock();
                if (blk >= 0) return blk;
            }

            // lectura del hábito rival (Hard lee más seguido)
            double counterChance = Difficulty == AIDifficulty.Hard ? 0.40 :
                                   Difficulty == AIDifficulty.Easy ? 0.0 : 0.20;
            if (_rng.NextDouble() < counterChance)
            {
                int habit = CardHabit();
                if (habit == (int)CardKind.Block && thr >= 0) return thr;
                if (habit == (int)CardKind.Throw && atkFast >= 0) return atkFast;
                if (habit == (int)CardKind.Attack)
                {
                    // la super dodge de Grave es LA lectura anti-strike
                    if (super2 >= 0 && s.Def(me, hand[super2]).DodgeCounter > 0 && _rng.NextDouble() < 0.4) return super2;
                    if (dodge >= 0 && hand.Count >= 6 && _rng.NextDouble() < 0.5) return dodge;
                    int blk = AnyBlock();
                    if (blk >= 0) return blk;
                }
                if (habit == (int)CardKind.Dodge && thr >= 0) return thr;
            }

            // mezcla base: pega, agarra, bloquea, zonea; supers cuando sobra meter
            if (r < 0.10 && super2 >= 0 && s.Def(me, hand[super2]).Kind == CardKind.Attack) return super2;
            if (r < 0.22 && atkFast >= 0) return atkFast;
            if (r < 0.36 && atkStrong >= 0) return atkStrong;
            if (r < 0.48 && proj >= 0) return proj;
            if (r < 0.64 && thr >= 0) return thr;
            if (r < 0.86) { int blk = AnyBlock(); if (blk >= 0) return blk; }
            if (dodge >= 0 && hand.Count >= 5) return dodge;
            if (atkFast >= 0) return atkFast;
            if (thr >= 0) return thr;

            for (int i = 0; i < hand.Count; i++) if (s.LegalOpener(me, i)) return i;
            return hand.Count > 0 ? 0 : -1; // sin legales: que dispare el wild swing
        }

        int CardHabit()
        {
            int best = -1, bestN = 0;
            for (int i = 0; i < _seenCardKind.Length; i++)
                if (_seenCardKind[i] > bestN) { bestN = _seenCardKind[i]; best = i; }
            return bestN >= 3 ? best : -1;
        }

        // Resuelve TODO el followup pendiente: castigo, combo y pumps.
        public void DoCardFollowup(CardSim s)
        {
            int guard = 0;
            while (s.AwaitingFollowup && guard++ < 24)
            {
                int me = s.FollowSide;
                if (s.FollowIsHitBack)
                {
                    if (!s.HitBackPlayed)
                    {
                        int best = -1, bestDmg = -1;
                        for (int i = 0; i < s.Hand[me].Count; i++)
                        {
                            var d = s.Def(me, s.Hand[me][i]);
                            if (d.Kind != CardKind.Attack && d.Kind != CardKind.Throw) continue;
                            if (d.IsSuper && s.Meter[me] < d.SuperCost) continue;
                            int dmg = d.Damage + (d.KnockdownOnHit ? 3 : 0); // el KD vale
                            if (dmg > bestDmg) { bestDmg = dmg; best = i; }
                        }
                        s.HitBack(best);
                    }
                    else if (s.CanPumpLast()) s.PumpLast(9);
                    else s.FollowupEnd();
                    continue;
                }

                // combo: ¿parar acá para conservar el knockdown del agarre?
                var last = s.Def(me, s.LastPlayed);
                if (last.KnockdownOnHit && _rng.NextDouble() < 0.45)
                {
                    s.FollowupEnd();
                    continue;
                }
                var opts = s.ComboOptions(me);
                if (opts.Count == 0)
                {
                    if (s.CanPumpLast()) { s.PumpLast(9); continue; }
                    s.FollowupEnd();
                    continue;
                }
                // puntuar cada continuación: daño + paso de cadena (meter) + super
                int bestIdx = -1; double bestScore = -1;
                foreach (int i in opts)
                {
                    var d = s.Def(me, s.Hand[me][i]);
                    double score = d.Damage;
                    if (last.Combo == ComboType.Chain && d.Combo == ComboType.Chain &&
                        d.ChainLetter == last.ChainLetter + 1) score += 3;   // +1 meter
                    if (d.IsSuper) score += 5;
                    if (d.SelfDamage > 0 && s.Hp[me] <= 20) score -= 6;      // no suicidarse
                    if (score > bestScore) { bestScore = score; bestIdx = i; }
                }
                if (bestIdx < 0 || !s.ComboAdd(bestIdx)) s.FollowupEnd();
                else if (s.AwaitingFollowup && s.CanPumpLast() && _rng.NextDouble() < 0.7) s.PumpLast(9);
            }
        }

        // Qué soltar en el exchange: un esquive sobrante o la normal repetida más débil.
        int CardToGive(CardSim s, int me)
        {
            var hand = s.Hand[me];
            int dodges = 0, firstDodge = -1;
            for (int i = 0; i < hand.Count; i++)
                if (hand[i] == CardCatalog.Dodge) { dodges++; if (firstDodge < 0) firstDodge = i; }
            if (dodges >= 2) return firstDodge;
            int give = -1, giveDmg = 99;
            for (int i = 0; i < hand.Count; i++)
            {
                var d = s.Def(me, hand[i]);
                if (!d.IsNormal || d.Kind != CardKind.Attack) continue;
                bool dup = false;
                for (int j = 0; j < hand.Count; j++) if (j != i && hand[j] == hand[i]) dup = true;
                if (dup && d.Damage < giveDmg) { giveDmg = d.Damage; give = i; }
            }
            if (give >= 0) return give;
            return dodges > 0 ? firstDodge : -1;
        }

        // ---- MODO DUELO (el núcleo casual): una carta secreta por turno,
        // premio del ganador y castigo del defensor. Ver DUELO.md ----

        readonly int[] _seenDuelKind = new int[4];    // strike/grab/guard/escape del rival
        readonly int[] _seenDuelHeight = new int[2];  // alto/bajo de sus golpes

        // Apagable para medir el VALOR DE LA INFORMACIÓN en el lab: con esto
        // en false la IA juega el mismo mix pero sin leer al rival.
        public bool ReadsHabits = true;

        public void ObserveDuel(in DuelCard card)
        {
            _seenDuelKind[(int)card.Kind]++;
            if (card.Kind == DuelKind.Strike && card.Height != DuelHeight.None)
                _seenDuelHeight[(int)card.Height]++;
        }

        // Elige la carta (índice de MANO). Lee lo público como un jugador:
        // derribos, vida, tamaño de mano y el hábito revelado del rival.
        public int PickDuelCard(DuelSim s, int me)
        {
            var hand = s.Hand[me];
            if (hand.Count == 0) return -1;
            int opp = 1 - me;
            int fast = -1, strong = -1, high = -1, low = -1, grab = -1,
                gHigh = -1, gLow = -1, escape = -1;
            for (int i = 0; i < hand.Count; i++)
            {
                var d = s.Def(me, hand[i]);
                switch (d.Kind)
                {
                    case DuelKind.Strike:
                        if (fast < 0 || d.Speed > s.Def(me, hand[fast]).Speed) fast = i;
                        if (strong < 0 || d.Damage > s.Def(me, hand[strong]).Damage) strong = i;
                        if (d.Height == DuelHeight.High) high = i; else low = i;
                        break;
                    // (los muy lentos se filtran abajo: pegan un montón pero
                    // pierden TODA carrera de velocidad — medido con el Golem)
                    case DuelKind.Grab: grab = i; break;
                    case DuelKind.Guard:
                        if (d.Height == DuelHeight.High) gHigh = i; else gLow = i;
                        break;
                    case DuelKind.Escape: escape = i; break;
                }
            }

            // ¿qué altura defiendo? Solo vale leer si el rival tiene un SESGO
            // real: saber que ataca mucho no dice nada (la altura es la
            // adivinanza); saber que ataca ABAJO, sí.
            int Guard()
            {
                if (gHigh < 0) return gLow;
                if (gLow < 0) return gHigh;
                int skew = HeightSkew();
                if (skew > 0) return gHigh;
                if (skew < 0) return gLow;
                return _rng.NextDouble() < 0.5 ? gHigh : gLow;
            }
            int AnyStrike() => fast >= 0 ? fast : strong;
            // Un golpe muy lento es una LECTURA (le gana a agarres y a la
            // guardia equivocada), no una jugada de default: si el más fuerte
            // de la mano es lentísimo, la mitad de las veces se juega el
            // rápido. Sin esto la IA regalaba las carreras con el Cabezazo.
            if (strong >= 0 && fast >= 0 && s.Def(me, hand[strong]).Speed <= 4 &&
                _rng.NextDouble() < 0.5) strong = fast;
            double r = _rng.NextDouble();

            // DERRIBADO: mi guardia no bloquea este turno. El escape es la
            // válvula; si no está, hay que pelear (el golpe le gana al agarre).
            if (s.KnockedDown[me])
            {
                if (escape >= 0 && (s.Hp[me] <= DuelConfig.MaxHp / 3 || r < 0.55)) return escape;
                if (r < 0.65 && fast >= 0) return fast;
                if (r < 0.85 && strong >= 0) return strong;
                if (grab >= 0) return grab;
                return AnyStrike() >= 0 ? AnyStrike() : 0;
            }

            // RIVAL DERRIBADO: no puede defender → el agarre pierde valor y el
            // golpe rápido gana casi toda carrera. Es el momento del daño.
            if (s.KnockedDown[opp])
            {
                if (r < 0.55 && fast >= 0) return fast;
                if (strong >= 0) return strong;
                if (grab >= 0) return grab;
            }

            // LECTURA DEL DESCARTE (público, Ley 5): las guardias que están en
            // su descarte NO están en su mano. Si se le fueron todas las
            // altas, pegar ALTO es gratis; si se le fueron las dos alturas,
            // no puede defender y el golpe rápido es el rey.
            if (ReadsHabits)
            {
                int cupo = s.Chr[opp].DeckCounts[DuelCatalog.GuardHigh];
                bool sinAlta = GuardiasEnDescarte(s, opp, DuelCatalog.GuardHigh) >= cupo;
                bool sinBaja = GuardiasEnDescarte(s, opp, DuelCatalog.GuardLow) >= cupo;
                if (sinAlta && sinBaja)
                {
                    if (r < 0.70 && fast >= 0) return fast;
                    if (strong >= 0) return strong;
                }
                else if (sinAlta && high >= 0 && _rng.NextDouble() < 0.75) return high;
                else if (sinBaja && low >= 0 && _rng.NextDouble() < 0.75) return low;
            }

            // lectura del hábito (Hard lee más seguido)
            double counterChance = Difficulty == AIDifficulty.Hard ? 0.45 :
                                   Difficulty == AIDifficulty.Easy ? 0.0 : 0.25;
            if (ReadsHabits && _rng.NextDouble() < counterChance)
            {
                int habit = DuelHabit();
                if (habit == (int)DuelKind.Guard && grab >= 0) return grab;
                if (habit == (int)DuelKind.Grab && fast >= 0) return fast;
                // "ataca mucho" NO es accionable: medido en el lab, defender
                // MÁS seguido por esa lectura es una jugada perdedora (el
                // agarre castiga al que se queda). La lectura de altura vale
                // para elegir QUÉ guardia, no para guardar más veces — eso ya
                // lo hace Guard() en la mezcla base.
            }

            // rematar: si el rival está a tiro, el golpe fuerte paga más
            if (strong >= 0 && s.Hp[opp] <= s.Def(me, hand[strong]).Damage) return strong;

            // mezcla base: pega rápido, pega fuerte, agarra, defiende
            if (r < 0.20 && fast >= 0) return fast;
            if (r < 0.34 && strong >= 0) return strong;
            if (r < 0.44) { int alt = _rng.NextDouble() < 0.5 ? high : low; if (alt >= 0) return alt; }
            if (r < 0.62 && grab >= 0) return grab;
            if (r < 0.90) { int g = Guard(); if (g >= 0) return g; }
            if (fast >= 0) return fast;
            if (grab >= 0) return grab;
            int guardFallback = Guard();
            return guardFallback >= 0 ? guardFallback : 0;
        }

        // +1 = le pega ALTO seguido · −1 = ABAJO · 0 = no hay sesgo legible.
        // Pide muestra Y mayoría clara: media docena de golpes 60/40 no es
        // un hábito, es ruido.
        int HeightSkew()
        {
            if (!ReadsHabits) return 0;
            int hi = _seenDuelHeight[(int)DuelHeight.High], lo = _seenDuelHeight[(int)DuelHeight.Low];
            int n = hi + lo;
            if (n < 3) return 0;
            if (hi * 100 >= n * 65) return 1;
            if (lo * 100 >= n * 65) return -1;
            return 0;
        }

        static int GuardiasEnDescarte(DuelSim s, int side, int card)
        {
            int n = 0;
            foreach (int c in s.Discard[side]) if (c == card) n++;
            return n;
        }

        int DuelHabit()
        {
            int best = -1, bestN = 0;
            for (int i = 0; i < _seenDuelKind.Length; i++)
                if (_seenDuelKind[i] > bestN) { bestN = _seenDuelKind[i]; best = i; }
            return bestN >= 3 ? best : -1;
        }

        // Cierra la decisión pendiente: el premio del ganador (+DAÑO vs
        // DERRIBO — el tradeoff de la Ley 12) o el castigo del defensor.
        public void DoDuelChoice(DuelSim s)
        {
            int me = s.PendingSide;
            if (me < 0) return;
            if (s.PendingIsPunish) { s.Punish(BestAttack(s, me)); return; }

            var fuel = s.PrizeFuel(me);
            if (fuel.Count == 0) { s.ChoosePrize(DuelPrize.Knockdown); return; }
            int best = -1, bestDmg = -1;
            foreach (int i in fuel)
            {
                int dmg = s.Def(me, s.Hand[me][i]).Damage;
                if (dmg > bestDmg) { bestDmg = dmg; best = i; }
            }
            int opp = 1 - me;
            // el daño cierra la partida → sin dudarlo; si no, quemar carta solo
            // vale con mano gorda y combustible que pague
            bool lethal = s.Hp[opp] <= bestDmg;
            bool worth = s.Hand[me].Count >= 5 && bestDmg >= 5;
            if (lethal || worth) s.ChoosePrize(DuelPrize.Damage, best);
            else s.ChoosePrize(DuelPrize.Knockdown);
        }

        static int BestAttack(DuelSim s, int me)
        {
            int best = -1, bestDmg = -1;
            for (int i = 0; i < s.Hand[me].Count; i++)
            {
                var d = s.Def(me, s.Hand[me][i]);
                if (!d.IsAttack) continue;
                if (d.Damage > bestDmg) { bestDmg = d.Damage; best = i; }
            }
            return best;
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
