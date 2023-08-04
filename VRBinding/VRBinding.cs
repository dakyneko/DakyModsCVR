using HarmonyLib;
using MelonLoader;
using System.Linq;
using Valve.VR;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(VRBinding.VRBindingMod), "VRBinding", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace VRBinding
{
    public class VRBindingMod : MelonMod
    {
        private static MelonLogger.Instance logger;
        private static VRBindingMod instance;

        private static Dictionary<string, Binding> bindings = new();
        private static bool initialized = false;
        public static readonly string actionPrefix = "/actions/AlphaBlendInteractive/in";

        public enum Requirement
        {
            optional,
            suggested,
            mandatory,
        }
        public abstract class Binding
        {
            public string name;
            public string description;
            public Requirement requirement = Requirement.optional;

            public virtual string type { get; }
            public Action onUpdateDispatch;
        }

        public class BindingBoolean : Binding {
            public Action<SteamVR_Action_Boolean> onUpdate;
            public override string type => "boolean";
        }
        public class BindingSingle : Binding {
            public Action<SteamVR_Action_Single> onUpdate;
            public override string type => "vector1";
        }
        public class BindingVector2 : Binding {
            public Action<SteamVR_Action_Vector2> onUpdate;
            public override string type => "vector2";
        }

        public static void RegisterBinding(string name, string description, Requirement requirement, Action<SteamVR_Action_Boolean> onUpdate) {
            RegisterBinding(new VRBindingMod.BindingBoolean()
            {
                name = name,
                description = description,
                requirement = requirement,
                onUpdate = onUpdate,
            });
        }

        public static void RegisterBinding(string name, string description, Requirement requirement, Action<SteamVR_Action_Single> onUpdate) {
            RegisterBinding(new VRBindingMod.BindingSingle()
            {
                name = name,
                description = description,
                requirement = requirement,
                onUpdate = onUpdate,
            });
        }

        public static void RegisterBinding(string name, string description, Requirement requirement, Action<SteamVR_Action_Vector2> onUpdate) {
            RegisterBinding(new VRBindingMod.BindingVector2()
            {
                name = name,
                description = description,
                requirement = requirement,
                onUpdate = onUpdate,
            });
        }
        public static void RegisterBinding(Binding binding)
        {
            if (initialized)
                throw new System.Exception($"VR Binding are already initialized, it's too late!");
            var fullPath = actionPrefix + "/" + binding.name;
            if (bindings.ContainsKey(fullPath))
                logger.Warning($"Binding {binding.name} already exists! Overwriting");
            bindings[fullPath] = binding;
        }


        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;
            instance = this;

            HarmonyInstance.Patch(
                AccessTools.Method(typeof(Valve.VR.SteamVR_Input_Source), nameof(Valve.VR.SteamVR_Input_Source.Initialize)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(VRBindingMod), nameof(SteamVR_Initialize))));
        }

        // call this only if you know what you're doing!
        private static void SteamVRAddBinding(string fullPath, Binding b_)
        {
            SteamVR_Action action;
            switch (b_)
            {
                case BindingBoolean b: {
                    var a = SteamVR_Action.Create<SteamVR_Action_Boolean>(fullPath);
                    b.onUpdateDispatch = () => b.onUpdate(a);
                    action = a;
                    break;
                }
                case BindingSingle b: {
                    var a = SteamVR_Action.Create<SteamVR_Action_Single>(fullPath);
                    b.onUpdateDispatch = () => b.onUpdate(a);
                    action = a;
                    break;
                }
                case BindingVector2 b: {
                    var a = SteamVR_Action.Create<SteamVR_Action_Vector2>(fullPath);
                    b.onUpdateDispatch = () => b.onUpdate(a);
                    action = a;
                    break;
                }
                default:
                    throw new Exception("never");
            };

            // all those are necessary to work
            action.Initialize(true);
            SteamVR_Input.actions = SteamVR_Input.actions.Append(action).ToArray();
            switch (action)
            {
                case ISteamVR_Action_In a:
                    SteamVR_Input.actionsIn = SteamVR_Input.actionsIn.Append(a).ToArray();
                    SteamVR_Input.actionsNonPoseNonSkeletonIn = SteamVR_Input.actionsNonPoseNonSkeletonIn.Append(a).ToArray();
                    break;
                default: break;

            }
            switch (action)
            {
                case SteamVR_Action_Boolean a: SteamVR_Input.actionsBoolean = SteamVR_Input.actionsBoolean.Append(a).ToArray(); break;
                case SteamVR_Action_Single a: SteamVR_Input.actionsSingle = SteamVR_Input.actionsSingle.Append(a).ToArray(); break;
                case SteamVR_Action_Vector2 a: SteamVR_Input.actionsVector2 = SteamVR_Input.actionsVector2.Append(a).ToArray(); break;
                default: break;
            }

            // may not be necessary but won't hurt?
            SteamVR_Input.actionsByPath.Add(action.fullPath, action);
            SteamVR_Input.actionsByPathLowered.Add(action.fullPath.ToLower(), action);
        }

        private static void SteamVR_Initialize()
        {
            // register in file
            logger.Msg($"Patching steamvr actions.json with {bindings.Count} mod bindings");
            var actionsJsonPath = SteamVR_Input.GetActionsFilePath();
            var txt = File.ReadAllText(actionsJsonPath);
            var x = JsonConvert.DeserializeObject<SteamVRActions>(txt);
            x.actions.RemoveAll(a => bindings.ContainsKey(a.name));
            var localization = x.localization.FirstOrDefault(l => l["language_tag"] == "en_US");
            localization["language_tag"] = "en_US";
            foreach (var kv in bindings)
            {
                var b = kv.Value;
                x.actions.Add(new SteamVRActionsAction()
                {
                    name = kv.Key,
                    type = b.type.ToString(),
                    requirement = b.requirement.ToString(),
                });
                localization[kv.Key] = kv.Value.description;
            }
            // TODO: make a backup?
            var y = JsonConvert.SerializeObject(x, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            File.WriteAllText(actionsJsonPath, y);
            SteamVR_Input.IdentifyActionsFile(true); // force reload

            // register in SteamVR runtime
            logger.Msg($"Initializing {bindings.Count} bindings: {bindings.Keys.Join(delimiter: ", ")}");
            foreach (var kv in bindings)
            {
                SteamVRAddBinding(kv.Key, kv.Value);
            }

            initialized = true;
        }

        public override void OnUpdate()
        {
            if (!initialized) return;

            foreach (var b in bindings.Values)
            {
                b.onUpdateDispatch();
            }
        }
    }
    public class SteamVRActionsAction
    {
        public string name, type, requirement, skeleton;
    }
    public class SteamVRActionsActionSet
    {
        public string name, usage;
    }
    public class SteamVRActionsDefaultBindings
    {
        public string controller_type, binding_url;
    }
        
    public class SteamVRActions {
        public List<SteamVRActionsAction> actions;
        public List<SteamVRActionsActionSet> action_sets;
        public List<SteamVRActionsDefaultBindings> default_bindings;
        public List<Dictionary<string, string>> localization;
    }
}
