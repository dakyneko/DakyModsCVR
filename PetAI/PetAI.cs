using MelonLoader;
using UnityEngine;
using ABI.CCK.Components;
using System.Linq;
using System.Collections.Generic;

using ActionMenu;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using UnityEngine.AI;
using System.Security.Policy;
using Unity.Jobs.LowLevel.Unsafe;
using Gaia;
using System.Collections;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(PetAI.PetAIMod), "PetAI", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]
[assembly:MelonAdditionalDependencies("ActionMenu")]

namespace PetAI
{
    public class PetAIMod : MelonMod
    {
        private static MelonLogger.Instance logger;
        private static PetAIMod instance;
        private Menu menu;

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;
            instance = this;
			
            menu = new Menu();
        }
		
        public class Menu : ActionMenu.ActionMenuMod.Lib
        {
            protected override string modName => "PetAI";
            protected override string? modIcon => "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAMAAABg3Am1AAAAxHpUWHRSYXcgcHJvZmlsZSB0eXBlIGV4aWYAAHjabVBRDsMgCP33FDsCAiocx65tshvs+EOk3drsJTyRR55I2t6vPT0GMHPi0qRqrWBgZcVuicBEd87Azg4Kye6XejoFtBJ9O6VG/1HPp8E8umXlx0ieISxXQTn85WYUD9GYCC1Zw0jDiHAKOQz6/BZUlfb7hWWDK2RGGqQVdjcL9/udm21vLfYOIW6UCYyJeA5AIyhRt0ScizVmzzOxV1pMYgv5t6cD6QNJWFnygwkZywAAAYRpQ0NQSUNDIHByb2ZpbGUAAHicfZE9SMNAHMVfU0uLtCjYQcQhQ3Wyi4o4lioWwUJpK7TqYHLpFzQxJCkujoJrwcGPxaqDi7OuDq6CIPgB4uripOgiJf4vKbSI8eC4H+/uPe7eAUKrzlSzLwGommVkU0mxUFwRg68IIYBBRBCUmKmncwt5eI6ve/j4ehfnWd7n/hwRpWQywCcSJ5huWMTrxDObls55nzjKqpJCfE48YdAFiR+5Lrv8xrnisMAzo0Y+O0ccJRYrPSz3MKsaKvE0cUxRNcoXCi4rnLc4q/UG69yTvzBc0pZzXKc5ihQWkUYGImQ0UEMdFuK0aqSYyNJ+0sM/4vgz5JLJVQMjxzw2oEJy/OB/8Ltbszw16SaFk0DgxbY/xoDgLtBu2vb3sW23TwD/M3Cldf0bLWD2k/RmV4sdAQPbwMV1V5P3gMsdYPhJlwzJkfw0hXIZeD+jbyoCQ7dA/6rbW2cfpw9AnrpaugEODoHxCmWvebw71Nvbv2c6/f0ANdpyjjqkmwQAAA12aVRYdFhNTDpjb20uYWRvYmUueG1wAAAAAAA8P3hwYWNrZXQgYmVnaW49Iu+7vyIgaWQ9Ilc1TTBNcENlaGlIenJlU3pOVGN6a2M5ZCI/Pgo8eDp4bXBtZXRhIHhtbG5zOng9ImFkb2JlOm5zOm1ldGEvIiB4OnhtcHRrPSJYTVAgQ29yZSA0LjQuMC1FeGl2MiI+CiA8cmRmOlJERiB4bWxuczpyZGY9Imh0dHA6Ly93d3cudzMub3JnLzE5OTkvMDIvMjItcmRmLXN5bnRheC1ucyMiPgogIDxyZGY6RGVzY3JpcHRpb24gcmRmOmFib3V0PSIiCiAgICB4bWxuczp4bXBNTT0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL21tLyIKICAgIHhtbG5zOnN0RXZ0PSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvc1R5cGUvUmVzb3VyY2VFdmVudCMiCiAgICB4bWxuczpHSU1QPSJodHRwOi8vd3d3LmdpbXAub3JnL3htcC8iCiAgICB4bWxuczpkYz0iaHR0cDovL3B1cmwub3JnL2RjL2VsZW1lbnRzLzEuMS8iCiAgICB4bWxuczp0aWZmPSJodHRwOi8vbnMuYWRvYmUuY29tL3RpZmYvMS4wLyIKICAgIHhtbG5zOnhtcD0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wLyIKICAgeG1wTU06RG9jdW1lbnRJRD0iZ2ltcDpkb2NpZDpnaW1wOjQ4ZDFkOTBlLWVlNzktNGM1My04NTZkLTUyNmM0ZGI5ODhkMSIKICAgeG1wTU06SW5zdGFuY2VJRD0ieG1wLmlpZDpkNTcxYmExNi0xMTBhLTRiY2ItYjBmYS1jNzVmN2Q1MzkzYTciCiAgIHhtcE1NOk9yaWdpbmFsRG9jdW1lbnRJRD0ieG1wLmRpZDphMGQ0NGEzNC01MWU2LTQwZTYtYmRjOS1kYjQzZGM4NDc0MDEiCiAgIEdJTVA6QVBJPSIyLjAiCiAgIEdJTVA6UGxhdGZvcm09IldpbmRvd3MiCiAgIEdJTVA6VGltZVN0YW1wPSIxNjkxMjUyMDQ4NzE3MDQ3IgogICBHSU1QOlZlcnNpb249IjIuMTAuMzQiCiAgIGRjOkZvcm1hdD0iaW1hZ2UvcG5nIgogICB0aWZmOk9yaWVudGF0aW9uPSIxIgogICB4bXA6Q3JlYXRvclRvb2w9IkdJTVAgMi4xMCIKICAgeG1wOk1ldGFkYXRhRGF0ZT0iMjAyMzowODowNVQxODoxNDowNyswMjowMCIKICAgeG1wOk1vZGlmeURhdGU9IjIwMjM6MDg6MDVUMTg6MTQ6MDcrMDI6MDAiPgogICA8eG1wTU06SGlzdG9yeT4KICAgIDxyZGY6U2VxPgogICAgIDxyZGY6bGkKICAgICAgc3RFdnQ6YWN0aW9uPSJzYXZlZCIKICAgICAgc3RFdnQ6Y2hhbmdlZD0iLyIKICAgICAgc3RFdnQ6aW5zdGFuY2VJRD0ieG1wLmlpZDplMjU5YTQ2MC01NTFkLTRhNjAtODQyNy0xM2U3OGY3ZGI3NGMiCiAgICAgIHN0RXZ0OnNvZnR3YXJlQWdlbnQ9IkdpbXAgMi4xMCAoV2luZG93cykiCiAgICAgIHN0RXZ0OndoZW49IjIwMjMtMDgtMDVUMTg6MTQ6MDgiLz4KICAgIDwvcmRmOlNlcT4KICAgPC94bXBNTTpIaXN0b3J5PgogIDwvcmRmOkRlc2NyaXB0aW9uPgogPC9yZGY6UkRGPgo8L3g6eG1wbWV0YT4KICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIAogICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgCiAgICAgICAgICAgICAgICAgICAgICAgICAgIAo8P3hwYWNrZXQgZW5kPSJ3Ij8+mZAnCwAAAwBQTFRFAAAA////AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAZ3bsYwAAAAF0Uk5TAEDm2GYAAAABYktHRACIBR1IAAAACXBIWXMAABmNAAAZjQEn17ZGAAAAB3RJTUUH5wgFEA4ITEU8pQAAAMBJREFUSMfVldsSgCAIROH/f7qaKWUXEHypyQfzskdG2Erk5aZnW80jvZHQNNLfsqGfq7n+GsAjJ1QpUgngSIONFFA490+AxoDL0lOpApiyUX+sHFflkc1OpieSmYlsbQRKDH4++BzvVoqGQOXLVwDZA4TSBLc2Y8olVVOxFxaF2bNxwnw7M6VWCt2aj1Ogsy759+dHwPald4EstJae7AaI95a+3wZkbbAOUelZUevB99rR+79oF+nL3RvcROSrdgBe6QFl+28VEgAAAABJRU5ErkJggg==";

