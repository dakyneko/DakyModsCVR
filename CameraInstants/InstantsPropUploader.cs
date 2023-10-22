using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
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

        var msg = j.GetValue("message")?.ToString();
        if (msg != null) MelonLogger.Msg($"API step 1 says: {msg}");
        var gid = (j["data"] as JObject)?.GetValue("id")?.ToString();
        if (gid == null) throw new Exception($"API didn't provide a gid");

        return gid;
    }

    public static async Task UploadPropBundle(UploadTask upload)
    {
        var client = GetClient(upload);
        var watch = new Stopwatch();
        watch.Start();
        
        // step 2: get upload location
        var req = await client.GetAsync($"https://api.abinteractive.net/2/cck/contentInfo/Spawnable/{upload.gid}?platform=pc_standalone&region=0");
        if (req.StatusCode != HttpStatusCode.OK) throw new Exception($"Step 2 API error: {req}");
        MelonLogger.Msg($"UploadPropBundle GetAsync {watch.ElapsedMilliseconds} msec)"); watch.Restart();
        var res = await req.Content.ReadAsStringAsync();
        var j = JsonConvert.DeserializeObject(res) as JObject;
        var msg = j.GetValue("message")?.ToString();
        if (msg != null) MelonLogger.Msg($"API step 2 says: {msg}");
        var location = (j["data"] as JObject)?.GetValue("uploadLocation")?.ToString();
        if (location == null) throw new Exception($"API didn't provide a location");

        // step 3: upload
        MelonLogger.Msg($"UploadPropBundle start Step 3 {watch.ElapsedMilliseconds} msec)"); watch.Restart();
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
