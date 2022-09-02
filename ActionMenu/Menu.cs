using Newtonsoft.Json;
using System.Collections.Generic;

namespace ActionMenu
{
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
        public object? min_value;
        public object? max_value;
        public object? default_value;
        public object? min_value_x; // TODO: put into separate nullable struct?
        public object? max_value_x;
        public object? default_value_x;
        public object? min_value_y;
        public object? max_value_y;
        public object? default_value_y;
        public bool? toggle;
        public bool? exclusive_option; // highlight a single option in current menu
        public float? duration; // in seconds (float)
    }

    // defines modifications to perform on existing Menus: removing, adding and replacing items in them
    public struct MenusPatch
    {
        // menu name -> item names to remove
        public Dictionary<string, HashSet<string>> remove_items;
        // menu name -> item to add
        public Menus add_items;
        // menu name -> menu to completely replace
        // if it didn't exist, it is created
        public Menus overwrites;
    }

    public struct MenuSettings
    {
        public bool in_vr;
        public bool flick_selection;
        public bool boring_back_button;
    }
}