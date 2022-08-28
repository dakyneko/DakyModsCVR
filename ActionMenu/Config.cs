using Newtonsoft.Json;
using System.Collections.Generic;

namespace ActionMenu
{

    struct Config
    {
        public Dictionary<string, List<MenuItem>> menus;
    }
    struct MenuItem
    {
        public string? name;
        public string? icon;
        public ItemAction action;
    } 
    struct ItemAction
    {
        public string type;
        public string? menu;
        [JsonProperty(PropertyName = "event")]
        public string? event_;
        public string? control;
        public string? parameter;
        public object? value;
        public object? min_value;
        public object? max_value;
        public object? default_value;
        public bool? toggle;
        public bool? exclusive_option; // highlight a single option in current menu
        public float? duration;
    }
}