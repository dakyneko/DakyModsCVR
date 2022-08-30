
namespace ActionMenu
{
    public class API {
        public static event OnAvatarMenuLoadedEvent? OnOnAvatarMenuLoaded;
        public delegate void OnAvatarMenuLoadedEvent(string avatarGuid, Menus menus);
        internal static void InvokeOnAvatarMenuLoaded(string avatarGuid, Menus menus) =>
            OnOnAvatarMenuLoaded?.Invoke(avatarGuid, menus);

        public static event OnGlobalMenuLoadedEvent? OnOnGlobalMenuLoaded;
        public delegate void OnGlobalMenuLoadedEvent(Menus menus);
        internal static void InvokeOnGlobalMenuLoaded(Menus menus) =>
            OnOnGlobalMenuLoaded?.Invoke(menus);
    }
}
