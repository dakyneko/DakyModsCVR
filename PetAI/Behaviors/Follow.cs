using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace PetAI.Behaviors;
using static Daky.Dakytils;

public class Follow : Behavior
{
    public Func<Vector3> getTarget;
    public NavMeshAgent agent;
    public float stopDistance = 1.00f, reachedDistance = 1.05f, teleportDistance = 50, maxSpeed = 1;
    public bool reached = false, speedStalled = false, teleportIfTooFar = false;
    public float stuckThresholdTime = 1.5f, reachedThresholdTime = 3;
    public Vector3 toTarget = Vector3.zero, lastPosition = Vector3.zero;
    public float stuckTime = 0f, velocity = 0f;
    public int moveAnimation;
    public enum EndReason { lostTarget, invalidPath, reached };
    public EndReason endReason = EndReason.lostTarget;

    // caller is responsible to stop if target becomes null
    // TODO: remove AllowedLocomotion, was a bad idea
    public Follow(PuPet pet, Func<Vector3> getTarget,
        int moveAnimation = 3) : base(pet)
    {
        this.getTarget = getTarget;
        this.agent = pet.agent;
        this.maxSpeed = pet.maxSpeed;
        this.moveAnimation = moveAnimation;
    }
    public void Start()
    {
        agent.speed = maxSpeed;
    }
    public override void End()
    {
        base.End();
        pet.SetAnimation(0);
        agent.enabled = false;
    }

    public void SetNavWeight() => pet.SetFollowTargetWeight(0.05f);
    public void SetFlyWeight() => pet.SetFollowTargetWeight(0.05f);

    public override string StateToString() => $"getTarget={getTarget()} velocity={velocity:0.00} stuckTime={stuckTime:0.00} reached={reached} speedStalled={speedStalled} agent.enabled={agent.enabled} toTarget.magnitude={toTarget.magnitude:F2}";

    public override IEnumerable Run()
    {
        Start();
        endReason = EndReason.lostTarget;
        lastPosition = pet.transform.position;
        var forward = Forward().GetEnumerator();
        var reachedConsecutive = 0f;
        logger.Msg($"Follow Run start");

        // TODO: debug stuff
        var triggerReached = TriggerOnChange(() => reached, (v1, v2) => logger.Msg($"reached: {v1} -> {v2}"));
        var triggerAgentEnabled = TriggerOnChange(() => agent.enabled, (v1, v2) => logger.Msg($"agent.enabled: {v1} -> {v2}"));
        var triggerSpeedStalled = TriggerOnChange(() => speedStalled, (v1, v2) => logger.Msg($"speedStalled: {v1} -> {v2}"));
        var triggerReachedConsecutive = TriggerOnChange(() => Mathf.Round(reachedConsecutive), (v1, v2) => logger.Msg($"reachedConsecutive: {v1} -> {v2}"));
        var triggerAnimation = TriggerOnChange(() => pet.GetSyncedParameter("animation"), (v1, v2) => logger.Msg($"GetSyncedParameter=animation: {v1} -> {v2}"));

        while (true)
        {
            // TODO: debug stuff
            triggerReached();
            triggerAgentEnabled();
            triggerSpeedStalled();
            triggerReachedConsecutive();
            triggerAnimation();

            var newPos = pet.transform.position;
            // smooth a bit for speedStalled, so it doesn't flap
            velocity = 0.5f * velocity + 0.5f * (newPos - lastPosition).magnitude / Time.deltaTime;
            speedStalled = velocity <= 0.1f;
            if (moveAnimation != 0) // animation disabled
                pet.SetAnimation(speedStalled ? 0 : moveAnimation); // hop or idle
            lastPosition = newPos;
            toTarget = getTarget() - newPos;

            if (!forward.MoveNext()) break;

            if (reached) reachedConsecutive += Time.deltaTime;
            else         reachedConsecutive = 0;

            pet.SetFollowTargetAimWeight(reached ? 0f : 0.05f); // avoid glitch when reached

            // if threshold is set and arrived for long enough, we're done
            if (reached && reachedThresholdTime >= 0 && reachedConsecutive >= reachedThresholdTime)
            {
                logger.Msg($"reachedConsecutive done = {reachedConsecutive}");
                endReason = EndReason.reached;
                pet.spawnable.needsUpdate = true; // one last update?
                yield break;
            }

            yield return null;
        }
    }

    public IEnumerable Forward()
    {
        while (true)
        {
            logger.Msg($"Forward loop");
            if (teleportIfTooFar && toTarget.magnitude >= teleportDistance)
                logger.Warning($"Pet too far, should teleport"); // TODO: implement

            var byNav = ByNavMesh().GetEnumerator();
            var stuckTime = 0f;
            while (byNav.MoveNext())
            {
                if (speedStalled) stuckTime += Time.deltaTime;
                else              stuckTime = 0;

                if (!reached && stuckTime >= stuckThresholdTime)
                {
                    logger.Msg($"by nav stuck");
                    break; // try by fly
                }
                yield return null;
            }

            var byFly = ByFly().GetEnumerator();
            var lastTryNav = 0f;
            while (byFly.MoveNext())
            {
                // check if we can rejoin the navmesh if it's close by, reactivate agent and continue
                NavMeshHit hit;
                var q = new NavMeshQueryFilter() with { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
                // lastTryNav: only do it once in a while to prevent expensive computations every frame!
                if (lastTryNav >= 1 && NavMesh.SamplePosition(pet.transform.position, out hit, 1, q))
                {
                    agent.enabled = true; // try
                    // check navmesh bullshit, if destination is too far we don't accept it! continue flying
                    var p = new NavMeshPath();
                    var target = getTarget();
                    if (agent.isOnNavMesh
                        && agent.CalculatePath(target, p) // will be costly
                        && (p.corners.Last() - target).magnitude <= stopDistance)
                    {
                        logger.Msg($"fly detected can go by nav now");
                        agent.enabled = true;
                        agent.velocity = velocity * toTarget.normalized;
                        agent.Warp(hit.position); // TODO: should move slowly, not teleport
                        break; // try by nav
                    }
                    lastTryNav = 0f;
                    agent.enabled = false; // failed
                }
                lastTryNav += Time.deltaTime;
                yield return null;
            }
        }
    }

    public IEnumerable ByNavMesh()
    {
        logger.Msg($"Start by nav");
        agent.enabled = true;
        agent.stoppingDistance = stopDistance;
        SetNavWeight();
        while (true)
        {
            reached = toTarget.magnitude <= reachedDistance;

            if (!reached)
            {
                agent.destination = getTarget();
                pet.spawnable.needsUpdate = true;
            }
            while (agent.pathPending) yield return null;

            yield return null;
        }
    }

    // TODO: maybe refactor with Behavior FlyTo
    public IEnumerable ByFly()
    {
        logger.Msg($"Start by fly");
        agent.enabled = false;
        SetFlyWeight();
        while (true)
        {
            var dist = toTarget.magnitude;
            reached = dist <= reachedDistance;

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
