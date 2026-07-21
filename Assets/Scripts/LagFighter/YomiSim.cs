using System;

namespace LagFighter
{
    // ---- MODO YOMI v2 (2026-07-20): resolución DISCRETA por tabla ----
    // Dos distancias (CERCA/LEJOS), UNA acción por turno y por jugador,
    // revelación simultánea. Nada de frames: la matriz de counters ES la ley
    // (ver DESIGN.md, sección Modo YOMI). La sim de frames queda solo como
    // teatro de presentación en MatchController.
    // Pura y determinista (sin UnityEngine): cada celda de la matriz es un test.

    public enum YomiAction
    {
        Jab = 0,     // 1 AP · solo cerca · gana a Kick/Agarre, caza el salto en el despegue
        Kick = 1,    // 2 AP · ambas distancias · caza dashes; pierde con Jab (cerca), Salto y Parry
        Grab = 2,    // 2 AP · solo cerca · rompe Parry, tira a LEJOS; pierde con golpes
        Parry = 3,   // 1 AP · si bloquea un golpe: +1 AP y devuelve 1 (el rechazo); pierde con Agarre
        Shoryu = 4,  // 3 AP · cerca: gana a TODO · lejos: SOLO antiaéreo de lectura · whiff = recovery
        Dash = 5,    // 1 AP · cerca: te vas (esquiva Jab/Agarre/Shoryu, Kick te caza) · lejos: entrás
        Jump = 6,    // 2 AP · cerca: escapás saltando (Jab te baja) · lejos: entrás con patada
        Charge = 7,  // 0 AP · +2 AP si no te pegan; todo golpe al que carga es counter (+1)
        Recovery = 8 // forzada: whiffeaste el Shoryu — perdés el turno, te pegan con counter
    }

    public static class YomiConfig
    {
        public const int MaxHp = 6;
        public const int StartAp = 3;
        public const int ApIncome = 1;    // automático al cerrar cada turno
        public const int ApCap = 6;
        public const int ChargeGain = 2;  // Cargar, si nadie te pegó
        public const int ParryGain = 1;   // parry exitoso
        public const int RiposteDamage = 1;
        public const int CounterBonus = 1; // golpe al que carga o está en recovery
        public const int TurnsPerRound = 20;

        public static int Cost(YomiAction a)
        {
            switch (a)
            {
                case YomiAction.Jab: case YomiAction.Parry: case YomiAction.Dash: return 1;
                case YomiAction.Kick: case YomiAction.Grab: case YomiAction.Jump: return 2;
                case YomiAction.Shoryu: return 3;
                default: return 0; // Charge, Recovery
            }
        }

        // daño base del golpe de cada acción (Jump = la patada de entrada)
        public static int Damage(YomiAction a)
        {
            switch (a)
            {
                case YomiAction.Jab: case YomiAction.Jump: return 1;
                case YomiAction.Kick: case YomiAction.Grab: return 2;
                case YomiAction.Shoryu: return 3;
                default: return 0;
            }
        }

        public static string Name(YomiAction a)
        {
            switch (a)
            {
                case YomiAction.Jab: return "Jab";
                case YomiAction.Kick: return "Kick";
                case YomiAction.Grab: return "Agarre";
                case YomiAction.Parry: return "Parry";
                case YomiAction.Shoryu: return "Shoryuken";
                case YomiAction.Dash: return "Dash";
                case YomiAction.Jump: return "Salto";
                case YomiAction.Charge: return "Cargar";
                default: return "Recovery";
            }
        }

        public static string Chip(YomiAction a)
        {
            switch (a)
            {
                case YomiAction.Jab: return "JAB";
                case YomiAction.Kick: return "KCK";
                case YomiAction.Grab: return "AGR";
                case YomiAction.Parry: return "PRY";
                case YomiAction.Shoryu: return "SHO";
                case YomiAction.Dash: return "DSH";
                case YomiAction.Jump: return "SLT";
                case YomiAction.Charge: return "CRG";
                default: return "REC";
            }
        }
    }

