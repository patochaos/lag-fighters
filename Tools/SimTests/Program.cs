using System;
using System.Collections.Generic;
using LagFighter;

// Tests de framedata sobre la sim pura y determinista. Sin frameworks:
// cada test es un escenario armado a mano con el resultado esperado.
class Tests
{
    static int _passed, _failed;

    static void Check(bool cond, string name, string detail = "")
    {
        if (cond) { _passed++; Console.WriteLine($"  ok  {name}"); }
        else { _failed++; Console.WriteLine($"FALLO {name}{(detail == "" ? "" : $" — {detail}")}"); }
    }

    static MatchSim NewSim(float x0, float x1, bool p1Blocks)
    {
        var s = new MatchSim();
        s.Fighters[0].X = x0;
        s.Fighters[1].X = x1;
        s.Fighters[1].BlockEnabled = p1Blocks;
        return s;
    }

    static List<SimEvent> Run(MatchSim s, int frames)
    {
        var evs = new List<SimEvent>();
        for (int t = 0; t < frames && !s.Over; t++)
        {
            s.Step();
            evs.AddRange(s.LastEvents);
        }
        return evs;
    }

    static SimEvent? Find(List<SimEvent> evs, EvKind kind, int attacker = -1)
    {
        foreach (var e in evs)
            if (e.Kind == kind && (attacker < 0 || e.Attacker == attacker)) return e;
        return null;
    }

    static void Main()
    {
        JabOnHitEsMasDos();
        JabOnBlockEsMenosCinco();
        SweepDerriba();
        TatsuAtraviesaHadouken();
        ShoryuInvulnerableAlArranque();
        LosJabsBloqueadosRompenLaGuardia();
        DpBloqueadoEsMenosQuince();
        ElFinalDelTatsuComeProyectiles();
        LaEsquinaEmpujaAlAtacante();
        AgarreVsAgarreEsTech();
        WakeupAjustaElKnockdown();
        if (SimConfig.LimbsEnabled)
        {
            TresJabsArrancanElBrazo();
            GolpesBajosArrancanLaPierna();
        }
        else Console.WriteLine("  --  miembros: desactivado (SimConfig.LimbsEnabled), tests salteados");
        if (SimConfig.CrouchEnabled)
        {
            ElJabPasaSobreElAgachado();
            ElHadoukenPasaSobreElAgachado();
            LaPatadaBajaPegaAlAgachado();
        }
        else
        {
            Console.WriteLine("  --  agachado: desactivado (SimConfig.CrouchEnabled), tests salteados");
            ElAgachadoDesactivadoDegradaAEsperar();
        }
        CodigoDeTurnoIdaYVuelta();
        MismaEntradaMismaPelea();

        Console.WriteLine($"\n{_passed} ok, {_failed} fallos");
        Environment.Exit(_failed > 0 ? 1 : 0);
    }

