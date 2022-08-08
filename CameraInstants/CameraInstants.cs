using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using ABI.CCK.Components;

using LfsApi = LagFreeScreenshots.API.LfsApi;
using PortableCamera = ABI_RC.Systems.Camera.PortableCamera;
using CVRInteractableManager = ABI_RC.Core.Savior.CVRInteractableManager;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(CameraInstants.CameraInstantsMod), "CameraInstants", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace CameraInstants
{
    public class CameraInstantsMod : MelonMod
    {
        private static MelonLogger.Instance logger;
        private MelonPreferences_Entry<bool> myInstantsEnabled;

        public override void OnApplicationStart()
        {
            logger = LoggerInstance;

            var category = MelonPreferences.CreateCategory("CameraInstants", "CameraInstants");
            myInstantsEnabled = category.CreateEntry("InstantsEnabled", true, "When using the camera, spawn the image in the world (local only)");
            // TODO: should listen to events on myInstantsEnabled change and add/rem listener instead
            LfsApi.OnScreenshotTexture += OnScreenshotTexture;

            // TODO: can add options to camera settings panel
            // - spawn position: top, bottom, left, right
            // - spawn size, transparency, resolution
            // - allowed action like delete
            // TODO: implement delete on disk (move into trash bin)
        }

        private void OnScreenshotTexture(UnityEngine.RenderTexture rtex)
        {
            var portableCamera = PortableCamera.Instance;
            if (!myInstantsEnabled.Value || portableCamera == null) return;

            var tex = new Texture2D(rtex.width, rtex.height, TextureFormat.ARGB32, false);
            Graphics.CopyTexture(rtex, tex);
            var aspectRatio = (float)tex.height / (float)tex.width;

            // TODO: gotta downscale to avoid huge memory usage!
            //var resizedWidth = Math.Min(tex.width, 1920);
            //tex.Resize(resizedWidth, (int)Mathf.Floor(aspectRatio * resizedWidth));
            //tex.Apply();

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var t = plane.transform;
            t.SetParent(portableCamera.transform.parent, false); // to CVR Camera 2.0
            t.localPosition = 100 * Vector3.left;
            t.localRotation = Quaternion.Euler(270, 0, 0);
            t.localScale = new Vector3(8f, 0.01f, 8f * aspectRatio);

            // make it double sided because easy to lose it in the world
            var backside = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var t2 = backside.transform;
            t2.SetParent(t, false);
            t2.localRotation = Quaternion.Euler(0, 0, 180); // backside

            var m = new Material(Shader.Find("Unlit/Texture"));
            m.mainTexture = tex;
            plane.GetComponent<Renderer>().material = m;
            backside.GetComponent<Renderer>().material = m;

            plane.name = "CameraInstants";
            backside.name = "back";
            //plane.layer = LayerMask.NameToLayer("InternalUI"); // TODO: which layer?
            plane.GetComponent<Collider>().isTrigger = true;

            var body = plane.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            var pickup = plane.AddComponent<CVRPickupObject>();
            pickup.gripType = CVRPickupObject.GripType.Free;
            bool grabbed = false;
            pickup.drop.AddListener(() => grabbed = false);
            pickup.grab.AddListener(() => {
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
        }

        private static UnityEvent UnityEventWithAction(UnityAction f)
    {
            var ev = new UnityEvent();
            ev.AddListener(f);
            return ev;
    }
}
}
