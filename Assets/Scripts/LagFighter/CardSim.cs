using System;
using System.Collections.Generic;

namespace LagFighter
{
    // ---- MODO CARTAS (2026-07-21): copia de Yomi 2 sin combos ni supers ----
    // Cada acción es una carta; el mazo es el de Grave con sus números reales
    // (rulebook v7.7 + Mizuumi — ver YOMI2-CARDS.md). Turnos ALTERNADOS: el
    // activo juega su opener boca abajo, el otro boca arriba, se revela y
    // resuelve: attack > throw > block/dodge > attack, con alturas high/low/mid,
    // speed (empates al activo), proyectiles por nivel, knockdown, exchange,
    // remezcla única y time over.
    // Pura y determinista (sin UnityEngine): cada regla es un test en
    // Tools/SimTests. La UI y el teatro de MatchController solo LEEN.

    public enum CardKind { Attack = 0, Throw = 1, Block = 2, Dodge = 3 }
    public enum CardHeight { Mid = 0, High = 1, Low = 2 }

    public struct CardDef
    {
        public string Name;      // nombre de la carta
        public string Short;     // etiqueta corta para chips/logs
        public CardKind Kind;
        public int Speed;        // mayor = más rápido (Yomi 2 real)
        public int Damage;
        public int BlockDamage;  // chip si te la bloquean (especiales)
        public CardHeight Height;    // altura del ataque
        public bool Projectile;
        public int ProjLevel;        // nivel de proyectil (vs proyectil se comparan niveles)
        public bool Recurring;       // vuelve a la mano si abriste con ella y no te pegaron
        public bool Lockdown;        // el que la bloquea NO roba carta
        public bool UnsafeOnBlock;   // bloqueada: te devuelven UN ataque/throw
        public bool KnockdownOnHit;  // derriba (sin combos, siempre que pegue)
        public bool BlocksHigh, BlocksLow; // qué alturas cubre (blocks)
        public bool IsNormal;        // tiene ícono de exchange (todas las normales)

        public bool IsStrike => Kind == CardKind.Attack && !Projectile;
        public bool Blocks(CardHeight h) =>
            h == CardHeight.Mid ? (BlocksHigh || BlocksLow)
            : h == CardHeight.High ? BlocksHigh : BlocksLow;
    }

    public static class CardCatalog
    {
        public const int AttackA = 0;   // Quick Attack  · low  · s8 d3
        public const int AttackB = 1;   // Light Attack  · low  · s7 d4
        public const int AttackC = 2;   // Medium Attack · mid  · s6 d5
        public const int AttackD = 3;   // Heavy Attack  · high · s5 d6
        public const int AttackE = 4;   // Power Attack  · high · s4 d7
        public const int Throw = 5;     // s5 d7 · knockdown
        public const int Dodge = 6;     // esquiva; vs strike devolvés un golpe
        public const int LowBlock = 7;  // bloquea low+mid · roba 1 · recurring
        public const int HighBlock = 8; // bloquea high+mid · roba 1 · recurring
        public const int SpecialX = 9;  // Lightning Cloud · proyectil Nv1 · s7 d8 chip4 · recurring · lockdown
        public const int SpecialY = 10; // Stormborne Sword · s11 d10 chip2 · UNSAFE (el reversal)
        public const int SpecialZ = 11; // Whirlwind · high · s7 d7 chip1 (el mixup rápido de altura)

