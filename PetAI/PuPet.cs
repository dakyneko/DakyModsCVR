using ABI.CCK.Components;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using System;
using Common = Daky.Dakytils;

using NavMeshTools = Kafe.NavMeshTools.API;
using Behavior = PetAI.Behaviors.Behavior;

namespace PetAI
{
    public class PuPet : MonoBehaviour
    {
        internal MelonLogger.Instance logger;
        private Dictionary<string, Behavior> behaviors = new();

        public Transform followObject, lookObject, headObject;
        public CVRSpawnable spawnable;
        public Dictionary<string, int> syncedIds;
        public (float height, float radius, float climb) geometry = (0.8f, 0.2f, 0.79f);
        public NavMeshAgent agent;
        public float maxSpeed = 3f, maxAngularSpeed = 60f;
        public NavMeshBuildSettings navSettings;
        public TriggerCallback headTriggerCallback;

        public void Init(PetAIMod mod, MelonLogger.Instance logger)
        {
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
            {
                agent = followObject.gameObject.AddComponent<NavMeshAgent>();
                agent.agentTypeID = 0; // default human
                agent.enabled = false;
                agent.height = geometry.height;
                agent.radius = geometry.radius;
                agent.speed = maxSpeed;
                agent.angularSpeed = maxAngularSpeed;
                agent.stoppingDistance = 1f;

                var tri = NavMesh.CalculateTriangulation();
                if (tri.vertices.Length == 0) // if empty, generate
                    InitNavMesh();
            }
        }

        public void InitNavMesh(bool force = false)
        {
            var navSettings = new NavMeshTools.Agent(
                agentRadius: geometry.radius,
                agentHeight: geometry.height,
                agentClimb: geometry.climb,
                generateNavMeshLinks: false);

            agent.agentTypeID = navSettings.AgentTypeID;

            NavMeshTools.BakeCurrentWorldNavMesh(navSettings, (_, success) =>
            {
                if (!success)
                    logger.Error("BakeCurrentWorldNavMesh failed");
                else
                    logger.Msg("BakeCurrentWorldNavMesh succeed");
            }, force);
        }

        public void FondOfPlayer(Transform target, Animator animator)
        {
            if (target == null) logger.Warning($"Player avatar not found");
            else AddBehavior(new Behaviors.Fond(this, target, animator));
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

        public void AddBehavior<T>(T behavior) where T : Behavior
        {
            var name = typeof(T).Name;
            if (behaviors.ContainsKey(name))
                logger.Warning($"Behavior {name} already exist, overwriting");
            behavior.pet = this;
            behavior.logger = logger;
            behavior.iterator = behavior.Run().GetEnumerator();
            behaviors[name] = behavior;
            logger.Msg($"Added behavior {name}");
        }

        public void RemBehavior<T>() where T : Behavior
        {
            var name = typeof(T).Name;
            if (!behaviors.ContainsKey(name))
            {
                logger.Warning($"Behavior {name} is not running");
                return;
            }
            var behavior = behaviors[name];
            behaviors.Remove(name);
            behavior.End();
            logger.Msg($"Removed behavior {name}");
        }

        public void RemAllBehaviors()
        {
            foreach (var kv in behaviors.ToList())
            {
                behaviors.Remove(kv.Key);
                kv.Value.End(); // also for non standalone, is that ok?
            }
        }

        public void ShowAllBehaviors()
        {
            logger.Msg($"All {behaviors.Count} {this.name}'s behaviors running:"
                + string.Join("", behaviors.Select(kv => $"\n - {kv.Key}: {kv.Value.ToString()}")));
        }

        public bool HasBehavior<T>() where T : Behavior => behaviors.ContainsKey(typeof(T).Name);

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