    // Todo lo que pasó en un turno, para el teatro, el log y los tests.
    public struct YomiTurnResult
    {
        public YomiAction A0, A1;
        public int Dmg0, Dmg1;          // daño RECIBIDO por cada lado
        public bool Parry0, Parry1;     // ese lado parryó con éxito (+AP y rechazo)
        public bool Charged0, Charged1; // la carga entró (+2)
        public bool Counter0, Counter1; // el daño recibido fue counter (cargaba / recovery)
        public bool Tech;               // agarre vs agarre
        public bool Rec0Next, Rec1Next; // whiffeó el Shoryu: pierde el próximo turno
        public bool CloseBefore, CloseAfter;

        public YomiAction Act(int i) => i == 0 ? A0 : A1;
        public int Dmg(int i) => i == 0 ? Dmg0 : Dmg1;
        public bool Parried(int i) => i == 0 ? Parry0 : Parry1;
        public bool Charged(int i) => i == 0 ? Charged0 : Charged1;
        public bool RecNext(int i) => i == 0 ? Rec0Next : Rec1Next;
    }

    public class YomiSim
    {
        public readonly int[] Hp = { YomiConfig.MaxHp, YomiConfig.MaxHp };
        public readonly int[] Ap = { YomiConfig.StartAp, YomiConfig.StartAp };
        public readonly bool[] Recovery = new bool[2];
        public bool Close = true;   // arranca CERCA (pedido 2026-07-20): acción desde el turno 1
        public int Turn;            // turnos ya resueltos
        public bool Over;
        public int Winner = -1;     // -1 = empate / sigue

        public bool Legal(int side, YomiAction a)
        {
            if (Recovery[side]) return a == YomiAction.Recovery;
            if (a == YomiAction.Recovery) return false;
            if (YomiConfig.Cost(a) > Ap[side]) return false;
            if (!Close && (a == YomiAction.Jab || a == YomiAction.Grab)) return false; // no llegan
            return true;
        }

        // La matriz. Muta el estado y devuelve el detalle del turno.
        public YomiTurnResult Resolve(YomiAction a0, YomiAction a1)
        {
            // defensa: una acción ilegal degenera en Cargar (la UI y la IA no
            // deberían mandarla nunca; esto evita estados rotos)
            if (!Legal(0, a0)) a0 = Recovery[0] ? YomiAction.Recovery : YomiAction.Charge;
            if (!Legal(1, a1)) a1 = Recovery[1] ? YomiAction.Recovery : YomiAction.Charge;

            var r = new YomiTurnResult { A0 = a0, A1 = a1, CloseBefore = Close, CloseAfter = Close };

            Ap[0] -= YomiConfig.Cost(a0);
            Ap[1] -= YomiConfig.Cost(a1);

            bool Is(YomiAction x, YomiAction y) => (a0 == x && a1 == y) || (a0 == y && a1 == x);
            int SideOf(YomiAction x) => a0 == x ? 0 : 1;

            void Hit(int side, int dmg)
            {
                var victim = side == 0 ? a0 : a1;
                bool counter = victim == YomiAction.Charge || victim == YomiAction.Recovery;
                if (counter) dmg += YomiConfig.CounterBonus;
                if (side == 0) { r.Dmg0 += dmg; r.Counter0 |= counter; }
                else { r.Dmg1 += dmg; r.Counter1 |= counter; }
            }
            void ParryOk(int side)
            {
                if (side == 0) r.Parry0 = true; else r.Parry1 = true;
                Hit(1 - side, YomiConfig.RiposteDamage); // el rechazo
            }
            void WhiffRecovery(int side) { if (side == 0) r.Rec0Next = true; else r.Rec1Next = true; }

            if (Close) ResolveClose(a0, a1, ref r, Is, SideOf, Hit, ParryOk, WhiffRecovery);
            else ResolveFar(a0, a1, ref r, Is, SideOf, Hit, ParryOk, WhiffRecovery);

            // la carga entra solo si no te pegaron
            r.Charged0 = a0 == YomiAction.Charge && r.Dmg0 == 0;
            r.Charged1 = a1 == YomiAction.Charge && r.Dmg1 == 0;
            if (r.Charged0) Ap[0] += YomiConfig.ChargeGain;
            if (r.Charged1) Ap[1] += YomiConfig.ChargeGain;
            if (r.Parry0) Ap[0] += YomiConfig.ParryGain;
            if (r.Parry1) Ap[1] += YomiConfig.ParryGain;

            Hp[0] = Math.Max(0, Hp[0] - r.Dmg0);
            Hp[1] = Math.Max(0, Hp[1] - r.Dmg1);

            Recovery[0] = r.Rec0Next;
            Recovery[1] = r.Rec1Next;

            Ap[0] = Math.Min(YomiConfig.ApCap, Ap[0] + YomiConfig.ApIncome);
            Ap[1] = Math.Min(YomiConfig.ApCap, Ap[1] + YomiConfig.ApIncome);

            Close = r.CloseAfter;
            Turn++;
            if (Hp[0] <= 0 || Hp[1] <= 0)
            {
                Over = true;
                Winner = Hp[0] <= 0 && Hp[1] <= 0 ? -1 : Hp[0] <= 0 ? 1 : 0;
            }
            return r;
        }

