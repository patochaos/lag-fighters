using UnityEngine;

namespace LagFighter
{
    // El vocabulario de poses de DUELO. Una por VERBO del juego más los
    // estados que quedan: nada de hadouken/shoryuken/tatsu, que eran nombres
    // de otro modo actuando otra cosa.
    public enum DuelPose
    {
        Idle,        // guardia arriba, respirando
        Coiled,      // ya elegiste: congelado a mitad de carga, sin decir el verbo
        StrikeHigh,  // el puño termina ARRIBA de la línea
        StrikeLow,   // barrida al ras: termina ABAJO de la línea
        Grab,        // brazos abiertos y cierre de pinza
        GuardHigh,   // placa sobre la mitad alta
        GuardLow,    // agachado, placa sobre la mitad baja
        Escape,      // paso atrás: este turno no pasa nada
        Hurt,        // comiste el golpe
        GuardBroken, // cubriste la mitad equivocada
        Down,        // en el piso, TODO el turno siguiente
        Win,         // sostiene el golpe que conectó
    }

    // ---- LOS CUERPOS EN DUELO (DUELO-LOOK.md §5) ----
    //
    // El teatro viejo se apagó por la razón correcta: las animaciones heredadas
    // del modo clásico MIENTEN — cuentan una pelea de frames que en DUELO no
    // existe y, peor, en 90 frames vuelven a idle. O sea: cuentan la historia y
    // después la BORRAN. En un juego por turnos la pose que queda ES la
    // información.
    //
    // Acá el cuerpo no es un actor, es el marcador. Contesta tres cosas sin una
    // palabra de texto: qué pasó recién, en qué estado estoy ahora, y si esto
    // pega arriba o abajo. Por eso:
    //   · la LÍNEA ALTO/BAJO está dibujada en el cuerpo, siempre,
    //   · la zona golpeada se ENCIENDE (cabeza+torso o cadera+piernas),
    //   · la guardia es una PLACA sobre la mitad que cubrió — y si erró, se
    //     parte y el golpe pasa por el otro lado,
    //   · y la última pose PERSISTE hasta la próxima revelación.
    public partial class FighterView
    {
        DuelPose _dPose = DuelPose.Idle;
        float _dT;                       // segundos en la pose actual
        float _dSnap;                    // 0..1: cuánto ya se acomodó a la pose
        float _dZoneHi, _dZoneLo;        // encendido de zona (decae solo)
        float _dShield;                  // 0..1 placa de guardia
        bool _dShieldHigh, _dShieldBroken;
        float _dShieldBreak;             // animación de la placa partiéndose
        float _dFace = 1f;               // +1 mira a la derecha, −1 a la izquierda
        Vector3 _dBuild = Vector3.one;   // proporciones del personaje

        Transform _dWaist, _dWaist2, _dZoneHiT, _dZoneLoT, _dPlateT, _dChalkT;
        Renderer _dWaistR, _dWaistR2, _dZoneHiR, _dZoneLoR, _dPlateR, _dChalkR;
        bool _dBuilt;

        // alturas de las dos zonas del cuerpo — el eje del juego, en metros
        const float WaistY = 0.95f;
        const float HeadTop = 1.86f;

        // Los miembros son cajas con el pivote AL MEDIO, así que rotarlas las
        // despega del cuerpo (quedan de tablón cruzado sobre el hombro). Estas
        // dos funciones hacen que la rotación pivotee donde tiene que pivotear:
        // el hombro y la cadera. Ángulo 0 = brazo al frente / pierna al piso;
        // negativo = sube.
        static readonly Vector3 ShoulderF = new Vector3(0.16f, 1.36f, 0.02f);
        static readonly Vector3 ShoulderB = new Vector3(-0.16f, 1.40f, -0.02f);
        static readonly Vector3 HipF = new Vector3(0.10f, 0.95f, 0.03f);
        static readonly Vector3 HipB = new Vector3(-0.10f, 0.95f, -0.03f);
        const float ArmHalf = 0.27f, LegHalf = 0.47f;

