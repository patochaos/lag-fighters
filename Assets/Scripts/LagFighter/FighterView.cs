using System.Collections.Generic;
using UnityEngine;

namespace LagFighter
{
    // Stickman de bloques 3D: legible, barato, y calza con los hurt/hitboxes
    // rectangulares. Animación 100% procedural desde el estado de la sim.
    //
    // En modo DUELO NO se anima desde la sim de frames: el cuerpo es un
    // MARCADOR DE ESTADO con un vocabulario de poses propio. Esa mitad vive en
    // FighterViewDuel.cs (ver DUELO-LOOK.md §5).
    public partial class FighterView : MonoBehaviour
    {
        MatchController _mc;
        int _index;
        bool _ghostMode;

        Transform _rig;
        Transform _torso, _head, _armF, _armB, _legF, _legB;
        Renderer _armFR, _fistFR, _legFR, _footFR; // para el tinte de fase del limb que ataca
        readonly List<Renderer> _tintRenderers = new List<Renderer>();
        readonly List<Color> _origColors = new List<Color>();
        TrailRenderer _trailArm, _trailLeg;
        Transform _shadow;
        Renderer _shadowR;
        bool _wasWinner;
        float _flash;
        Color _flashColor;
        float _faceYaw;
        MoveDef _calloutMove;
        float _calloutStart;
        Color _baseColor;
        float _lastAfterimage;
        MoveDef _dustMove;
        float _dustStart;
        bool _prevAir, _prevDown;

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
            _baseColor = baseColor;
            _rig = new GameObject("Rig").transform;
            _rig.SetParent(transform, false);

            var dark = baseColor * 0.55f;
            _torso = Part(TorsoPos, new Vector3(0.44f, 0.62f, 0.3f), baseColor);
            _head = Part(HeadPos, new Vector3(0.28f, 0.28f, 0.28f), baseColor * 1.25f);

            // ojos: un cubito oscuro por lateral de la cabeza, pegado al borde
            // delantero (la cámara es lateral: en la cara frontal no se veían).
            // La cabeza lisa obligaba a leer los brazos para saber el facing.
            var eyeC = new Color(0.07f, 0.08f, 0.11f);
            Extremity(_head, new Vector3(0.5f, 0.16f, 0.26f), new Vector3(0.045f, 0.11f, 0.11f), eyeC);
            Extremity(_head, new Vector3(-0.5f, 0.16f, 0.26f), new Vector3(0.045f, 0.11f, 0.11f), eyeC);
            _armF = Part(ArmFPos, new Vector3(0.15f, 0.15f, 0.55f), dark);
            _armB = Part(ArmBPos, new Vector3(0.15f, 0.15f, 0.5f), dark);
            _legF = Part(LegFPos, new Vector3(0.17f, 0.95f, 0.2f), dark);
            _legB = Part(LegBPos, new Vector3(0.17f, 0.95f, 0.2f), dark);
            _armFR = _armF.GetComponent<Renderer>();
            _legFR = _legF.GetComponent<Renderer>();

            // puños y pies: bloques brillantes en la punta de cada limb. Son
            // el punto de contacto real de los golpes, así que destacan igual
            // que la cabeza y viajan gratis con la rotación del limb padre.
            var bright = baseColor * 1.15f;
            var fistF = Extremity(_armF, new Vector3(0f, 0f, 0.66f), new Vector3(0.22f, 0.22f, 0.22f), bright);
            Extremity(_armB, new Vector3(0f, 0f, 0.68f), new Vector3(0.2f, 0.2f, 0.2f), bright);
            var footF = Extremity(_legF, new Vector3(0f, -0.52f, 0.15f), new Vector3(0.24f, 0.13f, 0.34f), bright);
            Extremity(_legB, new Vector3(0f, -0.52f, 0.15f), new Vector3(0.24f, 0.13f, 0.34f), bright);
            _fistFR = fistF.GetComponent<Renderer>();
            _footFR = footF.GetComponent<Renderer>();

            if (!_ghostMode)
            {
                // los trails salen del puño/pie: marcan el punto de contacto
                _trailArm = MakeTrail(fistF, baseColor);
                _trailLeg = MakeTrail(footF, baseColor);

                // sombra de contacto: queda en el piso cuando el rig salta,
                // así se lee de un vistazo dónde va a caer
                var sh = VizLib.MakeBox("Shadow", new Color(0f, 0f, 0f, 0.42f), transform);
                sh.transform.localPosition = new Vector3(0f, 0.025f, 0f);
                sh.transform.localScale = new Vector3(0.62f, 0.012f, 0.42f);
                _shadow = sh.transform;
                _shadowR = sh.GetComponent<Renderer>();
            }
        }

