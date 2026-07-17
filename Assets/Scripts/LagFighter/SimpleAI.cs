using System.Collections.Generic;

namespace LagFighter
{
    // IA de planificación: arma la cola completa del turno a ciegas (solo ve
    // posiciones y stun actuales). Juega un Ryu razonable: zonea con hadouken,
    // castiga knockdowns, mezcla footsies, y cada tanto apuesta un shoryuken.
    public class SimpleAI
    {
        readonly System.Random _rng;

        public SimpleAI(int seed) { _rng = new System.Random(seed); }

        public List<int> Plan(MatchSim sim, int me)
        {
            int opp = 1 - me;
            var plan = new List<int>();
            int frames = 0;
            float myX = sim.Fighters[me].X;
            float oppX = sim.Fighters[opp].X;
            int face = oppX >= myX ? 1 : -1;
            bool oppDown = sim.StunRemaining(opp) > 20;
            bool threwFireball = false;
            int budget = SimConfig.TurnFrames - sim.StunRemaining(me); // el stun arrastrado come turno

            while (frames < budget)
            {
                float dist = System.Math.Abs(oppX - myX);
                double r = _rng.NextDouble();
                int pick;

                if (oppDown && plan.Count < 3)
                {
                    // okizeme: acercarse y elegir entre pegar o agarrar al levantarse
                    pick = dist > 1.8f ? MoveCatalog.DashF :
                           r < 0.35 ? MoveCatalog.AttackB :
                           r < 0.55 ? MoveCatalog.Grab :
                           r < 0.80 ? MoveCatalog.AttackA : MoveCatalog.Tatsu;
                }
                else if (dist > 2.6f)
                {
                    if (!threwFireball && r < 0.35) { pick = MoveCatalog.Hadouken; threwFireball = true; }
                    else if (r < 0.60) pick = MoveCatalog.WalkF;
                    else if (r < 0.72) pick = MoveCatalog.DashF;
                    else if (r < 0.82) pick = MoveCatalog.JumpF; // por si viene hadouken de vuelta
                    else if (r < 0.92) pick = MoveCatalog.Wait;
                    else pick = MoveCatalog.WalkB;
                }
                else if (dist > 1.5f)
                {
                    if (r < 0.18) pick = MoveCatalog.WalkF;
                    else if (r < 0.34) pick = MoveCatalog.AttackB;
                    else if (r < 0.44) pick = MoveCatalog.AttackA;
                    else if (r < 0.54) pick = MoveCatalog.WalkB;   // bloquea caminando atrás
                    else if (r < 0.62) pick = MoveCatalog.Wait;    // neutral que bloquea
                    else if (r < 0.70) pick = MoveCatalog.Tatsu;   // giratoria que viaja
                    else if (r < 0.78) pick = MoveCatalog.DashB;
                    else if (r < 0.86) pick = MoveCatalog.JumpF;
                    else if (r < 0.94 && !threwFireball) { pick = MoveCatalog.Hadouken; threwFireball = true; }
                    else pick = MoveCatalog.AttackB;
                }
                else
                {
                    if (r < 0.20) pick = MoveCatalog.AttackA;
                    else if (r < 0.32) pick = MoveCatalog.AttackB;
                    else if (r < 0.46) pick = MoveCatalog.Grab;    // rompe a los bloqueadores
                    else if (r < 0.56) pick = MoveCatalog.WalkB;
                    else if (r < 0.64) pick = MoveCatalog.Wait;
                    else if (r < 0.72) pick = MoveCatalog.DashB;
                    else if (r < 0.78) pick = MoveCatalog.Shoryuken; // la apuesta
                    else if (r < 0.84) pick = MoveCatalog.Tatsu;
                    else if (r < 0.92) pick = MoveCatalog.JumpB;
                    else pick = MoveCatalog.JumpN;
                }

                var m = MoveCatalog.All[pick];
                if (frames + m.Total > budget)
                {
                    if (frames + MoveCatalog.All[MoveCatalog.Wait].Total <= budget)
                    {
                        plan.Add(MoveCatalog.Wait);
                        frames += MoveCatalog.All[MoveCatalog.Wait].Total;
                        continue;
                    }
                    break;
                }

                plan.Add(pick);
                frames += m.Total;
                myX += face * m.MoveDx;
                myX = System.Math.Max(-SimConfig.StageHalfWidth, System.Math.Min(SimConfig.StageHalfWidth, myX));
                if (oppDown && plan.Count >= 3) oppDown = false;
            }

            return plan;
        }
    }
}
