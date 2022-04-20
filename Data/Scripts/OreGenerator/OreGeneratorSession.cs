using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using System;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.ModAPI;
using Sandbox.Game;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.Game.EntityComponents;
using VRage;

namespace Stollie.OreGenerator
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class OreGeneratorSession : MySessionComponentBase
    {
        public static List<string> oreNamesAndAmounts = new List<string>();

        public HashSet<IMyEntity> entityList = new HashSet<IMyEntity>();
        public HashSet<IMyCubeGrid> gridsList = new HashSet<IMyCubeGrid>();
        public List<IMyConveyorSorter> oreGenerators = new List<IMyConveyorSorter>();
        public OreGeneratorSettings oreGeneratorSettings;
        public float POWER_REQUIRED;
        public Dictionary<string, int> ORE_NAMES_AND_AMOUNTS = new Dictionary<string, int>();
        public int SECONDS_BETWEEN_CYCLES;

        internal string oreGeneratorSubtypeName = "SmallOreGenerator";
        internal static readonly MyDefinitionId electricityId = MyResourceDistributorComponent.ElectricityId;
        private int oreSpawnTimer = 0;
        private long ticksBetweenSpawns = 0; 
        internal bool initalized = false;

        public static OreGeneratorSession Instance; // the only way to access session comp from other classes and the only accepted static field.

        public override void LoadData()
        {
            Instance = this;

            Log.Info("*** Load Data STARTED *** ");

            // If you're not the server OR the game mode is not OFFLINE don't run this.
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            Log.Info("Loading Config File on Server");

            oreGeneratorSettings = new OreGeneratorSettings();
            oreGeneratorSettings.LoadConfigFile();

            MyAPIGateway.Utilities.GetVariable("PowerRequired", out POWER_REQUIRED);
            
            //POWER_REQUIRED = oreGeneratorSettings.powerRequired;
            ORE_NAMES_AND_AMOUNTS = oreGeneratorSettings.oreNamesAndAmountsDict;
            SECONDS_BETWEEN_CYCLES = oreGeneratorSettings.secondsBetweenCycles;

            Log.Info("Session Load Data Power Required: " + POWER_REQUIRED + "");
            Log.Info("Session Load Data SecondsBetweenCycles: " + SECONDS_BETWEEN_CYCLES + "");
            Log.Info("*** Load Data ENDED *** ");
            Log.Info("");
        }
      
        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            if (!initalized)
            {
                Initalize();
                initalized = true;
            }

            if (oreSpawnTimer == (int)ticksBetweenSpawns && initalized)
            {
                GenerateOre();
            }
            oreSpawnTimer++;
        }

        public void Initalize()
        {
            try
            {
                Log.Info("*** Session Initilization STARTED ***");
                MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;

                ticksBetweenSpawns = SECONDS_BETWEEN_CYCLES * 60;

                entityList.Clear();
                gridsList.Clear();
                oreGenerators.Clear();

                MyAPIGateway.Entities.GetEntities(entityList);
                foreach (var entity in entityList)
                {
                    if (entity as IMyCubeGrid != null)
                    {
                        gridsList.Add(entity as IMyCubeGrid);
                    }
                }

                foreach (var grid in gridsList)
                {
                    List<IMySlimBlock> gridBlocks = new List<IMySlimBlock>();
                    gridBlocks.Clear();
                    grid.GetBlocks(gridBlocks);
                    foreach (var block in gridBlocks)
                    {
                        if (block.BlockDefinition.Id.SubtypeName == oreGeneratorSubtypeName)
                        {
                            var blockAsCargoContainer = block.FatBlock as IMyConveyorSorter;
                            oreGenerators.Add(blockAsCargoContainer);
                        }
                    }
                    grid.OnBlockAdded += BlockAddedToGrid;
                    grid.OnBlockRemoved += BlockRemovedFromGrid;
                }

                MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
                MyAPIGateway.Entities.OnEntityRemove += EntityRemoved;
                Log.Info("Ticks between spawns: " + ticksBetweenSpawns + "");
                Log.Info("Found " + entityList.Count + " Entities");
                Log.Info("Found " + gridsList.Count + " Grids");
                Log.Info("Found " + oreGenerators.Count + " Ore Generators");
                Log.Info("Found " + ORE_NAMES_AND_AMOUNTS.Count + " Ores in Config File ");
                Log.Info("*** Session Initilization ENDED ***");
                Log.Info("");
            }
            catch (Exception e)
            {
                Log.Error($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }

        public void GenerateOre()
        {
            //Log.Info("");
            //Log.Info("Execute");
            oreSpawnTimer = 0;
            string[] currentOreNames;
            MyDefinitionManager.Static.GetOreTypeNames(out currentOreNames);
            foreach (var oreNameAndAmount in ORE_NAMES_AND_AMOUNTS)
            {
                int oreAmount = oreNameAndAmount.Value;
                var oreName = oreNameAndAmount.Key;

                var item = new MyObjectBuilder_InventoryItem()
                {
                    Amount = oreAmount,
                    Content = new MyObjectBuilder_Ore() { SubtypeName = oreName },
                };

                foreach (var oreGenerator in oreGenerators)
                {
                    if (oreGenerator.SlimBlock.FatBlock == null)
                        continue;
                    
                    var cargoInventory = oreGenerator.SlimBlock.FatBlock.GetInventory();
                    var powerSink = oreGenerator.Components.Get<MyResourceSinkComponent>();
                    var gridPowerDistributionGroup = (MyResourceDistributorComponent)oreGenerator.CubeGrid.ResourceDistributor;
                    var gridPowerState = gridPowerDistributionGroup.ResourceStateByType(electricityId);

                    //Log.Info("Grid Power State: " + gridPowerState);
                    //Log.Info("Grid Max Power Output: " + gridPowerDistributionGroup.MaxAvailableResourceByType(electricityId));
                    //Log.Info("Current Input: " + powerSink.CurrentInputByType(electricityId).ToString());
                    //Log.Info("OreGenerator Power is " + POWER_REQUIRED + " Available?: " + powerSink.IsPowerAvailable(electricityId, POWER_REQUIRED));
                    

                    if (gridPowerState != MyResourceStateEnum.Ok)
                        continue;

                    if (oreGenerator.SlimBlock.FatBlock.IsWorking && oreGenerator.IsFunctional && powerSink != null &&
                        powerSink.IsPowerAvailable(electricityId, POWER_REQUIRED) &&
                        cargoInventory != null && !cargoInventory.IsFull && item != null && currentOreNames.Contains(oreName) && item.Amount > 0)
                    {
                        cargoInventory.AddItems(item.Amount, item.Content);
                        //Log.Info(oreAmount.ToString() + " " + oreName.ToString() + " generated in " + oreGenerator.DisplayNameText);
                    }
                }
            }
            //Log.Info("End of Ore Generation Loop");
            //Log.Info("");
        }
        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            sendToOthers = false;
            try
            {
                if (messageText == "empty")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Empty Ore Generators");
                    foreach (var oreGenerator in oreGenerators)
                    {
                        var cargoInventory = oreGenerator.SlimBlock.FatBlock.GetInventory();
                        cargoInventory.Clear();
                    }
                }
                if (messageText == "cp")
                {
                    MyVisualScriptLogicProvider.SendChatMessage("Checking Power");
                    foreach (var oreGenerator in oreGenerators)
                    {
                        MyResourceDistributorComponent distributor = (MyResourceDistributorComponent)oreGenerator.CubeGrid.ResourceDistributor;

                        if (distributor == null)
                            continue;

                        float availableGridPower = distributor.MaxAvailableResourceByType(electricityId);

                        var powerSink = oreGenerator.Components.Get<MyResourceSinkComponent>();
                        if (powerSink != null)
                            MyVisualScriptLogicProvider.SendChatMessage(oreGenerator.SlimBlock.FatBlock.DisplayNameText + " has power: "
                                + powerSink.IsPowerAvailable(electricityId, POWER_REQUIRED).ToString() + " and is using: "
                                + powerSink.CurrentInputByType(electricityId) + " of " + availableGridPower.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                MyVisualScriptLogicProvider.SendChatMessage(e.ToString());
            }
            
            
        }

        public void EntityAdded(IMyEntity entity)
        {
            try
            {
                if (entity as IMyCubeGrid != null)
                {
                    var grid = entity as IMyCubeGrid;
                    List<IMySlimBlock> gridBlocks = new List<IMySlimBlock>();
                    gridBlocks.Clear();
                    grid.GetBlocks(gridBlocks);
                    foreach (var block in gridBlocks)
                    {
                        if (block.BlockDefinition.Id.SubtypeName == oreGeneratorSubtypeName)
                        {
                            var oreGeneratorBlock = block.FatBlock as IMyConveyorSorter;
                            oreGenerators.Add(oreGeneratorBlock);
                        }
                    }
                    grid.OnBlockAdded += BlockAddedToGrid;
                    grid.OnBlockRemoved += BlockRemovedFromGrid;
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("EntityAdditionError", e.ToString());
            }

        }

        public void EntityRemoved(IMyEntity entity)
        {
            try
            {
                if (entity as IMyCubeGrid != null && gridsList.Contains(entity as IMyCubeGrid))
                {
                    var grid = (entity as IMyCubeGrid);
                    gridsList.Remove(grid);
                    grid.OnBlockAdded -= BlockAddedToGrid;
                    grid.OnBlockRemoved -= BlockRemovedFromGrid;
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("EntityRemovalError", e.ToString());
            }

        }

        public void BlockAddedToGrid(IMySlimBlock slimBlock)
        {
            try
            {
                if (slimBlock.FatBlock != null && slimBlock != null)
                {
                    if (slimBlock.FatBlock.BlockDefinition.SubtypeName == oreGeneratorSubtypeName)
                    {
                        var oreGenerator = slimBlock.FatBlock as IMyConveyorSorter;
                        if (oreGenerator != null)
                        {
                            Log.Info("Added " + slimBlock.FatBlock.BlockDefinition.SubtypeName);
                            oreGenerators.Add(oreGenerator);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("BlockAdditionError", e.ToString());
            }
        }

        public void BlockRemovedFromGrid(IMySlimBlock slimBlock)
        {
            try
            {
                if (slimBlock.FatBlock != null && slimBlock != null)
                {
                    if (slimBlock.FatBlock.BlockDefinition.SubtypeName == oreGeneratorSubtypeName)
                    {
                        var oreGenerator = slimBlock.FatBlock as IMyConveyorSorter;
                        oreGenerators.Remove(oreGenerator);
                    }
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("BlockRemovalError", e.ToString());
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
            MyAPIGateway.Entities.OnEntityRemove -= EntityRemoved;

            foreach (var grid in gridsList)
            {
                grid.OnBlockAdded += BlockAddedToGrid;
                grid.OnBlockRemoved += BlockRemovedFromGrid;
            }

            entityList.Clear();
            gridsList.Clear();
            oreGenerators.Clear();

            MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
            Instance = null; // important for avoiding this object to remain allocated in memory
        }
    }
}