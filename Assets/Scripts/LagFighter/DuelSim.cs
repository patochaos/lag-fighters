using System;
using System.Collections.Generic;

namespace LagFighter
{
    // ---- MODO DUELO (2026-07-25): el núcleo casual ----
    // Ver DUELO.md. El juego entero en 7 reglas: carta secreta simultánea,
    // GOLPE > AGARRE > GUARDIA > GOLPE, la velocidad desempata golpes (empate
    // = trade), cada golpe es ALTO o BAJO y cada guardia cubre UNA altura, el
    // ganador elige +DAÑO o DERRIBO, defender roba 1 y la guardia vuelve a la
    // mano, robás 1 por turno con mano máxima 8 y el mazo se remezcla UNA vez.
    //
    // Diferencias deliberadas con CardSim (la copia de Yomi 2): sin turno
    // activo, sin main phase, sin combos/meter/supers/abilities, sin niveles
    // de proyectil ni categoría "esquive". Y sin wild swing: TODA carta es
    // jugable siempre porque el derribo no prohíbe la guardia — la apaga.
    //
    // Pura y determinista (sin UnityEngine): cada regla es un test.

    public enum DuelKind { Strike = 0, Grab = 1, Guard = 2, Escape = 3 }
    public enum DuelHeight { High = 0, Low = 1, None = 2 }
    public enum DuelPrize { None = 0, Damage = 1, Knockdown = 2 }

    public struct DuelCard
    {
        public string Name;
        public string Short;
        public DuelKind Kind;
        public int Speed;            // mayor = más rápido (solo golpes)
        public int Damage;
        public DuelHeight Height;    // golpes: ALTO/BAJO · guardias: la que cubren
        public int Chip;             // "aunque te la defiendan, pega N"
        public bool FreeKnockdown;   // conecta → derribo gratis, además del premio
        public bool PunishOnGuard;   // te la defienden → el rival pega un golpe de su mano
        // AGUANTE (super armor, el clásico del grappler): el golpe rival te
        // pega igual, pero vos EJECUTÁS lo tuyo. En la tabla: golpe vs agarre
        // deja de ser una derrota limpia y pasa a ser un cambio de golpes.
        public bool Armor;

        public bool IsAttack => Kind == DuelKind.Strike || Kind == DuelKind.Grab;
    }

    public class DuelChar
    {
        public string Name;
        public string Tag;           // una línea: la identidad, para la UI
        public DuelCard[] Cards;     // layout FIJO (ver DuelCatalog)
        public int[] DeckCounts;     // suma 20
        public int HpBonus;          // Ley 11: el tanque aguanta más (0 = vida estándar)
    }

    public static class DuelCatalog
    {
        // Layout fijo por personaje: el mismo índice es la misma ranura en los
        // dos mazos — la UI, la IA y el teatro indexan igual.
        public const int AttackA = 0;
        public const int AttackB = 1;
        public const int AttackC = 2;
        public const int AttackD = 3;
        public const int Throw = 4;
        public const int GuardHigh = 5;
        public const int GuardLow = 6;
        public const int Sig1 = 7;
        public const int Sig2 = 8;
        public const int Escape = 9;
        public const int CardsPerChar = 10;

        // El esqueleto es común (regla de Yomi 2: los normales son compartidos).
        // La correlación ES el juego: rápido = BAJO, lento = ALTO. Las cartas
        // firma existen para romperla.
        static DuelCard[] BaseCards() => new[]
        {
            new DuelCard { Name = "Jab (A)",     Short = "A",   Kind = DuelKind.Strike, Speed = 8, Damage = 3, Height = DuelHeight.Low },
            new DuelCard { Name = "Directo (B)", Short = "B",   Kind = DuelKind.Strike, Speed = 7, Damage = 4, Height = DuelHeight.Low },
            new DuelCard { Name = "Gancho (C)",  Short = "C",   Kind = DuelKind.Strike, Speed = 6, Damage = 5, Height = DuelHeight.High },
            new DuelCard { Name = "Patada (D)",  Short = "D",   Kind = DuelKind.Strike, Speed = 4, Damage = 7, Height = DuelHeight.High },
            // Daño 7 como el throw real de Yomi 2 (5/7): el depredador del
            // bloqueo pega como un pesado (Ley 3) — clave desde que la
            // guardia cobra el truco en cartas.
            new DuelCard { Name = "Agarre",      Short = "AGR", Kind = DuelKind.Grab,   Speed = 5, Damage = 7, Height = DuelHeight.None },
            new DuelCard { Name = "Guardia Alta",Short = "ALT", Kind = DuelKind.Guard,  Height = DuelHeight.High },
            new DuelCard { Name = "Guardia Baja",Short = "BJA", Kind = DuelKind.Guard,  Height = DuelHeight.Low },
        };

        //                            A  B  C  D  AGR ALT BJA S1 S2 ESC   = 20
        static readonly int[] Counts = { 2, 2, 2, 2, 3, 2, 2, 2, 2, 1 };

