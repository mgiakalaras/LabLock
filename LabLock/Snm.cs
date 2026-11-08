using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace LabLock
{
    /// <summary>
    /// The link to School Network Manager (SNM). After every Apply, Undo or Restore, LabLock tells SNM what is
    /// locked on this PC: each setting with its state, and when LabLock applied it. SNM finds the PC by its MAC
    /// address, IP address or name, the same way its agent does, and shows it on the PC's page.
    /// Settings in LabLock.ini: snm=http://192.168.2.114:5055 and snmkey= (the agent key: SNM, Tools > Computer agent).
    /// Without them nothing is sent, so LabLock works the same in a school without SNM.
    /// </summary>
    internal static class Snm
    {
        private const int TimeoutMs = 6000;

        public static string Url(AppConfig config)
        {
            string url = config.Get("snm", "").Trim().TrimEnd('/');
            if (url.Length > 0 && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
            }
            return url;
        }

        public static bool Configured(AppConfig config)
        {
            return Url(config).Length > 0 && config.Get("snmkey", "").Trim().Length > 0;
        }

        public static string LabLockVersion
        {
            get
            {
                System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        /// <summary>
        /// Sends the state of this PC to SNM. Never throws: if SNM is off or not set, it only writes a line in the log.
        /// eventName is apply, undo, restore, board or check.
        /// </summary>
        public static bool Report(AppConfig config, RunContext c, string eventName, Action<string> log)
        {
            if (!Configured(config)) return false;
            try
            {
                string body = BuildReport(c, eventName);
                string answer = Post(Url(config) + "/api/agent/lablock", config.Get("snmkey", "").Trim(), body);
                string message = JsonText(answer, "message");
                log("School Network Manager: " + (message.Length > 0 ? message : "OK"));
                return true;
            }
            catch (WebException ex)
            {
                string detail = ex.Message;
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    string message = "";
                    try
                    {
                        using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        {
                            message = JsonText(reader.ReadToEnd(), "message");
                        }
                    }
                    catch (Exception)
                    {
                    }
                    if ((int)response.StatusCode == 401) detail = "the key (snmkey) is wrong";
                    else if (message.Length > 0) detail = message;
                }
                log("WARNING: School Network Manager did not take the report: " + detail);
                return false;
            }
            catch (Exception ex)
            {
                log("WARNING: School Network Manager did not take the report: " + ex.Message);
                return false;
            }
        }

        /// <summary>A quick test of the address and the key, for the settings window.</summary>
        public static string Test(string url, string key)
        {
            AppConfig probe = AppConfig.Load();
            probe.Set("snm", url);
            probe.Set("snmkey", key);
            string full = Url(probe);
            try
            {
                Post(full + "/api/agent/lablock", key.Trim(), "{\"test\":true}");
                return null;
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null && (int)response.StatusCode == 401) return "The key is wrong. Copy it from SNM: Tools > Computer agent > Show.";
                if (response != null && (int)response.StatusCode == 404) return "School Network Manager answered, but it is an older version without the LabLock link (update it to 2.17 or newer).";
                return "School Network Manager did not answer at " + full + ": " + ex.Message;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string BuildReport(RunContext c, string eventName)
        {
            List<Setting> all = Catalog.Build();
            Dictionary<string, string> states = Engine.States(all, c);
            List<string> recorded = Journal.Ids(c);

            StringBuilder json = new StringBuilder();
            json.Append("{");
            Field(json, "labLockVersion", LabLockVersion);
            Field(json, "event", eventName);
            Field(json, "hostname", Environment.MachineName);
            Field(json, "student", c.Student != null ? c.Student.Name : "");

            List<string> ips = new List<string>();
            List<string> macs = new List<string>();
            try
            {
                NetInfo net = NetConfig.Current();
                if (net != null)
                {
                    if (net.Address.Length > 0) ips.Add(net.Address);
                    if (net.Mac.Length > 0) macs.Add(net.Mac);
                }
            }
            catch (Exception)
            {
            }
            json.Append("\"ips\":").Append(JsonArray(ips)).Append(",");
            json.Append("\"macs\":").Append(JsonArray(macs)).Append(",");

            json.Append("\"items\":[");
            bool first = true;
            foreach (Setting setting in all)
            {
                string state;
                if (!states.TryGetValue(setting.Id, out state)) state = "";
                if (!first) json.Append(",");
                first = false;
                json.Append("{");
                Field(json, "id", setting.Id);
                Field(json, "group", L.ToGreek(setting.Group));
                Field(json, "title", L.ToGreek(setting.Title));
                Field(json, "state", L.ToGreek(state));
                Field(json, "kind", Kind(state));
                json.Append("\"recorded\":").Append(Quote(recorded.Contains(setting.Id) ? Journal.When(c, setting.Id) : ""));
                json.Append("}");
            }
            json.Append("]}");
            return json.ToString();
        }

        /// <summary>applied, partly, off or unknown, so SNM can colour the line without reading Greek.</summary>
        private static string Kind(string state)
        {
            if (state.StartsWith("Applied", StringComparison.Ordinal)) return "applied";
            if (state.StartsWith("Partly", StringComparison.Ordinal)) return "partly";
            if (state.StartsWith("Not applied", StringComparison.Ordinal)) return "off";
            return "unknown";
        }

        private static string Post(string url, string key, string body)
        {
            byte[] data = Encoding.UTF8.GetBytes(body);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["X-Agent-Key"] = key;
            request.Timeout = TimeoutMs;
            request.ReadWriteTimeout = TimeoutMs;
            request.ContentLength = data.Length;
            request.Proxy = null;   // the school server is on the local network
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        // ---- a little JSON, enough for this report (the .NET Framework has no JSON writer built in)

        private static void Field(StringBuilder json, string name, string value)
        {
            json.Append(Quote(name)).Append(":").Append(Quote(value)).Append(",");
        }

        private static string JsonArray(List<string> values)
        {
            StringBuilder text = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) text.Append(",");
                text.Append(Quote(values[i]));
            }
            return text.Append("]").ToString();
        }

        private static string Quote(string value)
        {
            StringBuilder text = new StringBuilder("\"");
            foreach (char ch in value ?? "")
            {
                switch (ch)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (ch < ' ') text.Append("\\u").Append(((int)ch).ToString("x4"));
                        else text.Append(ch);
                        break;
                }
            }
            return text.Append("\"").ToString();
        }

        /// <summary>Reads one text field ("name":"value") from a small JSON answer.</summary>
        private static string JsonText(string json, string name)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string marker = "\"" + name + "\":\"";
            int start = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return "";
            start += marker.Length;
            StringBuilder value = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '"') break;
                if (ch == '\\' && i + 1 < json.Length)
                {
                    i++;
                    char next = json[i];
                    if (next == 'u' && i + 4 < json.Length)
                    {
                        value.Append((char)Convert.ToInt32(json.Substring(i + 1, 4), 16));
                        i += 4;
                    }
                    else if (next == 'n') value.Append(' ');
                    else value.Append(next);
                    continue;
                }
                value.Append(ch);
            }
            return value.ToString();
        }
    }
}