        public static readonly CardDef[] All =
        {
            new CardDef { Name = "Golpe Rápido (A)", Short = "A", Kind = CardKind.Attack, Speed = 8, Damage = 3, Height = CardHeight.Low, IsNormal = true },
            new CardDef { Name = "Golpe Ligero (B)", Short = "B", Kind = CardKind.Attack, Speed = 7, Damage = 4, Height = CardHeight.Low, IsNormal = true },
            new CardDef { Name = "Golpe Medio (C)", Short = "C", Kind = CardKind.Attack, Speed = 6, Damage = 5, Height = CardHeight.Mid, IsNormal = true },
            new CardDef { Name = "Golpe Pesado (D)", Short = "D", Kind = CardKind.Attack, Speed = 5, Damage = 6, Height = CardHeight.High, IsNormal = true },
            new CardDef { Name = "Golpe Poderoso (E)", Short = "E", Kind = CardKind.Attack, Speed = 4, Damage = 7, Height = CardHeight.High, IsNormal = true },
            new CardDef { Name = "Agarre", Short = "AGR", Kind = CardKind.Throw, Speed = 5, Damage = 7, KnockdownOnHit = true, IsNormal = true },
            new CardDef { Name = "Esquive", Short = "ESQ", Kind = CardKind.Dodge, IsNormal = true },
            new CardDef { Name = "Bloqueo Bajo", Short = "BJO", Kind = CardKind.Block, BlocksLow = true, Recurring = true, IsNormal = true },
            new CardDef { Name = "Bloqueo Alto", Short = "ALT", Kind = CardKind.Block, BlocksHigh = true, Recurring = true, IsNormal = true },
            new CardDef { Name = "Nube Eléctrica (X)", Short = "X", Kind = CardKind.Attack, Speed = 7, Damage = 8, BlockDamage = 4, Projectile = true, ProjLevel = 1, Recurring = true, Lockdown = true },
            new CardDef { Name = "Espada Tormenta (Y)", Short = "Y", Kind = CardKind.Attack, Speed = 11, Damage = 10, BlockDamage = 2, UnsafeOnBlock = true },
            new CardDef { Name = "Torbellino (Z)", Short = "Z", Kind = CardKind.Attack, Speed = 7, Damage = 7, BlockDamage = 1, Height = CardHeight.High },
        };

        // Mazo de Grave sin supers ni ability: 24 cartas.
        public static readonly int[] DeckCounts = { 2, 2, 2, 2, 2, 3, 3, 1, 1, 2, 2, 2 };
    }

    public static class CardConfig
    {
        public const int MaxHp = 45;          // Grave real: 90 — acá SIN combos el daño
                                              // por turno es la mitad; 45 mantiene la
                                              // duración de partida del original.
        public const int HandLimit = 12;
        public const int DrawPerTurn = 2;
        public const int FirstTurnDraw = 1;   // el que empieza roba 1 en su primer turno
        public const int OpeningRandomDraws = 4; // + Low Block, High Block y un Agarre fijos
        public const int ExchangesPerTurn = 2;   // innate de Grave: puede DOS exchanges
        public const int KnockdownMinSpeed = 10; // vs derribado, tus moves lentos suben a 10
    }

    // Todo lo que pasó en un combate, para el teatro, el log y los tests.
    public struct CardTurnResult
    {
        public int Card0, Card1;        // openers FINALES (id de catálogo, tras wild swing)
        public int Wild0, Wild1;        // cuántas cartas descartó el wild swing de cada lado
        public int Dmg0, Dmg1;          // daño total RECIBIDO por cada lado (incluye chip y hit-back)
        public int Chip0, Chip1;        // cuánto de eso fue block damage
        public bool Blocked0, Blocked1;     // ese lado bloqueó BIEN (altura correcta)
        public bool WrongBlock0, WrongBlock1; // bloqueó la altura EQUIVOCADA (comió el golpe)
        public bool Dodged0, Dodged1;   // ese lado esquivó el ataque rival
        public bool Thrown0, Thrown1;   // ese lado fue agarrado
        public bool ProjCancel;         // proyectil vs proyectil del mismo nivel: se anulan
        public bool KdNext0, KdNext1;   // derribado para el PRÓXIMO combate
        public int HitBackSide;         // quién devolvió el castigo (-1 = nadie)
        public int HitBackCard;         // con qué carta (-1)
        public bool Returned0, Returned1;   // su carta recurring volvió a la mano
        public int Drew0, Drew1;        // cartas robadas por bloquear
        public int Active;              // de quién era el turno
        public bool TimeOver;

        public int Card(int i) => i == 0 ? Card0 : Card1;
        public int Dmg(int i) => i == 0 ? Dmg0 : Dmg1;
        public int Chip(int i) => i == 0 ? Chip0 : Chip1;
        public bool Blocked(int i) => i == 0 ? Blocked0 : Blocked1;
        public bool Dodged(int i) => i == 0 ? Dodged0 : Dodged1;
        public bool Thrown(int i) => i == 0 ? Thrown0 : Thrown1;
        public bool KdNext(int i) => i == 0 ? KdNext0 : KdNext1;
    }

