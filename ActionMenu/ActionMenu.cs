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
using ABI.CCK.Scripts;
using System.IO;

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
        private static CohtmlView cohtmlView;

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
            view.RegisterForEvent("CVRAppActionActionMenuReady", new Action(instance.OnActionMenuReady));
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

            v.enabled = true;
            cohtmlView = v;
        }

        private static void OnToggleQuickMenu(CVR_MenuManager __instance, bool show)
        {
            logger.Msg($"OnToggleQuickMenu {show}: {__instance}");
            // TODO: this doesn't work with fly mode
            // TODO: technically the opposite hand should be free to work (look around or move)
            PlayerSetup.Instance._movementSystem.SetImmobilized(show);
            var view = cohtmlView.View;
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

            var view = cohtmlView.View;
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

        // we interpret names with | as folders to make a hierarchy, ex: Head|Hair|Length
        // we'll build menus and submenus necessary to allow it automatically
        private static readonly char HierarchySep = '|';
        private static readonly string AvatarMenuPrefix = "avatar";

        private static Menu avatarMenus;
        private static void OnAvatarAdvancedSettings(PlayerSetup __instance)
        {
            var menuPrefix = AvatarMenuPrefix;
            avatarMenus = new() { menus = new() };
            var m = avatarMenus.menus;
            HashSet<(string parent, string child)> hierarchy_pairs = new();

            // Build menus from the items directly (leaves in the hierarchy)
            logger.Msg($"OnAvatarAdvancedSettings {__instance._avatarDescriptor.avatarSettings.settings.Count} items");
            foreach (var s in __instance._avatarDescriptor.avatarSettings.settings)
            {
                var name = s.name;
                var parents = menuPrefix;
                var i = s.name.LastIndexOf(HierarchySep);
                if (i >= 0) { // is in a folder
                    name = s.name.Substring(i + 1);
                    parents += HierarchySep + s.name.Substring(0, i);
                }
                logger.Msg($"OnAvatarAdvancedSettings loop {name} <- {parents}: {s.type}");

                var item = new MenuItem { name = name };
                AvatarParamToItem(ref item, s, menuPrefix, m);

                var aitems = m.GetWithDefault(parents, () => new());
                aitems.Add(item);

                // register the hierarchy upward
                var hierarchy = parents.Split(HierarchySep);
                var child = menuPrefix;
                for (var j = 1; j < hierarchy.Length; ++j) {
                    var parent = child;
                    child = parent + HierarchySep + hierarchy[j];
                    logger.Msg($"OnAvatarAdvancedSettings register hierarchy pair loop {child} <- {parent}");
                    hierarchy_pairs.Add((parent, child));
                }
            }

            // now build the reset, non-leaves up to the root menu (hierarchy of menus)
            foreach (var x in hierarchy_pairs)
            {
                var parent = x.parent;
                var child = x.child;
                var i = child.LastIndexOf(HierarchySep);
                var childName = (i >= 0) ? x.child.Substring(i+1) : child;
                logger.Msg($"OnAvatarAdvancedSettings build hierarchy ({childName}): {child} <- {parent}");

                var item = new MenuItem
                {
                    name = childName,
                    action = new ItemAction
                    {
                        type = "menu",
                        menu = child,
                    },
                };
                m.GetWithDefault(parent, () => new()).Add(item);
            }

            instance.OnActionMenuReady();
        }

        private static void AvatarParamToItem(ref MenuItem item, CVRAdvancedSettingsEntry s,
            string menuPrefix, Menus m)
        {
            // build the items in the menu
            switch (s.type)
            {
                case SettingsType.GameObjectToggle:
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = "toggle",
                        value = 1f,
                    };
                    break;

                case SettingsType.GameObjectDropdown:
                    var submenuName = menuPrefix + HierarchySep + s.name;
                    // if parameter name has suffix Impulse, adapt control type
                    var isImpulse = s.machineName.EndsWith("Impulse");

                    item.action = new ItemAction
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
                            control = isImpulse ? "impulse" : "toggle",
                            value = index,
                            exclusive_option = !isImpulse,
                        },
                    }).ToList();

                    m.Add(submenuName, sitems);
                    break;

                case SettingsType.Slider:
                    var sslider = s.sliderSettings;
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = "radial",
                        default_value = sslider.defaultValue,
                        min_value = 0.0f,
                        max_value = 1.0f,
                    };
                    break;

                case SettingsType.InputSingle:
                    item.action = new ItemAction
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

                case SettingsType.Joystick2D:
                    var sjoy = s.joystick2DSetting;
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = "joystick_2d",
                        min_value_x = 0.0f, // TODO: cvr seems to ignore min/max defined in unity, sad :(
                        max_value_x = 1.0f,
                        default_value_x = sjoy.defaultValue.x,
                        min_value_y = 0.0f,
                        max_value_y = 1.0f,
                        default_value_y = sjoy.defaultValue.y,
                    };
                    break;

                case SettingsType.InputVector2:
                    var svec = s.inputVector2Settings;
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = "input_vector_2d",
                        min_value_x = 0.0f, // TODO: cvr has no min max values so we're left guessing here
                        max_value_x = 1.0f,
                        default_value_x = svec.defaultValue.x,
                        min_value_y = 0.0f,
                        max_value_y = 1.0f,
                        default_value_y = svec.defaultValue.y,
                    };
                    break;

                case SettingsType.MaterialColor:
                case SettingsType.Joystick3D:
                case SettingsType.InputVector3:
                    break; // TODO: unsupported
            };
        }

        // TODO: sync state of mic, camera on/off, seated, etc
        private void OnActionMenuReady()
        {
            var view = cohtmlView.View;
            logger.Msg($"OnActionMenuReady for view {view}");
            // TODO: file path should be a config variable
            var fromFile = File.ReadAllText(@"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\my_actionmenu\actionmenu.json");
            var config = JsonConvert.DeserializeObject<Menu>(fromFile);
            logger.Msg($"Loaded config with {config.menus.Count} menus: {string.Join(", ", config.menus.Keys)}");

            // avatar menu from avatar itself (cvr advanced settings)
            if (avatarMenus.menus != null)
            {
                foreach (var x in avatarMenus.menus)
                    config.menus.Upsert(x.Key, x.Value);
                logger.Msg($"Loaded config from avatar {avatarMenus.menus.Count} menus: {string.Join(", ", avatarMenus.menus.Keys)}");
            }

            var avatarGuid = PlayerSetup.Instance?._avatarDescriptor?.avatarSettings?._avatarGuid ?? "default";
            API.InvokeOnAvatarMenuLoaded(avatarGuid, config.menus);

            // avatar menu override from file
            var avatarOverridesFile = @"UserData\ActionMenu\AvatarOverrides\for_" + avatarGuid + ".json";
            if (File.Exists(avatarOverridesFile))
            {
                try
                {
                    logger.Msg($"loading avatar overrides for {avatarGuid}: {avatarOverridesFile}");
                    var txt = File.ReadAllText(avatarOverridesFile);
                    var overrides = JsonConvert.DeserializeObject<Menus>(txt);
                    foreach (var x in overrides)
                        // TODO: instead of upsert, could have different sections in json for different actions:
                        // at root: {"add": {}, "remove": {}, "replace": {}} to add to items to a menu, remove to a menu or totally replace a menu
                        // a bit like an advanced "patch" system
                        config.menus.Upsert(x.Key, x.Value);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error while reading avatar overrides json for avatar {avatarGuid}: {ex}");
                }
            }
            else
                logger.Msg($"No avatar overrides for {avatarGuid}: {avatarOverridesFile}");

            // global overrides
            var globalOverridesFiles = Dakytils.TryOrDefault(() => Directory.GetFiles(@"UserData\ActionMenu\GlobalOverrides"));
            if (globalOverridesFiles == null || globalOverridesFiles.Length == 0)
                logger.Msg($"no global overrides");
            foreach (var fpath in globalOverridesFiles ?? new string[] { } )
            {
                try
                {
                    logger.Msg($"loading global overrides {fpath}");
                    if (!fpath.EndsWith(".json")) continue;
                    var txt = File.ReadAllText(fpath);
                    var overrides = JsonConvert.DeserializeObject<Menus>(txt);
                    foreach (var x in overrides)
                        config.menus.Upsert(x.Key, x.Value);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error while reading global overrides json {fpath}: {ex}");
                }
            }

            API.InvokeOnGlobalMenuLoaded(config.menus);

            var jsonTxt = JsonConvert.SerializeObject(config,new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            view.TriggerEvent<string, bool>("LoadActionMenu", jsonTxt, PlayerSetup.Instance._inVr);
        }

        private void SpawnActionMenu()
        {
            if (cohtmlView == null)
            {
                logger.Msg($"SpawnActionMenu view is null!");
                return;
            }
            cohtmlView.View.Reload();
            cohtmlView.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            logger.Msg($"view reloaded {cohtmlView} {cohtmlView.View}");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F3)) SpawnActionMenu(); // reload
        }
    }
}