        // Trail en la mano/pie de adelante: solo emite durante frames activos.
        TrailRenderer MakeTrail(Transform limb, Color baseColor)
        {
            var tr = limb.gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.16f;
            tr.startWidth = 0.11f;
            tr.endWidth = 0.01f;
            tr.numCapVertices = 2;
            tr.material = new Material(VizLib.BaseMat) { color = new Color(1f, 1f, 1f, 0.55f) };
            tr.startColor = Color.Lerp(baseColor, Color.white, 0.6f);
            tr.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.emitting = false;
            return tr;
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

        // Cubo hijo de un limb (puño/pie): hereda posición y rotación del
        // padre. localPos va en unidades locales del padre; worldSize se
        // compensa contra la escala no uniforme del limb.
        Transform Extremity(Transform limb, Vector3 localPos, Vector3 worldSize, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(limb, false);
            var ps = limb.localScale;
            go.transform.localScale = new Vector3(worldSize.x / ps.x, worldSize.y / ps.y, worldSize.z / ps.z);
            go.transform.localPosition = localPos;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            MatLib.Apply(go, color);
            _tintRenderers.Add(go.GetComponent<Renderer>());
            _origColors.Add(color);
            return go.transform;
        }

        public void OnMatchReset()
        {
            _flash = 0f;
            _wasWinner = false;
            transform.position = new Vector3(_mc.Sim.Fighters[_index].X, 0f, 0f);
        }

        public void FlashHit(bool counter = false)
        {
            // counter hit: flash naranja y más largo, se distingue de un golpe normal
            _flash = counter ? 1.4f : 1f;
            _flashColor = counter ? new Color(1f, 0.45f, 0.05f) : Color.white;
        }
        public void FlashBlock() { _flash = 0.7f; _flashColor = new Color(0.4f, 0.8f, 1f); }
        public void FlashParry() { _flash = 1f; _flashColor = new Color(0.25f, 0.95f, 1f); }

        void Update()
        {
            if (_ghostMode) return; // al ghost lo posa GhostViz con su propia sim
            if (SimConfig.DuelEnabled) { TickDuel(Time.deltaTime); return; }
            if (_mc == null || _mc.Sim == null) return;
            bool showBlock = _mc.State == MatchController.Flow.Executing || _mc.State == MatchController.Flow.Replay;
            ApplyPose(_mc.Sim, _mc.TickFloat, Time.deltaTime, showBlock);
        }

        // Posa el rig desde CUALQUIER sim (la real o la del ghost de preview).
        public void ApplyPose(MatchSim sim, float tf, float dt, bool showBlockPose)
        {
            var f = sim.Fighters[_index];

            // pérdida de miembros: el bloque desaparece del rig
            if (_armF.gameObject.activeSelf != f.ArmHp > 0f) _armF.gameObject.SetActive(f.ArmHp > 0f);
            if (_legF.gameObject.activeSelf != f.LegHp > 0f) _legF.gameObject.SetActive(f.LegHp > 0f);

            // Posición HONESTA: interpolación entre el tick anterior y el actual
            // con el acumulador de la sim. El smoothing exponencial viejo dejaba
            // al muñeco ~0.4u atrás en un dash (media hurtbox de mentira visual).
            // El smear queda para inclinación, extremidades y trails.
            float frac = Mathf.Clamp01(tf - sim.Tick); // acc/tickDur: cuánto avanzó el frame visual
            float renderX = Mathf.Lerp(f.PrevX, f.X, frac);
            transform.position = new Vector3(renderX, 0f, 0f);
            float wantYaw = f.Face > 0 ? 90f : -90f;
            _faceYaw = Mathf.LerpAngle(_faceYaw, wantYaw, 1f - Mathf.Exp(-10f * dt));

            var m = sim.CurrentMove(_index);
            float phase = m != null ? Mathf.Clamp(tf - f.MoveStartTick, 0f, m.Total) : 0f;

            // cartel con el nombre del ataque al arrancar: se entiende QUÉ tiró
            // cada uno sin tener que leer las fichas de la timeline.
            //
            // En DUELO no: el nombre lo dice la CARTA. Estos carteles usan los
            // nombres del catálogo clásico ("BARRIDA", "GOLPE FUERTE") mientras
            // el veredicto dice "JAB (A) conecta por 3" — dos vocabularios para
            // la misma acción. Y con los peleadores a un metro se pisaban entre
            // sí en el centro de la pantalla.
            if (!_ghostMode && !SimConfig.DuelEnabled && showBlockPose && m != null && m.IsAttack
                && (m != _calloutMove || f.MoveStartTick != _calloutStart))
            {
                _calloutMove = m;
                _calloutStart = f.MoveStartTick;
                WorldFX.Popup(f.X, m.Name.ToUpperInvariant(),
                    _index == 0 ? new Color(0.55f, 0.85f, 1f) : new Color(1f, 0.7f, 0.5f), 0.85f, WorldFX.LaneCallout);
            }
            float pk = 0f;
            if (m != null)
            {
                if (phase < m.Startup) pk = m.Startup <= 0 ? 1f : phase / m.Startup;
                else if (phase < m.Startup + m.Active) pk = 1f;
                else pk = m.Recovery <= 0 ? 0f : 1f - (phase - m.Startup - m.Active) / m.Recovery;
            }

            // Curva de ataque con anticipación (usar con LerpUnclamped):
            // startup = CARGA hacia atrás (t negativo), primeros frames activos
            // = SNAP a extensión con overshoot, recovery = vuelta lenta (1-t²,
            // se queda extendido: se LEE que es castigable). pk lineal queda
            // para los moves de movimiento.
            // wind-up gordo (−0.7): la regla de oro de legibilidad es que el
            // startup se TELEGRAFÍE — acá además es info de juego (counters)
            float atk = 0f;
            if (m != null)
            {
                if (phase < m.Startup)
                    atk = m.Startup <= 0 ? 1f : -0.7f * Mathf.Sin(phase / m.Startup * Mathf.PI * 0.5f);
                else if (phase < m.Startup + m.Active)
                {
                    float ap = m.Active <= 0 ? 1f : (phase - m.Startup) / m.Active;
                    atk = 1f + 0.12f * Mathf.Max(0f, 1f - ap * 3f);
                }
                else
                {
                    float rp = m.Recovery <= 0 ? 1f : (phase - m.Startup - m.Active) / m.Recovery;
                    atk = 1f - rp * rp;
                }
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
            var torsoRot = Quaternion.identity; var head = HeadPos;
            float rigPitch = 0f, rigZOff = 0f, spinYaw = 0f, crouchY = 0f;

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
                        // jab: carga atrás en startup, snap al frente en activos,
                        // el torso y la cabeza acompañan el golpe
                        armF = Vector3.LerpUnclamped(ArmFPos, new Vector3(0.1f, 1.42f, 0.78f), atk);
                        rigZOff = Mathf.Max(0f, atk) * 0.1f;
                        torsoRot = Quaternion.Euler(0f, atk * 16f, 0f);
                        head = HeadPos + new Vector3(0f, 0f, atk * 0.05f);
                        break;
                    case AnimKind.AttackB:
                        // barrida: la pierna se arma atrás y barre con snap
                        legF = Vector3.LerpUnclamped(LegFPos, new Vector3(0.08f, 0.9f, 0.55f), atk);
                        legFRot = Quaternion.Euler(Mathf.LerpUnclamped(0f, 80f, atk), 0f, 0f);
                        rigPitch = -Mathf.Max(0f, atk) * 10f;
                        torsoRot = Quaternion.Euler(Mathf.Max(0f, atk) * 10f, 0f, 0f);
                        break;
                    case AnimKind.Fireball:
                        // carga: las manos van a la cadera en startup y DISPARAN
                        // juntas al frente en el frame de spawn
                        armF = Vector3.LerpUnclamped(ArmFPos, new Vector3(0.06f, 1.2f, 0.7f), atk);
                        armB = Vector3.LerpUnclamped(ArmBPos, new Vector3(-0.06f, 1.2f, 0.66f), atk);
                        rigZOff = Mathf.Max(0f, atk) * 0.06f;
                        torsoRot = Quaternion.Euler(0f, atk * 10f, 0f);
                        break;
                    case AnimKind.Dragon:
                        // uppercut: se agacha un toque en la carga y sube con el
                        // brazo al cielo; en recovery el brazo baja (vulnerable)
                        armF = Vector3.LerpUnclamped(ArmFPos, new Vector3(0.1f, 2.05f, 0.3f), Mathf.Max(atk, -0.3f));
                        crouchY = atk < 0f ? atk * 0.5f : 0f;
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
                    case AnimKind.Crouch:
                        // en cuclillas: el rig baja y las piernas se pliegan
                        crouchY = -0.5f;
                        legFRot = Quaternion.Euler(75f, 0f, 0f);
                        legBRot = Quaternion.Euler(75f, 0f, 0f);
                        armF = new Vector3(0.1f, 1.15f, 0.38f);
                        armB = new Vector3(-0.1f, 1.28f, 0.34f);
                        break;
                    case AnimKind.LowKick:
                        // rastrera: agachado con la pierna estirada al ras del piso
                        crouchY = -0.5f;
                        legBRot = Quaternion.Euler(75f, 0f, 0f);
                        legF = Vector3.Lerp(LegFPos, new Vector3(0.08f, 0.62f, 0.55f), pk);
                        legFRot = Quaternion.Euler(Mathf.Lerp(20f, 95f, pk), 0f, 0f);
                        rigPitch = pk * 6f;
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
                    case AnimKind.Parry:
                        // Brazos cruzados y cuerpo hacia atrás: lectura activa,
                        // visualmente distinta de la guardia automática.
                        armF = new Vector3(-0.08f, 1.42f, 0.46f);
                        armB = new Vector3(0.08f, 1.24f, 0.42f);
                        rigPitch = -10f;
                        break;
                }
            }

            if (stunned && !down)
            {
                rigPitch = f.Stun == StunKind.Blockstun ? -6f : -16f;
                if (f.Stun == StunKind.Hitstun) head = HeadPos + new Vector3(0f, 0.02f, -0.1f); // cabeza sacudida
            }

            // FX de movimiento (solo la vista real, no el ghost de preview):
            // estela en el dash, polvo al dashear, aterrizar y levantarse
            if (!_ghostMode)
            {
                if (m != null && m.Anim == AnimKind.Dash && !stunned)
                {
                    if (m != _dustMove || f.MoveStartTick != _dustStart)
                    {
                        _dustMove = m;
                        _dustStart = f.MoveStartTick;
                        SparkFX.Dust(transform.position, 6);
                    }
                    if (Time.time - _lastAfterimage > 0.045f)
                    {
                        _lastAfterimage = Time.time;
                        AfterimageFX.Spawn(transform.position, _baseColor);
                    }
                }
                bool airNow = airY > 0.05f;
                if (_prevAir && !airNow) SparkFX.Dust(transform.position, 8);
                _prevAir = airNow;
                if (_prevDown && !down && !sim.Over) SparkFX.Dust(transform.position, 5);
                _prevDown = down;
            }

            // festejo: saltitos + burst dorado una sola vez + público eufórico
            float winBounce = 0f;
            if (winner && !_ghostMode)
            {
                winBounce = Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.14f;
                if (!_wasWinner)
                {
                    _wasWinner = true;
                    SparkFX.Burst(transform.position + new Vector3(0f, 1.7f, 0f), new Color(1f, 0.85f, 0.3f), 18, 3.6f);
                    CrowdBob.Excite(3.5f);
                }
            }
            else if (!winner) _wasWinner = false;

            float lieAngle = down || loser ? -85f : rigPitch;
            var wantRot = Quaternion.Euler(lieAngle, _faceYaw + spinYaw, 0f);
            _rig.localRotation = spinYaw > 0.01f ? wantRot : Quaternion.Slerp(_rig.localRotation, wantRot, 1f - Mathf.Exp(-9f * dt));

            // sacudida corta al comer un golpe (el envelope reusa el flash del hit)
            float shake = stunned && !down && f.Stun == StunKind.Hitstun
                ? Mathf.Sin(Time.time * 68f) * 0.05f * _flash : 0f;
            _rig.localPosition = new Vector3(shake, (down || loser) ? 0.25f : airY + breathe + crouchY + winBounce, 0f);

            // squash & stretch del salto: estira al despegar, comprime cayendo
            // y aplasta 5f al aterrizar; el volumen se conserva en x/z
            float sy = 1f;
            if (m != null && !stunned && m.HasAir)
            {
                if (phase >= m.AirStart && phase < m.AirEnd)
                    sy = 1f + 0.1f * Mathf.Cos((phase - m.AirStart) / (float)(m.AirEnd - m.AirStart) * Mathf.PI);
                else if (phase >= m.AirEnd && phase < m.AirEnd + 5f)
                    sy = 0.88f + 0.12f * ((phase - m.AirEnd) / 5f);
            }
            float sxz = 1f / Mathf.Sqrt(sy);
            _rig.localScale = new Vector3(sxz, sy, sxz);

            if (winner) armF = new Vector3(0.12f, 1.7f, 0.1f);

            // la sombra vive en el root (no sube con el rig): se achica y
            // desvanece con la altura, se estira con el cuerpo tirado
            if (_shadow != null)
            {
                float h = Mathf.Max(airY, winBounce);
                float k = Mathf.Clamp01(1f - h * 0.38f);
                _shadow.localScale = down || loser
                    ? new Vector3(1.0f, 0.012f, 0.42f)
                    : new Vector3(0.62f * k, 0.012f, 0.42f * k);
                var sc = _shadowR.material.color;
                sc.a = 0.42f * Mathf.Clamp01(1f - h * 0.3f);
                _shadowR.material.color = sc;
            }

            // bloqueo visible: brazos cubriendo (si está en estado de guardia durante ejecución)
            bool blocking = sim.IsBlockingState(_index) && !sim.Over && showBlockPose;
            if (blocking && (m == null || m.Anim == AnimKind.Walk))
            {
                armF = new Vector3(0.1f, 1.3f, 0.4f);
                armB = new Vector3(-0.1f, 1.44f, 0.36f);
            }

            _torso.localRotation = torsoRot;
            _head.localPosition = head;
            _armF.localPosition = armF + new Vector3(0f, 0f, rigZOff);
            _armB.localPosition = armB;
            _legF.localPosition = legF;
            _legB.localPosition = legB;
            _legF.localRotation = legFRot;
            _legB.localRotation = legBRot;

            // ventana de golpe REAL viva y ventana por venir, compartidas por
            // los trails y el tinte de fase (antes los trails usaban
            // Startup/Active genéricos: el salto "brillaba" 28f cuando la
            // patada pega 8)
            bool liveWindow = false, windowPending = false;
            if (m != null && !stunned && m.Hits.Length > 0)
            {
                for (int wi = 0; wi < m.Hits.Length; wi++)
                {
                    var h = m.Hits[wi];
                    if ((f.WindowHit & (1u << wi)) != 0) continue;
                    if (phase >= h.Start && phase < h.Start + h.Duration) { liveWindow = true; break; }
                    if (phase < h.Start) windowPending = true;
                }
            }
            bool armMove = m != null && (m.Anim == AnimKind.AttackA || m.Anim == AnimKind.Dragon || m.Anim == AnimKind.Grab || m.Anim == AnimKind.Fireball);
            if (_trailArm != null)
            {
                _trailArm.emitting = liveWindow && armMove;
                _trailLeg.emitting = liveWindow && !armMove;
            }

            // Tinte de fase en el limb que pega: el mismo lenguaje que la
            // mini-barra de framedata (amarillo = por pegar, rojo = pegando,
            // azul = recovery castigable), llevado al muñeco.
            bool phaseTint = false; var phaseC = Color.white;
            if (m != null && !stunned && m.IsAttack)
            {
                phaseTint = true;
                if (m.Hits.Length > 0)
                    phaseC = liveWindow ? new Color(1f, 0.25f, 0.18f)
                        : windowPending ? new Color(0.95f, 0.8f, 0.2f)
                        : new Color(0.35f, 0.55f, 0.95f);
                else // hadouken: fases S/A/R clásicas
                    phaseC = phase < m.Startup ? new Color(0.95f, 0.8f, 0.2f)
                        : phase < m.Startup + m.Active ? new Color(1f, 0.25f, 0.18f)
                        : new Color(0.35f, 0.55f, 0.95f);
            }

            _flash = Mathf.Max(0f, _flash - dt * 4f);
            for (int i = 0; i < _tintRenderers.Count; i++)
            {
                Color shown = f.Stun == StunKind.Blockstun && stunned ? Color.Lerp(_origColors[i], new Color(0.4f, 0.6f, 1f), 0.4f) : _origColors[i];
                var r = _tintRenderers[i];
                if (phaseTint && (armMove ? (r == _armFR || r == _fistFR) : (r == _legFR || r == _footFR)))
                {
                    var pc = phaseC;
                    pc.a = shown.a; // el ghost conserva su transparencia
                    shown = Color.Lerp(shown, pc, 0.85f);
                }
                shown = Color.Lerp(shown, _flashColor, _flash);
                _tintRenderers[i].material.color = shown;
            }
        }
    }
}
