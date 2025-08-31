using ABI.CCK.Components;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using MelonLoader;
using System.Runtime.CompilerServices;
using UnityEngine;
using ActionMenu;
using System.Collections.Generic;

[assembly:MelonGame(null, "ChilloutVR")]
[assembly:MelonInfo(typeof(MiniMap.MiniMapMod), "MiniMap", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]
[assembly:MelonAdditionalDependencies("ActionMenu")]

namespace MiniMap;
using static Daky.Dakytils;

public class MiniMapMod : MelonMod
{
    private static MelonLogger.Instance logger;
    private static MiniMapMod instance;
    private MelonPreferences_Entry<bool> iconsEnabled;
    private MelonPreferences_Entry<float> iconWidth, iconAbove, cutAbove, fovScale, elevation, azimuth, stereoScale;

    private Transform view, viewer;
    private Camera camera;
    private (int width, int height) resolution = (1920, 1080); // TODO: make square instead?
    private float viewWidth = 0.5f, cameraHalfWidth, viewAspectRatio, cameraDistance = 60f;
    private RenderTexture textureL, textureR;
    private Material unlitMaterial;
    private Shader stereoShader;
    private bool inFullscreen = false;

    public override void OnInitializeMelon()
    {
        logger = LoggerInstance;
        instance = this;

        unlitMaterial = new Material(Shader.Find("Unlit/Texture")); // TODO: Unlit/Transparent?
        LoadShaders();

        var ns = typeof(MiniMapMod).Namespace;
        var category = MelonPreferences.CreateCategory(ns, ns);
        iconsEnabled = category.CreateEntry("icons", true, "Icons", "Display player icons above their head");
        iconWidth = category.CreateEntry("iconWidth", 0.1f, "Icons size", "Width");
        iconAbove = category.CreateEntry("iconAbove", 1f, "Icons offset", "Height offset above player");
        cutAbove = category.CreateEntry("cutAbove", 2f, "Cut above", "View cuts everything above that height"); // cut a little higher than viewpoint
        cutAbove.OnEntryValueChanged.Subscribe((_, v) => RecomputeProjectionMatrix());
        azimuth = category.CreateEntry("azimuth", -150f, "View azimuth", "Viewpoint angle around you");
        azimuth.OnEntryValueChanged.Subscribe((_, v) => RecomputeProjectionMatrix());
        elevation = category.CreateEntry("elevation", 45f, "View elevation", "Viewpoint angle above the ground");
        elevation.OnEntryValueChanged.Subscribe((_, v) => RecomputeProjectionMatrix());
        fovScale = category.CreateEntry("fovScale", 2.4f, "Distance", "Proportional to camera fov"); // view width = distance
        fovScale.OnEntryValueChanged.Subscribe((_, v) => RecomputeProjectionMatrix() );
        stereoScale = category.CreateEntry("stereoScale", 0.094f, "3D depth",  "Stereo camera width"); // 3D effect = VR stereo render

        // Action menu
        new SettingsMenu();
    }

    private bool inStereo => MetaPort.Instance.isUsingVr;

    private void LoadShaders()
    {
        var ns = GetType().Namespace + ".Resources";
        var bundleBytes = BytesFromAssembly(ns, "stereo_shader.bundle");
        var bundle = AssetBundle.LoadFromMemory(bundleBytes);
        stereoShader = bundle.LoadAsset<Shader>("assets/stereotexture/stereo.shader");
        stereoShader.hideFlags = HideFlags.DontUnloadUnusedAsset;
    }

    private void ToggleFullscreen(bool enable)
    {
        if (inStereo) return;
        // TODO: only work in desktop tho
        // TODO: viewAspectRatio is wrong then
        PlayerSetup.Instance.desktopCamera.GetComponent<Camera>().enabled = !enable;
        inFullscreen = enable;
    }

    private void ToggleView(bool enable)
    {
        if (view == null)
        {
            // TODO: players pics aren't loaded if hidden, we need to force them
            if (enable)
                SpawnView();
        }
        else
        {
            view.gameObject.SetActive(enable);
            camera.gameObject.SetActive(enable);
            // TODO: maybe we should release the rendertexture?
            // TODO: if switching between vr <> desktop, then we need alloc/free right rtex
        }
    }

    private void SpawnView()
    {
        // setup Texture
        var (w, h) = resolution;
        viewAspectRatio = 1f * w / h;
        textureL = new RenderTexture(w, h, 32) { antiAliasing = 1, useMipMap = false };
        if (inStereo)
            textureR = new RenderTexture(w, h, 32) { antiAliasing = 1, useMipMap = false };

        // create the view pickup
        var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var t = view = plane.transform;
        var player = PlayerSetup.Instance;
        t.SetParent(player.transform, false);
        t.localPosition += new Vector3(0, 0.5f, 0.5f); // forward and up
        // TODO: add option to stick to user arm or on hud
        t.localRotation = Quaternion.Euler(10, 0, 0);
        t.localScale = new Vector3(viewWidth, viewWidth / viewAspectRatio, 0.1f);

        var mat = new Material(stereoShader);
        mat.SetTexture("_LTex", textureL); 
        mat.SetTexture("_RTex", textureR);
        plane.GetComponent<Renderer>().material = mat;
        plane.name = "MiniMap";
        plane.layer = LayerMask.NameToLayer("UI");

        var body = plane.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        var pickup = plane.AddComponent<CVRPickupObject>();
        pickup.gripType = CVRPickupObject.GripType.Free;

        // create the camera
        var go = new GameObject("MiniMap Camera");
        go.transform.SetParent(player.transform.root, false);
        camera = go.AddComponent<Camera>();
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.enabled = false; // manual rendering

        UpdatePosition();
        RecomputeProjectionMatrix();
    }

    private void RecomputeProjectionMatrix()
    {
        if (camera == null) return;

        // fov part first
        var fov = Mathf.Exp(fovScale.Value);
        camera.fieldOfView = fov; // exp makes it easier to adjust
        cameraHalfWidth = Mathf.Tan(fov/2 * Mathf.PI / 180) * cameraDistance;

        // camera projection matrix cuts open the world to see inside from above
        var n = Vector3.up;
        var pos = GetViewer().position + cutAbove.Value * Vector3.up; // cut a little higher than viewpoint
        var worldSpace = (Vector4) n; // vector3 normal + w=distance
        worldSpace.w = -Vector3.Dot(n, pos); // distance from camera to cut plane projected on normal
        var cameraSpace = Matrix4x4.Transpose(camera.cameraToWorldMatrix) * worldSpace;
        camera.ResetProjectionMatrix(); // take fov in consideration
        camera.projectionMatrix = camera.CalculateObliqueMatrix(cameraSpace);
    }

    private Transform GetViewer()
    {
        return viewer = viewer.NN()
            ?? PlayerSetup.Instance.Animator?.GetBoneTransform(HumanBodyBones.Head)
            ?? PlayerSetup.Instance.transform;
    }

    private void UpdatePosition()
    {
        // camera follows local players from above
        var t = camera.transform;
        var v = GetViewer();
        t.position = v.position - Quaternion.Euler(elevation.Value, azimuth.Value, 0) * (cameraDistance * Vector3.forward);
        t.LookAt(v.position);
    }

    public override void OnFixedUpdate() { // TODO: do we dare Update()
        if (camera?.gameObject?.activeInHierarchy != true) return;

        UpdatePosition();

        var t = camera.transform;
        var pos = t.position;
        var stereo = inStereo ? stereoScale.Value * cameraHalfWidth : 0;
        t.position = pos - stereo * t.right; // left eye
        if (inFullscreen)
            CameraRender(null);
        else
            CameraRender(textureL);
        if (!inStereo) return;
        t.position = pos + stereo * t.right; // right eye
        CameraRender(textureR);
    }

    private void CameraRender(RenderTexture tex)
    {
        camera.targetTexture = tex;
        camera.Render();

        // draw player's icons
        if (iconsEnabled.Value)
        {
            RenderTexture.active = tex;
            GL.PushMatrix();
            GL.LoadOrtho();
            foreach (var p in MetaPort.Instance.PlayerManager.NetworkPlayers)
                DrawPlayerIcon(p, width: iconWidth.Value, above: iconAbove.Value);
            GL.PopMatrix();
            RenderTexture.active = null;
            unlitMaterial.mainTexture = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawPlayerIcon(CVRPlayerEntity player, float width = 0.2f, float above = 1f) {
        var t = player.PlayerObject?.transform;
        var icon = player.PlayerNameplate?.playerImage?.texture;
        if (t == null || icon == null) return;

        var p = camera.WorldToViewportPoint(t.position + above * Vector3.up);
        if (p.x < 0 || p.x > resolution.width || p.y < 0 || p.y > resolution.height) return; // outside canvas
        var x1 = p.x - width / (2f * viewAspectRatio);
        var x2 = p.x + width / (2f * viewAspectRatio);
        var y1 = p.y - width / 2f;
        var y2 = p.y + width / 2f;

        unlitMaterial.SetTexture("_MainTex", icon);
        unlitMaterial.SetPass(0);
        GL.Begin(GL.QUADS);
        GL.TexCoord2(0, 0); GL.Vertex3(x1, y1, 0);
        GL.TexCoord2(0, 1); GL.Vertex3(x1, y2, 0);
        GL.TexCoord2(1, 1); GL.Vertex3(x2, y2, 0);
        GL.TexCoord2(1, 0); GL.Vertex3(x2, y1, 0);
        GL.End();
    }

    private class SettingsMenu : ActionMenuMod.Lib
    {
        protected override string modName => "MiniMap";
        //protected override string? modIcon => ""; // TODO: add icon?

        protected override List<MenuItem> modMenuItems()
        {
            var m = instance;
            var xs = new List<MenuItem>()
            {
                Toggle("Toggle", m.ToggleView), // TODO: should get default value
                Radial("Height", v => m.cutAbove.Value = v,
                    defaultValue: m.cutAbove.Value, minValue: 0, maxValue: 5f),
                Menu("Player icons", () => new () {
                    Toggle("Enabled", v => m.iconsEnabled.Value = v, m.iconsEnabled.Value),
                    Radial("Size", v => m.iconWidth.Value = v,
                        defaultValue: m.iconWidth.Value, minValue: 0.01f, maxValue: 0.2f),
                    Radial("Offset", v => m.iconAbove.Value = v,
                        defaultValue: m.iconAbove.Value, minValue: 0f, maxValue: 5f),
                }),
                InputVector2D("Angle", (x,y) => { m.azimuth.Value = -x; m.elevation.Value = 45 + y; },
                    defaultValueX: m.azimuth.Value,   minValueX: 0f, maxValueX: 360f,
                    defaultValueY: m.elevation.Value, minValueY: -44f, maxValueY: +44f),
                Radial("Distance", v => m.fovScale.Value = v,
                    defaultValue: m.fovScale.Value, minValue: 0.005f, maxValue: 5f),
            };
            if (instance.inStereo) {
                xs.Add(Radial("3D depth", v => m.stereoScale.Value = v,
                    defaultValue: m.stereoScale.Value, minValue: -0.3f, maxValue: 0.3f));
            }
            else {
                xs.Add(Toggle("Fullscreen", m.ToggleFullscreen));
            }
            return xs;
        }
    }
}
