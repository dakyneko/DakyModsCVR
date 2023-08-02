using HarmonyLib;
using MelonLoader;
using SkipIntro = ABI_RC.Core.Savior.SkipIntro;
using RefFlags = System.Reflection.BindingFlags;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(NoIntro.NoIntroMod), "NoIntro", "1.0.1", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace NoIntro
{
    public class NoIntroMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            // Thank you DragonPlayer for your help ;)
            HarmonyInstance.Patch(
                typeof(SkipIntro).GetMethod(nameof(SkipIntro.Start),  RefFlags.Instance | RefFlags.NonPublic),
                new HarmonyMethod(AccessTools.Method(typeof(NoIntroMod), nameof(OnStart))));
        }

        private static bool OnStart(SkipIntro __instance)
        {
            __instance.Skip();
            return false;
        }
    }
}
