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
        public List<string> ORE_NAMES_AND_AMOUNTS;
        public int SECONDS_BETWEEN_CYCLES;

        internal string oreGeneratorSubtypeName = "SmallOreGenerator";
        internal static readonly MyDefinitionId electricityId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        private int oreSpawnTimer = 0;
        private long ticksBetweenSpawns = 0; 
        internal bool initalized = false;

        public static OreGeneratorSession Instance; // the only way to access session comp from other classes and the only accepted static field.

        public override void LoadData()
        {
            Instance = this;
            Log.Info("*** BeforeStart Initalization Started *** ");

            //if (!MyAPIGateway.Multiplayer.IsServer)
            //    return;

            oreGeneratorSettings = OreGeneratorSettings.LoadConfigFile();
            //var newOres = CheckConfigContent(oreGeneratorSettings.oreNamesAndAmounts);
            //if (newOres)
            //{
            //    Log.Info("Found new ores = " + newOres);
            //    MyAPIGateway.Utilities.DeleteFileInWorldStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings));

            //    // Generate a new config file to grab new ores
            //    oreGeneratorSettings = OreGeneratorSettings.LoadConfigFile();
            //    ORE_NAMES_AND_AMOUNTS = oreGeneratorSettings.oreNamesAndAmounts;
            //    POWER_REQUIRED = oreGeneratorSettings.powerRequired;
            //    SECONDS_BETWEEN_CYCLES = oreGeneratorSettings.secondsBetweenCycles;
            //}

            POWER_REQUIRED = oreGeneratorSettings.powerRequired;
            ORE_NAMES_AND_AMOUNTS = oreGeneratorSettings.oreNamesAndAmounts;
            SECONDS_BETWEEN_CYCLES = oreGeneratorSettings.secondsBetweenCycles;

            Log.Info("---- Power Required: " + POWER_REQUIRED + " ----");
            Log.Info("---- SecondsBetweenCycles: " + SECONDS_BETWEEN_CYCLES + " ----");
           
            Log.Info("*** BeforeStart Initalization Finished *** ");
            Log.Info("");
        }
      
        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            //if (!MyAPIGateway.Multiplayer.IsServer)
            //    return;

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
                Log.Info("*** Session Initilization Started ***");
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
                Log.Info("---- Ticks between spawns: " + ticksBetweenSpawns + " ----");
                Log.Info("---- Found " + entityList.Count + " Entities ----");
                Log.Info("---- Found " + gridsList.Count + " Grids ----");
                Log.Info("---- Found " + oreGenerators.Count + " Ore Generators ----");
                Log.Info("---- Found " + ORE_NAMES_AND_AMOUNTS.Count + " Ores in Config File ---- ");
                Log.Info("*** Session Initilization Complete ***");
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
            //Log.Info("---- Execute ----");
            oreSpawnTimer = 0;
            string[] currentOreNames;
            MyDefinitionManager.Static.GetOreTypeNames(out currentOreNames);
            foreach (var oreNameAndAmount in ORE_NAMES_AND_AMOUNTS)
            {
                var splitString = oreNameAndAmount.Split(new[] { ',' }, 2);
                int oreAmount = int.Parse(splitString[0]);
                var oreName = splitString[1];

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
                    
                    if (oreGenerator.SlimBlock.FatBlock.IsWorking && oreGenerator.IsFunctional && powerSink != null &&
                        powerSink.IsPowerAvailable(electricityId, POWER_REQUIRED) &&
                        cargoInventory != null && !cargoInventory.IsFull && item != null && currentOreNames.Contains(oreName) && item.Amount > 0)
                    {
                        cargoInventory.AddItems(item.Amount, item.Content);
                        //Log.Info(oreAmount.ToString() + " " + oreName.ToString() + " generated in " + oreGenerator.DisplayNameText);
                    }
                }
            }
            //Log.Info("---- End of Ore Generation Loop ----");
            //Log.Info("");
        }

        public static bool CheckConfigContent(List<string> oreNamesAndAmounts)
        {
            string[] oreNames;
            List<string> oreNamesSplit = new List<string>();

            MyDefinitionManager.Static.GetOreTypeNames(out oreNames);
            foreach (var oreNameAndAmount in oreNamesAndAmounts)
            {
                var splitString = oreNameAndAmount.Split(new[] { ',' }, 2);
                var oreName = splitString[1];
                oreNamesSplit.Add(oreName);
            }

            foreach (var ore in oreNames)
            {
                if (!oreNamesSplit.Contains(ore) && !ore.ToLower().Contains("scrap"))
                {
                    Log.Info(ore + " not found");
                    MyVisualScriptLogicProvider.SendChatMessage("New Ores Found - Re-generating Settings File!", "OreGeneratorMod", 0, "Red");
                    return true;
                }
            }
            return false;
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

                        float availableGridPower = distributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId);

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