        public static readonly DuelChar Grave = MakeGrave();
        public static readonly DuelChar Jaina = MakeJaina();
        public static readonly DuelChar Golem = MakeGolem();
        public static readonly DuelChar[] Chars = { Grave, Jaina, Golem };
        public const int GraveIdx = 0, JainaIdx = 1, GolemIdx = 2;

        static DuelCard EscapeCard() => new DuelCard
        {
            Name = "Escape", Short = "ESC", Kind = DuelKind.Escape, Height = DuelHeight.None,
        };

        static DuelChar MakeGrave()
        {
            var cards = new DuelCard[CardsPerChar];
            BaseCards().CopyTo(cards, 0);
            // El "proyectil" sin inventar la palabra proyectil: el más rápido
            // del juego después de la espada, y pega igual si lo defendés.
            // Daño 4→5 (2026-07-25): precio de firma estilo Sirlin, y crea
            // LA colisión de tanto en 10 (X+X bajo = C+C alto) — el número
            // acertijo del envido.
            cards[Sig1] = new DuelCard { Name = "Nube Eléctrica (X)", Short = "X", Kind = DuelKind.Strike, Speed = 10, Damage = 5, Height = DuelHeight.Low, Chip = 2 };
            // El rompe-correlación: ALTO y rápido — caza al que defiende bajo.
            cards[Sig2] = new DuelCard { Name = "Torbellino (Z)", Short = "Z", Kind = DuelKind.Strike, Speed = 7, Damage = 6, Height = DuelHeight.High };
            cards[Escape] = EscapeCard();
            return new DuelChar
            {
                Name = "GRAVE",
                Tag = "Controla el espacio: su X pega 2 aunque la defiendas",
                Cards = cards,
                DeckCounts = Counts,
            };
        }

        static DuelChar MakeJaina()
        {
            var cards = new DuelCard[CardsPerChar];
            BaseCards().CopyTo(cards, 0);
            // La apuesta: gana casi cualquier carrera de velocidad, pero si te
            // la defienden pagás con un golpe gratis del rival.
            cards[Sig1] = new DuelCard { Name = "Espada del Alba (Y)", Short = "Y", Kind = DuelKind.Strike, Speed = 11, Damage = 6, Height = DuelHeight.High, PunishOnGuard = true };
            // Derribo gratis: el premio deja de ser una elección con ella.
            cards[Sig2] = new DuelCard { Name = "Patada Cruzada (K)", Short = "K", Kind = DuelKind.Strike, Speed = 6, Damage = 5, Height = DuelHeight.Low, FreeKnockdown = true };
            cards[Escape] = EscapeCard();
            return new DuelChar
            {
                Name = "JAINA",
                Tag = "Apuesta: su Y gana toda carrera, defendida te cuesta un golpe",
                Cards = cards,
                DeckCounts = Counts,
            };
        }

        // El GRAPPLER, en estado puro de la Ley 11: no tiene ninguna regla
        // nueva — tiene DOS cartas de agarre (5 agarres en 20 cartas) y más
        // vida. Eso solo ya re-pesa todo el juego contra él: defender pasa a
        // ser carísimo, así que hay que pelearle, y pelearle es lo que su
        // Cabezazo castiga.
        static DuelChar MakeGolem()
        {
            var cards = new DuelCard[CardsPerChar];
            BaseCards().CopyTo(cards, 0);
                        // La armadura se PAGA: es el agarre más lento del juego (pierde
            // con el agarre común) y pega poco. Con vel 7 / 8 de daño no
            // perdía con nada y el Golem se iba a 66% en el lab.
            cards[Sig1] = new DuelCard { Name = "Roca Rodante (R)", Short = "R", Kind = DuelKind.Grab, Speed = 3, Damage = 5, Height = DuelHeight.None, Armor = true };
            cards[Sig2] = new DuelCard { Name = "Cabezazo (H)", Short = "H", Kind = DuelKind.Strike, Speed = 3, Damage = 9, Height = DuelHeight.High };
            cards[Escape] = EscapeCard();
            return new DuelChar
            {
                Name = "GOLEM",
                Tag = "Grappler: 5 agarres y su Roca AGUANTA el golpe y te agarra igual",
                Cards = cards,
                DeckCounts = Counts,
                // 8→4 con los rounds (2026-07-26): el bonus es POR ROUND y
                // 8 sobre 26 era +31% de vida (el lab lo mandó a 63%).
                HpBonus = 4,
            };
        }
    }

    public static class DuelConfig
    {
        // Diales de balance. No son const a propósito: el lab los barre en
        // A/B (como SimConfig en el modo clásico) y los restaura después.
        // ROUNDS (2026-07-26, DUELO.md §12): MaxHp pasó a ser vida POR ROUND
        // (46→26); se juega al mejor de 3 y cada round resetea todo menos
        // la lectura.
        public static int MaxHp = 26;
        public static int RoundsToWin = 2;
        // Ley 7 (dial anotado): true = solo el ESCAPE garantizado en la mano
        // inicial, el resto al azar — más dispersión de fuerza entre manos.
        public static bool LooseOpening = false;
        public static int HandLimit = 8;
        public static int DrawPerTurn = 1;   // ambos roban TODOS los turnos (no hay turno activo)
        public static int OpeningRandom = 2; // + guardia alta, baja, agarre y escape garantizados
        public static int GuardDraw = 1;     // cartas que roba el que defiende BIEN (Ley 2: la defensa paga en economía).
                                             // 2→1 (2026-07-26, jugado por Patricio): con 2 defender se SIENTE OP —
                                             // el lab decía lo contrario (duelotune), pero el lab es IA vs IA y la
                                             // mano del humano manda. Con truco: roba 2 · retruco 3 · vale cuatro 4.
        public static int HardTurnCap = 40;  // red de seguridad; el mazo suele cerrar antes

