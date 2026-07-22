using System;
using System.Collections.Generic;

namespace LagFighter
{
    // ---- MODO CARTAS v2 (2026-07-22): copia COMPLETA de Yomi 2 ----
    // Ahora con todo el juego real: mazos de 30 (supers y ability incluidos),
    // super meter (0..3), Power Up por pares, COMBOS (combo points, chains,
    // starters/linkers/enders, +1 meter por paso de cadena), pumps, knockdown
    // solo si NO seguís de combo, abilities ongoing (Wind Summon / Arc Shot),
    // the Edge, wild swing que DEBE jugar la super si hay meter, e innates
    // (Grave: doble exchange · Jaina: Recklessness). Dos personajes: Grave
    // (HP 90, max combo 4) y Jaina (HP 85, max combo 5). Ver YOMI2-CARDS.md.
    // Pura y determinista (sin UnityEngine): cada regla es un test.

    public enum CardKind { Attack = 0, Throw = 1, Block = 2, Dodge = 3, Ability = 4 }
    public enum CardHeight { Mid = 0, High = 1, Low = 2 }
    public enum ComboType { None = 0, Chain = 1, Starter = 2, Linker = 3, Ender = 4, CantCombo = 5 }
    public enum PumpFuel { None = 0, ZCard = 1, AnyCard = 2, SuperCard = 3 }

    public struct CardDef
    {
        public string Name;
        public string Short;
        public CardKind Kind;
        public int Speed;            // mayor = más rápido
        public int Damage;
        public int BlockDamage;      // chip si te la bloquean
        public CardHeight Height;
        public bool Projectile;
        public int ProjLevel;
        public bool Recurring;       // vuelve si abriste con ella y no te pegaron
        public bool Lockdown;        // el que la bloquea NO roba
        public bool UnsafeOnBlock;   // bloqueada: te devuelven UN golpe
        public bool KnockdownOnHit;  // derriba si es el ÚLTIMO move del combo
        public bool BlocksHigh, BlocksLow;
        public bool IsNormal;        // ícono de exchange
        public ComboType Combo;
        public int ComboPoints;
        public int ChainLetter;      // 0..4 = A..E (solo normales de cadena), -1 si no
        public bool IsSuper;
        public int SuperCost;        // 1..3 de meter
        public PumpFuel Pump;        // qué carta descarta cada paquete de pump
        public int PumpDamage;       // daño extra por paquete
        public int PumpMax;          // cuántos paquetes admite
        public int SelfDamage;       // Jaina Y: 5 al jugarla (salvo HP <= 35)
        public int DodgeCounter;     // Grave S2: esquiva y devuelve N fijo vs strikes

        public bool IsStrike => Kind == CardKind.Attack && !Projectile;
        public bool Blocks(CardHeight h) =>
            h == CardHeight.Mid ? (BlocksHigh || BlocksLow)
            : h == CardHeight.High ? BlocksHigh : BlocksLow;
    }

    public class CharacterDef
    {
        public string Name;
        public int MaxHp;
        public int MaxCombo;         // combo points por turno
        public int ExchangesPerTurn; // Grave: 2 (innate) · resto: 1
        public bool Reckless;        // innate de Jaina: main phase sin blocks en mano → 2 dmg y roba 1
        public string InnateText;
        public string AbilityText;
        public CardDef[] Cards;      // 15, layout fijo (ver índices de CardCatalog)
        public int[] DeckCounts;     // por carta; suma 30
    }

    public static class CardCatalog
    {
        // Layout FIJO por personaje: el mismo índice es la misma "ranura" en
        // ambos mazos — toda la tubería (UI, IA, teatro) indexa igual.
        public const int AttackA = 0;
        public const int AttackB = 1;
        public const int AttackC = 2;
        public const int AttackD = 3;
        public const int AttackE = 4;
        public const int Throw = 5;
        public const int Dodge = 6;
        public const int LowBlock = 7;
        public const int HighBlock = 8;
        public const int SpecialX = 9;
        public const int SpecialY = 10;
        public const int SpecialZ = 11;
        public const int Super1 = 12;
        public const int Super2 = 13;
        public const int Ability = 14;
        public const int CardsPerChar = 15;

        // A..E, Throw, Dodge y blocks son idénticos entre personajes (regla
        // de Yomi 2: los normales son el esqueleto común).
        static CardDef[] BaseCards() => new[]
        {
            new CardDef { Name = "Golpe Rápido (A)", Short = "A", Kind = CardKind.Attack, Speed = 8, Damage = 3, Height = CardHeight.Low, IsNormal = true, Combo = ComboType.Chain, ComboPoints = 1, ChainLetter = 0 },
            new CardDef { Name = "Golpe Ligero (B)", Short = "B", Kind = CardKind.Attack, Speed = 7, Damage = 4, Height = CardHeight.Low, IsNormal = true, Combo = ComboType.Chain, ComboPoints = 1, ChainLetter = 1 },
            new CardDef { Name = "Golpe Medio (C)", Short = "C", Kind = CardKind.Attack, Speed = 6, Damage = 5, Height = CardHeight.Mid, IsNormal = true, Combo = ComboType.Chain, ComboPoints = 1, ChainLetter = 2 },
            new CardDef { Name = "Golpe Pesado (D)", Short = "D", Kind = CardKind.Attack, Speed = 5, Damage = 6, Height = CardHeight.High, IsNormal = true, Combo = ComboType.Chain, ComboPoints = 1, ChainLetter = 3 },
            new CardDef { Name = "Golpe Poderoso (E)", Short = "E", Kind = CardKind.Attack, Speed = 4, Damage = 7, Height = CardHeight.High, IsNormal = true, Combo = ComboType.Chain, ComboPoints = 1, ChainLetter = 4 },
            new CardDef { Name = "Agarre", Short = "AGR", Kind = CardKind.Throw, Speed = 5, Damage = 7, KnockdownOnHit = true, IsNormal = true, Combo = ComboType.Starter, ComboPoints = 2, ChainLetter = -1 },
            new CardDef { Name = "Esquive", Short = "ESQ", Kind = CardKind.Dodge, IsNormal = true, ChainLetter = -1 },
            new CardDef { Name = "Bloqueo Bajo", Short = "BJO", Kind = CardKind.Block, BlocksLow = true, Recurring = true, IsNormal = true, ChainLetter = -1 },
            new CardDef { Name = "Bloqueo Alto", Short = "ALT", Kind = CardKind.Block, BlocksHigh = true, Recurring = true, IsNormal = true, ChainLetter = -1 },
        };

