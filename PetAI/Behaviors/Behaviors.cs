using ABI.CCK.Components;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PetAI.Behaviors;

public abstract class Behavior
{
    public string name;
    public PuPet pet;
    public IEnumerator iterator;
    public MelonLogger.Instance logger;

    public virtual IEnumerable Run() { yield break; }
    public virtual void Start() { }
    public virtual void End() { }
}

public class LookAt : Behavior
{
    public Transform lookingAt;
    public LookAt(Transform lookingAt)
    {
        this.name = "LookAt";
        this.lookingAt = lookingAt;
    }

    public override void Start()
    {
        pet.SetSyncedParameter("lookTargetToggle", 1);
        pet.SetSyncedParameter("lookTargetSmooth", 0.01f); // TODO: speed
    }
    public override void End()
    {
        pet.SetSyncedParameter("lookTargetToggle", 0);
        pet.SetSyncedParameter("lookTargetSmooth", 0);
    }

    public override IEnumerable Run()
    {
        while (lookingAt != null)
        {
            pet.lookObject.position = lookingAt.position;
            pet.spawnable.needsUpdate = true;
            yield return null;
        }
    }
}

public class Follow : Behavior
{
    public Transform following;
    public float stopDistance = 1.05f;
    public Follow(Transform following)
    {
        this.name = "Follow";
        this.following = following;
    }
    public override void Start() {
        pet.SetSyncedParameter("followTargetWeight", 0.01f); // TODO: speed
    }
    public override void End()
    {
        pet.SetSyncedParameter("followTargetWeight", 0);
        pet.SetAnimation(0);
    }

    public override IEnumerable Run()
    {
        while (following != null)
        {
            var dist = following.position - pet.transform.position;
            if (dist.magnitude < stopDistance)
            {
                if (pet.GetSyncedParameter("animation") == 3) // not running = ignore
                {
                    pet.SetAnimation(0); // stop walking animation
                    if (pet.agent == null || !pet.agent.enabled)
                        End();
                        //pet.followObject.position = pet.transform.position + 0.01f * dist.normalized; // stay there
                    pet.spawnable.needsUpdate = true;
                }
            }
            else
            {
                pet.SetAnimation(3);
                Start();
                if (pet.agent == null || !pet.agent.enabled)
                    pet.followObject.position = pet.transform.position + 1f * dist.normalized; // TODO: speed
                else
                    pet.agent.destination = following.position;
                pet.spawnable.needsUpdate = true;
            }
            yield return null;
        }
    }
}