        // ---- LOS CANTOS (DUELO.md §11) ----
        public static int EnvidoChip = 4;     // lo que cobra el ganador del envido querido (6→4 con rounds: proporcional a la vida de 26)
        public static int EnvidoFoldChip = 1; // el "no quiero" al envido paga al cantor
        public static int TrucoMaxLevel = 3;  // 1=TRUCO ×2 · 2=RETRUCO ×3 · 3=VALE CUATRO ×4
        public static bool TrucoPrizeToo = false; // dial: el quiero multiplica también el premio +DAÑO
        public static int TrucoFoldBonus = 0;     // dial: chip extra del no quiero (el peaje del cobarde)
    }

    // Todo lo que pasó en un turno, para el teatro, el log y los tests.
    public class DuelTurnResult
    {
        public int Card0 = -1, Card1 = -1;   // -1 = sin cartas en mano
        public int Dmg0, Dmg1;               // daño RECIBIDO por cada lado
        public int Chip0, Chip1;             // cuánto de eso fue chip
        public bool Hit0, Hit1;              // recibió un golpe REAL (el chip no cuenta)
        public bool Guarded0, Guarded1;      // defendió bien
        public bool WrongGuard0, WrongGuard1;// defendió la altura equivocada (o estaba derribado)
        // Por QUÉ falló la guardia: derribado (no bloquea) vs altura errada.
        // Sin esto la UI confundía la CONSECUENCIA (el premio derribo de este
        // turno) con la CAUSA, y cantaba "derribado" en el turno 1.
        public bool GuardWasDown0, GuardWasDown1;
        public bool Escaped0, Escaped1;
        public bool Trade, Tech;
        public bool Armor;                   // el aguante del grappler decidió el turno
        public int Winner = -1;              // ganó el intercambio limpio
        public int PrizeSide = -1, PrizeCard = -1;
        public DuelPrize Prize = DuelPrize.None;
        public int PrizeDamage;              // daño extra cobrado por +DAÑO
        public int PunishSide = -1, PunishCard = -1;
        public int PunishDamage;
        public int Truco;                    // nivel de truco COBRADO este turno (0 = no había o no se cobró)
        public bool RoundEnd;                // terminó un round este turno
        public int RoundWinner = -1;         // quién lo ganó (-1 = doble KO parejo)
        public bool KdNext0, KdNext1;
        public bool Returned0, Returned1;    // la guardia volvió a la mano
        public int Drew0, Drew1;
        public bool TimeOver;

        public int Card(int i) => i == 0 ? Card0 : Card1;
        public int Dmg(int i) => i == 0 ? Dmg0 : Dmg1;
        public int Chip(int i) => i == 0 ? Chip0 : Chip1;
        public bool Hit(int i) => i == 0 ? Hit0 : Hit1;
        public bool Guarded(int i) => i == 0 ? Guarded0 : Guarded1;
        public bool WrongGuard(int i) => i == 0 ? WrongGuard0 : WrongGuard1;
        public bool GuardWasDown(int i) => i == 0 ? GuardWasDown0 : GuardWasDown1;
        public bool KdNext(int i) => i == 0 ? KdNext0 : KdNext1;
        public bool Returned(int i) => i == 0 ? Returned0 : Returned1;
        public int Drew(int i) => i == 0 ? Drew0 : Drew1;
    }

    // ---- LOS CANTOS (DUELO.md §11): la capa de apuestas del truco ----
    // La negociación (quién canta, quién sube, quién acepta) vive AFUERA de
    // la sim (IA/UI/protocolo); la sim valida y aplica el RESULTADO.

    public struct DuelEnvidoResult
    {
        public int Cantor;
        public bool Quiero;
        public int Winner;          // -1 = no quiero o empate
        public int Tanto0, Tanto1;  // -1 = no se compararon (no quiero)
        public int Chip;            // daño cobrado (por ganar o por el fold)
    }

    public struct DuelTrucoResult
    {
        public int Caller;          // quién cantó el ÚLTIMO nivel
        public int Level;           // 1..3 (×2/×3/×4)
        public bool Quiero;
        public int Chip;            // lo que cobró el caller si no quisieron
    }

    public class DuelSim
    {
        public readonly DuelChar[] Chr = new DuelChar[2];
        public readonly int[] CharIdx = new int[2];
        public readonly int[] Hp = new int[2];
        public readonly List<int>[] Deck = { new List<int>(), new List<int>() };
        public readonly List<int>[] Hand = { new List<int>(), new List<int>() };
        public readonly List<int>[] Discard = { new List<int>(), new List<int>() }; // público
        public readonly List<int>[] Spent = { new List<int>(), new List<int>() };   // el escape gastado: no vuelve nunca
        public readonly bool[] KnockedDown = new bool[2];  // afecta el turno EN CURSO: tu guardia no bloquea
        public readonly int[] DeckOuts = new int[2];
        public int Turn;
        public bool Over;
        public int Winner = -1;