        static readonly int[] StandardCounts = { 2, 2, 2, 2, 2, 3, 3, 1, 1, 2, 2, 2, 2, 2, 2 }; // = 30

        public static readonly CharacterDef Grave = MakeGrave();
        public static readonly CharacterDef Jaina = MakeJaina();
        public static readonly CharacterDef[] Chars = { Grave, Jaina };
        public const int GraveIdx = 0, JainaIdx = 1;

        static CharacterDef MakeGrave()
        {
            var cards = new CardDef[CardsPerChar];
            BaseCards().CopyTo(cards, 0);
            cards[SpecialX] = new CardDef { Name = "Nube Eléctrica (X)", Short = "X", Kind = CardKind.Attack, Speed = 7, Damage = 8, BlockDamage = 4, Projectile = true, ProjLevel = 1, Recurring = true, Lockdown = true, Combo = ComboType.Ender, ComboPoints = 1, ChainLetter = -1 };
            cards[SpecialY] = new CardDef { Name = "Espada Tormenta (Y)", Short = "Y", Kind = CardKind.Attack, Speed = 11, Damage = 10, BlockDamage = 2, UnsafeOnBlock = true, Combo = ComboType.Ender, ComboPoints = 3, ChainLetter = -1 };
            cards[SpecialZ] = new CardDef { Name = "Torbellino (Z)", Short = "Z", Kind = CardKind.Attack, Speed = 7, Damage = 7, BlockDamage = 1, Height = CardHeight.High, Combo = ComboType.Linker, ComboPoints = 2, ChainLetter = -1, Pump = PumpFuel.ZCard, PumpDamage = 8, PumpMax = 1 };
            cards[Super1] = new CardDef { Name = "Corazón de Dragón", Short = "S1", Kind = CardKind.Attack, Speed = 15, Damage = 20, BlockDamage = 1, UnsafeOnBlock = true, IsSuper = true, SuperCost = 2, Combo = ComboType.Ender, ComboPoints = 3, ChainLetter = -1 };
            cards[Super2] = new CardDef { Name = "Poder de las Tormentas", Short = "S2", Kind = CardKind.Dodge, IsSuper = true, SuperCost = 3, DodgeCounter = 40, ChainLetter = -1 };
            cards[Ability] = new CardDef { Name = "Invocar Viento", Short = "HAB", Kind = CardKind.Ability, ChainLetter = -1 };
            return new CharacterDef
            {
                Name = "GRAVE",
                MaxHp = 90,
                MaxCombo = 4,
                ExchangesPerTurn = 2,
                Reckless = false,
                InnateText = "Estilo Versátil: DOS exchanges por turno",
                AbilityText = "Invocar Viento (2 combates): tu proyectil sube a Nv.2, le gana a esquives, +4 dmg / +2 chip · tus supers cuestan 2 combo points",
                Cards = cards,
                DeckCounts = StandardCounts,
            };
        }

        static CharacterDef MakeJaina()
        {
            var cards = new CardDef[CardsPerChar];
            BaseCards().CopyTo(cards, 0);
            cards[SpecialX] = new CardDef { Name = "Flecha de Fuego (X)", Short = "X", Kind = CardKind.Attack, Speed = 7, Damage = 7, BlockDamage = 5, Projectile = true, ProjLevel = 1, Recurring = true, Lockdown = true, Combo = ComboType.Ender, ComboPoints = 1, ChainLetter = -1 };
            cards[SpecialY] = new CardDef { Name = "Corazón de Dragón (Y)", Short = "Y", Kind = CardKind.Attack, Speed = 14, Damage = 8, BlockDamage = 1, UnsafeOnBlock = true, Combo = ComboType.Ender, ComboPoints = 3, ChainLetter = -1, Pump = PumpFuel.AnyCard, PumpDamage = 5, PumpMax = 1, SelfDamage = 5 };
            cards[SpecialZ] = new CardDef { Name = "Patada Cruzada (Z)", Short = "Z", Kind = CardKind.Attack, Speed = 8, Damage = 6, BlockDamage = 3, Height = CardHeight.High, Combo = ComboType.Linker, ComboPoints = 2, ChainLetter = -1, Pump = PumpFuel.ZCard, PumpDamage = 7, PumpMax = 1 };
            cards[Super1] = new CardDef { Name = "Dragón Rojo", Short = "S1", Kind = CardKind.Attack, Speed = 12, Damage = 10, BlockDamage = 2, UnsafeOnBlock = true, IsSuper = true, SuperCost = 1, Combo = ComboType.CantCombo, ComboPoints = 0, ChainLetter = -1, Pump = PumpFuel.SuperCard, PumpDamage = 9, PumpMax = 2 };
            cards[Super2] = new CardDef { Name = "Aliento de Dragón", Short = "S2", Kind = CardKind.Attack, Speed = 8, Damage = 18, BlockDamage = 4, Projectile = true, ProjLevel = 3, IsSuper = true, SuperCost = 2, Combo = ComboType.Ender, ComboPoints = 2, ChainLetter = -1 };
            cards[Ability] = new CardDef { Name = "Tiro en Arco", Short = "HAB", Kind = CardKind.Ability, ChainLetter = -1 };
            return new CharacterDef
            {
                Name = "JAINA",
                MaxHp = 85,
                MaxCombo = 5,
                ExchangesPerTurn = 1,
                Reckless = true,
                InnateText = "Imprudencia: si cerrás tu main phase con AMBOS bloqueos en el descarte, te hacés 2 de daño y robás 1",
                AbilityText = "Tiro en Arco (2 combates): si el rival abre con ATAQUE come 7 y no puede combear ni pumpear; si abre con BLOQUEO come 5 de chip · tu Y es segura bloqueada",
                Cards = cards,
                DeckCounts = StandardCounts,
            };
        }
    }