    public class CardSim
    {
        public readonly int[] Hp = { CardConfig.MaxHp, CardConfig.MaxHp };
        public readonly List<int>[] Deck = { new List<int>(), new List<int>() };
        public readonly List<int>[] Hand = { new List<int>(), new List<int>() };
        public readonly List<int>[] Discard = { new List<int>(), new List<int>() }; // público (boca arriba)
        public readonly bool[] KnockedDown = new bool[2];  // afecta el combate del turno EN CURSO
        public readonly int[] DeckOuts = new int[2];       // 1 = ya remezcló; 2 = time over
        public int Active;            // jugador activo del turno en curso
        public int Turn;              // turnos ya arrancados (1 durante el primero)
        public int ExchangesLeft;     // del activo, resetea por turno
        public bool Over;
        public int Winner = -1;       // -1 = empate / sigue
        public bool AwaitingHitBack;  // hay un castigo pendiente (dodge o unsafe)
        public int HitBackSide = -1;

        uint _rng;
        CardTurnResult _r;            // resultado del combate en curso / último
        readonly bool[] _openedRecurring = new bool[2]; // abrió con carta recurring
        public CardTurnResult LastResult => _r;

        public CardSim(int seed, int firstPlayer)
        {
            _rng = seed == 0 ? 0x9E3779B9u : (uint)seed;
            Active = firstPlayer;
            for (int s = 0; s < 2; s++)
            {
                for (int c = 0; c < CardCatalog.DeckCounts.Length; c++)
                    for (int n = 0; n < CardCatalog.DeckCounts[c]; n++)
                        Deck[s].Add(c);
                // mano inicial garantizada: Bloqueo Bajo + Bloqueo Alto + un Agarre
                TakeFromDeck(s, CardCatalog.LowBlock);
                TakeFromDeck(s, CardCatalog.HighBlock);
                TakeFromDeck(s, CardCatalog.Throw);
                Shuffle(Deck[s]);
                for (int n = 0; n < CardConfig.OpeningRandomDraws; n++) DrawOne(s, false);
            }
        }

        void TakeFromDeck(int side, int card)
        {
            Deck[side].Remove(card);
            Hand[side].Add(card);
        }

        uint NextRng() { _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5; return _rng; }

        void Shuffle(List<int> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = (int)(NextRng() % (uint)(i + 1));
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
        }

        // ---- fases ----

        // Arranca el turno del jugador Active: roba 2 (1 el primerísimo turno).
        public void StartTurn()
        {
            if (Over) return;
            Turn++;
            ExchangesLeft = CardConfig.ExchangesPerTurn;
            int draws = Turn == 1 ? CardConfig.FirstTurnDraw : CardConfig.DrawPerTurn;
            for (int n = 0; n < draws && !Over; n++) DrawOne(Active, true);
            SortHands();
        }

        // La mano ordenada como en un juego de cartas real (A→E, agarre,
        // esquive, bloqueos, X/Y/Z): legibilidad pura. Solo se ordena en
        // puntos sin selección pendiente — los índices que ven la UI y la IA
        // siempre salen de la lista YA ordenada. Determinista.
        void SortHands()
        {
            Hand[0].Sort();
            Hand[1].Sort();
        }

        public bool CanExchange(int handIdx, int discardIdx)
        {
            if (Over || AwaitingHitBack || ExchangesLeft <= 0) return false;
            var hand = Hand[Active]; var disc = Discard[Active];
            if (handIdx < 0 || handIdx >= hand.Count) return false;
            if (discardIdx < 0 || discardIdx >= disc.Count) return false;
            return CardCatalog.All[hand[handIdx]].IsNormal && CardCatalog.All[disc[discardIdx]].IsNormal;
        }

        // Exchange del ACTIVO: descarta una normal de la mano y recupera una normal del descarte.
        public bool Exchange(int handIdx, int discardIdx)
        {
            if (!CanExchange(handIdx, discardIdx)) return false;
            int outCard = Hand[Active][handIdx], inCard = Discard[Active][discardIdx];
            Hand[Active].RemoveAt(handIdx);
            Discard[Active].RemoveAt(discardIdx);
            Discard[Active].Add(outCard);
            Hand[Active].Add(inCard);
            ExchangesLeft--;
            SortHands(); // la carta recuperada entra en su lugar
            return true;
        }

