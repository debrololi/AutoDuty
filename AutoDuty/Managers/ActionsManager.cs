﻿using AutoDuty.Data;
using AutoDuty.Helpers;
using AutoDuty.IPC;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.StringHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static AutoDuty.Helpers.ObjectHelper;

namespace AutoDuty.Managers
{
    internal class ActionsManager(AutoDuty _plugin, Chat _chat, TaskManager _taskManager)
    {
        public readonly List<(string, string, string)> ActionsList =
        [
            ("<-- Comment -->","comment?","Adds a Comment to the path; AutoDuty will do nothing but display them.\nExample: <-- Trash Pack #1 -->"),
            ("Wait","how long?", "Adds a Wait (for x milliseconds) step to the path; after moving to the position, AutoDuty will wait x milliseconds.\nExample: Wait|0.02, 23.85, -394.89|8000"),
            ("WaitFor","for?","Adds a WaitFor (Condition) step to the path; after moving to the position, AutoDuty will wait for a condition from the following list:\nCombat - waits until in combat\nIsReady - waits until the player is ready\nIsValid - waits until the player is valid\nIsOccupied - waits until the player is occupied\nBNpcInRadius - waits until a battle npc either spawns or path's into the radius specified\nExample: WaitFor|-12.12, 18.76, -148.05|Combat"),
            ("Boss","false", "Adds a Boss step to the path; after (and while) moving to the position, AutoDuty will attempt to find the boss object. If not found, AD will wait 10s at the position for the boss to spawn and will then Invoke the Boss Action.\nExample: Boss|-2.91, 2.90, -204.68|"),
            ("Interactable","interact with?", "Adds an Interactable step to the path; after moving to within 2y of the position, AutoDuty will interact with the object specified (recommended to input DataId) until either the object is no longer targetable, you meet certain conditions, or a YesNo/Talk addon appears.\nExample: Interactable|21.82, 7.10, 27.40|1004346 (Goblin Pathfinder)"),
            ("TreasureCoffer","false", "Adds a TreasureCoffer flag to the path; AutoDuty will loot any treasure coffers automatically if it gets within interact range of one (while Config Loop Option is on), this is just a flag to mark the positions of Treasure Coffers.\nNote: AutoDuty will ignore this Path entry when Looting is disabled entirely or Boss Loot Only is enabled.\nExample: TreasureCoffer|3.21, 6.06, -97.63|"),
            ("SelectYesno","yes or no?", "Adds a SelectYesNo step to the path; after moving to the position, AutoDuty will click Yes or No on this addon.\nExample: SelectYesno|9.41, 1.94, -311.25|Yes"),
            ("MoveToObject","Object Name?", "Adds a MoveToObject step to the path; AutoDuty will will move the object specified (recommend input DataId)"),
            ("DutySpecificCode","step #?", "Adds a DutySpecificCode step to the path; after moving to the position, AutoDuty will invoke the Duty Specific Action for this TerritoryType and the step # specified.\nExample: DutySpecificCode|174.68, 102.00, -66.46|1"),
            ("BossMod", "on / off", "Adds a BossMod step to the path; after moving to the position, AutoDuty will turn BossMod on or off.\nExample: BossMod|-132.08, -342.25, 1.98|Off"),
            ("Rotation", "on / off", "Adds a Rotation step to the path; after moving to the position, AutoDuty will turn Rotation Plugin on or off.\nExample: Rotation|-132.08, -342.25, 1.98|Off"),
            ("Target", "Target what?", "Adds a Target step to the path; after moving to the position, AutoDuty will Target the object specified (recommend inputing DataId)."),
            ("AutoMoveFor", "how long?", "Adds an AutoMoveFor step to the path; AutoDuty will turn on Standard Mode and Auto Move for the time specified in milliseconds (or until player is not ready).\nExample: AutoMoveFor|-18.21, 1.61, 114.16|3000"),
            ("ChatCommand","Command with args?", "Adds a ChatCommand step to the path; after moving to the position, AutoDuty will execute the Command specified.\nExample: ChatCommand|-5.86, 164.00, 501.72|/bmrai follow Alisaie"),
            ("StopForCombat","true/false", "Adds a StopForCombat step to the path; after moving to the position, AutoDuty will turn StopForCombat on or off.\nExample: StopForCombat|-1.36, 5.76, -108.78|False"),
            ("Revival", "false", "Adds a Revive flag to the path; this is just a flag to mark the positions of Revival Points, AutoDuty will ignore this step during navigation.\nUse this if the Revive Teleporter does not take you directly to the arena of the last boss you killed, such as Sohm Al.\nExample: Revival|33.57, -202.93, -70.30|"),
            ("ForceAttack",  "false", "Adds a ForceAttack step to the path; after moving to the position, AutoDuty will ForceAttack the closest mob.\nExample: ForceAttack|-174.24, 6.56, -301.67|"),
            ("Jump", "automove for how long before", "Adds a Jump step to the path; after AutoMoving, AutoDuty will jump.\nExample: Jump|0, 0, 0|200"),
            //("PausePandora", "Which feature | how long"),
            ("CameraFacing", "Face which Coords?", "Adds a CameraFacing step to the path; after moving to the position, AutoDuty will face the coordinates specified.\nExample: CameraFacing|720.66, 57.24, 9.18|722.05, 62.47, 15.55"),
            ("ClickTalk", "false", "Adds a ClickTalk step to the path; after moving to the position, AutoDuty will click the talk addon."),
            ("ConditionAction","condition;args,action;args", "Adds a ConditionAction step to the path; after moving to the position, AutoDuty will check the condition specified and invoke Action."),
            ("ModifyIndex", "what number (0-based)", "Adds a ModifyIndex step to the path; after moving to the position, AutoDuty will modify the index to the number specified.")
        ];

