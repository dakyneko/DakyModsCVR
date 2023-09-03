using ABI.CCK.Components;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using System.Collections;
using UnityEngine;

namespace PetAI.Behaviors;
using static Daky.Dakytils;

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
        follow.reachedDistance = 1.2f;
        follow.reachedThresholdTime = 3;
        follow.Restart();
        while (follow.Step())
        {
            // TODO: this won't work with world props
            // TODO: do we even need to set this since we're grabbing it?
            if (spawnable != null)
                spawnable.needsUpdate = true;
            yield return null;
        }

        logger.Msg($"finish fetch");
        pickup.Drop();
    }
}
