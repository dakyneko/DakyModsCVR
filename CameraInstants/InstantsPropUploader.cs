using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CameraInstants;

public class InstantsPropUploader
{
    private static HttpClient GetClient(UploadTask upload)
    {
        var client = new HttpClient();
        var hs = client.DefaultRequestHeaders;
        hs.Add("User-Agent", "ChilloutVR API-Requests");
        hs.Add("Username", upload.username);
        hs.Add("AccessKey", upload.key);
        return client;
    }

    public static async Task<string> NewPropGid(UploadTask upload)
    {
        var client = GetClient(upload);

        // step 1: get new gid
        var req = await client.PutAsync("https://api.abinteractive.net/2/cck/generate/spawnable", null);
        if (req.StatusCode != HttpStatusCode.OK) throw new Exception($"Step 1 API error: {req}");
        var res = await req.Content.ReadAsStringAsync();
        var j = JsonConvert.DeserializeObject(res) as JObject;

        var msg = j.GetValue("Message")?.ToString();
        if (msg != null) MelonLogger.Msg($"API step 1 says: {msg}");
        var gid = (j["Data"] as JObject)?.GetValue("Id")?.ToString();
        if (gid == null) throw new Exception($"API didn't provide a gid");

        return gid;
    }

    private static async Task<JObject> GetCckData(UploadTask upload, string path)
    {
        var client = GetClient(upload);
        var req = await client.GetAsync($"https://api.abinteractive.net/2/cck/{path}");
        if (req.StatusCode != HttpStatusCode.OK) throw new Exception($"API error on cck/{path}: {req}");
        var j = JsonConvert.DeserializeObject(await req.Content.ReadAsStringAsync()) as JObject;
        return j?.GetValue("Data", StringComparison.OrdinalIgnoreCase) as JObject
            ?? throw new Exception($"API didn't provide data for cck/{path}");
    }

    // how many props are uploaded on the account and how many are allowed
    public static async Task<(int used, int limit)> GetPropUsage(UploadTask upload)
    {
        var data = await GetCckData(upload, "userinfo");
        int Spawnables(string field) =>
            (data.GetValue(field, StringComparison.OrdinalIgnoreCase) as JObject)
            ?.GetValue("Spawnables", StringComparison.OrdinalIgnoreCase)?.Value<int>()
            ?? throw new Exception($"API didn't provide {field}.Spawnables");
        return (Spawnables("ContentStats"), Spawnables("Limits"));
    }

    // all props of the account, least recently updated first
    public static async Task<List<(string gid, string description)>> ListMyProps(UploadTask upload)
    {
        var props = new List<(string, string)>();
        // same paging as the CCK: pages start at 0, stop on empty page
        for (int page = 0, totalPages = 0; page <= totalPages; ++page)
        {
            var data = await GetCckData(upload, $"my/spawnables?page={page}&sortAscending=true&order=LastUpdated");
            totalPages = data.GetValue("totalPages", StringComparison.OrdinalIgnoreCase)?.Value<int>() ?? 0;
            var entries = data.GetValue("entries", StringComparison.OrdinalIgnoreCase) as JArray;
            if (entries == null || entries.Count == 0) break;
            foreach (var e in entries.OfType<JObject>())
            {
                var gid = e.GetValue("id", StringComparison.OrdinalIgnoreCase)?.ToString();
                if (gid != null)
                    props.Add((gid, e.GetValue("description", StringComparison.OrdinalIgnoreCase)?.ToString() ?? ""));
            }
        }
        return props;
    }

    public static async Task UploadPropBundle(UploadTask upload)
    {
        var client = GetClient(upload);
        var watch = new Stopwatch();
        watch.Start();
        
        // step 2: get upload location
        var req = await client.GetAsync($"https://api.abinteractive.net/2/cck/contentInfo/Spawnable/{upload.gid}?platform=pc_standalone&region=0");
        if (req.StatusCode != HttpStatusCode.OK) throw new Exception($"Step 2 API error: {req}");
        MelonLogger.Msg($"UploadPropBundle GetAsync ({watch.ElapsedMilliseconds} msec)"); watch.Restart();
        var res = await req.Content.ReadAsStringAsync();
        var j = JsonConvert.DeserializeObject(res) as JObject;
        var msg = j.GetValue("Message")?.ToString();
        if (msg != null) MelonLogger.Msg($"API step 2 says: {msg}");
        var location = (j["Data"] as JObject)?.GetValue("UploadLocation")?.ToString();
        if (location == null) throw new Exception($"API didn't provide a location");

        // step 3: upload
        MelonLogger.Msg($"UploadPropBundle start Step 3 ({watch.ElapsedMilliseconds} msec)"); watch.Restart();
        var hs = client.DefaultRequestHeaders;
        hs.Remove("Username"); hs.Remove("AccessKey"); // part of form below
        using var form = new MultipartFormDataContent();
        // the string fields
        void FormString(string field, string value) =>
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(value)), field);
        FormString("Username", upload.username);
        FormString("AccessKey", upload.key);
        FormString("ContentId", upload.gid);
        FormString("ContentType", "Spawnable");
        FormString("ContentName", upload.propName);
        FormString("ContentDescription", upload.propDesc);
        FormString("ContentChangelog", "new");
        FormString("Platform", "pc_standalone");
        FormString("CompatibilityVersion", "2"); // unity 2021
        FormString("Flag_SetFileAsActive", "1");
        FormString("Flag_OverwritePicture", "1");
        // the file fields
        void FormFile(string field, string path, string filename) =>
            form.Add(new StreamContent(File.OpenRead(path)), field, filename);
        var basename = $"cvrspawnable_{upload.gid}";
        FormFile("AssetFile", upload.bundle, $"{basename}.cvrprop");
        FormFile("AssetThumbnail", upload.thumbnail, $"{basename}.jpg");
        void FormStream(string field, Stream stream, string filename) =>
            form.Add(new StreamContent(stream), field, filename); // TODO: is filename necessary?
        var manifestStream = new MemoryStream(Encoding.ASCII.GetBytes("ManifestFileVersion: 0\nDependencies: []\n"));
        FormStream("AssetManifestFile", manifestStream, $"{basename}.manifest");
        req = await client.PostAsync($"https://{location}/v1/upload-file", form);
        if (req.StatusCode != HttpStatusCode.OK) throw new Exception($"Step 3 API error: {req}");

        MelonLogger.Msg($"UploadPropBundle done {watch.ElapsedMilliseconds} msec)");

        // TODO: step 4: check progress and success with progress-for-file
    }
}
