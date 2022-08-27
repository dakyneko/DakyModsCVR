using HarmonyLib;
using MelonLoader;
using UnityEngine;
using ABI.CCK.Components;
using ABI_RC.Core.Savior;
using System.Collections.Generic;
using System.Linq;
using cohtml;
using cohtml.Net;
using ABI_RC.Core.InteractionSystem;
using System;
using Newtonsoft.Json;

using OpCodes = System.Reflection.Emit.OpCodes;
using PlayerSetup = ABI_RC.Core.Player.PlayerSetup;
using SchedulerSystem = ABI_RC.Core.IO.SchedulerSystem;
using SettingsType = ABI.CCK.Scripts.CVRAdvancedSettingsEntry.SettingsType;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(ActionMenu.ActionMenuMod), "ActionMenu", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace ActionMenu
{
    using Daky;
    public class ActionMenuMod : MelonMod
    {
        private static MelonLogger.Instance logger;
        private static ActionMenuMod instance;

        public override void OnApplicationStart()
        {
            logger = LoggerInstance;
            instance = this;

            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(CVR_MenuManager).RegisterEvents()),
                transpiler: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(TranspileMenuManagerRegisterEvents))));

            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(CVR_MenuManager).ToggleQuickMenu(default)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnToggleQuickMenu))));

            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(CVR_MenuManager).LateUpdate()),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnLateUpdateQuickMenu))));

            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(CVR_MenuManager).SendCoreUpdate()),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnCoreUpdateQuickMenu))));


            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PlayerSetup).initializeAdvancedAvatarSettings()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnAvatarAdvancedSettings))));

            MelonCoroutines.Start(WaitActionMenu());
        }

        private static IEnumerable<CodeInstruction> TranspileMenuManagerRegisterEvents(IEnumerable<CodeInstruction> instructions)
        {
            var xs = new List<CodeInstruction>(instructions);
            var i = xs.Count - 8;
            if (xs[i].opcode != OpCodes.Ldarg_0 || xs[i+1].opcode != OpCodes.Ldftn || xs[i+2].opcode != OpCodes.Newobj)
            {
                logger.Error($"TranspileMenuManagerRegisterEvents patch failed");
                return instructions;
            }
            var m = AccessTools.Method(typeof(ActionMenuMod), nameof(MenuManagerRegisterEvents));
            xs.Insert(i++, new CodeInstruction(OpCodes.Call, m));
            return xs.AsEnumerable();
        }

        public static void MenuManagerRegisterEvents()
        {
            var view = CVR_MenuManager.Instance.quickMenu.View;
            view.RegisterForEvent("CVRAppActionActionMenuReady", (Delegate)new Action(() => instance.OnActionMenuReady(view)));
        }

        private System.Collections.IEnumerator WaitActionMenu()
        {
            GameObject qm;
            while ((qm = GameObject.Find("/Cohtml/QuickMenu")) == null)
                yield return null;
            logger.Msg($"WaitActionMenu start {qm}");

            var go = qm;
            var t = go.transform;
            var v = t.GetComponent<CohtmlView>();

            go.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            v.enabled = false;
            v.AutoFocus = false;
            v.IsTransparent = true;
            v.Width = 500;
            v.Height = 500;
            v.Page = "coui://uiresources/my_actionmenu/index.html";

            v.enabled = true; // no need to reload!
            //v.View.Reload();
        }

        private static void OnToggleQuickMenu(CVR_MenuManager __instance, bool show)
        {
            logger.Msg($"OnToggleQuickMenu {show}: {__instance}");
            // TODO: this doesn't work with fly mode
            // TODO: technically the opposite hand should be free to work (look around or move)
            PlayerSetup.Instance._movementSystem.SetImmobilized(show);
            var view = CVR_MenuManager.Instance.quickMenu.View;
            view.TriggerEvent<bool>("ToggleQuickMenu", show);
        }

        [Serializable]
        internal struct QuickMenuData
        {
            public Vector2 joystick;
            public float trigger;
        } 

        private static bool OnLateUpdateQuickMenu(CVR_MenuManager __instance)
        {
            if (__instance._inputManager == null)
                __instance._inputManager = CVRInputManager.Instance;
            if (CVRInputManager.Instance.quickMenuButton)
            {
                if (__instance.coreData.menuParameters.quickMenuInGrabMode)
                    __instance.ExitRepositionMode();
                else
                    __instance.ToggleQuickMenu(!__instance._quickMenuOpen);
            }
            if (__instance._inputManager.quickMenuButtonHold)
                __instance.ToggleQuickMenu(true);

            if (!__instance._quickMenuOpen) return false;


            var joystick = Vector2.zero;
            var trigger = 0f;
            if (__instance._desktopMouseMode && !MetaPort.Instance.isUsingVr) // Desktop mode
            {
                if (__instance._camera == null) __instance._camera = PlayerSetup.Instance.desktopCamera.GetComponent<Camera>();

                RaycastHit hitInfo;
                if (__instance.quickMenuCollider.Raycast(__instance._camera.ScreenPointToRay(Input.mousePosition), out hitInfo, 1000f))
                {
                    var coord = hitInfo.textureCoord;
                    joystick = new Vector2(coord.x * 2 - 1, coord.y * 2 - 1);
                }
                trigger = Input.GetMouseButtonDown(0) ? 1 : 0; // do we need button up anyway?
            }
            else
            {
                if (__instance._quickMenuCollider.enabled)
                {
                    var qm = __instance.quickMenu.transform;
                    var anch = __instance._leftVrAnchor.transform;
                    qm.position = anch.position;
                    qm.rotation = anch.rotation;
                }

                var movVect = __instance._inputManager.movementVector;
                joystick = new Vector2(movVect.x, movVect.z); // y is 0 and irrelevant
                trigger = __instance._inputManager.interactLeftValue;
            }

            var view = CVR_MenuManager.Instance.quickMenu.View;
            var data = new QuickMenuData {
                joystick = joystick,
                trigger = trigger,
            };
            view.TriggerEvent<string>("ActionMenuData", JsonUtility.ToJson(data));

            return false; // never run the original method
        }

        private static bool OnCoreUpdateQuickMenu(CVR_MenuManager __instance)
        {
            SchedulerSystem.RemoveJob(new SchedulerSystem.Job(__instance.SendCoreUpdate));
            return false;
        }

        private static Config avatarMenus;
        private static void OnAvatarAdvancedSettings(PlayerSetup __instance)
        {
            avatarMenus = new() { menus = new() };
            var m = avatarMenus.menus;
            var avatarMenuPrefix = "avatar";
            List<MenuItem> aitems = new ();

            // TODO: build hierarchy of menus from parameter names containing slash / like folders
            logger.Msg($"OnAvatarAdvancedSettings {__instance._avatarDescriptor.avatarSettings.settings.Count} items");
            foreach (var s in __instance._avatarDescriptor.avatarSettings.settings)
            {
                logger.Msg($"OnAvatarAdvancedSettings loop {s.name}: {s.type}");
                var i = new MenuItem { name = s.name };

                switch (s.type)
                {
                    case SettingsType.GameObjectToggle:
                        i.action = new ItemAction
                        {
                            type = "avatar parameter",
                            parameter = s.machineName,
                            control = "toggle",
                            value = 1f,
                        };
                        break;

                    case SettingsType.GameObjectDropdown:
                        var submenuName = $"{avatarMenuPrefix}_{s.name}";

                        i.action = new ItemAction
                        {
                            type = "menu",
                            menu = submenuName,
                        };

                        List<MenuItem> sitems = s.dropDownSettings.options.Select((o, index) => new MenuItem
                        {
                            name = o.name,
                            action = new ItemAction
                            {
                                type = "avatar parameter",
                                parameter = s.machineName,
                                control = "toggle",
                                value = index,
                                exclusive_option = true,
                            },
                        }).ToList();

                        m.Add(submenuName, sitems);
                        break;

                    case SettingsType.Slider:
                        var sslider = s.sliderSettings;
                        i.action = new ItemAction
                        {
                            type = "avatar parameter",
                            parameter = s.machineName, // TODO: probably wrong
                            control = "radial",
                            default_value = sslider.defaultValue,
                            min_value = 0.0f,
                            max_value = 1.0f,
                        };
                        break;

                    case SettingsType.InputSingle:
                        i.action = new ItemAction
                        {
                            type = "avatar parameter",
                            parameter = s.machineName,
                            control = "radial",
                            default_value = s.inputSingleSettings.defaultValue,
                            // TODO: we're guessing here, we should allow to override somewhere
                            min_value = 0.0f,
                            max_value = 1.0f,
                        };
                        break;

                    case SettingsType.MaterialColor:
                    case SettingsType.Joystick2D:
                    case SettingsType.Joystick3D:
                    case SettingsType.InputVector2:
                    case SettingsType.InputVector3:
                        break; // TODO: unsupported
                };

                aitems.Add(i);
            }

            m.Add(avatarMenuPrefix, aitems);

            // TODO: very ugly
            var view = CVR_MenuManager.Instance.quickMenu.View;
            instance.OnActionMenuReady(view);
        }

        private void OnActionMenuReady(View view)
        {
            logger.Msg($"OnActionMenuReady for view {view}");
            // TODO: load menu dynamically, for ex for avatars params?
            // TODO: sync state of mic, camera on/off, seated, etc
            var fromFile = System.IO.File.ReadAllText(@"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\my_actionmenu\actionmenu.json");
            var config = JsonConvert.DeserializeObject<Config>(fromFile);
            logger.Msg($"Loaded config with {config.menus.Count} menus: {string.Join(", ", config.menus.Keys)}");

            if (avatarMenus.menus != null)
            {
                foreach (var x in avatarMenus.menus)
                    config.menus.Upsert(x.Key, x.Value);
                logger.Msg($"Loaded config from avatar {avatarMenus.menus.Count} menus: {string.Join(", ", avatarMenus.menus.Keys)}");
            }

            var jsonTxt = JsonConvert.SerializeObject(config, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            view.TriggerEvent<string, bool>("LoadActionMenu", jsonTxt, PlayerSetup.Instance._inVr);
        }

        private void SpawnActionMenu()
        {
            var view = CVR_MenuManager.Instance?.quickMenu;
            if (view == null)
            {
                logger.Msg($"SpawnActionMenu view is null!");
                return;
            }
            view.View.Reload();
            view.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            logger.Msg($"view reloaded {view} {view.View}");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F3)) SpawnActionMenu(); // reload
        }
    }

    struct Config
    {
        public Dictionary<string, List<MenuItem>> menus;
    }
    struct MenuItem
    {
        public string? name;
        public string? icon;
        public ItemAction action;
    } 
    struct ItemAction
    {
        public string type;
        public string? menu;
        [JsonProperty(PropertyName = "event")]
        public string? event_;
        public string? control;
        public string? parameter;
        public object? value;
        public object? min_value;
        public object? max_value;
        public object? default_value;
        public bool? toggle;
        public bool? exclusive_option; // highlight a single option in current menu
        public float? duration;
    }
}
