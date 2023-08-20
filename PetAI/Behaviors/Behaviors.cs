using ABI.CCK.Components;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace PetAI.Behaviors;
using static Daky.Dakytils;

public abstract class Behavior
{
    public PuPet pet;
    public IEnumerator iterator;
    public MelonLogger.Instance logger;
    public List<Behavior> children = new();
    public static uint uniqueIdNext = 0;
    public uint uniqueId; // TODO: debug

    public Behavior(PuPet pet)
    {
        this.pet = pet;
        this.logger = pet.logger;
        this.iterator = Run().GetEnumerator();
        this.uniqueId = uniqueIdNext++;
    }

    public T Add<T>(T behavior) where T : Behavior
    {
        logger.Msg($"Addition {behavior.GetType().Name}({behavior.uniqueId}) -> {this.GetType().Name}({uniqueId}) ");
        children.Add(behavior); // calling Start() is responsibilty of Behavior
        return behavior;
    }
    public void Remove<T>(T behavior) where T : Behavior
    {
        logger.Msg($"Removal {behavior.GetType().Name}({behavior.uniqueId}) -> {this.GetType().Name}({uniqueId}) ");
        behavior.End();
        if (!children.Remove(behavior))
            logger.Warning($"Couldn't remove children behavior {behavior}");
    }

    public virtual IEnumerable Run() { yield break; }
    public bool Step() => iterator.MoveNext();
    public virtual void End()
    {
        foreach (var behavior in children)
        {
            Remove(behavior);
        }
    }
    public virtual string StateToString() => "";
    public override string ToString() => ToString(1);
    public string ToString(int indent)
    {
        var childrenStr = string.Join("", children.Select(
            children => "\n "+ (new string(' ', 2*indent)) +"- " + children.ToString(indent+1)));
        return $"{this.GetType().Name}[{this.uniqueId}]({StateToString()}){childrenStr}";
    }
}

public class LookAt : Behavior
{
    public Transform lookingAt;
    public float speed = 0.01f;

    // caller is responsible to stop if target becomes null
    public LookAt(PuPet pet, Transform lookingAt) : base(pet)
    {
        this.lookingAt = lookingAt;
    }

    public void Start()
    {
        pet.SetSyncedParameter("lookTargetToggle", 1);
        pet.SetSyncedParameter("lookTargetSmooth", speed);
    }
    public override void End()
    {
        base.End();
        pet.SetSyncedParameter("lookTargetToggle", 0);
        pet.SetSyncedParameter("lookTargetSmooth", 0);
    }

    public override IEnumerable Run()
    {
        Start();
        while (true)
        {
            pet.lookObject.position = lookingAt.position;
            pet.spawnable.needsUpdate = true;
            yield return null;
        }
    }
    public override string StateToString() => $"lookingAt={lookingAt?.name}";
}

public class Follow : Behavior
{
    public Func<Vector3> getTarget;
    public NavMeshAgent agent;
    public float stopDistance = 1.05f, teleportDistance = 50, maxSpeed = 1;
    public bool reached = false, speedStalled = false, teleportIfTooFar = false;
    public float stuckThresholdTime = 1.5f, reachedThresholdTime = 3;
    public Vector3 toTarget = Vector3.zero, lastPosition = Vector3.zero;
    public float stuckTime = 0f, velocity = 0f;
    public enum EndReason { lostTarget, invalidPath, reached };
    public EndReason endReason = EndReason.lostTarget;

    // caller is responsible to stop if target becomes null
    public Follow(PuPet pet, Func<Vector3> getTarget) : base(pet)
    {
        this.getTarget = getTarget;
        this.agent = pet.agent;
        this.maxSpeed = pet.maxSpeed;
    }
    public void Start()
    {
        SetNavWeight();
        agent.speed = maxSpeed;
    }
    public override void End()
    {
        base.End();
        pet.SetAnimation(0);
        agent.enabled = false;
    }

    public void SetNavWeight() => pet.SetSyncedParameter("followTargetWeight", 0.05f);
    public void SetFlyWeight() => pet.SetSyncedParameter("followTargetWeight", 0.01f);

    public override string StateToString() => $"getTarget={getTarget()} velocity={velocity:0.00} stuckTime={stuckTime:0.00} reached={reached} speedStalled={speedStalled} agent.enabled={agent.enabled} toTarget.magnitude={toTarget.magnitude:F2}";

