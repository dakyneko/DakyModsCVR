using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using CVRPlayerEntity = ABI_RC.Core.Player.CVRPlayerEntity;
using MetaPort = ABI_RC.Core.Savior.MetaPort;

namespace LagFreeScreenshots.API
{
    public class MetadataV2
    {
        public readonly ScreenshotRotation ImageRotation;
        public readonly CurrentPlayerInfo? CurrentUser;
        public readonly CurrentInstanceInfo? CurrentInstance;
        public readonly List<CVRPlayerEntity> PlayerList;

        public MetadataV2(ScreenshotRotation imageRotation, CurrentPlayerInfo? currentUser, CurrentInstanceInfo? currentInstance, List<CVRPlayerEntity> playerList)
        {
            ImageRotation = imageRotation;
            CurrentUser = currentUser;
            CurrentInstance = currentInstance;
            PlayerList = playerList;
        }

        private static string FormatNumber(float v) => v.ToString(CultureInfo.InvariantCulture);

        public override string ToString()
        {
            var worldString = "null,Not in any world";
            if (CurrentInstance.HasValue)
                worldString = CurrentInstance.Value.WorldId + "," + CurrentInstance.Value.InstanceId;

            var authorString = "?,?";
            var positionString = "?,?,?";
            if (CurrentUser.HasValue) {
                authorString = CurrentUser.Value.Uuid + "," + CurrentUser.Value.Username;
                var pos = CurrentUser.Value.Transform.position;
                positionString = $"{FormatNumber(pos.x)},{FormatNumber(pos.y)},{FormatNumber(pos.z)}";
            }

            return "lfs|cvr|1|author:"
                + authorString
                + "|world:" + worldString
                + "|pos:" + positionString
                + (ImageRotation != ScreenshotRotation.NoRotation ? "|rq:" + ImageRotation : "")
                + "|players:" + string.Join(";", PlayerList.Select(PlayerListToString));
        }

        private static string PlayerListToString(CVRPlayerEntity p)
        {
            if (p == null) return "null,0,0,0,null";
            var pos = p.PlayerObject.transform.position;
            return $"{p.Uuid},{FormatNumber(pos.x)},{FormatNumber(pos.y)},{FormatNumber(pos.z)},{p.Username}";
        }

        public static List<CVRPlayerEntity> GetPlayerList(Camera camera)
        {
            var localPosition = camera.transform.position;

            // TODO: this won't include current user, is that OK?
            // TODO: this can be improved a lot (can miss people)
            var result = MetaPort.Instance.PlayerManager.NetworkPlayers.FindAll(p =>
            {
                var avatarRoot = p.PlayerObject.transform;
                var playerPositionTransform = avatarRoot?.GetComponent<Animator>()?.GetBoneTransform(HumanBodyBones.Head) ?? avatarRoot;
                var playerPosition = playerPositionTransform.position;
                Vector3 viewPos = camera.WorldToViewportPoint(playerPosition);

                if (viewPos.z < 2 && Vector3.Distance(localPosition, playerPosition) < 2)
                {
                    //User standing right next to photographer, might be visible (approx.)
                    return true;
                }
                else if (viewPos.x > -0.03 && viewPos.x < 1.03 && viewPos.y > -0.03 && viewPos.y < 1.03 && viewPos.z > 2 && viewPos.z < 30)
                {
                    //User in viewport, might be obstructed but still...
                    return true;
                }
                return false;
            }).ToList();

            return result;
        }

    }

    public struct CurrentPlayerInfo
    {
        public string Uuid;
        public string Username;
        public Transform Transform;
    }
    public struct CurrentInstanceInfo
    {
        public string InstanceId;
        public string WorldId;
    }
}