using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú inicial por pasos:
    //  1) NORMAL o LAG MODE (cada 3 turnos el lag sube 50%)
    //  2) Práctica / VS IA / 1v1 local / POR CÓDIGO
    // VS IA agrega perfil y dificultad. Teclas 1-6, flechas+Enter, o click.
    public class ModeMenuUI : MonoBehaviour
    {
        static readonly (string label, string desc)[] LagOptions =
        {
            ("NORMAL", "Turnos de 60 frames, parejos toda la pelea."),
            ("LAG MODE", "Cada 3 turnos el lag sube 50%: 60 → 90 → 135 → 202 → 303 frames. It gets laggier."),
        };

        static readonly (string label, string desc, GameMode mode)[] Modes =
        {
            ("PRÁCTICA", "Solo vos y un dummy quieto. Probá comandos, distancias y framedata.", GameMode.Practice),
            ("VS IA", "La CPU planifica su turno en secreto, igual que vos.", GameMode.VsAI),
            ("1v1 LOCAL", "Misma PC: planifica J1, pantalla de 'pasá el teclado', planifica J2.", GameMode.PvP),
            ("POR CÓDIGO", "Pelea por chat: cada turno intercambian un código corto y ambos ven la misma pelea. Sin servidores.", GameMode.Async),
            ("ONLINE", "Sala con código de invitación: uno crea, el otro se une. Turnos con timer de 30s.", GameMode.Online),
        };

        static readonly (string label, string desc)[] OnlineOptions =
        {
            ("CREAR SALA", "Te da un código de 4 letras para pasarle a tu rival por donde sea."),
            ("UNIRSE", "Escribí el código que te pasaron y a pelear."),
        };

        static readonly (string label, string desc)[] Sides =
        {
            ("SOY JUGADOR 1", "El de la izquierda (azul). Arreglen quién es quién antes de empezar."),
            ("SOY JUGADOR 2", "El de la derecha (naranja)."),
        };

        static readonly (string label, string desc, AIProfile profile)[] AIProfiles =
        {
            ("RANDOM", "Elige un perfil al empezar la partida y lo mantiene durante todos los rounds.", AIProfile.Random),
            ("ZONER", "Controla distancia con Hadouken, retroceso y anti-air.", AIProfile.Zoner),
            ("AGGRESSIVE", "Cierra espacio, presiona y mezcla golpes con agarres.", AIProfile.Aggressive),
            ("DEFENSIVE", "Prioriza spacing, castigos, anti-air y Parry.", AIProfile.Defensive),
            ("TRICKSTER", "Usa baits, cambios de ritmo, saltos y agarres inesperados.", AIProfile.Trickster),
            ("ADAPTIVE", "Estudia tus planes revelados y contrarresta tus hábitos en turnos futuros.", AIProfile.Adaptive),
        };

        static readonly (string label, string desc, AIDifficulty difficulty)[] AIDifficulties =
        {
            ("FÁCIL", "Deja parte del turno libre y se desvía seguido de su estrategia.", AIDifficulty.Easy),
            ("NORMAL", "Ejecuta su perfil con errores ocasionales.", AIDifficulty.Normal),
            ("DIFÍCIL", "Aprovecha todo el turno, comete pocos errores y reacciona mejor al estado visible.", AIDifficulty.Hard),
        };

        MatchController _mc;
        Font _font;
        GameObject _root;
        Image[] _cards;
        Text[] _cardLabels;
        Text _desc, _stepTitle;
        int _sel;
        int _step; // 0 lag, 1 modo, 2 lado async, 3 perfil IA, 4 dificultad IA,
                   // 5 online (crear/unirse), 6 escribir código, 7 esperando rival
        bool _lagChoice;
        AIProfile _aiProfileChoice = AIProfile.Random;
        bool _active;
        Vector2 _lastMouse;
        string _codeInput = "";
        Text _bigCode;

        public static ModeMenuUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.ModeMenu");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var ui = go.AddComponent<ModeMenuUI>();
            ui._mc = mc;
            ui.Build(go.GetComponent<RectTransform>());
            ui._root.SetActive(false);
            return ui;
        }

        void Build(RectTransform canvasRt)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRt, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            _root.GetComponent<Image>().raycastTarget = false;

            // splash art de fondo (si está importada) + announcer
            var splash = Resources.Load<Texture2D>("LagFighter/splash");
            if (splash != null)
            {
                var bgGo = new GameObject("Splash", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.SetParent(rootRt, false);
                bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
                var raw = bgGo.GetComponent<RawImage>();
                raw.texture = splash;
                raw.raycastTarget = false;
                var fitter = bgGo.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = splash.width / (float)splash.height;
                bgGo.transform.SetAsFirstSibling(); // detrás de todo lo demás… pero delante del velo oscuro
                _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            }
            bool hasSplash = splash != null;
            if (!hasSplash)
            {
                Txt(rootRt, "Title", "LAG FIGHTERS", new Vector2(0f, 230f), 78, Color.white, FontStyle.Bold);
                Txt(rootRt, "Sub", "programá tu turno · ejecución simultánea · footsies puro", new Vector2(0f, 166f), 21, new Color(1f, 0.9f, 0.4f), FontStyle.Normal);
            }

            // banda oscura detrás de la parte interactiva (la splash es clara)
            var band = new GameObject("Band", typeof(RectTransform), typeof(Image));
            var bandRt = band.GetComponent<RectTransform>();
            bandRt.SetParent(rootRt, false);
            bandRt.anchorMin = bandRt.anchorMax = new Vector2(0.5f, 0.5f);
            bandRt.anchoredPosition = new Vector2(0f, -10f);
            bandRt.sizeDelta = new Vector2(1160f, 500f);
            band.GetComponent<Image>().color = new Color(0f, 0f, 0f, hasSplash ? 0.62f : 0.25f);
            band.GetComponent<Image>().raycastTarget = false;

            _stepTitle = Txt(rootRt, "Step", "", new Vector2(0f, 175f), 15, new Color(1f, 1f, 1f, 0.85f), FontStyle.Normal);
            _stepTitle.font = UIFonts.Pixel;

            // Perfil IA usa seis cartas en una grilla 3x2.
            _cards = new Image[6];
            _cardLabels = new Text[6];
            for (int i = 0; i < _cards.Length; i++)
            {
                var card = new GameObject("Card" + i, typeof(RectTransform), typeof(Image));
                var rt = card.GetComponent<RectTransform>();
                rt.SetParent(rootRt, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(300f, 110f);
                _cards[i] = card.GetComponent<Image>();
                _cards[i].raycastTarget = false;

                var k = Txt(rt, "K", (i + 1).ToString(), new Vector2(-118f, 32f), 12, new Color(1f, 1f, 1f, 0.5f), FontStyle.Normal);
                k.font = UIFonts.Pixel;
                _cardLabels[i] = Txt(rt, "L", "", new Vector2(0f, 0f), 15, Color.white, FontStyle.Normal);
                _cardLabels[i].font = UIFonts.Pixel;
            }

            // código de sala grande (escribirlo / mostrarlo mientras esperás)
            _bigCode = Txt(rootRt, "BigCode", "", new Vector2(0f, 30f), 40, new Color(0.5f, 0.95f, 1f), FontStyle.Normal);
            _bigCode.font = UIFonts.Pixel;
            _bigCode.gameObject.SetActive(false);

            _desc = Txt(rootRt, "Desc", "", new Vector2(0f, -86f), 20, new Color(1f, 1f, 1f, 0.85f), FontStyle.Normal);
            Txt(rootRt, "Help", "1-6 o flechas + Enter · click también funciona · en partida: R reinicia, M vuelve acá",
                new Vector2(0f, -210f), 16, new Color(1f, 1f, 1f, 0.5f), FontStyle.Normal);
        }

        Text Txt(RectTransform parent, string name, string content, Vector2 pos, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1400f, 90f);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        int OptionCount => _step == 0 ? LagOptions.Length : _step == 1 ? Modes.Length :
            _step == 2 ? Sides.Length : _step == 3 ? AIProfiles.Length :
            _step == 4 ? AIDifficulties.Length : _step == 5 ? OnlineOptions.Length : 0;

        public void Open()
        {
            _root.SetActive(true);
            _active = true;
            _step = 0;
            _sel = 0;
            Layout();
        }

        public void Close()
        {
            _active = false;
            if (_root != null) _root.SetActive(false);
        }

        void Layout()
        {
            int count = OptionCount;
            _stepTitle.text = _step == 0 ? "¿CUÁNTO LAG QUERÉS?" :
                              _step == 1 ? (_lagChoice ? "LAG MODE — elegí rival" : "NORMAL — elegí rival") :
                              _step == 2 ? "POR CÓDIGO — ¿de qué lado jugás?" :
                              _step == 3 ? "VS IA — ELEGÍ UN PERFIL" :
                              _step == 4 ? "VS IA — ELEGÍ DIFICULTAD" :
                              _step == 5 ? "ONLINE — SALA CON CÓDIGO" :
                              _step == 6 ? "ESCRIBÍ EL CÓDIGO DE LA SALA" :
                              "ESPERANDO AL RIVAL…";

            _bigCode.gameObject.SetActive(_step >= 6);
            if (_step == 6)
            {
                _bigCode.text = _codeInput.PadRight(4, '_');
                _desc.text = "letras A-Z · Enter confirma · Escape vuelve";
            }
            else if (_step == 7)
            {
                _bigCode.text = NetLobby.I.Room;
                _desc.text = "pasale este código a tu rival · la sala espera hasta que se una · Escape cancela";
            }
            float cardW = count >= 4 ? 300f : 330f;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i < count;
                _cards[i].gameObject.SetActive(on);
                if (!on) continue;
                bool grid = count > 4;
                int col = grid ? i % 3 : i;
                int row = grid ? i / 3 : 0;
                float x = grid ? (col - 1) * cardW : (i - (count - 1) * 0.5f) * cardW;
                float y = grid ? 55f - row * 115f : 10f;
                _cards[i].rectTransform.sizeDelta = grid ? new Vector2(280f, 100f) : new Vector2(300f, 110f);
                _cards[i].rectTransform.anchoredPosition = new Vector2(x, y);
                _cardLabels[i].text = _step == 0 ? LagOptions[i].label : _step == 1 ? Modes[i].label :
                    _step == 2 ? Sides[i].label : _step == 3 ? AIProfiles[i].label : AIDifficulties[i].label;
                _cardLabels[i].fontSize = count >= 4 ? 23 : 28;
            }
            _desc.rectTransform.anchoredPosition = new Vector2(0f, count > 4 ? -145f : -86f);
            if (count > 0) Highlight(_sel);
        }

        void Highlight(int idx)
        {
            _sel = Mathf.Clamp(idx, 0, OptionCount - 1);
            for (int i = 0; i < OptionCount; i++)
            {
                bool lagCard = _step == 0 && i == 1;
                _cards[i].color = i == _sel
                    ? (lagCard ? new Color(0.6f, 0.25f, 0.15f, 0.98f) : new Color(0.25f, 0.42f, 0.62f, 0.98f))
                    : new Color(0.12f, 0.13f, 0.17f, 0.9f);
            }
            _desc.text = _step == 0 ? LagOptions[_sel].desc : _step == 1 ? Modes[_sel].desc :
                _step == 2 ? Sides[_sel].desc : _step == 3 ? AIProfiles[_sel].desc : AIDifficulties[_sel].desc;
        }

        void Confirm(int idx)
        {
            if (_step == 0)
            {
                _lagChoice = idx == 1;
                _step = 1;
                _sel = 1; // VS IA por defecto
                Layout();
                return;
            }
            if (_step == 1)
            {
                if (Modes[idx].mode == GameMode.VsAI)
                {
                    _step = 3;
                    _sel = 0; // RANDOM por defecto
                    Layout();
                    return;
                }
                if (Modes[idx].mode == GameMode.Async)
                {
                    _step = 2;
                    _sel = 0;
                    Layout();
                    return;
                }
                if (Modes[idx].mode == GameMode.Online)
                {
                    _step = 5;
                    _sel = 0;
                    Layout();
                    return;
                }
                _mc.StartMatch(Modes[idx].mode, _lagChoice);
                return;
            }
            if (_step == 2)
            {
                _mc.StartMatch(GameMode.Async, _lagChoice, idx);
                return;
            }
            if (_step == 5)
            {
                if (idx == 0) // crear sala
                {
                    _desc.text = "creando sala…";
                    NetLobby.I.CreateRoom(_lagChoice,
                        code =>
                        {
                            if (!_active) return;
                            _step = 7;
                            Layout();
                            NetLobby.I.WaitForGuest(() =>
                            {
                                if (_active && _step == 7)
                                    _mc.StartMatch(GameMode.Online, _lagChoice, 0);
                            });
                        },
                        err => { if (_active) _desc.text = err; });
                }
                else // unirse
                {
                    _codeInput = "";
                    _step = 6;
                    Layout();
                }
                return;
            }
            if (_step == 3)
            {
                _aiProfileChoice = AIProfiles[idx].profile;
                _step = 4;
                _sel = 1; // NORMAL por defecto
                Layout();
                return;
            }
            _mc.StartMatch(GameMode.VsAI, _lagChoice, 0, _aiProfileChoice, AIDifficulties[idx].difficulty);
        }

        void Update()
        {
            if (!_active) return;

            // pasos online sin cartas: escribir código / esperar rival
            if (_step == 6)
            {
                char c = GameInput.LetterPressed();
                if (c != '\0' && _codeInput.Length < 4) { _codeInput += c; Layout(); }
                if (GameInput.UndoPressed() && _codeInput.Length > 0)
                {
                    _codeInput = _codeInput.Substring(0, _codeInput.Length - 1);
                    Layout();
                }
                if (GameInput.CancelPressed()) { _step = 5; _sel = 1; Layout(); return; }
                if (_codeInput.Length == 4 && c == '\0' && (GameInput.ConfirmPressed() || GameInput.EndTurnPressed()))
                {
                    _desc.text = $"uniéndome a {_codeInput}…";
                    NetLobby.I.JoinRoom(_codeInput,
                        lagMode => { if (_active) _mc.StartMatch(GameMode.Online, lagMode, 1); },
                        err => { if (_active && _step == 6) _desc.text = err; });
                }
                return;
            }
            if (_step == 7)
            {
                if (GameInput.CancelPressed())
                {
                    NetLobby.I.Leave();
                    _step = 5;
                    _sel = 0;
                    Layout();
                }
                return;
            }

            // hover muestra la descripción de cada opción; click la confirma
            var mp = GameInput.MousePos();
            if ((mp - _lastMouse).sqrMagnitude > 4f)
            {
                _lastMouse = mp;
                for (int i = 0; i < OptionCount; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cards[i].rectTransform, mp, null)) continue;
                    if (i != _sel) Highlight(i);
                    break;
                }
            }

            if (GameInput.ClickPressed())
            {
                var pos = GameInput.MousePos();
                for (int i = 0; i < OptionCount; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cards[i].rectTransform, pos, null)) continue;
                    Confirm(i);
                    return;
                }
            }

            if (GameInput.LeftPressed()) Highlight(_sel - 1);
            if (GameInput.RightPressed()) Highlight(_sel + 1);
            if (_step == 3 && GameInput.UpPressed()) Highlight(_sel - 3);
            if (_step == 3 && GameInput.DownPressed()) Highlight(_sel + 3);
            int n = GameInput.NumberPressed();
            if (n >= 1 && n <= OptionCount) { Confirm(n - 1); return; }
            if (GameInput.ConfirmPressed()) Confirm(_sel);
        }
    }
}