        static Vector3 ArmAt(Vector3 shoulder, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return shoulder + new Vector3(0f, -Mathf.Sin(r) * ArmHalf, Mathf.Cos(r) * ArmHalf);
        }

        static Vector3 LegAt(Vector3 hip, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return hip + new Vector3(0f, -Mathf.Cos(r) * LegHalf, -Mathf.Sin(r) * LegHalf);
        }

        public DuelPose Pose => _dPose;

        // ---- construcción perezosa de los adornos de estado ----

        void BuildDuelBits()
        {
            if (_dBuilt) return;
            _dBuilt = true;

            // LA LÍNEA: parte al peleador en ALTO y BAJO. Está siempre, en
            // todas las poses, porque es el eje de adivinanza del juego y hasta
            // ahora no existía en ningún lado del mundo 3D.
            // Son DOS TICKS a los costados, no una barra que cruce el cuerpo:
            // una barra atravesándolo se lee como un palo clavado, y encima
            // tapa justo la zona que después hay que encender.
            for (int s = -1; s <= 1; s += 2)
            {
                var t = VizLib.MakeBox("WaistTick" + s, new Color(0.92f, 0.94f, 1f, 0.22f), transform);
                t.transform.localPosition = new Vector3(s * 0.33f, WaistY, -0.2f);
                t.transform.localScale = new Vector3(0.2f, 0.022f, 0.02f);
                if (s < 0) { _dWaist = t.transform; _dWaistR = t.GetComponent<Renderer>(); }
                else { _dWaist2 = t.transform; _dWaistR2 = t.GetComponent<Renderer>(); }
            }

            var zh = VizLib.MakeBox("ZoneHigh", new Color(1f, 0.23f, 0.19f, 0f), transform);
            zh.transform.localPosition = new Vector3(0f, (WaistY + HeadTop) * 0.5f, -0.02f);
            zh.transform.localScale = new Vector3(0.5f, HeadTop - WaistY, 0.62f);
            _dZoneHiT = zh.transform;
            _dZoneHiR = zh.GetComponent<Renderer>();

            var zl = VizLib.MakeBox("ZoneLow", new Color(1f, 0.23f, 0.19f, 0f), transform);
            zl.transform.localPosition = new Vector3(0f, WaistY * 0.5f, -0.02f);
            zl.transform.localScale = new Vector3(0.5f, WaistY, 0.62f);
            _dZoneLoT = zl.transform;
            _dZoneLoR = zl.GetComponent<Renderer>();

            // LA PLACA de guardia: cubre la mitad que elegiste. Que se vea
            // DÓNDE está la guardia es lo que convierte al mixup de alturas en
            // algo que se aprende mirando.
            // Va DE CANTO y del lado del rival, como un escudo levantado entre
            // los dos: una lámina plana pegada al pecho tapaba al peleador
            // entero y no se leía como guardia, se leía como una cortina.
            var pl = VizLib.MakeBox("GuardPlate", new Color(1f, 0.77f, 0.24f, 0f), transform);
            pl.transform.localScale = new Vector3(0.09f, 0.9f, 0.66f);
            _dPlateT = pl.transform;
            _dPlateR = pl.GetComponent<Renderer>();

            // contorno de tiza: el derribado deja marca en el piso
            var ch = VizLib.MakeBox("Chalk", new Color(1f, 0.23f, 0.19f, 0f), transform);
            ch.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            ch.transform.localScale = new Vector3(1.7f, 0.012f, 0.55f);
            _dChalkT = ch.transform;
            _dChalkR = ch.GetComponent<Renderer>();
        }

        // ---- API que usa MatchController ----

        // ---- identidad de personaje: sale GRATIS ----
        // El rig se construye por código, así que las proporciones por
        // personaje no cuestan un asset. "Estoy peleando contra el Golem" pasa
        // a saberse de un vistazo en vez de leyendo el panel.
        // El pivote del rig está en los pies, así que escalar en Y estira
        // desde el piso y no hay que recolocar nada.
        public void SetDuelBuild(int charIdx)
        {
            BuildDuelBits();
            switch (charIdx)
            {
                case DuelCatalog.JainaIdx:   // la que apuesta: alta y flaca
                    _dBuild = new Vector3(0.80f, 1.13f, 0.80f);
                    break;
                case DuelCatalog.GolemIdx:   // el grappler: bajo, ancho, +8 de vida
                    _dBuild = new Vector3(1.36f, 0.81f, 1.36f);
                    break;
                default:                     // GRAVE: la referencia
                    _dBuild = Vector3.one;
                    break;
            }
        }

