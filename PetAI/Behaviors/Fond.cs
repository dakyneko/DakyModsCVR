using ABI_RC.Core.Player;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace PetAI.Behaviors;

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