    // "A on hit = +2": la ventaja del jab conectado es exactamente +2f.
    static void JabOnHitEsMasDos()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: false);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        var ev = Find(Run(s, 30), EvKind.Hit, 0);
        Check(ev.HasValue && ev.Value.FrameAdv == 2 && ev.Value.Damage == 1f,
            "jab on hit = +2, 1 dmg", ev.HasValue ? $"adv {ev.Value.FrameAdv}, dmg {ev.Value.Damage}" : "no conectó");
    }

    // "A on block = −5".
    static void JabOnBlockEsMenosCinco()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: true); // P1 en neutral bloquea
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        var ev = Find(Run(s, 30), EvKind.Blocked, 0);
        Check(ev.HasValue && ev.Value.FrameAdv == -5,
            "jab on block = −5", ev.HasValue ? $"adv {ev.Value.FrameAdv}" : "no lo bloqueó");
    }

    static void SweepDerriba()
    {
        var s = NewSim(-0.5f, 0.6f, p1Blocks: false);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackB });
        var evs = Run(s, 25);
        var ev = Find(evs, EvKind.Hit, 0);
        Check(ev.HasValue && ev.Value.Damage == 2f && s.Fighters[1].Stun == StunKind.Knockdown,
            "sweep pega 2 y derriba", $"stun {s.Fighters[1].Stun}");
    }

    // "tatsu atraviesa hadouken": el proyectil no le pega girando (frames 8..40).
    static void TatsuAtraviesaHadouken()
    {
        var s = NewSim(-1f, 3.5f, p1Blocks: false);
        s.Projectiles.Add(new Projectile { Owner = 1, X = 0f, Dir = -1, Alive = true });
        s.SetQueue(0, new List<int> { MoveCatalog.Tatsu });
        var evs = Run(s, 46);
        Check(s.Fighters[0].Hp == SimConfig.MaxHp && Find(evs, EvKind.Hit, 1) == null,
            "tatsu atraviesa hadoukens", $"hp {s.Fighters[0].Hp}");
    }

    // Invuln 1..10 del shoryu: un proyectil encima no conecta en el arranque.
    static void ShoryuInvulnerableAlArranque()
    {
        var s = NewSim(-1f, 3.5f, p1Blocks: false);
        s.Projectiles.Add(new Projectile { Owner = 1, X = -0.9f, Dir = -1, Alive = true });
        s.SetQueue(0, new List<int> { MoveCatalog.Shoryuken });
        var evs = Run(s, 12);
        Check(s.Fighters[0].Hp == SimConfig.MaxHp && Find(evs, EvKind.Hit, 1) == null,
            "shoryu invulnerable frames 1-10", $"hp {s.Fighters[0].Hp}");
    }

    static void LosJabsBloqueadosRompenLaGuardia()
    {
        // genérico sobre las constantes: con barra de 70 y jab −15 son 5 jabs
        int perJab = 15;
        int needed = (int)Math.Ceiling(SimConfig.GuardMax / (double)perJab);
        var s = NewSim(-0.5f, 0.5f, p1Blocks: true);
        int blocked = 0;
        SimEvent? crush = null;
        for (int turn = 0; turn < 10 && crush == null; turn++)
        {
            s.SetQueue(0, new List<int> { MoveCatalog.WalkF, MoveCatalog.AttackA, MoveCatalog.AttackA });
            s.SetQueue(1, new List<int>());
            for (int t = 0; t < SimConfig.TurnFrames && crush == null; t++)
            {
                s.Step();
                foreach (var e in s.LastEvents)
                {
                    if (e.Kind == EvKind.Blocked) blocked++;
                    if (e.Kind == EvKind.GuardCrush) crush = e;
                }
            }
            s.OnTurnEnd(0);
            s.OnTurnEnd(1);
        }
        Check(crush.HasValue && blocked == needed - 1 && s.Fighters[1].Guard == SimConfig.GuardCrushRespawn,
            $"guard crush al {needed}° jab bloqueado, barra renace al 50%",
            $"bloqueados {blocked}, guardia {s.Fighters[1].Guard}");
    }

    // La ventaja del DP bloqueado en el primer frame activo (la peor para el
    // defensor) tiene que coincidir con lo que dice la carta: −15.
    static void DpBloqueadoEsMenosQuince()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.Shoryuken });
        var ev = Find(Run(s, 50), EvKind.Blocked, 0);
        Check(ev.HasValue && ev.Value.FrameAdv == -15,
            "shoryu bloqueado = −15 en el primer frame activo",
            ev.HasValue ? $"adv {ev.Value.FrameAdv}" : "no lo bloqueó");
    }

    // La inmunidad del tatsu termina en 34: un proyectil que llega al final
    // del giro SÍ conecta (el recovery quedó castigable).
    static void ElFinalDelTatsuComeProyectiles()
    {
        var s = NewSim(-1f, 3.9f, p1Blocks: false);
        s.Projectiles.Add(new Projectile { Owner = 1, X = 2.75f, Dir = -1, Alive = true });
        s.SetQueue(0, new List<int> { MoveCatalog.Tatsu });
        Run(s, 46);
        Check(s.Fighters[0].Hp < SimConfig.MaxHp,
            "la inmunidad del tatsu termina en 34: el final come proyectiles",
            $"hp {s.Fighters[0].Hp}");
    }

    static void LaEsquinaEmpujaAlAtacante()
    {
        var s = NewSim(SimConfig.StageHalfWidth - 1f, SimConfig.StageHalfWidth, p1Blocks: false);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        float before = s.Fighters[0].X;
        Run(s, 15);
        Check(s.Fighters[0].X < before - 0.05f,
            "el pushback en la esquina se transfiere al atacante",
            $"P0 {before:0.00} → {s.Fighters[0].X:0.00}");
    }

    static void AgarreVsAgarreEsTech()
    {
        var s = NewSim(-0.45f, 0.45f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.Grab });
        s.SetQueue(1, new List<int> { MoveCatalog.Grab });
        var evs = Run(s, 20);
        Check(Find(evs, EvKind.Tech) != null && s.Fighters[0].Hp == 6f && s.Fighters[1].Hp == 6f,
            "agarre vs agarre = tech, nadie come daño");
    }

    static void WakeupAjustaElKnockdown()
    {
        var s = NewSim(-0.5f, 0.6f, p1Blocks: false);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackB }); // derriba
        Run(s, 25);
        int before = s.StunRemaining(1);
        s.AdjustKnockdown(1, -16);
        int quick = s.StunRemaining(1);
        s.AdjustKnockdown(1, 32); // rápido → quedarse
        int stay = s.StunRemaining(1);
        Check(before > 0 && quick == before - 16 && stay == quick + 32,
            "wakeup ajusta el knockdown arrastrado", $"{before} → {quick} → {stay}");
    }

    // Pérdida de miembros: 3 de daño arriba de la cintura vuelan el brazo,
    // y sin brazo no hay ni A ni Hadouken.
    static void TresJabsArrancanElBrazo()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: false);
        SimEvent? lost = null;
        for (int turn = 0; turn < 4 && lost == null; turn++)
        {
            s.SetQueue(0, new List<int> { MoveCatalog.WalkF, MoveCatalog.AttackA, MoveCatalog.AttackA });
            s.SetQueue(1, new List<int>());
            for (int t = 0; t < SimConfig.TurnFrames && lost == null; t++)
            {
                s.Step();
                var e = Find(s.LastEvents, EvKind.LimbLost, 0);
                if (e.HasValue) lost = e;
            }
            s.OnTurnEnd(0);
            s.OnTurnEnd(1);
        }
        Check(lost.HasValue && lost.Value.Limb == Limb.Arm && s.Fighters[1].ArmHp == 0f &&
              !s.MoveAllowed(1, MoveCatalog.AttackA) && !s.MoveAllowed(1, MoveCatalog.Hadouken) &&
              s.MoveAllowed(1, MoveCatalog.AttackB),
            "3 de daño arriba vuelan el brazo (chau A y hadouken)",
            $"armHp {s.Fighters[1].ArmHp}");
    }

    // Golpes bajos (sweep, Y bajo la cintura) comen la pierna: chau B/tatsu.
    static void GolpesBajosArrancanLaPierna()
    {
        var s = NewSim(-0.5f, 0.6f, p1Blocks: false);
        SimEvent? lost = null;
        for (int turn = 0; turn < 5 && lost == null; turn++)
        {
            s.SetQueue(0, new List<int> { MoveCatalog.WalkF, MoveCatalog.AttackB });
            s.SetQueue(1, new List<int>());
            for (int t = 0; t < SimConfig.TurnFrames && lost == null; t++)
            {
                s.Step();
                var e = Find(s.LastEvents, EvKind.LimbLost, 0);
                if (e.HasValue) lost = e;
            }
            s.OnTurnEnd(0);
            s.OnTurnEnd(1);
        }
        Check(lost.HasValue && lost.Value.Limb == Limb.Leg && s.Fighters[1].LegHp == 0f &&
              !s.MoveAllowed(1, MoveCatalog.AttackB) && !s.MoveAllowed(1, MoveCatalog.Tatsu) &&
              s.MoveAllowed(1, MoveCatalog.AttackA),
            "golpes bajos vuelan la pierna (chau B y tatsu)",
            $"legHp {s.Fighters[1].LegHp}");
    }

    // Agacharse: hurtbox de 0.9 → el jab (Y0 1.0) pasa por arriba.
    static void ElJabPasaSobreElAgachado()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        s.SetQueue(1, new List<int> { MoveCatalog.Crouch, MoveCatalog.Crouch, MoveCatalog.Crouch });
        var evs = Run(s, 30);
        Check(Find(evs, EvKind.Hit, 0) == null && Find(evs, EvKind.Blocked, 0) == null &&
              Find(evs, EvKind.Whiff, 0) != null && s.Fighters[1].Guard == SimConfig.GuardMax,
            "el jab pasa por arriba del agachado (ni guardia gasta)");
    }

    static void ElHadoukenPasaSobreElAgachado()
    {
        var s = NewSim(-1.5f, 1.5f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.Hadouken });
        s.SetQueue(1, new List<int> { MoveCatalog.Crouch, MoveCatalog.Crouch, MoveCatalog.Crouch,
                                      MoveCatalog.Crouch, MoveCatalog.Crouch, MoveCatalog.Crouch });
        var evs = Run(s, 70);
        Check(Find(evs, EvKind.Hit, 0) == null && Find(evs, EvKind.Blocked, 0) == null &&
              s.Fighters[1].Hp == SimConfig.MaxHp,
            "el hadouken pasa por arriba del agachado");
    }

    static void LaPatadaBajaPegaAlAgachado()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: false);
        s.SetQueue(0, new List<int> { MoveCatalog.LowKick });
        s.SetQueue(1, new List<int> { MoveCatalog.Crouch, MoveCatalog.Crouch, MoveCatalog.Crouch });
        var ev = Find(Run(s, 30), EvKind.Hit, 0);
        Check(ev.HasValue && ev.Value.FrameAdv == 2,
            "la patada baja pega al agachado y es +2", ev.HasValue ? $"adv {ev.Value.FrameAdv}" : "no pegó");
    }

    // Con el agachado desactivado, un código async con Crouch/LowKick no debe
    // romper nada: la orden degrada a Esperar.
    static void ElAgachadoDesactivadoDegradaAEsperar()
    {
        var s = NewSim(-2f, 2f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.Crouch, MoveCatalog.LowKick });
        s.SetQueue(1, new List<int>());
        s.Step();
        Check(s.CurrentMove(0) != null && s.CurrentMove(0).Id == "wait" && !s.IsCrouching(0),
            "agachado off: Crouch/LowKick degradan a Esperar");
    }

    // Online asincrónico: el código de turno serializa y deserializa exacto,
    // y rechaza basura.
    static void CodigoDeTurnoIdaYVuelta()
    {
        var plan = new List<int> { MoveCatalog.WalkF, MoveCatalog.AttackA, MoveCatalog.Shoryuken };
        string code = TurnCode.Encode(1, 7, wakeQuick: false, plan);
        bool ok = TurnCode.TryDecode(code, out int side, out int turn, out bool quick, out var moves);
        bool roundtrip = ok && side == 1 && turn == 7 && !quick &&
                         moves.Count == 3 && moves[0] == MoveCatalog.WalkF &&
                         moves[1] == MoveCatalog.AttackA && moves[2] == MoveCatalog.Shoryuken;
        bool rejects = !TurnCode.TryDecode("hola", out _, out _, out _, out _) &&
                       !TurnCode.TryDecode("LF!!!!", out _, out _, out _, out _) &&
                       !TurnCode.TryDecode("", out _, out _, out _, out _);
        Check(roundtrip && rejects, "código de turno: ida y vuelta exacta, rechaza basura", code);
    }

    // La base de todo: misma entrada, misma pelea, bit a bit.
    static void MismaEntradaMismaPelea()
    {
        var a = PeleaConSeed(99);
        var b = PeleaConSeed(99);
        Check(a == b, "determinismo: misma entrada, misma pelea", $"{a} vs {b}");
    }

    static string PeleaConSeed(int seed)
    {
        var s = new MatchSim();
        var ai0 = new SimpleAI(seed);
        var ai1 = new SimpleAI(seed + 1);
        for (int turn = 0; turn < 30 && !s.Over; turn++)
        {
            s.SetQueue(0, ai0.Plan(s, 0, SimConfig.TurnFrames));
            s.SetQueue(1, ai1.Plan(s, 1, SimConfig.TurnFrames));
            for (int t = 0; t < SimConfig.TurnFrames && !s.Over; t++) s.Step();
            s.OnTurnEnd(0);
            s.OnTurnEnd(1);
        }
        return $"{s.Tick}|{s.Winner}|{s.Fighters[0].Hp}|{s.Fighters[1].Hp}|{s.Fighters[0].X:0.0000}|{s.Fighters[1].X:0.0000}";
    }
}