        // Opener válido: existe y no es un dodge estando derribado.
        public bool LegalOpener(int side, int handIdx)
        {
            if (handIdx < 0 || handIdx >= Hand[side].Count) return false;
            var def = CardCatalog.All[Hand[side][handIdx]];
            return !(def.Kind == CardKind.Dodge && KnockedDown[side]);
        }

        public bool HasLegalOpener(int side)
        {
            for (int i = 0; i < Hand[side].Count; i++) if (LegalOpener(side, i)) return true;
            return false;
        }

        // Resuelve el combate del turno con los openers elegidos (índices de mano).
        // Un opener inválido dispara el WILD SWING: se descarta y juega la carta
        // de arriba del mazo hasta que salga una válida. Si queda un castigo
        // pendiente (dodge a strike / unsafe bloqueado), AwaitingHitBack queda
        // true y hay que llamar HitBack() para cerrar el combate.
        public CardTurnResult Resolve(int handIdx0, int handIdx1)
        {
            _r = new CardTurnResult { Active = Active, HitBackSide = -1, HitBackCard = -1 };
            if (Over) { _r.TimeOver = true; return _r; }

            int c0 = PlayOpener(0, handIdx0, ref _r.Wild0);
            int c1 = PlayOpener(1, handIdx1, ref _r.Wild1);
            if (Over) { _r.TimeOver = true; return _r; } // el wild swing agotó el mazo
            _r.Card0 = c0; _r.Card1 = c1;
            _openedRecurring[0] = CardCatalog.All[c0].Recurring;
            _openedRecurring[1] = CardCatalog.All[c1].Recurring;

            Fight(c0, c1);

            if (!AwaitingHitBack) FinishCombat();
            else SortHands(); // el robo por bloqueo ya entró: el menú de castigo sale ordenado
            return _r;
        }

        // Cierra el castigo pendiente. handIdx -1 = declinar. La carta debe ser
        // ataque o throw; pega su daño entero (y derriba si derriba).
        public CardTurnResult HitBack(int handIdx)
        {
            if (!AwaitingHitBack) return _r;
            int side = HitBackSide, victim = 1 - side;
            if (handIdx >= 0 && handIdx < Hand[side].Count)
            {
                var def = CardCatalog.All[Hand[side][handIdx]];
                if (def.Kind == CardKind.Attack || def.Kind == CardKind.Throw)
                {
                    int card = Hand[side][handIdx];
                    Hand[side].RemoveAt(handIdx);
                    Discard[side].Add(card);
                    _r.HitBackSide = side; _r.HitBackCard = card;
                    Damage(victim, def.Damage, false);
                    if (def.KnockdownOnHit) SetKdNext(victim);
                }
            }
            AwaitingHitBack = false; HitBackSide = -1;
            FinishCombat();
            return _r;
        }

        // ---- resolución ----

        // Speed efectivo: contra un rival derribado, tus ataques/throws lentos suben a 10.
        int EffSpeed(int side, in CardDef def) =>
            KnockedDown[1 - side] && def.Speed < CardConfig.KnockdownMinSpeed
                ? CardConfig.KnockdownMinSpeed : def.Speed;

