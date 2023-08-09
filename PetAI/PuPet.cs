using ABI.CCK.Components;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using NavMeshTools = Kafe.NavMeshTools.API;

using Behavior = PetAI.Behaviors.Behavior;

namespace PetAI
{
    public class PuPet : MonoBehaviour
    {
        private PetAIMod mod;
        private MelonLogger.Instance logger;
        private Dictionary<string, Behavior> behaviors = new();

        public Transform followObject, lookObject, headObject;
        public CVRSpawnable spawnable;
        public Dictionary<string, int> syncedIds;
        public (float height, float radius, float climb) geometry = (0.3f, 0.3f, 0.29f);
        public NavMeshAgent agent;
        public float agentSpeed = 3f;
        public NavMeshBuildSettings navSettings;
        public TriggerCallback headTriggerCallback;

        public void Init(PetAIMod mod, MelonLogger.Instance logger)
        {
            this.mod = mod;
            this.logger = logger;

            spawnable = transform.parent.GetComponent<CVRSpawnable>();
            if (spawnable == null)
            {
                logger.Error($"Pet not found: pet={this} spawnable={spawnable}");
                return;
            }

            followObject = spawnable.transform?.Find("followTarget");
            lookObject = spawnable.transform?.Find("lookTarget");
            // TODO: add config for name suffix?
            headObject = transform.Find("Armature_Kitsune/Root_Kitsune/Pivot_Kitsune/Kitsune_Root.003/Kitsune_Head_Rotate/Kitsune_Head");
            if (headObject != null)
            {
                var cb = headTriggerCallback = headObject.gameObject.AddComponent<TriggerCallback>();
                // TODO: debug to remove?
                cb.EnterListener += other => logger.Msg($"head trigger enter {other.name} {other}");
                cb.ExitListener += other => logger.Msg($"head trigger exit {other.name} {other}");
            }
            else
                logger.Warning($"Failed to find pet head object");

            syncedIds = new();
            var i = 0;
            foreach (var v in spawnable.syncValues)
                syncedIds[v.name] = i++;

            logger.Msg($"Found pet {this}: spawnable={spawnable} follow={followObject} look={lookObject}");

            if (followObject.GetComponent<NavMeshAgent>() == null)
                InitNavMesh();
        }

        public void InitNavMesh()
        {
            navSettings = NavMesh.CreateSettings() with
            {
                agentHeight = geometry.height,
                agentRadius = geometry.radius,
                agentSlope = 45f,
                agentClimb = geometry.climb,
                minRegionArea = 0.5f,
                maxJobWorkers = (uint)Mathf.Max(1, Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount - 2),
            };

            agent = followObject.gameObject.AddComponent<NavMeshAgent>();
            agent.agentTypeID = navSettings.agentTypeID;
            agent.enabled = false;
            agent.height = geometry.height;
            agent.radius = geometry.radius;
            agent.speed = agentSpeed;
            agent.angularSpeed = 60f; // enough?
            agent.stoppingDistance = 1f;

            var violations = navSettings.ValidationReport(new Bounds()); // TODO: empty bounds work?
            if (violations.Length > 0)
            {
                logger.Error($"Navmesh settings violations: {string.Join(", ", violations)}");
            }
            BakeNavMesh();
        }

        public void BakeNavMesh(bool force = false)
        {
            NavMeshTools.BakeCurrentWorldNavMesh(navSettings, force, success =>
            {
                if (!success)
                {
                    logger.Error("BakeCurrentWorldNavMesh failed");
                    return;
                }

                logger.Msg("BakeCurrentWorldNavMesh succeed");
            });
        }

        public void FollowPlayer(Transform target, Animator a)
        {
            if (target == null) logger.Warning($"Player avatar not found");
            else AddBehavior(new Behaviors.Follow(target));
            var head = a?.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) logger.Warning($"Player {target?.name} head not found");
            else if (target != null) AddBehavior(new Behaviors.LookAt(head ?? target));
        }


        public void ToggleNavMeshFollow(bool enable)
        {
            if (agent == null)
            {
                logger.Error($"ToggleNavMeshFollow agent null!");
                return;
            }
            agent.enabled = enable;
        }

        public void SetSyncedParameter(string name, float value) => spawnable.SetValue(syncedIds[name], value);
        public void SetSyncedParameterIntensity(string name, int code, float intensity = 1) =>
            SetSyncedParameter(name, (code - 1) + 0.1f + 0.9f * intensity); // with blendtrees: each state go smoothly from x.1 to x+1=code
        public float GetSyncedParameter(string name) => spawnable.GetValue(syncedIds[name]);

        public void SetEyes(int code, float intensity = 1) => SetSyncedParameterIntensity("eyes", code, intensity);
        public void SetMouth(int code, float intensity = 1) => SetSyncedParameterIntensity("mouth", code, intensity);
        public void SetAnimation(int code) => SetSyncedParameter("animation", code);
        

        public void Animate(int code)
        {
            RemAllBehaviors();
            SetAnimation(code);
        }

        public void SetSound(int code)
        {
            SetSyncedParameter("sound", code);
        }

        public void AddBehavior(Behavior behavior)
        {
            if (behaviors.ContainsKey(behavior.name))
                logger.Warning($"Behavior {behavior.name} already exists, overwriting");
            behavior.pet = this;
            behavior.logger = logger;
            behavior.iterator = behavior.Run().GetEnumerator();
            behavior.Start();
            behaviors[behavior.name] = behavior;
            logger.Msg($"Add behavior {behavior.name}");
        }

        public void RemBehavior(string name)
        {
            if (!behaviors.ContainsKey(name))
            {
                logger.Warning($"Behavior {name} is not running");
                return;
            }
            var behavior = behaviors[name];
            behaviors.Remove(name);
            behavior.End();
        }

        public void RemAllBehaviors()
        {
            foreach (var kv in behaviors.ToList())
            {
                behaviors.Remove(kv.Key);
                kv.Value.End();
            }
        }

        public bool HasBehavior(string name) => behaviors.ContainsKey(name);

        public void FixedUpdate()
        {
            foreach (var kv in behaviors.ToList())
            {
                var it = kv.Value.iterator;
                if (!it.MoveNext())
                {
                    logger.Msg($"Behavior {kv.Key} ended");
                    behaviors.Remove(kv.Key);
                    kv.Value.End();
                }
            }
        }
    }
}
