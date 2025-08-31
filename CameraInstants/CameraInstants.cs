using ABI.CCK.Components;
using ABI_RC.Core.Player;
using ABI_RC.Core.IO.FileDragDrop;
using Daky;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Bitmap = System.Drawing.Bitmap;
using LfsApi = LagFreeScreenshots.API.LfsApi;
using PortableCamera = ABI_RC.Systems.Camera.PortableCamera;

[assembly: MelonGame(null, "ChilloutVR")]
[assembly: MelonInfo(typeof(CameraInstants.CameraInstantsMod), "CameraInstants", "2.0.5", "daky", "https://github.com/dakyneko/DakyModsCVR")]
[assembly:MelonAdditionalDependencies("LagFreeScreenshots")]
[assembly:MelonOptionalDependencies("libwebpwrapper",
    // just to silent MelonLoader warnings, those are dependencies of AssetsTools, it works anyway
    "AssetRipper.TextureDecoder", "System.Half")]

namespace CameraInstants;

public class CameraInstantsMod : MelonMod
{
    private static MelonLogger.Instance logger;
    private MelonPreferences_Entry<bool> myInstantsEnabled, captureAutoPropUpload, autoSpawnProp;
    private MelonPreferences_Entry<float> autoSpawnPropSize;
    private MelonPreferences_Entry<string> uploadUsername, uploadKey;
    private MelonPreferences_Entry<int> uploadMaxSize;
    private Queue<string> autoSpawnPropsGids = new();
    private static bool isWebPInstalled = false;
    private static FileDragDropListener? FileDragDropListener;

    public override void OnInitializeMelon()
    {
        logger = LoggerInstance;

        var category = MelonPreferences.CreateCategory("CameraInstants", "CameraInstants");
        myInstantsEnabled = category.CreateEntry("InstantsEnabled", true, "Spawn instants locally", "When shooting with the camera, spawn the image in the world (local only)");
        captureAutoPropUpload = category.CreateEntry("CaptureAutoPropUpload", false, "Instants props", "When shooting with the camera, spawn the image in the world (for everybody). This builds a prop and upload it to CVR (requires upload username and key).");
        autoSpawnProp = category.CreateEntry("AutoSpawnProp", false, "Spawn instant props", "Spawn instants props automatically");
        autoSpawnPropSize = category.CreateEntry("AutoSpawnPropSize", 0.6f, "Size of Instant props", "Maximum length (width or height) in game dimension");
        uploadUsername = category.CreateEntry("UploadUserName", "", "CCK Username", "Necessary for instants props");
        uploadKey = category.CreateEntry("UploadKey", "", "CCK Key", "Necessary for instants props");
        uploadMaxSize = category.CreateEntry("UploadMaxSize", -1, "Max upload size", "Resize the largest dimension of the image before upload (0 or negative means disabled)");
        // TODO: should listen to events on myInstantsEnabled change and add/rem listener instead
        LfsApi.OnScreenshotTexture += OnScreenshotTexture;
        LfsApi.OnScreenshotSavedV2 += OnScreenshotSaved;

        // TODO: support piles of pictures (multiple stacked on each other)
        // either we can spread them or put them between them, latest should be always on top?
        // TODO: can add options to camera settings panel
        // - spawn position: top, bottom, left, right
        // - spawn size, transparency, resolution
        // - allowed action like delete
        // TODO: implement delete on disk (move into trash bin)

        FileDragDropListener = new FileDragDropListener
        {
            Priority = -1,
            AcceptingDrops = true,
            ValidExtensions = {
                "jpg", "jpeg", "jpe", "jif", "jfif", // who came up with so many?!
                "png",
                "bmp",
                "webp",
            },
            OnFileDrop = OnDropFile,
        };
        FileDragDropHandler.AddListener(FileDragDropListener);

        isWebPInstalled = LagFreeScreenshots.WebpUtils.IsWebpSupported();

        // Check for BTKUILib and add settings UI
        if (RegisteredMelons.Any(m => m.Info.Name == "BTKUILib"))
            Daky.DakyBTKUI.AutoGenerateCategory(category);
    }

