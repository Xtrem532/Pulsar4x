﻿using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Pulsar4X.ECSLib
{
    /// <summary>
    /// This class manages the games static data. This includes import/export of static data 
    /// for an existing game as well as the initial import of the static data for a new game.
    /// </summary>
    public class StaticDataManager
    {
        /// <summary>
        /// The static data store.
        /// </summary>
        //public static StaticDataStore StaticDataStore = new StaticDataStore();

        /// <summary>
        /// Default data directory, static data is stored in subfolders.
        /// @todo make this load from some sort of settings file.
        /// </summary>
        private const string DefaultDataDirectory = "./Data";

        /// <summary>
        /// The subdirectory of defaultDataDirectory that contains the offical game data.
        /// We will want to load this first so that mods overwrite our game files.
        /// @todo make this load from some sort of settings file.
        /// @todo make this more cross platform (currently Windows only).
        /// </summary>
        private const string OfficialDataDirectory = "\\Pulsar4x";

        // Serializer, specifically configured for static data.
        private static JsonSerializer _serializer = new JsonSerializer
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
            ContractResolver = new ForceUseISerializable(),
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
        };

        /// <summary>
        /// Loads all data from the default static data directory.
        /// Used when initializing a new game.
        /// </summary>
        [PublicAPI]
        public static StaticDataStore LoadFromDefaultDataDirectory()
        {
            // create an amprt static data store:
            StaticDataStore staticDataStore = new StaticDataStore();

            // get list of default sub-directories:
            string assemblyDir = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            if (assemblyDir == null)
            {
                throw new DirectoryNotFoundException("StaticDataStore could not find/access the executable's directory.");
            }
            Directory.SetCurrentDirectory(assemblyDir);
            var dataDirs = Directory.GetDirectories(DefaultDataDirectory);

            // safety check:
            if (dataDirs.GetLength(0) < 1)
            {
                ///< @todo Should we throw an exception here?
                return staticDataStore;  // return empty static data.
            }

            // let's make sure we load the official data first:
            int i = Array.FindIndex(dataDirs, x => x == (DefaultDataDirectory + OfficialDataDirectory));
            if (i < 0 || String.IsNullOrEmpty(dataDirs[i]))
                return staticDataStore; // bad?

            LoadFromDirectory(dataDirs[i], staticDataStore);
            dataDirs[i] = null; // prevent us from loading it again.

            // Loop through dirs, looking for files to load:
            foreach (var dir in dataDirs)
            {
                if (dir != null)
                    LoadFromDirectory(dir, staticDataStore);
            }

            return staticDataStore;
        }

        /// <summary>
        /// Loads all data from the specified directory.
        /// @todo change this function so that bad data does not cause a partial, irreversible import.
        /// </summary>
        [PublicAPI]
        public static void LoadFromDirectory(string directory, StaticDataStore staticDataStore)
        {
            try
            {
                // we start by looking for a version file, no version file, no load.
                VersionInfo dataVInfo;
                if (CheckDataDirectoryVersion(directory, VersionInfo.PulsarVersionInfo, out dataVInfo) == false)
                    return; ///< @todo log failure to import due to incompatible version.

                // now we can move on to looking for json files:
                var files = Directory.GetFiles(directory, "*.json");

                if (files.GetLength(0) < 1)
                    return;

                foreach (var file in files)
                {
                    JObject obj = Load(file);
                    StoreObject(obj, staticDataStore);
                }

                if (!staticDataStore.LoadedDataSets.Contains(dataVInfo))
                {
                    staticDataStore.LoadedDataSets.Add(dataVInfo);
                }
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(JsonSerializationException))
                    throw new StaticDataLoadException("Bad Json provided in directory: " + directory, e);
                if (e.GetType() == typeof(DirectoryNotFoundException))
                    throw new StaticDataLoadException("Directory not found: " + directory, e);
                
                throw e;  // rethrow exception if not known ;)
            }
        }

        /// <summary>
        /// Checks for a valid vinfo file in the specified directory, if the file is found it loads it and 
        /// checks that it is compatible with the version info provided.
        /// </summary>
        /// <param name="directory">Directory to check.</param>
        /// <param name="vinfo">Version to check against.</param>
        /// <returns>true if a compatible vinfo file was found, false otherwise.</returns>
        private static bool CheckDataDirectoryVersion(string directory, VersionInfo pulsarVInfo, out VersionInfo dataVInfo)
        {
            dataVInfo = new VersionInfo(); // need to assign some value to this to compile okay. value doesn't matter unless we return true tho.

            var vInfoFile = Directory.GetFiles(directory, "*.vinfo");

            if (vInfoFile.GetLength(0) < 1 || String.IsNullOrEmpty(vInfoFile[0]))
                return false;

            dataVInfo = LoadVinfo(vInfoFile[0]);

            ///< @todo make this check against saved data for that mod or against the game library.
            return dataVInfo.IsCompatibleWith(pulsarVInfo);
        }

        /// <summary>
        /// Stores the data in the provided JObject if it is valid.
        /// </summary>
        private static void StoreObject(JObject obj, StaticDataStore staticDataStore)
        {
            // we need to work out the type:
            Type type = StaticDataStore.GetType(obj["Type"].ToString());

            // grab the data:
            // use dynamic here to avoid having to know/use the exact the types.
            // we are alreading checking the types via StaticDataStore.*Type, so we 
            // can rely on there being an overload of StaticDataStore.Store
            // that supports that type.
            dynamic data = obj["Data"].ToObject(type, _serializer);

            staticDataStore.Store(data);
        }

        /// <summary>
        /// Loads the specified file into a JObject for further processing.
        /// </summary>
        static JObject Load(string file)
        {
            JObject obj = null;
            using (StreamReader sr = new StreamReader(file))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                obj = (JObject)_serializer.Deserialize(reader);
            }

            return obj;
        }

        /// <summary>
        /// Loads the specified object into a VersionInfo struct.
        /// </summary>
        static VersionInfo LoadVinfo(string file)
        {
            var obj = Load(file); // load into json data.
            VersionInfo info = (VersionInfo)obj["Data"].ToObject(typeof(VersionInfo), _serializer);

            return info;
        }


        public static void ImportStaticData(string file)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Exports the provided static data into the specified file.
        /// </summary>
        public static void ExportStaticData(object staticData, string file)
        {
            var data = new DataExportContainer();
            data.Data = staticData;
            data.Type = StaticDataStore.GetTypeString(staticData.GetType());

            if (String.IsNullOrEmpty(data.Type) == false)
            {
                using (StreamWriter sw = new StreamWriter(file))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    _serializer.Serialize(writer, data);
                }
            }
        }
    }

    #region Helper classes

    /// <summary>
    /// Exception which is thown when an error occurs during loading of atatic data.
    /// usually InnerException is set to the original exception which caused the error. 
    /// </summary>
    public class StaticDataLoadException : Exception
    {
        public StaticDataLoadException()
            : base("Unknown error occured during Static Data load.")
        { }

        public StaticDataLoadException(string message)
            : base("Error while loading static data: " + message)
        { }

        public StaticDataLoadException(string message, Exception inner)
            : base("Error while loading static data: " + message, inner)
        { }

        // This constructor is needed for serialization.
        public StaticDataLoadException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
    }

    /// <summary>
    /// This is a simple attribute that should be attached to Static Data structs. It assists reflection in finding 
    /// Static data and dealing with it. It has two properties, HasID and IDPropertyName, that are used to 
    /// signal that this piece of static data has a unique guid that represents it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class StaticDataAttribute : Attribute
    {
        public bool HasID { get; set; }

        public string IDPropertyName { get; set; }

        public StaticDataAttribute(bool hasID)
        {
            HasID = hasID;
        }
    }

    /// <summary>
    /// A small helper for exporting static data.
    /// </summary>
    public struct DataExportContainer
    {
        public string Type;
        public dynamic Data;
    }

    #endregion
}
