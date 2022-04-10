using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Stollie.OreGenerator
{

    public class OreGeneratorSettings
    {
        public float powerRequired;
        public int secondsBetweenCycles;
        public List<string> oreNamesAndAmountsList = new List<string>();

        [XmlIgnore]
        public Dictionary<string, int> oreNamesAndAmounts = new Dictionary<string, int>();
        
        public OreGeneratorSettings()
        {
            powerRequired = 5.0f;
            secondsBetweenCycles = 10;

            /* We need the ore list ready before loading data from config file.
             * This is to cover the case of missing a few ores in config file.*/
            GenerateOreNames();
        }

        private void GenerateOreNames()
        {
            string[] oreNames;
            MyDefinitionManager.Static.GetOreTypeNames(out oreNames);
            foreach (var ore in oreNames)
            {
                if (!ore.ToLower().Contains("scrap"))
                {
                    //Log.Info("Found: " + ore);
                    oreNamesAndAmounts[ore] = 10;
                }
            }
        }

        public void LoadConfigFile()
        {
            // Check if it exists
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings)) == true)
            {
                try
                {
                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings));
                    string configcontents = reader.ReadToEnd();
                    reader.Close();
                    Log.Info("Found Existing Config File");

                    OreGeneratorSettings config = MyAPIGateway.Utilities.SerializeFromXML<OreGeneratorSettings>(configcontents);
                    Log.Info("PowerRequired value found in config file: " + config.powerRequired);
                    MyAPIGateway.Utilities.SetVariable("PowerRequired", config.powerRequired);
                    //powerRequired = config.powerRequired;
                    secondsBetweenCycles = config.secondsBetweenCycles;
                    oreNamesAndAmountsList = config.oreNamesAndAmountsList;
                }
                catch (Exception exc)
                {
                    Log.Error(string.Format("Logging.WriteLine Error: {0}", exc.ToString()));
                }
                
                foreach (var listItem in oreNamesAndAmountsList)
                {
                    string[] pair = listItem.Split(',');
                    int oreAmount = 0;
                    if (!int.TryParse(pair[0], out oreAmount))
                        continue;

                    string oreName = pair[1];
                    
                    oreNamesAndAmounts[oreName] = oreAmount;
                }

                // if you don't want to save back config file right after loading it, add a return statement here;
                // saving back loaded config is useful in case config file is corrupted, or missing values;

                return;
            }

            // If not make a new one
            Log.Info("Config File Not Found. Using Default Values");
            Save();
        }

        public void Save()
        {
            oreNamesAndAmountsList.Clear();
            foreach (var pair in oreNamesAndAmounts)
            {
                oreNamesAndAmountsList.Add(pair.Value + "," + pair.Key);
            }

            try
            {
                string configcontents = MyAPIGateway.Utilities.SerializeToXML(this);

                TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings));
                writer.Write(configcontents);
                writer.Flush();
                writer.Close();
            }
            catch (Exception exc)
            {
                Log.Error(string.Format("Logging.WriteLine Error: {0}", exc.ToString()));
            }
        }
    }
}
