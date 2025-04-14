# Action Menu

It's a remake from scratch of the one in VRChat but for CVR with CoHTML. Customization and mods in mind. The UI itself is rendered by CoHTML (built-in CVR) and written with vanilla Javascript, HTML and CSS. It is customizable without any code using JSON overrides and it is totally moddable with dynamic menus in C#.
For Mod developers looking into expanding the Action Menu, see [Modding API (C#)](https://github.com/dakyneko/DakyModsCVR/tree/master/ActionMenu#modding-api-c)

## Usage

By default the Action Menu can be opened:
- In Desktop by pressing e. The key is configurable in a MelonPreference.
- In VR, you have to assign a button on your controller in SteamVR Binding to "Open Action Menu". To do so, open SteamVR dashboard in VR, open your settings and look for your controller bindings, pick a controller button, assign a press or double press to the action menu.

There is an option to swap the short press for the Action Menu. Currently the menu is left-handed only but support for both hands may appear soon.

Possibly in the future, custom bindings may be possible wit [DynamicOpenVR](https://github.com/nicoco007/DynamicOpenVR) if enough interest appears.

## Avatar parameters

The avatar submenu is automatically generated from the avatar CVR "Advanced Settings". If you're the owner of the avatar there are two things you can do to help the Action Menu to look nice:

- You can use slashes `/` in the parameter "Name" text field to make submenu. For example let's say you have two parameters `Clothing/Dress/Long` and `Clothing/Dress/Short`. That means the submenus will be: Avatar > Clothing > Dress which will contain two items: Long and Short.
- You can append "Impulse" in the "Parameter" text field to make it a temporary trigger type. Meaning it will set its value for a second only. Useful to trigger things.

If you aren't the owner of the avatar you can still customize through JSON overrides, see below.
 
## Custom code: CSS and JS

Especially for developpers and designers, you can add your own CSS rules and Javascript code to extend the style and behavior of ActionMenu: you'll find those files UIResources/custom.css and UIResources/custom.js feel free to edit them.

## Avatar Overrides (JSON)

Even though the avatar menu is auto-generated, it can be patched with a json file afterward. By edited we mean adding menu items, remove some, replacing (eg: to add icons) / moving menus altogether (restructuring) once it's been generated on the fly. Changes are not persistent, menus are all built on the fly.

I recommend you check the example in `OverrideExamples/AvatarOverrides/for_842eb3b7-6bd7-4cd0-a2d1-214863aca5a4.json` . The file needs to be put into your CVR directory under `UserData\ActionMenu\AvatarOverrides\`. The filename is important, because it depends on your avatar id, it will only apply for that avatar. The format is `for_` followed by your avatar id and .json like the example.

Next is another illustrated example of an avatar with its parameter.

### Examples
Menu Structure:
```
 Structure                     | Action Menu Entry        | Advanced Avatar Entry
-------------------------------#--------------------------#----------------------
├── Clothing                   | Sub Menu                 | ---
│   ├── Hue                    | Slider                   | Slider
│   ├── Jacket                 | Toggle                   | Game Object Toggle
│   ├── Glasses                | Toggle                   | Game Object Toggle
│   ├── Hats                   | Sub Menu                 | Game Object Dropdown
│   │   ├── None               | Toggle (exclusive)       | - Dropdown Option
│   │   ├── Tophat             | Toggle (exclusive)       | - Dropdown Option
│   │   ├── Santa Hat          | Toggle (exclusive)       | - Dropdown Option
│   │   └── Witch Hat          | Toggle (exclusive)       | - Dropdown Option
├── Inputs                     | Sub Menu                 | ---
│   ├── Sample Input Single    | Slider                   | Input Single
│   └── Sample Input vector2   | 4 Axis Joystick          | Input Vector2
├── Overlay Position           | 4 Axis Joystick          | Joystick2D
└── ... Other Inputs ...
```

<details>
  <summary> Dropdown </summary> 

  ![image](https://user-images.githubusercontent.com/31988415/191075756-26923c47-911e-42c1-a6fb-0f7af9b9b9b3.png)
  ![image](https://user-images.githubusercontent.com/31988415/191089582-bb2821ee-8f3d-413e-94d5-b8b6747e795a.png)
</details>
<details>
  <summary> Toggle </summary> 

  ![image](https://user-images.githubusercontent.com/31988415/191076051-1a27fef9-b9de-4d6d-8568-b0d0f4b22235.png)
  ![image](https://user-images.githubusercontent.com/31988415/191089447-8e2e8519-c291-49c0-9590-3aabc88d86e9.png)
</details>
<details>
  <summary> Slider </summary> 

  ![image](https://user-images.githubusercontent.com/31988415/191076532-d9576773-069e-4ebd-a094-92bb862cbfe0.png)
  ![image](https://user-images.githubusercontent.com/31988415/191089317-95be7102-f55a-4d69-92e3-f713b97407e3.png)
</details>
<details>
  <summary> Input Single </summary> 

  The Input Single currently has a fixed range from 0 to 1, we are still evaluating how to approach this widget

  ![image](https://user-images.githubusercontent.com/31988415/191084986-4ff5823f-c3b3-4943-893d-4f781c3f50bf.png)
  ![image](https://user-images.githubusercontent.com/31988415/191088271-7c35d9b5-6325-430d-988d-405009071f80.png)
</details>
<details>
  <summary> Joystick2D </summary> 

  ![image](https://user-images.githubusercontent.com/31988415/191075664-33b31260-ca6a-4b08-ae8a-a770d668541e.png)
  ![image](https://user-images.githubusercontent.com/31988415/191087936-c519d21f-d925-43d3-a80b-21d5ae17eb4e.png)
</details>
<details>
  <summary> Input Vector2 </summary> 

  ![image](https://user-images.githubusercontent.com/31988415/191085027-0406f9b7-2304-405a-ac07-26d51dc26b82.png)
  ![image](https://user-images.githubusercontent.com/31988415/191088916-567c9d04-b535-417f-9e95-798977308c3c.png)
</details>
<details>
  <summary> [Not Implemented] Color </summary> 

  Not Implemented, we are still evaluating how to approach this widget, contributions are welcome!
  Current Ideas are as follows:
  - sub menu with 3 sliders for each color channel, possibly color coded background
  - 2D widget with HS and separate V slider, or similar
  - complete circular RGB color selector (its impossible to hit all colors, we probably won't do this)
  - just a few preset colors in a kind of palette, maybe hexagonal - this option doesn't expose all colors, but might be more practical
</details>
<details>
  <summary> [Not Implemented] Joystick3D </summary> 

  Not Implemented, we are still evaluating how to approach this widget, contributions are welcome!
</details>
<details>
  <summary> [Not Implemented] Vector3 </summary> 

  Not Implemented, we are still evaluating how to approach this widget, contributions are welcome!
</details>

## Global Overrides (JSON)

Earlier we talked about avatar submenu and now we'll talk how to customize any menu, anywhere: the global override. Menu Elements can be modified through json overrides. No code required. Just put your json file at the right place and it will be loaded.
  - For Global overrides, check `OverrideExamples/GlobalOverrides/dakytest.json` for an example. It needs to be put into your CVR directory under `UserData\ActionMenu\GlobalOverrides\`. Any name is fine with extension .json of course.

## Modding API (C#)

If you want to mod it. Take a look at `ActionMenuExampleMod/` or `ActionMenuAvatarsList` at the root of the repository (folder above). Those are good start for most mods.
