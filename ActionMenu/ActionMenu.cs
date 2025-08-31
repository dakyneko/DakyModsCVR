using HarmonyLib;
using MelonLoader;
using UnityEngine;
using ABI_RC.Core.Savior;
using ABI_RC.Core.InteractionSystem;
using ABI.CCK.Scripts;
using System.Collections.Generic;
using System.Linq;
using cohtml;
using System;
using Newtonsoft.Json;
using System.IO;
using Valve.VR;
using VRBinding;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Core.UI;
using ABI_RC.Core.UI.UIRework.Managers;

using PlayerSetup = ABI_RC.Core.Player.PlayerSetup;
using SettingsType = ABI.CCK.Scripts.CVRAdvancedSettingsEntry.SettingsType;
using MovementSystem = ABI_RC.Systems.Movement.BetterBetterCharacterController;

[assembly:MelonGame(null, "ChilloutVR")]
[assembly:MelonInfo(typeof(ActionMenu.ActionMenuMod), "Action Menu", "1.1.13", "daky", "https://github.com/dakyneko/DakyModsCVR")]
[assembly:MelonAdditionalDependencies("VRBinding")]

namespace ActionMenu
{
    using static Daky.Dakytils;
    using MenuBuilder = Func<List<MenuItem>>;
    public class ActionMenuMod : MelonMod
    {
        /// <summary>
        /// Public library for all mods to use, you can extend this
        /// </summary>
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

            // auto-register your mod to create menus
            // if you don't need this, you can override it empty
            virtual protected void RegisterOnLoaded()
            {
                API.OnGlobalMenuLoaded += OnGlobalMenuLoaded;
                API.OnAvatarMenuLoaded += OnAvatarMenuLoaded;
            }

            virtual protected string modName => prefixNs; // by default your class name
            virtual protected string? modIcon => null; // accept: URLs, on-disk-path or inlined data:image/jpeg;base64…
            virtual protected string entry => "main"; // the name of your mod main menu, don't change unless you know what you're doing
            virtual protected string ModMenuPath(params string[] components)
                => prefixNs + hierarchySeparator + string.Join(hierarchySeparator.ToString(), components);

            // Mods can create a menu very easily by filling up some items here.
            virtual protected List<MenuItem> modMenuItems() => new();

            // Or if you need finer control, you will need to do more yourself
            // Create many menus yourself for your mod. Your menus will have automatically the prefixNs prefix.
            virtual public Menus BuildModMenu()
            {
                return new()
                {
                    // the main menu of the mod
                    [entry] = modMenuItems(),
                    // more submenus can be added separately here
                };
            }

            /// <summary>
            /// Or even finer control, total control but it comes with responsability: pick unique names for your menus!
            /// Override this to manipulate/add menus after all other menus are built. Usually not required for simple mod menus.
            /// </summary>
            /// <param name="menus"></param>
            virtual protected void OnGlobalMenuLoaded(Menus menus)
            {
                // the default behavior below makes modders' life easier and prevents collision between mods
                var m = BuildModMenu();
                if (m.Keys.SequenceEqual(new string[] { entry }) && m.GetWithDefault(entry).Count == 0)
                    return; // if empty don't create anything

                // add all mod menus and take care of the prefixNs prefix
                var menuPrefix = ModMenuPath();
                foreach (var x in m) {
                    var items = x.Value.Select(item =>
                    {
                        // when refer to other menu, add prefix but only if it's missing
                        if (item.action.type == "menu" && !item.action.menu.StartsWith(menuPrefix))
                            item.action.menu = ModMenuPath(item.action.menu);
                        return item;
                    });
                    // we check if prefix is already added by chance, only add if missing
                    var menuWithPrefix = !x.Key.StartsWith(menuPrefix) ? ModMenuPath(x.Key) : x.Key;
                    menus.GetWithDefault(menuWithPrefix).AddRange(items);
                }
                ModsMainMenu(menus).Add(new MenuItem()
                {
                    name = modName,
                    icon = modIcon,
                    action = new ItemAction() { type = "menu", menu = ModMenuPath(entry) },
                });
            }

            public static List<MenuItem> ModsMainMenu(Menus menus) => menus.GetWithDefault(modsMenuName);

            /// <summary>
            /// override this to manipulate avatar menus after they're built
            /// </summary>
            /// <param name="avatarGuid"></param>
            /// <param name="menus"></param>
            virtual protected void OnAvatarMenuLoaded(string avatarGuid, Menus menus)
            {
            }

            /// <summary>
            /// Nice way to edit menus by applying patches. This is to play nice with other mods as well.
            /// </summary>
            /// <param name="menus"></param>
            /// <param name="patch"></param>
            public static void ApplyMenuPatch(Menus menus, MenusPatch patch) => patch.ApplyToMenu(menus);

