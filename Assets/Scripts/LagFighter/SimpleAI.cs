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

        // ---- MODO CARTAS (copia de Yomi 2): opener, exchange y castigo ----

        readonly int[] _seenCardKind = new int[4]; // attack/throw/block/dodge revelados del rival

        public void ObserveCard(int cardId) =>
            _seenCardKind[(int)CardCatalog.All[cardId].Kind]++;

        // Elige el opener (índice de MANO). Lee el estado como un jugador:
        // rival derribado = mixup fuerte, yo derribado = bloquear o reversal,
        // y una lectura del hábito rival según dificultad.
        public int PickCardOpener(CardSim s, int me)
        {
            var hand = s.Hand[me];
            int opp = 1 - me;
            int atkFast = -1, atkStrong = -1, thr = -1, dodge = -1,
                blockHigh = -1, blockLow = -1, proj = -1, reversal = -1;
            for (int i = 0; i < hand.Count; i++)
            {
                var d = CardCatalog.All[hand[i]];
                switch (d.Kind)
                {
                    case CardKind.Attack:
                        if (hand[i] == CardCatalog.SpecialY) reversal = i;
                        if (d.Projectile) proj = i;
                        if (atkFast < 0 || d.Speed > CardCatalog.All[hand[atkFast]].Speed) atkFast = i;
                        if (atkStrong < 0 || d.Damage > CardCatalog.All[hand[atkStrong]].Damage) atkStrong = i;
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

            // rival derribado: no puede esquivar y tus lentos suben a 10 → mixup
            if (s.KnockedDown[opp])
            {
                if (r < 0.55 && atkStrong >= 0) return atkStrong;
                if (r < 0.85 && thr >= 0) return thr;
                if (atkFast >= 0) return atkFast;
            }
            // derribado yo: bloquear adivinando altura, o el reversal (s11 no se apura)
            if (s.KnockedDown[me])
            {
                if (r < 0.30 && reversal >= 0) return reversal;
                int blk = AnyBlock();
                if (blk >= 0) return blk;
            }

            // lectura del hábito rival (Hard lee más seguido)
            double counterChance = Difficulty == AIDifficulty.Hard ? 0.40 :
                                   Difficulty == AIDifficulty.Easy ? 0.0 : 0.20;
            if (_rng.NextDouble() < counterChance)
            {
                int habit = CardHabit();
                if (habit == (int)CardKind.Block && thr >= 0) return thr;      // al tortuga: agarre
                if (habit == (int)CardKind.Throw && atkFast >= 0) return atkFast; // al agarrón: golpe
                if (habit == (int)CardKind.Attack)
                {
                    // esquivar es caro: solo con mano gorda; si no, bloquear
                    if (dodge >= 0 && hand.Count >= 6 && _rng.NextDouble() < 0.5) return dodge;
                    int blk = AnyBlock();
                    if (blk >= 0) return blk;
                }
                if (habit == (int)CardKind.Dodge && thr >= 0) return thr;      // throw le gana al dodge
            }

            // mezcla base: pega, agarra, bloquea, y X para construir mano
            if (r < 0.20 && atkFast >= 0) return atkFast;
            if (r < 0.36 && atkStrong >= 0) return atkStrong;
            if (r < 0.48 && proj >= 0) return proj;       // recurring + lockdown: el motor de mano
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
            return bestN >= 3 ? best : -1; // sin muestra no hay lectura
        }

        // El castigo (dodge a strike / unsafe bloqueado): el golpe más caro,
        // pero prefiere el throw si derriba y la mano rival está gorda.
        public int PickCardHitBack(CardSim s, int me)
        {
            int best = -1, bestDmg = -1, thr = -1;
            for (int i = 0; i < s.Hand[me].Count; i++)
            {
                var d = CardCatalog.All[s.Hand[me][i]];
                if (d.Kind == CardKind.Throw) thr = i;
                if (d.Kind != CardKind.Attack && d.Kind != CardKind.Throw) continue;
                if (d.Damage > bestDmg) { bestDmg = d.Damage; best = i; }
            }
            if (thr >= 0 && _rng.NextDouble() < 0.35) return thr; // el knockdown también paga
            return best;
        }

        // Main phase del activo IA: recuperar blocks que falten (la regla de
        // oro de Yomi 2) y después mejorar la mano. Grave banca dos exchanges.
        public void DoCardExchanges(CardSim s)
        {
            int me = s.Active;
            while (s.ExchangesLeft > 0)
            {
                int want = -1;
                if (!s.Hand[me].Contains(CardCatalog.LowBlock))
                    want = s.Discard[me].IndexOf(CardCatalog.LowBlock);
                if (want < 0 && !s.Hand[me].Contains(CardCatalog.HighBlock))
                    want = s.Discard[me].IndexOf(CardCatalog.HighBlock);
                if (want < 0 && !s.Hand[me].Contains(CardCatalog.Throw))
                    want = s.Discard[me].IndexOf(CardCatalog.Throw);
                if (want < 0) return;

                int give = CardToGive(s, me);
                if (give < 0 || !s.Exchange(give, want)) return;
            }
        }

        // Qué soltar en el exchange: un dodge sobrante o la normal repetida más débil.
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
                var d = CardCatalog.All[hand[i]];
                if (!d.IsNormal || d.Kind != CardKind.Attack) continue;
                bool dup = false;
                for (int j = 0; j < hand.Count; j++) if (j != i && hand[j] == hand[i]) dup = true;
                if (dup && d.Damage < giveDmg) { giveDmg = d.Damage; give = i; }
            }
            if (give >= 0) return give;
            return dodges > 0 ? firstDodge : -1;
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