    private void CheckUploadConfiguration()
    {
        if (uploadUsername.Value == "" || uploadKey.Value == "")
            throw new Exception($"Auto prop upload cannot work without CCK settings");
    }

    private UploadTask CreateUploadTask(string? propName = null)
    {
        return new UploadTask()
        {
            propName = propName ?? ("Picture " + DateTime.Now.ToString("dd MMM, HH:mm:ss")),
            propDesc = "unattended instants upload",
            username = uploadUsername.Value,
            key = uploadKey.Value,
        };
    }

    private void OnDropFile(String filepath)
    {
        var ext = Path.GetExtension(filepath);
        logger.Msg($"OnDropFile start AsyncPropUploader {filepath} (extension: {ext})");
        var propName = Path.ChangeExtension(Path.GetFileName(filepath), "");
        CheckUploadConfiguration();
        Task.Run(() => AutoPropTask(filepath, propName));
    }

    private void OnScreenshotTexture(RenderTexture rtex)
    {
        var portableCamera = PortableCamera.Instance;
        if (!myInstantsEnabled.Value || portableCamera == null) return;

        // let's downscale the instants texture to save memory
        var aspectRatio = 1f * rtex.height / rtex.width;
        int w = 640; // TODO: make this a setting
        int h = Mathf.FloorToInt(w * aspectRatio);
        var rtex2 = RenderTexture.GetTemporary(w, h, rtex.depth, rtex.format);
        RenderTexture.active = rtex2;
        GL.sRGBWrite = true; // needed to keep colors+brightness
        Graphics.Blit(rtex, rtex2);

        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false); // we're restricted to RGBA because GPU = RenderTexture are stuck with RGBA
        tex.filterMode = FilterMode.Bilinear;
        Graphics.CopyTexture(rtex2, tex);
        RenderTexture.ReleaseTemporary(rtex2);
        RenderTexture.active = null;

        var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plane.SetActive(false);
        var t = plane.transform;
        t.SetParent(portableCamera.transform.parent, false); // to CVR Camera 2.0
        t.localPosition = 150 * Vector3.left;
        t.localRotation = Quaternion.Euler(0, 0, 180);
        t.localScale = new Vector3(140f, 140f * aspectRatio, 1f);

        // make it double sided because easy to lose it in the world
        var backside = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var t2 = backside.transform;
        t2.SetParent(t, false);
        t2.localRotation = Quaternion.Euler(0, 180, 0); // backside

        var m = new Material(Shader.Find("Unlit/Texture"));
        m.mainTexture = tex;
        plane.GetComponent<Renderer>().material = m;
        backside.GetComponent<Renderer>().material = m;

        plane.name = "CameraInstants";
        backside.name = "back";
        plane.layer = LayerMask.NameToLayer("UI");
        backside.GetComponent<Collider>().enabled = false;

        var body = plane.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        var pickup = plane.AddComponent<CVRPickupObject>();
        pickup.gripType = CVRPickupObject.GripType.Free;
        bool grabbed = false;
        pickup.onDrop.AddListener((_) => grabbed = false);
        pickup.onGrab.AddListener((_) =>
        {
            t.SetParent(null, true);
            grabbed = true;
        });

        var interactable = plane.AddComponent<CVRInteractable>();
        interactable.actions = new() {
            new() {
                actionType = CVRInteractableAction.ActionRegister.OnInteractDown,
                execType = CVRInteractableAction.ExecutionType.LocalNotNetworked,
                operations = new() {
                    new CVRInteractableActionOperation {
                        type = CVRInteractableActionOperation.ActionType.MethodCall,
                        gameObjectVal = plane,
                        customEvent = UnityEventWithAction(() => {
                            if (grabbed)
                                GameObject.Destroy(plane);
                        }),
                    },
                },
            }
        };