        public void InvokeAction(PathAction action)
        {
            try
            {
                if (action != null)
                {
                    var thisType = GetType();
                    var actionTask = thisType.GetMethod(action.Name);
                    _taskManager.Enqueue(() => actionTask?.Invoke(this, [action]), $"InvokeAction-{actionTask?.Name}");
                }
                else
                    Svc.Log.Error("no action");
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.ToString());
            }
        }
        
        public void Follow(PathAction action) => FollowHelper.SetFollow(ObjectHelper.GetObjectByName(action.Argument));

        public void SetBMSettings(PathAction action) => AutoDuty.Plugin.SetBMSettings(bool.TryParse(action.Argument, out bool defaultsettings) && defaultsettings);

        public unsafe void ConditionAction(PathAction action)
        {
            if (!action.Argument.Any(x => x.Equals('&'))) return;

            AutoDuty.Plugin.Action = $"ConditionAction: {action.Argument}";
            var conditionActionArray = action.Argument.Split("&");
            var condition = conditionActionArray[0];
            string[] conditionArray = [];
            if (condition.Any(x => x.EqualsAny(';')))
                conditionArray = condition.Split(";");
            var actions = conditionActionArray[1];
            string[] actionArray = [];
            if (actions.Any(x => x.EqualsAny(';')))
                actionArray = actions.Split(";");
            var invokeAction = false;
            var operation = new Dictionary<string, Func<object, object, bool>>
            {
              { ">", (x, y) => (float)x > (float)y },
              { ">=", (x, y) => (float)x >= (float)y },
              { "<", (x, y) => (float)x < (float)y },
              { "<=", (x, y) => (float)x <= (float)y },
              { "==", (x, y) => x == y },
              { "!=", (x, y) => x != y }
            };
            var operatorValue = string.Empty;
            var operationResult = false;
           
            switch (conditionArray[0])
            {
                case "GetDistanceToPlayer":
                    if (conditionArray.Length < 4) return;
                    if (!conditionArray[1].TryToGetVector3(out var vector3)) return;
                    if (!float.TryParse(conditionArray[3], out var distance)) return;
                    if (!(operatorValue = conditionArray[2]).EqualsAny(operation.Keys)) return;
                    var getDistance = GetDistanceToPlayer(vector3);
                    if (operationResult = operation[operatorValue](getDistance, distance))
                        invokeAction = true;
                    Svc.Log.Info($"Condition: {getDistance}{operatorValue}{distance} = {operationResult}");
                    break;
                case "ItemCount":
                    if (conditionArray.Length < 4) return;
                    if (!uint.TryParse(conditionArray[1], out var itemId)) return;
                    if (!uint.TryParse(conditionArray[2], out var quantity)) return;
                    if (!(operatorValue = conditionArray[2]).EqualsAny(operation.Keys)) return;
                    var itemCount = InventoryHelper.ItemCount(itemId);
                    if (operationResult = operation[operatorValue](itemCount, quantity))
                        invokeAction = true;
                    Svc.Log.Info($"Condition: {itemCount}{operatorValue}{quantity} = {operationResult}");
                    break;
                case "ObjectData":
                    if (conditionArray.Length > 3)
                    {
                        IGameObject? gameObject = null;
                        if ((gameObject = GetObjectByDataId(uint.TryParse(conditionArray[1], out uint dataId) ? dataId : 0)) != null)
                        {
                            var csObj = *gameObject.Struct();
                            switch (conditionArray[2])
                            {
                                case "EventState":
                                    if (csObj.EventState == (int.TryParse(conditionArray[3], out int es) ? es : -1))
                                        invokeAction = true;
                                    break;
                                case "IsTargetable":
                                    if (csObj.GetIsTargetable() == (bool.TryParse(conditionArray[3], out bool it) && it))
                                        invokeAction = true;
                                    break;
                            }
                        }
                    }
                    break;
            }
            if (invokeAction)
            {
                var actionActual = actionArray[0];
                string actionArguments = actionArray.Length > 1 ? actionArray[1] : "";
                Svc.Log.Debug($"ConditionAction: Invoking Action: {actionActual} with Arguments: {actionArguments}");
                InvokeAction(new PathAction() { Name = actionActual, Argument = actionArguments });
            }
        }

