using HarmonyLib;
using MelonLoader;
using UnityEngine;
using ABI_RC.Core;
using ABI_RC.Core.Savior;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Scripts;
using System.Collections.Generic;
using System.Linq;
using cohtml;
using System;
using Newtonsoft.Json;
using System.IO;
using Valve.VR;

using PlayerSetup = ABI_RC.Core.Player.PlayerSetup;
using SettingsType = ABI.CCK.Scripts.CVRAdvancedSettingsEntry.SettingsType;
using BindFlags = System.Reflection.BindingFlags;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(ActionMenu.ActionMenuMod), "ActionMenu", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace ActionMenu
{
    using static Daky.Dakytils;
    using MenuBuilder = Func<List<MenuItem>>;
    public class ActionMenuMod : MelonMod
    {
        // Public library for all mods to use, you can extend this
        public class Lib
        {
            private ActionMenuMod instance;
            public readonly string prefixNs;

            public Lib()
            {
                instance = ActionMenuMod.instance;

                // auto detect namespace of caller so we can suffix identifier to avoid collision between mods
                // basically, reflection goes weeeeeeeeee
                var stackTrace = new System.Diagnostics.StackTrace();
                prefixNs = stackTrace.GetFrame(1).GetMethod().DeclaringType.Namespace;

                RegisterOnLoaded();
            }

            // if you don't need this, you can override it empty
            virtual protected void RegisterOnLoaded()
            {
                API.OnGlobalMenuLoaded += OnGlobalMenuLoaded;
                API.OnAvatarMenuLoaded += OnAvatarMenuLoaded;
            }

            virtual protected string modName => prefixNs; // by default your class name
            virtual protected string? modIcon => null; // can accept: data:image/jpeg;base64
            virtual protected string entry => "main"; // the name of your mod main menu
            virtual protected string modMenuPath(params string[] components)
                => prefixNs + HierarchySep + string.Join(HierarchySep.ToString(), components);

            // Mods can create a menu very easily by filling up some items here.
            virtual protected List<MenuItem> modMenuItems() => new();

            // Or create many menus yourself for your mod. Your menus will have automatically the prefixNs prefix.
            virtual public Menus BuildModMenu()
            {
                var xs = modMenuItems();
                return new()
                {
                    // the main menu of the mod
                    [entry] = modMenuItems(),
                    // more submenus can be added separately here
                };
            }

            // Override this to manipulate menus after all menus are built. Usually not required for simple mod menus.
            virtual protected void OnGlobalMenuLoaded(Menus menus)
            {
                var m = BuildModMenu();
                if (m.Keys.SequenceEqual(new string[] { entry }) && m.GetWithDefault(entry).Count == 0)
                    return; // if empty don't create anything

                // add all mod menus and take care of the prefixNs prefix
                foreach (var x in m) {
                    var items = x.Value.Select(item =>
                    {
                        if (item.action.type == "menu")
                            item.action.menu = modMenuPath(item.action.menu);
                        return item;
                    });
                    menus.GetWithDefault(modMenuPath(x.Key)).AddRange(items);
                }
                menus.GetWithDefault(SubmenuNameForMods).Add(new MenuItem()
                {
                    name = modName,
                    icon = modIcon,
                    action = new ItemAction() { type = "menu", menu = modMenuPath(entry) },
                });
            }

            // override this to manipulate avatar menus after they're built
            virtual protected void OnAvatarMenuLoaded(string avatarGuid, Menus menus)
            {
            }

            // Nice way to modify the Menus. This is to play nice with other mods as well.
            public static void ApplyMenuPatch(Menus menus, MenusPatch patch) => patch.ApplyToMenu(menus);

            // Create an ItemAction when triggered, will call your function
            // Basically for button/widget item for callback in your mod
            public ItemAction BuildButtonItem(string name, Action callback, bool exclusiveOption = false)
            {
                var identifier = prefixNs + ".call." + name;
                instance.callbackItems[identifier] = callback;
                return new ItemAction()
                {
                    type = "callback",
                    parameter = identifier,
                    exclusive_option = exclusiveOption,
                };
            }

            private ItemAction BuildBoolItem(string name, Action<bool> callback, string control,
                object? value = null, object? defaultValue = null, float? duration = null)
            {
                var identifier = prefixNs + "." + control + "." + name;
                instance.callbackItems_bool[identifier] = callback;
                return new ItemAction()
                {
                    type = "callback",
                    parameter = identifier,
                    control = "toggle",
                    duration = duration,
                    value = value,
                    default_value = defaultValue,
                };
            }

            // Creates a button with two states: enabled and disabled.
            public ItemAction BuildToggleItem(string name, Action<bool> callback)
                => BuildBoolItem(name, callback, "toggle", value: true, defaultValue: false);
            // Creates a button that temporarily switch its value, then back to default_value
            public ItemAction BuildImpulseItem(string name, Action<bool> callback, float duration = 1f, object? value = null, object? defaultValue = null)
                => BuildBoolItem(name, callback, "impulse", value: value, defaultValue: defaultValue, duration: duration);

            // Creates a radial widget for picking values between a min and max.
            public ItemAction BuildRadialItem(string name, Action<float> callback,
                float? minValue = null, float? maxValue = null, float? defaultValue = null)
            {
                var identifier = prefixNs + ".radial." + name;
                instance.callbackItems_float[identifier] = callback;
                return new ItemAction() {
                    type = "callback",
                    parameter = identifier,
                    control = "radial",
                    min_value = minValue,
                    max_value = maxValue,
                    default_value = defaultValue,
                };
            }

            private ItemAction Build2DItem(string name, Action<float, float> callback, string control,
                float? minValueX = null, float? maxValueX = null, float? defaultValueX = null,
                float? minValueY = null, float? maxValueY = null, float? defaultValueY = null)
            {
                var identifier = prefixNs + "." + control + "." + name;
                instance.callbackItems_float_float[identifier] = callback;
                return new ItemAction() {
                    type = "callback",
                    parameter = identifier,
                    control = control,
                    min_value_x = minValueX,
                    max_value_x = maxValueX,
                    default_value_x = defaultValueX,
                    min_value_y = minValueY,
                    max_value_y = maxValueY,
                    default_value_y = defaultValueY,
                };
            }

            // Creates a 2D widget for picking two values simultaneously setting absolute coordinates, between min and max values.
            public ItemAction BuildJoystick2D(string name, Action<float, float> callback,
                float? minValueX = null, float? maxValueX = null, float? defaultValueX = null,
                float? minValueY = null, float? maxValueY = null, float? defaultValueY = null)
                => Build2DItem(name, callback, "joystick_2d",
                        minValueX: minValueX, maxValueX: maxValueX, defaultValueX: defaultValueX,
                        minValueY: minValueY, maxValueY: maxValueY, defaultValueY: defaultValueY
                    );
            // Creates a 2D widget for picking two values simultaneously moving in relative coordinates, between min and max values.
            public ItemAction BuildInputVector2D(string name, Action<float, float> callback,
                float? minValueX = null, float? maxValueX = null, float? defaultValueX = null,
                float? minValueY = null, float? maxValueY = null, float? defaultValueY = null)
                => Build2DItem(name, callback, "input_vector_2d",
                        minValueX: minValueX, maxValueX: maxValueX, defaultValueX: defaultValueX,
                        minValueY: minValueY, maxValueY: maxValueY, defaultValueY: defaultValueY
                    );

            // Create an ItemAction when triggered, will call you back so you can build your own menu dynamically
            // Basically building dynamic menus from a mod
            public ItemAction BuildCallbackMenu(string name, MenuBuilder menuBuilder)
            {
                var identifier = prefixNs + "." + name;
                instance.dynamic_menus[identifier] = menuBuilder;
                return new ItemAction()
                {
                    type = "dynamic menu",
                    menu = identifier,
                };
            }

            // if you want to expose your MelonPreference into a menu, build them with this.
            // it may have to build a hierarchy of menu so add an item pointing to: settings/YourNameMod
            // TODO: implement other types because for now it's only boolean
            // TODO: add listener so we can update menu items state automatically (enabled, visually etc)
            public Menus BuildMelonPrefsMenus(List<MelonPreferences_Entry> melonPrefs)
            {
                var m = new Menus();
                var items = m.GetWithDefault(Path(prefixNs, "settings"));

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
        }

        // for avatar menu we interpret names with / as folders to make a hierarchy, ex: Head/Hair/Length
        // we'll build menus and submenus necessary to allow it automatically
        public static readonly string MainMenu = "main";
        public static readonly char HierarchySep = '/';
        public static readonly string SubmenuNameForMods = "mods";
        public static readonly string AvatarMenuPrefix = "avatar";
        public static readonly string couiPath = @"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\ActionMenu";
        public static readonly string couiUrl = "coui://UIResources/ActionMenu";
        public static readonly string[] couiFiles = new string[]
        {
            "index.html", "index.js", "index.css", "actionmenu.json",
            "icon_actionmenu.svg", "icon_menu.svg", "icon_back.svg", "icon_avatar_emotes.svg", "icon_melon.svg",
            "icon_avatar_settings_profile.svg",
        };


        // Private implementation
        private static MelonLogger.Instance logger;
        private static ActionMenuMod instance;
        private static Transform menuTransform;
        private static CohtmlView cohtmlView;
        private static Collider menuCollider;
        private static CVR_MenuManager menuManager;
        private static Animator menuAnimator;
        private static int cohtmlReadyState = 0; // 0=stratup, 1=binding ready, 2=ActionMenuReady
        private static Lib ourLib;

        private MelonPreferences_Category melonPrefs;
        private Dictionary<string, MelonPreferences_Entry> melonPrefsMap;
        private MelonPreferences_Entry<bool> flickSelection, boringBackButton, dontInstallResources,
            splitAvatarOvercrowdedMenu, quickMenuLongPress;

        // unique identifier -> function or menu
        private Dictionary<string, Action> callbackItems = new();
        private Dictionary<string, Action<bool>> callbackItems_bool = new();
        private Dictionary<string, Action<float>> callbackItems_float = new();
        private Dictionary<string, Action<float, float>> callbackItems_float_float = new();
        private Dictionary<string, MenuBuilder> dynamic_menus = new();

        public static string Path(params string[] components) => string.Join(HierarchySep.ToString(), components);

        public override void OnApplicationStart()
        {
            logger = LoggerInstance;
            instance = this;
            ourLib = new OurLib();

            melonPrefs = MelonPreferences.CreateCategory("ActionMenu", "Action Menu");
            flickSelection = melonPrefs.CreateEntry("flick_selection", false, "Flick select",
                description: "Trigger items by just selecting one and recentering selection (no need for trigger)");
            boringBackButton = melonPrefs.CreateEntry("boring_back_button", false, "Eccentric 'Back'",
                description: "Show the Back button like other items (not in the middle), as the last element in all menus");
            dontInstallResources = melonPrefs.CreateEntry("dont_install_resources", false, "Dev mode",
                description: "Don't install nor overwrite the resource files (useful for developping the Action Menu)");
            quickMenuLongPress = melonPrefs.CreateEntry("quickmenu_long_press", false, "Long press for QM",
                description: "Makes the ActionMenu appear with a short press and QuickMenu with long");
            splitAvatarOvercrowdedMenu = melonPrefs.CreateEntry("split_overcrowded_avatar_menu", false, "No crowded menus",
                description: "Split avatar menu in multiple pages when it's too crowded");

            melonPrefsMap = new();
            foreach (var e in melonPrefs.Entries)
                melonPrefsMap.Add(e.Identifier, e);
            BuildOurMelonPrefsMenus();


            // override the quickmenu button behavior, long press means action menu
            HarmonyInstance.Patch(
                typeof(InputModuleSteamVR).GetMethod(nameof(InputModuleSteamVR.UpdateInput), BindFlags.Public | BindFlags.Instance),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnUpdateInputSteamVR))));
            HarmonyInstance.Patch(
                typeof(InputModuleSteamVR).GetMethod(nameof(InputModuleSteamVR.UpdateImportantInput), BindFlags.Public | BindFlags.Instance),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnUpdateInputSteamVR))));
            HarmonyInstance.Patch(
                typeof(InputModuleMouseKeyboard).GetMethod(nameof(InputModuleMouseKeyboard.UpdateInput), BindFlags.Public | BindFlags.Instance),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnUpdateInputDesktop))));
            HarmonyInstance.Patch(
                typeof(InputModuleMouseKeyboard).GetMethod(nameof(InputModuleMouseKeyboard.UpdateImportantInput), BindFlags.Public | BindFlags.Instance),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnUpdateInputDesktop))));


            // FIXME: this stops the avatar animator from moving too, but not ideal, cannot fly anymore or rotate head
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(MovementSystem).Update()),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnUpdateMovementSystem))));

            // close action menu when main menu opens
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(ViewManager).UiStateToggle(default)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnMainMenuToggle))));

            // cohtml reads files so let's install all that stuff, it's easier for everybody
            if (dontInstallResources.Value)
            {
                logger.Msg($"We won't install resource files as requested. Beware of updates though.");
            }
            else
            {
                Directory.CreateDirectory(couiPath);
                var couiNs = this.GetType().Namespace + ".UIResources";
                foreach (var fname in couiFiles)
                {
                    var raw = BytesFromAssembly(couiNs, fname);
                    if (raw == null)
                    {
                        logger.Warning($"File missing from assembly {fname}");
                        continue;
                    }
                    File.WriteAllBytes(couiPath + @"\" + fname, raw);
                }
                logger.Msg($"Installed {couiFiles.Length} resource files from assembly into {couiPath}");
            }
            MelonCoroutines.Start(WaitCohtmlSpawned());


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

        private static bool OnUpdateMovementSystem(ABI_RC.Core.Player.CVR_MovementSystem __instance)
        {
            if (!MetaPort.Instance.isUsingVr) return true;
            return cohtmlView?.enabled != true; // TODO: animation still run, prevent emotes
        }

        private static void OnMainMenuToggle(ViewManager __instance, bool show)
        {
            if (!show || cohtmlView?.enabled != true) return;

            instance.ToggleMenu(false);
        }

        private void MenuManagerRegisterEvents()
        {
            var view = cohtmlView.View;
            logger.Msg($"MenuManagerRegisterEvents called {view}");
            view.RegisterForEvent("ActionMenuReady", new Action(OnActionMenuReady));
            view.BindCall("CVRAppCallSystemCall", new Action<string, string, string, string, string>(menuManager.HandleSystemCall));
            view.BindCall("SetMelonPreference", new Action<string, string>(OnSetMelonPreference));
            view.BindCall("ItemCallback", new Action<string>(OnItemCallback));
            view.BindCall("ItemCallback_bool", new Action<string, bool>(OnItemCallback_bool));
            view.BindCall("ItemCallback_float", new Action<string, float>(OnItemCallback_float));
            view.BindCall("ItemCallback_float_float", new Action<string, float, float>(OnItemCallback_float_float));
            view.BindCall("RequestDynamicMenu", new Action<string>(OnRequestDynamicMenu));

            // TODO: adjust effect
            var material = menuTransform.GetComponent<MeshRenderer>().materials[0];
            material.SetTexture("_DesolvePattern", menuManager.pattern);
            material.SetTexture("_DesolveTiming", menuManager.timing);
            material.SetTextureScale("_DesolvePattern", new Vector2(1f, 1f));

            cohtmlReadyState = 1;
        }

        private System.Collections.IEnumerator WaitCohtmlSpawned()
        {
            GameObject cwv;
            CohtmlUISystem cohtmlUISystem;
            while ((cwv = GameObject.Find("/Cohtml/CohtmlWorldView")) == null)
                yield return null;
            while ((cohtmlUISystem = GameObject.Find("/Cohtml/CohtmlUISystem").GetComponent<CohtmlUISystem>()) == null)
                yield return null;
            logger.Msg($"WaitCohtmlSpawned start {cwv}");
            menuManager = CVR_MenuManager.Instance;

            var parent = cwv.transform.parent;
            var animator = cwv.GetComponent<Animator>().runtimeAnimatorController;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.SetActive(false);
            go.name = "ActionMenu";
            go.layer = LayerMask.NameToLayer("UI");

            var t = menuTransform = go.transform;
            t.SetParent(parent, false);
            var scale = MetaPort.Instance.isUsingVr ? 0.2f : 0.5f;
            t.localScale = new Vector3(scale, scale, scale);

            menuCollider = t.GetComponent<Collider>();

            var r = t.GetComponent<MeshRenderer>();
            r.sortingLayerID = 0;
            r.sortingOrder = 10;

            var a = menuAnimator = go.AddComponent<Animator>();
            a.runtimeAnimatorController = animator;
            a.SetBool("Open", true);

            var v = cohtmlView = go.AddComponent<CohtmlView>();
            v.Listener.ReadyForBindings += MenuManagerRegisterEvents;
            v.enabled = false;
            v.CohtmlUISystem = cohtmlUISystem;
            v.AutoFocus = false;
            v.IsTransparent = true;
            v.Width = 500;
            v.Height = 500;
            v.Page = couiUrl + "/index.html";

            // ready set go
            v.enabled = true;
            go.SetActive(true);
            UpdatePositionToAnchor();
        }

        private static void OnCVRCameraToggle(CVRCamController __instance)
        {
            if (cohtmlReadyState < 2) return; // not ready for events
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
            cohtmlView.View.TriggerEvent<string>("GameValueUpdate", JsonSerialize(u));
        }

        private static void OnCVRMicrophoneToggle(bool muted)
        {
            if (cohtmlReadyState < 2) return; // not ready for events
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
            cohtmlView.View.TriggerEvent<string>("GameValueUpdate", JsonSerialize(u));
        }

        private static void OnCVRSeatedToggle(PlayerSetup __instance)
        {
            if (cohtmlReadyState < 2) return; // not ready for events
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
            cohtmlView.View.TriggerEvent<string>("GameValueUpdate", JsonSerialize(u));
        }

        private static void OnCVRFlyToggle(MovementSystem __instance)
        {
            if (cohtmlReadyState < 2) return; // not ready for events
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
            cohtmlView.View.TriggerEvent<string>("GameValueUpdate", JsonSerialize(u));
        }

        [Serializable]
        internal struct InputData
        {
            public Vector2 joystick;
            public float trigger;
        }

        public void ToggleMenu(bool show)
        {
            logger.Msg($"ToggleMenu show={show} , cohtmlView.enabled={cohtmlView.enabled} collider={menuCollider?.enabled} vr={MetaPort.Instance.isUsingVr}");
            if (cohtmlView == null || cohtmlView.View == null) return;

            cohtmlView.View.TriggerEvent<bool>("ToggleActionMenu", show);
            cohtmlView.enabled = show; // TODO: doesn this reload cohtml each time? careful
            menuAnimator.SetBool("Open", show);

            var vr = !menuManager._desktopMouseMode || MetaPort.Instance.isUsingVr;
            var moveSys = PlayerSetup.Instance._movementSystem;

            if (show && menuCollider?.enabled == true)
                UpdatePositionToAnchor();

            if (vr)
            {
                //moveSys.SetImmobilized(show);
                //moveSys.canMove = !show; // TODO: this isn't enough, body animator still move
                //moveSys.canFly = !show;
            }
            else
            {
                moveSys.disableCameraControl = show;
                CVRInputManager.Instance.inputEnabled = !show;
                RootLogic.Instance.ToggleMouse(show);
                menuManager.desktopControllerRay.enabled = !show;
            }
        }

        private static void OnUpdateInputSteamVR(InputModuleSteamVR __instance)
        {
            if (__instance.vrMenuButton == null) return; // cvr calls this even in desktop, doh

            instance.OnUpdateInput(
                __instance.vrMenuButton.GetStateDown(SteamVR_Input_Sources.LeftHand),
                __instance.vrMenuButton.GetStateUp(SteamVR_Input_Sources.LeftHand));
            // TODO: add support for right hand too
        }

        private static void OnUpdateInputDesktop(InputModuleMouseKeyboard __instance)
        {
            instance.OnUpdateInput(
                Input.GetKeyDown(KeyCode.Tab),
                Input.GetKeyUp(KeyCode.Tab));
        }

        private static float qmButtonStart = -1;
        private static readonly float actionMenuShowHoldDuration = 0.5f;
        private void ShortPressMenuToggle(bool show)
        {
            if (quickMenuLongPress.Value) ToggleMenu(show);
            else menuManager.ToggleQuickMenu(show);
        }
        private void LongPressMenuToggle(bool show) {
            if (quickMenuLongPress.Value) menuManager.ToggleQuickMenu(show);
            else ToggleMenu(show);
        }
        private void OnUpdateInput(bool buttonDown, bool buttonUp)
        {
            if (cohtmlView == null || menuManager == null) return; // not yet ready

            var now = Time.time;
            var im = CVRInputManager.Instance;
            var amOpen = cohtmlView?.enabled == true;
            var qmOpen = menuManager._quickMenuOpen;
            var shortPressMenuShown = quickMenuLongPress.Value ? amOpen : qmOpen;
            var longPressMenuShown = quickMenuLongPress.Value ? qmOpen : amOpen;

            im.quickMenuButton = false; // override the default behavior, always
            im.quickMenuButtonHold = false; // TODO: allow people to use it still

            if (buttonUp)
            {
                if (qmButtonStart >= 0)
                    ShortPressMenuToggle(!shortPressMenuShown);
                qmButtonStart = -1;
            }
            else if (buttonDown) // ignore if quickmenu is open
            {
                if (longPressMenuShown)
                    LongPressMenuToggle(false);
                else if (shortPressMenuShown)
                    ShortPressMenuToggle(false);
                else
                    qmButtonStart = now;
            }
            else if (qmButtonStart >= 0 && !longPressMenuShown) // holding
            {
                if (now - qmButtonStart >= actionMenuShowHoldDuration)
                {
                    LongPressMenuToggle(true);
                    qmButtonStart = -1; // prevent other menu to pop
                }
            }
        }

        public override void OnLateUpdate()
        {
            if (menuManager == null || menuTransform == null) return;
            if (menuManager._inputManager == null)
                menuManager._inputManager = CVRInputManager.Instance;

            if (cohtmlView?.enabled != true || cohtmlView?.View == null) return;

            var joystick = Vector2.zero;
            var trigger = 0f;
            if (menuManager._desktopMouseMode && !MetaPort.Instance.isUsingVr) // Desktop mode
            {
                if (menuManager._camera == null)
                    menuManager._camera = PlayerSetup.Instance.desktopCamera.GetComponent<Camera>();

                RaycastHit hitInfo;
                if (menuCollider.Raycast(menuManager._camera.ScreenPointToRay(Input.mousePosition), out hitInfo, 1000f))
                {
                    var coord = hitInfo.textureCoord;
                    joystick = new Vector2(coord.x * 2 - 1, coord.y * 2 - 1);
                }
                trigger = Input.GetMouseButtonDown(0) ? 1 : 0; // do we need button up anyway?
                UpdatePositionToDesktopAnchor();
            }
            else
            {
                if (menuCollider.enabled)
                {
                    UpdatePositionToVrAnchor();
                }

                var movVect = menuManager._inputManager.movementVector;
                joystick = new Vector2(movVect.x, movVect.z); // y is 0 and irrelevant
                trigger = menuManager._inputManager.interactLeftValue; // TODO: auto detect side
            }

            if (cohtmlReadyState < 2) return;
            var view = cohtmlView.View;
            var data = new InputData {
                joystick = joystick,
                trigger = trigger,
            };
            view.TriggerEvent<string>("InputData", JsonUtility.ToJson(data));
        }

        private void UpdatePositionToAnchor()
        {
            if (MetaPort.Instance.isUsingVr)
                UpdatePositionToVrAnchor();
            else
                UpdatePositionToDesktopAnchor();
        }

        private void UpdatePositionToDesktopAnchor()
        {
            if (cohtmlReadyState < 1) return;
            Transform rotationPivot = PlayerSetup.Instance._movementSystem.rotationPivot;
            menuTransform.eulerAngles = rotationPivot.eulerAngles;
            menuTransform.position = rotationPivot.position + rotationPivot.forward * 1f;
        }

        private void UpdatePositionToVrAnchor()
        {
            if (cohtmlReadyState < 1) return;
            // TODO: auto detect left or right anchor
            var anch = menuManager._leftVrAnchor.transform;
            menuTransform.position = anch.position;
            menuTransform.rotation = anch.rotation;
        }

        private Menus? melonPrefsMenus;
        private void BuildOurMelonPrefsMenus()
        {
            melonPrefsMenus = ourLib.BuildMelonPrefsMenus(melonPrefs.Entries);
        }

        // Build all menu hierarchy found in names (hierarchy separator character)
        // Example if there are items: Clothing/Dress/Long and Clothing/Dress/Short then the items themselves will be moved into new nested menus Clothing > Dress
        public static Menus ReifyHierarchyInNames(Menus menus)
        {
            var m = new Menus(); // we'll rebuild the menu from scratch, ensuring it's clean
            var hierarchyPairs = new HashSet<(string parent, string child)>();

            foreach (var x in menus)
            {
                foreach (var item in x.Value)
                {
                    // split basename from parents, like uri or paths
                    var name = item.name;
                    var parents = x.Key;
                    var i = name.LastIndexOf(HierarchySep);
                    if (i >= 0) { // is in a folder
                        parents += HierarchySep + item.name.Substring(0, i);
                        name = name.Substring(i + 1);
                    }

                    var item2 = item;
                    item2.name = name;
                    var aitems = m.GetWithDefault(parents);
                    aitems.Add(item2);

                    // register the hierarchy upward
                    var hierarchy = parents.Split(HierarchySep);
                    var child = hierarchy[0];
                    for (var j = 1; j < hierarchy.Length; ++j) {
                        var parent = child;
                        child = parent + HierarchySep + hierarchy[j];
                        hierarchyPairs.Add((parent, child));
                    }
                }
            }

            // now build the hierarchy, non-leaves up to the root menu (hierarchy of menus)
            foreach (var x in hierarchyPairs)
            {
                var parent = x.parent;
                var child = x.child;
                var i = child.LastIndexOf(HierarchySep);
                var childName = (i >= 0) ? x.child.Substring(i + 1) : child;

                var item = new MenuItem
                {
                    name = childName,
                    action = new ItemAction
                    {
                        type = "menu",
                        menu = child,
                    },
                };
                m.GetWithDefault(parent).Add(item);
            }

            return m;
        }

        public static Menus AvatarAdvancedSettingsToMenus(List<CVRAdvancedSettingsEntry> advSettings, Animator animator, string menuPrefix)
        {
            var m = new Menus();

            // Build menus from the avatar parameters, focus on items directly (leaves in the hierarchy)
            logger.Msg($"OnAvatarAdvancedSettings {advSettings.Count} items");
            foreach (var s in advSettings)
            {
                switch (AvatarParamToItem(s, animator, menuPrefix, m))
                {
                    case null:
                        continue;

                    case MenuItem item:
                        item.name = s.name;
                        logger.Msg($"OnAvatarAdvancedSettings parameter {item.name}: {s.type}");
                        var aitems = m.GetWithDefault(menuPrefix);
                        aitems.Add(item);
                        break;
                }
            }

            return ReifyHierarchyInNames(m);
        }

        public static Menus SplitOverCrowdedMenus(Menus m, uint overcrowdedMenuValue = 7)
        {
            var pagePrefix = "page-";
            var keys = m.Keys.OrderBy(n => -n.Length).ToArray(); // process bottom-up (deepest first)
            foreach (var parents in keys)
            {
                var items = m[parents];
                if (items.Count <= overcrowdedMenuValue) continue;

                // first create new submenu for each page with the crowded items
                var page = 1;
                var pageItems = 0;
                foreach (var item in items)
                {
                    if (pageItems >= overcrowdedMenuValue)
                    {
                        ++page;
                        pageItems = 0;
                    }

                    m.GetWithDefault(Path(parents, pagePrefix + page)).Add(item);
                    ++pageItems;
                };

                // now create links to the submenu in the parent
                items.Clear();
                for (var i = 1; i <= page; ++i)
                {
                    items.Add(new MenuItem()
                    {
                        name = $"Page {i}",
                        action = new ItemAction { type = "menu", menu = Path(parents, pagePrefix + i) },
                    });
                }
            }

            return m;
        }

        private static Menus? avatarMenus;
        private static void OnAvatarAdvancedSettings(PlayerSetup __instance)
        {
            var animator = __instance._animator;
            var advSettings = __instance._avatarDescriptor.avatarSettings.settings;
            var menuPrefix = AvatarMenuPrefix;
            var m = avatarMenus = AvatarAdvancedSettingsToMenus(advSettings, animator, menuPrefix);

            if (instance.splitAvatarOvercrowdedMenu.Value)
                m = SplitOverCrowdedMenus(m);

            // add avatar emotes
            var emoteNames = __instance.GetEmoteNames();
            if (emoteNames.Length > 0)
            {
                var parents = Path(menuPrefix, "emotes");
                var aitems = m.GetWithDefault(parents);
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

                m.GetWithDefault(menuPrefix).Add(new MenuItem()
                {
                    name = "Emotes",
                    icon = "icon_avatar_emotes.svg",
                    action = new() { type = "menu", menu = parents },
                });
            }
            // TODO: add cvr avatar ToggleState?

            // add avatar advanced settings profiles
            var profilesNames = __instance.getCurrentAvatarSettingsProfiles().ToList();
            if (profilesNames.Count > 0)
            {
                profilesNames.Add("default"); // there is an implicit default in cvr
                var parents = Path(menuPrefix, "profiles");
                var aitems = m.GetWithDefault(parents);
                profilesNames.Do(name =>
                {
                    logger.Msg($"OnAvatarAdvancedSettings profiles {name} <- {parents}");
                    var item = new MenuItem
                    {
                        name = name,
                        action = new ItemAction
                        {
                            type = "system call",
                            event_ = "AppChangeAvatarProfile",
                            // TODO: the avatar items should reload all the parameters values
                            event_arguments = new string[] { name },
                            exclusive_option = true,
                            toggle = true,
                        },
                    };
                    aitems.Add(item);
                });

                m.GetWithDefault(menuPrefix).Add(new MenuItem()
                {
                    name = "Profiles",
                    icon = "icon_avatar_settings_profile.svg",
                    action = new() { type = "menu", menu = parents },
                });
            }

            // let mods modify the avatar menu
            var avatarGuid = __instance?._avatarDescriptor?.avatarSettings?._avatarGuid ?? "default";
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
                    patch.ApplyToMenu(m);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error while reading avatar overrides json for avatar {avatarGuid}: {ex}");
                }
            }

            instance.OnActionMenuReady(); // reload
        }

        public static MenuItem? AvatarParamToItem(CVRAdvancedSettingsEntry s, Animator animator,
            // used in case of nested menus like drop down
            string menuPrefix, Menus m)
        {
            var item = new MenuItem();

            // build the items in the menu
            switch (s.type)
            {
                case SettingsType.GameObjectToggle: {
                    item.enabled = animator?.GetBool(s.machineName) ?? s.toggleSettings?.defaultValue ?? false;
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = "toggle",
                        value = 1f,
                    };
                    break;
                }

                case SettingsType.GameObjectDropdown: {
                    var submenuName = Path(menuPrefix, s.name);
                    // if parameter name has suffix Impulse, adapt control type
                    var isImpulse = s.machineName.EndsWith("Impulse");
                    var dd = s.dropDownSettings;
                    var selectedValue = animator?.GetInteger(s.machineName) ?? dd?.defaultValue;

                    item.action = new ItemAction
                    {
                        type = "menu",
                        menu = submenuName,
                    };

                    List<MenuItem> sitems = dd.options.Select((o, index) => new MenuItem
                    {
                        name = o.name,
                        enabled = index == selectedValue,
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
                }

                case SettingsType.Slider: {
                    var defaultValue = animator?.GetFloat(s.machineName) ?? s.sliderSettings?.defaultValue;
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = "radial",
                        default_value = defaultValue,
                        min_value = 0.0f,
                        max_value = 1.0f,
                    };
                    break;
                }

                case SettingsType.InputSingle: {
                    var defaultValue = animator?.GetFloat(s.machineName) ?? s.inputSingleSettings?.defaultValue;
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = "radial",
                        default_value = defaultValue,
                        // TODO: we're guessing here, we should allow to override somewhere
                        min_value = 0.0f,
                        max_value = 1.0f,
                    };
                    break;
                }

                case SettingsType.Joystick2D: {
                    var sjoy = s.joystick2DSetting;
                    var defaultValueX = animator?.GetFloat(s.machineName + "-x") ?? sjoy.defaultValue.x;
                    var defaultValueY = animator?.GetFloat(s.machineName + "-y") ?? sjoy.defaultValue.y;
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = "joystick_2d",
                        min_value_x = 0.0f, // TODO: cvr seems to ignore min/max defined in unity, sad :(
                        max_value_x = 1.0f,
                        default_value_x = defaultValueX,
                        min_value_y = 0.0f,
                        max_value_y = 1.0f,
                        default_value_y = defaultValueY,
                    };
                    break;
                }

                case SettingsType.InputVector2: {
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
                }

                case SettingsType.MaterialColor:
                case SettingsType.Joystick3D:
                case SettingsType.InputVector3:
                    logger.Msg($"Avatar parameter {s.name} ignored, its type {s.type} is not supported yet");
                    return null; // TODO: unsupported
            };

            return item;
        }

        private void OnActionMenuReady()
        {
            var view = cohtmlView.View;
            logger.Msg($"OnActionMenuReady for view {view}");
            var fromFile = File.ReadAllText(@"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\ActionMenu\actionmenu.json");
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
            var globalOverridesFiles = TryOrDefault(() => Directory.GetFiles(@"UserData\ActionMenu\GlobalOverrides"));
            globalOverridesFiles?
                .Where(fpath => fpath.EndsWith(".json"))
                .Do(fpath =>
            {
                try
                {
                    var txt = File.ReadAllText(fpath);
                    var patch = JsonConvert.DeserializeObject<MenusPatch>(txt);
                    patch.ApplyToMenu(config.menus);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error while reading global overrides json {fpath}: {ex}");
                }
            });

            API.InvokeOnGlobalMenuLoaded(config.menus);

            // add item to link to all mods menu if any is present
            if (config.menus.GetWithDefault(SubmenuNameForMods).Count > 0)
            {
                config.menus.GetWithDefault(MainMenu).Add(new MenuItem()
                {
                    name = "Mods",
                    icon = "icon_melon.svg",
                    action = new ItemAction() { type = "menu", menu = SubmenuNameForMods },
                });
            }

            var settings = new MenuSettings
            {
                in_vr = MetaPort.Instance.isUsingVr,
                flick_selection = flickSelection.Value,
                boring_back_button = boringBackButton.Value,
            };

            var configTxt = JsonSerialize(config);
            var settingsTxt = JsonSerialize(settings);
            if (cohtmlReadyState > 0) {
                view.TriggerEvent<string, string>("LoadActionMenu", configTxt, settingsTxt);
                cohtmlReadyState = 2;
                cohtmlView.enabled = false; // hide it by default
            }
        }

        internal struct MenuSettings
        {
            public bool in_vr;
            public bool flick_selection;
            public bool boring_back_button;
        }

        private static string JsonSerialize(object value) => JsonConvert.SerializeObject(value, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        private void Reload()
        {
            if (cohtmlView == null)
            {
                logger.Warning($"Reload view is null!");
                return;
            }
            cohtmlReadyState = 0;
            OnAvatarAdvancedSettings(PlayerSetup.Instance);
            cohtmlView.View.Reload(); // TODO: reloading is broken, this fix?

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

        private void OnItemCallback(string identifier)
        {
            Action f;
            if (!callbackItems.TryGetValue(identifier, out f) || f == null)
            {
                logger.Error($"didn't find callback {identifier}");
                return;
            }

            logger.Msg($"OnItemCallback calling {identifier}: {f}"); // TODO debug
            try { f(); }
            catch (Exception e) { logger.Error($"failure in callback {identifier}: {e}"); }
        }

        private void OnItemCallback_bool(string identifier, bool value)
        {
            Action<bool> f;
            if (!callbackItems_bool.TryGetValue(identifier, out f) || f == null)
            {
                logger.Error($"didn't find callback {identifier}");
                return;
            }

            try { f(value); }
            catch (Exception e) { logger.Error($"failure in callback {identifier}: {e}"); }
        }

        private void OnItemCallback_float(string identifier, float value)
        {
            Action<float> f;
            if (!callbackItems_float.TryGetValue(identifier, out f) || f == null)
            {
                logger.Error($"didn't find callback {identifier}");
                return;
            }

            try { f(value); }
            catch (Exception e) { logger.Error($"failure in callback {identifier}: {e}"); }
        }

        private void OnItemCallback_float_float(string identifier, float x, float y)
        {
            Action<float, float> f;
            if (!callbackItems_float_float.TryGetValue(identifier, out f) || f == null)
            {
                logger.Error($"didn't find callback {identifier}");
                return;
            }

            try { f(x, y); }
            catch (Exception e) { logger.Error($"failure in callback {identifier}: {e}"); }
        }

        private void OnRequestDynamicMenu(string identifier)
        {
            MenuBuilder f;
            if (!dynamic_menus.TryGetValue(identifier, out f) || f == null)
            {
                logger.Error($"didn't find builder {identifier}");
                return;
            }

            logger.Msg($"OnRequestDynamicMenu calling {identifier}: {f}"); // TODO debug
            try
            {
                var items = f();
                var menus = new Menus { [identifier] = items };
                cohtmlView.View.TriggerEvent<string>("DynamicMenuData", JsonSerialize(menus));
            }
            catch (Exception e)
            {
                logger.Error($"failure in callback: {e}");
            }
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F3)) Reload(); // reload

            if (menuTransform != null) UpdatePositionToVrAnchor();
        }

        internal class OurLib : Lib
        {
            override protected void RegisterOnLoaded() { } // we don't need it ourself
        }
    }
}