    public override IEnumerable Run()
    {
        Start();
        endReason = EndReason.lostTarget;
        agent.enabled = true;
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
            pet.SetAnimation(speedStalled ? 0 : 3); // hop or idle
            lastPosition = newPos;
            toTarget = getTarget() - newPos;

            if (!forward.MoveNext()) break;

            if (reached) reachedConsecutive += Time.deltaTime;
            else         reachedConsecutive = 0;

            pet.SetSyncedParameter("followTargetAimWeight", reached ? 0f : 0.05f); // avoid glitch when reached

            if (reachedConsecutive >= reachedThresholdTime)
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
                pet.spawnable.needsUpdate = true;

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
                pet.spawnable.needsUpdate = true;

                // check if we can rejoin the navmesh if it's close by, reactivate agent and continue
                var hit = new NavMeshHit();
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
        SetNavWeight();
        while (true)
        {
            reached = toTarget.magnitude <= stopDistance // all good
                || (speedStalled && toTarget.magnitude < 2f); // stuck a bit further but navmesh sux anyway

            if (!reached) // no need to modify path if we reached, right?
                agent.destination = getTarget();
            while (agent.pathPending) yield return null;

            yield return null;
        }
    }

    public IEnumerable ByFly()
    {
        logger.Msg($"Start by fly");
        agent.enabled = false;
        SetFlyWeight();
        while (true)
        {
            reached = toTarget.magnitude <= stopDistance;

            var direction = toTarget.normalized;
            // 0.95 to ensure we reach stopDistance or reachedConsecutive will wait a long time!
            var toDestination = toTarget - 0.95f * stopDistance * direction;
            var v = Mathf.Min(1.5f, toDestination.magnitude);
            pet.followObject.position = pet.transform.position + v * direction;

            yield return null;
        }
    }
}

public class PatsLover : Behavior
{
    public class PatsInfo
    {
        public float score;
        public uint count;
        public Vector3 lastPosition;
        public Collider collider;
        public bool inside;
    }
    public Dictionary<string, PatsInfo> pats = new();
    public TriggerCallback callback;
    public float scoreThreshold = 1;
    public float scoreDecay = 0.997f, scorePuur = 0.5f, scoreCalming = 0.1f;
    public PlayerDescriptor winner;
    public override string StateToString() => $"scoreThreshold={scoreThreshold} winner={winner?.userName} pats={pats.Count}[{string.Join(", ", pats.Select(p => $"{p.Key}={p.Value.score:0.00}/{p.Value.count}") )}]";

    public PatsLover(PuPet pet) : base(pet)
    {
        this.callback = pet.headTriggerCallback;
    }

    public void Start()
    {
        this.callback.EnterListener += OnEnter;
        this.callback.ExitListener += OnExit;
        winner = null;
        pats.Clear(); // reset
    }
    public override void End()
    {
        base.End();
        this.callback.EnterListener -= OnEnter;
        this.callback.ExitListener -= OnExit;
    }

    public string[] allowedPointerTypes = new string[] { "index", "grab", "hand" };
    private void OnEnter(Collider other) {
        var p = other.GetComponent<CVRPointer>();
        if (p?.type != null && !allowedPointerTypes.Contains(p.type)) return;

        if (pats.ContainsKey(p.name))
        {
            var pat = pats[p.name];
            pat.lastPosition = p.transform.position;
            pat.inside = true;
        }
        else
        {
            pats[p.name] = new PatsInfo()
            {
                score = 0,
                count = 0,
                lastPosition = p.transform.position,
                collider = other,
                inside = true,
            };
        }
    }
    private void OnExit(Collider other) {
        if (!pats.ContainsKey(other.name)) return;

        var pat = pats[other.name];
        pat.inside = false;
        pat.score *= 0.5f; // punish for getting out, prevent slapping fast
        if (pat.score <= 0.1)
            pats.Remove(other.name);
    }