            private int uniqueItemId = 0;
            private string nextUniqueItemId(string suffix) => $"item_{uniqueItemId++}_{suffix}";

            // New easier API to create MenuItem with ItemAction in 1-call
            public MenuItem Button(string label, Action callback, bool? enabled = null, string? icon = null, bool exclusiveOption = false)
            {
                return new MenuItem(label, icon, BuildButtonItem(nextUniqueItemId("button"), callback, exclusiveOption: exclusiveOption), enabled: enabled);
            }
            public MenuItem Toggle(string label, Action<bool> callback, bool? enabled = null, string? icon = null)
            {
                return new MenuItem(label, icon, BuildToggleItem(nextUniqueItemId(label), callback), enabled: enabled);
            }
            public MenuItem Impulse(string label, Action<bool> callback, float duration = 1f, object? value = null, string? icon = null)
            {
                return new MenuItem(label, icon, BuildImpulseItem(nextUniqueItemId(label), callback, duration: duration, value: value));
            }
            public MenuItem Radial(string label, Action<float> callback, string? icon = null,
                float? minValue = null, float? maxValue = null, float? defaultValue = null)
            {
                return new MenuItem(label, icon, BuildRadialItem(nextUniqueItemId("radial"), callback,
                    minValue: minValue, maxValue: maxValue, defaultValue: defaultValue));
            }
            public MenuItem Menu(string label, MenuBuilder menuBuilder, string? icon = null)
            {
                return new MenuItem(label, icon, BuildCallbackMenu(nextUniqueItemId("menu"), menuBuilder));
            }
            public MenuItem Joystick2D(string label, Action<float, float> callback, string? icon = null,
                float? minValueX = null, float? maxValueX = null, float? defaultValueX = null,
                float? minValueY = null, float? maxValueY = null, float? defaultValueY = null)
            {
                return new MenuItem(label, icon, BuildJoystick2D(nextUniqueItemId("joystick2d"), callback,
                minValueX: minValueX, maxValueX: maxValueX, defaultValueX: defaultValueX,
                minValueY: minValueY, maxValueY: maxValueY, defaultValueY: defaultValueY));
            }
            public MenuItem InputVector2D(string label, Action<float, float> callback, string? icon = null,
                float? minValueX = null, float? maxValueX = null, float? defaultValueX = null,
                float? minValueY = null, float? maxValueY = null, float? defaultValueY = null)
            {
                return new MenuItem(label, icon, BuildInputVector2D(nextUniqueItemId("vector2d"), callback,
                minValueX: minValueX, maxValueX: maxValueX, defaultValueX: defaultValueX,
                minValueY: minValueY, maxValueY: maxValueY, defaultValueY: defaultValueY));
            }

            // below is older API where you need to build more stuff yourself

            /// <summary>
            /// Create an ItemAction when triggered, will call your function
            /// Basically for button/widget item for callback in your mod
            /// </summary>
            /// <param name="name"></param>
            /// <param name="callback"></param>
            /// <param name="exclusiveOption"></param>
            /// <returns></returns>
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
                object? value = null, float? duration = null)
            {
                var identifier = prefixNs + "." + control + "." + name;
                instance.callbackItems_bool[identifier] = callback;
                return new ItemAction()
                {
                    type = "callback",
                    parameter = identifier,
                    control = control,
                    toggle = true,
                    duration = duration,
                    value = value,
                };
            }

            /// <summary>
            /// Creates a button with two states: enabled and disabled.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="callback"></param>
            /// <returns></returns>
            public ItemAction BuildToggleItem(string name, Action<bool> callback)
                => BuildBoolItem(name, callback, "toggle", value: true);
            /// <summary>
            /// Creates a button that temporarily switch its value, then back to default_value
            /// </summary>
            /// <param name="name"></param>
            /// <param name="callback"></param>
            /// <param name="duration"></param>
            /// <param name="value"></param>
            /// <returns></returns>
            public ItemAction BuildImpulseItem(string name, Action<bool> callback, float duration = 1f, object? value = null)
                => BuildBoolItem(name, callback, "impulse", value: value, duration: duration);

            /// <summary>
            /// Creates a radial widget for picking values between a min and max.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="callback"></param>
            /// <param name="minValue"></param>
            /// <param name="maxValue"></param>
            /// <param name="defaultValue"></param>
            /// <returns></returns>
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