            protected override List<MenuItem> modMenuItems()
            {
                return new List<MenuItem>()
                {
                    new MenuItem("Follow me", BuildToggleItem("followme", instance.ToggleFollowMe), instance.following != null),
                    new MenuItem("Follow", BuildCallbackMenu("follow_player", () =>
                        MetaPort.Instance.PlayerManager.NetworkPlayers.Select(
                            p => new MenuItem(p.Username, BuildButtonItem(p.Username, () => instance.FollowPlayer(p)))).ToList())),
                    new MenuItem("Animate", BuildCallbackMenu("animate", () =>
                        (new List<string> { "Idle", "Licky", "Scratch", "Hop", "Stand" }).Select(
                            (x, i) => new MenuItem(x, BuildButtonItem(x, () => instance.Animate(i)))).ToList())),
                    new MenuItem("Mouth", BuildCallbackMenu("mouth", () =>
                        (new List<string> { "tongue", "Idle", ":O", ":/", ":(", ":<", ":>" }).Select(
                            (x, i) => new MenuItem(x, BuildButtonItem(x, () => instance.SetMouth(i-1)))).ToList())),
                    new MenuItem("Eyes", BuildCallbackMenu("eyes", () =>
                        (new List<string> { "Idle", "Happy", "Calm" }).Select(
                            (x, i) => new MenuItem(x, BuildButtonItem(x, () => instance.SetEyes(i)))).ToList())),
                    new MenuItem("Sound", BuildCallbackMenu("sound", () =>
                        (new List<string> { "None", "Kevie", "Meow" }).Select(
                            (x, i) => new MenuItem(x, BuildButtonItem(x, () => instance.SetSound(i)))).ToList())),
                };
            }
        }

