using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Stollie.OreGenerator
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Collector), false, "SmallOreGenerator")]
    public class OreGeneratorBlock : MyGameLogicComponent
    {
        private IMyCubeBlock block = null;
        private int animationMovementLoop = 0;
        Vector3 scaleDirection = new Vector3(-1, -1, -1);

        internal static readonly MyDefinitionId electricityId = MyResourceDistributorComponent.ElectricityId;
        private static float powerRequired = 0.0f;
        private bool initalPowerChangeApplied = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                //Log.Info("*** Block Init STARTED ***");
                block = (IMyCubeBlock)Entity;
                MyAPIGateway.Utilities.GetVariable("PowerRequired", out powerRequired);
                
                //Log.Info("Block Init Power Required Value Retrieved from Session Class: " + OreGeneratorSession.Instance.POWER_REQUIRED + "");
                NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                //Log.Info("*** Block Init ENDED ***");
                //Log.Info("");
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

                if (!initalPowerChangeApplied)
                {
                    ApplyInitialPowerChange();
                    initalPowerChangeApplied = true;
                }

                (block as IMyTerminalBlock).RefreshCustomInfo();

                if (MyAPIGateway.Utilities.IsDedicated)
                    return;

                var powerSink = Entity.Components.Get<MyResourceSinkComponent>();

                // Animation
                if (block.IsWorking && powerSink.IsPowerAvailable(electricityId, powerRequired))
                    MoveOrb();

                SetEmissives();
            }
            catch (Exception e)
            {
                Log.Error($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        public void ApplyInitialPowerChange()
        {
            //Log.Info("*** Initial Block Power Application STARTED ***");

            var powerSink = Entity.Components.Get<MyResourceSinkComponent>();
            if (powerSink != null)
            {
                powerSink.SetRequiredInputFuncByType(electricityId, ComputePowerRequired);
                powerSink.SetRequiredInputByType(electricityId, powerRequired);
                powerSink.SetMaxRequiredInputByType(electricityId, powerRequired);
                powerSink.Update();
            }

            //Log.Info("Block Enabled: " + (block as IMyFunctionalBlock).Enabled + " - Config Power Required: " + powerRequired);
            //Log.Info("Current Input: " + powerSink.CurrentInputByType(electricityId).ToString());
            //Log.Info("Req Input: " + powerSink.RequiredInputByType(electricityId).ToString());
            //Log.Info("Max Req Input: " + powerSink.MaxRequiredInputByType(electricityId).ToString());
            //Log.Info("*** Initial Block Power Application ENDED ***");
            //Log.Info("");

            (block as IMyTerminalBlock).AppendingCustomInfo += OreGeneratorBlock_AppendingCustomInfo;
            (block as IMyFunctionalBlock).EnabledChanged += OreGeneratorBlock_EnabledChanged;
            MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
        }

        private float ComputePowerRequired()
        {
            if (!(block as IMyFunctionalBlock).Enabled || !(block as IMyFunctionalBlock).IsFunctional)
                return 0f;

            return powerRequired;
        }

        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            sendToOthers = false;
            if (messageText == "c")
            {
                MyVisualScriptLogicProvider.SendChatMessage("Session PowerReq: " + powerRequired);
                MyVisualScriptLogicProvider.SendChatMessage("Block PowerReq Variable: " + powerRequired);
            }
        }

        private void OreGeneratorBlock_EnabledChanged(IMyTerminalBlock block)
        {
            //Log.Info("*** Enable Change Event STARTED ***");

            var powerSink = Entity.Components.Get<MyResourceSinkComponent>(); 
            powerSink.Update();

            //Log.Info("Block Enabled: " + (block as IMyFunctionalBlock).Enabled + " - Config Power Required: " + powerRequired);
            //Log.Info("Current Input: " + powerSink.CurrentInputByType(electricityId).ToString());
            //Log.Info("Req Input: " + powerSink.RequiredInputByType(electricityId).ToString());
            //Log.Info("Max Req Input: " + powerSink.MaxRequiredInputByType(electricityId).ToString());
            //Log.Info("*** Enable Change Event ENDED ***");
            //Log.Info("");
        }

        private void SetEmissives()
        {
            MyResourceDistributorComponent distributor = (MyResourceDistributorComponent)block.CubeGrid.ResourceDistributor;
            var gridPowerState = distributor.ResourceStateByType(electricityId);
            
            MyEntitySubpart oreInnerSubpart;
            block.TryGetSubpart("orb_inner", out oreInnerSubpart);

            if (block.IsWorking)
            {
                oreInnerSubpart.SetEmissiveParts("Emissive_Clean", Color.Green, 1.0f);
            }
            if (!block.IsWorking)
            {
                if (gridPowerState == VRage.MyResourceStateEnum.OverloadAdaptible || gridPowerState == VRage.MyResourceStateEnum.OverloadBlackout)
                    oreInnerSubpart.SetEmissiveParts("Emissive_Clean", Color.Yellow, 1.0f);

                if (gridPowerState == VRage.MyResourceStateEnum.NoPower || gridPowerState == VRage.MyResourceStateEnum.Disconnected)
                    oreInnerSubpart.SetEmissiveParts("Emissive_Clean", Color.Red, 1.0f);

                if (gridPowerState == VRage.MyResourceStateEnum.Ok)
                    oreInnerSubpart.SetEmissiveParts("Emissive_Clean", Color.Red, 1.0f);
            }
        }
        public void MoveOrb()
        {
            try
            {
                MyEntitySubpart oreInnerSubpart;
                block.TryGetSubpart("orb_inner", out oreInnerSubpart);

                MyEntitySubpart oreOuterSubpart;
                block.TryGetSubpart("orb_outer", out oreOuterSubpart);

                if (oreInnerSubpart != null)
                {
                    var initialMatrix = oreInnerSubpart.PositionComp.LocalMatrix;
                    
                    double rotationX = 0.0055f;
                    double rotationY = 0.0055f;
                    double rotationZ = 0.0055f;
                    MatrixD scaleMatrix = new MatrixD();

                    var rotationMatrix = MatrixD.CreateRotationX(rotationX) * MatrixD.CreateRotationY(rotationY) * MatrixD.CreateRotationZ(rotationZ);

                    if (animationMovementLoop < 100)
                        scaleMatrix = MatrixD.CreateScale(0.9995);

                    if (animationMovementLoop >= 100)
                        scaleMatrix = MatrixD.CreateScale(1.0005);

                    oreInnerSubpart.PositionComp.LocalMatrix = rotationMatrix * scaleMatrix * initialMatrix;
                    //oreInnerSubpart.PositionComp.LocalMatrix = rotationMatrix * initialMatrix;

                }

                if (oreOuterSubpart != null)
                {
                    var initialMatrix = oreOuterSubpart.PositionComp.LocalMatrix;
                    double rotationX = 0.0055f;
                    double rotationY = -0.0055f;
                    double rotationZ = 0.0055f;
                    MatrixD scaleMatrix = new MatrixD();

                    var rotationMatrix = MatrixD.CreateRotationX(rotationX) * MatrixD.CreateRotationY(rotationY) * MatrixD.CreateRotationZ(rotationZ);
                    
                    if (animationMovementLoop < 100)
                        scaleMatrix = MatrixD.CreateScale(0.9994);

                    if (animationMovementLoop >= 100)
                        scaleMatrix = MatrixD.CreateScale(1.0006);

                    //oreOuterSubpart.PositionComp.LocalMatrix = rotationMatrix * scaleMatrix * initialMatrix;
                    oreOuterSubpart.PositionComp.LocalMatrix = rotationMatrix * initialMatrix;
                }

                animationMovementLoop++;
                if (animationMovementLoop == 201) animationMovementLoop = 0;
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.ShowNotificationToAll("Update Error" + e, 2500, "Red");
            }
        }

        private void OreGeneratorBlock_AppendingCustomInfo(IMyTerminalBlock oreGenerator, System.Text.StringBuilder stringBuilder)
        {
            var gridPowerDistributionGroup = (MyResourceDistributorComponent)block.CubeGrid.ResourceDistributor;
            var gridPowerState = gridPowerDistributionGroup.ResourceStateByType(electricityId);
            var powerSink = Entity.Components.Get<MyResourceSinkComponent>();

            stringBuilder.Clear();
            if (block.IsWorking && powerSink.IsPowerAvailable(electricityId, powerRequired))
            {
                stringBuilder.Append("\n[ Ore Generation Enabled ]\n");
                stringBuilder.Append("Grid Power Used: " + Math.Round(gridPowerDistributionGroup.TotalRequiredInputByType(electricityId, oreGenerator.CubeGrid), 2) + " MW");
            }
            if (!block.IsWorking)
            {
                if (gridPowerState == VRage.MyResourceStateEnum.OverloadAdaptible || gridPowerState == VRage.MyResourceStateEnum.OverloadBlackout)
                {
                    stringBuilder.Append("\n[ Grid Power Overloaded ]\n");
                    stringBuilder.Append("Grid Max Power Output: " + Math.Round(gridPowerDistributionGroup.MaxAvailableResourceByType(electricityId), 2) + " MW\n");
                }
                
                if (gridPowerState == VRage.MyResourceStateEnum.NoPower)
                    stringBuilder.Append("\n[ Grid Power Off ]\n");
                
                if (gridPowerState == VRage.MyResourceStateEnum.Ok)
                    stringBuilder.Append("\n[ Ore Generator Off ]\n");

                stringBuilder.Append("\n[ Ore Generation Disabled ]");
            }
        }
        public override void Close()
        {
            (block as IMyTerminalBlock).AppendingCustomInfo -= OreGeneratorBlock_AppendingCustomInfo;
            (block as IMyFunctionalBlock).EnabledChanged -= OreGeneratorBlock_EnabledChanged;
            MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
        }
    }
}