            /// <summary>
            /// Creates a 2D widget for picking two values simultaneously setting absolute coordinates, between min and max values.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="callback"></param>
            /// <param name="minValueX"></param>
            /// <param name="maxValueX"></param>
            /// <param name="defaultValueX"></param>
            /// <param name="minValueY"></param>
            /// <param name="maxValueY"></param>
            /// <param name="defaultValueY"></param>
            /// <returns></returns>
            public ItemAction BuildJoystick2D(string name, Action<float, float> callback,
                float? minValueX = null, float? maxValueX = null, float? defaultValueX = null,
                float? minValueY = null, float? maxValueY = null, float? defaultValueY = null)
                => Build2DItem(name, callback, "joystick_2d",
                        minValueX: minValueX, maxValueX: maxValueX, defaultValueX: defaultValueX,
                        minValueY: minValueY, maxValueY: maxValueY, defaultValueY: defaultValueY
                    );
            /// <summary>
            /// Creates a 2D widget for picking two values simultaneously moving in relative coordinates, between min and max values.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="callback"></param>
            /// <param name="minValueX"></param>
            /// <param name="maxValueX"></param>
            /// <param name="defaultValueX"></param>
            /// <param name="minValueY"></param>
            /// <param name="maxValueY"></param>
            /// <param name="defaultValueY"></param>
            /// <returns></returns>
            public ItemAction BuildInputVector2D(string name, Action<float, float> callback,
                float? minValueX = null, float? maxValueX = null, float? defaultValueX = null,
                float? minValueY = null, float? maxValueY = null, float? defaultValueY = null)
                => Build2DItem(name, callback, "input_vector_2d",
                        minValueX: minValueX, maxValueX: maxValueX, defaultValueX: defaultValueX,
                        minValueY: minValueY, maxValueY: maxValueY, defaultValueY: defaultValueY
                    );

            /// <summary>
            /// Create an ItemAction when triggered, will call you back so you can build your own menu dynamically
            /// Basically building dynamic menus from a mod
            /// </summary>
            /// <param name="name"></param>
            /// <param name="menuBuilder"></param>
            /// <returns></returns>
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

