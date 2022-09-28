using ActionMenu;
using MelonLoader;
using System.Collections.Generic;

[assembly:MelonGame("Alpha Blend Interactive", "ChilloutVR")]
[assembly:MelonInfo(typeof(ActionMenuExample.ActionMenuExampleMod), "ActionMenuExample", "1.0.0", "daky", "https://github.com/dakyneko/DakyModsCVR")]

namespace ActionMenuExample
{
    public class ActionMenuExampleMod : MelonMod
    {

        private Menu lib;
        public override void OnApplicationStart()
        {
            lib = new Menu();
        }

        // Public library for all mods to use, you can extend this
        // for more extended doc, look at the base ActionMenu.ActionMenuMod.Lib
        public class Menu : ActionMenu.ActionMenuMod.Lib
        {
            // optionnal
            protected override string modName => "DakyMenu";
            // optionnal
            protected override string? modIcon => "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAYAAABXAvmHAAAACXBIWXMAAA7DAAAOwwHHb6hkAAAAG3RFWHRTb2Z0d2FyZQBDZWxzeXMgU3R1ZGlvIFRvb2zBp+F8AAAEwUlEQVRo3u3aeYiVVRgG8N+tUbMSs6wsaTdpIUqyjfa9tKg02wtUCooWyDZjhErDIgsiKCJbqMy0zcyKdhOiPY02w7KyBalIzWwZnTn9Me+Fj8vd585S+MDA+c53zvne52zv8753ciklFfAZmjAOb+kaHIL7sQ57lmuYq4LALxiItbgJU9HaSYb3wiRMjEn7FVs2ikAeb+B8/Nhg44fgERyYqesUAvmBx+G5gvoBGIodos/GUf9n9FmGL7GioN9Y3Il+Rb7TKQQg4S7Mxwgcjl2wQYXx2rA0+r2MMfFXDJ1KoCuwnsB6AusJ/I8I9ME//2UCR2MLzK6VwDJs100Evsf2UR6JGdgb39VC4O0C996VeAcHRXlcCLwXgkzVBO7Gxd1E4B5cEuVbcU2UR+DFagmcgjlVfGxNHLSmCu3WogWbVDHmqXg2ygtwaJQ/wP5I1RDoE+dgqzJtXsPJGByD9y/RbhX2ww+Yi2PKjPlz7P9/4vAuL5icIzG/GgJwXcQBpTAJUzKzs2+Jdh8EAWjG5DJjTsQtUb481GoWj+Hcagn0xcfYtcT75bgKO+NG5Eq0S7gBX2MaBpVotyRum78iyPkilG4Wq7F1tQTEnnsTG3Xywf07pPl78XxpyPZiOL4WAnA6ZlZxUOvFOpyNJ+N5h1j5Umdqcq0ExGGdWeUtUgvW4Jw43GKl5+OAMn3m1UNAHMS5ZfZwrVge1/V7meB+Fk6r0G9xLqU0EEdUCAU/wlcFdTvheezeQeO/CM/6TTxvisez3rYMVuZSSp9hjwoNzwsdkv/AH5kg/umYgHrwJkbht3jeJhIF+1bZvzWXUmqtIhAfhkVxPS4MQp9mHN30qKsFMzA+I5H3xDzsWMMYbbmU0u9F0hlZ/BgesQ27xZKvxGi8ntdU4ZSuL+MDsr5gajiy/AE8Km6eATVOwupcSumrIk4ii2bcHOVrM96xBRfi4Uzb8SHAesXMNkf9lFiptSHOpmf6XID70LuOLfh1LqX0Mo4t0eBb7BV7vgmfF3jjvGednJnN4/BE7OX8tno0rt8z8FJm1SZF/1ydZ+iVXEppGiYUeflnREHvFOjxYngIF8UMCxlwUmblmmN/L8pck/dGRq4juCOXUhqFpwperAivm9/j24ZHLBdavhp9VmVmOBUp948VOrYB/mN0LqU0IBxJ7/jIXFyRCds2DrlcTVT2Scz8shLvt4+V2KsBxrdgUN4T3x77fLb23wPy2AzP1HjP/xQkFhbU7xPGD26Q934RI8pJiYNjbw+pY/DVOCviVzgxpEG/BmqnMXiykEAOw+NQj6nCwVVSlnkZfFmDFezS8ElrswSGxSztqudjPB4oDOo3xLs16JDuwochsVuLZSWGxb3fu4ca3xJ5oo/K5YUmRLzaE3F1oW3FCOS0/9h2bg8zfob2HxdTJQJ5iTwHJ/QQ41+KiK2m7HTfuJVO7mbj54UI/KvYy0oxcRPuiNRGrhuMvwtXhk9RD4E8ztSe5N28iwxfoT2hPKtSw1qyEtvgtkh9dNZqpEjZXB2aSiMJ5DE8ApGR4fwagbbIcEzG+7V0rDcvJETe2IgBhtY5xpKIhR+Mcs3oCIGs3xiCw2J1dlf6fyW+w2LtWeoFYXSHDPgXi7Gca+peC28AAAAASUVORK5CYII=";

            // recommended way to make a mod menu
            protected override List<MenuItem> modMenuItems()
            {
                return new List<MenuItem>()
                {
                    new MenuItem("Super button",
                        BuildButtonItem("rawr", () => MelonLogger.Msg($"Big rawr"))),

                    new MenuItem("Toggle the rawr",
                        BuildToggleItem("rawr", (v) => MelonLogger.Msg($"toggle RAWR: {v}"))),

                    new MenuItem("Radial rawr",
                        BuildRadialItem("rawr", (v) => MelonLogger.Msg($"slider of RAWR loudness: {v}"), minValue: -1, maxValue: 1)),

                    new MenuItem("Moving rawr in 2D",
                        BuildJoystick2D("rawr", (x, y) => MelonLogger.Msg($"2D RAWR position: {x}, {y}"))),
                };
            }
        }
    }
}