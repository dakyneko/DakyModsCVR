using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace ActionMenu
{
    using static Daky.Dakytils;

    public class Menus : Dictionary<string, List<MenuItem>> { }

    public struct Menu
    {
        public Menus menus;
    }
    public struct MenuItem
    {
        public string? name;
        public string? icon;
        public ItemAction action;
        public bool? enabled;

        public MenuItem(string name, ItemAction action, bool? enabled = null)
        {
            this.name = name;
            this.icon = null;
            this.action = action;
            this.enabled = enabled;
        }

        public MenuItem(string name, string icon, ItemAction action, bool? enabled = null)
        {
            this.name = name;
            this.icon = icon;
            this.action = action;
            this.enabled = enabled;
        }
    } 
    public struct ItemAction
    {
        public string type;
        public string? menu;
        [JsonProperty(PropertyName = "event")]
        public string? event_;
        public string[]? event_arguments;
        public string? control;
        public string? parameter;
        public object? value;
        public float? min_value;
        public float? max_value;
        public object? default_value;
        public float? min_value_x; // TODO: put into separate nullable struct?
        public float? max_value_x;
        public float? default_value_x;
        public float? min_value_y;
        public float? max_value_y;
        public float? default_value_y;
        public bool? toggle;
        public bool? exclusive_option; // highlight a single option in current menu
        public float? duration; // in seconds (float)
    }

    // defines modifications to perform on existing Menus: removing, adding items and replacing whole menu
    public class MenusPatch
    {
        // menu name -> item names to remove
        [JsonProperty(PropertyName = "remove_items")]
        protected Dictionary<string, HashSet<string>> removeItems = new Dictionary<string, HashSet<string>>();
        // menu name -> item to add
        [JsonProperty(PropertyName = "add_items")]
        protected Menus addItems = new();
        // menu name -> menu to completely replace
        // if it didn't exist, it is created
        [JsonProperty(PropertyName = "overwrites")]
        protected Menus overwrites = new();

        public MenusPatch() { }

        // Remove elements by name in the specified menu. Won't fail if missing.
        public MenusPatch RemoveItems(string menu, params string[] items) => RemoveItems(menu, items);

        // Remove elements by name in the specified menu. Won't fail if missing.
        public MenusPatch RemoveItems(string menu, IEnumerable<string> itemNames)
        {
            this.removeItems.GetWithDefault(menu).UnionWith(itemNames);
            return this;
        }

        // Add new items at the end of the specified menu.
        public MenusPatch AddItems(string menu, params MenuItem[] items) => AddItems(menu, items);

        // Add new items at the end of the specified menu.
        public MenusPatch AddItems(string menu, IEnumerable<MenuItem> items)
        {
            this.addItems.GetWithDefault(menu).AddRange(items);
            return this;
        }

        // Completely replace the given menu with the provided items. If the menu doesn't exist, it is created.
        public MenusPatch Overwrite(string menu, IEnumerable<MenuItem> items)
        {
            this.overwrites[menu] = items.ToList();
            return this;
        }

        public void ApplyToMenu(Menus menus)
        {
            foreach (var x in removeItems)
            {
                if (!menus.TryGetValue(x.Key, out var items))
                    continue;
                var toRemove = x.Value;
                items.RemoveAll(item => item.name != null && toRemove.Contains(item.name));
            }

            foreach (var x in addItems)
            {
                var items = menus.GetWithDefault(x.Key);
                foreach (var item in x.Value)
                    items.Add(item);
            }

            foreach (var x in overwrites)
                menus[x.Key] = x.Value;
        }
    }

    // serves to communicate change of CVR and other game update to the menu so it can react to them
    public struct MenuItemValueUpdate
    {
        public ItemAction action; // only some fields are used to determine impacted menu items
    }
}