        // ---- DE CERCA ----
        static void ResolveClose(YomiAction a0, YomiAction a1, ref YomiTurnResult r,
            Func<YomiAction, YomiAction, bool> Is, Func<YomiAction, int> SideOf,
            Action<int, int> Hit, Action<int> ParryOk, Action<int> WhiffRecovery)
        {
            const YomiAction J = YomiAction.Jab, K = YomiAction.Kick, G = YomiAction.Grab,
                P = YomiAction.Parry, S = YomiAction.Shoryu, D = YomiAction.Dash,
                U = YomiAction.Jump, C = YomiAction.Charge, R = YomiAction.Recovery;

            bool escapes = a0 == D || a0 == U || a1 == D || a1 == U; // alguien se va → LEJOS salvo que lo bajen

            // espejo
            if (Is(J, J)) { Hit(0, 1); Hit(1, 1); return; }                        // trade
            if (Is(K, K)) { Hit(0, 2); Hit(1, 2); return; }                        // trade
            if (Is(G, G)) { r.Tech = true; return; }                               // TECH
            if (Is(S, S)) { Hit(0, 3); Hit(1, 3); r.CloseAfter = false; return; }  // doble KD
            if (Is(D, D) || Is(U, U) || Is(D, U)) { r.CloseAfter = false; return; }
            if (Is(P, P) || Is(C, C) || Is(R, R) || Is(P, C) || Is(P, R) || Is(C, R)) return;

            // Shoryuken: gana a todo en corta… salvo que el rival se haya ido
            if (a0 == S || a1 == S)
            {
                int s = SideOf(S), o = 1 - s;
                var other = s == 0 ? a1 : a0;
                if (other == D) { r.CloseAfter = false; WhiffRecovery(s); return; }  // se fue por abajo: whiff
                Hit(o, 3);
                r.CloseAfter = false; // KD: manda a LEJOS
                return;
            }

            // Jab: gana a Kick/Agarre/Cargar/Recovery, caza el salto en el despegue; Dash lo esquiva
            if (a0 == J || a1 == J)
            {
                int j = SideOf(J), o = 1 - j;
                var other = j == 0 ? a1 : a0;
                if (other == P) { ParryOk(o); return; }
                if (other == D) { r.CloseAfter = false; return; }        // esquivado
                if (other == U) { Hit(o, 1); return; }                   // lo baja: se queda CERCA
                Hit(o, 1);                                               // K, G, C, R
                return;
            }

            // Kick: gana a Agarre/Cargar/Recovery y CAZA el dash que se retira; el salto lo esquiva
            if (a0 == K || a1 == K)
            {
                int k = SideOf(K), o = 1 - k;
                var other = k == 0 ? a1 : a0;
                if (other == P) { ParryOk(o); return; }
                if (other == D) { Hit(o, 2); r.CloseAfter = false; return; } // lo caza saliendo
                if (other == U) { r.CloseAfter = false; return; }            // aéreo: esquiva
                Hit(o, 2);                                                   // G, C, R
                return;
            }

            // Agarre: rompe Parry (tira a LEJOS); Dash/Salto lo whiffean
            if (a0 == G || a1 == G)
            {
                int g = SideOf(G), o = 1 - g;
                var other = g == 0 ? a1 : a0;
                if (other == P || other == C || other == R) { Hit(o, 2); r.CloseAfter = false; return; }
                r.CloseAfter = false; // whiff contra D/U: el otro igual se fue
                return;
            }

            // Parry contra movimiento / carga: no hay nada que parryar
            if (escapes) r.CloseAfter = false;
        }

