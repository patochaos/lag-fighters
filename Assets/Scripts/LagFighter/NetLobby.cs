using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LagFighter
{
    // Lobby online por código de invitación sobre Supabase REST.
    // El servidor es un relay tonto: guarda TurnCodes y estado de sala; la
    // sim determinista garantiza que ambos clientes vean la misma pelea.
    // Sin cuentas ni matchmaking: creás sala → código de 4 letras → el otro
    // se une. Como es por turnos, polling HTTP cada ~1.5s alcanza de sobra.
    public class NetLobby : MonoBehaviour
    {
        const string Url = "https://cuzbxgescjztzmgoofwr.supabase.co/rest/v1";
        const string Key = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImN1emJ4Z2VzY2p6dHptZ29vZndyIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODQwNDgxMjQsImV4cCI6MjA5OTYyNDEyNH0.paK5oZTuFlLaylPxFqAqMM-ysojfdXwYyemRrau7EDc";
        const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ"; // sin I/L/O: se dictan por voz sin confusión
        const float PollSeconds = 1.5f;

        static NetLobby _inst;
        public static NetLobby I
        {
            get
            {
                if (_inst == null)
                {
                    var go = new GameObject("LagFighter.NetLobby");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<NetLobby>();
                }
                return _inst;
            }
        }

        public string Room { get; private set; } = "";
        public int Generation { get; private set; } // invalida polls viejos al salir de una sala

        public void Leave()
        {
            Room = "";
            Generation++;
            StopAllCoroutines();
        }

        // ---------- sala ----------

        public void CreateRoom(bool lagMode, Action<string> onOk, Action<string> onError)
        {
            StartCoroutine(CreateRoomCo(lagMode, onOk, onError));
        }

        IEnumerator CreateRoomCo(bool lagMode, Action<string> onOk, Action<string> onError)
        {
            // limpieza oportunista de salas viejas (best effort, no bloquea)
            using (var clean = Req("POST", $"{Url}/rpc/lf_cleanup", "{}"))
                yield return clean.SendWebRequest();

            for (int attempt = 0; attempt < 4; attempt++)
            {
                string code = RandomCode();
                using var req = Req("POST", $"{Url}/lf_rooms",
                    $"{{\"code\":\"{code}\",\"lag_mode\":{(lagMode ? "true" : "false")}}}");
                yield return req.SendWebRequest();
                if (req.responseCode == 201)
                {
                    Room = code;
                    Generation++;
                    onOk(code);
                    yield break;
                }
                if (req.responseCode != 409) // 409 = código repetido: probar otro
                {
                    onError($"no pude crear la sala ({req.responseCode})");
                    yield break;
                }
            }
            onError("no pude crear la sala (códigos agotados, raro)");
        }

        public void WaitForGuest(Action onJoined) => StartCoroutine(WaitForGuestCo(onJoined));

        IEnumerator WaitForGuestCo(Action onJoined)
        {
            int gen = Generation;
            string room = Room;
            while (gen == Generation)
            {
                using (var req = Req("GET", $"{Url}/lf_rooms?code=eq.{room}&select=guest_joined"))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success &&
                        req.downloadHandler.text.Contains("\"guest_joined\":true"))
                    {
                        if (gen == Generation) onJoined();
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(PollSeconds);
            }
        }

        public void JoinRoom(string code, Action<bool> onOk, Action<string> onError)
        {
            StartCoroutine(JoinRoomCo(code.ToUpperInvariant(), onOk, onError));
        }

        IEnumerator JoinRoomCo(string code, Action<bool> onOk, Action<string> onError)
        {
            using var get = Req("GET", $"{Url}/lf_rooms?code=eq.{code}&select=lag_mode,guest_joined");
            yield return get.SendWebRequest();
            string body = get.downloadHandler != null ? get.downloadHandler.text : "";
            if (get.result != UnityWebRequest.Result.Success || body.Length < 3)
            {
                onError("sala no encontrada (¿el código está bien?)");
                yield break;
            }
            if (body.Contains("\"guest_joined\":true"))
            {
                onError("esa sala ya está llena");
                yield break;
            }
            bool lag = body.Contains("\"lag_mode\":true");

            using var patch = Req("PATCH", $"{Url}/lf_rooms?code=eq.{code}", "{\"guest_joined\":true}");
            yield return patch.SendWebRequest();
            if (patch.result != UnityWebRequest.Result.Success)
            {
                onError($"no pude unirme ({patch.responseCode})");
                yield break;
            }
            Room = code;
            Generation++;
            onOk(lag);
        }

        // ---------- turnos ----------

        public void PushTurn(int seq, int side, string payload)
        {
            StartCoroutine(PushTurnCo(seq, side, payload));
        }

        IEnumerator PushTurnCo(int seq, int side, string payload)
        {
            // reintenta hasta que entre: sin este turno la pelea no avanza
            for (int attempt = 0; attempt < 30; attempt++)
            {
                using var req = Req("POST", $"{Url}/lf_turns",
                    $"{{\"room\":\"{Room}\",\"seq\":{seq},\"side\":{side},\"payload\":\"{payload}\"}}");
                yield return req.SendWebRequest();
                if (req.responseCode == 201 || req.responseCode == 409) yield break; // 409 = ya estaba, ok
                yield return new WaitForSecondsRealtime(2f);
            }
        }

        public void PollTurn(int seq, int side, Action<string> onPayload)
        {
            StartCoroutine(PollTurnCo(seq, side, onPayload));
        }

        IEnumerator PollTurnCo(int seq, int side, Action<string> onPayload)
        {
            int gen = Generation;
            string room = Room;
            while (gen == Generation)
            {
                using (var req = Req("GET", $"{Url}/lf_turns?room=eq.{room}&seq=eq.{seq}&side=eq.{side}&select=payload"))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        string payload = ExtractPayload(req.downloadHandler.text);
                        if (payload != null)
                        {
                            if (gen == Generation) onPayload(payload);
                            yield break;
                        }
                    }
                }
                yield return new WaitForSecondsRealtime(PollSeconds);
            }
        }

        // ---------- helpers ----------

        // El payload es base64 (sin caracteres escapables en JSON): parseo a mano
        // y me ahorro una lib entera. [{"payload":"LFxxxx"}]
        static string ExtractPayload(string json)
        {
            const string tag = "\"payload\":\"";
            int i = json.IndexOf(tag, StringComparison.Ordinal);
            if (i < 0) return null;
            int start = i + tag.Length;
            int end = json.IndexOf('"', start);
            return end < 0 ? null : json.Substring(start, end - start);
        }

        string RandomCode()
        {
            var sb = new StringBuilder(4);
            for (int i = 0; i < 4; i++)
                sb.Append(Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)]);
            return sb.ToString();
        }

        static UnityWebRequest Req(string method, string url, string body = null)
        {
            var req = new UnityWebRequest(url, method);
            req.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.SetRequestHeader("apikey", Key);
            req.SetRequestHeader("Authorization", $"Bearer {Key}");
            req.SetRequestHeader("Prefer", "return=minimal");
            req.timeout = 10;
            return req;
        }
    }
}