        // ---- rounds (al mejor de 3, DUELO.md §12) ----
        public readonly int[] RoundWins = new int[2];
        public int Round = 1;

        // ---- los cantos (todo POR ROUND) ----
        public bool EnvidoUsed;              // un envido por round
        public bool FirstBlood;              // cualquier daño del round cierra la ventana del envido
        public int PublicTanto = -1;         // el tanto del ganador del envido: PÚBLICO (siembra la lectura)
        public int PublicTantoSide = -1;
        public int TrucoLevel;               // multiplicador ARMADO hasta que alguien gane un intercambio (0 = nada)
        public int TrucoCaller = -1;
        public bool TrucoChainUsed;          // UNA cadena de truco por round (como la mano del truco real)

        public bool CanEnvido => !Over && !EnvidoUsed && !FirstBlood;
        public bool CanTruco => !Over && TrucoLevel == 0 && !TrucoChainUsed;
        public static int TrucoMult(int level) => level + 1;   // 1→×2 · 2→×3 · 3→×4

        // ---- decisión pendiente (premio del ganador o castigo del defensor) ----
        public int PendingSide = -1;
        public bool PendingIsPunish;
        public bool AwaitingChoice => PendingSide >= 0;

        // Un stream POR LADO: con uno compartido, el mazo que se baraja
        // segundo hereda la correlación del primero y aparece un sesgo de
        // lado medible (lab: P0 52.3% con bots random, 48.5% con heurística
        // — el signo se daba vuelta con la política, la firma del artefacto).
        readonly uint[] _rng = new uint[2];
        DuelTurnResult _r = new DuelTurnResult();
        int _victim = -1;
        int _prizeMult = 1;   // el multiplicador del truco cobrado, por si el premio también dobla
        bool _finished;
        bool _pendingTimeOver;
        public DuelTurnResult LastResult => _r;

        public DuelCard Def(int side, int card) => Chr[side].Cards[card];
        public int MaxHpOf(int side) => DuelConfig.MaxHp + Chr[side].HpBonus;

        // ONLINE (lockstep ESPEJADO): cada cliente construye la sim con él
        // mismo como lado 0, y los streams de RNG viajan con el JUGADOR
        // (streamTag = 0 para el host, 1 para el invitado), no con el índice
        // local. Como toda la resolución de DuelSim es simétrica por lado,
        // las dos sims espejadas barajan idéntico y quedan en lockstep sin
        // tocar una línea de la UI (que asume "vos = lado 0").
        public DuelSim(int seed, int char0 = DuelCatalog.GraveIdx, int char1 = DuelCatalog.GraveIdx,
            int streamTag0 = 0, int streamTag1 = 1)
        {
            _rng[0] = Mix((uint)seed * 0x9E3779B9u + (uint)(streamTag0 + 1) * 0x85EBCA6Bu);
            _rng[1] = Mix((uint)seed * 0x9E3779B9u + (uint)(streamTag1 + 1) * 0x85EBCA6Bu);
            CharIdx[0] = char0; CharIdx[1] = char1;
            for (int s = 0; s < 2; s++)
            {
                Chr[s] = DuelCatalog.Chars[CharIdx[s]];
                DealSide(s);
            }
        }

        // Reparto de un round: mazo completo, mano garantizada, vida llena.
        void DealSide(int s)
        {
            Hp[s] = DuelConfig.MaxHp + Chr[s].HpBonus;
            Deck[s].Clear(); Hand[s].Clear(); Discard[s].Clear(); Spent[s].Clear();
            KnockedDown[s] = false;
            DeckOuts[s] = 0;
            for (int c = 0; c < DuelCatalog.CardsPerChar; c++)
                for (int n = 0; n < Chr[s].DeckCounts[c]; n++)
                    Deck[s].Add(c);
            int guaranteed;
            if (DuelConfig.LooseOpening)
            {
                // Ley 7: solo la válvula garantizada — el reparto dispersa fuerza
                MoveDeckToHand(s, DuelCatalog.Escape);
                guaranteed = 1;
            }
            else
            {
                // mano garantizada: las dos guardias, un agarre y el escape
                MoveDeckToHand(s, DuelCatalog.GuardHigh);
                MoveDeckToHand(s, DuelCatalog.GuardLow);
                MoveDeckToHand(s, DuelCatalog.Throw);
                MoveDeckToHand(s, DuelCatalog.Escape);
                guaranteed = 4;
            }
            Shuffle(s);
            int handSize = 4 + DuelConfig.OpeningRandom;   // 6 con los defaults, en ambos modos
            for (int n = guaranteed; n < handSize; n++)
            {
                int card = TopOfDeck(s);
                if (card >= 0) Hand[s].Add(card);
            }
            Hand[s].Sort();
        }

