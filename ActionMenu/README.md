# Action Menu

Made from scratch. The UI itself is based on CoHTML with vanilla Javascript, HTML and CSS. Dynamic menus and modding is done in C#. Documentation is coming soon.

## Avatar parameters

The avatar submenu is automatically generated from the avatar CVR "Advanced Settings". If you're the owner of the avatar there are two things you can do to help the Action Menu to look nice:

 - You can use slashes `/` in the "Name" of parameters to make submenu. For example let's say you have two parameters `Clothing/Dress/Long` and `Clothing/Dress/Short`. That means the hierarchy of menu will be: Avatar > Clothing > Dress and there will be Long and Short.
 - You can append "Impulse" in the "Name" of your parameter to make it a "Button" type, meaning it will set its value for a few seconds only. Useful to trigger things.

 If you aren't the owner of the avatar you can still customize through JSON overrides, see below.

# Customization

There are two ways to customize it.

## JSON Overrides

Through json overrides. No code required. Just put your json file at the right place and it will be loaded.
  - For Global overrides, check `OverrideExamples/GlobalOverrides/dakytest.json` for an example. It needs to be put into your CVR directory under `UserData\ActionMenu\GlobalOverrides\`. Any name is fine with extension .json of course.
  - For Avatar overrides, check `OverrideExamples/AvatarOverrides/for_842eb3b7-6bd7-4cd0-a2d1-214863aca5a4.json` for an example. It needs to be put into your CVR directory under `UserData\ActionMenu\AvatarOverrides\`. The name is important, because it depends on your avatar id, it will only apply for that avatar. The format is `for_` followed by your avatar id and .json like the example.

## Modding C#

If you want to mod it. Take a look at `ActionMenuExampleMod/` at the root of this repository.