            /// <summary>
            /// if you want to expose your MelonPreference into a menu, build them with this.
            /// beware that it's still rudimentary: toggles are fine but floats are between 0 and 1, that's all
            /// to refer it, look below at the path, it has prefix, example: YourNameMod/settings
            /// </summary>
            /// <param name="melonPrefs"></param>
            /// <returns></returns>
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
                                enabled = e.Value,
                                action = new()
                                {
                                    type = "set melon preference",
                                    control = "toggle",
                                    parameter = e.Identifier,
                                    toggle = true,
                                }
                            });
                            break;

                        case MelonPreferences_Entry<float> e:
                            items.Add(new MenuItem()
                            {
                                name = e.DisplayName,
                                action = new()
                                {
                                    type = "set melon preference",
                                    control = "radial",
                                    parameter = e.Identifier,
                                    default_value = e.Value,
                                }
                            });
                            break;

                        case MelonPreferences_Entry<Vector2> e:
                            var v = e.Value;
                            items.Add(new MenuItem()
                            {
                                name = e.DisplayName,
                                action = new()
                                {
                                    type = "set melon preference",
                                    control = "input_vector_2d",
                                    parameter = e.Identifier,
                                    default_value_x = v.x,
                                    default_value_y = v.y,
                                }
                            });
                            break;

                        // TODO: add support for enum and other types
                        default:
                            logger.Warning($"BuildMelonPrefsMenus {e_.Identifier} unsupported type {e_.GetReflectedType()}");
                            break;
                    }
                }

                return m;
            }
        }

        // various parameters for consistency everywhere
        public static readonly string mainMenuName = "main";
        public static readonly string modsMenuName = "mods";
        public static readonly string avatarMenuName = "avatar";
        public static readonly char hierarchySeparator = '/'; // mostly for ReifyHierarchyInNames (avatar menus)
        public static readonly string couiPath = @"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\ActionMenu";
        public static readonly string couiUrl = "coui://UIResources/ActionMenu";
        public static readonly Vector2Int canvasSize = new Vector2Int(500, 500);
        public static readonly float menuBaseSizeDesktop = 1f;
        public static readonly float menuBaseSizeVr = 0.6f;
        public static readonly string[] couiFiles = new string[]
        {
            "index.html", "index.js", "index.css", "actionmenu.json", "Montserrat-Regular.ttf",
            "icon_actionmenu.svg", "icon_menu.svg", "icon_back.svg", "icon_avatar_emotes.svg", "icon_melon.svg",
            "icon_avatar_settings_profile.svg",
        };

        // Show or hide the Action menu, use it cautiously
        public static void Toggle(bool show) => instance.ToggleMenu(show);
        // Build the path of hierarchy of submenu
        public static string Path(params string[] components) => string.Join(hierarchySeparator.ToString(), components);

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
                    var i = name.LastIndexOf(hierarchySeparator);
                    if (i >= 0) { // is in a folder
                        parents += hierarchySeparator + item.name.Substring(0, i);
                        name = name.Substring(i + 1);
                    }

                    var item2 = item;
                    item2.name = name;
                    var aitems = m.GetWithDefault(parents);
                    aitems.Add(item2);

                    // register the hierarchy upward
                    var hierarchy = parents.Split(hierarchySeparator);
                    var child = hierarchy[0];
                    for (var j = 1; j < hierarchy.Length; ++j) {
                        var parent = child;
                        child = parent + hierarchySeparator + hierarchy[j];
                        hierarchyPairs.Add((parent, child));
                    }
                }
            }

            // now build the hierarchy, non-leaves up to the root menu (hierarchy of menus)
            foreach (var x in hierarchyPairs)
            {
                var parent = x.parent;
                var child = x.child;
                var i = child.LastIndexOf(hierarchySeparator);
                var childName = (i >= 0) ? x.child.Substring(i + 1) : child;

                var aitems = m.GetWithDefault(parent);
                // first check if the link to this menu was already created
                // for ex: avatar drop down created by AvatarParamToItem already add a reference to its menu
                if (aitems.Any(item => item.action.menu == child)) continue;

                var item = new MenuItem
                {
                    name = childName,
                    action = new ItemAction
                    {
                        type = "menu",
                        menu = child,
                    },
                };
                aitems.Add(item);
            }

            return m;
        }

        // Allows to update the state of any items having those item action
        // especially useful for toggle where the state is changed externally, use this.
        public static void UpdateItemState(ItemAction action) => instance.SendItemUpdate(action);

        public static Menus AvatarAdvancedSettingsToMenus(List<CVRAdvancedSettingsEntry> advSettings, Animator animator, string menuPrefix)
        {
            var m = new Menus();

            if (advSettings == null || advSettings.Count == 0)
                return m;

            // Build menus from the avatar parameters, focus on items directly (leaves in the hierarchy)
            foreach (var s in advSettings)
            {
                switch (AvatarParamToItem(s, animator, menuPrefix, m))
                {
                    case null:
                        continue;

                    case MenuItem item:
                        item.name = s.name;
#if DEBUG
                        logger.Msg($"OnAvatarAdvancedSettings parameter {item.name}: {s.type}");
#endif
                        var aitems = m.GetWithDefault(menuPrefix);
                        aitems.Add(item);
                        break;
                }
            }

            return ReifyHierarchyInNames(m);
        }

        public static Menus SplitOverCrowdedMenus(Menus m, uint overcrowdedMenuValue = 8)
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

        public static MenuItem? AvatarParamToItem(CVRAdvancedSettingsEntry s, Animator animator,
            // Menus is used in case of nested menus like drop down
            string menuPrefix, Menus m)
        {
            if (s.name.EndsWith("<hidden>")) return null;

            var item = new MenuItem();

            // build the items in the menu
            switch (s.type)
            {
                case SettingsType.GameObjectToggle: {
                    var (isImpulse, duration) = ParseImpulseTag(s.machineName);
                    item.enabled = animator?.GetBool(s.machineName) ?? s.toggleSettings?.defaultValue ?? false;
                    item.action = new ItemAction
                    {
                        type = "avatar parameter",
                        parameter = s.machineName,
                        control = isImpulse ? "impulse" : "toggle",
                        duration = duration,
                        value = 1f,
                    };
                    break;
                }

                case SettingsType.GameObjectDropdown: {
                    var submenuName = Path(menuPrefix, s.name);
                    // if parameter name has suffix Impulse, adapt control type
                    var (isImpulse, duration) = ParseImpulseTag(s.machineName);
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
                            duration = duration,
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
                    logger.Warning($"Avatar parameter {s.name} ignored, its type {s.type} is not supported yet");
                    return null; // TODO: unsupported
            };

            return item;
        }

        private static readonly System.Text.RegularExpressions.Regex impulseTagRegex = new System.Text.RegularExpressions.Regex(
            @"\s*<impulse(?:=(?<duration>\d+(?:\.?\d*)))>$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled
            );

        private static (bool isImpulse, float? duration) ParseImpulseTag(string machineName)
        {
            if (machineName.EndsWith("Impulse")) return (isImpulse: true, duration: null);
            var match = impulseTagRegex.Match(machineName);
            if (!match.Success) return (isImpulse: false, duration: null);
            var durationStr = match.Groups["duration"].Value;
            float? duration = null;
            if (durationStr != string.Empty)
                if (float.TryParse(durationStr, out var value))
                    duration = value;
                else
                    logger.Warning($"Avatar parameter {machineName} is impulse but invalid duration format: {durationStr}");
            return (isImpulse: true, duration: duration);
        }


        // ---------------------------
        // WARNING: implementation follows, all stuff below are private and shouldn't be used if possible, please
        // ---------------------------
        private static ActionMenuMod instance;
        private static MelonLogger.Instance logger;
        private static Transform menuTransform;
        private static CohtmlView cohtmlView;
        private static Collider menuCollider;
        private static CVR_MenuManager menuManager;
        private static Animator menuAnimator;
        private static int cohtmlReadyState = 0; // 0=startup, 1=binding ready, 2=ActionMenuReady
        private static bool anchorToLeftHand = true;
        private static Lib ourLib;

        // our melon prefs
        private MelonPreferences_Category melonPrefs;
        private Dictionary<string, MelonPreferences_Entry> melonPrefsMap;
        private MelonPreferences_Entry<bool> flickSelection, boringBackButton, dontInstallResources,
            splitAvatarOvercrowdedMenu;
        private MelonPreferences_Entry<float> menuSize;
        private MelonPreferences_Entry<Vector2> menuPositionOffset, menuRotationXY;
        private MelonPreferences_Entry<KeyCode> openKeyBinding, reloadKeyBinding;

        // for mod dynamic items and menus: unique identifier -> function or menu
        private Dictionary<string, Action> callbackItems = new();
        private Dictionary<string, Action<bool>> callbackItems_bool = new();
        private Dictionary<string, Action<float>> callbackItems_float = new();
        private Dictionary<string, Action<float, float>> callbackItems_float_float = new();
        private Dictionary<string, MenuBuilder> dynamic_menus = new();

        public override void OnInitializeMelon()
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
            splitAvatarOvercrowdedMenu = melonPrefs.CreateEntry("split_overcrowded_avatar_menu", false, "No crowded menus",
                description: "Split avatar menu in multiple pages when it's too crowded");
            menuSize = melonPrefs.CreateEntry("menu_size", 0.5f, "Resize",
                description: "Resize the menu bigger or small as you see fit");
            menuPositionOffset = melonPrefs.CreateEntry("menu_position_offset", 0.5f * Vector2.one, "Reposition",
                description: "Move the menu off-center by this offset (in ratio of the screen height)");
            menuRotationXY = melonPrefs.CreateEntry("menu_rotation_xy", Vector2.zero, "Rotation XY",
                description: "Rotate the menu in the X and Y axis (tilt)");
            openKeyBinding = melonPrefs.CreateEntry("open_key_binding", KeyCode.R, "Open binding",
                description: "Key binding to open the menu");
            reloadKeyBinding = melonPrefs.CreateEntry("reload_key_binding", KeyCode.F3, "Reload binding",
                description: "Key binding to reload the menu (combine with shift for config reload)");

            melonPrefsMap = new();
            foreach (var e in melonPrefs.Entries)
                melonPrefsMap.Add(e.Identifier, e);
            BuildOurMelonPrefsMenus();

            // listen to change to our melon prefs so we can reload the action menu
            // some like positionOffset already change on the fly, so we don't want to reload!
            foreach (var e_ in melonPrefs.Entries)
                switch (e_) {
                    case MelonPreferences_Entry<bool> e: // all bool prefs impact the menu deeply = need reload
                        e.OnEntryValueChanged.Subscribe((_, _) => {
                            BuildOurMelonPrefsMenus(); // rebuild and send it back
                            FullReload();
                        });
                        break;

                    default:
                        break; // nothing
                }

            // some need custom handling
            menuSize.OnEntryValueChanged.Subscribe((_, v) => {
                UpdateMenuScale();
            });

            CVRGameEventSystem.MainMenu.OnOpen.AddListener(() => ToggleMenu(false, handleDesktopInputs: false));
            CVRGameEventSystem.QuickMenu.OnOpen.AddListener(() => ToggleMenu(false, handleDesktopInputs: false));
            CVRGameEventSystem.Microphone.OnMute.AddListener(() => OnCVRMicrophoneToggle(false));
            CVRGameEventSystem.Microphone.OnUnmute.AddListener(() => OnCVRMicrophoneToggle(true));

            // immobilize when the action menu is open
            // FIXME: this stops the avatar animator from moving too, but not ideal, cannot fly anymore or rotate head
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(MovementSystem).Update()),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnUpdateMovementSystem))));

            // build avatar menu from parameters after avatar is loaded
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PlayerSetup).initializeAdvancedAvatarSettings()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnAvatarAdvancedSettings))));

            // monitor game changes to update menu items state
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(CVRCamController).Toggle()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnCVRCameraToggle))));
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PlayerSetup).SwitchSeatedPlay(default)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnCVRSeatedToggle))));
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(MovementSystem).ToggleFlight()),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ActionMenuMod), nameof(OnCVRFlyToggle))));

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
                        logger.Error($"File missing from assembly {fname}");
                        continue;
                    }
                    File.WriteAllBytes(couiPath + @"\" + fname, raw);
                }
