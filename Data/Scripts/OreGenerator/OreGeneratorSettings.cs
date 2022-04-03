using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;

namespace Stollie.OreGenerator
{

    public class OreGeneratorSettings
    {
        public float powerRequired;
        public int secondsBetweenCycles;
        public static string[] oreNames;
        public static List<string> oreNamesAndAmountsList = new List<string>();
        public List<string> oreNamesAndAmounts = new List<string>();
        public OreGeneratorSettings()
        {
            powerRequired = 10.0f;
            secondsBetweenCycles = 10;
            oreNamesAndAmounts = oreNamesAndAmountsList;
        }

        public static void GenerateOreNames()
        {
            
            MyDefinitionManager.Static.GetOreTypeNames(out oreNames);
            foreach (var ore in oreNames)
            {
                if (!ore.ToLower().Contains("scrap"))
                {
                    Log.Info("Found: " + ore);
                    oreNamesAndAmountsList.Add(10.ToString() + "," + ore);
                }
            }
        }

        public static void Save()
        {
            TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(writer));
            writer.Flush();
            writer.Close();
        }

        public static OreGeneratorSettings LoadConfigFile()
        {
            // Check if it exists
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings)) == true)
            {
                try
                {
                    OreGeneratorSettings defaultConfig = new OreGeneratorSettings();
                    OreGeneratorSettings config = null;
                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings));
                    string configcontents = reader.ReadToEnd();
                    config = MyAPIGateway.Utilities.SerializeFromXML<OreGeneratorSettings>(configcontents);
                    Log.Info("---- Found Config File ----");
                    reader.Close();

                    Log.Info("Pwoer found = " + config.powerRequired);
                    
                    defaultConfig.powerRequired = config.powerRequired;
                    defaultConfig.secondsBetweenCycles = config.secondsBetweenCycles;
                    defaultConfig.oreNamesAndAmounts = config.oreNamesAndAmounts;

                    return defaultConfig;
                }
                catch (Exception exc)
                {
                    Log.Error(string.Format("Logging.WriteLine Error: {0}", exc.ToString()));
                }
            }

            // If not make a new one
            OreGeneratorSettings newDefaultconfig = new OreGeneratorSettings();
            Log.Info("Config File Not Found. Using Default Values");
            GenerateOreNames();
            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings)))
            {
                writer.Write(MyAPIGateway.Utilities.SerializeToXML<OreGeneratorSettings>(newDefaultconfig));
                writer.Close();
            }
            return newDefaultconfig;
        }

        //public static OreGeneratorSettings LoadConfigFile()
        //{
        //    // Check if it exists
        //    if (MyAPIGateway.Utilities.FileExistsInLocalStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings)) == true)
        //    {
        //        try
        //        {
        //            OreGeneratorSettings config = null;
        //            var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings));

        //            string configcontents = reader.ReadToEnd();
        //            config = MyAPIGateway.Utilities.SerializeFromXML<OreGeneratorSettings>(configcontents);
        //            Log.Info("Found Config File" + reader.ToString());
        //            reader.Close();
        //            return config;
        //        }
        //        catch (Exception exc)
        //        {
        //            Log.Error(string.Format("Logging.WriteLine Error: {0}", exc.ToString()));
        //        }
        //    }

        //    // If not make a new one
        //    OreGeneratorSettings defaultconfig = new OreGeneratorSettings();
        //    Log.Info("Config File Not Found. Using Default Values");
        //    GenerateOreNames();
        //    using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("OreGeneratorSettings.xml", typeof(OreGeneratorSettings)))
        //    {
        //        writer.Write(MyAPIGateway.Utilities.SerializeToXML<OreGeneratorSettings>(defaultconfig));
        //        writer.Close();
        //    }
        //    return defaultconfig;
        //}
    }
}