        public void SetDuelPose(DuelPose p, bool restart = true)
        {
            BuildDuelBits();
            if (_dPose == p && !restart) return;
            _dPose = p;
            _dT = 0f;
            // los golpes SNAPEAN (el impacto tiene que sentirse); los estados
            // se acomodan más suave
            _dSnap = 0f;
        }

        // La zona golpeada se enciende: rojo si entró, ámbar si la paró la
        // placa. Decae sola — el flash es el evento, la pose es el estado.
        public void FlashZone(bool high, bool blocked = false)
        {
            BuildDuelBits();
            if (high) _dZoneHi = 1f; else _dZoneLo = 1f;
            var c = blocked ? new Color(1f, 0.77f, 0.24f) : new Color(1f, 0.23f, 0.19f);
            if (high) _dZoneHiR.material.color = new Color(c.r, c.g, c.b, 0f);
            else _dZoneLoR.material.color = new Color(c.r, c.g, c.b, 0f);
        }

        public void ShowGuardPlate(bool on, bool high, bool broken)
        {
            BuildDuelBits();
            _dShield = on ? 1f : 0f;
            _dShieldHigh = high;
            _dShieldBroken = broken;
            _dShieldBreak = broken ? 0f : -1f;
        }

        public void ClearDuelMarks()
        {
            BuildDuelBits();
            _dZoneHi = _dZoneLo = 0f;
            _dShield = 0f;
            _dShieldBreak = -1f;
        }

        // Plantar al peleador en su lugar mirando al centro (sin sim de frames).
        public void PlaceDuel(float x)
        {
            BuildDuelBits();
            transform.position = new Vector3(x, 0f, 0f);
            _dFace = x < 0f ? 1f : -1f;
            _faceYaw = x < 0f ? 90f : -90f;
        }

        // ---- el frame ----

        void TickDuel(float dt)
        {
            if (!_dBuilt) BuildDuelBits();
            _dT += dt;
            // snap rápido para los verbos, entrada suave para los estados
            float rate = _dPose == DuelPose.StrikeHigh || _dPose == DuelPose.StrikeLow
                      || _dPose == DuelPose.Grab || _dPose == DuelPose.Hurt ? 26f : 12f;
            _dSnap = Mathf.Lerp(_dSnap, 1f, 1f - Mathf.Exp(-rate * dt));

            ApplyDuelPose(dt);
            TickDuelMarks(dt);
        }

