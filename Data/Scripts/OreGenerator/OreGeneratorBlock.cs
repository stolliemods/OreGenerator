using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using Sandbox.ModAPI;
using VRageMath;
using System;
using Sandbox.Game;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using System.Linq;
using System.Collections.Generic;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.Game.EntityComponents;
using VRage.Utils;
using Sandbox.Common.ObjectBuilders;
using System.Text;
using Sandbox.Game.Entities;

namespace Stollie.OreGenerator
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "SmallOreGenerator")]
    public class OreGeneratorBlock : MyGameLogicComponent
    {
        private IMyCubeBlock block = null;
        private int animationMovementLoop = 0;
        internal static readonly MyDefinitionId electricityId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        private static float powerRequired = 0.0f;
        private MyResourceSinkComponent powerSink;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                Log.Info("*** Block Init Started ***");
                block = (IMyCubeBlock)Entity;
                powerRequired = OreGeneratorSession.Instance.POWER_REQUIRED;
                Log.Info("Power Required Value Retrieved: " + OreGeneratorSession.Instance.POWER_REQUIRED);
                powerSink = block.Components.Get<MyResourceSinkComponent>();
                powerSink.ClearAllData();
                if ((block as IMyFunctionalBlock).Enabled)
                {
                    //powerSink.SetInputFromDistributor(electricityId, powerRequired, false);
                    powerSink.SetMaxRequiredInputByType(electricityId, powerRequired);
                    powerSink.SetRequiredInputFuncByType(electricityId, () => powerRequired);
                }
                if (!(block as IMyFunctionalBlock).Enabled)
                {
                    //powerSink.SetInputFromDistributor(electricityId, 0.0f, false);
                    powerSink.SetMaxRequiredInputByType(electricityId, 0.0f);
                    powerSink.SetRequiredInputFuncByType(electricityId, () => 0.0f);
                }
                //powerSink.Update();

                (block as IMyTerminalBlock).AppendingCustomInfo += OreGeneratorBlock_AppendingCustomInfo;
                (block as IMyFunctionalBlock).EnabledChanged += OreGeneratorBlock_EnabledChanged;
                NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_FRAME;

                Log.Info("Block Enabled: " + (block as IMyFunctionalBlock).Enabled + " - Config Power Required: " + powerRequired);
                Log.Info("Block Init Current Input: " + powerSink.CurrentInputByType(electricityId).ToString());
                Log.Info("Block Init Req Input: " + powerSink.RequiredInputByType(electricityId).ToString());
                Log.Info("Block Init Max Req Input: " + powerSink.MaxRequiredInputByType(electricityId).ToString());
                Log.Info("*** Block Init Complete ***");
                Log.Info("");
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.SendChatMessage("Block Init Error: " + e.Message);
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                // Animation
                if (block.IsWorking && powerSink.IsPowerAvailable(electricityId, powerRequired))
                    MoveOrb();

                SetEmissives();
                (block as IMyTerminalBlock).RefreshCustomInfo();
            }
            catch (Exception e)
            {
                Log.Error($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        private void OreGeneratorBlock_EnabledChanged(IMyTerminalBlock block)
        {
            Log.Info("*** Enable Change Started ***");
            powerSink.ClearAllData();
            powerRequired = OreGeneratorSession.Instance.POWER_REQUIRED;
            if ((block as IMyFunctionalBlock).Enabled)
            {
                //powerSink.SetInputFromDistributor(electricityId, powerRequired, false);
                powerSink.SetMaxRequiredInputByType(electricityId, powerRequired);
                powerSink.SetRequiredInputFuncByType(electricityId, () => powerRequired);
            }
            if (!(block as IMyFunctionalBlock).Enabled)
            {
                //powerSink.SetInputFromDistributor(electricityId, 0.0f, false);
                powerSink.SetMaxRequiredInputByType(electricityId, 0.0f);
                powerSink.SetRequiredInputFuncByType(electricityId, () => 0.0f);
            }
            //powerSink.Update();

            Log.Info("Block Enabled: " + (block as IMyFunctionalBlock).Enabled + " - Config Power Required: " + powerRequired);
            Log.Info("Current Input: " + powerSink.CurrentInputByType(electricityId).ToString());
            Log.Info("Req Input: " + powerSink.RequiredInputByType(electricityId).ToString());
            Log.Info("Max Req Input: " + powerSink.MaxRequiredInputByType(electricityId).ToString());
            Log.Info("*** Enable Change Finished ***");
            Log.Info("");
        }

        private void SetEmissives()
        {
            MyResourceDistributorComponent distributor = (MyResourceDistributorComponent)block.CubeGrid.ResourceDistributor;
            var gridPowerState = distributor.ResourceStateByType(MyResourceDistributorComponent.ElectricityId);
            var orbSubpart = block.GetSubpart("orb");

            if (block.IsWorking && powerSink.IsPowerAvailable(electricityId, powerRequired))
            {
                orbSubpart.SetEmissiveParts("Emissive_Clean", Color.Green, 1.0f);
            }
            if (!block.IsWorking)// || !powerSink.IsPowerAvailable(electricityId, powerRequired))
            {
                if (gridPowerState == VRage.MyResourceStateEnum.OverloadAdaptible || gridPowerState == VRage.MyResourceStateEnum.OverloadBlackout)
                {
                    orbSubpart.SetEmissiveParts("Emissive_Clean", Color.Yellow, 1.0f);
                }
                if (gridPowerState == VRage.MyResourceStateEnum.NoPower || gridPowerState == VRage.MyResourceStateEnum.Ok
                    || gridPowerState == VRage.MyResourceStateEnum.Disconnected)
                {
                    orbSubpart.SetEmissiveParts("Emissive_Clean", Color.Red, 1.0f);
                }
            }
        }
        public void MoveOrb()
        {
            try
            {
                MyEntitySubpart subpart;
                block.TryGetSubpart("orb", out subpart);

                if (subpart != null)
                {
                    var initialMatrix = subpart.PositionComp.LocalMatrix;
                    double rotationX = 0.0055f;
                    double rotationY = 0.0055f;
                    double rotationZ = 0.0055f;

                    if (animationMovementLoop == 200) animationMovementLoop = 0;

                    var rotationMatrix = MatrixD.CreateRotationX(rotationX) * MatrixD.CreateRotationY(rotationY) * MatrixD.CreateRotationZ(rotationZ);
                    var matrix = rotationMatrix * initialMatrix;
                    subpart.PositionComp.LocalMatrix = matrix;
                    animationMovementLoop++;
                }
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.ShowNotificationToAll("Update Error" + e, 2500, "Red");
            }
        }
        private void OreGeneratorBlock_AppendingCustomInfo(IMyTerminalBlock oreGeneratorBlock, System.Text.StringBuilder stringBuilder)
        {
            MyResourceDistributorComponent distributor = (MyResourceDistributorComponent)block.CubeGrid.ResourceDistributor;
            var gridPowerState = distributor.ResourceStateByType(MyResourceDistributorComponent.ElectricityId);

            stringBuilder.Clear();
            if (block.IsWorking && powerSink.IsPowerAvailable(electricityId, powerRequired))
            {
                stringBuilder.Append("\n[ Generator Enabled ]");
            }
            if (!block.IsWorking || !powerSink.IsPowerAvailable(electricityId, powerRequired))
            {
                if (gridPowerState == VRage.MyResourceStateEnum.OverloadAdaptible || gridPowerState == VRage.MyResourceStateEnum.OverloadBlackout)
                {
                    stringBuilder.Append("\n[ Grid Power Overloaded ]");
                }
                if (gridPowerState == VRage.MyResourceStateEnum.NoPower)
                {
                    stringBuilder.Append("\n[ Grid Power Off ]");
                }
                stringBuilder.Append("\n[ Generator Disabled ]");
            }
        }
        public override void Close()
        {
            (block as IMyTerminalBlock).AppendingCustomInfo -= OreGeneratorBlock_AppendingCustomInfo;
            (block as IMyFunctionalBlock).EnabledChanged -= OreGeneratorBlock_EnabledChanged;
        }
    }
}
