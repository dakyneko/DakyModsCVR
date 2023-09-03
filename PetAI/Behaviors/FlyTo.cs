using System;
using System.Collections;
using UnityEngine;

namespace PetAI.Behaviors;

public class FlyTo : Behavior
{
    public Func<Vector3> getTarget;
    public float stopDistance = 1f, reachedDistance = 1.1f, maxSpeed = 1;
    public bool reached = false, speedStalled = false;
    public float velocity = 0f, reachedThresholdTime = 3;
    public Vector3 lastPosition = Vector3.zero;
    public int moveAnimation;

    public FlyTo(PuPet pet, Func<Vector3> getTarget, int moveAnimation = 3) : base(pet)
    {
        this.getTarget = getTarget;
        this.moveAnimation = moveAnimation;
    }

    public void Start() { }
    public override void End()
    {
        base.End();
        pet.SetAnimation(0);
    }

    public override IEnumerable Run()
    {
        var reachedConsecutive = 0f;
        while (true)
        {
            var newPos = pet.transform.position;
            // smooth a bit for speedStalled, so it doesn't flap
            velocity = 0.5f * velocity + 0.5f * (newPos - lastPosition).magnitude / Time.deltaTime;
            speedStalled = velocity <= 0.1f;
            if (moveAnimation != 0) // animation disabled
                pet.SetAnimation(speedStalled ? 0 : moveAnimation); // hop or idle
            lastPosition = newPos;
            Vector3 toTarget = getTarget() - newPos;
            reached = toTarget.magnitude <= reachedDistance;

            if (reached) reachedConsecutive += Time.deltaTime;
            else         reachedConsecutive = 0;

            if (reached && reachedThresholdTime >= 0 && reachedConsecutive >= reachedThresholdTime)
            {
                logger.Msg($"reachedConsecutive done = {reachedConsecutive}");
                yield break; // done
            }

            var dist = toTarget.magnitude;
            if (dist > stopDistance)
            {
                var direction = toTarget.normalized;
                var toDestination = toTarget - stopDistance * direction;
                var v = Mathf.Min(maxSpeed * Time.deltaTime, toDestination.magnitude);
                pet.followObject.position += v * direction;
                pet.spawnable.needsUpdate = true;
            }

            yield return null;
        }
    }
}