        void Fight(int c0, int c1)
        {
            var d0 = CardCatalog.All[c0]; var d1 = CardCatalog.All[c1];
            var k0 = d0.Kind; var k1 = d1.Kind;

            if (k0 == CardKind.Attack && k1 == CardKind.Attack)
            {
                if (d0.Projectile && d1.Projectile)
                {
                    // proyectil vs proyectil: SOLO importa el nivel; igual nivel se anulan
                    if (d0.ProjLevel == d1.ProjLevel) { _r.ProjCancel = true; return; }
                    int w = d0.ProjLevel > d1.ProjLevel ? 0 : 1;
                    Damage(1 - w, (w == 0 ? d0 : d1).Damage, false);
                    return;
                }
                int s0 = EffSpeed(0, d0), s1 = EffSpeed(1, d1);
                int win = s0 == s1 ? Active : (s0 > s1 ? 0 : 1); // empate al activo
                var wd = win == 0 ? d0 : d1;
                Damage(1 - win, wd.Damage, false);
                if (wd.KnockdownOnHit) SetKdNext(1 - win);
                return;
            }

            // attack vs throw: el ataque gana SIEMPRE (sin importar speed)
            if (k0 == CardKind.Attack && k1 == CardKind.Throw) { Damage(1, d0.Damage, false); return; }
            if (k1 == CardKind.Attack && k0 == CardKind.Throw) { Damage(0, d1.Damage, false); return; }

            if (k0 == CardKind.Attack && k1 == CardKind.Block) { AttackVsBlock(0, d0, d1); return; }
            if (k1 == CardKind.Attack && k0 == CardKind.Block) { AttackVsBlock(1, d1, d0); return; }

            if (k0 == CardKind.Attack && k1 == CardKind.Dodge) { AttackVsDodge(0, d0); return; }
            if (k1 == CardKind.Attack && k0 == CardKind.Dodge) { AttackVsDodge(1, d1); return; }

            if (k0 == CardKind.Throw && k1 == CardKind.Throw)
            {
                int s0 = EffSpeed(0, d0), s1 = EffSpeed(1, d1);
                int win = s0 == s1 ? Active : (s0 > s1 ? 0 : 1);
                LandThrow(win, win == 0 ? d0 : d1);
                return;
            }

            // throw vs block/dodge: el throw agarra
            if (k0 == CardKind.Throw) { LandThrow(0, d0); return; }
            if (k1 == CardKind.Throw) { LandThrow(1, d1); return; }

            // block/dodge vs block/dodge: no pasa nada (los recurring vuelven en el cleanup)
        }

        void AttackVsBlock(int atkSide, in CardDef atk, in CardDef blk)
        {
            int blocker = 1 - atkSide;
            if (blk.Blocks(atk.Height))
            {
                if (blocker == 0) _r.Blocked0 = true; else _r.Blocked1 = true;
                if (atk.BlockDamage > 0) Damage(blocker, atk.BlockDamage, true); // chip: no es "hit"
                if (!atk.Lockdown)
                {
                    DrawOne(blocker, true);
                    if (blocker == 0) _r.Drew0++; else _r.Drew1++;
                }
                if (atk.UnsafeOnBlock) { AwaitingHitBack = true; HitBackSide = blocker; }
            }
            else
            {
                if (blocker == 0) _r.WrongBlock0 = true; else _r.WrongBlock1 = true;
                Damage(blocker, atk.Damage, false);
                if (atk.KnockdownOnHit) SetKdNext(blocker);
            }
        }

        void AttackVsDodge(int atkSide, in CardDef atk)
        {
            int dodger = 1 - atkSide;
            if (dodger == 0) _r.Dodged0 = true; else _r.Dodged1 = true;
            // solo los STRIKES se castigan al esquivarlos; el proyectil se esquiva y ya
            if (atk.IsStrike) { AwaitingHitBack = true; HitBackSide = dodger; }
        }

        void LandThrow(int side, in CardDef thr)
        {
            int victim = 1 - side;
            if (victim == 0) _r.Thrown0 = true; else _r.Thrown1 = true;
            Damage(victim, thr.Damage, false);
            if (thr.KnockdownOnHit) SetKdNext(victim);
        }

        void Damage(int side, int dmg, bool chip)
        {
            if (side == 0) { _r.Dmg0 += dmg; if (chip) _r.Chip0 += dmg; }
            else { _r.Dmg1 += dmg; if (chip) _r.Chip1 += dmg; }
        }

        void SetKdNext(int side)
        {
            if (side == 0) _r.KdNext0 = true; else _r.KdNext1 = true;
        }

        // ---- cierre del combate ----