        public void BossMod(PathAction action) => _chat.ExecuteCommand($"/vbmai {action.Argument}");

        public void ModifyIndex(PathAction action)
        {
            if (!int.TryParse(action.Argument, out int _index)) return;
            AutoDuty.Plugin.Indexer = _index;
            AutoDuty.Plugin.Stage = Stage.Reading_Path;
        }

        private bool _autoManageRotationPluginState = false;
        public void Rotation(PathAction action)
        {
            if (action.Argument.Equals("off", StringComparison.InvariantCultureIgnoreCase))
            {
                if (AutoDuty.Plugin.Configuration.AutoManageRotationPluginState)
                {
                    _autoManageRotationPluginState = true;
                    AutoDuty.Plugin.Configuration.AutoManageRotationPluginState = false;
                }
                AutoDuty.Plugin.SetRotationPluginSettings(false, true);
            }
            else if (action.Argument.Equals("on", StringComparison.InvariantCultureIgnoreCase))
            {
                if (_autoManageRotationPluginState)
                    AutoDuty.Plugin.Configuration.AutoManageRotationPluginState = true;

                AutoDuty.Plugin.SetRotationPluginSettings(true, true);
            }
        }

        public void StopForCombat(PathAction action)
        {
            if (!Player.Available)
                return;

            var boolTrueFalse = action.Argument.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            AutoDuty.Plugin.Action = $"StopForCombat: {action.Argument}";
            AutoDuty.Plugin.StopForCombat = boolTrueFalse;
            _taskManager.Enqueue(() => _chat.ExecuteCommand($"/vbmai followtarget {(boolTrueFalse ? "on" : "off")}"), "StopForCombat");
        }