        void ApplyDuelPose(float dt)
        {
            // Los miembros se describen por ÁNGULO desde el hombro/la cadera:
            // 0 = brazo al frente o pierna al piso, negativo = sube. Rotarlos
            // es lo que separa una guardia de un golpe; sin rotación toda pose
            // salía "estirando la mano".
            // Ojo con los ángulos: el brazo es una caja RÍGIDA de 0.55 (no hay
            // codo), así que pasado ~45° el puño termina arriba de la cabeza y
            // el brazo se lee como un tablón cruzado. La guardia creíble con un
            // solo hueso es "manos al frente a la altura de la cara".
            // La diferencia entre GUARDIA y GOLPE con un brazo rígido no está
            // en la altura del puño: está en cuánto se EXTIENDE hacia adelante.
            // Guardia = puños cerca de la cara (ángulo alto, poca z). Golpe =
            // brazo casi horizontal, puño lejos.
            float aF = -52f, aB = -62f;   // guardia por defecto
            float lF = 8f, lB = -8f;      // piernas apenas escalonadas: se ven las dos
            var torsoRot = Quaternion.identity;
            var head = new Vector3(0f, 1.66f, 0f);
            float pitch = 0f, crouch = 0f, zoff = 0f, lie = 0f;
            float breathe = Mathf.Sin(Time.time * 2.2f + _index) * 0.014f;

            switch (_dPose)
            {
                case DuelPose.Idle:
                    break;

                case DuelPose.Coiled:
                    // ya elegiste y falta el rival: cargado hacia atrás, tenso,
                    // sin revelar el verbo
                    aF = -46f; aB = -54f;
                    lF = 14f; lB = -12f;
                    pitch = -9f;
                    crouch = -0.09f;
                    zoff = -0.07f;
                    breathe *= 0.2f;
                    break;

                case DuelPose.StrikeHigh:
                    // el puño termina ARRIBA de la línea: la altura se ve
                    aF = -18f; aB = -74f;
                    lF = -16f; lB = 18f;
                    torsoRot = Quaternion.Euler(0f, 16f, 0f);
                    head += new Vector3(0f, 0.02f, 0.05f);
                    zoff = 0.18f;
                    pitch = 3f;
                    break;

                case DuelPose.StrikeLow:
                    // barrida al ras: la pierna termina ABAJO de la línea
                    crouch = -0.34f;
                    lF = -78f; lB = 22f;
                    aF = -8f; aB = -58f;
                    pitch = 11f;
                    zoff = 0.06f;
                    break;

                case DuelPose.Grab:
                    // los dos brazos al frente, tirándose a buscar el cuerpo
                    aF = 4f; aB = 10f;
                    lF = -10f; lB = 12f;
                    zoff = 0.24f;
                    pitch = 12f;
                    break;

                case DuelPose.GuardHigh:
                    // los dos puños pegados a la cara: nada de extenderse
                    aF = -74f; aB = -80f;
                    pitch = -5f;
                    zoff = -0.07f;
                    break;

                case DuelPose.GuardLow:
                    // agachado, los brazos abajo cubriendo la cadera
                    crouch = -0.33f;
                    aF = 10f; aB = 2f;
                    lF = 26f; lB = -20f;
                    pitch = 7f;
                    break;

                case DuelPose.Escape:
                    // un paso atrás y las manos afuera: no pasa nada este turno
                    aF = -96f; aB = -88f;
                    lF = 20f; lB = -22f;
                    zoff = -0.38f;
                    pitch = -14f;
                    break;

                case DuelPose.Hurt:
                    // la cabeza se va para atrás y los brazos se sueltan
                    aF = -116f; aB = -104f;
                    lF = -18f; lB = 20f;
                    head = new Vector3(0f, 1.66f, -0.17f);
                    pitch = -21f;
                    zoff = -0.20f;
                    break;

                case DuelPose.GuardBroken:
                    // los brazos se van para arriba y afuera: quedaste abierto
                    aF = -134f; aB = -126f;
                    lF = -12f; lB = 14f;
                    pitch = -16f;
                    zoff = -0.13f;
                    break;

                case DuelPose.Down:
                    lie = -84f;
                    lF = 26f; lB = -14f;
                    aF = -44f; aB = 26f;
                    head = new Vector3(0f, 1.70f, -0.06f);
                    break;

                case DuelPose.Win:
                    // sostiene el golpe: el brazo todavía afuera, respirando
                    aF = -22f; aB = -68f;
                    lF = -12f; lB = 14f;
                    torsoRot = Quaternion.Euler(0f, 13f, 0f);
                    zoff = 0.10f;
                    breathe *= 2.2f;
                    break;
            }

            float k = _dSnap;
            var wantRot = Quaternion.Euler(_dPose == DuelPose.Down ? lie : pitch, _faceYaw, 0f);
            _rig.localRotation = Quaternion.Slerp(_rig.localRotation, wantRot, 1f - Mathf.Exp(-14f * dt));
            _rig.localScale = _dBuild;
            _rig.localPosition = Vector3.Lerp(_rig.localPosition,
                new Vector3(0f, (_dPose == DuelPose.Down ? 0.26f : 0f) + crouch + breathe, zoff),
                1f - Mathf.Exp(-14f * dt));

            _torso.localRotation = Quaternion.Slerp(_torso.localRotation, torsoRot, k);
            _head.localPosition = Vector3.Lerp(_head.localPosition, head, k);
            _armF.localPosition = Vector3.Lerp(_armF.localPosition, ArmAt(ShoulderF, aF), k);
            _armB.localPosition = Vector3.Lerp(_armB.localPosition, ArmAt(ShoulderB, aB), k);
            _legF.localPosition = Vector3.Lerp(_legF.localPosition, LegAt(HipF, lF), k);
            _legB.localPosition = Vector3.Lerp(_legB.localPosition, LegAt(HipB, lB), k);
            _armF.localRotation = Quaternion.Slerp(_armF.localRotation, Quaternion.Euler(aF, 0f, 0f), k);
            _armB.localRotation = Quaternion.Slerp(_armB.localRotation, Quaternion.Euler(aB, 0f, 0f), k);
            _legF.localRotation = Quaternion.Slerp(_legF.localRotation, Quaternion.Euler(lF, 0f, 0f), k);
            _legB.localRotation = Quaternion.Slerp(_legB.localRotation, Quaternion.Euler(lB, 0f, 0f), k);

            // tinte: el flash del impacto y nada más. En DUELO el cuerpo no
            // tiene fases de framedata que contar.
            _flash = Mathf.Max(0f, _flash - dt * 3.4f);
            for (int i = 0; i < _tintRenderers.Count; i++)
                _tintRenderers[i].material.color = Color.Lerp(_origColors[i], _flashColor, _flash);

            if (_shadow != null)
            {
                _shadow.localScale = _dPose == DuelPose.Down
                    ? new Vector3(1.5f, 0.012f, 0.5f) : new Vector3(0.66f, 0.012f, 0.44f);
                var sc = _shadowR.material.color;
                sc.a = 0.45f;
                _shadowR.material.color = sc;
            }
        }