        // Round nuevo: TODO se resetea menos el marcador (y la lectura, que
        // vive en los jugadores, no acá).
        void ResetRound()
        {
            Round++;
            for (int s = 0; s < 2; s++) DealSide(s);
            EnvidoUsed = false;
            FirstBlood = false;
            PublicTanto = -1; PublicTantoSide = -1;
            TrucoLevel = 0; TrucoCaller = -1; TrucoChainUsed = false;
            _pendingTimeOver = false;
        }

        // Cierra el round: anota el marcador y, si el match no terminó,
        // reparte el siguiente. rw = -1 (doble KO parejo): nadie anota.
        void EndRound(int rw)
        {
            _r.RoundEnd = true;
            _r.RoundWinner = rw;
            if (rw >= 0)
            {
                RoundWins[rw]++;
                if (RoundWins[rw] >= DuelConfig.RoundsToWin) { Over = true; Winner = rw; return; }
            }
            ResetRound();
        }

        void MoveDeckToHand(int side, int card)
        {
            Deck[side].Remove(card);
            Hand[side].Add(card);
        }

        // mezclador de avalancha: descorrelaciona seeds vecinas y lados
        static uint Mix(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352du;
            x ^= x >> 15; x *= 0x846ca68bu;
            x ^= x >> 16;
            return x == 0 ? 0x9E3779B9u : x;
        }

        uint NextRng(int side)
        {
            uint r = _rng[side];
            r ^= r << 13; r ^= r >> 17; r ^= r << 5;
            _rng[side] = r;
            return r;
        }

        void Shuffle(int side)
        {
            var pile = Deck[side];
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = (int)(NextRng(side) % (uint)(i + 1));
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
        }

        void SortHands() { Hand[0].Sort(); Hand[1].Sort(); }

        // ---- los cantos (DUELO.md §11) ----

        // El TANTO: tus dos golpes de la MISMA altura suman su VELOCIDAD (el
        // palo ES la altura). Con un solo golpe, esa velocidad; sin golpes, 0.
        // Por qué velocidad y no daño (Patricio, 2026-07-26): rápido=débil,
        // así que el que gana el envido NO es el favorito del combate — como
        // en el truco real, donde el 33 no son las cartas que ganan la mano.
        // Desacopla las dos apuestas y mata la bola de nieve. Y como
        // rápido=BAJO, un tanto grande filtra "tiene los bajitos" (con la Y
        // de Jaina, vel 11 y ALTA, como la mentirosa del sistema).
        public int Tanto(int side)
        {
            int bestPair = 0, bestSingle = 0;
            for (int h = 0; h < 2; h++)
            {
                int a = 0, b = 0;
                foreach (int c in Hand[side])
                {
                    var d = Def(side, c);
                    if (d.Kind != DuelKind.Strike || (int)d.Height != h) continue;
                    if (d.Speed >= a) { b = a; a = d.Speed; }
                    else if (d.Speed > b) b = d.Speed;
                    if (d.Speed > bestSingle) bestSingle = d.Speed;
                }
                if (b > 0 && a + b > bestPair) bestPair = a + b;
            }
            return bestPair > 0 ? bestPair : bestSingle;
        }

        // ENVIDO (apuesta de INFORMACIÓN, solo hasta la primera sangre, una
        // por partida). Querido: gana el tanto mayor, cobra EnvidoChip y su
        // tanto se hace público; del perdedor solo se sabe que es menor
        // ("son buenas"). No querido: el cantor cobra EnvidoFoldChip y nadie
        // muestra nada. Empate: nadie cobra, nada se publica.
        public DuelEnvidoResult ResolveEnvido(int cantor, bool quiero)
        {
            var er = new DuelEnvidoResult { Cantor = cantor, Quiero = quiero, Winner = -1, Tanto0 = -1, Tanto1 = -1 };
            if (!CanEnvido || cantor < 0 || cantor > 1) return er;
            EnvidoUsed = true;
            if (!quiero)
            {
                er.Chip = DuelConfig.EnvidoFoldChip;
                DirectDamage(1 - cantor, er.Chip);
                return er;
            }
            er.Tanto0 = Tanto(0); er.Tanto1 = Tanto(1);
            int w = er.Tanto0 > er.Tanto1 ? 0 : er.Tanto1 > er.Tanto0 ? 1 : -1;
            er.Winner = w;
            if (w >= 0)
            {
                PublicTanto = w == 0 ? er.Tanto0 : er.Tanto1;
                PublicTantoSide = w;
                er.Chip = DuelConfig.EnvidoChip;
                DirectDamage(1 - w, er.Chip);
            }
            return er;
        }