    // TODO: score is per collider but should be per player
    public override IEnumerable Run()
    {
        Start();
        while (true)
        {
            foreach (var kv in pats.ToList())
            {
                var (name, pat) = (kv.Key, kv.Value);
                if (pat.collider == null) /// became invalid?
                {
                    pats.Remove(name);
                    continue;
                }
                var newPos = pat.collider.transform.position;
                pat.score *= scoreDecay;
                if (pat.inside)
                {
                    var dist = (pat.lastPosition - newPos).magnitude;
                    logger.Msg($"Headpatter candidate {pat.collider.name}, inside={pat.inside} score={pat.score:0.00} dist={dist:0.00} count={pat.count}");
                    pat.score += dist;
                    pat.count += 1;
                    if (pat.score > scoreThreshold)
                    {
                        PlayerDescriptor playerDesc = null; // try through pickup first
                        var pickup = pat.collider.gameObject.GetComponentInParent<CVRPickupObject>();
                        if (pickup != null)
                        {
                            if (MetaPort.Instance.ownerId == pickup.grabbedBy) // local
                                playerDesc = PlayerSetup.Instance.gameObject.GetComponent<PlayerDescriptor>();
                            else
                            {
                                var player = MetaPort.Instance.PlayerManager.NetworkPlayers.FirstOrDefault(p => p.Uuid == pickup.grabbedBy);
                                playerDesc = player?.PlayerDescriptor;
                            }
                            logger.Msg($"Found player from pickup grabbed by: {playerDesc} <- {pickup.grabbedBy} <- {pickup}");
                        }
                        // otherwise it's on a player directly
                        playerDesc ??= pat.collider.gameObject.GetComponentInParent<PlayerDescriptor>();
                        if (playerDesc == null)
                        {
                            logger.Error($"Can't find player from collider {pat.collider.name}");
                            pats.Remove(name);
                            continue;
                        }
                        logger.Msg($"Headpatter winner {playerDesc.userName} <- {pat.collider.name}, score={pat.score} count={pat.count}");
                        winner = playerDesc;
                        yield break;
                    }
                }
                pat.lastPosition = newPos;
            }
            float highestScore = pats.Values.Select(pat => (float?) pat.score).Max() ?? 0;
            if (highestScore >= scoreCalming)
                pet.SetEyes(2, highestScore / scoreThreshold);
            if (highestScore >= scorePuur)
                pet.SetSound(2); // puur
            else
                pet.SetSound(0); // no more
            yield return null;
        }
    }
}

public class Fond : Behavior
{
    public Transform target;
    public Animator animator;
    public Transform head;

    // caller is responsible to stop if target becomes null
    public Fond(PuPet pet, Transform target, Animator? animator = null) : base(pet)
    {
        this.target = target;
        this.animator = animator ?? target.GetComponent<PlayerSetup>()?._animator ?? target.GetComponent<PuppetMaster>()._animator;
        this.head = animator?.GetBoneTransform(HumanBodyBones.Head);
    }

    public override IEnumerable Run()
    {
        var follow = Add(new Follow(pet, () => target.position));
        var lookAt = Add(new LookAt(pet, head ?? target));
        while (follow.Step() && lookAt.Step()) yield return null;
        Remove(follow); Remove(lookAt);

        while (true) // try go into lap in loop
        {
            logger.Msg($"loop lap");
            var waitDuration = 1f; // TODO: make longer
            var lapAvailable = 0f;
            Vector3? lapPosition = null;
            for (var wait = 0f; wait < waitDuration; wait += Time.deltaTime)
            {
                var newLapPosition = GetLapPosition();
                lapPosition = newLapPosition ?? lapPosition;
                if (newLapPosition != null) lapAvailable += Time.deltaTime;
                yield return null;
            }
            logger.Msg($"Lap {target.name} availability: {lapAvailable} / {waitDuration}");

            if (lapPosition != null && lapAvailable / waitDuration > 0.9) // go into lap
            {
                // TODO: add transition to go in lap: jump + animation
                pet.SetSyncedParameter("followTargetWeight", 0.8f); // stick to it
                logger.Msg($"Lap {target.name} available");
                pet.SetSyncedParameter("followTargetAimWeight", 001f); // much slower
                pet.SetSyncedParameter("lookTargetToggle", 1); // TODO: hacky
                pet.SetSyncedParameter("lookTargetSmooth", 0.01f);
                pet.SetSound(2);
                pet.SetEyes(1); // happy
                var noMoreLapAvailable = 0f;
                while (noMoreLapAvailable < 2f)
                {
                    var newLapPosition = GetLapPosition();
                    lapPosition = newLapPosition ?? lapPosition;
                    if (newLapPosition == null)
                    {
                        logger.Msg($"lap {target.name} counting not avail: {newLapPosition} : {noMoreLapAvailable} / 2");
                        noMoreLapAvailable += Time.deltaTime;
                    }
                    pet.followObject.position = lapPosition.Value;
                    //pet.lookObject.position = -(head ?? target).right;
                    pet.spawnable.needsUpdate = true;
                    yield return null;
                }
                logger.Msg($"lap {target.name} no more available");
            }
            pet.SetSound(0);
            pet.SetEyes(0);
            pet.SetSyncedParameter("lookTargetToggle", 0);

            // pick random position around player and go there
            // TODO: not nice to move away from player! actually
            var nextPosition = Vector3.zero;
            for (var tries = 0; tries < 5; ++tries)
            {
                var dist = UnityEngine.Random.Range(1.5f, 5f);
                var angle = Quaternion.Euler(0, UnityEngine.Random.Range(-75, +75), 0);
                var forward = ((head ?? target).forward with { y = 0 }).normalized; // in front of target
                nextPosition = target.position + angle * forward * dist;
                // we want our next point to be visible from player
                // and on the navmesh if possible. Try a few times
                if (Physics.Linecast(target.position, nextPosition))
                {
                    continue;
                }
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(nextPosition, out hit, 1f, new NavMeshQueryFilter() { agentTypeID = pet.agent.agentTypeID, areaMask = NavMesh.AllAreas }))
                {
                    continue;
                }
                nextPosition = hit.position; // on navmesh
                break; // success
            }

            // move there using Follow
            // TODO: should only allow nav move, no fly if possible
            follow = Add(new Follow(pet, () => nextPosition));
            follow.reachedThresholdTime = 1;
            follow.stuckThresholdTime = 0.5f;
            follow.stopDistance = 0;
            follow.maxSpeed = 0.5f; // slower
            while (follow.Step()) yield return null;
            Remove(follow);
            waitDuration = UnityEngine.Random.Range(1f, 5f);
            for (var wait = 0f; wait < waitDuration; wait += Time.deltaTime)
                yield return null; // wait a bit there

            yield return null;
        }

