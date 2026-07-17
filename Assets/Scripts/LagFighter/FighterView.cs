using System.Collections.Generic;
using UnityEngine;

namespace LagFighter
{
    // Stickman de bloques 3D: legible, barato, y calza con los hurt/hitboxes
    // rectangulares. Animación 100% procedural desde el estado de la sim.
    public class FighterView : MonoBehaviour
    {
        MatchController _mc;
        int _index;
        bool _ghostMode;

        Transform _rig;
        Transform _torso, _head, _armF, _armB, _legF, _legB;
        readonly List<Renderer> _tintRenderers = new List<Renderer>();
        readonly List<Color> _origColors = new List<Color>();
        float _flash;
        Color _flashColor;
        float _faceYaw;

        static readonly Vector3 TorsoPos = new Vector3(0f, 1.22f, 0f);
        static readonly Vector3 HeadPos = new Vector3(0f, 1.66f, 0f);
        static readonly Vector3 ArmFPos = new Vector3(0.16f, 1.38f, 0.18f);
        static readonly Vector3 ArmBPos = new Vector3(-0.16f, 1.38f, 0.10f);
        static readonly Vector3 LegFPos = new Vector3(0.10f, 0.48f, 0.05f);
        static readonly Vector3 LegBPos = new Vector3(-0.10f, 0.48f, -0.05f);

        public static FighterView Create(int index, MatchController mc)
        {
            var go = new GameObject("Fighter" + index);
            var v = go.AddComponent<FighterView>();
            v._mc = mc;
            v._index = index;
            v.BuildRig(index == 0 ? new Color(0.25f, 0.7f, 0.95f) : new Color(0.95f, 0.45f, 0.25f));
            return v;
        }

        // Mismo blockman pero fantasma: semi-transparente, posado por GhostViz.
        public static FighterView CreateGhost(int index)
        {
            var go = new GameObject("GhostFighter" + index);
            var v = go.AddComponent<FighterView>();
            v._index = index;
            v._ghostMode = true;
            v.BuildRig(index == 0 ? new Color(0.35f, 0.8f, 1f) : new Color(1f, 0.55f, 0.35f));
            for (int i = 0; i < v._tintRenderers.Count; i++)
            {
                var c = v._origColors[i];
                c.a = 0.34f;
                v._tintRenderers[i].material = new Material(VizLib.BaseMat) { color = c };
                v._origColors[i] = c; // el tint loop de ApplyPose conserva el alpha
            }
            return v;
        }

        void BuildRig(Color baseColor)
        {
            _rig = new GameObject("Rig").transform;
            _rig.SetParent(transform, false);

            var dark = baseColor * 0.55f;
            _torso = Part(TorsoPos, new Vector3(0.44f, 0.62f, 0.3f), baseColor);
            _head = Part(HeadPos, new Vector3(0.28f, 0.28f, 0.28f), baseColor * 1.25f);
            _armF = Part(ArmFPos, new Vector3(0.15f, 0.15f, 0.55f), dark);
            _armB = Part(ArmBPos, new Vector3(0.15f, 0.15f, 0.5f), dark);
            _legF = Part(LegFPos, new Vector3(0.17f, 0.95f, 0.2f), dark);
            _legB = Part(LegBPos, new Vector3(0.17f, 0.95f, 0.2f), dark);
        }

        Transform Part(Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_rig, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            MatLib.Apply(go, color);
            var r = go.GetComponent<Renderer>();
            _tintRenderers.Add(r);
            _origColors.Add(color);
            return go.transform;
        }

        public void OnMatchReset()
        {
            _flash = 0f;
            transform.position = new Vector3(_mc.Sim.Fighters[_index].X, 0f, 0f);
        }

        public void FlashHit() { _flash = 1f; _flashColor = Color.white; }
        public void FlashBlock() { _flash = 0.7f; _flashColor = new Color(0.4f, 0.8f, 1f); }

        void Update()
        {
            if (_ghostMode) return; // al ghost lo posa GhostViz con su propia sim
            if (_mc == null || _mc.Sim == null) return;
            bool showBlock = _mc.State == MatchController.Flow.Executing || _mc.State == MatchController.Flow.Replay;
            ApplyPose(_mc.Sim, _mc.TickFloat, Time.deltaTime, showBlock);
        }

