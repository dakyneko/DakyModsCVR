using ABI.CCK.Components;
using ABI_RC.Systems.Camera;
using Daky;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonGame(null, "ChilloutVR")]
[assembly: MelonInfo(typeof(CameraStar.CameraStarMod), "CameraStar", "1.1.3", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace CameraStar
{
    using static Dakytils;
    public class CameraStarMod : MelonMod
    {
        private static MelonLogger.Instance logger;
        public static bool disableCameraFadeout = true;

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;

            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PortableCamera).Start()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(CameraStarMod), nameof(OnPortableCameraStart))));
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PortableCamera).Update()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(CameraStarMod), nameof(OnPortableCameraUpdate))));
        }
        // TODO: there are too many built-in mods, we should have options to hide them or group them (like vrc)

        private static void OnPortableCameraUpdate(PortableCamera __instance)
        {
            if (disableCameraFadeout)
                __instance.cameraCanvasGroup.alpha = 1; // override
        }

        private static void OnPortableCameraStart(PortableCamera __instance)
        {
            __instance.RegisterMod(new AlphaTransparency());
            __instance.RegisterMod(new CameraLocker());

            PortableCameraAddHeader(__instance);
            NewCameraSetting(__instance, "hideworld", "Hide world", typeof(CameraStarMod), false, v =>
            {
                if (v) __instance._camera.cullingMask &=
                    (1 << LayerMask.NameToLayer("PlayerLocal")) |
                    (1 << LayerMask.NameToLayer("PlayerNetwork"));
                else __instance.RestoreLayerMask();
            });

            NewCameraSetting(__instance, "disablefadeout", "Transparent when inactive", typeof(CameraStarMod),
                !disableCameraFadeout,
                v => disableCameraFadeout = !v);

            NewCameraSetting(__instance, "fov", "FOV (Field Of View)", typeof(CameraStarMod),
                __instance._camera.fieldOfView, minValue: 3f, maxValue: 180f,
                onChange: v => __instance._camera.fieldOfView = v);

            NewCameraSetting(__instance, "clipnear", "Near Clipping", typeof(CameraStarMod), 0f, minValue: 0f, maxValue: 1f,
                onChange: v => __instance._camera.nearClipPlane = 0.01f + Mathf.Exp(5 * v) - 1);

            NewCameraSetting(__instance, "clipfar", "Far Clipping", typeof(CameraStarMod), 1f, minValue: 0f, maxValue: 1f,
                onChange: v => __instance._camera.farClipPlane = 0.01f + Mathf.Exp(8 * v) - 1);

            NewCameraSetting(__instance, "orthographic", "Orthographic", typeof(CameraStarMod),
                __instance._camera.orthographic,
                v => __instance._camera.orthographic = v);
        }
    }

    public class CameraLocker : ICameraVisualMod
    {
        private PortableCamera _portable = null;
        private Sprite _icon;
        private Transform _cvrCamera20;
        private Transform _modsButtonParent;
        private Transform _sideButtonParent;

        public CameraLocker() => _icon = SpriteFromAssembly(this.GetType().Namespace, "locker.png");
        public string GetModName(string language) => "Lock";
        public Sprite GetModImage() => _icon;
        public int GetSortingOrder() => 42;
        public bool ActiveIsOrange() => false;
        public bool DefaultIsOn() => false;
        public void Setup(PortableCamera portableCam, Camera camera)
        {
            _portable = portableCam;
            _cvrCamera20 = portableCam.transform.parent;
            var canvas = _cvrCamera20.Find("Content/Camera Canvas");
            _modsButtonParent = canvas.Find("VisualMods/Scroll View/Viewport/Content");
            _sideButtonParent = canvas.Find("Background");
        }

        private void SetState(bool active)
        {
            var camPickup = _cvrCamera20.GetComponent<CVRPickupObject>();
            if (camPickup != null) camPickup.enabled = !active;
            foreach (Transform t in _modsButtonParent)
            {
                if (t.GetComponent<VisualMod>()?.mod == this) continue;
                t.gameObject.SetActive(!active);
            }
            foreach (Transform t in _sideButtonParent)
            {
                if (!t.name.StartsWith("Button")) continue;
                t.gameObject.SetActive(!active);
            }
            var mod = _portable.loadedMods[this];
            mod?.tooltipText?.SetText(active ? "Unlock" : "Lock");
        }

        public void Enable() => SetState(true);
        public void Disable() => SetState(false);

        public PortableCamera.CaptureMode GetSupportedCaptureModes() => PortableCamera.CaptureMode.All;
        public bool SupportsCaptureMode(PortableCamera.CaptureMode captureMode) => true;
    }

    public class AlphaTransparency : ICameraVisualMod
    {
        private PortableCamera _portable = null;
        private Camera _camera = null;
        private Sprite _icon;

        public AlphaTransparency() => _icon = SpriteFromAssembly(this.GetType().Namespace, "opacity-512.png");
        public string GetModName(string language) => "Alpha Transparency";
        public Sprite GetModImage() => _icon;
        public int GetSortingOrder() => 40;
        public bool ActiveIsOrange() => false;
        public bool DefaultIsOn() => false;
        public void Setup(PortableCamera portableCam, Camera camera)
        {
            _portable = portableCam;
            _camera = camera;
        }

        public void Enable()
        {
            _camera.cullingMask &= 5376; // same as in Camera Greensceen mod
            _camera.clearFlags = CameraClearFlags.Color;
            _camera.backgroundColor = Color.clear;
        }
        public void Disable()
        {
            _portable.RestoreLayerMask();
        }
        public PortableCamera.CaptureMode GetSupportedCaptureModes() => PortableCamera.CaptureMode.All;
        public bool SupportsCaptureMode(PortableCamera.CaptureMode captureMode) => true;
    }
}
