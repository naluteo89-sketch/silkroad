using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace Silkroad
{
    /// <summary>
    /// 태블릿 안의 아주 작은 웹서버. 저장한 그림을 같은 Wi-Fi 의 휴대폰에서 QR 로 받아 가게 함.
    ///   http://태블릿IP:포트/s/{id}   → 그림 + '이미지 저장' 버튼 페이지
    ///   http://태블릿IP:포트/i/{id}.png → 그림 (보기)
    ///   http://태블릿IP:포트/d/{id}.png → 그림 (다운로드)
    /// 외부 서버를 쓰지 않으며, 앱이 켜져 있는 동안만 열립니다. 공유 파일은 keepHours 지나면 자동 삭제.
    /// </summary>
    public class ShareServer : MonoBehaviour
    {
        public int port = 8080;
        [Tooltip("공유 파일 보관 시간 (지나면 앱 시작 때 삭제)")]
        public float keepHours = 24f;
        public string pageTitle = "실크로드 그림 공방";

        static ShareServer _instance;
        public static ShareServer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ShareServer>();
                    if (_instance == null) _instance = new GameObject("ShareServer").AddComponent<ShareServer>();
                }
                return _instance;
            }
        }

        string _folder;
        TcpListener _listener;
        Thread _thread;
        volatile bool _running;
        int _activePort;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _folder = Path.Combine(Application.persistentDataPath, "Share");
            Directory.CreateDirectory(_folder);
            CleanupOld();
        }

        /// <summary>그림을 공유 폴더에 넣고 휴대폰용 주소를 돌려줌. 네트워크가 없으면 null (error 에 이유).</summary>
        public string Publish(byte[] png, out string error)
        {
            error = null;
            if (!EnsureStarted(out error)) return null;
            string ip = LocalIPv4();
            if (ip == null) { error = "Wi-Fi 에 연결되어 있지 않아요"; return null; }
            string id = DateTime.Now.ToString("MMddHHmmss") + UnityEngine.Random.Range(1000, 9999);
            File.WriteAllBytes(Path.Combine(_folder, id + ".png"), png);
            return "http://" + ip + ":" + _activePort + "/s/" + id;
        }

        bool EnsureStarted(out string error)
        {
            error = null;
            if (_running) return true;
            for (int p = port; p < port + 10; p++) // 포트가 사용 중이면 다음 번호
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Any, p);
                    _listener.Start();
                    _activePort = p;
                    _running = true;
                    _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "SilkroadShare" };
                    _thread.Start();
                    Debug.Log("[Silkroad] 공유 서버 시작: 포트 " + p);
                    return true;
                }
                catch (Exception e) { error = e.Message; _listener = null; }
            }
            error = "공유 서버를 시작하지 못했어요: " + error;
            return false;
        }

        void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => Handle(client));
                }
                catch (Exception) { if (!_running) return; Thread.Sleep(200); }
            }
        }

        static readonly Regex Route = new Regex(@"^/(s|i|d)/([A-Za-z0-9_]{4,40})(\.png)?$");

        void Handle(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 15000;
                    var stream = client.GetStream();
                    string path = ReadRequestPath(stream);
                    if (path == null) return;
                    int q = path.IndexOf('?');
                    if (q >= 0) path = path.Substring(0, q);
                    var m = Route.Match(path);
                    if (!m.Success) { Send(stream, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("없는 주소예요")); return; }
                    string kind = m.Groups[1].Value, id = m.Groups[2].Value;
                    string file = Path.Combine(_folder, id + ".png");
                    if (!File.Exists(file)) { Send(stream, 404, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Page(null))); return; }
                    if (kind == "s") Send(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Page(id)));
                    else Send(stream, 200, "image/png", File.ReadAllBytes(file),
                        kind == "d" ? "Content-Disposition: attachment; filename=\"silkroad_" + id + ".png\"\r\n" : "");
                }
                catch (Exception) { /* 휴대폰이 중간에 끊어도 무시 */ }
            }
        }

        static string ReadRequestPath(NetworkStream s)
        {
            var buf = new byte[8192];
            int len = 0;
            while (len < buf.Length)
            {
                int n = s.Read(buf, len, buf.Length - len);
                if (n <= 0) break;
                len += n;
                string sofar = Encoding.ASCII.GetString(buf, 0, len);
                if (sofar.Contains("\r\n\r\n") || sofar.Contains("\n\n")) break;
            }
            string req = Encoding.ASCII.GetString(buf, 0, len);
            var parts = req.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0].Split(' ');
            if (parts.Length < 2 || (parts[0] != "GET" && parts[0] != "HEAD")) return null;
            return parts[1];
        }

        static void Send(NetworkStream s, int code, string type, byte[] body, string extraHeaders = "")
        {
            string status = code == 200 ? "200 OK" : "404 Not Found";
            string head = "HTTP/1.1 " + status + "\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length +
                          "\r\nCache-Control: no-store\r\n" + extraHeaders + "Connection: close\r\n\r\n";
            var h = Encoding.ASCII.GetBytes(head);
            s.Write(h, 0, h.Length);
            s.Write(body, 0, body.Length);
            s.Flush();
        }

        string Page(string id)
        {
            string css = "body{margin:0;background:#2b2118;color:#f3e6c8;font-family:-apple-system,sans-serif;text-align:center}" +
                         "img{max-width:100%;height:auto;display:block;margin:0 auto}" +
                         "a{display:inline-block;margin:18px auto 8px;padding:14px 28px;background:#b8862b;color:#fff;border-radius:28px;text-decoration:none;font-size:18px}" +
                         "p{margin:6px 16px 24px;font-size:15px;opacity:.85}h1{font-size:20px;margin:18px 16px}";
            string body = id == null
                ? "<h1>그림을 찾을 수 없어요</h1><p>보관 기간이 지났거나 주소가 잘못되었어요.</p>"
                : "<h1>" + WebUtility.HtmlEncode(pageTitle) + "</h1><img src=\"/i/" + id + ".png\" alt=\"그림\">" +
                  "<a href=\"/d/" + id + ".png\" download=\"silkroad_" + id + ".png\">이미지 저장</a>" +
                  "<p>버튼이 안 되면 그림을 길게 눌러 저장하세요.</p>";
            return "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                   "<title>" + WebUtility.HtmlEncode(pageTitle) + "</title><style>" + css + "</style></head><body>" + body + "</body></html>";
        }

        /// <summary>태블릿의 Wi-Fi(사설망) IPv4 주소.</summary>
        public static string LocalIPv4()
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("8.8.8.8", 65530); // 실제로 보내지 않음, 경로만 확인
                    var ip = (s.LocalEndPoint as IPEndPoint)?.Address;
                    if (ip != null && !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any)) return ip.ToString();
                }
            }
            catch { }
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                            return ua.Address.ToString();
                }
            }
            catch { }
            return null;
        }

        void CleanupOld()
        {
            try
            {
                foreach (var f in Directory.GetFiles(_folder, "*.png"))
                    if ((DateTime.Now - File.GetLastWriteTime(f)).TotalHours > keepHours) File.Delete(f);
            }
            catch { }
        }

        void OnDestroy() => Stop();
        void OnApplicationQuit() => Stop();

        void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }
    }
}
