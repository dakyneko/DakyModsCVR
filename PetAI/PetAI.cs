using HarmonyLib;
using MelonLoader;
using UnityEngine;
using ABI.CCK.Components;
using System.Linq;
using ABI_RC.Core;
using System.Collections.Generic;

using ActionMenu;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using AwesomeTechnologies.Utility.Quadtree;

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
                    new MenuItem("Come here", BuildButtonItem("comehere", instance.ComeHere)),
                    new MenuItem("Licky", BuildButtonItem("licky", () => instance.Animate(1) )),
                    new MenuItem("Scratch", BuildButtonItem("scratch", () => instance.Animate(2) )),
                };
            }
        }

        private Transform towardTarget = null;
        private Transform pet = null;
        private Animator animator = null;
        private CVRSpawnable spawnable = null;
        private Dictionary<string, int> syncedIds = null;
        
        private void FindGameObject()
        {
            if (pet != null) return;

            // TODO: add config for name suffix?
            pet = GameObject.Find("PetAI_Daky")?.transform;
            animator = pet?.GetComponent<Animator>();
            spawnable = pet?.parent?.GetComponent<CVRSpawnable>(); ;
            if (pet == null || animator == null || spawnable == null)
            {
                logger.Error($"Pet not found (pet={pet}, a={animator}, spawnable={spawnable}");
                return;
            }

            syncedIds = new();
            var i = 0;
            foreach (var v in spawnable.syncValues)
                syncedIds[v.name] = i++;

            logger.Msg($"Found pet: {pet}");
        }

        private void ComeHere()
        {
            FindGameObject();
            if (pet == null) return;

            // TODO: get head
            towardTarget = GameObject.Find("_PLAYERLOCAL").transform; // TODO: how to get differently?
        }

        private void SetSyncedParameter(string name, float value) => spawnable.SetValue(syncedIds[name], value);

        private void SetAnimation(int code) => SetSyncedParameter("Animation", code);
        

        private void Animate(int code)
        {
            FindGameObject();
            if (pet == null) return;
            SetAnimation(code);
        }

        public override void OnUpdate()
        {
            if (towardTarget == null || pet == null) return;

            pet.LookAt(towardTarget); // TODO: slowly turn, head instead of whole body?
            var dist = towardTarget.position - pet.position;
            if (dist.magnitude < 1)
            {
                // stop walking
                if (animator.GetFloat("Animation") != 3) return; // not running = ignore
                SetAnimation(0);
            }
            else
            {
                pet.position += 0.01f * dist.normalized; // TODO: speed
                SetAnimation(3);
            }
            spawnable.needsUpdate = true;
        }
    }
}
