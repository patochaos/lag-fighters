using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú inicial: Práctica / VS IA / Online. Teclas 1-3, flechas+Enter, o click.
    public class ModeMenuUI : MonoBehaviour
    {
        // Purga de modos (2026-07-20): LAG MODE, IA CUSTOM y YOMI retirados del
        // menú — queda solo NORMAL con 3 opciones. La maquinaria sigue viva:
        // StartMatch acepta lagMode/yomi, y los pasos 3-4 (perfil/dificultad)
        // quedan acá por si IA CUSTOM vuelve. 1v1 LOCAL y POR CÓDIGO ya habían
        // salido el 2026-07-18; TurnCode la usa ONLINE.
        // DUELO (2026-07-25) es EL juego: primera tarjeta, seleccionada por
        // defecto. El resto queda detrás como modos EXPERTO.
        const int DueloIdx = 0;
        const int QuickAIIdx = 2;
        // CARTAS (2026-07-21): la copia de Yomi 2 — mazo, mano y combate por
        // tabla contra la IA. Usa GameMode.VsAI + el flag cards de StartMatch.
        const int CardsIdx = 3;
        static readonly (string label, string desc, GameMode mode)[] Modes =
        {
            ("JUGAR — DUELO", "El juego: una carta secreta por turno. GOLPE gana a AGARRE, AGARRE gana a GUARDIA, GUARDIA gana a GOLPE — y cada golpe es ALTO o BAJO. Siete reglas, se aprende en una partida.", GameMode.VsAI),
            ("PRÁCTICA", "EXPERTO · Solo vos y un dummy quieto. Probá comandos, distancias y framedata.", GameMode.Practice),
            ("VS IA", "EXPERTO · Directo a pelear: la IA adaptativa en dificultad normal planifica en secreto, igual que vos.", GameMode.VsAI),
            ("CARTAS", "EXPERTO · El combate como cartas (copia de Yomi 2): robá, cambiá con el descarte y jugá tu opener contra la IA.", GameMode.VsAI),
            ("ONLINE", "Sala con código de invitación: uno crea, el otro se une. Turnos con timer de 30s.", GameMode.Online),
        };

        static readonly (string label, string desc)[] OnlineOptions =
        {
            ("CREAR SALA", "Te da un código de 4 letras para pasarle a tu rival por donde sea."),
            ("UNIRSE", "Escribí el código que te pasaron y a pelear."),
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
        int _step; // 1 modo, 3 perfil IA, 4 dificultad IA (3-4 hoy inalcanzables),
                   // 5 online (crear/unirse), 6 escribir código, 7 esperando rival
                   // (el 0 era "lag" y el 2 "lado async"; quedaron libres)
        bool _lagChoice; // siempre false desde la purga; queda por si LAG MODE vuelve
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

        // ---- el menú también es la SALA DE ESPERA (DUELO-LOOK.md) ----
        // Era la única pantalla que seguía con el look viejo: panel gris al 55%
        // sobre una foto clara, con Arial y cinco tarjetas iguales. Es lo
        // primero que ve cualquiera que abre el juego.
        readonly Image[] _cardAccent = new Image[6];
        readonly Image[][] _cardBrackets = new Image[6][];
        readonly Text[] _cardKey = new Text[6];
        readonly Text[] _cardTag = new Text[6];
        readonly GameObject[] _cardSil = new GameObject[6];
        Text _title, _expertLbl;

        void Build(RectTransform canvasRt)
        {
            _font = UIFonts.Para;

            _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRt, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = Duelo.Void;
            _root.GetComponent<Image>().raycastTarget = false;

            // La splash deja de ser FONDO y pasa a ser ATMÓSFERA: la pared
            // apenas se adivina detrás del negro. Antes era una foto clara con
            // la UI encima y no se leía nada.
            var splash = Resources.Load<Texture2D>("LagFighter/splash");
            if (splash != null)
            {
                var bgGo = new GameObject("Splash", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.SetParent(rootRt, false);
                bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
                var raw = bgGo.GetComponent<RawImage>();
                raw.texture = splash;
                // muy abajo a propósito: la pared se adivina, no se lee (y el
                // logo de la foto no compite con el título de arriba)
                raw.color = new Color(0.42f, 0.5f, 0.68f, 0.07f);
                raw.raycastTarget = false;
                var fitter = bgGo.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = splash.width / (float)splash.height;
            }

            _title = Txt(rootRt, "Title", "LAG FIGHTERS", new Vector2(0f, 372f), 54, Duelo.Paper, FontStyle.Normal);
            _title.font = UIFonts.Pixel;
            var sub = Txt(rootRt, "Sub", "los dos tiran al mismo tiempo · el juego decide después",
                new Vector2(0f, 318f), 24, Duelo.Alpha(Duelo.Gold, 0.9f), FontStyle.Normal);
            sub.font = UIFonts.Data;

            // en condensada y no en pixel: los títulos de paso llevan acentos
            // ("ELEGÍ RIVAL", "¿CONTRA QUIÉN?") y la pixel los dibuja enanos
            _stepTitle = Txt(rootRt, "Step", "", new Vector2(0f, 254f), 26, Duelo.Mute, FontStyle.Normal);
            _stepTitle.font = UIFonts.Data;

            _expertLbl = Txt(rootRt, "Expert", "", new Vector2(0f, -46f), 17, Duelo.Alpha(Duelo.Mute, 0.75f), FontStyle.Normal);
            _expertLbl.font = UIFonts.Data;

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

                _cardAccent[i] = Img(rt, "Accent", new Vector2(0.5f, 1f), new Vector2(0f, -3f),
                    new Vector2(300f, 6f), Duelo.Line);
                _cardBrackets[i] = Brackets(rt, 300f, 110f, Duelo.Line);
                _cardKey[i] = Txt(rt, "K", (i + 1).ToString(), new Vector2(0f, 0f), 12,
                    Duelo.Alpha(Duelo.Mute, 0.85f), FontStyle.Normal);
                _cardKey[i].font = UIFonts.Pixel;
                _cardTag[i] = Txt(rt, "Tag", "", new Vector2(0f, 0f), 14, Duelo.Alpha(Duelo.Mute, 0.8f), FontStyle.Normal);
                _cardTag[i].font = UIFonts.Data;
                _cardLabels[i] = Txt(rt, "L", "", new Vector2(0f, 0f), 15, Duelo.Paper, FontStyle.Normal);
                _cardLabels[i].font = UIFonts.Pixel;

                var sil = new GameObject("Sil", typeof(RectTransform));
                var srt = sil.GetComponent<RectTransform>();
                srt.SetParent(rt, false);
                srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
                // pivote ABAJO: sin esto el rect queda centrado en el ancla y
                // los pies del muñeco se dibujan medio rect por debajo de la
                // tarjeta (se veían las piernas colgando afuera)
                srt.pivot = new Vector2(0.5f, 0f);
                srt.anchoredPosition = new Vector2(0f, 22f);
                srt.sizeDelta = new Vector2(160f, 200f);
                _cardSil[i] = sil;
                sil.SetActive(false);
            }

            // código de sala grande (escribirlo / mostrarlo mientras esperás)
            _bigCode = Txt(rootRt, "BigCode", "", new Vector2(0f, 30f), 56, Duelo.Vel, FontStyle.Normal);
            _bigCode.font = UIFonts.Pixel;
            _bigCode.gameObject.SetActive(false);

            _desc = Txt(rootRt, "Desc", "", new Vector2(0f, -86f), 22, Duelo.Alpha(Duelo.Paper, 0.92f), FontStyle.Normal);
            _desc.rectTransform.sizeDelta = new Vector2(1180f, 90f);
            _desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var help = Txt(rootRt, "Help", "flechas + ENTER · o el número · click también · ESC vuelve · en partida: R revancha, M acá",
                new Vector2(0f, -330f), 17, Duelo.Alpha(Duelo.Mute, 0.7f), FontStyle.Normal);
            help.font = UIFonts.Data;

            // toggle experimental (tecla C): turno fluido = el último move
            // puede cruzar el límite del turno en vez de entrar completo
            _carryLine = Txt(rootRt, "Carry", "", new Vector2(0f, -372f), 16, Duelo.Mute, FontStyle.Normal);
            _carryLine.font = UIFonts.Data;
        }

        static Image Img(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        // Los brackets de esquina del cromo, pero guardados para poder
        // recolorearlos cuando la tarjeta se selecciona.
        static Image[] Brackets(RectTransform parent, float w, float h, Color c)
        {
            var list = new Image[8];
            const float len = 16f, th = 2f;
            for (int s = 0; s < 4; s++)
            {
                float sx = (s & 1) == 0 ? -1f : 1f;
                float sy = (s & 2) == 0 ? 1f : -1f;
                var a = new Vector2(sx < 0 ? 0f : 1f, sy > 0 ? 1f : 0f);
                list[s * 2] = Img(parent, "BrH" + s, a, new Vector2(sx * -len * 0.5f, sy * -th * 0.5f), new Vector2(len, th), c);
                list[s * 2 + 1] = Img(parent, "BrV" + s, a, new Vector2(sx * -th * 0.5f, sy * -len * 0.5f), new Vector2(th, len), c);
            }
            return list;
        }

        // La silueta del personaje, con SUS proporciones: el Golem bajo y
        // ancho, Jaina alta y flaca. Es la misma información que el rig
        // procedural pone en el escenario, dicha antes de empezar.
        void BuildSilhouette(int card, int charIdx, Color c)
        {
            var host = _cardSil[card];
            for (int k = host.transform.childCount - 1; k >= 0; k--) Destroy(host.transform.GetChild(k).gameObject);
            var rt = host.GetComponent<RectTransform>();
            // (ancho, alto) relativos — los mismos números que SetDuelBuild
            float bw = charIdx == DuelCatalog.GolemIdx ? 1.36f : charIdx == DuelCatalog.JainaIdx ? 0.80f : 1f;
            float bh = charIdx == DuelCatalog.GolemIdx ? 0.81f : charIdx == DuelCatalog.JainaIdx ? 1.13f : 1f;
            float u = 42f;   // unidad de dibujo
            var dark = Duelo.Alpha(c, 0.72f);
            Img(rt, "Head", new Vector2(0.5f, 0f), new Vector2(0f, u * 3.05f * bh), new Vector2(u * 0.62f * bw, u * 0.62f * bh), c);
            Img(rt, "Torso", new Vector2(0.5f, 0f), new Vector2(0f, u * 2.05f * bh), new Vector2(u * 1.05f * bw, u * 1.35f * bh), c);
            Img(rt, "LegL", new Vector2(0.5f, 0f), new Vector2(-u * 0.26f * bw, u * 0.7f * bh), new Vector2(u * 0.36f * bw, u * 1.45f * bh), dark);
            Img(rt, "LegR", new Vector2(0.5f, 0f), new Vector2(u * 0.26f * bw, u * 0.7f * bh), new Vector2(u * 0.36f * bw, u * 1.45f * bh), dark);
            Img(rt, "ArmL", new Vector2(0.5f, 0f), new Vector2(-u * 0.64f * bw, u * 2.2f * bh), new Vector2(u * 0.26f * bw, u * 1.05f * bh), dark);
            Img(rt, "ArmR", new Vector2(0.5f, 0f), new Vector2(u * 0.64f * bw, u * 2.2f * bh), new Vector2(u * 0.26f * bw, u * 1.05f * bh), dark);
        }

        Text _carryLine;

        void RefreshCarryLine()
        {
            // Modo AP (2026-07-20): el turno se presupuesta en ACTION POINTS y
            // el préstamo (overflow) ya es parte del juego — el toggle C del
            // turno fluido quedó absorbido y se oculta.
            if (SimConfig.ApEnabled)
            {
                _carryLine.text = "el turno se juega en ACTION POINTS: cada move cuesta AP, lo que no gastás se guarda y bloquear bien banca +1";
                _carryLine.color = new Color(0.45f, 0.9f, 1f, 0.7f);
                return;
            }
            bool on = SimConfig.CarryoverEnabled;
            _carryLine.text = on
                ? "‹C› TURNO FLUIDO: ON — el último move puede cruzar el turno (quedás comprometido y el rival te ve)"
                : "‹C› TURNO FLUIDO: OFF — los moves tienen que entrar completos en el turno";
            _carryLine.color = on ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.5f);
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

        // CARTAS: elegir personaje (paso 8) — las cartas y los números salen
        // del catálogo real, así el menú nunca miente
        static readonly (string label, string desc)[] DuelChars =
        {
            ("GRAVE", "Controla el espacio. Su Nube Eléctrica es el golpe más rápido del mazo y pega 2 aunque se la defiendan; su Torbellino es ALTO y rápido, para cazar al que se cubre abajo."),
            ("JAINA", "La apuesta. Su Espada del Alba gana casi toda carrera de velocidad, pero si se la defienden te pegan gratis; su Patada Cruzada derriba sin pagar el premio."),
            ("GOLEM", "El grappler. CINCO agarres en 20 cartas y más vida: defenderle sale carísimo, así que hay que pelearle — y su Cabezazo de 9 castiga al que se anima."),
        };

        static readonly (string label, string desc)[] CardChars =
        {
            ("GRAVE", "HP 90 · combo 4 · DOS cambios por turno · la Espada s11 de reversal y la super esquive que devuelve 40."),
            ("JAINA", "HP 85 · combo 5 · agresiva: sangra por cartas (Imprudencia), Arco que castiga y supers baratas."),
        };

        // DUELO: contra quién (paso 10, después de elegir personaje)
        // El rótulo largo ("ONLINE — CREAR SALA") no entraba en la tarjeta y se
        // pisaba con la de al lado: la palabra ONLINE se mudó al tag.
        static readonly (string label, string tag, string desc)[] DuelDest =
        {
            ("VS IA", "", "Contra la máquina, ya mismo. Rounds al mejor de 3, envido y truco incluidos."),
            ("CREAR SALA", "ONLINE", "Sala con código de 4 letras: pasáselo a tu rival y esperá a que se una. Cada uno en su compu."),
            ("UNIRSE", "ONLINE", "Escribí el código que te pasaron. OJO: los dos tienen que entrar por DUELO (no por el ONLINE clásico)."),
        };
        int _duelCharChoice;
        bool _duelOnline;

        int OptionCount => _step == 1 ? Modes.Length :
            _step == 3 ? AIProfiles.Length :
            _step == 4 ? AIDifficulties.Length : _step == 5 ? OnlineOptions.Length :
            _step == 8 ? CardChars.Length :
            _step == 9 ? DuelChars.Length :
            _step == 10 ? DuelDest.Length : 0;

        public void Open()
        {
            _root.SetActive(true);
            _active = true;
            _step = 1;
            _duelOnline = false;
            _lagChoice = false; // NORMAL siempre: LAG MODE quedó fuera del menú
            _sel = Mathf.Clamp(PlayerPrefs.GetInt("lf_menu_mode", 1), 0, Modes.Length - 1); // arranca donde quedaste
            SimConfig.YomiEnabled = false; // el modo YOMI lo prende StartMatch; acá se apaga al volver
            SimConfig.CardsEnabled = false; // ídem CARTAS
            SimConfig.DuelEnabled = false;  // ídem DUELO
            // BUG FIX (2026-07-20): en modo AP el pref viejo del toggle C se
            // ignoraba en la UI pero se seguía CARGANDO — con el toggle
            // prendido de antes, los moves cruzaban el turno "gratis" (el
            // tatsu fantasma sin costo) y los slots comprometidos comían AP.
            SimConfig.CarryoverEnabled = !SimConfig.ApEnabled && PlayerPrefs.GetInt("lf_carryover", 0) == 1;
            RefreshCarryLine();
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
            _stepTitle.text = _step == 1 ? "ELEGÍ RIVAL" :
                              _step == 3 ? "IA CUSTOM — ELEGÍ UN PERFIL" :
                              _step == 4 ? "IA CUSTOM — ELEGÍ DIFICULTAD" :
                              _step == 5 ? "ONLINE — SALA CON CÓDIGO" :
                              _step == 6 ? "ESCRIBÍ EL CÓDIGO DE LA SALA" :
                              _step == 8 ? "CARTAS — ELEGÍ TU PERSONAJE" :
                              _step == 9 ? "DUELO — ELEGÍ TU PERSONAJE" :
                              _step == 10 ? "DUELO — ¿CONTRA QUIÉN?" :
                              "ESPERANDO AL RIVAL…";

            _bigCode.gameObject.SetActive(_step >= 6);
            // la línea de ACTION POINTS es del modo clásico: no tiene por qué
            // estar en la portada. Aparece solo si estás mirando un EXPERTO.
            if (_carryLine != null) _carryLine.gameObject.SetActive(_step == 1 && _sel != DueloIdx);
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
            // JERARQUÍA (DUELO.md §5): DUELO es EL juego y el resto son modos
            // EXPERTO. Hasta ahora eran cinco tarjetas idénticas en fila, o sea
            // que la pantalla decía "acá hay cinco juegos, elegí uno".
            bool modeStep = _step == 1;
            bool chars = _step == 9;
            _expertLbl.text = modeStep ? "MÁS MODOS  ·  EXPERTO" : "";

            float cardW = count >= 4 ? 300f : 330f;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i < count;
                _cards[i].gameObject.SetActive(on);
                if (!on) continue;

                Vector2 size, pos;
                if (modeStep && i == DueloIdx)          { size = new Vector2(760f, 188f); pos = new Vector2(0f, 118f); }
                else if (modeStep)                      { size = new Vector2(272f, 104f); pos = new Vector2((i - 1 - (count - 2) * 0.5f) * 286f, -124f); }
                else if (chars)                         { size = new Vector2(300f, 260f); pos = new Vector2((i - (count - 1) * 0.5f) * 320f, 46f); }
                else if (count > 4)                     { size = new Vector2(280f, 100f); pos = new Vector2(((i % 3) - 1) * cardW, 55f - (i / 3) * 115f); }
                else                                    { size = new Vector2(330f, 118f); pos = new Vector2((i - (count - 1) * 0.5f) * 344f, 30f); }

                var rt = _cards[i].rectTransform;
                rt.sizeDelta = size;
                rt.anchoredPosition = pos;
                _cardAccent[i].rectTransform.sizeDelta = new Vector2(size.x, 6f);
                LayoutBrackets(i, size);

                bool big = modeStep && i == DueloIdx;
                _cardLabels[i].text = modeStep ? Modes[i].label :
                    _step == 3 ? AIProfiles[i].label :
                    _step == 5 ? OnlineOptions[i].label :
                    _step == 8 ? CardChars[i].label :
                    chars ? DuelChars[i].label :
                    _step == 10 ? DuelDest[i].label : AIDifficulties[i].label;
                _cardLabels[i].fontSize = big ? 40 : chars ? 22 : count >= 4 ? 16 : 22;
                _cardLabels[i].rectTransform.anchoredPosition =
                    new Vector2(0f, big ? 34f : chars ? 96f : 0f);

                // ningún rótulo puede desbordar su tarjeta: se achica hasta
                // entrar (así fue como "ONLINE — CREAR SALA" terminó pisando a
                // la tarjeta de al lado)
                _cardLabels[i].resizeTextForBestFit = true;
                _cardLabels[i].resizeTextMinSize = 10;
                _cardLabels[i].resizeTextMaxSize = _cardLabels[i].fontSize;
                _cardLabels[i].rectTransform.sizeDelta = new Vector2(size.x - 26f, big ? 56f : 34f);

                // el rótulo EXPERTO vive en la tarjeta, no en la descripción
                _cardTag[i].text = big ? "el juego"
                    : modeStep && i != DueloIdx ? "EXPERTO"
                    : _step == 10 ? DuelDest[i].tag : "";
                _cardTag[i].rectTransform.anchoredPosition = new Vector2(0f, big ? -18f : chars ? -34f : -32f);
                _cardTag[i].fontSize = big ? 22 : 13;

                _cardKey[i].text = (i + 1).ToString();
                _cardKey[i].rectTransform.anchoredPosition = new Vector2(size.x * 0.5f - 18f, size.y * 0.5f - 16f);

                bool sil = chars;
                if (_cardSil[i].activeSelf != sil) _cardSil[i].SetActive(sil);
                if (sil) BuildSilhouette(i, i, Duelo.P1);
            }
            _desc.rectTransform.anchoredPosition = new Vector2(0f, modeStep ? -228f : count > 4 ? -145f : chars ? -166f : -110f);
            if (count > 0) Highlight(_sel);
        }

        void LayoutBrackets(int card, Vector2 size)
        {
            var b = _cardBrackets[card];
            const float len = 16f, th = 2f;
            for (int s = 0; s < 4; s++)
            {
                float sx = (s & 1) == 0 ? -1f : 1f;
                float sy = (s & 2) == 0 ? 1f : -1f;
                var a = new Vector2(sx < 0 ? 0f : 1f, sy > 0 ? 1f : 0f);
                b[s * 2].rectTransform.anchorMin = b[s * 2].rectTransform.anchorMax = a;
                b[s * 2].rectTransform.anchoredPosition = new Vector2(sx * -len * 0.5f, sy * -th * 0.5f);
                b[s * 2 + 1].rectTransform.anchorMin = b[s * 2 + 1].rectTransform.anchorMax = a;
                b[s * 2 + 1].rectTransform.anchoredPosition = new Vector2(sx * -th * 0.5f, sy * -len * 0.5f);
            }
        }

        void Highlight(int idx)
        {
            _sel = Mathf.Clamp(idx, 0, OptionCount - 1);
            for (int i = 0; i < OptionCount; i++)
            {
                bool sel = i == _sel;
                // DUELO se acentúa en DORADO (es la ceremonia, el juego); el
                // resto en el celeste del lado propio. Sin seleccionar, la
                // tarjeta es cromo puro y no compite.
                var acc = _step == 1 && i == DueloIdx ? Duelo.Gold : Duelo.P1;
                _cards[i].color = sel ? Duelo.Stage : Duelo.Panel;
                _cardAccent[i].color = sel ? acc : Duelo.Line;
                _cardLabels[i].color = sel ? Duelo.Paper : Duelo.Alpha(Duelo.Paper, 0.62f);
                _cardTag[i].color = sel ? Duelo.Alpha(acc, 0.9f) : Duelo.Alpha(Duelo.Mute, 0.7f);
                var bc = sel ? Duelo.Alpha(acc, 0.9f) : Duelo.Alpha(Duelo.Line, 0.8f);
                foreach (var b in _cardBrackets[i]) b.color = bc;
                _cards[i].rectTransform.localScale = Vector3.one * (sel ? 1.03f : 1f);
            }
            if (_carryLine != null) _carryLine.gameObject.SetActive(_step == 1 && _sel != DueloIdx);
            _desc.text = _step == 1 ? Modes[_sel].desc :
                _step == 3 ? AIProfiles[_sel].desc :
                _step == 5 ? OnlineOptions[_sel].desc :
                _step == 8 ? CardChars[_sel].desc :
                _step == 9 ? DuelChars[_sel].desc :
                _step == 10 ? DuelDest[_sel].desc : AIDifficulties[_sel].desc;
        }

        void Confirm(int idx)
        {
            SfxLib.Play(SfxLib.Kind.UiClick, 0.8f);
            if (_step == 1)
            {
                PlayerPrefs.SetInt("lf_menu_mode", idx);
                if (idx == DueloIdx) // DUELO: primero elegí tu personaje
                {
                    _step = 9;
                    _sel = Mathf.Clamp(PlayerPrefs.GetInt("lf_menu_duelchar", 0), 0, DuelChars.Length - 1);
                    Layout();
                    return;
                }
                if (idx == QuickAIIdx) // VS IA directo: adaptativa en normal, a pelear
                {
                    _mc.StartMatch(GameMode.VsAI, _lagChoice, 0, AIProfile.Adaptive, AIDifficulty.Normal);
                    return;
                }
                if (idx == CardsIdx) // CARTAS: primero elegí tu personaje
                {
                    _step = 8;
                    _sel = Mathf.Clamp(PlayerPrefs.GetInt("lf_menu_cardchar", 0), 0, CardChars.Length - 1);
                    Layout();
                    return;
                }
                if (Modes[idx].mode == GameMode.Online)
                {
                    _duelOnline = false;
                    _step = 5;
                    _sel = 0;
                    Layout();
                    return;
                }
                _mc.StartMatch(Modes[idx].mode, _lagChoice);
                return;
            }
            if (_step == 5)
            {
                if (idx == 0) CrearSala();
                else // unirse
                {
                    _codeInput = "";
                    _step = 6;
                    Layout();
                }
                return;
            }
            if (_step == 9) // DUELO: personaje elegido → ¿contra quién?
            {
                PlayerPrefs.SetInt("lf_menu_duelchar", idx);
                _duelCharChoice = idx;
                _step = 10;
                _sel = 0;
                Layout();
                return;
            }
            if (_step == 10) // DUELO: vs IA u online
            {
                if (idx == 0)
                {
                    _mc.StartMatch(GameMode.VsAI, false, 0, AIProfile.Adaptive, AIDifficulty.Normal,
                        yomi: false, cards: false, cardsChar: 0, duel: true, duelChar: _duelCharChoice);
                    return;
                }
                _duelOnline = true;
                if (idx == 1) { CrearSala(); return; }
                _codeInput = "";
                _step = 6;
                Layout();
                return;
            }
            if (_step == 8) // CARTAS: personaje elegido, a jugar
            {
                PlayerPrefs.SetInt("lf_menu_cardchar", idx);
                _mc.StartMatch(GameMode.VsAI, false, 0, AIProfile.Adaptive, AIDifficulty.Normal,
                    yomi: false, cards: true, cardsChar: idx);
                return;
            }
            if (_step == 3)
            {
                _aiProfileChoice = AIProfiles[idx].profile;
                PlayerPrefs.SetInt("lf_menu_profile", idx);
                _step = 4;
                _sel = Mathf.Clamp(PlayerPrefs.GetInt("lf_menu_diff", 1), 0, AIDifficulties.Length - 1);
                Layout();
                return;
            }
            PlayerPrefs.SetInt("lf_menu_diff", idx);
            _mc.StartMatch(GameMode.VsAI, _lagChoice, 0, _aiProfileChoice, AIDifficulties[idx].difficulty);
        }

        // Crear sala online: la usan el ONLINE clásico (paso 5) y el DUELO
        // online (paso 10). Al unirse el rival arranca el modo que toque.
        void CrearSala()
        {
            _desc.text = "creando sala…";
            NetLobby.I.CreateRoom(_duelOnline ? false : _lagChoice,
                code =>
                {
                    if (!_active) return;
                    _step = 7;
                    Layout();
                    NetLobby.I.WaitForGuest(() =>
                    {
                        if (!_active || _step != 7) return;
                        if (_duelOnline)
                            _mc.StartMatch(GameMode.Online, false, 0, AIProfile.Adaptive, AIDifficulty.Normal,
                                yomi: false, cards: false, cardsChar: 0, duel: true, duelChar: _duelCharChoice);
                        else
                            _mc.StartMatch(GameMode.Online, _lagChoice, 0);
                    });
                },
                err => { if (_active) _desc.text = err; });
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
                if (GameInput.CancelPressed()) { _step = _duelOnline ? 10 : 5; _sel = 1; Layout(); return; }
                if (_codeInput.Length == 4 && c == '\0' && (GameInput.ConfirmPressed() || GameInput.EndTurnPressed()))
                {
                    _desc.text = $"uniéndome a {_codeInput}…";
                    NetLobby.I.JoinRoom(_codeInput,
                        lagMode =>
                        {
                            if (!_active) return;
                            if (_duelOnline)
                                _mc.StartMatch(GameMode.Online, false, 1, AIProfile.Adaptive, AIDifficulty.Normal,
                                    yomi: false, cards: false, cardsChar: 0, duel: true, duelChar: _duelCharChoice);
                            else
                                _mc.StartMatch(GameMode.Online, lagMode, 1);
                        },
                        err => { if (_active && _step == 6) _desc.text = err; });
                }
                return;
            }
            if (_step == 7)
            {
                if (GameInput.CancelPressed())
                {
                    NetLobby.I.Leave();
                    _step = _duelOnline ? 10 : 5;
                    _sel = 0;
                    Layout();
                }
                return;
            }

            // C conmuta el turno fluido mientras elegís lag/modo
            // (en modo AP no hay toggle: el préstamo ya es parte del juego)
            if (!SimConfig.ApEnabled && _step <= 1 && GameInput.LetterPressed() == 'C')
            {
                SimConfig.CarryoverEnabled = !SimConfig.CarryoverEnabled;
                PlayerPrefs.SetInt("lf_carryover", SimConfig.CarryoverEnabled ? 1 : 0);
                RefreshCarryLine();
                SfxLib.Play(SfxLib.Kind.UiClick, 0.6f);
            }

            // ESC vuelve un paso atrás (en el paso 1 no hay adónde)
            if (GameInput.CancelPressed() && _step > 1)
            {
                SfxLib.Play(SfxLib.Kind.UiCancel, 0.6f);
                if (_step == 4) // dificultad → perfil
                {
                    _step = 3;
                    _sel = Mathf.Clamp(PlayerPrefs.GetInt("lf_menu_profile", 0), 0, AIProfiles.Length - 1);
                }
                else if (_step == 10) // duelo destino → personaje
                {
                    _duelOnline = false;
                    _step = 9;
                    _sel = Mathf.Clamp(PlayerPrefs.GetInt("lf_menu_duelchar", 0), 0, DuelChars.Length - 1);
                }
                else // perfil / online → elegir rival
                {
                    _step = 1;
                    _sel = Mathf.Clamp(PlayerPrefs.GetInt("lf_menu_mode", 1), 0, Modes.Length - 1);
                }
                Layout();
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
                    if (i != _sel) { Highlight(i); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
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

            // En el paso 1 la grilla es en DOS pisos (DUELO arriba solo, los
            // EXPERTO abajo en fila), así que arriba/abajo saltan de piso y
            // izquierda/derecha se mueven dentro del de abajo.
            if (_step == 1)
            {
                if (GameInput.UpPressed() && _sel != DueloIdx) { Highlight(DueloIdx); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
                if (GameInput.DownPressed() && _sel == DueloIdx) { Highlight(1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
                if (GameInput.LeftPressed()) { Highlight(_sel <= 1 ? DueloIdx : _sel - 1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
                if (GameInput.RightPressed()) { Highlight(_sel == DueloIdx ? 1 : _sel + 1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            }
            else
            {
                if (GameInput.LeftPressed()) { Highlight(_sel - 1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
                if (GameInput.RightPressed()) { Highlight(_sel + 1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
                if (OptionCount > 4 && GameInput.UpPressed()) { Highlight(_sel - 3); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
                if (OptionCount > 4 && GameInput.DownPressed()) { Highlight(_sel + 3); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            }
            int n = GameInput.NumberPressed();
            if (n >= 1 && n <= OptionCount) { Confirm(n - 1); return; }
            if (GameInput.ConfirmPressed()) Confirm(_sel);
        }
    }
}
