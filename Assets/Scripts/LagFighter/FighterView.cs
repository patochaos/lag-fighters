using System.Collections.Generic;
using UnityEngine;

namespace LagFighter
{
    // Stickman de bloques 3D: legible, barato, y calza perfecto con los
    // hurtboxes/hitboxes rectangulares. Animación 100% procedural desde el
    // estado de la sim. Sin lógica de juego.
    public class FighterView : MonoBehaviour
    {
        MatchController _mc;
        int _index;

        Transform _rig;           // rota para encarar y para caídas
        Transform _torso, _head, _armF, _armB, _legF, _legB;
        readonly List<Renderer> _tintRenderers = new List<Renderer>();
        readonly List<Color> _origColors = new List<Color>();
        float _flash;
        Color _flashColor;
        float _faceYaw;

        // poses base (local, +z = hacia el rival)
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
            var r = go.GetComponent<Renderer>();
            r.material.color = color;
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
            var sim = _mc.Sim;
            if (sim == null) return;
            var f = sim.Fighters[_index];
            float tf = _mc.TickFloat;
            float dt = Time.deltaTime;

            // posición y facing
            var target = new Vector3(f.X, 0f, 0f);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-14f * dt));
            float wantYaw = f.Face > 0 ? 90f : -90f; // local +z hacia el rival
            _faceYaw = Mathf.LerpAngle(_faceYaw, wantYaw, 1f - Mathf.Exp(-10f * dt));

            // estado visual
            var m = sim.CurrentMove(_index);
            float phase = m != null ? Mathf.Clamp(tf - f.MoveStartTick, 0f, m.Total) : 0f;
            float p01 = m != null ? phase / m.Total : 0f;
            float pk = 0f; // progreso 0→1→0 por fases
            if (m != null)
            {
                if (phase < m.Startup) pk = m.Startup <= 0 ? 1f : phase / m.Startup;
                else if (phase < m.Startup + m.Active) pk = 1f;
                else pk = m.Recovery <= 0 ? 0f : 1f - (phase - m.Startup - m.Active) / m.Recovery;
            }

            bool stunned = sim.IsStunned(_index);
            bool down = f.Down && stunned;
            bool loser = sim.Over && sim.Winner != _index;
            bool winner = sim.Over && sim.Winner == _index;

            // pose por defecto + respiración
            float breathe = Mathf.Sin(Time.time * 2.5f + _index) * 0.015f;
            var armF = ArmFPos; var armB = ArmBPos; var legF = LegFPos; var legB = LegBPos;
            var armFRot = Quaternion.identity; var legFRot = Quaternion.identity; var legBRot = Quaternion.identity;
            float rigPitch = 0f, rigZOff = 0f;

            if (m != null && !stunned)
            {
                switch (m.Anim)
                {
                    case AnimKind.Walk:
                        float swing = Mathf.Sin(p01 * Mathf.PI * 2f) * 22f;
                        legFRot = Quaternion.Euler(swing, 0f, 0f);
                        legBRot = Quaternion.Euler(-swing, 0f, 0f);
                        break;
                    case AnimKind.Dash:
                        rigPitch = m.MoveDx > 0f ? 14f : -12f;
                        legFRot = Quaternion.Euler(30f, 0f, 0f);
                        legBRot = Quaternion.Euler(-30f, 0f, 0f);
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
                    case AnimKind.Guard:
                        armF = new Vector3(0.1f, 1.3f, 0.42f);
                        armB = new Vector3(-0.1f, 1.45f, 0.38f);
                        break;
                }
            }

            if (stunned && !down) rigPitch = -16f;

            // caída (knockdown / KO): el rig se acuesta
            float lieAngle = down || loser ? -85f : rigPitch;
            var wantRot = Quaternion.Euler(lieAngle, _faceYaw, 0f);
            _rig.localRotation = Quaternion.Slerp(_rig.localRotation, wantRot, 1f - Mathf.Exp(-9f * dt));
            _rig.localPosition = new Vector3(0f, (down || loser) ? 0.25f : breathe, 0f);

            // brazo ganador al cielo
            if (winner) armF = new Vector3(0.12f, 1.7f, 0.1f);

            _armF.localPosition = armF + new Vector3(0f, 0f, rigZOff);
            _armB.localPosition = armB;
            _armF.localRotation = armFRot;
            _legF.localPosition = legF;
            _legB.localPosition = legB;
            _legF.localRotation = legFRot;
            _legB.localRotation = legBRot;

            // flashes
            _flash = Mathf.Max(0f, _flash - dt * 4f);
            bool guarding = sim.IsGuarding(_index);
            for (int i = 0; i < _tintRenderers.Count; i++)
            {
                Color shown = guarding ? Color.Lerp(_origColors[i], new Color(0.4f, 0.6f, 1f), 0.4f) : _origColors[i];
                shown = Color.Lerp(shown, _flashColor, _flash);
                _tintRenderers[i].material.color = shown;
            }
        }
    }
}