        public unsafe void ForceAttack(PathAction action)
        {
            var tot = action.Argument.IsNullOrEmpty() ? 10000 : int.TryParse(action.Argument, out int time) ? time : 0;
            if (action.Argument.IsNullOrEmpty())
                action.Argument = "10000";
            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "ForceAttack-GA16");
            _taskManager.Enqueue(() => Svc.Targets.Target != null, 500, "ForceAttack-GA1");
            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 1), "ForceAttack-GA1");
            _taskManager.Enqueue(() => Player.Object.InCombat(), tot, "ForceAttack-WaitForCombat");
        }

        public unsafe void Jump(PathAction action)
        {
            AutoDuty.Plugin.Action = $"Jumping";

            if (int.TryParse(action.Argument, out int wait) && wait > 0)
            {
                _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove on"), "Jump");
                _taskManager.Enqueue(() => EzThrottler.Throttle("AutoMove", Convert.ToInt32(wait)), "Jump");
                _taskManager.Enqueue(() => EzThrottler.Check("AutoMove"), Convert.ToInt32(wait), "Jump");
            }

            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2), "Jump");

            if (wait > 0)
            {
                _taskManager.Enqueue(() => EzThrottler.Throttle("AutoMove", Convert.ToInt32(100)), "Jump");
                _taskManager.Enqueue(() => EzThrottler.Check("AutoMove"), Convert.ToInt32(100), "AutoMove");
                _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove off"), "Jump");
            }
        }

        public void ChatCommand(PathAction action)
        {
            if (!Player.Available)
                return;
            AutoDuty.Plugin.Action = $"ChatCommand: {action.Argument}";
            _taskManager.Enqueue(() => _chat.ExecuteCommand(action.Argument), "ChatCommand");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void AutoMoveFor(PathAction action)
        {
            if (!Player.Available)
                return;
            AutoDuty.Plugin.Action = $"AutoMove For {action.Argument}";
            var movementMode = Svc.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) ? mode : 0;
            _taskManager.Enqueue(() => { if (movementMode == 1) Svc.GameConfig.UiControl.Set("MoveMode", 0); }, "AutoMove-MoveMode");
            _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove on"), "AutoMove-On");
            _taskManager.Enqueue(() => EzThrottler.Throttle("AutoMove", Convert.ToInt32(action.Argument)), "AutoMove-Throttle");
            _taskManager.Enqueue(() => EzThrottler.Check("AutoMove") || !ObjectHelper.IsReady, Convert.ToInt32(action.Argument), "AutoMove-CheckThrottleOrNotReady");
            _taskManager.Enqueue(() => { if (movementMode == 1) Svc.GameConfig.UiControl.Set("MoveMode", 1); }, "AutoMove-MoveMode2");
            _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "AutoMove-WaitIsReady");
            _taskManager.Enqueue(() => _chat.ExecuteCommand("/automove off"), "AutoMove-Off");
        }

        public unsafe void Wait(PathAction action)
        {
            AutoDuty.Plugin.Action = $"Wait: {action.Argument}";
            if (AutoDuty.Plugin.StopForCombat)
                _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "Wait");
            _taskManager.Enqueue(() => EzThrottler.Throttle("Wait", Convert.ToInt32(action.Argument)), "Wait");
            _taskManager.Enqueue(() => EzThrottler.Check("Wait"), Convert.ToInt32(action.Argument), "Wait");
            if (AutoDuty.Plugin.StopForCombat)
                _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "Wait");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public unsafe void WaitFor(PathAction action)
        {
            AutoDuty.Plugin.Action = $"WaitFor: {action.Argument}";
            var waitForWhats = action.Argument.Split(';');
            switch (waitForWhats[0])
            {
                case "Combat":
                    _taskManager.Enqueue(() => Player.Character->InCombat, "WaitFor-Combat");
                    break;
                case "OOC":
                    _taskManager.Enqueue(() => Player.Character->InCombat, 500, "WaitFor-Combat-500");
                    _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "WaitFor-OOC");
                    break;
                case "IsValid":
                    _taskManager.Enqueue(() => !ObjectHelper.IsValid, 500, "WaitFor-NotIsValid-500");
                    _taskManager.Enqueue(() => ObjectHelper.IsValid, int.MaxValue, "WaitFor-IsValid");
                    break;
                case "IsOccupied":
                    _taskManager.Enqueue(() => !ObjectHelper.IsOccupied, 500, "WaitFor-NotIsOccupied-500");
                    _taskManager.Enqueue(() => ObjectHelper.IsOccupied, int.MaxValue, "WaitFor-IsOccupied");
                    break;
                case "IsReady":
                    _taskManager.Enqueue(() => !ObjectHelper.IsReady, 500, "WaitFor-NotIsReady-500");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "WaitFor-IsReady");
                    break;
                case "ConditionFlag":
                    if (waitForWhats.Length < 3)
                        return;
                    ConditionFlag conditionFlag = Enum.TryParse(waitForWhats[1], out ConditionFlag condition) ? condition : ConditionFlag.None;
                    bool active = bool.TryParse(waitForWhats[2], out active) && active;

                    if (conditionFlag == ConditionFlag.None) return;

                    _taskManager.Enqueue(() => Svc.Condition[conditionFlag] == !active, 500, $"WaitFor-{conditionFlag}=={!active}-500");
                    _taskManager.Enqueue(() => Svc.Condition[conditionFlag] == active, int.MaxValue, $"WaitFor-{conditionFlag}=={!active}");
                    break;
                case "BNpcInRadius":
                    if (waitForWhats.Length == 1)
                        return;
                    _taskManager.Enqueue(() => !(ObjectHelper.GetObjectsByRadius(int.TryParse(waitForWhats[1], out var radius) ? radius : 0)?.Count > 0), $"WaitFor-BNpcInRadius{waitForWhats[1]}");
                    _taskManager.Enqueue(() => ObjectHelper.IsReady, int.MaxValue, "WaitFor");
                    break;
            }
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");

        }

        private bool CheckPause() => _plugin.Stage == Stage.Paused;

        public unsafe void ExitDuty(PathAction action)
        {
            _taskManager.Enqueue(() => { ExitDutyHelper.Invoke(); }, "ExitDuty-Invoke");
            _taskManager.Enqueue(() => ExitDutyHelper.State != ActionState.Running, "ExitDuty-WaitExitDutyRunning");
        }

        public unsafe bool IsAddonReady(nint addon) => addon > 0 && GenericHelpers.IsAddonReady((AtkUnitBase*)addon);

        public void SelectYesno(PathAction action)
        {
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"SelectYesno: {action.Argument}", "SelectYesno");
            _taskManager.Enqueue(() => AddonHelper.ClickSelectYesno(action.Argument.ToUpper().Equals("YES")), "SelectYesno");
            _taskManager.DelayNext("SelectYesno", 500);
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "SelectYesno");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public unsafe void MoveToObject(PathAction action)
        {
            if (!TryGetObjectIdRegex(action.Argument, out var objectDataId)) return;

            IGameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"MoveToObject: {objectDataId}";

            _taskManager.Enqueue(() => ObjectHelper.TryGetObjectByDataId(uint.Parse(objectDataId), out gameObject), "MoveToObject-GetGameObject");
            _taskManager.Enqueue(() => MovementHelper.Move(gameObject), int.MaxValue, "MoveToObject-Move");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void TreasureCoffer(PathAction _)
        {
            return;
        }

        private bool TargetCheck(IGameObject? gameObject)
        {
            if (gameObject == null || gameObject.IsTargetable || gameObject.IsValid() || Svc.Targets.Target == gameObject)
                return true;

            if (EzThrottler.Check("TargetCheck"))
            {
                EzThrottler.Throttle("TargetCheck", 25);
                Svc.Targets.Target = gameObject;
            }
            return false;
        }

        public unsafe void Target(PathAction action)
        {
            if (!TryGetObjectIdRegex(action.Argument, out var objectDataId)) return;

            IGameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"Target: {objectDataId}";

            _taskManager.Enqueue(() => ObjectHelper.TryGetObjectByDataId(uint.Parse(objectDataId), out gameObject), "Target-GetGameObject");
            _taskManager.Enqueue(() => TargetCheck(gameObject), "Target-Check");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void ClickTalk(PathAction action) => _taskManager.Enqueue(() => AddonHelper.ClickTalk(), "ClickTalk");

        private unsafe bool InteractableCheck(IGameObject? gameObject)
        {
            if (Conditions.IsMounted || Conditions.IsMounted2)
                return true;

            if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno) && !AddonHelper.ClickSelectYesno(true))
                return false;
            else if (AddonHelper.ClickSelectYesno(true))
                return true;

            if (GenericHelpers.TryGetAddonByName("Talk", out AtkUnitBase* addonTalk) && GenericHelpers.IsAddonReady(addonTalk) && !AddonHelper.ClickTalk())
                return false;
            else if (AddonHelper.ClickTalk())
                return true;

            if (gameObject == null || !gameObject.IsTargetable || !gameObject.IsValid() || !ObjectHelper.IsValid)
                return true;

            if (EzThrottler.Throttle("Interactable", 250))
            {
                if (ObjectHelper.GetBattleDistanceToPlayer(gameObject) > 2f)
                    MovementHelper.Move(gameObject, 0.25f, 2f, false);
                else
                {
                    if (VNavmesh_IPCSubscriber.Path_IsRunning())
                        VNavmesh_IPCSubscriber.Path_Stop();
                    ObjectHelper.InteractWithObject(gameObject);
                };
            }

            return false;
        }
        private unsafe void Interactable(IGameObject? gameObject)
        {
            _taskManager.Enqueue(() => InteractableCheck(gameObject), "Interactable-InteractableCheck");
            _taskManager.Enqueue(() => ObjectHelper.PlayerIsCasting, 500, "Interactable-WaitPlayerIsCasting");
            _taskManager.Enqueue(() => !ObjectHelper.PlayerIsCasting, "Interactable-WaitNotPlayerIsCasting");
            _taskManager.DelayNext("Interactable-DelayNext100", 100);
            _taskManager.Enqueue(() =>
            {
                var boolAddonSelectYesno = GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addonSelectYesno) && GenericHelpers.IsAddonReady(addonSelectYesno);

                var boolAddonTalk = GenericHelpers.TryGetAddonByName("Talk", out AtkUnitBase* addonTalk) && GenericHelpers.IsAddonReady(addonTalk);

                if (!boolAddonSelectYesno && !boolAddonTalk && (!(gameObject?.IsTargetable ?? false) ||
                Conditions.IsMounted ||
                Conditions.IsMounted2 ||
                Svc.Condition[ConditionFlag.BetweenAreas] ||
                Svc.Condition[ConditionFlag.BetweenAreas51] ||
                Svc.Condition[ConditionFlag.BeingMoved] ||
                Svc.Condition[ConditionFlag.Jumping61] ||
                Svc.Condition[ConditionFlag.CarryingItem] ||
                Svc.Condition[ConditionFlag.CarryingObject] ||
                Svc.Condition[ConditionFlag.Occupied] ||
                Svc.Condition[ConditionFlag.Occupied30] ||
                Svc.Condition[ConditionFlag.Occupied33] ||
                Svc.Condition[ConditionFlag.Occupied38] ||
                Svc.Condition[ConditionFlag.Occupied39] ||
                gameObject?.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj))
                {
                    AutoDuty.Plugin.Action = "";
                }
                else
                {
                    Interactable(gameObject);
                }
            }, "Interactable-LoopCheck");
        }

        public unsafe void Interactable(PathAction action)
        {
            if (!TryGetObjectIdRegex(action.Argument, out var objectDataId)) return;
            IGameObject? gameObject = null;
            AutoDuty.Plugin.Action = $"Interactable: {objectDataId}";

            _taskManager.Enqueue(() => ObjectHelper.TryGetObjectByDataId(uint.Parse(objectDataId), out gameObject) || Player.Character->InCombat, "Interactable-GetGameObjectUnlessInCombat");
            _taskManager.Enqueue(() =>
            {
                if (Player.Character->InCombat)
                {
                    _taskManager.Abort();
                    _taskManager.Enqueue(() => !Player.Character->InCombat, int.MaxValue, "Interactable-InCombatWait");
                    Interactable(action);
                }
                else if (gameObject == null)
                    _taskManager.Abort();
                }, "Interactable-InCombatCheck");
            _taskManager.Enqueue(() => gameObject?.IsTargetable ?? true, "Interactable-WaitGameObjectTargetable");
            _taskManager.Enqueue(() => Interactable(gameObject), "Interactable-InteractableLoop");
        }

        private bool TryGetObjectIdRegex(string input, out string output) => (RegexHelper.ObjectIdRegex().Match(input).Success ? output = RegexHelper.ObjectIdRegex().Match(input).Captures.First().Value : output = string.Empty) != string.Empty;

        private bool BossCheck()
        {
            if (((AutoDuty.Plugin.BossObject?.IsDead ?? true) && !Svc.Condition[ConditionFlag.InCombat]) || !Svc.Condition[ConditionFlag.InCombat])
                return true;

            if (EzThrottler.Throttle("PositionalChecker", 25) && ReflectionHelper.Avarice_Reflection.PositionalChanged(out Positional positional))
                AutoDuty.Plugin.Chat.ExecuteCommand($"/vbm cfg AIConfig DesiredPositional {positional}");

            return false;
        }

        private bool BossMoveCheck(Vector3 bossV3)
        {
            if (AutoDuty.Plugin.BossObject != null && AutoDuty.Plugin.BossObject.InCombat())
            {
                VNavmesh_IPCSubscriber.Path_Stop();
                return true;
            }
            return MovementHelper.Move(bossV3);
        }

        private void BossLoot(List<IGameObject>? gameObjects, int index)
        {
            if (gameObjects == null || gameObjects.Count < 1)
            {
                _taskManager.DelayNext("BossLoot-WaitASecToLootChest", 1000);
                return;
            }

            _taskManager.Enqueue(() => MovementHelper.Move(gameObjects[index], 0.25f, 1f), "BossLoot-MoveToChest");
            _taskManager.Enqueue(() =>
            {
                index++;
                if (gameObjects.Count > index)
                    BossLoot(gameObjects, index);
                else
                    _taskManager.DelayNext("BossLoot-WaitASecToLootChest", 1000);
            }, "BossLoot-LoopOrDelay");
        }

        public void Boss(PathAction action)
        {
            Svc.Log.Info($"Starting Action Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? "null"}");
            int index = 0;
            List<IGameObject>? treasureCofferObjects = null;
            AutoDuty.Plugin.SkipTreasureCoffer = false;
            _taskManager.Enqueue(() => BossMoveCheck(action.Position), "Boss-MoveCheck");
            if (AutoDuty.Plugin.BossObject == null)
                _taskManager.Enqueue(() => (AutoDuty.Plugin.BossObject = ObjectHelper.GetBossObject()) != null, "Boss-GetBossObject");
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = $"Boss: {AutoDuty.Plugin.BossObject?.Name.TextValue ?? ""}", "Boss-SetActionVar");
            _taskManager.Enqueue(() => Svc.Condition[ConditionFlag.InCombat], "Boss-WaitInCombat");
            _taskManager.Enqueue(() => BossCheck(), int.MaxValue, "Boss-BossCheck");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.StopForCombat = true; }, "Boss-SetStopForCombatTrue");
            _taskManager.Enqueue(() => { AutoDuty.Plugin.BossObject = null; }, "Boss-ClearBossObject");
            
            if (AutoDuty.Plugin.Configuration.LootTreasure)
            {
                _taskManager.DelayNext("Boss-TreasureDelay", 1000);
                _taskManager.Enqueue(() => treasureCofferObjects = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure)?.Where(x => ObjectHelper.GetDistanceToPlayer(x) <= 50).ToList(), "Boss-GetTreasureChests");
                _taskManager.Enqueue(() => BossLoot(treasureCofferObjects, index), "Boss-LootCheck");
            }
        }

        public void PausePandora(PathAction _)
        {
            return;
            //disable for now until we have a need other than interact objects
            //if (PandorasBox_IPCSubscriber.IsEnabled)
            //_taskManager.Enqueue(() => PandorasBox_IPCSubscriber.PauseFeature(featureName, int.Parse(intMs)));
        }

        public void Revival(PathAction _)
        {
            _taskManager.Enqueue(() => AutoDuty.Plugin.Action = "");
        }

        public void CameraFacing(PathAction action)
        {
            if (action != null)
            {
                string[] v = action.Argument.Split(", ");
                if (v.Length == 3)
                {
                    Vector3 facingPos = new(float.Parse(v[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[2], System.Globalization.CultureInfo.InvariantCulture));
                    AutoDuty.Plugin.OverrideCamera.Face(facingPos);
                }
            }
        }

        public enum OID : uint
        {
            Blue = 0x1E8554,
            Red = 0x1E8A8C,
            Green = 0x1E8A8D,
        }

        private string? GlobalStringStore;

        private unsafe void PraeUpdate(IFramework _)
        {
            if (!EzThrottler.Throttle("PraeUpdate", 50))
                return;

            var objects = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc);

            if (objects != null)
            {
                var protoArmOrDoor = objects.FirstOrDefault(x => x.IsTargetable && x.DataId is 14566 or 14616 && ObjectHelper.GetDistanceToPlayer(x) <= 25);
                if (protoArmOrDoor != null)
                    Svc.Targets.Target = protoArmOrDoor;
            }

            if (Svc.Targets.Target != null && Svc.Targets.Target.IsHostile())
            {
                var dir = Vector2.Normalize(new Vector2(Svc.Targets.Target.Position.X, Svc.Targets.Target.Position.Z) - new Vector2(Player.Position.X, Player.Position.Z));
                float rot = (float)Math.Atan2(dir.X, dir.Y);

                Player.Object.Struct()->SetRotation(rot);

                var targetPosition = Svc.Targets.Target.Position;
                ActionManager.Instance()->UseActionLocation(ActionType.Action, 1128, Player.Object.GameObjectId, &targetPosition);
            }
        }

        public unsafe void DutySpecificCode(PathAction action)
        {
            IGameObject? gameObject = null;
            switch (Svc.ClientState.TerritoryType)
            {
                //Prae
                case 1044:
                    switch (action.Argument)
                    {
                        case "1":
                            AutoDuty.Plugin.Chat.ExecuteCommand($"/vbm cfg AIConfig OverridePositional false");
                            Svc.Framework.Update += PraeUpdate;
                            Interactable(new PathAction() { Argument= "2012819" });
                            break;
                        case "2":
                            AutoDuty.Plugin.Chat.ExecuteCommand($"/vbm cfg AIConfig OverridePositional true");
                            Svc.Framework.Update -= PraeUpdate;
                            break;
                    }
                    break;
                //Sastasha
                //Blue -  2000213
                //Red -  2000214
                //Green - 2000215
                case 1036:
                    switch (action.Argument)
                    {
                        case "1":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectsByObjectKind(Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj)?.FirstOrDefault(a => a.IsTargetable && (OID)a.DataId is OID.Blue or OID.Red or OID.Green)) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() =>
                            {
                                if (gameObject != null)
                                {
                                    switch ((OID)gameObject.DataId)
                                    {
                                        case OID.Blue:
                                            GlobalStringStore = "2000213";
                                            break;
                                        case OID.Red:
                                            GlobalStringStore = "2000214";
                                            break;
                                        case OID.Green:
                                            GlobalStringStore = "2000215";
                                            break;
                                    }
                                }
                            }, "DutySpecificCode");
                            break;
                        case "2":
                            _taskManager.Enqueue(() => Interactable(new PathAction() { Argument = GlobalStringStore ?? "" }), "DutySpecificCode");
                            break;
                        case "3":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByDataId(2000216)) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.DelayNext("DutySpecificCode", 1000);
                            _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                            break;
                        default: break;
                    }
                    break;
                //Mount Rokkon
                case 1137:
                    switch (action.Argument)
                    {
                        case "5":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByDataId(16140)) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.DelayNext("DutySpecificCode", 1000);
                            _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                            if (ObjectHelper.IsValid)
                            {
                                _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                                _taskManager.Enqueue(() => AddonHelper.ClickSelectString(0));
                            }
                            break;
                        case "6":
                            _taskManager.Enqueue(() => (gameObject = ObjectHelper.GetObjectByDataId(16140)) != null, "DutySpecificCode");
                            _taskManager.Enqueue(() => MovementHelper.Move(gameObject, 0.25f, 2.5f), "DutySpecificCode");
                            _taskManager.DelayNext("DutySpecificCode", 1000);
                            if (ObjectHelper.IsValid)
                            {
                                _taskManager.Enqueue(() => ObjectHelper.InteractWithObject(gameObject), "DutySpecificCode");
                                _taskManager.Enqueue(() => AddonHelper.ClickSelectString(1));
                            }
                            break;
                        case "12":
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/rotation Settings AoEType Off"), "DutySpecificCode");
                            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(500)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(500), "DutySpecificCode");
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/mk ignore1"), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(100)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(100), "DutySpecificCode");

                            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(500)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(500), "DutySpecificCode");
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/mk ignore2"), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(100)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(100), "DutySpecificCode");

                            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(500)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(500), "DutySpecificCode");
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/mk attack1"), "DutySpecificCode");
                            break;
                        case "13":
                            _taskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 16), "DutySpecificCode");
                            _taskManager.Enqueue(() => EzThrottler.Throttle("DutySpecificCode", Convert.ToInt32(500)));
                            _taskManager.Enqueue(() => EzThrottler.Check("DutySpecificCode"), Convert.ToInt32(500), "DutySpecificCode");
                            _taskManager.Enqueue(() => _chat.ExecuteCommand("/mk attack1"), "DutySpecificCode");
                            break;

                        default: break;
                    }
                    break;
                default: break;
            }
        }
    }
}
