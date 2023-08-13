using ABI.CCK.Components;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using Daky;
using MelonLoader;
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

    public Behavior(PuPet pet)
    {
        this.pet = pet;
        this.logger = pet.logger;
        this.iterator = Run().GetEnumerator();
        pet.RegisterBehavior(this);
    }

    public virtual IEnumerable Run() { yield break; }
    public bool Step() => iterator.MoveNext();
    public virtual void Start() { }
    public virtual void End() => pet.UnregisterBehavior(this);
    public override string ToString() => $"Behavior<{this.GetType().Name}>";
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

    public override void Start()
    {
        pet.SetSyncedParameter("lookTargetToggle", 1);
        pet.SetSyncedParameter("lookTargetSmooth", speed);
    }
    public override void End()
    {
        pet.SetSyncedParameter("lookTargetToggle", 0);
        pet.SetSyncedParameter("lookTargetSmooth", 0);
        base.End();
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
    public override string ToString() => base.ToString() + $"(lookingAt={lookingAt?.name})";
}

public class Follow : Behavior
{
    public Transform following;
    public NavMeshAgent agent;
    public float stopDistance = 1.05f, teleportDistance = 50;
    public bool reached = false, speedStalled = false, teleportIfTooFar = false;
    public float stuckThresholdTime = 1.5f, reachedThresholdTime = 3;
    public Vector3 toTarget = Vector3.zero, lastPosition = Vector3.zero;
    public float stuckTime = 0f, velocity = 0f;
    public enum EndReason { lostTarget, invalidPath, reached };
    public EndReason endReason = EndReason.lostTarget;

    // caller is responsible to stop if target becomes null
    public Follow(PuPet pet, Transform following) : base(pet)
    {
        this.following = following;
        this.agent = pet.agent;
    }
    public override void Start() => SetNavWeight();
    public override void End()
    {
        pet.SetAnimation(0);
        base.End();
    }

    public void SetNavWeight() => pet.SetSyncedParameter("followTargetWeight", 0.05f);
    public void SetFlyWeight() => pet.SetSyncedParameter("followTargetWeight", 0.02f);

    public override string ToString() => base.ToString() + $"(following={following?.name} velocity={velocity:0.00} stuckTime={stuckTime:0.00} reached={reached} speedStalled={speedStalled} agent.enabled={agent.enabled} toTarget.magnitude={toTarget.magnitude:F2})";

    public override IEnumerable Run()
    {
        Start();
        endReason = EndReason.lostTarget;
        agent.enabled = true;
        lastPosition = pet.transform.position;
        var forward = Forward().GetEnumerator();
        var reachedConsecutive = 0f;

        while (true)
        {
            var newPos = pet.transform.position;
            // smooth a bit for speedStalled, so it doesn't flap
            velocity = 0.5f * velocity + 0.5f * (newPos - lastPosition).magnitude / Time.deltaTime;
            speedStalled = velocity <= 0.1f;
            pet.SetAnimation(speedStalled ? 0 : 3); // hop or idle
            lastPosition = newPos;
            toTarget = following.position - newPos;

            if (!forward.MoveNext()) break;

            if (reached) reachedConsecutive += Time.deltaTime;
            else         reachedConsecutive = 0;

            if (reachedConsecutive >= reachedThresholdTime)
            {
                endReason = EndReason.reached;
                pet.spawnable.needsUpdate = true; // one last update?
                End();
                yield break;
            }

            yield return null;
        }
    }

    public IEnumerable Forward()
    {
        while (true)
        {
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
                    if (agent.isOnNavMesh
                        && agent.CalculatePath(following.position, p) // will be costly
                        && (p.corners.Last() - following.position).magnitude <= stopDistance)
                    {
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
        agent.enabled = true;
        SetNavWeight();
        while (true)
        {
            reached = toTarget.magnitude <= stopDistance // all good
                || (speedStalled && toTarget.magnitude < 2f); // stuck a bit further but navmesh sux anyway

            if (!reached) // no need to modify path if we reached, right?
                agent.destination = following.position;
            while (agent.pathPending) yield return null;

            yield return null;
        }
    }

    public IEnumerable ByFly()
    {
        agent.enabled = false;
        SetFlyWeight();
        while (true)
        {
            reached = toTarget.magnitude <= stopDistance;

            var v = reached ? 0.01f : 1f; // TODO: honor stopDistance
            pet.followObject.position = pet.transform.position + v * toTarget.normalized;

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
    public override string ToString() => base.ToString() + $"(scoreThreshold={scoreThreshold} winner={winner?.userName} pats={pats.Count}[{string.Join(", ", pats.Select(p => $"{p.Key}={p.Value.score:0.00}/{p.Value.count}") )}] )";

    public PatsLover(PuPet pet) : base(pet)
    {
        this.callback = pet.headTriggerCallback;
    }

    public override void Start()
    {
        this.callback.EnterListener += OnEnter;
        this.callback.ExitListener += OnExit;
        winner = null;
        pats.Clear(); // reset
    }
    public override void End()
    {
        this.callback.EnterListener -= OnEnter;
        this.callback.ExitListener -= OnExit;
        base.End();
    }

    private void OnEnter(Collider other) {
        var p = other.GetComponent<CVRPointer>();
        if (p?.type != "index" && p?.type != "grab" && p?.type != "hand") return;

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
                        }
                        // otherwise it's on a player directly
                        playerDesc ??= pat.collider.gameObject.GetComponentInParent<PlayerDescriptor>();
                        if (playerDesc == null)
                        {
                            logger.Error($"Can't find player from collider {pat.collider.name}");
                            pats.Remove(name);
                            continue;
                        }
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

    // caller is responsible to stop if target becomes null
    public Fond(PuPet pet, Transform target) : base(pet)
    {
        this.target = target;
    }

    public override IEnumerable Run()
    {
        var follow = new Follow(pet, target);
        var lookAt = new LookAt(pet, target);
        while (follow.Step() && lookAt.Step()) yield return null;
        follow.End(); lookAt.End();

        pet.SetSound(2);
        for (var wait = 0f; wait < 1000; wait += Time.deltaTime)
            yield return null; // puur for a bit
        pet.SetSound(0);

        // TODO: do more interaction
    }
    public override string ToString() => base.ToString() + $"(target={target?.name})";
}

public class Main : Behavior
{

    public Main(PuPet pet) : base(pet) { }

    public override IEnumerable Run()
    {
        while (true)
        {
            Transform target;
            var patsLover = new PatsLover(pet);
            while (patsLover.Step()) yield return null;
            if (patsLover.winner == null) yield break; // TODO: why it stopped?
            target = patsLover.winner.transform;
            patsLover.End();

            var fond = new Fond(pet, target);
            while (target != null && fond.Step()) yield return null;
            fond.End();
            //if (target == null) continue; // restart
            // TODO
        }
    }
}