        plane.SetActive(true);
    }

    private static UnityEvent UnityEventWithAction(UnityAction f)
    {
        var ev = new UnityEvent();
        ev.AddListener(f);
        return ev;
    }

    private void OnScreenshotSaved(string filepath, int width, int height, LagFreeScreenshots.API.MetadataV2? metadata) {
        if (!captureAutoPropUpload.Value) return;

        logger.Msg($"OnScreenshotSaved start AutoPropTask {filepath}");
        CheckUploadConfiguration();
        Task.Run(() => AutoPropTask(filepath));
    }

    private async Task AutoPropTask(string imagePath, string? propName = null)
    {
        try { await AutoPropTask_(imagePath, propName); }
        catch (Exception e) { logger.Error($"Error in AutoPropTask: {e}"); }
    }

    private async Task AutoPropTask_(string imagePath, string? propName = null)
    {
        var watch = new Stopwatch();
        watch.Start();
        logger.Msg($"AutoPropTask starting");
        var upload = CreateUploadTask(propName);
        upload.gid = await InstantsPropUploader.NewPropGid(upload);
        logger.Msg($"Requested gid in {watch.ElapsedMilliseconds} msec"); watch.Restart();

        var ns = this.GetType().Namespace + ".Resources";
        var templateBundle = Dakytils.StreamFromAssembly(ns, "cvrspawnable_00000000-0000-0000-0000-000000000000.cvrprop");
        if (templateBundle == null) throw new Exception($"Missing bundle template");

        using var bitmap_orig = imagePath.EndsWith(".webp") ?
            LoadWebP(imagePath) :
            new Bitmap(imagePath);
        using var bitmap = (uploadMaxSize.Value > 0) ?
            InstantsPropBuilder.ResizeImage(bitmap_orig, uploadMaxSize.Value) :
            bitmap_orig;
        if (bitmap != bitmap_orig)
            bitmap_orig.Dispose(); // dispose as soon as possible
        // TODO: should resize the image so it uploads faster (CVR API is slow)
        logger.Msg($"Loaded template and image in {watch.ElapsedMilliseconds} msec"); watch.Restart();
        var thumbnail = InstantsPropBuilder.MakeThumbnail(bitmap, ImageFormat.Jpeg);
        logger.Msg($"Prepared thumbnail in {watch.ElapsedMilliseconds} msec"); watch.Restart();
        var bundle = InstantsPropBuilder.Build(templateBundle, bitmap, upload.gid, propSize: autoSpawnPropSize.Value);
        bitmap.Dispose();

        logger.Msg($"Prepared prop bundle in {watch.ElapsedMilliseconds} msec"); watch.Restart();
        upload.bundle = bundle;
        upload.thumbnail = thumbnail;
        await InstantsPropUploader.UploadPropBundle(upload);
        File.Delete(upload.bundle); // cleaning up
        File.Delete(upload.thumbnail);

        if (autoSpawnProp.Value)
            autoSpawnPropsGids.Enqueue(upload.gid); // queue prop for spawning
        logger.Msg($"Done upload in {watch.ElapsedMilliseconds} msec");
    }

    // WebPWrapper may not be installed, so we need to isolate it
    private static Bitmap LoadWebP(string imagePath) => new WebPWrapper.WebP().Load(imagePath);

    public override void OnUpdate()
    {
        if (!autoSpawnProp.Value) return;
        if (autoSpawnPropsGids.Count == 0) return;

        var gid = autoSpawnPropsGids.Dequeue();
        logger.Msg($"Spawning auto-uploaded image prop: {gid}");
        PlayerSetup.Instance.DropProp(gid);
    }
}

public class OnApplicationFocusCallback : MonoBehaviour
{
    public Action<bool> onFocus = null;

    private void OnApplicationFocus(bool focus) => onFocus?.Invoke(focus);
}

public class UploadTask
{
    public string gid, propName, propDesc, username, key;
    public string bundle, thumbnail;
}
