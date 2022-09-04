using HarmonyLib;
using MelonLoader;
using UnityEngine;
using ABI.CCK.Components;
using ABI_RC.Core.Savior;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Scripts;
using System.Collections.Generic;
using System.Linq;
using cohtml;
using cohtml.Net;
using System;
using Newtonsoft.Json;
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
        // we interpret names with | as folders to make a hierarchy, ex: Head|Hair|Length
        // we'll build menus and submenus necessary to allow it automatically
        public static readonly char HierarchySep = '|';
        public static readonly string AvatarMenuPrefix = "avatar";

        // Public library for all mods to use, you can extend this
        public class Lib
        {
            private ActionMenuMod instance;
            private string prefix_ns;

            public Lib()
            {
                instance = ActionMenuMod.instance;

                // auto detect namespace of caller so we can suffix identifier to avoid collision between mods
                // basically, reflection goes weeeeeeeeee
                var stackTrace = new System.Diagnostics.StackTrace();
                prefix_ns = stackTrace.GetFrame(1).GetMethod().DeclaringType.Namespace;

                API.OnAvatarMenuLoaded += OnAvatarMenuLoaded;
                API.OnGlobalMenuLoaded += OnGlobalMenuLoaded;
            }

            // override this to manipulate avatar menus after they're built
            virtual protected void OnAvatarMenuLoaded(string avatarGuid, Menus menus)
            {
            }

            // override this to manipulate menus after all menus are built
            virtual protected void OnGlobalMenuLoaded(Menus menus)
            {
            }

            // if you want to expose your MelonPreference into a menu, build them with this.
            // it may have to build a hierarchy of menu so add an item pointing to: settings|YourNameMod
            // TODO: implement other types, only boolean for now
            public Menus BuildMelonPrefsMenus(List<MelonPreferences_Entry> melonPrefs)
            {
                var m = new Menus();
                var items = m.GetWithDefault("settings"+ HierarchySep + prefix_ns, () => new());

                foreach (var e_ in melonPrefs)
                {
                    switch (e_)
                    {
                        case MelonPreferences_Entry<bool> e:
                            items.Add(new MenuItem()
                            {
                                name = e.DisplayName,
                                action = new()
                                {
                                    type = "set melon preference",
                                    parameter = e.Identifier,
                                    toggle = true,
                                    default_value = e.Value,
                                }
                            });
                            break;

                        default:
                            logger.Warning($"OnSetMelonPreference {e_.Identifier} unsupported type {e_.GetReflectedType()}");
                            break;
                    }
                }

                return m;
            }

            public static void ApplyMenuPatch(Menus menus, MenusPatch patch)
            {
                if (patch.remove_items != null)
                {
                    foreach (var x in patch.remove_items)
                    {
                        if (!menus.TryGetValue(x.Key, out var items))
                            continue;
                        var toRemove = x.Value;
                        items.RemoveAll(item => item.name != null && toRemove.Contains(item.name));
                    }
                }

                if (patch.add_items != null)
                {
                    foreach (var x in patch.add_items)
                    {
                        var items = menus.GetWithDefault(x.Key, () => new());
                        foreach (var item in x.Value)
                            items.Add(item);
                    }
                }

                if (patch.overwrites != null)
                    foreach (var x in patch.overwrites)
                        menus[x.Key] = x.Value;
            }

            // Use this to create an item that you can add to any menu and it will call your function when item is selected
            // TODO: implement other widget type: toggle with bool, radial with float, etc
            public ItemAction BuildCallbackMenuItem(string name, Action callback)
            {
                var identifier = prefix_ns + "." + name;
                instance.callback_items[identifier] = callback;
                return new ItemAction()
                {
                    type = "callback",
                    parameter = identifier,
                };
            }

        }

        // Private implementation
        private static MelonLogger.Instance logger;
        private static ActionMenuMod instance;
        private static CohtmlView cohtmlView;
        private static Lib ourLib;

        private MelonPreferences_Category melonPrefs;
        private Dictionary<string, MelonPreferences_Entry> melonPrefsMap;
        private MelonPreferences_Entry<bool> flickSelection, boringBackButton, splitAvatarOvercrowdedMenu;

        private Dictionary<string, Action> callback_items; // unique identifier -> function

        public override void OnApplicationStart()
        {
            logger = LoggerInstance;
            instance = this;
            callback_items = new();

            ourLib = new();

            melonPrefs = MelonPreferences.CreateCategory("ActionMenu", "Action Menu");
            flickSelection = melonPrefs.CreateEntry("flick_selection", false, "Flick selection");
            boringBackButton = melonPrefs.CreateEntry("boring_back_button", false, "Boring back button");
            // TODO: implement
            splitAvatarOvercrowdedMenu = melonPrefs.CreateEntry("split_overcrowded_avatar_menu", false, "Split avatar menu in multiple pages when it's too crowded");

            melonPrefsMap = new();
            foreach (var e in melonPrefs.Entries)
                melonPrefsMap.Add(e.Identifier, e);
            BuildOurMelonPrefsMenus();


            // FIXME: hijack quick menu cohtml for now
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

            MelonCoroutines.Start(WaitActionMenu()); // actual QuickMenu hijack

            // build avatar menu from parameters after avatar is loaded
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PlayerSetup).initializeAdvancedAvatarSettings()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnAvatarAdvancedSettings))));


            // monitor game changes to update menu items state
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(CVRCamController).Toggle()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnCVRCameraToggle))));
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => ABI_RC.Core.Base.Audio.SetMicrophoneActive(default)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnCVRMicrophoneToggle))));
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PlayerSetup).SwitchSeatedPlay(default)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnCVRSeatedToggle))));
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(MovementSystem).ToggleFlight()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnCVRFlyToggle))));
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
            var m = SymbolExtensions.GetMethodInfo(() => ActionMenuMod.MenuManagerRegisterEvents());
            xs.Insert(i++, new CodeInstruction(OpCodes.Call, m));
            return xs.AsEnumerable();
        }

        private static void MenuManagerRegisterEvents()
        {
            var view = CVR_MenuManager.Instance.quickMenu.View;
            view.RegisterForEvent("CVRActionMenuReady", new Action(instance.OnActionMenuReady));
            view.BindCall("CVRActionMenuSetMelonPreference", new Action<string, string>(instance.OnSetMelonPreference));
            view.BindCall("CVRActionMenuCallback", new Action<string>(instance.OnActionMenuCallback));
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

            v.enabled = false;
            v.AutoFocus = false;
            v.IsTransparent = true;
            v.Width = 500;
            v.Height = 500;
            v.Page = "coui://uiresources/my_actionmenu/index.html";

            v.enabled = true;
            cohtmlView = v;
        }

        private static void OnCVRCameraToggle(CVRCamController __instance)
        {
            MelonLogger.Msg($"OnCVRCameraToggle {__instance}");
            var u = new MenuItemValueUpdate()
            {
                action = new ItemAction()
                {
                    type = "system call",
                    event_ = "AppToggleCamera",
                    value = __instance?.cvrCamera?.activeSelf ?? false
                },
            };
            cohtmlView.View.TriggerEvent<string>("OnMenuItemValueUpdate", JsonSerialize(u));
        }

        private static void OnCVRMicrophoneToggle(bool muted)
        {
            MelonLogger.Msg($"OnCVRMicrophoneToggle {muted}");
            var u = new MenuItemValueUpdate()
            {
                action = new ItemAction()
                {
                    type = "system call",
                    event_ = "AppToggleMute",
                    value = !muted,
                },
            };
            cohtmlView.View.TriggerEvent<string>("OnMenuItemValueUpdate", JsonSerialize(u));
        }

        private static void OnCVRSeatedToggle(PlayerSetup __instance)
        {
            MelonLogger.Msg($"OnCVRSeatedToggle {__instance.seatedPlay}");
            var u = new MenuItemValueUpdate()
            {
                action = new ItemAction()
                {
                    type = "system call",
                    event_ = "AppToggleSeatedPlay",
                    value = __instance.seatedPlay,
                },
            };
            cohtmlView.View.TriggerEvent<string>("OnMenuItemValueUpdate", JsonSerialize(u));
        }

        private static void OnCVRFlyToggle(MovementSystem __instance)
        {
            MelonLogger.Msg($"OnCVRFlyToggle {__instance.flying}");
            var u = new MenuItemValueUpdate()
            {
                action = new ItemAction()
                {
                    type = "system call",
                    event_ = "AppToggleFLightMode",
                    value = __instance.flying,
                },
            };
            cohtmlView.View.TriggerEvent<string>("OnMenuItemValueUpdate", JsonSerialize(u));
        }

        private static void OnToggleQuickMenu(CVR_MenuManager __instance, bool show)
        {
            logger.Msg($"OnToggleQuickMenu {show}: {__instance}");
            // TODO: this doesn't work with fly mode
            // TODO: technically the opposite hand should be free to work (look around or move)
            PlayerSetup.Instance._movementSystem.SetImmobilized(show);
            var view = cohtmlView.View;
            view.TriggerEvent<bool>("ToggleQuickMenu", show);
            cohtmlView.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f); // FIXME: haxxx to keep it small
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

        private Menus? melonPrefsMenus;
        private void BuildOurMelonPrefsMenus()
        {
            melonPrefsMenus = ourLib.BuildMelonPrefsMenus(melonPrefs.Entries);
        }

        private static Menus? avatarMenus;
        private static void OnAvatarAdvancedSettings(PlayerSetup __instance)
        {
            var avatarGuid = PlayerSetup.Instance?._avatarDescriptor?.avatarSettings?._avatarGuid ?? "default";
            var menuPrefix = AvatarMenuPrefix;
            var m = avatarMenus = new();
            HashSet<(string parent, string child)> hierarchy_pairs = new();

            // Build menus from the avatar parameters, focus on items directly (leaves in the hierarchy)
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
                logger.Msg($"OnAvatarAdvancedSettings parameter {name} <- {parents}: {s.type}");

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
                    hierarchy_pairs.Add((parent, child));
                }
            }

            // now build the rest, non-leaves up to the root menu (hierarchy of menus)
            foreach (var x in hierarchy_pairs)
            {
                var parent = x.parent;
                var child = x.child;
                var i = child.LastIndexOf(HierarchySep);
                var childName = (i >= 0) ? x.child.Substring(i+1) : child;

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

            // add avatar emotes
            var emoteNames = PlayerSetup.Instance.GetEmoteNames();
            if (emoteNames.Length > 0)
            {
                var parents = menuPrefix + HierarchySep + "emotes";
                var aitems = m.GetWithDefault(parents, () => new());
                var i = 1;
                emoteNames.Do(name =>
                {
                    logger.Msg($"OnAvatarAdvancedSettings emote {name} <- {parents}");
                    var item = new MenuItem
                    {
                        name = name,
                        action = new ItemAction
                        {
                            type = "system call",
                            event_ = "AppPlayEmote",
                            event_arguments = new string[] { i.ToString() },
                            exclusive_option = true,
                            toggle = true, // TODO: would be nice to have control=impulse here
                        },
                    };
                    aitems.Add(item);
                    ++i;
                });

                m.GetWithDefault(menuPrefix, () => new()).Add(new MenuItem()
                {
                    name = "Emotes",
                    icon = "icon_avatar_emotes.png",
                    action = new() { type = "menu", menu = parents },
                });
            }
            // TODO: add cvr avatar ToggleState?

            // let mods modify the avatar menu
            API.InvokeOnAvatarMenuLoaded(avatarGuid, m);

            // avatar menu override from json file
            var avatarOverridesFile = @"UserData\ActionMenu\AvatarOverrides\for_" + avatarGuid + ".json";
            if (File.Exists(avatarOverridesFile))
            {
                try
                {
                    logger.Msg($"loading avatar overrides for {avatarGuid}: {avatarOverridesFile}");
                    var txt = File.ReadAllText(avatarOverridesFile);
                    var patch = JsonConvert.DeserializeObject<MenusPatch>(txt);
                    Lib.ApplyMenuPatch(m, patch);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error while reading avatar overrides json for avatar {avatarGuid}: {ex}");
                }
            }

            instance.OnActionMenuReady(); // reload
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

        private void OnActionMenuReady()
        {
            var view = cohtmlView.View;
            logger.Msg($"OnActionMenuReady for view {view}");
            // TODO: file path should be a config variable
            var fromFile = File.ReadAllText(@"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\my_actionmenu\actionmenu.json");
            var config = JsonConvert.DeserializeObject<Menu>(fromFile);
            logger.Msg($"Loaded config with {config.menus.Count} menus: {string.Join(", ", config.menus.Keys)}");

            // add our melon prefs
            if (melonPrefsMenus != null)
            {
                foreach (var x in melonPrefsMenus)
                    config.menus[x.Key] = x.Value;
            }

            // avatar menu from avatar itself (cvr advanced settings)
            if (avatarMenus != null)
            {
                foreach (var x in avatarMenus)
                    config.menus[x.Key] = x.Value;
                logger.Msg($"Loaded config from avatar {avatarMenus.Count} menus: {string.Join(", ", avatarMenus.Keys)}");
            }

            // global overrides
            var globalOverridesFiles = Dakytils.TryOrDefault(() => Directory.GetFiles(@"UserData\ActionMenu\GlobalOverrides"));
            globalOverridesFiles?
                .Where(fpath => fpath.EndsWith(".json"))
                .Do(fpath =>
            {
                try
                {
                    var txt = File.ReadAllText(fpath);
                    var patch = JsonConvert.DeserializeObject<MenusPatch>(txt);
                    Lib.ApplyMenuPatch(config.menus, patch);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error while reading global overrides json {fpath}: {ex}");
                }
            });

            API.InvokeOnGlobalMenuLoaded(config.menus);

            var settings = new MenuSettings
            {
                in_vr = PlayerSetup.Instance._inVr,
                flick_selection = flickSelection.Value,
                boring_back_button = boringBackButton.Value,
            };

            var configTxt = JsonSerialize(config);
            var settingsTxt = JsonSerialize(settings);
            view.TriggerEvent<string, string>("LoadActionMenu", configTxt, settingsTxt);
        }

        private static string JsonSerialize(object value) => JsonConvert.SerializeObject(value, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        private void SpawnActionMenu()
        {
            if (cohtmlView == null)
            {
                logger.Msg($"SpawnActionMenu view is null!");
                return;
            }
            cohtmlView.View.Reload();

            logger.Msg($"view reloaded {cohtmlView} {cohtmlView.View}");
        }

        private void OnSetMelonPreference(string identifier, string value)
        {
            MelonPreferences_Entry e_;
            if (!melonPrefsMap.TryGetValue(identifier, out e_) || e_ == null)
            {
                logger.Warning($"didn't find preference {identifier}");
                return;
            }

            switch (e_)
            {
                case MelonPreferences_Entry<bool> e: {
                    if (float.TryParse(value, out float valueInt))
                        e.Value = valueInt != 0;
                    break;
                }
                // TODO: implement other types

                default:
                    logger.Warning($"OnSetMelonPreference {identifier} unsupported type {e_.GetReflectedType()}");
                    break;
            }

            BuildOurMelonPrefsMenus(); // value update = rebuild and send it back
        }

        private void OnActionMenuCallback(string identifier)
        {
            Action f;
            if (!callback_items.TryGetValue(identifier, out f) || f == null)
            {
                logger.Warning($"didn't find callback {identifier}");
                return;
            }

            try
            {
                f();
            }
            catch (Exception e)
            {
                logger.Error($"failure in callback: {e}");
            }
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F3)) SpawnActionMenu(); // reload
        }
    }
}
