using ActionMenu;
using MelonLoader;
using System.Linq;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.UI;


[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(ActionMenuAvatarsList.ActionMenuAvatarsListMod), "ActionMenuAvatarsList", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace ActionMenuAvatarsList
{
    public class ActionMenuAvatarsListMod : MelonMod
    {

        private Menu lib;
        public override void OnApplicationStart() => lib = new Menu();

        public class Menu : ActionMenu.ActionMenuMod.Lib
        {
            protected override string modName => "Avatars List";

            // TODO: sort by most recent first + bonus if persistent on disk
            // TODO: cut at last 5 most used/recent and add a paginated More pages

            override protected void OnGlobalMenuLoaded(Menus menus)
            {
                ModsMainMenu(menus).Add(new MenuItem()
                {
                    name = modName,
                    icon = "../CVRTest/gfx/nav-avatars.svg",
                    action = BuildCallbackMenu("avatars", () => ViewManager.Instance._avatars.Select(avatar => new MenuItem()
                    {
                        name = avatar.AvatarName,
                        icon = avatar.AvatarImageUrl,
                        action = BuildButtonItem("avatar_" + avatar.AvatarName, () =>
                        {
                            ActionMenuMod.Toggle(false);
                            AssetManagement.Instance.LoadLocalAvatar(avatar.AvatarId);
                        }),
                    }).ToList()),
                });

                ModsMainMenu(menus).Add(new MenuItem()
                {
                    name = "Props list", // TODO: separate mod?
                    icon = "../CVRTest/gfx/btn-props.svg",
                    action = BuildCallbackMenu("props", () => ViewManager.Instance._spawneables.Select(s => new MenuItem()
                    {
                        name = s.SpawnableName,
                        icon = s.SpawnableImageUrl,
                        action = BuildButtonItem("spawnable_" + s.SpawnableName, () =>
                        {
                            // TODO: should add "wait recenter then close"
                            //ActionMenuMod.Toggle(false);
                            PlayerSetup.Instance.DropProp(s.SpawnableId);
                        }),
                    }).ToList()),
                });
            }
        }
    }
}