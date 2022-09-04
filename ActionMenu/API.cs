
namespace ActionMenu
{
    public class API {
        public static event OnAvatarMenuLoadedEvent? OnAvatarMenuLoaded;
        public delegate void OnAvatarMenuLoadedEvent(string avatarGuid, Menus menus);
        internal static void InvokeOnAvatarMenuLoaded(string avatarGuid, Menus menus) =>
            OnAvatarMenuLoaded?.Invoke(avatarGuid, menus);

        public static event OnGlobalMenuLoadedEvent? OnGlobalMenuLoaded;
        public delegate void OnGlobalMenuLoadedEvent(Menus menus);
        internal static void InvokeOnGlobalMenuLoaded(Menus menus) =>
            OnGlobalMenuLoaded?.Invoke(menus);
    }
}
