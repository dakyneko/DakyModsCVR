using ActionMenu;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.EventSystem;


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

            // TODO: add nice icon
            //protected override string? modIcon => base.modIcon;

            // TODO: sort by most recent first + bonus if persistent on disk
            // TODO: cut at last 5 most used/recent and add a paginated More pages
            // TODO: display avatar image async, gotta download and cache it somewhere?

            override protected void OnGlobalMenuLoaded(Menus menus)
            {
                ModsMainMenu(menus).Add(new MenuItem()
                {
                    name = modName,
                    icon = modIcon,
                    action = BuildCallbackMenu("avatars", () => ViewManager.Instance._avatars.Select(avatar => new MenuItem()
                    {
                        name = avatar.AvatarName,
                        action = BuildButtonItem("avatar_" + avatar.AvatarName, () =>
                        {
                            AssetManagement.Instance.LoadLocalAvatar(avatar.AvatarId);
                            ActionMenuMod.Toggle(false);
                        }),
                    }).ToList()),
                });
            }
        }
    }
}