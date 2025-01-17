using ABI.CCK.Components;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PetAI.Behaviors;

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
                            if (MetaPort.Instance.ownerId == pickup.GrabbedBy) // local
                                playerDesc = PlayerSetup.Instance.gameObject.GetComponent<PlayerDescriptor>();
                            else
                            {
                                var player = MetaPort.Instance.PlayerManager.NetworkPlayers.FirstOrDefault(p => p.Uuid == pickup.GrabbedBy);
                                playerDesc = player?.PlayerDescriptor;
                            }
                            logger.Msg($"Found player from pickup grabbed by: {playerDesc} <- {pickup.GrabbedBy} <- {pickup}");
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
