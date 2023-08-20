using ABI.CCK.Components;
using ABI_RC.Core.InteractionSystem;
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
        this.uniqueId = uniqueIdNext++;
        ResetEnumerator();
    }

    public void ResetEnumerator() => this.iterator = Run().GetEnumerator();

    public void Restart() => ResetEnumerator();

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
        foreach (var behavior in children.ToList())
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
    public Func<Vector3> getTarget;
    public float speed = 0.01f;

    // caller is responsible to stop if target becomes null
    public LookAt(PuPet pet, Func<Vector3> getTarget) : base(pet)
    {
        this.getTarget = getTarget;
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
            pet.lookObject.position = getTarget();
            pet.spawnable.needsUpdate = true;
            yield return null;
        }
    }
    public override string StateToString() => $"getTarget={getTarget()}";
}

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

            var direction = toTarget.normalized;
            // 0.95 to ensure we reach stopDistance or reachedConsecutive will wait a long time!
            var toDestination = toTarget - stopDistance * direction;
            var v = Mathf.Min(maxSpeed, toDestination.magnitude);
            pet.followObject.position = pet.transform.position + v * direction;
            pet.spawnable.needsUpdate = true;

            yield return null;
        }
    }
}

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

    public void SetNavWeight() => pet.SetSyncedParameter("followTargetWeight", 0.05f);
    public void SetFlyWeight() => pet.SetSyncedParameter("followTargetWeight", 0.01f);

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

            pet.SetSyncedParameter("followTargetAimWeight", reached ? 0f : 0.05f); // avoid glitch when reached

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
        SetNavWeight();
        while (true)
        {
            reached = toTarget.magnitude <= reachedDistance // all good
                || (speedStalled && toTarget.magnitude < 2*stopDistance); // stuck a bit further but navmesh sux anyway

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
            reached = toTarget.magnitude <= reachedDistance;

            var direction = toTarget.normalized;
            // 0.95 to ensure we reach stopDistance or reachedConsecutive will wait a long time!
            var toDestination = toTarget - 0.95f * stopDistance * direction;
            var v = Mathf.Min(maxSpeed, toDestination.magnitude);
            pet.followObject.position = pet.transform.position + v * direction;
            pet.spawnable.needsUpdate = true;

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
    public override string StateToString() => $"target={target?.name} animator={animator?.name} head={head?.name}";

    public override IEnumerable Run()
    {
        var follow = Add(new Follow(pet, () => target.position));
        var lookAt = Add(new LookAt(pet, () => (head ?? target).position));
        while (follow.Step() && lookAt.Step()) yield return null;
        Remove(follow); Remove(lookAt);

        while (true)
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
                logger.Msg($"Lap {target.name} available");
                pet.SetSyncedParameter("followTargetWeight", 0.9f); // stick to it
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
                    if ((pet.followObject.position - lapPosition.Value).magnitude >= 0.05f) // avoid jitter?
                        pet.followObject.position = lapPosition.Value;
                    pet.spawnable.needsUpdate = true;
                    yield return null;
                }
                logger.Msg($"lap {target.name} no more available");
            }

            // lap not available, climp up the player and go on shoulder :3
            if (animator != null)
            {
                logger.Msg($"Gonna climb on player");
                pet.SetSyncedParameter("lookTargetToggle", 1);
                pet.SetSyncedParameter("lookTargetSmooth", 0.1f);
                pet.SetSyncedParameter("followTargetWeight", 0.05f);
                pet.lookObject.position = head.position;
                pet.spawnable.needsUpdate = true;

                var footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Vector3 nextPosition = footL.position + 0.1f * footL.up + 0.05f * -footL.forward;
                // TODO: add crawling/climb animation instead
                var flyto = Add(new FlyTo(pet, () => nextPosition));
                flyto.reachedThresholdTime = 0;
                flyto.stopDistance = 0;
                flyto.reachedDistance = 0.02f;
                flyto.maxSpeed = 0.05f; // slower

                // TODO: improve follow so we don't need to check reach ourself!
                while (flyto.Step()) yield return null;

                // TODO: are the orientations of bone correct, to climb from front?
                logger.Msg($"climb leg");
                var legL = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                nextPosition = legL.position + 0.1f * -legL.forward; // TODO: how thick?
                flyto.Restart();
                while (flyto.Step()) yield return null;

                logger.Msg($"climb thigh");
                var thighL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                nextPosition = thighL.position + 0.1f * -thighL.forward;
                flyto.Restart();
                while (flyto.Step()) yield return null;

                logger.Msg($"climb chest");
                var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                nextPosition = chest.position + 0.1f * chest.forward;
                flyto.Restart();
                while (flyto.Step()) yield return null;

                logger.Msg($"climb neck");
                var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                nextPosition = neck.position + 0.1f * neck.forward;
                flyto.Restart();
                while (flyto.Step()) yield return null;

                pet.lookObject.position = head.position + 5f * head.forward; // look forward now
                pet.spawnable.needsUpdate = true;

                logger.Msg($"climb arm");
                var armR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                nextPosition = armR.position + 0.1f * Vector3.up;
                flyto.Restart();
                while (flyto.Step()) yield return null;

                // now pet stay on the arm (middle of upper arm), will look at different things
                // TODO: could go on top of head instead, especially if arm isn't available (not horizontal)
                var elbowR = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                flyto.getTarget = () => (armR.position + elbowR.position)/2 + 0.1f * Vector3.up + 0.01f * neck.forward; // rotate forward now
                flyto.maxSpeed = 10; // follow lock
                flyto.moveAnimation = 0; // no animation
                pet.SetAnimation(0);
                flyto.reachedThresholdTime = -1; // never stop
                flyto.Restart();
                pet.SetSyncedParameter("followTargetWeight", 0.9f); // stick to it
                pet.SetSyncedParameter("followTargetAimWeight", 0.3f); // much slower
                lookAt = Add(new LookAt(pet, () => head.position));
                var bistate = 0;
                while (true) // TODO: add stop condition
                {
                    waitDuration = UnityEngine.Random.Range(3f, 15f);
                    bistate = (bistate + 1) % 2;
                    logger.Msg(bistate == 1 ? $"look at player" : $"look forward");
                    if (bistate == 1)
                        lookAt.getTarget = () => head.position; // look at player again
                    else
                        lookAt.getTarget = () => head.position + 5f * head.forward; // look forward now

                    for (var wait = 0f; wait < waitDuration; wait += Time.deltaTime)
                    {
                        if (target == null || head == null) yield break;

                        flyto.Step();
                        //pet.followObject.position = elbowR.position;
                        lookAt.Step();

                        yield return null;
                    }

                    yield return null;
                }

                Remove(flyto);
                Remove(lookAt);
            }
            else
                logger.Warning($"Animator null, what to do?");

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

    // TODO: where to use WanderAround?
    private IEnumerable WanderAround(float distMin = 1.5f, float distMax = 5f, float angleMax = 75,
        bool stayInSight = true, bool stayOnNav = true, int sampleTries = 5)
    {
        pet.SetSound(0);
        pet.SetEyes(0);
        pet.SetSyncedParameter("lookTargetToggle", 0);

        while (true)
        {

            // pick random position around player and go there
            // TODO: not nice to move away from player! actually
            logger.Msg($"wander around player");
            var nextPosition = Vector3.zero;
            for (var tries = 0; tries < sampleTries; ++tries)
            {
                var dist = UnityEngine.Random.Range(distMin, distMax);
                var angle = Quaternion.Euler(0, UnityEngine.Random.Range(-angleMax, +angleMax), 0);
                var forward = ((head ?? target).forward with { y = 0 }).normalized; // in front of target
                nextPosition = target.position + angle * forward * dist;
                // we want our next point to be visible from player
                // and on the navmesh if possible. Try a few times
                if (stayInSight && Physics.Linecast(target.position, nextPosition))
                {
                    logger.Msg($"wander sample #{tries} fails by linecast");
                    continue;
                }
                if (!stayOnNav)
                    break; // success
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(nextPosition, out hit, 1f, new NavMeshQueryFilter() { agentTypeID = pet.agent.agentTypeID, areaMask = NavMesh.AllAreas }))
                {
                    logger.Msg($"wander sample #{tries} fails by navmesh");
                    continue;
                }
                logger.Msg($"found position with NavMesh to wander around player");
                nextPosition = hit.position; // on navmesh
                break; // success
            }

            // move there using Follow
            // TODO: should only allow nav move, no fly if possible
            var follow = Add(new Follow(pet, () => nextPosition));
            follow.reachedThresholdTime = 1;
            follow.stuckThresholdTime = 0.5f;
            follow.stopDistance = 0;
            follow.maxSpeed = 0.5f; // slower
            while (follow.Step()) yield return null;
            Remove(follow);
            var waitDuration = UnityEngine.Random.Range(1f, 5f);
            for (var wait = 0f; wait < waitDuration; wait += Time.deltaTime)
                yield return null; // wait a bit there
        }
    }

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