    public static class CardConfig
    {
        public const int HandLimit = 12;
        public const int DrawPerTurn = 2;
        public const int FirstTurnDraw = 1;
        public const int OpeningRandomDraws = 4;  // + Low Block, High Block y un Agarre fijos
        public const int KnockdownMinSpeed = 10;  // vs derribado, los moves lentos suben a 10
        public const int MeterCap = 3;
        public const int EdgeBonus = 3;           // the Edge: +3 speed, máx 10, un combate
        public const int EdgeCap = 10;
        public const int AbilityCombats = 2;      // Wind Summon y Arc Shot duran 2 combates
        public const int ArcAttackDamage = 7;     // Arc Shot vs opener de ataque
        public const int ArcBlockChip = 5;        // Arc Shot vs opener de bloqueo
        public const int RecklessDamage = 2;      // innate de Jaina
        public const int JainaYFreeBelow = 35;    // sin self-damage con HP <= 35
    }

    // Todo lo que pasó en un combate (opener + combo + castigo), para el
    // teatro, el log y los tests.
    public class CardTurnResult
    {
        public int Card0 = -1, Card1 = -1;  // openers finales (tras wild swing)
        public int Wild0, Wild1;
        public int Dmg0, Dmg1;              // daño total RECIBIDO (incluye chip, arco y castigo; NO self)
        public int Chip0, Chip1;            // cuánto fue block damage
        public int Self0, Self1;            // self-damage (Jaina Y, Recklessness)
        public bool Hit0, Hit1;             // recibió un "hit" real (excluye chip y self)
        public bool Blocked0, Blocked1;
        public bool WrongBlock0, WrongBlock1;
        public bool Dodged0, Dodged1;
        public bool Thrown0, Thrown1;
        public bool ProjCancel;
        public bool KdNext0, KdNext1;
        public bool Edge0Next, Edge1Next;
        public int HitBackSide = -1, HitBackCard = -1;
        public readonly List<int> Combo0 = new List<int>(); // cartas DE COMBO (sin el opener)
        public readonly List<int> Combo1 = new List<int>();
        public int PumpExtra0, PumpExtra1;  // daño extra por pumps que METIÓ ese lado
        public int Meter0, Meter1;          // meter ganado por chains este combate
        public bool Arc0, Arc1;             // ese lado COMIÓ el Arc Shot (7 / 5 chip)
        public bool Reckless;               // la Imprudencia de Jaina disparó (lado activo)
        public bool Returned0, Returned1;
        public int Drew0, Drew1;
        public int Active;
        public bool TimeOver;
        public int SuperCounter = -1;       // lado que contragolpeó con la super dodge (Grave S2)

        public int Card(int i) => i == 0 ? Card0 : Card1;
        public int Dmg(int i) => i == 0 ? Dmg0 : Dmg1;
        public int Chip(int i) => i == 0 ? Chip0 : Chip1;
        public int Self(int i) => i == 0 ? Self0 : Self1;
        public bool Blocked(int i) => i == 0 ? Blocked0 : Blocked1;
        public bool Dodged(int i) => i == 0 ? Dodged0 : Dodged1;
        public bool Thrown(int i) => i == 0 ? Thrown0 : Thrown1;
        public bool KdNext(int i) => i == 0 ? KdNext0 : KdNext1;
        public List<int> Combo(int i) => i == 0 ? Combo0 : Combo1;
        public int MeterGain(int i) => i == 0 ? Meter0 : Meter1;
    }

    public class CardSim
    {
        public readonly CharacterDef[] Chr = new CharacterDef[2];
        public readonly int[] CharIdx = new int[2];
        public readonly int[] Hp = new int[2];
        public readonly int[] Meter = new int[2];       // super meter 0..3, público
        public readonly List<int>[] Deck = { new List<int>(), new List<int>() };
        public readonly List<int>[] Hand = { new List<int>(), new List<int>() };
        public readonly List<int>[] Discard = { new List<int>(), new List<int>() }; // público
        public readonly bool[] KnockedDown = new bool[2];  // afecta el combate EN CURSO
        public readonly bool[] Edge = new bool[2];         // the Edge, un combate
        public readonly int[] Ongoing = new int[2];        // combates que le quedan a la ability
        public readonly int[] DeckOuts = new int[2];
        public int Active;
        public int Turn;
        public int ExchangesLeft;
        public bool PowerUpUsed;    // del activo, 1 por turno
        public bool AbilityUsed;    // ídem
        public bool Over;
        public int Winner = -1;

        // ---- followup (combo / pump / castigo) ----
        public int FollowSide = -1;          // lado que decide (-1 = nada pendiente)
        public bool FollowIsHitBack;         // true: castigo (un golpe, ender) · false: combo
        public bool HitBackPlayed;           // el castigo ya se jugó (solo queda pump/terminar)
        public int FollowCpLeft;             // combo points restantes
        public int LastPlayed = -1;          // último move que CONECTÓ (candidato a pump)
        public bool LastPumped;
        public bool AwaitingFollowup => FollowSide >= 0;
        readonly bool[] _noComboPump = new bool[2]; // Arc Shot: no combea ni pumpea