        void FinishCombat()
        {
            // recurring: vuelve a la mano si abriste con ella y NO te pegaron
            // (el chip no cuenta como golpe — regla oficial)
            for (int s = 0; s < 2; s++)
            {
                int card = s == 0 ? _r.Card0 : _r.Card1;
                bool hit = (s == 0 ? _r.Dmg0 - _r.Chip0 : _r.Dmg1 - _r.Chip1) > 0;
                if (_openedRecurring[s] && !hit)
                {
                    if (s == 0) _r.Returned0 = true; else _r.Returned1 = true;
                    AddToHand(s, card);
                }
                else Discard[s].Add(card);
            }

            // knockdown: dura UN combate; si caerían los dos, se cancelan
            bool kd0 = _r.KdNext0, kd1 = _r.KdNext1;
            if (kd0 && kd1) { kd0 = kd1 = false; _r.KdNext0 = _r.KdNext1 = false; }
            KnockedDown[0] = kd0;
            KnockedDown[1] = kd1;

            Hp[0] = Math.Max(0, Hp[0] - _r.Dmg0);
            Hp[1] = Math.Max(0, Hp[1] - _r.Dmg1);
            if (Hp[0] <= 0 || Hp[1] <= 0)
            {
                Over = true;
                Winner = Hp[0] <= 0 && Hp[1] <= 0
                    ? (Hp[0] == Hp[1] ? -1 : (Hp[0] > Hp[1] ? 0 : 1))
                    : (Hp[0] <= 0 ? 1 : 0);
            }
            else if (Over)
            {
                // TIME OVER saltó a mitad del combate (robo por bloqueo con el
                // mazo seco): re-juzgar con el daño de ESTE turno ya aplicado
                Winner = Hp[0] == Hp[1] ? -1 : (Hp[0] > Hp[1] ? 0 : 1);
            }

            SortHands(); // robos por bloqueo y recurring entran en su lugar
            Active = 1 - Active;
        }

        // ---- mazo / mano ----

        int PlayOpener(int side, int handIdx, ref int wildCount)
        {
            if (LegalOpener(side, handIdx))
            {
                int card = Hand[side][handIdx];
                Hand[side].RemoveAt(handIdx);
                return card;
            }
            // wild swing: descarta el opener inválido (si había) y da vuelta el mazo
            if (handIdx >= 0 && handIdx < Hand[side].Count)
            {
                Discard[side].Add(Hand[side][handIdx]);
                Hand[side].RemoveAt(handIdx);
                wildCount++;
            }
            while (!Over)
            {
                int card = TopOfDeck(side);
                if (card < 0) return CardCatalog.Dodge; // time over a mitad del swing
                var def = CardCatalog.All[card];
                if (def.Kind == CardKind.Dodge && KnockedDown[side]) { Discard[side].Add(card); wildCount++; continue; }
                return card;
            }
            return CardCatalog.Dodge;
        }

        int TopOfDeck(int side)
        {
            if (Deck[side].Count == 0 && !RefillDeck(side)) return -1;
            int card = Deck[side][Deck[side].Count - 1];
            Deck[side].RemoveAt(Deck[side].Count - 1);
            return card;
        }

        void DrawOne(int side, bool respectLimit)
        {
            int card = TopOfDeck(side);
            if (card < 0) return;
            if (respectLimit) AddToHand(side, card);
            else Hand[side].Add(card);
        }

        void AddToHand(int side, int card)
        {
            if (Hand[side].Count >= CardConfig.HandLimit) Discard[side].Add(card);
            else Hand[side].Add(card);
        }

        // Mazo vacío: la PRIMERA vez remezcla el descarte (los blocks quedan
        // afuera, recuperables por exchange); la SEGUNDA es TIME OVER.
        bool RefillDeck(int side)
        {
            DeckOuts[side]++;
            if (DeckOuts[side] >= 2) { TimeOver(); return false; }
            bool lowKept = false, highKept = false;
            var keep = new List<int>();
            var shuffleIn = new List<int>();
            foreach (int c in Discard[side])
            {
                if (c == CardCatalog.LowBlock && !lowKept) { keep.Add(c); lowKept = true; }
                else if (c == CardCatalog.HighBlock && !highKept) { keep.Add(c); highKept = true; }
                else shuffleIn.Add(c);
            }
            Discard[side].Clear();
            Discard[side].AddRange(keep);
            if (shuffleIn.Count == 0) { TimeOver(); return false; }
            Deck[side].AddRange(shuffleIn);
            Shuffle(Deck[side]);
            return true;
        }

        void TimeOver()
        {
            if (Over) return;
            Over = true;
            _r.TimeOver = true;
            Winner = Hp[0] == Hp[1] ? -1 : (Hp[0] > Hp[1] ? 0 : 1);
        }
    }
}