        void TickDuelMarks(float dt)
        {
            // zonas: se encienden de golpe y decaen
            _dZoneHi = Mathf.Max(0f, _dZoneHi - dt * 1.5f);
            _dZoneLo = Mathf.Max(0f, _dZoneLo - dt * 1.5f);
            SetAlpha(_dZoneHiR, _dZoneHi * 0.42f);
            SetAlpha(_dZoneLoR, _dZoneLo * 0.42f);

            // los ticks del eje: siempre, pero se apagan si está en el piso
            float wa = _dPose == DuelPose.Down ? 0f : 0.22f;
            SetAlpha(_dWaistR, wa);
            SetAlpha(_dWaistR2, wa);

            // la placa de guardia, y su rotura
            if (_dShieldBreak >= 0f) _dShieldBreak = Mathf.Min(1f, _dShieldBreak + dt * 2.2f);
            float py = _dShieldHigh ? (WaistY + HeadTop) * 0.5f : WaistY * 0.5f;
            float ph = _dShieldHigh ? HeadTop - WaistY : WaistY;
            if (_dShield > 0f)
            {
                // al romperse se cae hacia adelante y se apaga: el golpe pasó
                float br = Mathf.Max(0f, _dShieldBreak);
                _dPlateT.localPosition = new Vector3(_dFace * (0.36f + br * 0.22f), py - br * 0.62f, -0.02f);
                _dPlateT.localRotation = Quaternion.Euler(0f, 0f, _dFace * br * 74f);
                _dPlateT.localScale = new Vector3(0.09f, ph * 0.92f, 0.66f);
                var pc = _dShieldBroken ? new Color(1f, 0.23f, 0.19f) : new Color(1f, 0.77f, 0.24f);
                SetAlpha(_dPlateR, (_dShieldBroken ? 0.75f : 0.7f) * (1f - br * 0.85f), pc);
            }
            else SetAlpha(_dPlateR, 0f);

            SetAlpha(_dChalkR, _dPose == DuelPose.Down ? 0.5f : 0f, new Color(1f, 0.23f, 0.19f));
        }

        static void SetAlpha(Renderer r, float a)
        {
            if (r == null) return;
            var c = r.material.color;
            c.a = a;
            r.material.color = c;
        }

        static void SetAlpha(Renderer r, float a, Color rgb)
        {
            if (r == null) return;
            r.material.color = new Color(rgb.r, rgb.g, rgb.b, a);
        }
    }
}
