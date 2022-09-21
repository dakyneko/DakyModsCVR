# Action Menu

Made from scratch. The UI itself is based on CoHTML with vanilla Javascript, HTML and CSS. Dynamic menus and modding is done in C#.   
For Mod developers looking into expanding the Action Menu, see [Modding API (C#)](https://github.com/dakyneko/DakyModsCVR/tree/master/ActionMenu#modding-api-c)

## Avatar parameters

The avatar submenu is automatically generated from the avatar CVR "Advanced Settings". If you're the owner of the avatar there are two things you can do to help the Action Menu to look nice:

- You can use slashes `/` in the "Name" of parameters to make submenu. For example let's say you have two parameters `Clothing/Dress/Long` and `Clothing/Dress/Short`. That means the hierarchy of menu will be: Avatar > Clothing > Dress and there will be Long and Short.
- You can append "Impulse" in the "Name" of your parameter to make it a "Button" type, meaning it will set its value for a few seconds only. Useful to trigger things.

If you aren't the owner of the avatar you can still customize through JSON overrides, see below.
 
### Avatar JSON Overrides

The Avatar Menu can be overriden via [JSON Overrides (link)](https://github.com/dakyneko/DakyModsCVR/tree/master/ActionMenu#json-overrides).

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
  The simplest, but jank way of implementing this would be via separate sliders for each axis, alternatively a 2D widget + slider could be considered.
</details>
<details>
  <summary> [Not Implemented] Vector3 </summary> 

  Not Implemented, we are still evaluating how to approach this widget, contributions are welcome!
  The simplest, but jank way of implementing this would be via separate sliders for each axis, alternatively a 2D widget + slider could be considered.
</details>

## JSON Overrides

Menu Elements can be modified through json overrides. No code required. Just put your json file at the right place and it will be loaded.
  - For Global overrides, check `OverrideExamples/GlobalOverrides/dakytest.json` for an example. It needs to be put into your CVR directory under `UserData\ActionMenu\GlobalOverrides\`. Any name is fine with extension .json of course.
  - For Avatar overrides, check `OverrideExamples/AvatarOverrides/for_842eb3b7-6bd7-4cd0-a2d1-214863aca5a4.json` for an example. It needs to be put into your CVR directory under `UserData\ActionMenu\AvatarOverrides\`. The name is important, because it depends on your avatar id, it will only apply for that avatar. The format is `for_` followed by your avatar id and .json like the example.

## Modding API (C#)

If you want to mod it. Take a look at `ActionMenuExampleMod/` at the root of this repository.
