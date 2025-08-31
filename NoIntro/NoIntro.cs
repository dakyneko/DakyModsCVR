using MelonLoader;

[assembly:MelonGame(null, "ChilloutVR")]
[assembly:MelonInfo(typeof(NoIntro.NoIntroMod), "NoIntro", "1.0.3", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace NoIntro
{
    public class NoIntroMod : MelonMod
    {
        public override void OnInitializeMelon() => ABI_RC.Core.Savior.IntroManager.Skip(); // thanks kafe
    }
}
