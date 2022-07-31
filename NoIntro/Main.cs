using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(NoIntro.NoIntroMod), "NoIntro", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]


namespace NoIntro
{
    public class NoIntroMod : MelonMod
    {
        public override void OnApplicationStart()
        {
            MelonLogger.Msg($"Hello mod!");
        }
    }
}
