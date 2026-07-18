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
        int matches = args.Length > 0 ? int.Parse(args[0]) : 3000;
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

        for (int m = 0; m < matches; m++)
        {
            var sim = new MatchSim();
            var ai0 = new SimpleAI(m * 2 + 1);
            var ai1 = new SimpleAI(m * 2 + 2);
            int crushesThis = 0;

            // 15 turnos por round, como el juego real (TIME OVER → juez por vida)
            for (int turn = 0; turn < 15 && !sim.Over; turn++)
            {
                var p0 = ai0.Plan(sim, 0, SimConfig.TurnFrames);
                var p1 = ai1.Plan(sim, 1, SimConfig.TurnFrames);
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
                sim.OnTurnEnd(0);
                sim.OnTurnEnd(1);
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

    // Mismo criterio que el juego: KO manda, TIME OVER lo decide la vida.
    static int Judge(MatchSim sim)
    {
        if (sim.Over) return sim.Winner;
        float h0 = sim.Fighters[0].Hp, h1 = sim.Fighters[1].Hp;
        return h0 > h1 ? 0 : h1 > h0 ? 1 : -1;
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
                    for (; turn < 15 && !sim.Over; turn++) // reglas reales: TIME OVER a los 15
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
        Console.WriteLine($"\npeleas decididas por el juez (TIME OVER a los 15): {timeouts}");
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