        // TRUCO (apuesta de SANGRE). level = nivel ALCANZADO en la
        // negociación (1..3), lastCaller = quién cantó ese último nivel.
        // Querido: el multiplicador queda ARMADO hasta que alguien gane un
        // intercambio (guardia-guardia o trade no lo disipan). No querido:
        // el caller cobra nivel+1 de chip y no se arma nada.
        public DuelTrucoResult ResolveTruco(int lastCaller, int level, bool quiero)
        {
            var tr = new DuelTrucoResult { Caller = lastCaller, Level = level, Quiero = quiero };
            if (!CanTruco || lastCaller < 0 || lastCaller > 1 || level < 1) return tr;
            if (level > DuelConfig.TrucoMaxLevel) level = DuelConfig.TrucoMaxLevel;
            tr.Level = level;
            TrucoChainUsed = true;   // una cadena por round, querida o no
            if (quiero)
            {
                TrucoLevel = level;
                TrucoCaller = lastCaller;
            }
            else
            {
                tr.Chip = level + 1 + DuelConfig.TrucoFoldBonus;   // TRUCO no querido 2 · RETRUCO 3 · VALE CUATRO 4
                DirectDamage(1 - lastCaller, tr.Chip);
            }
            return tr;
        }

        // Daño fuera del intercambio (chips de cantos). Puede cerrar el
        // ROUND: rechazar un vale cuatro con 3 de vida es perderlo.
        void DirectDamage(int side, int dmg)
        {
            if (dmg <= 0) return;
            Hp[side] = Math.Max(0, Hp[side] - dmg);
            FirstBlood = true;
            if (Hp[side] <= 0 && !Over) EndRound(1 - side);
        }

        // ---- turno ----

        public void StartTurn()
        {
            if (Over) return;
            Turn++;
            if (Turn > 1)
                for (int s = 0; s < 2; s++)
                    for (int n = 0; n < DuelConfig.DrawPerTurn; n++) DrawOne(s);
            SortHands();
        }

        // Toda carta de la mano es legal siempre (no hay wild swing): el
        // derribo no prohíbe la guardia, la APAGA.
        public bool Legal(int side, int handIdx) => handIdx >= 0 && handIdx < Hand[side].Count;

        // Revelación simultánea. Si queda un premio o un castigo por decidir,
        // AwaitingChoice queda true: ChoosePrize / Punish lo cierran.
        public DuelTurnResult Resolve(int handIdx0, int handIdx1)
        {
            _r = new DuelTurnResult();
            _victim = -1;
            _prizeMult = 1;
            PendingSide = -1; PendingIsPunish = false;
            _finished = false;
            if (Over) { _r.TimeOver = true; return _r; }

            _r.Card0 = TakeCard(0, handIdx0);
            _r.Card1 = TakeCard(1, handIdx1);
            Fight(_r.Card0, _r.Card1);
            if (!AwaitingChoice) FinishTurn();
            else SortHands();
            return _r;
        }

        int TakeCard(int side, int handIdx)
        {
            if (!Legal(side, handIdx)) return -1;  // mano vacía: se come lo que venga
            int card = Hand[side][handIdx];
            Hand[side].RemoveAt(handIdx);
            return card;
        }

        // ---- la tabla ----

        void Fight(int c0, int c1)
        {
            // el ESCAPE congela el turno: no pasa nada. Es la válvula (una por
            // partida) y por eso es la respuesta al derribo.
            bool e0 = c0 >= 0 && Def(0, c0).Kind == DuelKind.Escape;
            bool e1 = c1 >= 0 && Def(1, c1).Kind == DuelKind.Escape;
            if (e0) _r.Escaped0 = true;
            if (e1) _r.Escaped1 = true;
            if (e0 || e1) return;

            if (c0 < 0 && c1 < 0) return;
            if (c0 < 0) { Unopposed(1, c1); return; }
            if (c1 < 0) { Unopposed(0, c0); return; }

            var k0 = Def(0, c0).Kind; var k1 = Def(1, c1).Kind;

            if (k0 == DuelKind.Strike && k1 == DuelKind.Strike)
            {
                int s0 = Def(0, c0).Speed, s1 = Def(1, c1).Speed;
                if (s0 == s1) { Trade(c0, c1); return; }
                int w = s0 > s1 ? 0 : 1;
                Land(w, w == 0 ? c0 : c1);
                return;
            }
            // el golpe le gana al agarre SIN mirar velocidad... salvo AGUANTE:
            // el agarre con armor se come el golpe y conecta igual (trade).
            if (k0 == DuelKind.Strike && k1 == DuelKind.Grab) { StrikeVsGrab(0, c0, c1); return; }
            if (k1 == DuelKind.Strike && k0 == DuelKind.Grab) { StrikeVsGrab(1, c1, c0); return; }

            if (k0 == DuelKind.Strike && k1 == DuelKind.Guard) { StrikeVsGuard(0, c0); return; }
            if (k1 == DuelKind.Strike && k0 == DuelKind.Guard) { StrikeVsGuard(1, c1); return; }

            // agarre vs agarre: desempata la velocidad, empate = TECH. Con
            // el mismo agarre de los dos lados siempre es TECH; el Golem, que
            // tiene un SEGUNDO agarre más rápido, es quien usa esta rama.
            if (k0 == DuelKind.Grab && k1 == DuelKind.Grab)
            {
                int g0 = Def(0, c0).Speed, g1 = Def(1, c1).Speed;
                if (g0 == g1) { _r.Tech = true; return; }
                int gw = g0 > g1 ? 0 : 1;
                Land(gw, gw == 0 ? c0 : c1);
                return;
            }

            if (k0 == DuelKind.Grab) { Land(0, c0); return; }   // vs guardia
            if (k1 == DuelKind.Grab) { Land(1, c1); return; }

            // guardia vs guardia: no pasa nada (vuelven a la mano, sin robo)
        }

