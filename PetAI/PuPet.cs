using ABI.CCK.Components;
using ABI_RC.Core.Player;
using MelonLoader;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using NavMeshTools = Kafe.NavMeshTools.API;

namespace PetAI
{
    public class PuPet : MonoBehaviour
    {
        private PetAIMod mod;
        private MelonLogger.Instance logger;

        public Transform following, lookingAt, followObject, lookObject, headObject;
        public CVRSpawnable spawnable;
        public Dictionary<string, int> syncedIds;
        public (float height, float radius, float climb) geometry = (0.3f, 0.3f, 0.29f);
        public NavMeshAgent agent;
        public float agentSpeed = 3f;
        public NavMeshBuildSettings navSettings;
        
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
                var cb = headObject.gameObject.AddComponent<TriggerCallback>();
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

        public void EnableFollow()
        {
            SetSyncedParameter("followTargetWeight", 0.01f); // TODO: speed
        }
        public void EnableLookAt()
        {
            SetSyncedParameter("lookTargetToggle", 1);
            SetSyncedParameter("lookTargetSmooth", 0.01f); // TODO: speed
        }

        public void DisableFollow()
        {
            following = null;
            SetSyncedParameter("followTargetWeight", 0);
        }

        public void DisableLookAt()
        {
            lookingAt = null;
            SetSyncedParameter("lookTargetToggle", 0);
            SetSyncedParameter("lookTargetSmooth", 0);
        }

        public void FollowPlayer(CVRPlayerEntity p)
        {
            var pm = p?.PuppetMaster;
            following = pm?.gameObject?.transform;
            if (following == null) logger.Warning($"Player avatar not found");
            var head = pm?._animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) logger.Warning($"Player {p} head not found");
            lookingAt = head ?? following;
            EnableFollow();
            EnableLookAt();
        }

        public void ToggleFollowMe(bool enable)
        {
            if (enable)
                FollowMe();
            else
            {
                SetAnimation(0);
                DisableFollow();
            }
        }

        public void FollowMe()
        {
            following = PlayerSetup.Instance.gameObject.transform;
            if (following == null) logger.Warning($"Local avatar not found");
            var head = PlayerSetup.Instance._animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) logger.Warning($"Local head not found");
            lookingAt = head ?? following;
            EnableFollow();
            EnableLookAt();
            if (agent != null) agent.enabled = false;
        }

        public void ToggleNavMeshFollow(bool enable)
        {
            if (agent == null)
            {
                logger.Error($"ToggleNavMeshFollow agent null!");
                return;
            }
            if (following == null)
                FollowMe(); // TODO: remove its agent.enabled = false?
            agent.enabled = enable;
        }

        public void SetSyncedParameter(string name, float value) => spawnable.SetValue(syncedIds[name], value);
        public float GetSyncedParameter(string name) => spawnable.GetValue(syncedIds[name]);

        public void SetEyes(int code) => SetSyncedParameter("eyes", code);
        public void SetMouth(int code) => SetSyncedParameter("mouth", code);
        public void SetAnimation(int code) => SetSyncedParameter("animation", code);
        

        public void Animate(int code)
        {
            SetAnimation(code);
            DisableLookAt();
        }

        public void SetSound(int code)
        {
            SetSyncedParameter("sound", code);
        }

        public void FixedUpdate()
        {
            if (following == null && lookingAt == null) return;

            if (lookingAt != null)
            {
                lookObject.position = lookingAt.position;
                spawnable.needsUpdate = true;
            }
            if (following != null)
            {
                var dist = following.position - transform.position;
                if (dist.magnitude < 1.05)
                {
                    if (GetSyncedParameter("animation") != 3) return; // not running = ignore
                    SetAnimation(0); // stop walking animation
                    if (agent == null || !agent.enabled)
                        followObject.position = transform.position + 0.01f * dist.normalized; // stay there
                    spawnable.needsUpdate = true;
                }
                else
                {
                    SetAnimation(3);
                    if (agent == null || !agent.enabled)
                        followObject.position = transform.position + 1f * dist.normalized; // TODO: speed
                    else
                        agent.destination = following.position;
                    spawnable.needsUpdate = true;
                }
            }
        }
    }
}