        // Posa el rig desde CUALQUIER sim (la real o la del ghost de preview).
        public void ApplyPose(MatchSim sim, float tf, float dt, bool showBlockPose)
        {
            var f = sim.Fighters[_index];

            var target = new Vector3(f.X, 0f, 0f);
            if ((target - transform.position).sqrMagnitude > 4f)
                transform.position = target; // salto grande (loop del ghost / reset): sin smear
            else
                transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-14f * dt));
            float wantYaw = f.Face > 0 ? 90f : -90f;
            _faceYaw = Mathf.LerpAngle(_faceYaw, wantYaw, 1f - Mathf.Exp(-10f * dt));

            var m = sim.CurrentMove(_index);
            float phase = m != null ? Mathf.Clamp(tf - f.MoveStartTick, 0f, m.Total) : 0f;
            float pk = 0f;
            if (m != null)
            {
                if (phase < m.Startup) pk = m.Startup <= 0 ? 1f : phase / m.Startup;
                else if (phase < m.Startup + m.Active) pk = 1f;
                else pk = m.Recovery <= 0 ? 0f : 1f - (phase - m.Startup - m.Active) / m.Recovery;
            }

            bool stunned = sim.IsStunned(_index);
            bool down = stunned && f.Stun == StunKind.Knockdown;
            bool loser = sim.Over && sim.Winner != _index;
            bool winner = sim.Over && sim.Winner == _index;

            // arco vertical (saltos y shoryuken)
            float airY = 0f;
            if (m != null && m.HasAir && phase >= m.AirStart && phase < m.AirEnd)
            {
                float ap = (phase - m.AirStart) / (m.AirEnd - m.AirStart);
                airY = Mathf.Sin(ap * Mathf.PI) * (m.Anim == AnimKind.Dragon ? 1.0f : 1.25f);
            }

            float breathe = Mathf.Sin(Time.time * 2.5f + _index) * 0.015f;
            var armF = ArmFPos; var armB = ArmBPos; var legF = LegFPos; var legB = LegBPos;
            var legFRot = Quaternion.identity; var legBRot = Quaternion.identity;
            float rigPitch = 0f, rigZOff = 0f, spinYaw = 0f;