        void StrikeVsGrab(int strikeSide, int strikeCard, int grabCard)
        {
            int grabSide = 1 - strikeSide;
            if (!Def(grabSide, grabCard).Armor) { Land(strikeSide, strikeCard); return; }
            // aguante: los dos cobran, nadie cobra premio (es un cambio)
            _r.Armor = true;
            _r.Trade = true;
            Damage(grabSide, Def(strikeSide, strikeCard).Damage, chip: false);
            Damage(strikeSide, Def(grabSide, grabCard).Damage, chip: false);
        }

        // El rival no tenía cartas: si atacaste, conecta.
        void Unopposed(int side, int card)
        {
            if (Def(side, card).IsAttack) Land(side, card);
        }

        void Trade(int c0, int c1)
        {
            _r.Trade = true;
            Damage(0, Def(1, c1).Damage, chip: false);
            Damage(1, Def(0, c0).Damage, chip: false);
            // sin ganador limpio: nadie cobra premio
        }

        void StrikeVsGuard(int atkSide, int card)
        {
            var atk = Def(atkSide, card);
            int def = 1 - atkSide;
            var guard = Def(def, _r.Card(def));
            // derribado: la guardia NO bloquea (dura un solo turno)
            bool down = KnockedDown[def];
            bool blocks = !down && guard.Height == atk.Height;
            if (!blocks)
            {
                if (def == 0) { _r.WrongGuard0 = true; _r.GuardWasDown0 = down; }
                else { _r.WrongGuard1 = true; _r.GuardWasDown1 = down; }
                Land(atkSide, card);
                return;
            }
            if (def == 0) _r.Guarded0 = true; else _r.Guarded1 = true;
            if (atk.Chip > 0) Damage(def, atk.Chip, chip: true);
            // El truco también se cobra BLOQUEANDO (Patricio, 2026-07-25):
            // la guardia acertada gana la apuesta en SU moneda — cartas
            // multiplicadas (Ley 2 aplicada al canto). Robás 1 normal, 2 con
            // truco, 3 con retruco, 4 con vale cuatro.
            int draw = DuelConfig.GuardDraw;
            if (TrucoLevel > 0)
            {
                draw *= TrucoMult(TrucoLevel);
                _r.Truco = TrucoLevel;
                TrucoLevel = 0;
                TrucoCaller = -1;
            }
            for (int n = 0; n < draw; n++)
            {
                DrawOne(def);
                if (def == 0) _r.Drew0++; else _r.Drew1++;
            }
            if (atk.PunishOnGuard) BeginPunish(def);
        }

        void Land(int side, int card)
        {
            var d = Def(side, card);
            int victim = 1 - side;
            // el truco armado se COBRA acá: multiplica el golpe que ganó el
            // intercambio, sea de quien sea (el riesgo del canto es simétrico).
            // Solo el golpe: el premio va a valor normal (dial anotado en §11).
            int mult = 1;
            if (TrucoLevel > 0)
            {
                mult = TrucoMult(TrucoLevel);
                _r.Truco = TrucoLevel;
                if (DuelConfig.TrucoPrizeToo) _prizeMult = mult;
                TrucoLevel = 0;
                TrucoCaller = -1;
            }
            Damage(victim, d.Damage * mult, chip: false);
            _r.Winner = side;
            _victim = victim;
            if (d.FreeKnockdown) SetKd(victim);
            BeginPrize(side);
        }

        void Damage(int side, int dmg, bool chip)
        {
            if (side == 0) { _r.Dmg0 += dmg; if (chip) _r.Chip0 += dmg; else _r.Hit0 = true; }
            else { _r.Dmg1 += dmg; if (chip) _r.Chip1 += dmg; else _r.Hit1 = true; }
        }

        void SetKd(int victim)
        {
            if (victim == 0) _r.KdNext0 = true; else _r.KdNext1 = true;
        }

        // ---- premio del ganador: +DAÑO o DERRIBO ----

        void BeginPrize(int side)
        {
            PendingSide = side;
            PendingIsPunish = false;
            // sin golpe para quemar, la única opción es el derribo: se cierra sola
            if (PrizeFuel(side).Count == 0) ChoosePrize(DuelPrize.Knockdown);
        }

        // Índices de mano quemables como +DAÑO (golpes y agarres).
        public List<int> PrizeFuel(int side)
        {
            var list = new List<int>();
            if (PendingSide != side || PendingIsPunish) return list;
            for (int i = 0; i < Hand[side].Count; i++)
                if (Def(side, Hand[side][i]).IsAttack) list.Add(i);
            return list;
        }