        uint _rng;
        CardTurnResult _r = new CardTurnResult();
        readonly bool[] _openedRecurring = new bool[2];
        int _followVictim = -1;
        bool _combatFinished; // FinishCombat corre UNA vez por Resolve (los
                              // auto-cierres del followup pasan por adentro)
        public CardTurnResult LastResult => _r;

        public CardDef Def(int side, int card) => Chr[side].Cards[card];
        public bool WindActive(int side) => CharIdx[side] == CardCatalog.GraveIdx && Ongoing[side] > 0;
        public bool ArcActive(int side) => CharIdx[side] == CardCatalog.JainaIdx && Ongoing[side] > 0;

        public CardSim(int seed, int firstPlayer, int char0 = CardCatalog.GraveIdx, int char1 = CardCatalog.GraveIdx)
        {
            _rng = seed == 0 ? 0x9E3779B9u : (uint)seed;
            Active = firstPlayer;
            CharIdx[0] = char0; CharIdx[1] = char1;
            for (int s = 0; s < 2; s++)
            {
                Chr[s] = CardCatalog.Chars[CharIdx[s]];
                Hp[s] = Chr[s].MaxHp;
                for (int c = 0; c < CardCatalog.CardsPerChar; c++)
                    for (int n = 0; n < Chr[s].DeckCounts[c]; n++)
                        Deck[s].Add(c);
                // una copia de cada super ARRANCA en el descarte (recuperable
                // por Power Up) — regla real de setup
                MoveDeckTo(s, CardCatalog.Super1, Discard[s]);
                MoveDeckTo(s, CardCatalog.Super2, Discard[s]);
                // mano garantizada: Bloqueo Bajo + Alto + un Agarre (sin Burst: no hay gems)
                MoveDeckTo(s, CardCatalog.LowBlock, Hand[s]);
                MoveDeckTo(s, CardCatalog.HighBlock, Hand[s]);
                MoveDeckTo(s, CardCatalog.Throw, Hand[s]);
                Shuffle(Deck[s]);
                for (int n = 0; n < CardConfig.OpeningRandomDraws; n++) DrawOne(s, false);
            }
        }

