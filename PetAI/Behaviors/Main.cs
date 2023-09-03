using System.Collections;
using UnityEngine;

namespace PetAI.Behaviors;

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
