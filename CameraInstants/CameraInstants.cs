using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using ABI.CCK.Components;

using LfsApi = LagFreeScreenshots.API.LfsApi;
using PortableCamera = ABI_RC.Systems.Camera.PortableCamera;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(CameraInstants.CameraInstantsMod), "CameraInstants", "1.1.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace CameraInstants
{
    public class CameraInstantsMod : MelonMod
    {
        private static MelonLogger.Instance logger;
        private MelonPreferences_Entry<bool> myInstantsEnabled;

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;

            var category = MelonPreferences.CreateCategory("CameraInstants", "CameraInstants");
            myInstantsEnabled = category.CreateEntry("InstantsEnabled", true, "When using the camera, spawn the image in the world (local only)");
            // TODO: should listen to events on myInstantsEnabled change and add/rem listener instead
            LfsApi.OnScreenshotTexture += OnScreenshotTexture;

            // TODO: support piles of pictures (multiple stacked on each other)
            // either we can spread them or put them between them, latest should be always on top?
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

            var aspectRatio = (float)rtex.height / (float)rtex.width;

            // let's downscale the instants texture to save memory
            int w = 640; // TODO: make this a setting
            int h = (int)Mathf.Floor( w * aspectRatio );
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
