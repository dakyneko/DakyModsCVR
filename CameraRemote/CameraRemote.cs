using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using ABI.CCK.Components;
using ABI_RC.Systems.Camera;
using HarmonyLib;
using System.IO;

using PortableCamera = ABI_RC.Systems.Camera.PortableCamera;
using CVRInteractableManager = ABI_RC.Core.Savior.CVRInteractableManager;
using RefFlags = System.Reflection.BindingFlags;
using VisualMods = ABI_RC.Systems.Camera.VisualMods;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(CameraRemote.CameraRemoteMod), "CameraRemote", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace CameraRemote
{
    using static Daky.Dakytils;
    public class CameraRemoteMod : MelonMod
    {
        private static MelonLogger.Instance logger;

        public override void OnApplicationStart()
        {
            logger = LoggerInstance;

            HarmonyInstance.Patch(
                typeof(PortableCamera).GetMethod(nameof(PortableCamera.Start),  RefFlags.Instance | RefFlags.NonPublic),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(CameraRemoteMod), nameof(OnPortableCameraStart))));
        }

        private static void OnPortableCameraStart(PortableCamera __instance)
        {
            var rc = new RemoteControl();
            __instance.RegisterMod(rc);
            __instance.RequireUpdate(rc);

            // TODO: add options for the remote control in settings panel
            // - switch control position: left or right
            // - speed, smooth factors
        }

        private static UnityEvent UnityEventWithAction(UnityAction f)
        {
            var ev = new UnityEvent();
            ev.AddListener(f);
            return ev;
        }
    }

    public class RemoteControl : ICameraVisualMod, ICameraVisualModRequireUpdate
    {
        private PortableCamera _portable = null;
        private Camera _camera = null;
        private Sprite _icon;
        private GameObject _remote;
        private bool _grabbed = false;

        public RemoteControl() => _icon = SpriteFromAssembly(this.GetType().Namespace, "img_431709.png");

        public string GetModName(string language) => "Remote Control";
        public Sprite GetModImage() => _icon;
        public int GetSortingOrder() => 10;
        public bool ActiveIsOrange() => false;
        public bool DefaultIsOn() => false;
        public void Setup(PortableCamera portableCam, Camera camera)
        {
            _portable = portableCam;
            _camera = camera;
        }

        public void Enable()
        {
            _portable.EnableModByType(typeof(VisualMods.CameraAttachment));
            if (_remote != null)
            {
                _remote.SetActive(true);
                return;
            }

            // first time: create the remote cube
            var parent = new GameObject("CameraRemoteParent");
            {
                var t = parent.transform;
                t.SetParent(_portable.transform.parent, false); // to CVR Camera 2.0
                // TODO: CVR Camera 2.0 has z-skewed scale, should we spawn our things elsewhere or unskew camera?
                t.localScale = new Vector3(20, 20, 20f / 3); // unskew inherited scale
                t.localPosition = 100 * Vector3.right;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            {
                var t = cube.transform;
                t.SetParent(parent.transform, false);
            }
            cube.name = "CameraRemote";
            cube.layer = LayerMask.NameToLayer("UI");
            cube.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Texture"));

            var body = cube.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            var pickup = cube.AddComponent<CVRPickupObject>();
            pickup.gripType = CVRPickupObject.GripType.Free; // TODO: check
            pickup.drop.AddListener(() =>
            {
                var t = cube.transform;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                _grabbed = false;
            });
            pickup.grab.AddListener(() => {
                _grabbed = true;
            });

            var interactable = cube.AddComponent<CVRInteractable>();
            interactable.actions = new() {
                new() {
                    actionType = CVRInteractableAction.ActionRegister.OnInteractDown,
                    execType = CVRInteractableAction.ExecutionType.LocalNotNetworked,
                    operations = new() {
                        new CVRInteractableActionOperation {
                            type = CVRInteractableActionOperation.ActionType.MethodCall,
                            gameObjectVal = cube,
                            customEvent = (new System.Func<UnityEvent>(() => // isn't that beautiful?
                            {
                                var ev = new UnityEvent();
                                ev.AddListener(() =>
                                {
                                    if (_grabbed)
                                        _portable.MakePhoto();
                                });
                                return ev;
                            }))(),
                        },
                    },
                }
            };

            _remote = cube;
        }
        public void Disable()
        {
            _remote?.SetActive(false);
        }

        public void Update()
        {
            if (!_grabbed || _remote?.activeInHierarchy != true) return;

            // follow but at slower scale
            var t = _remote.transform;
            var t2 = _camera.transform;
            t2.localPosition += t2.localRotation * (Time.deltaTime * 0.5f * t.localPosition);
            t2.localRotation *= Quaternion.Slerp(Quaternion.identity, t.localRotation, Time.deltaTime * 1.5f);
        }
    }
}
