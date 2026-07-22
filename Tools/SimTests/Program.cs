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
        ElParryRecargaGuardia();
        LaGuardiaRegeneraSoloJugando();
        ElFinalDelTatsuComeProyectiles();
        LaEsquinaEmpujaAlAtacante();
        AgarreVsAgarreEsTech();
        ParryRechazaUnJab();
        AgarreLeGanaAlParry();
        AtaqueDemoradoCastigaElParry();
        ParryDesactivaProyectilSinStunearAlZoner();
        PerfilesDeIAMantienenPresupuesto();
        WakeupAjustaElKnockdown();
        TurnoFluidoCruzaElLimite();
        LosCostosDeApSonLosEsperados();
        ElSlotDeApEspaciaLaCola();
        ElGolpeDevuelveElRestoDelSlot();
        LaEconomiaDeApRespira();
        ElBloqueoBancadoEsSoloConLaCarta();
        LaIaRespetaSuStock();
        ElReversalLevantaYSepara();
        LaSuperArrasaYPegaCuatro();
        YomiElJabLeGanaAlAgarre();
        YomiElGolpeFuerteEsAntiaereo();
        YomiMatrizDeCerca();
        YomiMatrizDeLejos();
        YomiShoryuEsUnaApuesta();
        YomiEconomiaDiscreta();
        YomiRecoveryYCounters();
        YomiKoYTech();
        YomiMatrizCompletaNoExplota();
        CartasAtaqueLeGanaAlAgarre();
        CartasSpeedYEmpateAlActivo();
        CartasAlturasDelBloqueo();
        CartasChipLockdownYRecurring();
        CartasProyectilVsProyectil();
        CartasUnsafeSeCastiga();
        CartasDodgeCastigaStrikesNoProyectiles();
        CartasThrowDerribaYElKnockdownApura();
        CartasThrowVsThrowEmpateAlActivo();
        CartasWildSwingDerribado();
        CartasRemezclaUnaVezYTimeOver();
        CartasLimiteDeMano();
        CartasExchangeSoloNormalesYDosVeces();
        CartasManoInicialGarantizada();
        CartasComboEncadenaYDaMeter();
        CartasComboPointsLimitan();
        CartasKnockdownSoloSinCombo();
        CartasPowerUpPagaSupers();
        CartasWindSummon();
        CartasArcShot();
        CartasJainaSelfDamageYSegura();
        CartasRecklessness();
        CartasPumpDeZ();
        CartasSuperDodgeContragolpea();
        CartasNivelesDeProyectil();
        CartasMismaSeedMismaPartida();
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
            ElAgachadoDesactivadoQuedaNeutral();
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

    // El parry exitoso recarga guardia (anti-chip del zoner).
    static void ElParryRecargaGuardia()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: true);
        s.Fighters[1].Guard = 30f;
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });          // jab activo en f6..9
        s.SetQueue(1, new List<int> { MoveCatalog.Parry, MoveCatalog.Parry }); // 2° parry activo f14..18… el 1° cubre f2..6
        var evs = Run(s, 30);
        bool parried = Find(evs, EvKind.Parry) != null;
        Check(parried && s.Fighters[1].Guard >= 30f + SimConfig.ParryGuardRefund - 1f,
            "el parry recarga guardia", $"parry {parried}, guardia {s.Fighters[1].Guard}");
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

    static void ParryRechazaUnJab()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: false);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        s.SetQueue(1, new List<int> { MoveCatalog.Parry });
        var ev = Find(Run(s, 10), EvKind.Parry, 1);
        Check(ev.HasValue && s.Fighters[1].Hp == SimConfig.MaxHp && s.IsStunned(0) && s.Fighters[0].MoveIndex < 0,
            "parry f3-7 rechaza el jab e interrumpe al atacante");
    }

    static void AgarreLeGanaAlParry()
    {
        var s = NewSim(-0.45f, 0.45f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.Grab });
        s.SetQueue(1, new List<int> { MoveCatalog.Parry });
        var evs = Run(s, 12);
        Check(Find(evs, EvKind.Hit, 0).HasValue && Find(evs, EvKind.Parry) == null && s.Fighters[1].Hp < SimConfig.MaxHp,
            "agarre le gana al parry");
    }

    static void AtaqueDemoradoCastigaElParry()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: false);
        s.Fighters[0].Stun = StunKind.Hitstun;
        s.Fighters[0].StunEndTick = 6;
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        s.SetQueue(1, new List<int> { MoveCatalog.Parry });
        var evs = Run(s, 16);
        Check(Find(evs, EvKind.Hit, 0).HasValue && Find(evs, EvKind.Parry) == null,
            "un ataque demorado castiga el recovery del parry");
    }

    static void ParryDesactivaProyectilSinStunearAlZoner()
    {
        var s = NewSim(-2f, 0.5f, p1Blocks: false);
        s.Projectiles.Add(new Projectile { Owner = 0, X = -0.25f, Dir = 1, Alive = true });
        s.SetQueue(1, new List<int> { MoveCatalog.Parry });
        var evs = Run(s, 8);
        Check(Find(evs, EvKind.Parry, 1).HasValue && s.Fighters[1].Hp == SimConfig.MaxHp && !s.IsStunned(0) && s.Projectiles.Count == 0,
            "parry apaga el proyectil sin stunear al zoner lejano");
    }

    static void PerfilesDeIAMantienenPresupuesto()
    {
        var sim = new MatchSim();
        bool ok = true;
        foreach (AIProfile profile in Enum.GetValues(typeof(AIProfile)))
        foreach (AIDifficulty difficulty in Enum.GetValues(typeof(AIDifficulty)))
        {
            var ai = new SimpleAI(77, profile, difficulty);
            var plan = ai.Plan(sim, 1, SimConfig.TurnFrames);
            // regla fluida: cada move tiene que ARRANCAR dentro del turno
            // (el último puede cruzar el límite pidiendo AP prestados)
            int start = 0;
            foreach (int move in plan)
            {
                ok &= start < SimConfig.TurnFrames;
                start += MoveCatalog.All[move].PaddedTotal;
            }
            ok &= ai.ResolvedProfile != AIProfile.Random;
        }
        Check(ok, "todos los perfiles y dificultades respetan el presupuesto");
    }

    // Guardia = stamina: quieto o bloqueando no regenera; ejecutando sí.
    static void LaGuardiaRegeneraSoloJugando()
    {
        var s = NewSim(-3f, 3f, p1Blocks: false);
        s.Fighters[0].Guard = 40f;
        Run(s, 30); // neutral: nada
        Check(s.Fighters[0].Guard == 40f, "la guardia NO regenera quieto", $"guard {s.Fighters[0].Guard}");
        s.SetQueue(0, new List<int> { MoveCatalog.DashF, MoveCatalog.DashF });
        Run(s, 48); // dos dashes en slots de AP (24f c/u): 32f ejecutando + padding
        Check(s.Fighters[0].Guard > 40f, "la guardia regenera ejecutando moves", $"guard {s.Fighters[0].Guard}");
        float afterDash = s.Fighters[0].Guard;
        s.SetQueue(0, new List<int> { MoveCatalog.WalkB });
        Run(s, 20); // bloquear: tampoco regenera
        Check(s.Fighters[0].Guard == afterDash, "bloquear no regenera guardia", $"guard {s.Fighters[0].Guard}");
    }

    // Turno fluido (SimConfig.FluidTurn): hoy SOLO existe con el overflow de
    // AP habilitado (dormido, ApOverflowEnabled=false) o el toggle legacy de
    // carryover. Por defecto el turno es ESTRICTO: el move que cruza se corta.
    static void TurnoFluidoCruzaElLimite()
    {
        // overflow dormido: se prende acá para que el código no se pudra
        SimConfig.ApOverflowEnabled = true;
        try
        {
            var s = NewSim(-2.5f, 2.5f, p1Blocks: false);
            // dash = 1 AP (slot de 12f) + hadouken 60f: arranca en f12 y cruza
            s.SetQueue(0, new List<int> { MoveCatalog.DashF, MoveCatalog.Hadouken });
            Run(s, SimConfig.TurnFrames);
            int lost = s.OnTurnEnd(0);
            bool sigue = s.Fighters[0].MoveIndex == MoveCatalog.Hadouken;
            int resto = s.CommittedRemaining(0);
            Check(lost == 0 && sigue && resto == 12,
                "overflow (dormido): el hadouken cruza el límite comprometido (1 AP prestado)",
                $"lost {lost}, move {s.Fighters[0].MoveIndex}, resto {resto}");
            Check(s.Fighters[0].Super == resto, "los frames de overflow cargan la barra de super",
                $"super {s.Fighters[0].Super}, resto {resto}");
            Run(s, resto + 2);
            Check(s.Fighters[0].MoveIndex == -1, "overflow: el move comprometido termina en el turno siguiente",
                $"move {s.Fighters[0].MoveIndex}");
        }
        finally { SimConfig.ApOverflowEnabled = false; }

        // default actual: estricto — se corta, se pierde, y el slot no deja deuda
        var s2 = NewSim(-2.5f, 2.5f, p1Blocks: false);
        s2.SetQueue(0, new List<int> { MoveCatalog.DashF, MoveCatalog.Hadouken });
        Run(s2, SimConfig.TurnFrames);
        int lost2 = s2.OnTurnEnd(0);
        Check(lost2 == 1 && s2.Fighters[0].MoveIndex == -1 && s2.CommittedRemaining(0) == 0,
            "turno estricto (default): el mismo move se corta, se pierde y no deja deuda de slot", $"lost {lost2}");
    }

    // ---- Economía de AP (2026-07-20): stock, ingreso y bloqueo bancado ----

    static void LaEconomiaDeApRespira()
    {
        int apPerTurn = SimConfig.TurnFrames / SimConfig.FramesPerAp; // 5
        var eco = new ApEconomy();
        eco.ResetRound(apPerTurn);
        Check(eco.Stock[0] == 5, "arrancás el round con el stock lleno (5)", $"stock {eco.Stock[0]}");
        eco.EndTurn(0, apPerTurn, spentAp: 5, banked: false);
        Check(eco.Stock[0] == 4, "gastar todo te deja corto (ingreso +4)", $"stock {eco.Stock[0]}");
        eco.EndTurn(0, apPerTurn, spentAp: 0, banked: false);
        Check(eco.Stock[0] == 5, "no gastar GUARDA hasta la barra llena (4+4 → cap 5)", $"stock {eco.Stock[0]}");
        eco.EndTurn(0, apPerTurn, spentAp: 4, banked: true);
        Check(eco.Stock[0] == 5, "el bloqueo bancado suma +1 (5−4+4+1 → cap 5)", $"stock {eco.Stock[0]}");
        eco.EndTurn(0, apPerTurn, spentAp: 5, banked: false);
        eco.EndTurn(0, apPerTurn, spentAp: 4, banked: false);
        Check(eco.Stock[0] == 4, "gastar el ingreso te mantiene; pasarte te achica", $"stock {eco.Stock[0]}");
    }

    // La carta Bloquear que bloquea un golpe banca; el bloqueo automático
    // en neutral defiende igual pero NO banca.
    static void ElBloqueoBancadoEsSoloConLaCarta()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        s.SetQueue(1, new List<int> { MoveCatalog.WalkB });
        var evs = Run(s, 30);
        Check(Find(evs, EvKind.Blocked, 0) != null && s.Fighters[1].BankedBlock,
            "bloquear con la CARTA banca +1 AP", $"banked {s.Fighters[1].BankedBlock}");
        s.OnTurnEnd(1);
        Check(!s.Fighters[1].BankedBlock, "el bancado se limpia al cerrar el turno");

        var s2 = NewSim(-0.5f, 0.5f, p1Blocks: true);
        s2.SetQueue(0, new List<int> { MoveCatalog.AttackA }); // P1 en neutral: bloquea igual
        var evs2 = Run(s2, 30);
        Check(Find(evs2, EvKind.Blocked, 0) != null && !s2.Fighters[1].BankedBlock,
            "el bloqueo automático en neutral NO banca", $"banked {s2.Fighters[1].BankedBlock}");
    }

    // La IA respeta su stock de AP: pobre = turno corto.
    static void LaIaRespetaSuStock()
    {
        var sim = new MatchSim();
        var ai = new SimpleAI(123, AIProfile.Aggressive);
        var plan = ai.Plan(sim, 0, SimConfig.TurnFrames, apBudget: 2);
        int ap = 0;
        foreach (var mv in plan) ap += MoveCatalog.All[mv].ApCost;
        Check(ap <= 2, "con 2 AP de stock la IA planifica corto", $"gastó {ap} AP");
    }

    // ---- Reversal: la válvula anti-vortex ----

    static void ElReversalLevantaYSepara()
    {
        var s = NewSim(-0.4f, 0.4f, p1Blocks: false);
        s.Fighters[0].Stun = StunKind.Knockdown;
        s.Fighters[0].StunEndTick = s.Tick + 40;
        s.Reversal(0);
        Check(!s.IsStunned(0), "el reversal te levanta YA");
        float dist = Math.Abs(s.Fighters[1].X - s.Fighters[0].X);
        Check(dist >= SimConfig.ReversalGap - 0.01f, "y separa a la distancia de escape",
            $"dist {dist:0.00}");

        // sin knockdown no hace nada (un código remoto trucho no rompe)
        var s2 = NewSim(-0.4f, 0.4f, p1Blocks: false);
        float before = s2.Fighters[1].X;
        s2.Reversal(0);
        Check(s2.Fighters[1].X == before, "sin knockdown el reversal es un no-op");
    }

    // ---- Modo AP (2026-07-20): el turno clásico dividido en action points ----

    // Los costos que ven las cartas: ceil(frames/12), 5 AP por turno de 60f.
    static void LosCostosDeApSonLosEsperados()
    {
        Check(SimConfig.TurnFrames / SimConfig.FramesPerAp == 5, "turno de 60f = 5 AP");
        (int move, int ap)[] esperados =
        {
            // dash adelante 1 AP (el move barato) · dash atrás 2 (sobreprecio
            // anti-turtle) · agarre 2 y barrida 4 (rebalance ofensivo)
            (MoveCatalog.DashF, 1), (MoveCatalog.DashB, 2),
            (MoveCatalog.Parry, 1), (MoveCatalog.WalkB, 2), (MoveCatalog.AttackA, 2),
            (MoveCatalog.Grab, 2),
            (MoveCatalog.Shoryuken, 4), (MoveCatalog.JumpF, 4), (MoveCatalog.JumpN, 4),
            (MoveCatalog.JumpB, 4), (MoveCatalog.Tatsu, 4),
            (MoveCatalog.AttackB, 4), (MoveCatalog.Hadouken, 5), (MoveCatalog.Super, 5),
        };
        foreach (var (move, ap) in esperados)
        {
            var m = MoveCatalog.All[move];
            Check(m.ApCost == ap, $"{m.Name} cuesta {ap} AP", $"da {m.ApCost}");
        }
    }

    // El move ocupa su slot ENTERO: tras un Bloquear (20f, slots hasta 24f)
    // la próxima orden espera el fin del slot en neutral (bloqueando).
    static void ElSlotDeApEspaciaLaCola()
    {
        var s = NewSim(-3f, 3f, p1Blocks: false);
        s.SetQueue(0, new List<int> { MoveCatalog.WalkB, MoveCatalog.AttackA });
        Run(s, 22); // el bloqueo terminó en f20; su slot va hasta f24
        Check(s.Fighters[0].MoveIndex == -1, "en el resto del slot se espera en neutral",
            $"move {s.Fighters[0].MoveIndex}");
        Check(s.IsBlockingState(0), "y en neutral se bloquea (el padding no es un hueco indefenso)");
        Run(s, 5); // f27: el jab ya tuvo que arrancar en f24
        Check(s.Fighters[0].MoveIndex == MoveCatalog.AttackA, "la orden siguiente arranca al abrirse el slot",
            $"move {s.Fighters[0].MoveIndex}");
    }

    // Un golpe cancela el move Y devuelve el resto del slot: el stun lo
    // reemplaza (sin esto el turno siguiente cobraría stun + slot a la vez).
    static void ElGolpeDevuelveElRestoDelSlot()
    {
        var s = NewSim(-0.5f, 0.5f, p1Blocks: false);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        s.SetQueue(1, new List<int> { MoveCatalog.AttackB }); // barrida: startup 16 — el jab (6) la pesca antes
        Run(s, 10); // el jab conecta en f6: P1 en hitstun con la barrida cancelada
        Check(s.IsStunned(1) && s.Fighters[1].MoveIndex == -1, "el golpe cancela el move del rival");
        Check(s.CommittedRemaining(1) == 0, "y devuelve el resto del slot (solo queda el stun)",
            $"committed {s.CommittedRemaining(1)}");
    }

    // La super exige barra llena, se consume al arrancar, arrasa el hadouken
    // rival y pega 4 con hard knockdown.
    static void LaSuperArrasaYPegaCuatro()
    {
        var s = NewSim(-2.5f, 2.5f, p1Blocks: false);
        Check(!s.MoveAllowed(0, MoveCatalog.Super), "sin barra llena no hay super");
        s.Fighters[0].Super = SimConfig.SuperMax;
        Check(s.MoveAllowed(0, MoveCatalog.Super), "barra llena habilita la super");

        s.SetQueue(0, new List<int> { MoveCatalog.Super });
        s.SetQueue(1, new List<int> { MoveCatalog.Hadouken });
        var evs = Run(s, 90); // la super conecta ~f52: a los 90 el KD de 60f sigue vivo
        var hit = Find(evs, EvKind.Hit, 0);
        Check(hit.HasValue && hit.Value.Damage == SimConfig.SuperDamage && hit.Value.MoveIndex == MoveCatalog.Super,
            "la super arrasa el hadouken rival y pega 4",
            hit.HasValue ? $"dmg {hit.Value.Damage}, move {hit.Value.MoveIndex}" : "no conectó");
        Check(s.Fighters[1].Stun == StunKind.Knockdown, "la super derriba (hard KD)", $"stun {s.Fighters[1].Stun}");
        Check(s.Fighters[0].Super == 0, "la barra se consume al tirarla", $"super {s.Fighters[0].Super}");
    }

    // ---- Modo YOMI: el triángulo tiene que ser limpio en la sim ----

    // Golpe > Agarre: el agarre yomi (startup 9) SIEMPRE pierde con el jab
    // (startup 6) — counter hit, sin trade turbio en el mismo frame.
    static void YomiElJabLeGanaAlAgarre()
    {
        var s = NewSim(-0.45f, 0.45f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.AttackA });
        s.SetQueue(1, new List<int> { MoveCatalog.YomiGrab });
        var evs = Run(s, 20);
        var hit = Find(evs, EvKind.Hit, 0);
        Check(hit.HasValue && hit.Value.Counter && Find(evs, EvKind.Hit, 1) == null,
            "yomi: el jab countereá al agarre (golpe > agarre)",
            hit.HasValue ? $"counter {hit.Value.Counter}" : "el jab no conectó");
    }

    // Golpe fuerte > Salto: hitbox alta (hasta 2.4) alcanza la hurtbox aérea
    // y conecta ANTES de la patada de jump-in (activo 14 vs hit 20).
    static void YomiElGolpeFuerteEsAntiaereo()
    {
        var s = NewSim(-0.5f, 1.7f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.Strong });
        s.SetQueue(1, new List<int> { MoveCatalog.JumpF });
        var evs = Run(s, 40);
        var hit = Find(evs, EvKind.Hit, 0);
        Check(hit.HasValue && Find(evs, EvKind.Hit, 1) == null && s.Fighters[1].Stun == StunKind.Knockdown,
            "yomi: el golpe fuerte baja al salto y derriba (antiaéreo)",
            hit.HasValue ? $"stun {s.Fighters[1].Stun}" : "no conectó");
    }

    // ---- Modo YOMI v2: la matriz discreta (YomiSim), celda por celda ----

    static YomiSim YS(bool close, int ap0 = 3, int ap1 = 3)
    {
        var y = new YomiSim { Close = close };
        y.Ap[0] = ap0;
        y.Ap[1] = ap1;
        return y;
    }

    static void YomiMatrizDeCerca()
    {
        var r = YS(true).Resolve(YomiAction.Jab, YomiAction.Kick);
        Check(r.Dmg1 == 1 && r.Dmg0 == 0, "cerca: jab le gana al kick", $"d0 {r.Dmg0} d1 {r.Dmg1}");

        r = YS(true).Resolve(YomiAction.Jab, YomiAction.Grab);
        Check(r.Dmg1 == 1 && r.Dmg0 == 0, "cerca: jab le gana al agarre");

        var y = YS(true);
        r = y.Resolve(YomiAction.Parry, YomiAction.Jab);
        Check(r.Parry0 && r.Dmg1 == 1 && y.Ap[0] == 4 && y.Hp[1] == 5,
            "cerca: parry bloquea el jab, +1 AP y devuelve 1", $"ap {y.Ap[0]} hp1 {y.Hp[1]}");

        y = YS(true);
        r = y.Resolve(YomiAction.Grab, YomiAction.Parry);
        Check(r.Dmg1 == 2 && !y.Close, "cerca: agarre rompe el parry y tira a LEJOS", $"d1 {r.Dmg1} close {y.Close}");

        y = YS(true);
        r = y.Resolve(YomiAction.Kick, YomiAction.Dash);
        Check(r.Dmg1 == 2 && !y.Close, "cerca: kick caza al dash que se retira", $"d1 {r.Dmg1}");

        y = YS(true);
        r = y.Resolve(YomiAction.Jab, YomiAction.Jump);
        Check(r.Dmg1 == 1 && y.Close, "cerca: jab baja al salto en el despegue (se queda cerca)");

        y = YS(true);
        r = y.Resolve(YomiAction.Jab, YomiAction.Dash);
        Check(r.Dmg0 == 0 && r.Dmg1 == 0 && !y.Close, "cerca: dash esquiva el jab y se va a LEJOS");
    }

    static void YomiMatrizDeLejos()
    {
        Check(!YS(false).Legal(0, YomiAction.Jab) && !YS(false).Legal(0, YomiAction.Grab),
            "lejos: jab y agarre no llegan (ilegales)");

        var y = YS(false);
        var r = y.Resolve(YomiAction.Kick, YomiAction.Dash);
        Check(r.Dmg1 == 2 && !y.Close, "lejos: kick frena al dash que entra", $"d1 {r.Dmg1} close {y.Close}");

        y = YS(false);
        r = y.Resolve(YomiAction.Jump, YomiAction.Kick);
        Check(r.Dmg1 == 1 && y.Close, "lejos: el salto pasa por arriba del kick y entra pegando");

        y = YS(false);
        r = y.Resolve(YomiAction.Jump, YomiAction.Parry);
        Check(r.Parry1 && r.Dmg0 == 1 && y.Close,
            "lejos: parry bloquea la patada del salto (que llega igual)");

        y = YS(false);
        r = y.Resolve(YomiAction.Dash, YomiAction.Parry);
        Check(r.Dmg0 == 0 && y.Close, "lejos: dash entra gratis contra el parry");
    }

    static void YomiShoryuEsUnaApuesta()
    {
        // de cerca le gana a todo (acá: al jab) y derriba a LEJOS
        var y = YS(true);
        var r = y.Resolve(YomiAction.Shoryu, YomiAction.Jab);
        Check(r.Dmg1 == 3 && r.Dmg0 == 0 && !y.Close, "cerca: shoryu le gana al jab y manda a LEJOS");

        // el rival se fue: whiff → recovery (el turno siguiente es forzado)
        y = YS(true);
        r = y.Resolve(YomiAction.Shoryu, YomiAction.Dash);
        Check(r.Dmg0 == 0 && r.Dmg1 == 0 && r.Rec0Next && y.Recovery[0] && !y.Close,
            "cerca: shoryu whiffea al dash y queda en recovery");
        Check(y.Legal(0, YomiAction.Recovery) && !y.Legal(0, YomiAction.Jab),
            "en recovery la única acción legal es Recovery");

        // de lejos es SOLO lectura antiaérea
        y = YS(false);
        r = y.Resolve(YomiAction.Shoryu, YomiAction.Jump);
        Check(r.Dmg1 == 3 && !y.Close, "lejos: shoryu baja al salto entrante (la lectura)");
        y = YS(false);
        r = y.Resolve(YomiAction.Shoryu, YomiAction.Charge);
        Check(r.Rec0Next && r.Charged1, "lejos: shoryu sin salto que bajar = whiff y recovery");
        y = YS(false);
        r = y.Resolve(YomiAction.Shoryu, YomiAction.Kick);
        Check(r.Dmg0 == 2 && !r.Rec0Next, "lejos: el kick castiga el shoryu whiffeado");
    }

    static void YomiEconomiaDiscreta()
    {
        // costo + ingreso automático: jab 3−1+1 = 3
        var y = YS(true);
        y.Resolve(YomiAction.Jab, YomiAction.Kick);
        Check(y.Ap[0] == 3, "economía: jab cuesta 1, ingreso +1 por turno", $"ap {y.Ap[0]}");

        // cargar sin que te peguen: +2 (y el tope es 6)
        y = YS(true, 3, 3);
        var r = y.Resolve(YomiAction.Parry, YomiAction.Charge);
        Check(r.Charged1 && y.Ap[1] == 6, "cargar limpio: +2 y el ingreso", $"ap {y.Ap[1]}");
        y = YS(true, 6, 6);
        y.Resolve(YomiAction.Charge, YomiAction.Charge);
        Check(y.Ap[0] == 6 && y.Ap[1] == 6, "el tope de AP es 6");

        // cargar y comerse un golpe: counter (+1) y NO cargás
        y = YS(true);
        r = y.Resolve(YomiAction.Jab, YomiAction.Charge);
        Check(!r.Charged1 && r.Dmg1 == 2 && r.Counter1 && y.Ap[1] == 4,
            "cargar interrumpido: counter de 2 y sin +2", $"d1 {r.Dmg1} ap {y.Ap[1]}");
    }

    static void YomiRecoveryYCounters()
    {
        // whiff de shoryu → el turno siguiente estás vendido: golpe counter
        var y = YS(true, 6, 6);
        y.Resolve(YomiAction.Shoryu, YomiAction.Dash);       // whiff → recovery, quedan LEJOS
        var r = y.Resolve(YomiAction.Recovery, YomiAction.Kick);
        Check(r.Dmg0 == 3 && r.Counter0 && !y.Recovery[0],
            "recovery: el kick pega counter (2+1) y el flag se limpia", $"d0 {r.Dmg0}");
    }

    static void YomiKoYTech()
    {
        var y = YS(true);
        y.Hp[1] = 1;
        y.Resolve(YomiAction.Jab, YomiAction.Kick);
        Check(y.Over && y.Winner == 0, "HP 0 = KO y hay ganador", $"over {y.Over} winner {y.Winner}");

        y = YS(true);
        var r = y.Resolve(YomiAction.Grab, YomiAction.Grab);
        Check(r.Tech && r.Dmg0 == 0 && r.Dmg1 == 0, "agarre vs agarre sigue siendo TECH");
    }

    // Toda celda legal de ambas matrices resuelve sin romper invariantes.
    static void YomiMatrizCompletaNoExplota()
    {
        bool ok = true;
        foreach (bool close in new[] { true, false })
            for (int a = 0; a <= (int)YomiAction.Charge; a++)
                for (int b = 0; b <= (int)YomiAction.Charge; b++)
                {
                    var y = YS(close, 6, 6);
                    if (!y.Legal(0, (YomiAction)a) || !y.Legal(1, (YomiAction)b)) continue;
                    var r = y.Resolve((YomiAction)a, (YomiAction)b);
                    ok &= y.Hp[0] >= 0 && y.Hp[1] >= 0 && y.Ap[0] >= 0 && y.Ap[0] <= 6 && y.Ap[1] >= 0 && y.Ap[1] <= 6;
                    ok &= r.Dmg0 >= 0 && r.Dmg0 <= 4 && r.Dmg1 >= 0 && r.Dmg1 <= 4;
                }
        Check(ok, "todas las celdas legales resuelven con invariantes sanos");
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
    // romper nada: la orden se consume y queda en neutral.
    static void ElAgachadoDesactivadoQuedaNeutral()
    {
        var s = NewSim(-2f, 2f, p1Blocks: true);
        s.SetQueue(0, new List<int> { MoveCatalog.Crouch, MoveCatalog.LowKick });
        s.SetQueue(1, new List<int>());
        s.Step();
        Check(s.CurrentMove(0) == null && !s.IsCrouching(0) && s.IsBlockingState(0),
            "agachado off: Crouch/LowKick se consumen en neutral");
    }

    // Online asincrónico: el código de turno serializa y deserializa exacto,
    // y rechaza basura.
    static void CodigoDeTurnoIdaYVuelta()
    {
        var plan = new List<int> { MoveCatalog.WalkF, MoveCatalog.AttackA, MoveCatalog.Shoryuken };
        string code = TurnCode.Encode(1, 7, TurnCode.WakeStay, plan);
        bool ok = TurnCode.TryDecode(code, out int side, out int turn, out int wake, out var moves);
        bool roundtrip = ok && side == 1 && turn == 7 && wake == TurnCode.WakeStay &&
                         moves.Count == 3 && moves[0] == MoveCatalog.WalkF &&
                         moves[1] == MoveCatalog.AttackA && moves[2] == MoveCatalog.Shoryuken;
        // v2: el wake es un trit — el REVERSAL viaja en el protocolo
        bool rev = TurnCode.TryDecode(TurnCode.Encode(0, 3, TurnCode.WakeReversal, plan),
                       out _, out _, out int wake2, out _) && wake2 == TurnCode.WakeReversal;
        bool rejects = !TurnCode.TryDecode("hola", out _, out _, out _, out _) &&
                       !TurnCode.TryDecode("LF!!!!", out _, out _, out _, out _) &&
                       !TurnCode.TryDecode("", out _, out _, out _, out _);
        Check(roundtrip && rev && rejects, "código de turno v2: ida y vuelta exacta (con reversal), rechaza basura", code);
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

    // ---- MODO CARTAS v2 (copia completa de Yomi 2): cada regla es un test ----

    // Sim con las manos armadas a mano: el combate es lo único bajo prueba.
    // Por defecto Grave vs Grave; chars distintos donde el test lo pide.
    static CardSim NewCards(int active, int card0, int card1,
        int char0 = CardCatalog.GraveIdx, int char1 = CardCatalog.GraveIdx)
    {
        var s = new CardSim(seed: 1234, firstPlayer: active, char0, char1);
        s.Hand[0].Clear(); s.Hand[0].Add(card0);
        s.Hand[1].Clear(); s.Hand[1].Add(card1);
        return s;
    }

    static void CartasAtaqueLeGanaAlAgarre()
    {
        // E es el ataque MÁS LENTO (speed 4) y aun así le gana al throw.
        var s = NewCards(0, CardCatalog.AttackE, CardCatalog.Throw);
        var r = s.Resolve(0, 0);
        Check(r.Dmg1 == 7 && r.Dmg0 == 0 && !r.KdNext1 && !s.AwaitingFollowup,
            "cartas: ataque > throw sin importar speed", $"dmg1 {r.Dmg1}");
    }

    static void CartasSpeedYEmpateAlActivo()
    {
        var s = NewCards(1, CardCatalog.AttackA, CardCatalog.AttackB);
        var r = s.Resolve(0, 0);
        bool rapido = r.Dmg1 == 3 && r.Dmg0 == 0;
        s = NewCards(1, CardCatalog.AttackA, CardCatalog.AttackA);
        r = s.Resolve(0, 0);
        bool empate = r.Dmg0 == 3 && r.Dmg1 == 0;
        Check(rapido && empate, "cartas: speed decide y el empate es del activo",
            $"rapido {rapido} empate {empate}");
    }

    static void CartasAlturasDelBloqueo()
    {
        var s = NewCards(0, CardCatalog.AttackA, CardCatalog.LowBlock);
        int deckBefore = s.Deck[1].Count;
        var r = s.Resolve(0, 0);
        bool bien = r.Blocked1 && r.Dmg1 == 0 && r.Drew1 == 1 && s.Deck[1].Count == deckBefore - 1
            && r.Returned1 && s.Hand[1].Contains(CardCatalog.LowBlock);
        s = NewCards(0, CardCatalog.AttackA, CardCatalog.HighBlock);
        r = s.Resolve(0, 0);
        bool mal = r.WrongBlock1 && r.Dmg1 == 3 && !s.Hand[1].Contains(CardCatalog.HighBlock);
        s = NewCards(0, CardCatalog.AttackD, CardCatalog.HighBlock);
        bool altoBien = s.Resolve(0, 0).Dmg1 == 0;
        s = NewCards(0, CardCatalog.AttackD, CardCatalog.LowBlock);
        bool altoMal = s.Resolve(0, 0).Dmg1 == 6;
        s = NewCards(0, CardCatalog.AttackC, CardCatalog.LowBlock);
        bool midBajo = s.Resolve(0, 0).Dmg1 == 0;
        s = NewCards(0, CardCatalog.AttackC, CardCatalog.HighBlock);
        bool midAlto = s.Resolve(0, 0).Dmg1 == 0;
        Check(bien && mal && altoBien && altoMal && midBajo && midAlto,
            "cartas: alturas del bloqueo (low/high/mid)",
            $"bien {bien} mal {mal} altoBien {altoBien} altoMal {altoMal} mid {midBajo}/{midAlto}");
    }

    static void CartasChipLockdownYRecurring()
    {
        var s = NewCards(0, CardCatalog.SpecialX, CardCatalog.LowBlock);
        int deckBefore = s.Deck[1].Count;
        var r = s.Resolve(0, 0);
        Check(r.Blocked1 && r.Chip1 == 4 && r.Dmg1 == 4 && r.Drew1 == 0
            && s.Deck[1].Count == deckBefore
            && r.Returned0 && s.Hand[0].Contains(CardCatalog.SpecialX)
            && r.Returned1 && s.Hand[1].Contains(CardCatalog.LowBlock),
            "cartas: chip + lockdown + recurring de X",
            $"chip {r.Chip1} drew {r.Drew1} retX {r.Returned0} retBlk {r.Returned1}");
    }

    static void CartasProyectilVsProyectil()
    {
        var s = NewCards(0, CardCatalog.SpecialX, CardCatalog.SpecialX);
        var r = s.Resolve(0, 0);
        Check(r.ProjCancel && r.Dmg0 == 0 && r.Dmg1 == 0,
            "cartas: proyectil vs proyectil del mismo nivel se anulan", $"cancel {r.ProjCancel}");
    }

    static void CartasUnsafeSeCastiga()
    {
        var s = NewCards(0, CardCatalog.SpecialY, CardCatalog.HighBlock);
        s.Hand[1].Add(CardCatalog.Throw);
        var r = s.Resolve(0, 0);
        bool pendiente = s.AwaitingFollowup && s.FollowIsHitBack && s.FollowSide == 1
            && r.Chip1 == 2 && r.Drew1 == 1;
        s.HitBack(s.Hand[1].IndexOf(CardCatalog.Throw));
        r = s.LastResult;
        Check(pendiente && r.HitBackSide == 1 && r.HitBackCard == CardCatalog.Throw
            && r.Dmg0 == 7 && s.KnockedDown[0],
            "cartas: unsafe on block se castiga (y el throw del castigo derriba)",
            $"pend {pendiente} dmg0 {r.Dmg0} kd {s.KnockedDown[0]}");
    }

    static void CartasDodgeCastigaStrikesNoProyectiles()
    {
        var s = NewCards(0, CardCatalog.AttackE, CardCatalog.Dodge);
        s.Hand[1].Add(CardCatalog.AttackE);
        var r = s.Resolve(0, 0);
        bool strike = s.AwaitingFollowup && s.FollowIsHitBack && s.FollowSide == 1;
        s.HitBack(s.Hand[1].IndexOf(CardCatalog.AttackE));
        r = s.LastResult;
        strike &= r.Dmg0 == 7 && r.Dodged1;
        s = NewCards(0, CardCatalog.SpecialX, CardCatalog.Dodge);
        r = s.Resolve(0, 0);
        bool proyectil = !s.AwaitingFollowup && r.Dodged1 && r.Dmg0 == 0 && r.Dmg1 == 0;
        Check(strike && proyectil, "cartas: dodge castiga strikes pero no proyectiles",
            $"strike {strike} proyectil {proyectil}");
    }

    static void CartasThrowDerribaYElKnockdownApura()
    {
        var s = NewCards(0, CardCatalog.Throw, CardCatalog.LowBlock);
        var r = s.Resolve(0, 0);
        bool agarro = r.Thrown1 && r.Dmg1 == 7 && r.KdNext1 && s.KnockedDown[1];
        s.Hand[0].Add(CardCatalog.AttackD);   // s5 → eff 10 contra derribado
        s.Hand[1].Add(CardCatalog.AttackA);   // s8 del caído
        r = s.Resolve(0, 0);
        bool apurado = r.Dmg1 == 6 && r.Dmg0 == 0;
        bool limpio = !s.KnockedDown[1];
        Check(agarro && apurado && limpio,
            "cartas: throw derriba y el knockdown apura los speeds a 10",
            $"agarro {agarro} apurado {apurado} limpio {limpio}");
    }

    static void CartasThrowVsThrowEmpateAlActivo()
    {
        var s = NewCards(1, CardCatalog.Throw, CardCatalog.Throw);
        var r = s.Resolve(0, 0);
        Check(r.Dmg0 == 7 && r.Dmg1 == 0 && s.KnockedDown[0],
            "cartas: throw vs throw, el empate es del activo y derriba", $"dmg0 {r.Dmg0}");
    }

    static void CartasWildSwingDerribado()
    {
        var s = NewCards(0, CardCatalog.Throw, CardCatalog.LowBlock);
        s.Resolve(0, 0); // deja al 1 derribado
        s.Hand[0].Add(CardCatalog.LowBlock);
        s.Hand[1].Add(CardCatalog.Dodge);
        s.Deck[1].Add(CardCatalog.AttackA);
        var r = s.Resolve(0, 0);
        Check(r.Wild1 == 1 && r.Card1 == CardCatalog.AttackA
            && s.Discard[1].Contains(CardCatalog.Dodge),
            "cartas: wild swing al abrir con dodge derribado",
            $"wild {r.Wild1} carta {r.Card1}");
    }

    static void CartasRemezclaUnaVezYTimeOver()
    {
        var s = new CardSim(seed: 5, firstPlayer: 0);
        s.Deck[0].Clear();
        s.Discard[0].Clear();
        s.Discard[0].AddRange(new[] { CardCatalog.LowBlock, CardCatalog.HighBlock,
            CardCatalog.Super1, CardCatalog.AttackC, CardCatalog.AttackD });
        int handBefore = s.Hand[0].Count;
        s.StartTurn(); // primer turno: roba 1 → dispara la remezcla
        bool remezclo = s.DeckOuts[0] == 1 && !s.Over
            && s.Discard[0].Count == 3 // blocks + la copia de super se quedan
            && s.Discard[0].Contains(CardCatalog.LowBlock)
            && s.Discard[0].Contains(CardCatalog.HighBlock)
            && s.Discard[0].Contains(CardCatalog.Super1)
            && s.Hand[0].Count == handBefore + 1
            && s.Deck[0].Count == 1;
        s.Deck[0].Clear();
        s.Discard[0].Clear();
        s.Discard[0].Add(CardCatalog.LowBlock);
        s.Hp[1] = 10;
        s.StartTurn();
        bool timeOver = s.Over && s.Winner == 0;
        Check(remezclo && timeOver && s.DeckOuts[0] >= 2,
            "cartas: remezcla única (blocks y supers afuera) y luego TIME OVER",
            $"remezclo {remezclo} over {s.Over} winner {s.Winner}");
    }

    static void CartasLimiteDeMano()
    {
        var s = new CardSim(seed: 6, firstPlayer: 0);
        s.Hand[0].Clear();
        for (int i = 0; i < CardConfig.HandLimit; i++) s.Hand[0].Add(CardCatalog.AttackC);
        s.Deck[0].Clear();
        s.Deck[0].Add(CardCatalog.SpecialY);
        s.Deck[0].Add(CardCatalog.SpecialY);
        s.Discard[0].Clear();
        s.Discard[0].Add(CardCatalog.AttackA);
        s.StartTurn();
        Check(s.Hand[0].Count == CardConfig.HandLimit && s.Discard[0].Contains(CardCatalog.SpecialY),
            "cartas: mano máxima 12, el exceso se descarta",
            $"mano {s.Hand[0].Count} desc {s.Discard[0].Count}");
    }

    static void CartasExchangeSoloNormalesYDosVeces()
    {
        var s = new CardSim(seed: 7, firstPlayer: 0);
        s.StartTurn();
        s.Hand[0].Clear();
        s.Hand[0].AddRange(new[] { CardCatalog.AttackA, CardCatalog.AttackB, CardCatalog.SpecialX });
        s.Discard[0].Clear();
        s.Discard[0].AddRange(new[] { CardCatalog.Throw, CardCatalog.Dodge, CardCatalog.SpecialY });
        bool especialNo = !s.CanExchange(2, 0) && !s.CanExchange(0, 2);
        bool uno = s.Exchange(0, 0);
        bool dos = s.Exchange(0, 0);
        bool tresNo = !s.Exchange(0, 0);
        Check(especialNo && uno && dos && tresNo && s.ExchangesLeft == 0,
            "cartas: exchange solo normales y máximo dos (innate de Grave)",
            $"esp {especialNo} 1:{uno} 2:{dos} 3:{!tresNo}");
    }

    static void CartasManoInicialGarantizada()
    {
        var s = new CardSim(seed: 42, firstPlayer: 0, CardCatalog.GraveIdx, CardCatalog.JainaIdx);
        bool ok = true;
        for (int side = 0; side < 2; side++)
        {
            ok &= s.Hand[side].Count == 7; // blocks + agarre + 4 (sin Burst: no hay gems)
            ok &= s.Hand[side].Contains(CardCatalog.LowBlock);
            ok &= s.Hand[side].Contains(CardCatalog.HighBlock);
            ok &= s.Hand[side].Contains(CardCatalog.Throw);
            ok &= s.Deck[side].Count == 21; // 30 - 2 supers al descarte - 7
            ok &= s.Discard[side].Contains(CardCatalog.Super1);
            ok &= s.Discard[side].Contains(CardCatalog.Super2);
        }
        ok &= s.Hp[0] == 90 && s.Hp[1] == 85; // Grave / Jaina
        Check(ok, "cartas: setup real (mano 7, supers al descarte, HP 90/85)", "");
    }

    static void CartasComboEncadenaYDaMeter()
    {
        // A > B > C: cada paso de letra da +1 meter — el corazón del combo.
        var s = NewCards(0, CardCatalog.AttackA, CardCatalog.Throw);
        s.Hand[0].AddRange(new[] { CardCatalog.AttackB, CardCatalog.AttackC });
        var r = s.Resolve(0, 0);
        bool combeando = s.AwaitingFollowup && !s.FollowIsHitBack && s.FollowSide == 0;
        bool b = s.ComboAdd(s.Hand[0].IndexOf(CardCatalog.AttackB));
        bool c = s.ComboAdd(s.Hand[0].IndexOf(CardCatalog.AttackC));
        r = s.LastResult;
        Check(combeando && b && c && r.Dmg1 == 12 && s.Meter[0] == 2 && r.Meter0 == 2
            && r.Combo0.Count == 2 && !s.AwaitingFollowup,
            "cartas: combo A>B>C pega 12 y da +2 meter",
            $"dmg {r.Dmg1} meter {s.Meter[0]} pend {s.AwaitingFollowup}");
    }

    static void CartasComboPointsLimitan()
    {
        // El combo del rulebook: Throw > D > E = 20 dmg + 1 meter, y los 4
        // combo points de Grave quedan EXACTOS (2+1+1): X ya no entra.
        var s = NewCards(0, CardCatalog.Throw, CardCatalog.LowBlock);
        s.Hand[0].AddRange(new[] { CardCatalog.AttackD, CardCatalog.AttackE, CardCatalog.SpecialX });
        var r = s.Resolve(0, 0);
        bool d = s.ComboAdd(s.Hand[0].IndexOf(CardCatalog.AttackD));
        bool e = s.ComboAdd(s.Hand[0].IndexOf(CardCatalog.AttackE));
        r = s.LastResult;
        // tras E los CP están en 0: el combo se cerró solo y X sigue en mano
        Check(d && e && r.Dmg1 == 20 && r.Meter0 == 1 && !s.AwaitingFollowup
            && s.Hand[0].Contains(CardCatalog.SpecialX) && !r.KdNext1,
            "cartas: Throw>D>E = 20 dmg +1 meter y los combo points cortan (sin KD: hubo combo)",
            $"dmg {r.Dmg1} meter {r.Meter0} pend {s.AwaitingFollowup} kd {r.KdNext1}");
    }

    static void CartasKnockdownSoloSinCombo()
    {
        // El agarre derriba SOLO si no seguís de combo (elegís parar).
        var s = NewCards(0, CardCatalog.Throw, CardCatalog.LowBlock);
        s.Hand[0].Add(CardCatalog.AttackD); // hay combo disponible…
        var r = s.Resolve(0, 0);
        bool pendiente = s.AwaitingFollowup;
        s.FollowupEnd(); // …pero paro: el knockdown se conserva
        r = s.LastResult;
        Check(pendiente && r.Dmg1 == 7 && r.KdNext1 && s.KnockedDown[1]
            && s.Hand[0].Contains(CardCatalog.AttackD),
            "cartas: parar el combo conserva el knockdown del agarre",
            $"kd {r.KdNext1} dmg {r.Dmg1}");
    }

    static void CartasPowerUpPagaSupers()
    {
        // Par al descarte → +2 meter → la S1 de Grave (cuesta 2) se puede abrir
        // y pega 20 al agarre.
        var s = new CardSim(seed: 9, firstPlayer: 0);
        s.StartTurn();
        s.Hand[0].Clear();
        s.Hand[0].AddRange(new[] { CardCatalog.AttackC, CardCatalog.AttackC, CardCatalog.Super1 });
        s.Hand[1].Clear(); s.Hand[1].Add(CardCatalog.Throw);
        bool antes = !s.LegalOpener(0, 2); // sin meter la super es inválida
        bool pu = s.PowerUp(0, 1, fetchSuper: false);
        bool despues = s.Meter[0] == 2 && s.LegalOpener(0, s.Hand[0].IndexOf(CardCatalog.Super1));
        var r = s.Resolve(s.Hand[0].IndexOf(CardCatalog.Super1), 0);
        Check(antes && pu && despues && r.Dmg1 == 20 && s.Meter[0] == 0,
            "cartas: power up +2 meter paga la super (y la super se cobra)",
            $"antes {antes} pu {pu} desp {despues} dmg {r.Dmg1} meter {s.Meter[0]}");

        // la otra rama: fetch de super del descarte (+1 meter)
        var s2 = new CardSim(seed: 10, firstPlayer: 0);
        s2.StartTurn();
        s2.Hand[0].Clear();
        s2.Hand[0].AddRange(new[] { CardCatalog.AttackD, CardCatalog.AttackD });
        bool fetch = s2.PowerUp(0, 1, fetchSuper: true, CardCatalog.Super1);
        Check(fetch && s2.Meter[0] == 1 && s2.Hand[0].Contains(CardCatalog.Super1)
            && !s2.Discard[0].Contains(CardCatalog.Super1),
            "cartas: power up con fetch recupera la super del descarte (+1 meter)",
            $"fetch {fetch} meter {s2.Meter[0]}");
    }

    static void CartasWindSummon()
    {
        // Invocar Viento: X sube a Nv.2, le gana a esquives y pega +4/+2 chip;
        // la S1 baja a 2 combo points (Throw > S1 entra en los 4 de Grave).
        var s = new CardSim(seed: 11, firstPlayer: 0);
        s.StartTurn();
        s.Hand[0].Clear();
        s.Hand[0].Add(CardCatalog.Ability);
        bool ab = s.PlayAbility(0);
        s.Hand[0].Add(CardCatalog.SpecialX);
        s.Hand[1].Clear(); s.Hand[1].Add(CardCatalog.Dodge);
        var r = s.Resolve(0, 0);
        bool venceDodge = r.Dmg1 == 12 && !r.Dodged1; // 8+4, el esquive no alcanza
        // segundo combate del viento: X (Nv.2) le gana al X rival (Nv.1)
        s.Hand[0].Clear(); s.Hand[0].Add(CardCatalog.SpecialX);
        s.Hand[1].Clear(); s.Hand[1].Add(CardCatalog.SpecialX);
        r = s.Resolve(0, 0);
        bool venceProj = r.Dmg1 == 12 && !r.ProjCancel;
        bool apagado = s.Ongoing[0] == 0; // duró exactamente 2 combates
        // S1 a 2 CP: Throw (2) + S1 (2) = 4 → el combo del viento
        var s3 = NewCards(0, CardCatalog.Throw, CardCatalog.LowBlock);
        s3.Meter[0] = 2;
        s3.Ongoing[0] = 1; // viento activo
        s3.Hand[0].Add(CardCatalog.Super1);
        s3.Resolve(0, 0);
        bool s1EnCombo = s3.AwaitingFollowup && s3.ComboOptions(0).Count == 1
            && s3.ComboAdd(s3.Hand[0].IndexOf(CardCatalog.Super1));
        var r3 = s3.LastResult;
        Check(ab && venceDodge && venceProj && apagado && s1EnCombo && r3.Dmg1 == 27,
            "cartas: Invocar Viento (Nv.2, gana a esquives, +4, S1 a 2 CP)",
            $"dodge {venceDodge} proj {venceProj} off {apagado} s1 {s1EnCombo} dmg {r3.Dmg1}");
    }

    static void CartasArcShot()
    {
        // Tiro en Arco de Jaina: el rival que abre con ATAQUE come 7 y no
        // combea; el que abre con BLOQUEO come 5 de chip.
        var s = new CardSim(seed: 12, firstPlayer: 1, CardCatalog.GraveIdx, CardCatalog.JainaIdx);
        s.StartTurn(); // turno de Jaina (1)
        s.Hand[1].Clear(); s.Hand[1].Add(CardCatalog.Ability);
        bool ab = s.PlayAbility(0);
        s.Hand[0].Clear();
        s.Hand[0].AddRange(new[] { CardCatalog.AttackA, CardCatalog.AttackB });
        s.Hand[1].Add(CardCatalog.Throw);
        var r = s.Resolve(0, 0); // A vs Throw: A gana… pero comió el arco
        bool arco = r.Arc0 && r.Dmg0 == 7 && r.Dmg1 == 3 && !s.AwaitingFollowup
            && s.Hand[0].Contains(CardCatalog.AttackB); // no pudo combear B
        // segundo combate del arco: bloquear también duele (5 chip)
        s.Hand[0].Clear(); s.Hand[0].Add(CardCatalog.LowBlock);
        s.Hand[1].Clear(); s.Hand[1].Add(CardCatalog.Throw);
        r = s.Resolve(0, 0);
        bool chip = r.Arc0 && r.Chip0 == 5;
        Check(ab && arco && chip, "cartas: Tiro en Arco castiga ataques (7, sin combo) y bloqueos (5 chip)",
            $"arco {arco} chip {chip}");
    }

    static void CartasJainaSelfDamageYSegura()
    {
        // La Y de Jaina se cobra 5 de vida al jugarla (salvo HP <= 35), y con
        // el arco activo es SEGURA aunque la bloqueen.
        var s = new CardSim(seed: 13, firstPlayer: 0, CardCatalog.JainaIdx, CardCatalog.GraveIdx);
        s.StartTurn();
        s.Hand[0].Clear(); s.Hand[0].Add(CardCatalog.SpecialY);
        s.Hand[1].Clear(); s.Hand[1].Add(CardCatalog.HighBlock);
        s.Hand[1].Add(CardCatalog.AttackE); // castigo disponible
        var r = s.Resolve(0, 0);
        bool unsafeSi = s.AwaitingFollowup && s.FollowIsHitBack; // sin arco: castigo
        s.HitBack(-1); // el HP se cobra al CERRAR el combate
        bool self = r.Self0 == 5 && s.Hp[0] == 85 - 5;
        // ahora con el arco activo: la misma Y bloqueada NO se castiga
        var s2 = new CardSim(seed: 14, firstPlayer: 0, CardCatalog.JainaIdx, CardCatalog.GraveIdx);
        s2.StartTurn();
        s2.Ongoing[0] = 1;
        s2.Hand[0].Clear(); s2.Hand[0].Add(CardCatalog.SpecialY);
        s2.Hand[1].Clear(); s2.Hand[1].Add(CardCatalog.HighBlock);
        s2.Hand[1].Add(CardCatalog.AttackE);
        s2.Resolve(0, 0);
        bool segura = !s2.AwaitingFollowup;
        // con 35 o menos de vida, la Y es gratis
        var s3 = new CardSim(seed: 15, firstPlayer: 0, CardCatalog.JainaIdx, CardCatalog.GraveIdx);
        s3.StartTurn();
        s3.Hp[0] = 30;
        s3.Hand[0].Clear(); s3.Hand[0].Add(CardCatalog.SpecialY);
        s3.Hand[1].Clear(); s3.Hand[1].Add(CardCatalog.Throw);
        var r3 = s3.Resolve(0, 0);
        Check(self && unsafeSi && segura && r3.Self0 == 0 && s3.Hp[0] == 30,
            "cartas: la Y de Jaina cuesta vida, es segura con arco y gratis con poca vida",
            $"self {self} unsafe {unsafeSi} segura {segura} low {r3.Self0}");
    }

    static void CartasRecklessness()
    {
        // Imprudencia: cerrar la main phase con ambos bloqueos en el descarte
        // = 2 de daño y una carta.
        var s = new CardSim(seed: 16, firstPlayer: 0, CardCatalog.JainaIdx, CardCatalog.GraveIdx);
        s.StartTurn();
        s.Hand[0].Remove(CardCatalog.LowBlock);
        s.Hand[0].Remove(CardCatalog.HighBlock);
        s.Discard[0].Add(CardCatalog.LowBlock);
        s.Discard[0].Add(CardCatalog.HighBlock);
        s.Hand[0].Clear(); s.Hand[0].Add(CardCatalog.AttackA);
        s.Hand[1].Clear(); s.Hand[1].Add(CardCatalog.Throw);
        var r = s.Resolve(0, 0);
        if (s.AwaitingFollowup) s.FollowupEnd(); // la carta robada pudo habilitar combo
        Check(r.Reckless && r.Self0 == 2 && s.Hp[0] == 83 && r.Dmg1 >= 3,
            "cartas: Imprudencia de Jaina (2 de vida por una carta)",
            $"reck {r.Reckless} self {r.Self0} hp {s.Hp[0]}");
    }

    static void CartasPumpDeZ()
    {
        // El Torbellino pumpeado: descartás el segundo Z y pega 7+8.
        var s = NewCards(0, CardCatalog.SpecialZ, CardCatalog.Throw);
        s.Hand[0].Add(CardCatalog.SpecialZ);
        var r = s.Resolve(0, 0);
        bool pend = s.AwaitingFollowup && s.CanPumpLast();
        bool pump = s.PumpLast(1);
        r = s.LastResult;
        Check(pend && pump && r.Dmg1 == 15 && r.PumpExtra0 == 8
            && !s.Hand[0].Contains(CardCatalog.SpecialZ),
            "cartas: pump del Z (+8 descartando el otro Z)",
            $"pend {pend} dmg {r.Dmg1}");
    }

    static void CartasSuperDodgeContragolpea()
    {
        // Poder de las Tormentas (Grave S2): esquiva el strike y devuelve 40.
        var s = NewCards(0, CardCatalog.Super2, CardCatalog.AttackE);
        s.Meter[0] = 3;
        var r = s.Resolve(0, 0);
        bool contra = r.SuperCounter == 0 && r.Dmg1 == 40 && s.Meter[0] == 0;
        // vs agarre: la super dodge PIERDE (throw > dodge)
        s = NewCards(0, CardCatalog.Super2, CardCatalog.Throw);
        s.Meter[0] = 3;
        r = s.Resolve(0, 0);
        bool pierde = r.Thrown0 && r.Dmg0 == 7 && s.Meter[0] == 0; // el meter se fue igual
        Check(contra && pierde, "cartas: la super dodge devuelve 40 a strikes y pierde con agarre",
            $"contra {contra} pierde {pierde}");
    }

    static void CartasNivelesDeProyectil()
    {
        // Aliento de Dragón (Jaina S2, Nv.3) arrasa el proyectil Nv.1.
        var s = new CardSim(seed: 17, firstPlayer: 0, CardCatalog.JainaIdx, CardCatalog.GraveIdx);
        s.StartTurn();
        s.Meter[0] = 2;
        s.Hand[0].Clear(); s.Hand[0].Add(CardCatalog.Super2);
        s.Hand[1].Clear(); s.Hand[1].Add(CardCatalog.SpecialX);
        var r = s.Resolve(0, 0);
        Check(r.Dmg1 == 18 && r.Dmg0 == 0 && !r.ProjCancel,
            "cartas: proyectil Nv.3 le gana al Nv.1 (ignora speeds)",
            $"dmg1 {r.Dmg1}");
    }

    static void CartasMismaSeedMismaPartida()
    {
        string a = PartidaDeCartas(2026), b = PartidaDeCartas(2026);
        Check(a == b, "cartas: misma seed, misma partida", $"{a} vs {b}");
    }

    static string PartidaDeCartas(int seed)
    {
        var s = new CardSim(seed, firstPlayer: 0, CardCatalog.GraveIdx, CardCatalog.JainaIdx);
        for (int turn = 0; turn < 80 && !s.Over; turn++)
        {
            s.StartTurn();
            if (s.Over) break;
            int h0 = PrimerLegal(s, 0), h1 = PrimerLegal(s, 1);
            s.Resolve(h0, h1);
            int guard = 0;
            while (s.AwaitingFollowup && guard++ < 20)
            {
                if (s.FollowIsHitBack && !s.HitBackPlayed) s.HitBack(PrimerGolpe(s, s.FollowSide));
                else if (!s.FollowIsHitBack && s.ComboOptions(s.FollowSide).Count > 0)
                    s.ComboAdd(s.ComboOptions(s.FollowSide)[0]);
                else s.FollowupEnd();
            }
        }
        return $"{s.Turn}|{s.Winner}|{s.Hp[0]}|{s.Hp[1]}|{s.Meter[0]}|{s.Meter[1]}|{s.Hand[0].Count}|{s.Hand[1].Count}";
    }

    static int PrimerLegal(CardSim s, int side)
    {
        for (int i = 0; i < s.Hand[side].Count; i++) if (s.LegalOpener(side, i)) return i;
        return -1;
    }

    static int PrimerGolpe(CardSim s, int side)
    {
        for (int i = 0; i < s.Hand[side].Count; i++)
        {
            var d = s.Def(side, s.Hand[side][i]);
            if ((d.Kind == CardKind.Attack || d.Kind == CardKind.Throw) &&
                (!d.IsSuper || s.Meter[side] >= d.SuperCost)) return i;
        }
        return -1;
    }
}
