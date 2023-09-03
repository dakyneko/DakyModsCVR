using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PetAI.Behaviors;

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
