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
            int frames = 0;
            foreach (int move in plan) frames += MoveCatalog.All[move].Total;
            ok &= frames <= SimConfig.TurnFrames && ai.ResolvedProfile != AIProfile.Random;
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
        Run(s, 32); // dos dashes = 32f ejecutando
        Check(s.Fighters[0].Guard > 40f, "la guardia regenera ejecutando moves", $"guard {s.Fighters[0].Guard}");
        float afterDash = s.Fighters[0].Guard;
        s.SetQueue(0, new List<int> { MoveCatalog.WalkB });
        Run(s, 20); // bloquear: tampoco regenera
        Check(s.Fighters[0].Guard == afterDash, "bloquear no regenera guardia", $"guard {s.Fighters[0].Guard}");
    }

    // Turno fluido (SimConfig.CarryoverEnabled): el move en curso cruza el
    // límite del turno en vez de cortarse; apagado, se corta y se pierde.
    static void TurnoFluidoCruzaElLimite()
    {
        SimConfig.CarryoverEnabled = true;
        try
        {
            var s = NewSim(-2.5f, 2.5f, p1Blocks: false);
            s.SetQueue(0, new List<int> { MoveCatalog.DashF, MoveCatalog.Hadouken }); // 16f + 60f: cruza el límite de 60
            Run(s, SimConfig.TurnFrames);
            int lost = s.OnTurnEnd(0);
            bool sigue = s.Fighters[0].MoveIndex == MoveCatalog.Hadouken;
            int resto = s.CommittedRemaining(0);
            Check(lost == 0 && sigue && resto > 0 && resto <= 16,
                "turno fluido: el hadouken cruza el límite comprometido",
                $"lost {lost}, move {s.Fighters[0].MoveIndex}, resto {resto}");
            Check(s.Fighters[0].Super == resto, "los frames de overflow cargan la barra de super",
                $"super {s.Fighters[0].Super}, resto {resto}");
            Run(s, resto + 2);
            Check(s.Fighters[0].MoveIndex == -1, "turno fluido: el move comprometido termina en el turno siguiente",
                $"move {s.Fighters[0].MoveIndex}");
        }
        finally { SimConfig.CarryoverEnabled = false; }

        var s2 = NewSim(-2.5f, 2.5f, p1Blocks: false);
        s2.SetQueue(0, new List<int> { MoveCatalog.DashF, MoveCatalog.Hadouken });
        Run(s2, SimConfig.TurnFrames);
        int lost2 = s2.OnTurnEnd(0);
        Check(lost2 == 1 && s2.Fighters[0].MoveIndex == -1 && s2.CommittedRemaining(0) == 0,
            "turno estricto: el mismo move se corta y cuenta como orden perdida", $"lost {lost2}");
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