            if (m != null && !stunned)
            {
                switch (m.Anim)
                {
                    case AnimKind.Walk:
                        float swing = Mathf.Sin(pk * Mathf.PI * 2f) * 22f;
                        legFRot = Quaternion.Euler(swing, 0f, 0f);
                        legBRot = Quaternion.Euler(-swing, 0f, 0f);
                        break;
                    case AnimKind.Dash:
                        rigPitch = m.MoveDx > 0f ? 14f : -12f;
                        legFRot = Quaternion.Euler(30f, 0f, 0f);
                        legBRot = Quaternion.Euler(-30f, 0f, 0f);
                        break;
                    case AnimKind.Jump:
                        if (m.Hits.Length > 0 && phase >= 18f && phase < 32f)
                        {
                            // patada aérea: pierna estirada adelante-abajo
                            legF = new Vector3(0.08f, 0.42f, 0.5f);
                            legFRot = Quaternion.Euler(115f, 0f, 0f);
                            legBRot = Quaternion.Euler(35f, 0f, 0f);
                        }
                        else
                        {
                            // piernas recogidas en el aire
                            legFRot = Quaternion.Euler(airY > 0.05f ? 55f : 0f, 0f, 0f);
                            legBRot = Quaternion.Euler(airY > 0.05f ? 40f : 0f, 0f, 0f);
                        }
                        break;
                    case AnimKind.AttackA:
                        armF = Vector3.Lerp(ArmFPos, new Vector3(0.1f, 1.42f, 0.78f), pk);
                        rigZOff = pk * 0.1f;
                        break;
                    case AnimKind.AttackB:
                        legF = Vector3.Lerp(LegFPos, new Vector3(0.08f, 0.9f, 0.55f), pk);
                        legFRot = Quaternion.Euler(Mathf.Lerp(0f, 80f, pk), 0f, 0f);
                        rigPitch = -pk * 10f;
                        break;
                    case AnimKind.Fireball:
                        // las dos manos juntas al frente
                        armF = Vector3.Lerp(ArmFPos, new Vector3(0.06f, 1.2f, 0.7f), pk);
                        armB = Vector3.Lerp(ArmBPos, new Vector3(-0.06f, 1.2f, 0.66f), pk);
                        rigZOff = pk * 0.06f;
                        break;
                    case AnimKind.Dragon:
                        // uppercut: brazo al cielo, cuerpo subiendo
                        armF = Vector3.Lerp(ArmFPos, new Vector3(0.1f, 2.05f, 0.3f), Mathf.Clamp01(phase / Mathf.Max(1, m.Startup + m.Active)));
                        legFRot = Quaternion.Euler(45f, 0f, 0f);
                        rigPitch = -6f;
                        break;
                    case AnimKind.Tatsu:
                        // patada giratoria: el cuerpo gira 720° con la pierna extendida
                        spinYaw = (phase / m.Total) * 720f;
                        legF = new Vector3(0.08f, 1.05f, 0.45f);
                        legFRot = Quaternion.Euler(80f, 0f, 0f);
                        legBRot = Quaternion.Euler(-15f, 0f, 0f);
                        break;
                    case AnimKind.Grab:
                        // agarre: los brazos se ABREN bien anchos en el startup
                        // y se CIERRAN como pinza al frente en la ventana activa
                        if (phase < m.Startup)
                        {
                            float open = m.Startup <= 0 ? 1f : phase / m.Startup;
                            armF = Vector3.Lerp(ArmFPos, new Vector3(0.48f, 1.32f, 0.34f), open);
                            armB = Vector3.Lerp(ArmBPos, new Vector3(-0.48f, 1.32f, 0.3f), open);
                        }
                        else
                        {
                            armF = new Vector3(0.07f, 1.28f, 0.66f);
                            armB = new Vector3(-0.07f, 1.28f, 0.62f);
                            rigZOff = 0.16f;
                            rigPitch = 8f; // se tira hacia adelante a buscar el cuerpo
                        }
                        break;
                }
            }

            if (stunned && !down) rigPitch = f.Stun == StunKind.Blockstun ? -6f : -16f;

            float lieAngle = down || loser ? -85f : rigPitch;
            var wantRot = Quaternion.Euler(lieAngle, _faceYaw + spinYaw, 0f);
            _rig.localRotation = spinYaw > 0.01f ? wantRot : Quaternion.Slerp(_rig.localRotation, wantRot, 1f - Mathf.Exp(-9f * dt));
            _rig.localPosition = new Vector3(0f, (down || loser) ? 0.25f : airY + breathe, 0f);

            if (winner) armF = new Vector3(0.12f, 1.7f, 0.1f);

            // bloqueo visible: brazos cubriendo (si está en estado de guardia durante ejecución)
            bool blocking = sim.IsBlockingState(_index) && !sim.Over && showBlockPose;
            if (blocking && (m == null || m.Anim == AnimKind.Walk || m.Anim == AnimKind.Wait))
            {
                armF = new Vector3(0.1f, 1.3f, 0.4f);
                armB = new Vector3(-0.1f, 1.44f, 0.36f);
            }

            _armF.localPosition = armF + new Vector3(0f, 0f, rigZOff);
            _armB.localPosition = armB;
            _legF.localPosition = legF;
            _legB.localPosition = legB;
            _legF.localRotation = legFRot;
            _legB.localRotation = legBRot;

            _flash = Mathf.Max(0f, _flash - dt * 4f);
            for (int i = 0; i < _tintRenderers.Count; i++)
            {
                Color shown = f.Stun == StunKind.Blockstun && stunned ? Color.Lerp(_origColors[i], new Color(0.4f, 0.6f, 1f), 0.4f) : _origColors[i];
                shown = Color.Lerp(shown, _flashColor, _flash);
                _tintRenderers[i].material.color = shown;
            }
        }
    }
}
