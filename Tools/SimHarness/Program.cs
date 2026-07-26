using System;
using System.Collections.Generic;
using LagFighter;

// Lab de balance: miles de peleas AI vs AI sobre la sim pura, sin Unity.
// Stats por movimiento (usos, conecta%, dmg) + guard crushes.
class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "crushtest") { CrushTest(); return; }
        if (args.Length > 0 && args[0] == "cornertest") { CornerTest(); return; }
        if (args.Length > 0 && args[0] == "profiles")
        {
            ProfileMatrix(args.Length > 1 ? int.Parse(args[1]) : 300);
            return;
        }
        if (args.Length > 0 && args[0] == "length")
        {
            LengthDistribution(args.Length > 1 ? int.Parse(args[1]) : 4000);
            return;
        }
        if (args.Length > 0 && args[0] == "yomi")
        {
            RunYomiLab(args.Length > 1 ? int.Parse(args[1]) : 5000);
            return;
        }
        if (args.Length > 0 && args[0] == "cards")
        {
            RunCardsLab(args.Length > 1 ? int.Parse(args[1]) : 5000);
            return;
        }
        if (args.Length > 0 && args[0] == "cardstrace")
        {
            RunCardsTrace(args.Length > 1 ? int.Parse(args[1]) : 1);
            return;
        }
        if (args.Length > 0 && args[0] == "duelo")
        {
            RunDueloLab(args.Length > 1 ? int.Parse(args[1]) : 5000);
            return;
        }
        if (args.Length > 0 && args[0] == "duelogap")
        {
            RunDueloGap(args.Length > 1 ? int.Parse(args[1]) : 4000);
            return;
        }
        if (args.Length > 0 && args[0] == "duelotune")
        {
            RunDueloTune(args.Length > 1 ? int.Parse(args[1]) : 3000);
            return;
        }
        if (args.Length > 0 && args[0] == "duelocantos")
        {
            RunDueloCantos(args.Length > 1 ? int.Parse(args[1]) : 3000);
            return;
        }
        int matches = args.Length > 0 ? int.Parse(args[0]) : 3000;
        RunLab(matches, carryover: false);
        Console.WriteLine();
        Console.WriteLine("=== TURNO FLUIDO (overflow + SUPER habilitados) ===");
        RunLab(matches, carryover: true);
        Console.WriteLine();
        Console.WriteLine("=== MODO YOMI (discreto: 2 distancias, 1 acción/turno) ===");
        RunYomiLab(matches);
        Console.WriteLine();
        Console.WriteLine("=== MODO CARTAS (copia de Yomi 2) ===");
        RunCardsLab(matches);
        Console.WriteLine();
        Console.WriteLine("=== MODO DUELO (el núcleo casual) ===");
        RunDueloLab(matches);
        Console.WriteLine();
        RunDueloGap(matches);
    }

    // ---- MODO DUELO ----------------------------------------------------
    // El lab mide dos cosas distintas: RunDueloLab el ritmo y el balance
    // (¿alguna carta domina? ¿cierra en KO? ¿los personajes están parejos?)
    // y RunDueloGap la PROFUNDIDAD (ver DUELO.md §6): cuánto le gana la IA
    // que juega bien a la que juega al azar, y cuánto vale leer al rival.

    // Full = la IA completa · NoReads = la misma sin leer al rival ·
    // Random = juega cualquier carta · Predecible = la MISMA IA competente
    // (sin lectura) pero con UN tic legible: cuando ataca, prefiere pegar
    // abajo. Es el sparring que hace falta para medir si la información
    // sirve — contra un random no hay nada que leer, y contra un bot tonto
    // el resultado se lo come el techo.
    // Para los CANTOS (DUELO.md §11): NoCantos = juega Full pero nunca canta
    // (sí responde) · RandomCantos = juega Full pero canta a frecuencia
    // parecida SIN mirar nada. El par mide si cantar BIEN es una habilidad.
    enum DuelBot { Full, NoReads, Random, Predictable, NoCantos, RandomCantos }

    class DuelPlayer
    {
        readonly SimpleAI _ai;
        readonly System.Random _rand;
        readonly bool _tellBajo;
        readonly bool _sings = true;        // NoCantos: responde pero no canta
        readonly System.Random _cantoRand;  // RandomCantos: canta a ciegas

        public DuelPlayer(int seed, DuelBot kind)
        {
            if (kind == DuelBot.Random)
            {
                _rand = new System.Random(seed);
                _cantoRand = new System.Random(seed ^ 0x5bd1e995);
                return;
            }
            _ai = new SimpleAI(seed);
            _ai.ReadsHabits = kind == DuelBot.Full || kind == DuelBot.NoCantos || kind == DuelBot.RandomCantos;
            _tellBajo = kind == DuelBot.Predictable;
            _sings = kind != DuelBot.NoCantos;
            if (kind == DuelBot.RandomCantos) _cantoRand = new System.Random(seed ^ 0x5bd1e995);
        }

        // ---- cantos ----
        // Frecuencias del random calibradas a ojo contra las del smart
        // (verificar en la salida del lab que queden comparables).
        public bool CantaEnvido(DuelSim d, int me)
        {
            if (!_sings) return false;
            if (_cantoRand != null) return _cantoRand.NextDouble() < 0.30;
            return _ai.CantaEnvido(d, me);
        }

        public bool QuiereEnvido(DuelSim d, int me)
        {
            if (_cantoRand != null) return _cantoRand.NextDouble() < 0.55;
            return _ai != null ? _ai.QuiereEnvido(d, me) : true;
        }

        public bool CantaTruco(DuelSim d, int me)
        {
            if (!_sings) return false;
            if (_cantoRand != null) return _cantoRand.NextDouble() < 0.10;
            return _ai.CantaTruco(d, me);
        }

        public int RespondeTruco(DuelSim d, int me, int level)
        {
            if (_cantoRand != null)
            {
                double r = _cantoRand.NextDouble();
                if (r < 0.12 && level < DuelConfig.TrucoMaxLevel) return 2;
                return r < 0.75 ? 1 : 0;
            }
            return _ai != null ? _ai.RespondeTruco(d, me, level) : 1;
        }

        public int Pick(DuelSim d, int me)
        {
            if (_ai == null) return d.Hand[me].Count == 0 ? -1 : _rand.Next(d.Hand[me].Count);
            int pick = _ai.PickDuelCard(d, me);
            if (!_tellBajo || pick < 0) return pick;
            // el tic: si eligió pegar ALTO y tiene un golpe bajo, pega abajo
            var c = d.Def(me, d.Hand[me][pick]);
            if (c.Kind != DuelKind.Strike || c.Height != DuelHeight.High) return pick;
            for (int i = 0; i < d.Hand[me].Count; i++)
            {
                var alt = d.Def(me, d.Hand[me][i]);
                if (alt.Kind == DuelKind.Strike && alt.Height == DuelHeight.Low) return i;
            }
            return pick;
        }

        // Cierra la decisión pendiente. El bot random elige a ciegas pero
        // SIEMPRE cierra (el derribo es el fallback legal).
        public void Choice(DuelSim d)
        {
            if (_ai != null) { _ai.DoDuelChoice(d); return; }
            int me = d.PendingSide;
            if (me < 0) return;
            if (d.PendingIsPunish)
            {
                var opts = new List<int>();
                for (int i = 0; i < d.Hand[me].Count; i++)
                    if (d.Def(me, d.Hand[me][i]).IsAttack) opts.Add(i);
                d.Punish(opts.Count > 0 ? opts[_rand.Next(opts.Count)] : -1);
                return;
            }
            var fuel = d.PrizeFuel(me);
            if (fuel.Count > 0 && _rand.NextDouble() < 0.5)
            {
                if (d.ChoosePrize(DuelPrize.Damage, fuel[_rand.Next(fuel.Count)])) return;
            }
            d.ChoosePrize(DuelPrize.Knockdown);
        }

        public void Observe(DuelCard card) => _ai?.ObserveDuel(card);
    }

    // Juega una partida entera. Devuelve el ganador (−1 empate).
    static int PlayDuel(int seed, int c0, int c1, DuelBot b0, DuelBot b1, DuelStats st = null)
    {
        // Seeds HASHEADAS por lado: System.Random correlaciona seeds que
        // difieren en un offset constante, y eso sesga el head-to-head sin
        // que la sim tenga nada raro (la trampa que ya mordió el lab de
        // cartas; acá se veía como "P0 gana 52% con bots random").
        var p0 = new DuelPlayer(HashSeed(seed, 0), b0);
        var p1 = new DuelPlayer(HashSeed(seed, 1), b1);
        var d = new DuelSim(seed, c0, c1);
        // envido→round: el ganador del envido de cada round vs quién ganó
        // ESE round (los rounds pueden terminar por KO, chip de canto o
        // time over — se detecta por el marcador, no por el turno).
        int envWinnerRound = -1, prevRound = 1, prevW0 = 0, prevW1 = 0;
        void RoundTick()
        {
            if (d.Round == prevRound && !d.Over) return;
            int rw = d.RoundWins[0] > prevW0 ? 0 : d.RoundWins[1] > prevW1 ? 1 : -1;
            prevRound = d.Round; prevW0 = d.RoundWins[0]; prevW1 = d.RoundWins[1];
            if (st != null && envWinnerRound >= 0 && rw >= 0)
            {
                st.EnvConGanador++;
                if (rw == envWinnerRound) st.EnvGanadorGanaPartida++;
            }
            envWinnerRound = -1;
        }
        int guard = 0;
        while (!d.Over && guard++ < 150)
        {
            d.StartTurn();
            if (d.Over) break;

            // ---- cantos, en la planificación: primero envido, después truco.
            // Quién habla primero alterna por turno (determinista por seed).
            bool canto = false;
            if (d.CanEnvido)
            {
                int first = d.Turn & 1;
                for (int k = 0; k < 2 && d.CanEnvido; k++)
                {
                    int side = k == 0 ? first : 1 - first;
                    if (!(side == 0 ? p0 : p1).CantaEnvido(d, side)) continue;
                    bool quiero = (side == 0 ? p1 : p0).QuiereEnvido(d, 1 - side);
                    var er = d.ResolveEnvido(side, quiero);
                    if (er.Winner >= 0) envWinnerRound = er.Winner;
                    st?.Envido(er);
                    canto = true;
                    break;
                }
            }
            if (!d.Over && d.CanTruco)
            {
                int first = 1 - (d.Turn & 1);
                for (int k = 0; k < 2 && d.CanTruco; k++)
                {
                    int caller = k == 0 ? first : 1 - first;
                    if (!(caller == 0 ? p0 : p1).CantaTruco(d, caller)) continue;
                    int level = 1, lastCaller = caller;
                    bool quiero;
                    while (true)
                    {
                        int resp = 1 - lastCaller;
                        int ans = (resp == 0 ? p0 : p1).RespondeTruco(d, resp, level);
                        if (ans == 2 && level < DuelConfig.TrucoMaxLevel) { level++; lastCaller = resp; continue; }
                        quiero = ans >= 1;
                        break;
                    }
                    // OJO: ResolveTruco en variable propia — dentro de
                    // st?.Truco(...) el null-condicional se saltea el
                    // argumento y el truco NO SE RESUELVE cuando st es null
                    // (todas las corridas 1v1). Costó una tarde de números.
                    var tr = d.ResolveTruco(lastCaller, level, quiero);
                    st?.Truco(tr);
                    canto = true;
                    break;
                }
            }
            if (canto && st != null) st.TurnosConCanto++;
            RoundTick();               // el chip de un canto pudo cerrar el round
            if (d.Over) break;

            int h0 = p0.Pick(d, 0), h1 = p1.Pick(d, 1);
            var r = d.Resolve(h0, h1);
            if (d.AwaitingChoice) (d.PendingSide == 0 ? p0 : p1).Choice(d);
            if (r.Card1 >= 0) p0.Observe(d.Def(1, r.Card1));
            if (r.Card0 >= 0) p1.Observe(d.Def(0, r.Card0));
            st?.Turn(d, r, c0, c1);
            RoundTick();
        }
        st?.Match(d);
        return d.Winner;
    }

    static int HashSeed(int seed, int side)
    {
        uint x = (uint)seed * 0x9E3779B9u + (uint)(side + 1) * 0x85EBCA6Bu;
        x ^= x >> 16; x *= 0x7feb352du;
        x ^= x >> 15; x *= 0x846ca68bu;
        x ^= x >> 16;
        return (int)(x & 0x7FFFFFFF);
    }

    class DuelStats
    {
        public long Turns, Kos, Draws, TimeOvers, Matches;
        public long GuardOk, GuardMal, Trades, Techs, Escapes, Chips;
        public readonly long[] GuardOkSide = new long[2], GuardMalSide = new long[2], WinsSide = new long[2];
        public long PrizeDmg, PrizeKd, Punishes, Kds, Empty;
        public long HandSum, HandSamples;
        public readonly long[,] Uses = new long[DuelCatalog.Chars.Length, DuelCatalog.CardsPerChar];

        // ---- cantos ----
        public long TurnosConCanto;
        public long EnvCantados, EnvQueridos, EnvEmpates, EnvTantoSum, EnvChipTotal;
        public long EnvGanadorGanaPartida, EnvConGanador;
        public long TrucoCantados, TrucoQueridos, TrucoFolds, TrucoChipTotal, TrucoCobrados;
        public readonly long[] TrucoNivel = new long[4];   // nivel aceptado 1..3
        public long PostEnvGuardOk, PostEnvGuardMal;       // la siembra: guardias contra el ganador del envido

        public void Envido(DuelEnvidoResult er)
        {
            EnvCantados++;
            if (!er.Quiero) { EnvChipTotal += er.Chip; return; }
            EnvQueridos++;
            if (er.Winner < 0) { EnvEmpates++; return; }
            EnvTantoSum += er.Winner == 0 ? er.Tanto0 : er.Tanto1;
            EnvChipTotal += er.Chip;
        }

        public void Truco(DuelTrucoResult tr)
        {
            TrucoCantados++;
            if (tr.Quiero) { TrucoQueridos++; TrucoNivel[tr.Level]++; }
            else { TrucoFolds++; TrucoChipTotal += tr.Chip; }
        }

        public long TrucoGuardCobros;   // trucos cobrados BLOQUEANDO (en cartas)

        public void Turn(DuelSim d, DuelTurnResult r, int c0, int c1)
        {
            if (r.Truco > 0) TrucoCobrados++;
            if (r.Truco > 0 && (r.Guarded0 || r.Guarded1)) TrucoGuardCobros++;
            // la siembra: ¿el que defiende CONTRA el ganador del envido
            // acierta más la altura? (los derribos no cuentan: no adivinaron)
            if (d.PublicTantoSide >= 0)
            {
                int reader = 1 - d.PublicTantoSide;
                if (r.Guarded(reader)) PostEnvGuardOk++;
                if (r.WrongGuard(reader) && !r.GuardWasDown(reader)) PostEnvGuardMal++;
            }
            if (r.Card0 >= 0) Uses[c0, r.Card0]++; else Empty++;
            if (r.Card1 >= 0) Uses[c1, r.Card1]++; else Empty++;
            if (r.Guarded0) { GuardOk++; GuardOkSide[0]++; }
            if (r.Guarded1) { GuardOk++; GuardOkSide[1]++; }
            if (r.WrongGuard0) { GuardMal++; GuardMalSide[0]++; }
            if (r.WrongGuard1) { GuardMal++; GuardMalSide[1]++; }
            if (r.Winner >= 0) WinsSide[r.Winner]++;
            if (r.Trade) Trades++;
            if (r.Tech) Techs++;
            if (r.Escaped0) Escapes++; if (r.Escaped1) Escapes++;
            if (r.Chip0 > 0) Chips++; if (r.Chip1 > 0) Chips++;
            if (r.Prize == DuelPrize.Damage) PrizeDmg++;
            if (r.Prize == DuelPrize.Knockdown) PrizeKd++;
            if (r.PunishSide >= 0) Punishes++;
            if (r.KdNext0) Kds++; if (r.KdNext1) Kds++;
            HandSum += d.Hand[0].Count + d.Hand[1].Count; HandSamples += 2;
        }

        public long RoundsSum;

        public void Match(DuelSim d)
        {
            Matches++;
            Turns += d.Turn;
            RoundsSum += d.Round;
            if (d.Hp[0] <= 0 || d.Hp[1] <= 0) Kos++; else TimeOvers++;
            if (d.Winner < 0) Draws++;
        }
    }

    static void RunDueloLab(int matches)
    {
        int nc = DuelCatalog.Chars.Length;
        var st = new DuelStats();
        var wins = new long[nc, nc];
        var games = new long[nc, nc];
        for (int m = 0; m < matches; m++)
        {
            int c0 = (m / nc) % nc, c1 = m % nc;
            int w = PlayDuel(m + 1, c0, c1, DuelBot.Full, DuelBot.Full, st);
            games[c0, c1]++;
            if (w == 0) wins[c0, c1]++;
        }
        var cn = new string[nc];
        for (int i = 0; i < nc; i++) cn[i] = DuelCatalog.Chars[i].Name;
        Console.WriteLine($"partidas: {matches} · empates {st.Draws} · KO {100.0 * st.Kos / matches:0.0}% · time over {100.0 * st.TimeOvers / matches:0.0}%");
        Console.WriteLine($"turnos/partida: {(double)st.Turns / matches:0.0} · rounds/partida {(double)st.RoundsSum / matches:0.0} · mano promedio {(double)st.HandSum / Math.Max(1, st.HandSamples):0.0}/{DuelConfig.HandLimit} · manos vacías {st.Empty}");
        Console.WriteLine($"guardias bien {st.GuardOk} · altura equivocada {st.GuardMal} ({100.0 * st.GuardOk / Math.Max(1, st.GuardOk + st.GuardMal):0}% acierto) · chips {st.Chips}");
        Console.WriteLine($"premio: +DAÑO {st.PrizeDmg} vs DERRIBO {st.PrizeKd} · derribos {st.Kds} · castigos {st.Punishes} · trades {st.Trades} · techs {st.Techs} · escapes {st.Escapes}");
        Console.WriteLine($"cantos: {100.0 * st.TurnosConCanto / Math.Max(1, st.Turns):0.0}% de los turnos");
        Console.WriteLine($"  envido: {st.EnvCantados} cantados ({100.0 * st.EnvCantados / Math.Max(1, matches):0}% de partidas) · queridos {st.EnvQueridos} · empates {st.EnvEmpates} · tanto ganador prom {(double)st.EnvTantoSum / Math.Max(1, st.EnvQueridos - st.EnvEmpates):0.0}");
        Console.WriteLine($"  envido→round: el ganador del envido gana el {100.0 * st.EnvGanadorGanaPartida / Math.Max(1, st.EnvConGanador):0.0}% de esos rounds [~50% = no lo define]");
        Console.WriteLine($"  siembra: acierto de guardia contra el CANTADO {100.0 * st.PostEnvGuardOk / Math.Max(1, st.PostEnvGuardOk + st.PostEnvGuardMal):0.0}% vs global {100.0 * st.GuardOk / Math.Max(1, st.GuardOk + st.GuardMal):0.0}%");
        Console.WriteLine($"  truco: {st.TrucoCantados} cantados · queridos {st.TrucoQueridos} (×2 {st.TrucoNivel[1]} · ×3 {st.TrucoNivel[2]} · ×4 {st.TrucoNivel[3]}) · no quiero {st.TrucoFolds} · cobrados {st.TrucoCobrados} (bloqueando {st.TrucoGuardCobros})");
        Console.WriteLine("  winrate global por personaje (los dos lados juntos):");
        for (int a = 0; a < nc; a++)
        {
            double w = 0, g = 0;
            for (int b = 0; b < nc; b++)
            {
                if (a == b) continue;
                w += wins[a, b]; g += games[a, b];
                w += games[b, a] - wins[b, a]; g += games[b, a];
            }
            Console.WriteLine($"    {cn[a],-8}{100.0 * w / Math.Max(1, g):0.0}%");
        }
        for (int a = 0; a < nc; a++)
            for (int b = 0; b < nc; b++)
                if (games[a, b] > 0)
                    Console.WriteLine($"  {cn[a]} vs {cn[b]}: {100.0 * wins[a, b] / games[a, b]:0.0}% para {cn[a]} ({games[a, b]} partidas)");
        for (int c = 0; c < nc; c++)
        {
            Console.WriteLine($"  usos — {cn[c]}:");
            var chr = DuelCatalog.Chars[c];
            long total = 0;
            for (int i = 0; i < DuelCatalog.CardsPerChar; i++) total += st.Uses[c, i];
            for (int i = 0; i < DuelCatalog.CardsPerChar; i++)
                if (st.Uses[c, i] > 0)
                    Console.WriteLine($"    {chr.Cards[i].Name,-22}{st.Uses[c, i],8}  ({100.0 * st.Uses[c, i] / Math.Max(1, total):0.0}%)");
        }
    }

    // Las dos métricas de PROFUNDIDAD de DUELO.md §6. Cada enfrentamiento se
    // juega en los dos lados (mismo seed) para que el sesgo de lado no
    // contamine el resultado.
    static void RunDueloGap(int matches)
    {
        Console.WriteLine($"=== DUELO: métricas de profundidad ({matches} partidas por test) ===");
        double gap = Duel1v1(matches, DuelBot.Full, DuelBot.Random);
        double espejo = Duel1v1(matches, DuelBot.Full, DuelBot.NoReads);
        double conLectura = Duel1v1(matches, DuelBot.Full, DuelBot.Predictable);
        double sinLectura = Duel1v1(matches, DuelBot.NoReads, DuelBot.Predictable);
        Console.WriteLine($"brecha de habilidad (heurística vs random): {gap * 100:0.0}%   [objetivo ≥75%]");
        Console.WriteLine($"contra un rival CON HÁBITO — leyendo: {conLectura * 100:0.0}% · sin leer: {sinLectura * 100:0.0}%");
        Console.WriteLine($"valor de la información: +{(conLectura - sinLectura) * 100:0.0} pp   [0 = la info pública es decorativa · objetivo >+5 con cantos]");
        double vsNoCanta = Duel1v1(matches, DuelBot.Full, DuelBot.NoCantos);
        double vsCantaRandom = Duel1v1(matches, DuelBot.Full, DuelBot.RandomCantos);
        Console.WriteLine($"valor de los cantos: contra el que NO canta {vsNoCanta * 100:0.0}% · contra el que canta AL AZAR {vsCantaRandom * 100:0.0}%");
        Console.WriteLine($"  [si ambos dan ~50%, el canto es moneda decorativa y se mata con datos]");
        Console.WriteLine($"(control: leyendo vs sin leer, ambos impredecibles: {espejo * 100:0.0}% — debe dar ~50%)");
        // Invariante: con reveal SIMULTÁNEO ningún lado tiene prioridad, así
        // que dos bots iguales sin alternar lados deben dar 50/50. Si no da,
        // hay una asimetría escondida (en la sim o en la IA).
        Console.WriteLine($"  simetría de lados (sin alternar, debe dar ~50%): " +
                          $"random {SidedP0(matches, DuelBot.Random) * 100:0.0}% · " +
                          $"heurística {SidedP0(matches, DuelBot.Full) * 100:0.0}%");
        Console.WriteLine("  diagnóstico contra el rival con hábito:");
        DueloDiagnostico(matches, DuelBot.Full, "leyendo");
        DueloDiagnostico(matches, DuelBot.NoReads, "sin leer");
    }

    // Barridos de la era de ROUNDS (DUELO.md §12): vida por round × robo de
    // guardia (el escenario Ley 3 de Patricio: robo 2 base → truco roba 4),
    // y el A/B de mano suelta (Ley 7: ¿más dispersión de reparto = más
    // dientes para el canto?).
    static void RunDueloTune(int matches)
    {
        int hp0 = DuelConfig.MaxHp, gd0 = DuelConfig.GuardDraw;
        Console.WriteLine($"=== DUELO: barrido vida-por-round × robo de guardia ({matches} partidas por celda) ===");
        Console.WriteLine("  vida  robo-def | turnos rounds   KO%   mano  brecha  info   vsNoCanta  cantarBien");
        foreach (int hp in new[] { 24, 26, 28 })
            foreach (int gd in new[] { 1, 2 })
            {
                DuelConfig.MaxHp = hp;
                DuelConfig.GuardDraw = gd;
                TuneRow($"  {hp,4}  {gd,8} |", matches);
            }
        DuelConfig.MaxHp = hp0;
        DuelConfig.GuardDraw = gd0;

        Console.WriteLine();
        Console.WriteLine($"=== DUELO: Ley 7 — mano garantizada vs mano SUELTA (solo escape) ===");
        Console.WriteLine("  reparto        | turnos rounds   KO%   mano  brecha  info   vsNoCanta  cantarBien");
        foreach (bool loose in new[] { false, true })
        {
            DuelConfig.LooseOpening = loose;
            TuneRow($"  {(loose ? "SUELTO" : "garantizado"),-14} |", matches);
        }
        DuelConfig.LooseOpening = false;
    }

    static void TuneRow(string label, int matches)
    {
        var st = new DuelStats();
        int nc = DuelCatalog.Chars.Length;
        for (int m = 0; m < matches; m++)
            PlayDuel(m + 1, (m / nc) % nc, m % nc, DuelBot.Full, DuelBot.Full, st);
        double gap = Duel1v1(matches, DuelBot.Full, DuelBot.Random);
        double con = Duel1v1(matches, DuelBot.Full, DuelBot.Predictable);
        double sin = Duel1v1(matches, DuelBot.NoReads, DuelBot.Predictable);
        double noCanta = Duel1v1(matches, DuelBot.Full, DuelBot.NoCantos);
        double cantaRandom = Duel1v1(matches, DuelBot.Full, DuelBot.RandomCantos);
        Console.WriteLine($"{label} {(double)st.Turns / matches,6:0.0} {(double)st.RoundsSum / matches,6:0.0} {100.0 * st.Kos / matches,5:0.0} " +
                          $"{(double)st.HandSum / Math.Max(1, st.HandSamples),6:0.0} " +
                          $"{gap * 100,7:0.0} {(con - sin) * 100,6:+0.0;-0.0} {noCanta * 100,10:0.0} {cantaRandom * 100,11:0.0}");
    }

    // Barrido del dial del envido (DUELO.md §11): la intuición de Patricio
    // es que 3 de chip es poco contra 46 de vida pero 10-15 define el
    // combate. Las columnas que deciden: cuánto gana la partida el ganador
    // del envido (¿la define?) y el valor de cantar bien.
    static void RunDueloCantos(int matches)
    {
        int chip0 = DuelConfig.EnvidoChip;
        Console.WriteLine($"=== DUELO: barrido del chip de envido ({matches} partidas por celda) ===");
        Console.WriteLine("  chip | turnos   KO%   env→partida%   vsNoCanta   vsCantaRandom   info");
        foreach (int chip in new[] { 3, 6, 10, 15 })
        {
            DuelConfig.EnvidoChip = chip;
            var st = new DuelStats();
            int nc = DuelCatalog.Chars.Length;
            for (int m = 0; m < matches; m++)
                PlayDuel(m + 1, (m / nc) % nc, m % nc, DuelBot.Full, DuelBot.Full, st);
            double noCanta = Duel1v1(matches, DuelBot.Full, DuelBot.NoCantos);
            double cantaRandom = Duel1v1(matches, DuelBot.Full, DuelBot.RandomCantos);
            double con = Duel1v1(matches, DuelBot.Full, DuelBot.Predictable);
            double sin = Duel1v1(matches, DuelBot.NoReads, DuelBot.Predictable);
            Console.WriteLine($"  {chip,4} | {(double)st.Turns / matches,6:0.0} {100.0 * st.Kos / matches,5:0.0} " +
                              $"{100.0 * st.EnvGanadorGanaPartida / Math.Max(1, st.EnvConGanador),13:0.0} " +
                              $"{noCanta * 100,10:0.0} {cantaRandom * 100,14:0.0} {(con - sin) * 100,7:+0.0;-0.0}");
        }
        DuelConfig.EnvidoChip = chip0;

        // Los diales del TRUCO: que el quiero multiplique también el premio
        // (el intercambio ganado duele de verdad) y que el no quiero pague
        // más caro (el peaje del cobarde). La pregunta: ¿alguna combinación
        // hace que cantar BIEN gane partidas?
        bool prize0 = DuelConfig.TrucoPrizeToo; int fold0 = DuelConfig.TrucoFoldBonus;
        Console.WriteLine();
        Console.WriteLine($"=== DUELO: diales del truco ({matches} partidas por celda) ===");
        Console.WriteLine("  premioX  fold+ | turnos   KO%   vsNoCanta   vsCantaRandom   info   brecha");
        foreach (bool prize in new[] { false, true })
            foreach (int fold in new[] { 0, 2 })
            {
                DuelConfig.TrucoPrizeToo = prize;
                DuelConfig.TrucoFoldBonus = fold;
                var st = new DuelStats();
                int nc = DuelCatalog.Chars.Length;
                for (int m = 0; m < matches; m++)
                    PlayDuel(m + 1, (m / nc) % nc, m % nc, DuelBot.Full, DuelBot.Full, st);
                double noCanta = Duel1v1(matches, DuelBot.Full, DuelBot.NoCantos);
                double cantaRandom = Duel1v1(matches, DuelBot.Full, DuelBot.RandomCantos);
                double con = Duel1v1(matches, DuelBot.Full, DuelBot.Predictable);
                double sin = Duel1v1(matches, DuelBot.NoReads, DuelBot.Predictable);
                double gap = Duel1v1(matches, DuelBot.Full, DuelBot.Random);
                Console.WriteLine($"  {(prize ? "sí" : "no"),7} {fold,6} | {(double)st.Turns / matches,6:0.0} {100.0 * st.Kos / matches,5:0.0} " +
                                  $"{noCanta * 100,10:0.0} {cantaRandom * 100,14:0.0} {(con - sin) * 100,7:+0.0;-0.0} {gap * 100,8:0.0}");
            }
        DuelConfig.TrucoPrizeToo = prize0;
        DuelConfig.TrucoFoldBonus = fold0;
    }

    static double Duel1v1(int matches, DuelBot a, DuelBot b, DuelStats[] porLado = null)
    {
        double score = 0; int played = 0;
        for (int m = 0; m < matches; m++)
        {
            int nc = DuelCatalog.Chars.Length;
            int c0 = (m / nc) % nc, c1 = m % nc;
            bool aFirst = (m & 1) == 0;
            int aSide = aFirst ? 0 : 1;
            var st = porLado?[aSide];
            int w = aFirst ? PlayDuel(m + 1, c0, c1, a, b, st) : PlayDuel(m + 1, c0, c1, b, a, st);
            if (w == aSide) score += 1;
            else if (w < 0) score += 0.5;
            played++;
        }
        return score / Math.Max(1, played);
    }

    // Winrate de P0 con el MISMO bot de los dos lados y sin alternar.
    static double SidedP0(int matches, DuelBot bot)
    {
        double score = 0;
        for (int m = 0; m < matches; m++)
        {
            int nc = DuelCatalog.Chars.Length;
            int w = PlayDuel(m + 1, (m / nc) % nc, m % nc, bot, bot);
            if (w == 0) score += 1; else if (w < 0) score += 0.5;
        }
        return score / Math.Max(1, matches);
    }

    // Diagnóstico: ¿la lectura mejora el ACIERTO de altura al defender, y ese
    // acierto se convierte en intercambios ganados?
    static void DueloDiagnostico(int matches, DuelBot lector, string nombre)
    {
        var st = new[] { new DuelStats(), new DuelStats() };
        Duel1v1(matches, lector, DuelBot.Predictable, st);
        long ok = st[0].GuardOkSide[0] + st[1].GuardOkSide[1];
        long mal = st[0].GuardMalSide[0] + st[1].GuardMalSide[1];
        long gan = st[0].WinsSide[0] + st[1].WinsSide[1];
        long perd = st[0].WinsSide[1] + st[1].WinsSide[0];
        Console.WriteLine($"    {nombre,-10} acierto de altura {100.0 * ok / Math.Max(1, ok + mal):0.0}% ({ok}/{ok + mal})" +
                          $" · intercambios ganados {gan} vs {perd}");
    }

    // Traza LEGIBLE de una partida completa de cartas v2, turno a turno:
    // manos, main phase (ability/power up/exchange), openers, combos, pumps,
    // castigos, meter y HP. Uso: cardstrace [seed]
    static void RunCardsTrace(int seed)
    {
        var s = new CardSim(seed, firstPlayer: 0, CardCatalog.GraveIdx, CardCatalog.JainaIdx);
        var ai0 = new SimpleAI(seed * 7919 + 13);
        var ai1 = new SimpleAI(seed * 104729 + 57);
        string CardName(int side, int c) => s.Def(side, c).Name;
        string Pile(int side, System.Collections.Generic.List<int> pile)
        {
            var counts = new int[CardCatalog.CardsPerChar];
            foreach (int c in pile) counts[c]++;
            var sb = new System.Text.StringBuilder();
            for (int c = 0; c < counts.Length; c++)
            {
                if (counts[c] == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(s.Def(side, c).Short);
                if (counts[c] > 1) sb.Append('x').Append(counts[c]);
            }
            return sb.Length == 0 ? "-" : sb.ToString();
        }

        Console.WriteLine($"=== {s.Chr[0].Name} (HP {s.Chr[0].MaxHp}) vs {s.Chr[1].Name} (HP {s.Chr[1].MaxHp}) — seed {seed}, P0 empieza ===");
        int guard = 0;
        while (!s.Over && guard++ < 200)
        {
            int active = s.Active;
            int deckBefore = s.Deck[active].Count;
            s.StartTurn();
            if (s.Over) { Console.WriteLine($"T{s.Turn}: TIME OVER al robar"); break; }
            Console.WriteLine($"\nT{s.Turn} — turno de P{active} ({s.Chr[active].Name}, robó {deckBefore - s.Deck[active].Count})");
            int exBefore = s.ExchangesLeft;
            int meterBefore = s.Meter[active];
            bool abilityBefore = s.AbilityUsed;
            bool puBefore = s.PowerUpUsed;
            (active == 0 ? ai0 : ai1).DoCardMainPhase(s);
            if (s.AbilityUsed && !abilityBefore) Console.WriteLine($"   P{active} juega su ABILITY ({s.Chr[active].AbilityText.Split(':')[0]})");
            if (s.PowerUpUsed && !puBefore) Console.WriteLine($"   P{active} hace POWER UP (meter {meterBefore}→{s.Meter[active]})");
            if (s.ExchangesLeft < exBefore) Console.WriteLine($"   P{active} cambió {exBefore - s.ExchangesLeft} carta(s)");
            Console.WriteLine($"   mano P0 [{Pile(0, s.Hand[0])}]  ·  mano P1 [{Pile(1, s.Hand[1])}]  ·  meter {s.Meter[0]}/{s.Meter[1]}");
            if (s.KnockedDown[0]) Console.WriteLine("   P0 DERRIBADO");
            if (s.KnockedDown[1]) Console.WriteLine("   P1 DERRIBADO");
            int h0 = ai0.PickCardOpener(s, 0), h1 = ai1.PickCardOpener(s, 1);
            var r = s.Resolve(h0, h1);
            if (s.AwaitingFollowup) (s.FollowSide == 0 ? ai0 : ai1).DoCardFollowup(s);
            r = s.LastResult;
            ai0.ObserveCard(s.Def(1, r.Card1).Kind);
            ai1.ObserveCard(s.Def(0, r.Card0).Kind);
            string res = $"   {CardName(0, r.Card0)}  VS  {CardName(1, r.Card1)}";
            if (r.Reckless) res += "  → IMPRUDENCIA (2 y roba)";
            if (r.Arc0) res += "  → P0 come el ARCO";
            if (r.Arc1) res += "  → P1 come el ARCO";
            if (r.ProjCancel) res += "  → proyectiles anulados";
            if (r.Blocked0) res += "  → P0 bloquea bien";
            if (r.Blocked1) res += "  → P1 bloquea bien";
            if (r.WrongBlock0) res += "  → P0 bloqueó MAL";
            if (r.WrongBlock1) res += "  → P1 bloqueó MAL";
            if (r.Dodged0) res += "  → P0 esquiva";
            if (r.Dodged1) res += "  → P1 esquiva";
            if (r.SuperCounter >= 0) res += $"  → ¡SUPER DODGE de P{r.SuperCounter}: 40!";
            for (int i = 0; i < 2; i++)
                if (r.Combo(i).Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (int c in r.Combo(i)) { if (sb.Length > 0) sb.Append('>'); sb.Append(s.Def(i, c).Short); }
                    res += $"  → COMBO P{i}: {sb}";
                }
            if (r.PumpExtra0 > 0) res += $"  → pump P0 +{r.PumpExtra0}";
            if (r.PumpExtra1 > 0) res += $"  → pump P1 +{r.PumpExtra1}";
            if (r.HitBackCard >= 0) res += $"  → P{r.HitBackSide} castiga con {CardName(r.HitBackSide, r.HitBackCard)}";
            if (r.Meter0 > 0) res += $"  → P0 +{r.Meter0} meter";
            if (r.Meter1 > 0) res += $"  → P1 +{r.Meter1} meter";
            if (r.Dmg0 > 0) res += $"  → P0 recibe {r.Dmg0}" + (r.Chip0 > 0 ? $" ({r.Chip0} chip)" : "");
            if (r.Dmg1 > 0) res += $"  → P1 recibe {r.Dmg1}" + (r.Chip1 > 0 ? $" ({r.Chip1} chip)" : "");
            if (r.Self0 > 0) res += $"  → P0 se hace {r.Self0}";
            if (r.Self1 > 0) res += $"  → P1 se hace {r.Self1}";
            if (r.KdNext0) res += "  → P0 derribado";
            if (r.KdNext1) res += "  → P1 derribado";
            if (r.Wild0 + r.Wild1 > 0) res += $"  → wild swing x{r.Wild0 + r.Wild1}";
            Console.WriteLine(res);
            Console.WriteLine($"   HP: P0 {s.Hp[0]} · P1 {s.Hp[1]}   mazos: {s.Deck[0].Count}/{s.Deck[1].Count}   desc: [{Pile(0, s.Discard[0])}] [{Pile(1, s.Discard[1])}]");
        }
        Console.WriteLine($"\n=== FIN en {s.Turn} turnos — {(s.Winner < 0 ? "EMPATE" : $"gana P{s.Winner} ({s.Chr[Math.Max(0, s.Winner)].Name})")} (HP {s.Hp[0]} vs {s.Hp[1]}) ===");
    }

    // Lab del modo CARTAS v2: partidas completas con la IA (main phase,
    // combos, pumps, supers). Rota los matchups Grave/Jaina.
    static void RunCardsLab(int matches)
    {
        var wins = new long[2, 2];   // [char0][char1] → wins de P0
        var games = new long[2, 2];
        long draws = 0, kos = 0, winsFirst = 0, winsSecond = 0;
        long turnsTotal = 0, comboCards = 0, combos = 0, pumps = 0, hitbacks = 0;
        long supersPlayed = 0, meterGained = 0, wilds = 0, arcs = 0, reckless = 0, superCounters = 0;
        long blocksOk = 0, blocksMal = 0, dodgesOk = 0, projCancels = 0, handSum = 0, handSamples = 0;
        var usesByChar = new long[2, CardCatalog.CardsPerChar];

        for (int m = 0; m < matches; m++)
        {
            int c0 = (m / 2) % 2, c1 = m % 2;
            var ai0 = new SimpleAI(m * 7919 + 13);
            var ai1 = new SimpleAI(m * 104729 + 57);
            var s = new CardSim(seed: m + 1, firstPlayer: (m / 4) % 2, c0, c1);
            int guard = 0;
            while (!s.Over && guard++ < 300)
            {
                s.StartTurn();
                if (s.Over) break;
                (s.Active == 0 ? ai0 : ai1).DoCardMainPhase(s);
                int h0 = ai0.PickCardOpener(s, 0);
                int h1 = ai1.PickCardOpener(s, 1);
                var r = s.Resolve(h0, h1);
                if (s.AwaitingFollowup) (s.FollowSide == 0 ? ai0 : ai1).DoCardFollowup(s);
                r = s.LastResult;
                ai0.ObserveCard(s.Def(1, r.Card1).Kind);
                ai1.ObserveCard(s.Def(0, r.Card0).Kind);

                usesByChar[c0, r.Card0]++;
                usesByChar[c1, r.Card1]++;
                for (int i = 0; i < 2; i++)
                {
                    int n = r.Combo(i).Count;
                    if (n > 0) { combos++; comboCards += n; }
                    foreach (int c in r.Combo(i)) if (s.Def(i, c).IsSuper) supersPlayed++;
                    if (r.Card(i) >= 0 && s.Def(i, r.Card(i)).IsSuper) supersPlayed++;
                }
                meterGained += r.Meter0 + r.Meter1;
                if (r.PumpExtra0 > 0) pumps++;
                if (r.PumpExtra1 > 0) pumps++;
                if (r.HitBackCard >= 0) hitbacks++;
                if (r.SuperCounter >= 0) superCounters++;
                if (r.Arc0) arcs++; if (r.Arc1) arcs++;
                if (r.Reckless) reckless++;
                wilds += r.Wild0 + r.Wild1;
                if (r.Blocked0) blocksOk++; if (r.Blocked1) blocksOk++;
                if (r.WrongBlock0) blocksMal++; if (r.WrongBlock1) blocksMal++;
                if (r.Dodged0) dodgesOk++; if (r.Dodged1) dodgesOk++;
                if (r.ProjCancel) projCancels++;
                handSum += s.Hand[0].Count + s.Hand[1].Count; handSamples += 2;
            }
            turnsTotal += s.Turn;
            games[c0, c1]++;
            if (s.Hp[0] <= 0 || s.Hp[1] <= 0) kos++;
            if (s.Winner == 0) wins[c0, c1]++;
            else if (s.Winner < 0) draws++;
            if (s.Winner >= 0) { if (s.Winner == (m / 4) % 2) winsFirst++; else winsSecond++; }
        }

        string[] cn = { "Grave", "Jaina" };
        Console.WriteLine($"partidas: {matches} · empates {draws} · gana el que EMPIEZA {winsFirst} vs {winsSecond}");
        Console.WriteLine($"KO: {100.0 * kos / matches:0.0}% · turnos/partida: {(double)turnsTotal / matches:0.0} · mano promedio: {(double)handSum / Math.Max(1, handSamples):0.0}");
        Console.WriteLine($"combos: {combos} ({(double)comboCards / Math.Max(1, combos):0.0} cartas/combo) · pumps {pumps} · supers jugadas {supersPlayed} · meter por chains {meterGained}");
        Console.WriteLine($"castigos {hitbacks} · super-dodge counters {superCounters} · arcos {arcs} · imprudencias {reckless} · wild swings {wilds}");
        Console.WriteLine($"bloqueos bien {blocksOk} · mal {blocksMal} · esquives {dodgesOk} · proyectiles anulados {projCancels}");
        for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
                if (games[a, b] > 0)
                    Console.WriteLine($"  {cn[a]} vs {cn[b]}: {100.0 * wins[a, b] / games[a, b]:0.0}% para {cn[a]} ({games[a, b]} partidas)");
        for (int c = 0; c < 2; c++)
        {
            Console.WriteLine($"  usos como opener — {cn[c]}:");
            var chr = CardCatalog.Chars[c];
            for (int i = 0; i < CardCatalog.CardsPerChar; i++)
                if (usesByChar[c, i] > 0)
                    Console.WriteLine($"    {chr.Cards[i].Name,-26}{usesByChar[c, i],8}");
        }
    }

    // Lab del modo YOMI v2: partidas discretas sobre YomiSim con la IA de
    // picks. Acá se ve si alguna acción domina, si el juego se estanca en
    // una distancia y cómo respira la economía de AP.
    static void RunYomiLab(int matches)
    {
        const int n = 9;
        var usesClose = new int[n];
        var usesFar = new int[n];
        var dmgBy = new double[n];
        int wins0 = 0, wins1 = 0, draws = 0, timeouts = 0, kos = 0;
        int techs = 0, parries = 0, recoveries = 0, counters = 0;
        long turnsTotal = 0, closeTurns = 0, totalTurns = 0, apSum = 0, apSamples = 0;
        int seed = 5000;

        for (int m = 0; m < matches; m++)
        {
            var y = new YomiSim();
            var ai0 = new SimpleAI(seed++);
            var ai1 = new SimpleAI(seed++);
            int turn = 0;
            for (; turn < YomiConfig.TurnsPerRound && !y.Over; turn++)
            {
                bool close = y.Close;
                var a0 = ai0.PickYomi(y, 0);
                var a1 = ai1.PickYomi(y, 1);
                var r = y.Resolve(a0, a1);
                ai0.ObserveYomi(a1, close); // cada IA aprende del pick rival ya revelado
                ai1.ObserveYomi(a0, close);
                var uses = close ? usesClose : usesFar;
                uses[(int)a0]++;
                uses[(int)a1]++;
                dmgBy[(int)a0] += r.Dmg1;
                dmgBy[(int)a1] += r.Dmg0;
                if (r.Tech) techs++;
                if (r.Parry0) parries++;
                if (r.Parry1) parries++;
                if (r.Rec0Next) recoveries++;
                if (r.Rec1Next) recoveries++;
                if (r.Counter0) counters++;
                if (r.Counter1) counters++;
                if (close) closeTurns++;
                totalTurns++;
                apSum += y.Ap[0] + y.Ap[1];
                apSamples += 2;
            }
            turnsTotal += turn;
            if (y.Over) kos++;
            else timeouts++;
            int w = y.Over ? y.Winner : y.Hp[0] > y.Hp[1] ? 0 : y.Hp[1] > y.Hp[0] ? 1 : -1;
            if (w == 0) wins0++;
            else if (w == 1) wins1++;
            else draws++;
        }

        Console.WriteLine($"partidas: {matches} · P0 {wins0} · P1 {wins1} · empates {draws} · KO {100.0 * kos / matches:0.0}% · timeout {timeouts}");
        Console.WriteLine($"turnos/partida: {(double)turnsTotal / matches:0.0} · turnos CERCA: {100.0 * closeTurns / totalTurns:0.0}% · AP promedio: {(double)apSum / apSamples:0.0}/{YomiConfig.ApCap}");
        Console.WriteLine($"parrys exitosos: {parries} · counters: {counters} · recoveries (shoryu whiff): {recoveries} · techs: {techs}");
        Console.WriteLine();
        Console.WriteLine($"{"acción",-12}{"usos cerca",12}{"usos lejos",12}{"dmg/uso",9}");
        for (int i = 0; i < n; i++)
        {
            int total = usesClose[i] + usesFar[i];
            if (total == 0) continue;
            Console.WriteLine($"{YomiConfig.Name((YomiAction)i),-12}{usesClose[i],12}{usesFar[i],12}{dmgBy[i] / total,9:0.00}");
        }
    }

    // Una pasada completa del lab clásico. Con carryover, la IA cruza el
    // límite del turno (45% cuando el presupuesto no alcanza), carga la barra
    // y tira el Shinku: acá se calibra la economía de la super.
    static void RunLab(int matches, bool carryover)
    {
        SimConfig.CarryoverEnabled = carryover;
        try { RunLabInner(matches, carryover); }
        finally { SimConfig.CarryoverEnabled = false; }
    }

    static void RunLabInner(int matches, bool carryover)
    {
        int n = MoveCatalog.All.Length;
        var uses = new int[n];
        var hits = new int[n];
        var blocks = new int[n];
        var whiffs = new int[n];
        var crushes = new int[n];
        var parries = new int[n];
        var dmg = new double[n];
        int wins0 = 0, wins1 = 0, draws = 0, timeouts = 0, techs = 0;
        int totalTurns = 0, totalCrushes = 0, matchesWithCrush = 0;
        double guardSum = 0; long guardSamples = 0;
        long overflowFrames = 0; int supersFull = 0; // economía del turno fluido
        long apStockSum = 0, apStockSamples = 0; int bankedBlocks = 0; // economía de AP

        for (int m = 0; m < matches; m++)
        {
            var sim = new MatchSim();
            var ai0 = new SimpleAI(m * 2 + 1);
            var ai1 = new SimpleAI(m * 2 + 2);
            int crushesThis = 0;

            // TurnsPerRound, como el juego real (TIME OVER → juez por vida).
            // La economía de AP corre igual que en el juego (ApEconomy):
            // stock, ingreso y bloqueo bancado — el lab la ejercita entera.
            var eco = new ApEconomy();
            int apPerTurn = SimConfig.TurnFrames / SimConfig.FramesPerAp;
            eco.ResetRound(apPerTurn);
            for (int turn = 0; turn < SimConfig.TurnsPerRound && !sim.Over; turn++)
            {
                var p0 = ai0.Plan(sim, 0, SimConfig.TurnFrames, eco.Stock[0]);
                var p1 = ai1.Plan(sim, 1, SimConfig.TurnFrames, eco.Stock[1]);
                foreach (var mv in p0) uses[mv]++;
                foreach (var mv in p1) uses[mv]++;
                sim.SetQueue(0, p0);
                sim.SetQueue(1, p1);

                for (int t = 0; t < SimConfig.TurnFrames && !sim.Over; t++)
                {
                    sim.Step();
                    foreach (var ev in sim.LastEvents)
                    {
                        switch (ev.Kind)
                        {
                            case EvKind.Hit: hits[ev.MoveIndex]++; dmg[ev.MoveIndex] += ev.Damage; break;
                            case EvKind.Blocked: blocks[ev.MoveIndex]++; break;
                            case EvKind.Parry: parries[MoveCatalog.Parry]++; break;
                            case EvKind.Whiff: whiffs[ev.MoveIndex]++; break;
                            case EvKind.Tech: techs++; break;
                            case EvKind.GuardCrush: crushes[ev.MoveIndex]++; totalCrushes++; crushesThis++; break;
                        }
                    }
                    guardSum += sim.Fighters[0].Guard + sim.Fighters[1].Guard;
                    guardSamples += 2;
                }
                overflowFrames += sim.CommittedRemaining(0) + sim.CommittedRemaining(1);
                // cierre económico ANTES de OnTurnEnd (que limpia el bancado)
                int spent0 = 0, spent1 = 0;
                foreach (var mv in p0) spent0 += MoveCatalog.All[mv].ApCost;
                foreach (var mv in p1) spent1 += MoveCatalog.All[mv].ApCost;
                eco.EndTurn(0, apPerTurn, spent0, sim.Fighters[0].BankedBlock);
                eco.EndTurn(1, apPerTurn, spent1, sim.Fighters[1].BankedBlock);
                if (sim.Fighters[0].BankedBlock) bankedBlocks++;
                if (sim.Fighters[1].BankedBlock) bankedBlocks++;
                apStockSum += eco.Stock[0] + eco.Stock[1];
                apStockSamples += 2;
                sim.OnTurnEnd(0);
                sim.OnTurnEnd(1);
                if (sim.Fighters[0].Super >= SimConfig.SuperMax) supersFull++;
                if (sim.Fighters[1].Super >= SimConfig.SuperMax) supersFull++;
                totalTurns++;
            }

            int winner = Judge(sim);
            if (!sim.Over) timeouts++; // TIME OVER: lo decide el juez por vida
            if (winner == 0) wins0++;
            else if (winner == 1) wins1++;
            else draws++;
            if (crushesThis > 0) matchesWithCrush++;
        }

        Console.WriteLine($"peleas: {matches} · P0 {wins0} · P1 {wins1} · dobleKO {draws} · timeout {timeouts} · techs {techs}");
        Console.WriteLine($"turnos/pelea: {(double)totalTurns / matches:0.0}");
        Console.WriteLine($"GUARD CRUSH: {totalCrushes} total · {(double)totalCrushes / matches:0.00}/pelea · {100.0 * matchesWithCrush / matches:0.0}% de peleas con >=1");
        Console.WriteLine($"guardia promedio en juego: {guardSum / guardSamples:0.0}/{SimConfig.GuardMax:0}");
        if (apStockSamples > 0)
            Console.WriteLine($"ECONOMÍA AP: stock promedio {(double)apStockSum / apStockSamples:0.0}/{ApEconomy.Cap(SimConfig.TurnFrames / SimConfig.FramesPerAp)} · bloqueos bancados: {bankedBlocks} ({(double)bankedBlocks / matches:0.00}/pelea)");
        if (carryover)
            Console.WriteLine($"OVERFLOW: {(double)overflowFrames / matches:0.0} frames/pelea · turnos con barra llena: {supersFull} · supers tiradas: {uses[MoveCatalog.Super]}");
        Console.WriteLine();
        Console.WriteLine($"{"mov",-18}{"usos",8}{"hit",7}{"block",7}{"whiff",7}{"parry",7}{"crush",7}{"hit%",7}{"dmg/uso",9}");
        for (int i = 0; i < n; i++)
        {
            var mdef = MoveCatalog.All[i];
            if (!mdef.IsAttack && uses[i] == 0) continue;
            int contacts = hits[i] + blocks[i] + whiffs[i];
            double hitPct = contacts > 0 ? 100.0 * hits[i] / contacts : 0;
            double dpu = uses[i] > 0 ? dmg[i] / uses[i] : 0;
            Console.WriteLine($"{mdef.Name,-18}{uses[i],8}{hits[i],7}{blocks[i],7}{whiffs[i],7}{parries[i],7}{crushes[i],7}{hitPct,6:0.0}%{dpu,9:0.00}");
        }
    }

    // ¿Cuánto dura una pelea SIN límite de turnos? Distribución natural para
    // calibrar el timer de round (perfiles al azar, como VS IA por defecto).
    static void LengthDistribution(int matches)
    {
        var lengths = new List<int>(matches);
        int seed = 40000, kos = 0;
        for (int m = 0; m < matches; m++)
        {
            var sim = new MatchSim();
            var ai0 = new SimpleAI(seed++);
            var ai1 = new SimpleAI(seed++);
            int turn = 0;
            for (; turn < 120 && !sim.Over; turn++)
            {
                var p0 = ai0.Plan(sim, 0, SimConfig.TurnFrames);
                var p1 = ai1.Plan(sim, 1, SimConfig.TurnFrames);
                sim.SetQueue(0, p0);
                sim.SetQueue(1, p1);
                for (int t = 0; t < SimConfig.TurnFrames && !sim.Over; t++) sim.Step();
                sim.OnTurnEnd(0);
                sim.OnTurnEnd(1);
                ai0.ObserveOpponentPlan(p1);
                ai1.ObserveOpponentPlan(p0);
            }
            lengths.Add(turn);
            if (sim.Over) kos++;
        }
        lengths.Sort();
        int P(double q) => lengths[(int)Math.Min(lengths.Count - 1, q * lengths.Count)];
        double avg = 0; foreach (var l in lengths) avg += l; avg /= lengths.Count;
        Console.WriteLine($"DURACIÓN NATURAL — {matches} peleas sin límite (perfiles al azar, dif. Normal)");
        Console.WriteLine($"KO: {100.0 * kos / matches:0.0}% de las peleas terminan solas (el resto llegaría a 120)");
        Console.WriteLine($"promedio {avg:0.0} · mediana {P(0.5)} · p25 {P(0.25)} · p75 {P(0.75)} · p90 {P(0.9)} · p95 {P(0.95)}");
        foreach (int cap in new[] { 10, 12, 15, 18, 20, 25, 30, 40 })
        {
            int ended = 0; foreach (var l in lengths) if (l <= cap) ended++;
            Console.WriteLine($"  con timer de {cap,2} turnos: {100.0 * ended / matches,5:0.0}% termina por KO, {100.0 * (matches - ended) / matches,4:0.0}% lo decide el juez");
        }
    }

    // Mismo criterio que el juego: KO manda, TIME OVER lo decide la vida y,
    // con vida igual, la GUARDIA restante (premia al que atacó: la guardia
    // solo regenera ejecutando moves que no bloquean).
    static int Judge(MatchSim sim)
    {
        if (sim.Over) return sim.Winner;
        float h0 = sim.Fighters[0].Hp, h1 = sim.Fighters[1].Hp;
        if (h0 != h1) return h0 > h1 ? 0 : 1;
        float g0 = sim.Fighters[0].Guard, g1 = sim.Fighters[1].Guard;
        return g0 > g1 + 0.01f ? 0 : g1 > g0 + 0.01f ? 1 : -1;
    }

    // Round-robin de perfiles de IA: cada perfil contra cada perfil (espejo
    // incluido), N peleas por cruce, dificultad Normal. Detecta perfiles
    // opresivos: win% total, duración y crushes por cruce.
    static void ProfileMatrix(int perPair)
    {
        var profiles = new[] { AIProfile.Zoner, AIProfile.Aggressive, AIProfile.Defensive, AIProfile.Trickster, AIProfile.Adaptive };
        int np = profiles.Length;
        var wins = new int[np, np];      // [a,b] = victorias de a contra b (a como P0)
        var games = new int[np, np];
        var turnsSum = new int[np, np];
        var crushSum = new int[np, np];
        int seed = 9000;
        int timeouts = 0;

        for (int a = 0; a < np; a++)
            for (int b = 0; b < np; b++)
                for (int g = 0; g < perPair; g++)
                {
                    var sim = new MatchSim();
                    var ai0 = new SimpleAI(seed++, profiles[a]);
                    var ai1 = new SimpleAI(seed++, profiles[b]);
                    int turn = 0;
                    for (; turn < SimConfig.TurnsPerRound && !sim.Over; turn++) // reglas reales
                    {
                        var p0 = ai0.Plan(sim, 0, SimConfig.TurnFrames);
                        var p1 = ai1.Plan(sim, 1, SimConfig.TurnFrames);
                        sim.SetQueue(0, p0);
                        sim.SetQueue(1, p1);
                        for (int t = 0; t < SimConfig.TurnFrames && !sim.Over; t++)
                        {
                            sim.Step();
                            foreach (var ev in sim.LastEvents)
                                if (ev.Kind == EvKind.GuardCrush) crushSum[a, b]++;
                        }
                        sim.OnTurnEnd(0);
                        sim.OnTurnEnd(1);
                        // ambos observan el plan ya revelado del otro (como en VS IA)
                        ai0.ObserveOpponentPlan(p1);
                        ai1.ObserveOpponentPlan(p0);
                    }
                    games[a, b]++;
                    turnsSum[a, b] += turn;
                    if (!sim.Over) timeouts++; // TIME OVER: juez por vida
                    if (Judge(sim) == 0) wins[a, b]++;
                    // empate: no suma para nadie
                }

        Console.WriteLine($"MATRIZ DE PERFILES — {perPair} peleas por cruce, dificultad Normal, win% del perfil de la FILA (como P0)\n");
        Console.Write($"{"",-12}");
        foreach (var p in profiles) Console.Write($"{p,-12}");
        Console.WriteLine("| win% total (como P0)");
        double[] totalWin = new double[np];
        for (int a = 0; a < np; a++)
        {
            Console.Write($"{profiles[a],-12}");
            double sumPct = 0;
            for (int b = 0; b < np; b++)
            {
                double pct = 100.0 * wins[a, b] / games[a, b];
                sumPct += pct;
                Console.Write($"{pct,-12:0.0}");
            }
            totalWin[a] = sumPct / np;
            Console.WriteLine($"| {totalWin[a]:0.0}%");
        }

        Console.WriteLine($"\nturnos promedio y crushes/pelea por cruce:");
        for (int a = 0; a < np; a++)
            for (int b = a; b < np; b++)
                Console.WriteLine($"  {profiles[a],-11} vs {profiles[b],-11}  turnos {(double)turnsSum[a, b] / games[a, b],5:0.0} · crushes {(double)crushSum[a, b] / games[a, b]:0.00}");
        Console.WriteLine($"\npeleas decididas por el juez (TIME OVER): {timeouts}");
        Console.WriteLine("nota: P0 vs P1 no es perfectamente simétrico; comparar fila vs columna del mismo cruce da el sesgo de lado.");
    }

    // Esquina real: P1 pegado a la pared. El pushback del golpe conectado
    // no puede mover a P1 → el sobrante tiene que empujar a P0 hacia atrás.
    static void CornerTest()
    {
        var sim = new MatchSim();
        sim.Fighters[1].X = SimConfig.StageHalfWidth; // contra la pared
        sim.Fighters[0].X = SimConfig.StageHalfWidth - 1.0f;
        sim.Fighters[1].BlockEnabled = false; // que el golpe conecte limpio

        sim.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        sim.SetQueue(1, new List<int>());
        float x0Before = sim.Fighters[0].X;
        for (int t = 0; t < 30; t++)
        {
            sim.Step();
            foreach (var ev in sim.LastEvents)
                Console.WriteLine($"  t{sim.Tick,3} {ev.Kind} | P0 {sim.Fighters[0].X:0.00} (antes {x0Before:0.00}) · P1 {sim.Fighters[1].X:0.00} (pared {SimConfig.StageHalfWidth})");
        }
        Console.WriteLine(sim.Fighters[0].X < x0Before - 0.05f
            ? "OK: el pushback de la esquina empujó al atacante hacia atrás"
            : "FALLO: el atacante no retrocedió");
    }

    // Escenario determinista: P0 presiona con jabs, P1 bloquea en neutral.
    // Esperado: la guardia de P1 baja 15 por jab bloqueado, cruje en 0,
    // stun de 50f, y la barra renace en 50.
    static void CrushTest()
    {
        var sim = new MatchSim();
        sim.Fighters[0].X = -0.5f;
        sim.Fighters[1].X = 0.5f;

        for (int turn = 0; turn < 12; turn++)
        {
            sim.SetQueue(0, new List<int> { MoveCatalog.WalkF, MoveCatalog.AttackA, MoveCatalog.AttackA });
            sim.SetQueue(1, new List<int>());
            for (int t = 0; t < SimConfig.TurnFrames && !sim.Over; t++)
            {
                sim.Step();
                foreach (var ev in sim.LastEvents)
                    Console.WriteLine($"  t{sim.Tick,4} {ev.Kind,-11} {MoveCatalog.All[ev.MoveIndex].Name,-14} adv {ev.FrameAdv,4} | guardia P1 = {sim.Fighters[1].Guard:0.0} stun {sim.StunRemaining(1)}f hp {sim.Fighters[1].Hp}");
            }
            sim.OnTurnEnd(0);
            sim.OnTurnEnd(1);
            Console.WriteLine($"fin turno {turn}: guardia P1 = {sim.Fighters[1].Guard:0.0} · hp P1 = {sim.Fighters[1].Hp}");
            if (sim.Over) { Console.WriteLine("KO"); break; }
        }
    }
}