        private Transform following = null, lookingAt = null;
        private Transform pet = null, followObject = null, lookObject = null, headObject;
        //private Animator animator = null;
        private CVRSpawnable spawnable = null;
        private Dictionary<string, int> syncedIds = null;
        
        private void FindGameObject()
        {
            if (pet != null) return;

            // TODO: add config for name suffix?
            pet = GameObject.Find("PetAI_Daky")?.transform;
            headObject = pet?.Find("Armature_Kitsune/Root_Kitsune/Pivot_Kitsune/Kitsune_Root.003/Kitsune_Head_Rotate/Kitsune_Head");
            var parent = pet?.transform?.parent;
            followObject = parent?.Find("followTarget");
            lookObject = parent?.Find("lookTarget");
            //animator = pet?.GetComponent<Animator>();
            spawnable = parent?.GetComponent<CVRSpawnable>();
            if (pet == null || spawnable == null)
            {
                logger.Error($"Pet not found: pet={pet} spawnable={spawnable}");
                return;
            }

            if (headObject != null)
            {
                var cb = headObject.gameObject.AddComponent<TriggerCallback>();
                cb.EnterListener += other => logger.Msg($"head trigger enter {other.name} {other}");
                cb.ExitListener += other => logger.Msg($"head trigger exit {other.name} {other}");
            }
            else
                logger.Warning($"Failed to find pet head object");

            syncedIds = new();
            var i = 0;
            foreach (var v in spawnable.syncValues)
                syncedIds[v.name] = i++;

            logger.Msg($"Found pet {pet}: spawnable={spawnable} follow={followObject} look={lookObject}");
        }

        private void EnableFollow()
        {
            SetSyncedParameter("followTargetWeight", 0.01f); // TODO: speed
        }
        private void EnableLookAt()
        {
            SetSyncedParameter("lookTargetToggle", 1);
            SetSyncedParameter("lookTargetSmooth", 0.01f); // TODO: speed
        }

        private void DisableFollow()
        {
            following = null;
            SetSyncedParameter("followTargetWeight", 0);
        }

        private void DisableLookAt()
        {
            lookingAt = null;
            SetSyncedParameter("lookTargetToggle", 0);
            SetSyncedParameter("lookTargetSmooth", 0);
        }

        private void FollowPlayer(CVRPlayerEntity p)
        {
            FindGameObject();
            if (pet == null) return;

            var pm = p?.PuppetMaster;
            following = pm?.gameObject?.transform;
            if (following == null) logger.Warning($"Player avatar not found");
            var head = pm?._animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) logger.Warning($"Player {p} head not found");
            lookingAt = head ?? following;
            EnableFollow();
            EnableLookAt();
        }

        private void ToggleFollowMe(bool enable)
        {
            if (enable)
                FollowMe();
            else
            {
                SetAnimation(0);
                DisableFollow();
            }
        }

        private void FollowMe()
        {
            FindGameObject();
            if (pet == null) return;

            following = PlayerSetup.Instance.gameObject.transform;
            if (following == null) logger.Warning($"Local avatar not found");
            var head = PlayerSetup.Instance._animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) logger.Warning($"Local head not found");
            lookingAt = head ?? following;
            EnableFollow();
            EnableLookAt();
        }

        private void SetSyncedParameter(string name, float value) => spawnable.SetValue(syncedIds[name], value);
        private float GetSyncedParameter(string name) => spawnable.GetValue(syncedIds[name]);

        private void SetEyes(int code) => SetSyncedParameter("eyes", code);
        private void SetMouth(int code) => SetSyncedParameter("mouth", code);
        private void SetAnimation(int code) => SetSyncedParameter("animation", code);
        

        private void Animate(int code)
        {
            FindGameObject();
            if (pet == null) return;
            SetAnimation(code);
            DisableLookAt();
        }

        private void SetSound(int code)
        {
            FindGameObject();
            if (pet == null) return;
            SetSyncedParameter("sound", code);
        }

        public override void OnFixedUpdate()
        {
            if (pet == null) return;
            if (following == null && lookingAt == null) return;

            if (lookingAt != null)
            {
                lookObject.position = lookingAt.position;
                spawnable.needsUpdate = true;
            }
            if (following != null)
            {
                var dist = following.position - pet.position;
                if (dist.magnitude < 1.05)
                {
                    if (GetSyncedParameter("animation") != 3) return; // not running = ignore
                    SetAnimation(0); // stop walking animation
                    followObject.position = pet.position + 0.01f * dist.normalized; // stay there
                    spawnable.needsUpdate = true;
                }
                else
                {
                    SetAnimation(3);
                    followObject.position = pet.position + 1f * dist.normalized; // TODO: speed
                    spawnable.needsUpdate = true;
                }
            }
        }
    }
}