        public bool ChoosePrize(DuelPrize prize, int handIdx = -1)
        {
            int side = PendingSide;
            if (side < 0 || PendingIsPunish) return false;
            if (prize == DuelPrize.Damage)
            {
                if (handIdx < 0 || handIdx >= Hand[side].Count) return false;
                int card = Hand[side][handIdx];
                var d = Def(side, card);
                if (!d.IsAttack) return false;
                Hand[side].RemoveAt(handIdx);
                Discard[side].Add(card);
                Damage(_victim, d.Damage * _prizeMult, chip: false);
                _r.PrizeCard = card;
                _r.PrizeDamage = d.Damage * _prizeMult;
            }
            else
            {
                prize = DuelPrize.Knockdown;
                SetKd(_victim);
            }
            _r.PrizeSide = side;
            _r.Prize = prize;
            PendingSide = -1;
            FinishTurn();
            return true;
        }

        // ---- castigo del defensor (la Y de Jaina defendida) ----

        void BeginPunish(int side)
        {
            PendingSide = side;
            PendingIsPunish = true;
            _victim = 1 - side;
            foreach (int c in Hand[side])
                if (Def(side, c).IsAttack) return;
            Punish(-1); // no tiene con qué castigar
        }

        // handIdx −1 = no castigar.
        public bool Punish(int handIdx)
        {
            int side = PendingSide;
            if (side < 0 || !PendingIsPunish) return false;
            if (handIdx >= 0)
            {
                if (handIdx >= Hand[side].Count) return false;
                int card = Hand[side][handIdx];
                var d = Def(side, card);
                if (!d.IsAttack) return false;
                Hand[side].RemoveAt(handIdx);
                Discard[side].Add(card);
                Damage(_victim, d.Damage, chip: false);
                _r.PunishSide = side;
                _r.PunishCard = card;
                _r.PunishDamage = d.Damage;
            }
            PendingSide = -1;
            PendingIsPunish = false;
            FinishTurn();
            return true;
        }

        // ---- cierre del turno ----

        void FinishTurn()
        {
            if (_finished) return;
            _finished = true;

            for (int s = 0; s < 2; s++)
            {
                int card = _r.Card(s);
                if (card < 0) continue;
                var d = Def(s, card);
                if (d.Kind == DuelKind.Escape) { Spent[s].Add(card); continue; }
                // la guardia vuelve si no te pegaron (el chip no cuenta)
                bool hit = _r.Hit(s);
                if (d.Kind == DuelKind.Guard && !hit)
                {
                    if (s == 0) _r.Returned0 = true; else _r.Returned1 = true;
                    AddToHand(s, card);
                }
                else Discard[s].Add(card);
            }

            KnockedDown[0] = _r.KdNext0;
            KnockedDown[1] = _r.KdNext1;

            Hp[0] = Math.Max(0, Hp[0] - _r.Dmg0);
            Hp[1] = Math.Max(0, Hp[1] - _r.Dmg1);
            if (_r.Dmg0 + _r.Dmg1 > 0) FirstBlood = true;   // cierra la ventana del envido

            SortHands();
            if (Hp[0] <= 0 || Hp[1] <= 0)
            {
                int rw = Hp[0] <= 0 && Hp[1] <= 0
                    ? (Hp[0] == Hp[1] ? -1 : (Hp[0] > Hp[1] ? 0 : 1))
                    : (Hp[0] <= 0 ? 1 : 0);
                EndRound(rw);
            }
            else if (_pendingTimeOver)   // el mazo del round se agotó dos veces
            {
                _pendingTimeOver = false;
                _r.TimeOver = true;
                EndRound(JudgeRound());
            }
            else if (Turn >= DuelConfig.HardTurnCap)
            {
                // red de seguridad global: cierra el MATCH por marcador y vida
                Over = true;
                _r.TimeOver = true;
                Winner = RoundWins[0] != RoundWins[1]
                    ? (RoundWins[0] > RoundWins[1] ? 0 : 1)
                    : JudgeRound();
            }
        }

        int JudgeRound() => Hp[0] == Hp[1] ? -1 : (Hp[0] > Hp[1] ? 0 : 1);

        // ---- mazo y mano ----

        int TopOfDeck(int side)
        {
            if (Deck[side].Count == 0 && !RefillDeck(side)) return -1;
            int card = Deck[side][Deck[side].Count - 1];
            Deck[side].RemoveAt(Deck[side].Count - 1);
            return card;
        }

        void DrawOne(int side)
        {
            int card = TopOfDeck(side);
            if (card >= 0) AddToHand(side, card);
        }

        void AddToHand(int side, int card)
        {
            if (Hand[side].Count >= DuelConfig.HandLimit) Discard[side].Add(card);
            else Hand[side].Add(card);
        }

        // Mazo vacío: la PRIMERA vez se remezcla el descarte (el escape gastado
        // nunca vuelve); la SEGUNDA es TIME OVER por vida.
        bool RefillDeck(int side)
        {
            DeckOuts[side]++;
            if (DeckOuts[side] >= 2) { TimeOver(); return false; }
            if (Discard[side].Count == 0) { TimeOver(); return false; }
            Deck[side].AddRange(Discard[side]);
            Discard[side].Clear();
            Shuffle(side);
            return true;
        }

        // El time over del round se procesa al CERRAR el turno (marcarlo acá
        // en medio de un robo dejaría el reparto del round nuevo a mitad de
        // una resolución).
        void TimeOver()
        {
            if (!Over) _pendingTimeOver = true;
        }
    }
}