        // ---- DE LEJOS ---- (Jab y Agarre no llegan: ilegales acá)
        static void ResolveFar(YomiAction a0, YomiAction a1, ref YomiTurnResult r,
            Func<YomiAction, YomiAction, bool> Is, Func<YomiAction, int> SideOf,
            Action<int, int> Hit, Action<int> ParryOk, Action<int> WhiffRecovery)
        {
            const YomiAction K = YomiAction.Kick, P = YomiAction.Parry, S = YomiAction.Shoryu,
                D = YomiAction.Dash, U = YomiAction.Jump, C = YomiAction.Charge, R = YomiAction.Recovery;

            // espejo
            if (Is(K, K)) { Hit(0, 2); Hit(1, 2); return; }                        // trade
            if (Is(U, U)) { r.CloseAfter = true; return; }                         // se cruzan en el aire
            if (Is(D, D)) { r.CloseAfter = true; return; }
            if (Is(S, S)) { WhiffRecovery(0); WhiffRecovery(1); return; }          // doble whiff
            if (Is(P, P) || Is(C, C) || Is(R, R) || Is(P, C) || Is(P, R) || Is(C, R)) return;

            // Shoryuken de lejos: SOLO lectura antiaérea — le gana únicamente al salto entrante
            if (a0 == S || a1 == S)
            {
                int s = SideOf(S), o = 1 - s;
                var other = s == 0 ? a1 : a0;
                if (other == U) { Hit(o, 3); return; }                    // antiaéreo: lo baja, sigue LEJOS
                if (other == K) { Hit(s, 2); return; }                    // whiffeás y la patada te alcanza
                WhiffRecovery(s);                                         // whiff limpio: recovery
                if (other == D) r.CloseAfter = true;                      // el dash entró igual
                return;
            }

            // Salto adelante: entra con patada — le gana a Kick/Dash/Cargar/Recovery; Parry lo bloquea al llegar
            if (a0 == U || a1 == U)
            {
                int u = SideOf(U), o = 1 - u;
                var other = u == 0 ? a1 : a0;
                r.CloseAfter = true; // llegás sí o sí
                if (other == P) { ParryOk(o); return; }
                Hit(o, 1);           // K (lo saltaste y castigás), D, C, R
                return;
            }

            // Kick: la zoneadora — caza al dash que entra y al que carga
            if (a0 == K || a1 == K)
            {
                int k = SideOf(K), o = 1 - k;
                var other = k == 0 ? a1 : a0;
                if (other == P) { ParryOk(o); return; }
                if (other == D) { Hit(o, 2); return; }   // frenado: sigue LEJOS
                Hit(o, 2);                                // C, R
                return;
            }

            // Dash adelante contra parry/carga/recovery: entrás gratis
            if (a0 == D || a1 == D) r.CloseAfter = true;
        }
    }
}