public class Fetch : Behavior
{
    public CVRPickupObject pickup;
    public CVRSpawnable spawnable;
    public PlayerDescriptor thrower;
    public Transform holdingObject;
    public float throwDistance = 5f;
    private ControllerRay cr;

    public Fetch(PuPet pet, CVRPickupObject pickup) : base(pet)
    {
        this.pickup = pickup;
        this.spawnable = pickup.GetComponent<CVRSpawnable>();
        this.holdingObject = pet.transform.Find("Armature_Kitsune/Root_Kitsune/Pivot_Kitsune/Kitsune_Root.003/Kitsune_Head_Rotate/Kitsune_Head/mouthHolder");
    }
    public override string StateToString() => $"pickup={pickup} spawnable={spawnable} holder={holdingObject} thrower={thrower}";

    public override void End()
    {
        base.End();
        if (cr != null) GameObject.Destroy(cr);
    }

    // TODO: what if we want to fetch an item without throwing it?
    public override IEnumerable Run()
    {
        var gofetch = GoFetch().GetEnumerator();
        while (pickup != null && gofetch.MoveNext()) yield return null;
    }

    // caller is responsible to stop if target becomes null
    public IEnumerable GoFetch()
    {
        var t = pickup.transform;
        var lookAt = Add(new LookAt(pet, () => t.position));
        lookAt.speed = 0.1f; // fast
        var triggerThrower = TriggerOnChange(() => thrower);
        logger.Msg($"waiting for thrower to grab");
        var lastGrabberId = "";
        while (thrower == null // wait until object is grabbed
            || (t.position - thrower.transform.position).magnitude < throwDistance) // wait until object is thrown
        {
            lookAt.Step();
            if (pickup.grabbedBy != "" && pickup.grabbedBy != lastGrabberId)
                thrower = GetPlayerById(pickup.grabbedBy); // pickup changed hand
            lastGrabberId = pickup.grabbedBy;
            triggerThrower((v1, v2) => logger.Msg($"thrower: {v1} -> {v2}, lastGrabberId={lastGrabberId}"));
            yield return null;
        }

        logger.Msg($"Thrown, follow pickup");

        var follow = Add(new Follow(pet, () => t.position));
        follow.stopDistance = 0.1f;
        follow.reachedDistance = 0.5f;
        follow.reachedThresholdTime = 1;
        while (follow.Step()) yield return null;

        // TODO: what if another player or pet already grabbed it? stop
        // TODO: while bringing back, it may get stolen too!

        cr = holdingObject.gameObject.AddComponent<ControllerRay>();
        cr.attachmentDistance = 0.1f;
        cr._enableTelepathicGrab = false;
        cr._enableHighlight = false;
        cr.Start(); // maybe create it earlier so it's ready
        logger.Msg($"Gonna grab pickup with {cr.name} <- {cr}");
        pickup.Grab(cr.transform, cr, holdingObject.position);

        lookAt.getTarget = follow.getTarget = () => thrower.transform.position;
        lookAt.speed = 0.05f;
        follow.stopDistance = 1;
        follow.reachedThresholdTime = 3;
        follow.Restart();
        while (follow.Step())
        {
            // TODO: this won't work with world props
            spawnable.needsUpdate = true;
            yield return null;
        }

        logger.Msg($"finish fetch");
        pickup.Drop();
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
