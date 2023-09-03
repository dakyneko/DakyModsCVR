using System;
using System.Collections;
using UnityEngine;

namespace PetAI.Behaviors;

public class LookAt : Behavior
{
    public Func<Vector3> getTarget;
    public float speed = 0.01f;

    // caller is responsible to stop if target becomes null
    public LookAt(PuPet pet, Func<Vector3> getTarget) : base(pet)
    {
        this.getTarget = getTarget;
    }

    public void Start()
    {
        pet.SetLookTargetToggle(1);
        pet.SetLookTargetSmooth(speed);
    }
    public override void End()
    {
        base.End();
        pet.SetLookTargetToggle(0);
        pet.SetLookTargetSmooth(0);
    }

    public override IEnumerable Run()
    {
        Start();
        while (true)
        {
            pet.lookObject.position = getTarget();
            pet.spawnable.needsUpdate = true;
            yield return null;
        }
    }
    public override string StateToString() => $"getTarget={getTarget()}";
}
