using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using System;
using System.Linq;
using System.Reflection;

namespace Daky;

internal class DakyBTKUI
{
    public static void AutoGenerateCategory(MelonPreferences_Category prefs, string? name = null)
    {
        name ??= prefs.Identifier;

        void MenuRegenerate(CVR_MenuManager _)
        {
            BTKUILib.QuickMenuAPI.OnMenuRegenerate -= MenuRegenerate;
            var category = BTKUILib.QuickMenuAPI.MiscTabPage.AddCategory(name);
            MelonPrefAuto(category, prefs);
        }
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += MenuRegenerate;
    }

    public static void MelonPrefAuto(BTKUILib.UIObjects.Category category, MelonPreferences_Category prefs)
    {
        foreach (var entry_ in prefs.Entries)
        {
            switch (entry_)
            {
                case MelonPreferences_Entry<bool> entry: {
                    var t = category.AddToggle(entry.DisplayName, entry.Description ?? "", entry.Value);
                    t.OnValueUpdated = v => entry.Value = v;
                    break;
                }

                case MelonPreferences_Entry<int> entry: {
                    var b = category.AddButton(entry.DisplayName, "", entry.Description ?? "");
                    b.OnPress = () => BTKUILib.QuickMenuAPI.OpenKeyboard(entry.Value.ToString(), v => entry.Value = int.Parse(v));
                    break;
                }

                case MelonPreferences_Entry<string> entry: {
                    var b = category.AddButton(entry.DisplayName, "", entry.Description ?? "");
                    b.OnPress = () => BTKUILib.QuickMenuAPI.OpenKeyboard(entry.Value, v => entry.Value = v);
                    break;
                }

                default: {
                    var tpe = entry_.GetReflectedType();
                    if (tpe.IsEnum)
                    {
                        var ms = (BTKUILib.UIObjects.Objects.MultiSelection)
                            typeof(DakyBTKUI)
                            .GetMethod("EnumToMultiSelection", BindingFlags.Static | BindingFlags.Public)
                            .MakeGenericMethod(tpe)
                            .Invoke(null, new object[] { category, entry_.DisplayName, entry_ });
                        var b = category.AddButton(entry_.DisplayName, "", entry_.Description ?? "");
                        b.OnPress = () => BTKUILib.QuickMenuAPI.OpenMultiSelect(ms);
                    }
                    else
                    {
                        MelonLogger.Warning($"Pref {entry_.DisplayName} won't have an automatically generated entry in BTKUILib");
                    }
                    break;
                }
            }
        }
    }

    public static BTKUILib.UIObjects.Objects.MultiSelection EnumToMultiSelection<T>(BTKUILib.UIObjects.Category category, string name, MelonPreferences_Entry<T> entry) where T : Enum {
        var current = entry.Value;
        var currentId = (int) Convert.ChangeType(current, typeof(int));
        var vs = Enum.GetValues(typeof(T)).Cast<T>();
        var ms = new BTKUILib.UIObjects.Objects.MultiSelection(name, vs.Select(v => v.ToString()).ToArray(), currentId);
        ms.OnOptionUpdated = v => entry.Value = (T) Enum.ToObject(typeof(T), v);
        return ms;
    }
}