        /* TODO:
        - if high energy: happy anims+noises (jumps, backflips, dance, etc), hop around player, jump on face/body
        - if mid: walk around, rug on legs
        - if low: sleep next to player or in lap
        depends on player position (stand, sit, lay) and movements (compute their energy, motion quantity)
        keep track of pet energy (distance traveled to meet player, or total overall?) and mood

        detect if player looks at pet, player talks to pet
        */
    }
    public override string StateToString() => $"target={target?.name}";

    private bool isHorizontalFn(Vector3 Vn) => Mathf.Abs(Vector3.Dot(Vn, Vector3.up)) <= Mathf.Cos(Mathf.PI / 6);
    private bool isForwardFn(Transform hips, Vector3 Vn) => Vector3.Dot(Vn, hips.forward) >= 0;

    private Vector3? GetLapPosition() {
        if (animator == null) return null;

        Transform hips, thighL, thighR, legL, legR;
        try
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            thighL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            thighR = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            legL = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            legR = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }
        catch (System.Exception ex)
        {
            logger.Error($"Asked GetLapPosition but target is not humanoid? {ex}");
            return null;
        }


        var vecL = legL.position - thighL.position;
        var vecR = legR.position - thighR.position;
        var vecLn = vecL.normalized;
        var vecRn = vecR.normalized;

        var isHorizontal = isHorizontalFn(vecLn) && isHorizontalFn(vecRn);
        var isForward = isForwardFn(hips, vecL) && isForwardFn(hips, vecR);
        var isLapAvailable = isHorizontal && isForward;
        if (!isLapAvailable)
            return null;

        var midThighL = (legL.position + thighL.position) / 2;
        var midThighR = (legR.position + thighR.position) / 2;
        var wouldFall = false; // (midThighL - midThighR).magnitude > pet.geometry.radius; // TODO: tweak

        if (wouldFall)
            return null;

        var thickV = thighR.position - thighL.position;
        var thickVn = thickV.normalized;
        // two thighs together making a virtual surface = lap spot
        var aboveThighV = (Vector3.Cross(thickVn, -vecLn) + Vector3.Cross(thickVn, -vecRn)).normalized;
        var p = (midThighL + midThighR + pet.geometry.radius * aboveThighV) / 2; // lap spot is between thighs

        return p;
    }
}

public class Main : Behavior
{

    public Main(PuPet pet) : base(pet) { }

    public override IEnumerable Run()
    {
        while (true)
        {
            Transform target;
            var patsLover = Add(new PatsLover(pet));
            while (patsLover.Step()) yield return null;
            if (patsLover.winner == null) yield break; // TODO: why it stopped?
            target = patsLover.winner.transform;
            Remove(patsLover);

            var fond = Add(new Fond(pet, target));
            while (target != null && fond.Step()) yield return null;
            Remove(fond);
            //if (target == null) continue; // restart
            // TODO
        }
    }
}
