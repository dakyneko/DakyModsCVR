using HarmonyLib;
using MelonLoader;
using UnityEngine;
using ABI_RC.Core;
using ABI_RC.Core.Savior;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Scripts;
using System.Collections.Generic;
using System.Linq;
using cohtml;
using System;
using Newtonsoft.Json;
using System.IO;
using Valve.VR;

namespace ActionMenu
{
    public class ActionInputManager
    { 

        public static bool useOneHandedControls = true;
        public static bool useLeftHand = true;
        
        public static Vector2 inputJoystick;
        public static float inputTrigger;

        private static Vector2 leftStickVector;
        private static Vector2 rightStickVector;
        private static SteamVR_Action_Vector2? vrMovementAction;
        private static SteamVR_Action_Vector2? vrLookAction;

        internal static void HandleDesktopInput(ref Vector2 ___lookVector, ref float ___interactRightValue)
        {
            //kindof works with gamepad now, but joystick doesn't snap back to center
            inputJoystick += ___lookVector / 10;
            inputJoystick = Vector2.ClampMagnitude(inputJoystick, 1f);
            ___lookVector = Vector2.zero;
            inputTrigger = ___interactRightValue;
        }

        internal static void HandleVRInput(ref Vector3 ___movementVector, ref Vector2 ___lookVector, ref float ___interactLeftValue, ref float ___interactRightValue)
        {
            // get our own joystick input for reliability and simplicity (Valve.VR/SteamVRActions)
            vrMovementAction = SteamVR_Actions.alphaBlendInteractive_WalkAction;
            vrLookAction = SteamVR_Actions.alphaBlendInteractive_ControllerRotation;
            leftStickVector = new Vector2(CVRTools.AxisDeadZone(vrMovementAction.GetAxis(SteamVR_Input_Sources.Any).x, (float)MetaPort.Instance.settings.GetSettingInt("ControlDeadZoneLeft") / 100f, true), CVRTools.AxisDeadZone(vrMovementAction.GetAxis(SteamVR_Input_Sources.Any).y, (float)MetaPort.Instance.settings.GetSettingInt("ControlDeadZoneLeft") / 100f, true));
            rightStickVector = new Vector2(CVRTools.AxisDeadZone(vrLookAction.GetAxis(SteamVR_Input_Sources.Any).x, (float)MetaPort.Instance.settings.GetSettingInt("ControlDeadZoneRight") / 100f, true), CVRTools.AxisDeadZone(vrLookAction.GetAxis(SteamVR_Input_Sources.Any).y, (float)MetaPort.Instance.settings.GetSettingInt("ControlDeadZoneRight") / 100f, true));

            if (useLeftHand) // left hand
            {
                inputJoystick = leftStickVector;
                //allows opposite hand to control joystick input if enabled, otherwise disable input
                
                //lookvector is already handled by right stick
                ___movementVector = useOneHandedControls ? new Vector3(rightStickVector.x * Math.Abs(rightStickVector.y) * 0.5f, 0, rightStickVector.y) : Vector3.zero;
                inputTrigger = ___interactLeftValue;
            }
            else // right hand
            {
                inputJoystick = rightStickVector;
                //allows opposite hand to control joystick input if enabled, otherwise disable input

                //handling lookvector with leftstick is special as we need to make sure to respect user turnspeed, otherwise we are a bayblade
                ___lookVector = useOneHandedControls ? leftStickVector * (float)MetaPort.Instance.settings.GetSettingInt("ControlTurnSpeed") / 100f : Vector3.zero;
                ___movementVector = useOneHandedControls ? new Vector3(leftStickVector.x * Math.Abs(leftStickVector.y) * 0.5f, 0, leftStickVector.y) : ___movementVector;
                inputTrigger = ___interactRightValue;
            }
        }
    }
}
