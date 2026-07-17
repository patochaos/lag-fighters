using System.Collections.Generic;

namespace LagFighter
{
    // IA de planificación: arma una cola completa de hasta TurnFrames por
    // turno, igual que el jugador. No ve el plan del rival (solo posiciones
    // y stun actuales) — mismo juego de adivinar.
    public class SimpleAI
    {
        readonly System.Random _rng;

        public SimpleAI(int seed) { _rng = new System.Random(seed); }

        public List<int> Plan(MatchSim sim, int me)
        {
            int opp = 1 - me;
            var plan = new List<int>();
            int frames = 0;
            // posición estimada propia mientras planifica (rival asumido quieto)
            float myX = sim.Fighters[me].X;
            float oppX = sim.Fighters[opp].X;
            int face = oppX >= myX ? 1 : -1;

            // si el rival quedó derribado, presionar el okizeme
            bool oppDown = sim.StunRemaining(opp) > 20;

            while (frames < SimConfig.TurnFrames)
            {
                float dist = System.Math.Abs(oppX - myX);
                double r = _rng.NextDouble();
                int pick;

                if (oppDown && plan.Count < 3)
                {
                    pick = dist > 1.6f ? MoveCatalog.DashF : (r < 0.5 ? MoveCatalog.AttackB : MoveCatalog.AttackA);
                }
                else if (dist > 2.4f)
                {
                    if (r < 0.50) pick = MoveCatalog.WalkF;
                    else if (r < 0.68) pick = MoveCatalog.DashF;
                    else if (r < 0.78) pick = MoveCatalog.Wait;
                    else if (r < 0.88) pick = MoveCatalog.WalkB;
                    else pick = MoveCatalog.AttackB; // patada al aire de amenaza
                }
                else if (dist > 1.4f)
                {
                    if (r < 0.24) pick = MoveCatalog.WalkF;
                    else if (r < 0.42) pick = MoveCatalog.AttackB;
                    else if (r < 0.54) pick = MoveCatalog.AttackA;
                    else if (r < 0.68) pick = MoveCatalog.Guard;
                    else if (r < 0.78) pick = MoveCatalog.DashB;
                    else if (r < 0.88) pick = MoveCatalog.WalkB;
                    else pick = MoveCatalog.Wait;
                }
                else
                {
                    if (r < 0.30) pick = MoveCatalog.AttackA;
                    else if (r < 0.44) pick = MoveCatalog.AttackB;
                    else if (r < 0.64) pick = MoveCatalog.Guard;
                    else if (r < 0.76) pick = MoveCatalog.WalkB;
                    else if (r < 0.88) pick = MoveCatalog.DashB;
                    else pick = MoveCatalog.Wait;
                }

                var m = MoveCatalog.All[pick];
                if (frames + m.Total > SimConfig.TurnFrames)
                {
                    // rellenar el hueco final con esperas si entran
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