        void MoveDeckTo(int side, int card, List<int> dest)
        {
            Deck[side].Remove(card);
            dest.Add(card);
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

        void SortHands()
        {
            Hand[0].Sort();
            Hand[1].Sort();
        }

        // ---- fases ----

        public void StartTurn()
        {
            if (Over) return;
            Turn++;
            ExchangesLeft = Chr[Active].ExchangesPerTurn;
            PowerUpUsed = false;
            AbilityUsed = false;
            int draws = Turn == 1 ? CardConfig.FirstTurnDraw : CardConfig.DrawPerTurn;
            for (int n = 0; n < draws && !Over; n++) DrawOne(Active, true);
            SortHands();
        }

        // -- exchange (main phase del activo) --

        public bool CanExchange(int handIdx, int discardIdx)
        {
            if (Over || AwaitingFollowup || ExchangesLeft <= 0) return false;
            var hand = Hand[Active]; var disc = Discard[Active];
            if (handIdx < 0 || handIdx >= hand.Count) return false;
            if (discardIdx < 0 || discardIdx >= disc.Count) return false;
            return Def(Active, hand[handIdx]).IsNormal && Def(Active, disc[discardIdx]).IsNormal;
        }

        public bool Exchange(int handIdx, int discardIdx)
        {
            if (!CanExchange(handIdx, discardIdx)) return false;
            int outCard = Hand[Active][handIdx], inCard = Discard[Active][discardIdx];
            Hand[Active].RemoveAt(handIdx);
            Discard[Active].RemoveAt(discardIdx);
            Discard[Active].Add(outCard);
            Hand[Active].Add(inCard);
            ExchangesLeft--;
            SortHands();
            return true;
        }

        // -- power up (main phase): descartás un PAR y elegís --

        public bool CanPowerUp(int handIdxA, int handIdxB)
        {
            if (Over || AwaitingFollowup || PowerUpUsed) return false;
            var hand = Hand[Active];
            if (handIdxA < 0 || handIdxB < 0 || handIdxA == handIdxB) return false;
            if (handIdxA >= hand.Count || handIdxB >= hand.Count) return false;
            return hand[handIdxA] == hand[handIdxB]; // "par" = mismo nombre
        }

        // fetchSuper: true = recuperar una super del descarte (+1 meter) ·
        // false = +2 meter. superCard: cuál (Super1/Super2), si fetchSuper.
        public bool PowerUp(int handIdxA, int handIdxB, bool fetchSuper, int superCard = -1)
        {
            if (!CanPowerUp(handIdxA, handIdxB)) return false;
            if (fetchSuper && !Discard[Active].Contains(superCard)) return false;
            var hand = Hand[Active];
            int hi = Math.Max(handIdxA, handIdxB), lo = Math.Min(handIdxA, handIdxB);
            Discard[Active].Add(hand[hi]); hand.RemoveAt(hi);
            Discard[Active].Add(hand[lo]); hand.RemoveAt(lo);
            if (fetchSuper)
            {
                Discard[Active].Remove(superCard);
                AddToHand(Active, superCard);
                GainMeter(Active, 1);
            }
            else GainMeter(Active, 2);
            PowerUpUsed = true;
            SortHands();
            return true;
        }

        void GainMeter(int side, int n) => Meter[side] = Math.Min(CardConfig.MeterCap, Meter[side] + n);

        // -- ability (main phase): jugar la carta de habilidad --

        public bool CanPlayAbility(int handIdx)
        {
            if (Over || AwaitingFollowup || AbilityUsed) return false;
            var hand = Hand[Active];
            if (handIdx < 0 || handIdx >= hand.Count) return false;
            return Def(Active, hand[handIdx]).Kind == CardKind.Ability;
        }

        public bool PlayAbility(int handIdx)
        {
            if (!CanPlayAbility(handIdx)) return false;
            Discard[Active].Add(Hand[Active][handIdx]);
            Hand[Active].RemoveAt(handIdx);
            Ongoing[Active] = CardConfig.AbilityCombats;
            AbilityUsed = true;
            return true;
        }

        // ---- openers ----

        public bool LegalOpener(int side, int handIdx)
        {
            if (handIdx < 0 || handIdx >= Hand[side].Count) return false;
            var def = Def(side, Hand[side][handIdx]);
            if (def.Kind == CardKind.Ability) return false;             // nunca opener
            if (def.IsSuper && Meter[side] < def.SuperCost) return false;
            if (def.Kind == CardKind.Dodge && KnockedDown[side]) return false;
            return true;
        }

        public bool HasLegalOpener(int side)
        {
            for (int i = 0; i < Hand[side].Count; i++) if (LegalOpener(side, i)) return true;
            return false;
        }

        // Resuelve el combate. Si queda un combo, pump o castigo por decidir,
        // AwaitingFollowup queda true: ComboAdd/PumpLast/HitBack/FollowupEnd.
        public CardTurnResult Resolve(int handIdx0, int handIdx1)
        {
            _r = new CardTurnResult { Active = Active };
            _noComboPump[0] = _noComboPump[1] = false;
            _followVictim = -1;
            FollowSide = -1; FollowIsHitBack = false; HitBackPlayed = false;
            LastPlayed = -1; LastPumped = false;
            _combatFinished = false;
            if (Over) { _r.TimeOver = true; return _r; }

            // Imprudencia de Jaina: cerró su main phase con ambos bloqueos en
            // el descarte → 2 de daño (self) y roba 1
            if (Chr[Active].Reckless &&
                Discard[Active].Contains(CardCatalog.LowBlock) &&
                Discard[Active].Contains(CardCatalog.HighBlock))
            {
                _r.Reckless = true;
                AddSelf(Active, CardConfig.RecklessDamage);
                DrawOne(Active, true);
            }

            int c0 = PlayOpener(0, handIdx0, ref _r.Wild0);
            int c1 = PlayOpener(1, handIdx1, ref _r.Wild1);
            if (Over) { _r.TimeOver = true; FinishDeaths(); return _r; }
            _r.Card0 = c0; _r.Card1 = c1;
            _openedRecurring[0] = Def(0, c0).Recurring;
            _openedRecurring[1] = Def(1, c1).Recurring;

            // pagar supers y self-damage al REVELAR (aunque después pierdan)
            for (int s = 0; s < 2; s++)
            {
                var d = Def(s, _r.Card(s));
                if (d.IsSuper) Meter[s] -= d.SuperCost;
                if (d.SelfDamage > 0 && Hp[s] > CardConfig.JainaYFreeBelow)
                    AddSelf(s, d.SelfDamage);
            }

            // Arc Shot: dispara al revelar, ANTES del combate (regla oficial)
            for (int s = 0; s < 2; s++)
            {
                int o = 1 - s;
                if (!ArcActive(o)) continue;
                var d = Def(s, _r.Card(s));
                if (d.Kind == CardKind.Attack)
                {
                    if (s == 0) _r.Arc0 = true; else _r.Arc1 = true;
                    Damage(s, CardConfig.ArcAttackDamage, chip: false);
                    _noComboPump[s] = true;
                }
                else if (d.Kind == CardKind.Block)
                {
                    if (s == 0) _r.Arc0 = true; else _r.Arc1 = true;
                    Damage(s, CardConfig.ArcBlockChip, chip: true);
                }
            }

            Fight(c0, c1);

            if (!AwaitingFollowup) FinishCombat();
            else SortHands();
            return _r;
        }

        // ---- resolución del choque ----

        // Speed efectivo: the Edge (+3, máx 10) y el knockdown rival (mín 10).
        int EffSpeed(int side, in CardDef def)
        {
            int s = def.Speed;
            if (Edge[side] && s < CardConfig.EdgeCap) s = Math.Min(CardConfig.EdgeCap, s + CardConfig.EdgeBonus);
            if (KnockedDown[1 - side] && s < CardConfig.KnockdownMinSpeed) s = CardConfig.KnockdownMinSpeed;
            return s;
        }

        int ProjLevel(int side, in CardDef def) =>
            def.ProjLevel + (WindActive(side) && def.Projectile ? 1 : 0);

        int ProjDamage(int side, in CardDef def) =>
            def.Damage + (WindActive(side) && def.Projectile ? 4 : 0);

        int ProjChip(int side, in CardDef def) =>
            def.BlockDamage + (WindActive(side) && def.Projectile ? 2 : 0);

        void Fight(int c0, int c1)
        {
            var d0 = Def(0, c0); var d1 = Def(1, c1);
            var k0 = d0.Kind; var k1 = d1.Kind;

            if (k0 == CardKind.Attack && k1 == CardKind.Attack)
            {
                if (d0.Projectile && d1.Projectile)
                {
                    int l0 = ProjLevel(0, d0), l1 = ProjLevel(1, d1);
                    if (l0 == l1) { _r.ProjCancel = true; return; }
                    int w = l0 > l1 ? 0 : 1;
                    LandAttack(w, w == 0 ? c0 : c1);
                    return;
                }
                int s0 = EffSpeed(0, d0), s1 = EffSpeed(1, d1);
                int win = s0 == s1 ? Active : (s0 > s1 ? 0 : 1);
                LandAttack(win, win == 0 ? c0 : c1);
                return;
            }

            if (k0 == CardKind.Attack && k1 == CardKind.Throw) { LandAttack(0, c0); return; }
            if (k1 == CardKind.Attack && k0 == CardKind.Throw) { LandAttack(1, c1); return; }

            if (k0 == CardKind.Attack && k1 == CardKind.Block) { AttackVsBlock(0, c0); return; }
            if (k1 == CardKind.Attack && k0 == CardKind.Block) { AttackVsBlock(1, c1); return; }

            if (k0 == CardKind.Attack && k1 == CardKind.Dodge) { AttackVsDodge(0, c0, c1); return; }
            if (k1 == CardKind.Attack && k0 == CardKind.Dodge) { AttackVsDodge(1, c1, c0); return; }

            if (k0 == CardKind.Throw && k1 == CardKind.Throw)
            {
                int s0 = EffSpeed(0, d0), s1 = EffSpeed(1, d1);
                int win = s0 == s1 ? Active : (s0 > s1 ? 0 : 1);
                LandThrow(win, win == 0 ? c0 : c1);
                return;
            }

            if (k0 == CardKind.Throw) { LandThrow(0, c0); return; }
            if (k1 == CardKind.Throw) { LandThrow(1, c1); return; }

            // block/dodge vs block/dodge: no pasa nada
        }

        // Un ataque CONECTA: daño + arranca el followup (combo/pump) si puede.
        void LandAttack(int atkSide, int card)
        {
            var d = Def(atkSide, card);
            int victim = 1 - atkSide;
            Damage(victim, d.Projectile ? ProjDamage(atkSide, d) : d.Damage, chip: false);
            BeginFollowup(atkSide, card);
        }

        void LandThrow(int side, int card)
        {
            int victim = 1 - side;
            if (victim == 0) _r.Thrown0 = true; else _r.Thrown1 = true;
            Damage(victim, Def(side, card).Damage, chip: false);
            BeginFollowup(side, card);
        }

        void AttackVsBlock(int atkSide, int card)
        {
            var atk = Def(atkSide, card);
            int blocker = 1 - atkSide;
            var blk = Def(blocker, _r.Card(blocker));
            if (blk.Blocks(atk.Height))
            {
                if (blocker == 0) _r.Blocked0 = true; else _r.Blocked1 = true;
                int chip = atk.Projectile ? ProjChip(atkSide, atk) : atk.BlockDamage;
                if (chip > 0) Damage(blocker, chip, chip: true);
                if (!atk.Lockdown)
                {
                    DrawOne(blocker, true);
                    if (blocker == 0) _r.Drew0++; else _r.Drew1++;
                }
                // Jaina: su Y es SEGURA bloqueada si su Arc Shot está activo
                bool unsafeNow = atk.UnsafeOnBlock &&
                    !(atk.SelfDamage > 0 && ArcActive(atkSide));
                if (unsafeNow) EnterHitBack(blocker);
            }
            else
            {
                if (blocker == 0) _r.WrongBlock0 = true; else _r.WrongBlock1 = true;
                LandAttack(atkSide, card);
            }
        }

        void AttackVsDodge(int atkSide, int card, int dodgeCard)
        {
            var atk = Def(atkSide, card);
            int dodger = 1 - atkSide;
            var dod = Def(dodger, dodgeCard);
            // Invocar Viento: el proyectil de Grave LE GANA a los esquives
            if (atk.Projectile && WindActive(atkSide)) { LandAttack(atkSide, card); return; }
            if (dodger == 0) _r.Dodged0 = true; else _r.Dodged1 = true;
            if (!atk.IsStrike) return; // el proyectil se esquiva y ya
            // super dodge de Grave: contragolpe FIJO, sin carta
            if (dod.DodgeCounter > 0)
            {
                _r.SuperCounter = dodger;
                Damage(atkSide, dod.DodgeCounter, chip: false);
                return;
            }
            EnterHitBack(dodger);
        }

        // ---- followup: combo, pump y castigo ----

        void BeginFollowup(int side, int openerCard)
        {
            var d = Def(side, openerCard);
            LastPlayed = openerCard;
            LastPumped = false;
            _followVictim = 1 - side;
            FollowSide = side;
            FollowIsHitBack = false;
            FollowCpLeft = Chr[side].MaxCombo - ComboCost(side, d);
            if (_noComboPump[side]) { FollowupEnd(); return; }
            if (ComboOptions(side).Count == 0 && !CanPumpLast()) FollowupEnd();
        }

        void EnterHitBack(int side)
        {
            FollowSide = side;
            FollowIsHitBack = true;
            HitBackPlayed = false;
            LastPlayed = -1;
            LastPumped = false;
            _followVictim = 1 - side;
            bool any = false;
            foreach (int c in Hand[side])
            {
                var k = Def(side, c).Kind;
                if (k == CardKind.Attack || k == CardKind.Throw) { any = true; break; }
            }
            if (!any) FollowupEnd();
        }

        // Wind Summon: las supers de Grave cuestan 2 combo points (no 3)
        int ComboCost(int side, in CardDef d) =>
            d.IsSuper && d.Kind == CardKind.Attack && WindActive(side) && d.ComboPoints > 2 ? 2 : d.ComboPoints;

        // ¿next puede seguir a prev en un combo?
        public bool CanFollow(int side, int prevCard, int nextCard)
        {
            var prev = Def(side, prevCard); var next = Def(side, nextCard);
            if (prev.Combo == ComboType.Ender || prev.Combo == ComboType.CantCombo || prev.Combo == ComboType.None) return false;
            if (next.Combo == ComboType.CantCombo || next.Combo == ComboType.None || next.Combo == ComboType.Starter) return false;
            if (next.Combo == ComboType.Linker || next.Combo == ComboType.Ender) return true;
            // next es normal de cadena: tras starter/linker entra CUALQUIERA;
            // tras otra normal, SOLO la letra siguiente
            if (prev.Combo == ComboType.Starter || prev.Combo == ComboType.Linker) return true;
            return prev.Combo == ComboType.Chain && next.ChainLetter == prev.ChainLetter + 1;
        }

        // Índices de mano jugables como próxima carta del combo.
        public List<int> ComboOptions(int side)
        {
            var list = new List<int>();
            if (FollowSide != side || FollowIsHitBack || LastPlayed < 0) return list;
            for (int i = 0; i < Hand[side].Count; i++)
            {
                int c = Hand[side][i];
                var d = Def(side, c);
                if (!CanFollow(side, LastPlayed, c)) continue;
                if (ComboCost(side, d) > FollowCpLeft) continue;
                if (d.IsSuper && Meter[side] < d.SuperCost) continue;
                list.Add(i);
            }
            return list;
        }

        // Juega la próxima carta del combo. Devuelve false si no es válida.
        public bool ComboAdd(int handIdx)
        {
            int side = FollowSide;
            if (side < 0 || FollowIsHitBack) return false;
            if (handIdx < 0 || handIdx >= Hand[side].Count) return false;
            int card = Hand[side][handIdx];
            var d = Def(side, card);
            if (!CanFollow(side, LastPlayed, card)) return false;
            if (ComboCost(side, d) > FollowCpLeft) return false;
            if (d.IsSuper && Meter[side] < d.SuperCost) return false;

            Hand[side].RemoveAt(handIdx);
            Discard[side].Add(card);
            if (d.IsSuper) Meter[side] -= d.SuperCost;
            if (d.SelfDamage > 0 && Hp[side] > CardConfig.JainaYFreeBelow) AddSelf(side, d.SelfDamage);
            FollowCpLeft -= ComboCost(side, d);
            // paso de cadena (letra N → N+1): +1 super meter, al instante
            var prev = Def(side, LastPlayed);
            if (prev.Combo == ComboType.Chain && d.Combo == ComboType.Chain && d.ChainLetter == prev.ChainLetter + 1)
            {
                GainMeter(side, 1);
                if (side == 0) _r.Meter0++; else _r.Meter1++;
            }
            Damage(_followVictim, d.Damage, chip: false);
            _r.Combo(side).Add(card);
            LastPlayed = card;
            LastPumped = false;
            // sin opciones ni pump: el combo muere solo
            if (ComboOptions(side).Count == 0 && !CanPumpLast()) FollowupEnd();
            return true;
        }

        // ¿El último move que conectó admite pump con lo que hay en mano?
        public bool CanPumpLast()
        {
            int side = FollowSide;
            if (side < 0 || LastPlayed < 0 || LastPumped) return false;
            if (_noComboPump[side]) return false;
            var d = Def(side, LastPlayed);
            if (d.Pump == PumpFuel.None) return false;
            return FirstFuelIdx(side, d.Pump) >= 0;
        }

        int FirstFuelIdx(int side, PumpFuel fuel, HashSet<int> used = null)
        {
            // AnyCard: quemar lo menos valioso primero (esquive sobrante,
            // ataque flojo), nunca un bloqueo si hay alternativa
            int best = -1, bestRank = int.MaxValue;
            for (int i = 0; i < Hand[side].Count; i++)
            {
                if (used != null && used.Contains(i)) continue;
                var d = Def(side, Hand[side][i]);
                bool ok = fuel == PumpFuel.AnyCard ||
                          (fuel == PumpFuel.ZCard && Hand[side][i] == CardCatalog.SpecialZ) ||
                          (fuel == PumpFuel.SuperCard && d.IsSuper);
                if (!ok) continue;
                if (fuel != PumpFuel.AnyCard) return i;
                int rank = d.Kind == CardKind.Dodge ? 0
                    : d.Kind == CardKind.Attack && !d.IsSuper ? 1 + d.Damage
                    : d.Kind == CardKind.Throw ? 12
                    : d.IsSuper ? 20 : 15; // blocks 15: último recurso salvo supers
                if (rank < bestRank) { bestRank = rank; best = i; }
            }
            return best;
        }

        // Pumpea el último move conectado descartando hasta PumpMax cartas
        // válidas de la mano. packets = cuántos paquetes (cada uno +PumpDamage).
        public bool PumpLast(int packets)
        {
            int side = FollowSide;
            if (side < 0 || LastPlayed < 0 || LastPumped || packets <= 0) return false;
            if (_noComboPump[side]) return false;
            var d = Def(side, LastPlayed);
            if (d.Pump == PumpFuel.None) return false;
            packets = Math.Min(packets, d.PumpMax);
            var used = new HashSet<int>();
            for (int p = 0; p < packets; p++)
            {
                int idx = FirstFuelIdx(side, d.Pump, used);
                if (idx < 0) break;
                used.Add(idx);
            }
            if (used.Count == 0) return false;
            var idxs = new List<int>(used);
            idxs.Sort(); idxs.Reverse();
            foreach (int i in idxs) { Discard[side].Add(Hand[side][i]); Hand[side].RemoveAt(i); }
            int extra = used.Count * d.PumpDamage;
            Damage(_followVictim, extra, chip: false);
            if (side == 0) _r.PumpExtra0 += extra; else _r.PumpExtra1 += extra;
            LastPumped = true;
            if (!FollowIsHitBack && ComboOptions(side).Count == 0) FollowupEnd();
            else if (FollowIsHitBack) FollowupEnd();
            return true;
        }

        // El castigo (dodge a strike / unsafe bloqueado): UN golpe o agarre,
        // queda como ender (después solo pump). handIdx −1 = no castigar.
        public bool HitBack(int handIdx)
        {
            int side = FollowSide;
            if (side < 0 || !FollowIsHitBack || HitBackPlayed) return false;
            if (handIdx < 0) { FollowupEnd(); return true; }
            if (handIdx >= Hand[side].Count) return false;
            int card = Hand[side][handIdx];
            var d = Def(side, card);
            if (d.Kind != CardKind.Attack && d.Kind != CardKind.Throw) return false;
            if (d.IsSuper && Meter[side] < d.SuperCost) return false;
            Hand[side].RemoveAt(handIdx);
            Discard[side].Add(card);
            if (d.IsSuper) Meter[side] -= d.SuperCost;
            if (d.SelfDamage > 0 && Hp[side] > CardConfig.JainaYFreeBelow) AddSelf(side, d.SelfDamage);
            _r.HitBackSide = side; _r.HitBackCard = card;
            Damage(_followVictim, d.Damage, chip: false);
            LastPlayed = card;
            LastPumped = false;
            HitBackPlayed = true;
            if (!CanPumpLast()) FollowupEnd();
            return true;
        }

        // Cierra el followup: el ÚLTIMO move decide knockdown/edge.
        public void FollowupEnd()
        {
            int side = FollowSide;
            if (side < 0) return;
            if (LastPlayed >= 0)
            {
                var d = Def(side, LastPlayed);
                if (d.KnockdownOnHit) { if (_followVictim == 0) _r.KdNext0 = true; else _r.KdNext1 = true; }
            }
            FollowSide = -1;
            FollowIsHitBack = false;
            FinishCombat();
        }

        // ---- daño / cierre ----

        void Damage(int side, int dmg, bool chip)
        {
            if (side == 0) { _r.Dmg0 += dmg; if (chip) _r.Chip0 += dmg; else _r.Hit0 = true; }
            else { _r.Dmg1 += dmg; if (chip) _r.Chip1 += dmg; else _r.Hit1 = true; }
        }

        void AddSelf(int side, int dmg)
        {
            if (side == 0) _r.Self0 += dmg; else _r.Self1 += dmg;
        }

        void FinishCombat()
        {
            if (_combatFinished) return;
            _combatFinished = true;
            // recurring: vuelve si abriste con ella y NO recibiste un hit
            // (el chip y el self-damage NO cuentan — regla oficial)
            for (int s = 0; s < 2; s++)
            {
                int card = _r.Card(s);
                if (card < 0) continue;
                bool hit = s == 0 ? _r.Hit0 : _r.Hit1;
                if (_openedRecurring[s] && !hit)
                {
                    if (s == 0) _r.Returned0 = true; else _r.Returned1 = true;
                    AddToHand(s, card);
                }
                else Discard[s].Add(card);
            }

            // knockdown: si caerían los dos, se cancelan; dura UN combate
            bool kd0 = _r.KdNext0, kd1 = _r.KdNext1;
            if (kd0 && kd1) { kd0 = kd1 = false; _r.KdNext0 = _r.KdNext1 = false; }
            KnockedDown[0] = kd0;
            KnockedDown[1] = kd1;

            // the Edge: un combate; ambos = nadie; knockdown lo pisa
            bool e0 = _r.Edge0Next, e1 = _r.Edge1Next;
            if (e0 && e1) e0 = e1 = false;
            if (kd0 || kd1) e0 = e1 = false;
            Edge[0] = e0;
            Edge[1] = e1;

            // abilities ongoing: se consumen por combate
            for (int s = 0; s < 2; s++) if (Ongoing[s] > 0) Ongoing[s]--;

            Hp[0] = Math.Max(0, Hp[0] - _r.Dmg0 - _r.Self0);
            Hp[1] = Math.Max(0, Hp[1] - _r.Dmg1 - _r.Self1);
            FinishDeaths();

            SortHands();
            Active = 1 - Active;
        }

        void FinishDeaths()
        {
            if (Hp[0] <= 0 || Hp[1] <= 0)
            {
                Over = true;
                Winner = Hp[0] <= 0 && Hp[1] <= 0
                    ? (Hp[0] == Hp[1] ? -1 : (Hp[0] > Hp[1] ? 0 : 1))
                    : (Hp[0] <= 0 ? 1 : 0);
            }
            else if (Over)
            {
                // TIME OVER a mitad del combate: juzgar con el daño aplicado
                Winner = Hp[0] == Hp[1] ? -1 : (Hp[0] > Hp[1] ? 0 : 1);
            }
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
            if (handIdx >= 0 && handIdx < Hand[side].Count)
            {
                Discard[side].Add(Hand[side][handIdx]);
                Hand[side].RemoveAt(handIdx);
                wildCount++;
            }
            while (!Over)
            {
                int card = TopOfDeck(side);
                if (card < 0) return CardCatalog.Dodge;
                var def = Def(side, card);
                bool invalid = def.Kind == CardKind.Ability ||
                               (def.Kind == CardKind.Dodge && KnockedDown[side]) ||
                               (def.IsSuper && Meter[side] < def.SuperCost);
                // regla: si el wild swing da una super Y TENÉS meter, DEBÉS jugarla
                if (invalid) { Discard[side].Add(card); wildCount++; continue; }
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

        // Mazo vacío: la PRIMERA vez remezcla dejando blocks y UNA copia de
        // cada super en el descarte; la SEGUNDA es TIME OVER.
        bool RefillDeck(int side)
        {
            DeckOuts[side]++;
            if (DeckOuts[side] >= 2) { TimeOver(); return false; }
            bool lowKept = false, highKept = false, s1Kept = false, s2Kept = false;
            var keep = new List<int>();
            var shuffleIn = new List<int>();
            foreach (int c in Discard[side])
            {
                if (c == CardCatalog.LowBlock && !lowKept) { keep.Add(c); lowKept = true; }
                else if (c == CardCatalog.HighBlock && !highKept) { keep.Add(c); highKept = true; }
                else if (c == CardCatalog.Super1 && !s1Kept) { keep.Add(c); s1Kept = true; }
                else if (c == CardCatalog.Super2 && !s2Kept) { keep.Add(c); s2Kept = true; }
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