#if DEBUG
                logger.Msg($"Installed {couiFiles.Length} resource files from assembly into {couiPath}");
#endif
            }
            MelonCoroutines.Start(WaitCohtmlSpawned());

            VRBindingMod.RegisterBinding("ActionMenuOpen", "Open Action Menu (left hand)", VRBindingMod.Requirement.optional, a =>
            {
                if (a.GetStateDown(SteamVR_Input_Sources.Any))
                    ToggleMenu(!cohtmlView.enabled, leftSide: true);
            });
            VRBindingMod.RegisterBinding("ActionMenuOpenRight", "Open Action Menu (right hand)", VRBindingMod.Requirement.optional, a =>
            {
                if (a.GetStateDown(SteamVR_Input_Sources.Any))
                    ToggleMenu(!cohtmlView.enabled, leftSide: false);
            });
        }

        private static bool OnUpdateMovementSystem(MovementSystem __instance)
        {
            if (!MetaPort.Instance.isUsingVr) return true;
            return cohtmlView?.enabled != true; // TODO: animation still run, prevent emotes
        }

        private void MenuManagerRegisterEvents()
        {
            var view = cohtmlView.View;
#if DEBUG
            logger.Msg($"MenuManagerRegisterEvents called {view}");
#endif
            view.RegisterForEvent("ActionMenuReady", new Action(OnActionMenuReady));
            view.BindCall("CVRAppCallSystemCall", new Action<string, string, string, string, string>(menuManager.HandleSystemCall));
            view.BindCall("CVRAppCallSaveSetting", new Action<string, string>(MetaPort.Instance.settings.SetSetting));
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
            while ((cwv = GameObject.Find("/Cohtml/MainMenuParent/Offset/CohtmlWorldView")) == null)
                yield return null;
            while ((cohtmlUISystem = GameObject.Find("/Cohtml/CohtmlDefaultUISystem")?.GetComponent<CohtmlControlledUISystem>()) == null)
                yield return null;
#if DEBUG
            logger.Msg($"WaitCohtmlSpawned start {cwv}");
#endif
            menuManager = CVR_MenuManager.Instance;

            var parent = cohtmlUISystem.transform.parent;
            var animator = cwv.GetComponent<Animator>().runtimeAnimatorController;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.SetActive(false);
            go.name = "ActionMenu";
            go.layer = LayerMask.NameToLayer("UI Internal");

            var t = menuTransform = go.transform;
            t.SetParent(parent, false);
            UpdateMenuScale();

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
            v.IsTransparent = true;
            v.Width = canvasSize.x;
            v.Height = canvasSize.y;
            v.Page = couiUrl + "/index.html";

            // ready set go
            v.enabled = true;
            go.SetActive(true);
            UpdatePositionToAnchor();
        }

        private void SendItemUpdate(ItemAction action)
        {
            var u = new MenuItemValueUpdate() { action = action };
            cohtmlView?.View?.TriggerEvent<string>("GameValueUpdate", JsonSerialize(u));
        }

        private static void OnCVRCameraToggle(CVRCamController __instance)
        {
            if (cohtmlReadyState < 2) return; // not ready for events
#if DEBUG
            MelonLogger.Msg($"OnCVRCameraToggle {__instance}");
#endif
            var action = new ItemAction()
            {
                type = "system call",
                event_ = "AppToggleCamera",
                value = __instance?.cvrCamera?.activeSelf ?? false
            };
            instance.SendItemUpdate(action);
        }

        private static void OnCVRMicrophoneToggle(bool active)
        {
            if (cohtmlReadyState < 2) return; // not ready for events
#if DEBUG
            MelonLogger.Msg($"OnCVRMicrophoneToggle {active}");
#endif
            var action = new ItemAction()
            {
                type = "system call",
                event_ = "AppToggleMute",
                value = !active,
            };
            instance.SendItemUpdate(action);
        }

        private static void OnCVRSeatedToggle(PlayerSetup __instance, bool seated)
        {
            if (cohtmlReadyState < 2) return; // not ready for events
#if DEBUG
            MelonLogger.Msg($"OnCVRSeatedToggle {seated}");
#endif
            var action = new ItemAction()
            {
                type = "system call",
                event_ = "AppToggleSeatedPlay",
                value = seated,
            };
            instance.SendItemUpdate(action);
        }

        private static void OnCVRFlyToggle(MovementSystem __instance)
        {
            if (cohtmlReadyState < 2) return; // not ready for events
#if DEBUG
            MelonLogger.Msg($"OnCVRFlyToggle {__instance.IsFlying()}");
#endif
            var action = new ItemAction()
            {
                type = "system call",
                event_ = "AppToggleFLightMode",
                value = __instance.IsFlying(),
            };
            instance.SendItemUpdate(action);
        }

        [Serializable]
        internal struct InputData
        {
            public Vector2 joystick;
            public float trigger;
        }

        private void ToggleMenu(bool show, bool handleDesktopInputs = true, bool leftSide = true)
        {
#if DEBUG
            logger.Msg($"ToggleMenu show={show} , cohtmlView.enabled={cohtmlView.enabled} collider={menuCollider?.enabled} vr={MetaPort.Instance.isUsingVr}");
#endif
            if (cohtmlView == null || cohtmlView.View == null) return;

            if (show)
            {
                // need to close down quick + main menu
                var mm = CVR_MenuManager.Instance;
                var vm = ViewManager.Instance;
                if (mm?.IsViewShown == true) mm.ToggleQuickMenu(false);
                else if (vm?.IsViewShown == true) vm.UiStateToggle(false);

                // remember which hand served to open the menu
                anchorToLeftHand = leftSide;
            }

            cohtmlView.View.TriggerEvent<bool>("ToggleActionMenu", show);
            cohtmlView.enabled = show; // TODO: doesn this reload cohtml each time? careful
            menuAnimator.SetBool("Open", show);

            if (show && menuCollider?.enabled == true)
                UpdatePositionToAnchor();

            var vr = MetaPort.Instance.isUsingVr;
            if (!vr)
            {
                if (handleDesktopInputs)
                {
                    CursorLockManager.Instance.SetUnlockWithId(show, "actionmenu");
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
            if (!MetaPort.Instance.isUsingVr) // Desktop mode
            {
                if (menuManager._camera == null)
                    menuManager._camera = PlayerSetup.Instance.desktopCamera.GetComponent<Camera>();

                var halfScreen = 0.5f * new Vector2(Screen.width, Screen.height);
                var mousePos = ((Vector2)Input.mousePosition - halfScreen) / canvasSize;
                joystick = Vector2.ClampMagnitude(3f * mousePos, 1f);

                trigger = Input.GetMouseButtonDown(0) ? 1 : 0; // do we need button up anyway?
                UpdatePositionToAnchor();
            }
            else
            {
                if (menuCollider.enabled)
                    UpdatePositionToAnchor();

                var im = menuManager._inputManager;
                joystick = anchorToLeftHand ?
                    new Vector2(im.movementVector.x, im.movementVector.z) : // y is 0 and irrelevant
                    new Vector2(im.rawLookVector.x, -im.rawLookVector.y); // vertical is inverted
                trigger = anchorToLeftHand ? menuManager._inputManager.interactLeftValue : menuManager._inputManager.interactRightValue;
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
            if (cohtmlReadyState < 1) return;

            var vr = MetaPort.Instance.isUsingVr;
            var t = vr
                ? (anchorToLeftHand ? menuManager._leftVrAnchor : menuManager._rightVrAnchor).transform
                : MovementSystem.Instance.RotationPivot;

            // TODO: weight by avatar scale
            var offset = 0.75f * (menuPositionOffset.Value - 0.5f * Vector2.one); // first value can be tweaked
            // TODO: which direction to pick?
            menuTransform.rotation = Quaternion.Euler(180*menuRotationXY.Value) * t.rotation;
            menuTransform.position = t.position + t.rotation * new Vector3(offset.x, offset.y, vr ? 0 : 1);
        }

        private Menus? melonPrefsMenus;
        private void BuildOurMelonPrefsMenus()
        {
            melonPrefsMenus = ourLib.BuildMelonPrefsMenus(
                // that one is for devs, change it in the melonprefs conf
                melonPrefs.Entries.FindAll(e => e.Identifier != dontInstallResources.Identifier));
        }


        private static Menus? avatarMenus;
        private static void OnAvatarAdvancedSettings(PlayerSetup __instance)
        {
            var animator = __instance.Animator;
            var advSettings = __instance.AvatarDescriptor.avatarSettings.settings;
            var menuPrefix = avatarMenuName;
            var m = avatarMenus = AvatarAdvancedSettingsToMenus(advSettings, animator, menuPrefix);

#if DEBUG
            logger.Msg($"OnAvatarAdvancedSettings {advSettings.Count} items");
#endif

            if (instance.splitAvatarOvercrowdedMenu.Value)
                m = SplitOverCrowdedMenus(m);

            // add avatar emotes
            var emoteNames = __instance?.AnimatorManager.GetLegacyEmoteNames();
            if (emoteNames != null && emoteNames.Length > 0)
            {
                var parents = Path(menuPrefix, "emotes");
                var aitems = m.GetWithDefault(parents);
                var i = 1;
                emoteNames.Do(name =>
                {
#if DEBUG
                    logger.Msg($"OnAvatarAdvancedSettings emote {name} <- {parents}");
#endif
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
            var profilesNames = __instance.getCurrentAvatarSettingsProfiles()?.ToList();
            if (profilesNames != null && profilesNames.Count > 0)
            {
                profilesNames.Add("default"); // there is an implicit default in cvr
                var parents = Path(menuPrefix, "profiles");
                var aitems = m.GetWithDefault(parents);
                profilesNames.Do(name =>
                {
#if DEBUG
                    logger.Msg($"OnAvatarAdvancedSettings profiles {name} <- {parents}");
#endif
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
            var avatarGuid = __instance?.AvatarDescriptor?.avatarSettings?._avatarGuid ?? "default";
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

        private void OnActionMenuReady()
        {
            var view = cohtmlView?.View;
            if (cohtmlView == null) return; // cannot proceed
#if DEBUG
            logger.Msg($"OnActionMenuReady for view {view}");
#endif
            string fromFile;
            try
            {
                fromFile = File.ReadAllText(@"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\ActionMenu\actionmenu.json");
            }
            catch (Exception)
            {
                logger.Error($"Cannot read main json file. Erase your melon preference for ActionMenu and try reinstalling the mod.");
                return;
            }
            var config = JsonConvert.DeserializeObject<Menu>(fromFile);
#if DEBUG
            logger.Msg($"Loaded config with {config.menus.Count} menus: {string.Join(", ", config.menus.Keys)}");
#endif

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
            if (config.menus.GetWithDefault(modsMenuName).Count > 0)
            {
                config.menus.GetWithDefault(mainMenuName).Add(new MenuItem()
                {
                    name = "Mods",
                    icon = "icon_melon.svg",
                    action = new ItemAction() { type = "menu", menu = modsMenuName },
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
                UpdateMenuScale(); // in case change avatar = new scale
            }
        }

        internal struct MenuSettings
        {
            public bool in_vr;
            public bool flick_selection;
            public bool boring_back_button;
        }

        private static string JsonSerialize(object value) => JsonConvert.SerializeObject(value, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        private void ConfigReload()
        {
            OnAvatarAdvancedSettings(PlayerSetup.Instance);
        }

        private void FullReload()
        {
            if (cohtmlView == null)
            {
                logger.Error($"Reload view is null!");
                return;
            }
            cohtmlReadyState = 0;
            OnAvatarAdvancedSettings(PlayerSetup.Instance);
            cohtmlView.View.Reload();
            MelonCoroutines.Start(DelayedRestart(cohtmlView.enabled)); // yes it's a ugly hack but it works, right?
            ToggleMenu(true);
        }

        private System.Collections.IEnumerator DelayedRestart(bool wasEnabled)
        {
            uint i = 42; // rolled a big dice, that's what came out, I swear
            while (--i > 0)
                yield return null;
            ToggleMenu(wasEnabled);
#if DEBUG
            logger.Msg($"view reloaded {cohtmlView} {cohtmlView.View}");
#endif
        }

        private void OnSetMelonPreference(string identifier, string value)
        {
            MelonPreferences_Entry e_;
            if (!melonPrefsMap.TryGetValue(identifier, out e_) || e_ == null)
            {
                logger.Error($"didn't find preference {identifier}");
                return;
            }

            switch (e_)
            {
                case MelonPreferences_Entry<bool> e: {
                        if (float.TryParse(value, out float valueFloat))
                            e.Value = valueFloat != 0;
                        break;
                    }

                case MelonPreferences_Entry<float> e: {
                        if (float.TryParse(value, out float valueFloat))
                            e.Value = valueFloat;
                        break;
                    }

                case MelonPreferences_Entry<Vector2> e: {
                        var strs = value.Split(',');
                        if (float.TryParse(strs[0], out float valueFloatX) && float.TryParse(strs[1], out float valueFloatY))
                            e.Value = new Vector2(valueFloatX, valueFloatY);
                        break;
                    }
                // TODO: implement other types

                default:
                    logger.Error($"OnSetMelonPreference {identifier} unsupported type {e_.GetReflectedType()}");
                    return;
            }
        }

        private void OnItemCallback(string identifier)
        {
            Action f;
            if (!callbackItems.TryGetValue(identifier, out f) || f == null)
            {
                logger.Error($"didn't find callback {identifier}");
                return;
            }

#if DEBUG
            logger.Msg($"OnItemCallback calling {identifier}: {f}"); // TODO debug
#endif
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

#if DEBUG
            logger.Msg($"OnRequestDynamicMenu calling {identifier}: {f}"); // TODO debug
#endif
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
            if (menuTransform != null)
                UpdatePositionToAnchor();

            if (KeyboardManager.Instance?.IsViewShown ?? true) return;

            if (Input.GetKeyDown(reloadKeyBinding.Value)) {
                var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift) ConfigReload();
                else FullReload();
            }

            if (Input.GetKeyDown(openKeyBinding.Value) && cohtmlView != null) {
                ToggleMenu(!cohtmlView.enabled);
            }
        }

        private void UpdateMenuScale()
        {
            var vr = MetaPort.Instance.isUsingVr;
            var scale = vr ? menuBaseSizeVr : menuBaseSizeDesktop; // TODO: experiment
            var avatarScale = vr ? PlayerSetup.Instance.GetAvatarHeight() / 1.8f : 1f;
            menuTransform.localScale = scale * avatarScale * menuSize.Value * Vector3.one;
        }

        internal class OurLib : Lib
        {
            override protected void RegisterOnLoaded() { } // we don't need it ourself
        }
    }
}
