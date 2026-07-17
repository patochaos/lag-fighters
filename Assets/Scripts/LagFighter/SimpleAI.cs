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

            while (frames < SimConfig.TurnFrames)
            {
                float dist = System.Math.Abs(oppX - myX);
                double r = _rng.NextDouble();
                int pick;

                if (oppDown && plan.Count < 3)
                {
                    // okizeme: acercarse y plantar una patada o presión
                    pick = dist > 1.8f ? MoveCatalog.DashF :
                           r < 0.45 ? MoveCatalog.AttackB :
                           r < 0.75 ? MoveCatalog.AttackA : MoveCatalog.Hadouken;
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
                    if (r < 0.20) pick = MoveCatalog.WalkF;
                    else if (r < 0.38) pick = MoveCatalog.AttackB;
                    else if (r < 0.48) pick = MoveCatalog.AttackA;
                    else if (r < 0.58) pick = MoveCatalog.WalkB;   // bloquea caminando atrás
                    else if (r < 0.66) pick = MoveCatalog.Wait;    // neutral que bloquea
                    else if (r < 0.74) pick = MoveCatalog.DashB;
                    else if (r < 0.82) pick = MoveCatalog.JumpF;
                    else if (r < 0.90 && !threwFireball) { pick = MoveCatalog.Hadouken; threwFireball = true; }
                    else pick = MoveCatalog.AttackB;
                }
                else
                {
                    if (r < 0.26) pick = MoveCatalog.AttackA;
                    else if (r < 0.40) pick = MoveCatalog.AttackB;
                    else if (r < 0.52) pick = MoveCatalog.WalkB;
                    else if (r < 0.62) pick = MoveCatalog.Wait;
                    else if (r < 0.70) pick = MoveCatalog.DashB;
                    else if (r < 0.78) pick = MoveCatalog.Shoryuken; // la apuesta
                    else if (r < 0.88) pick = MoveCatalog.JumpB;
                    else pick = MoveCatalog.AttackA;
                }

                var m = MoveCatalog.All[pick];
                if (frames + m.Total > SimConfig.TurnFrames)
                {
                    if (frames + MoveCatalog.All[MoveCatalog.Wait].Total <= SimConfig.TurnFrames)
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
