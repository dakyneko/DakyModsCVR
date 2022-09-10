# Action Menu

Made from scratch. Documentation is coming soon.

There are two ways to customize it.

## JSON Overrides

Through json overrides. No code required. Just put your json file at the right place and it will be loaded.
  - For Global overrides, check `OverrideExamples/GlobalOverrides/dakytest.json` for an example. It needs to be put into your CVR directory under `UserData\ActionMenu\GlobalOverrides\`. Any name is fine with extension .json of course.
  - For Avatar overrides, check `OverrideExamples/AvatarOverrides/for_842eb3b7-6bd7-4cd0-a2d1-214863aca5a4.json` for an example. It needs to be put into your CVR directory under `UserData\ActionMenu\AvatarOverrides\`. The name is important, because it depends on your avatar id, it will only apply for that avatar. The format is `for_` followed by your avatar id and .json like the example.

## Modding C#

If you want to mod it. Take a look at `ActionMenuExampleMod/` at the root of this repository.
