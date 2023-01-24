using AMP_Configurable.Patches;
using AMP_Configurable.PinConfig;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities;

namespace AMP_Configurable
{
  [BepInPlugin("amped.mod.auto_map_pins", "AMPED - Auto Map Pins Enhanced", "1.3.6")]
  [BepInProcess("valheim.exe")]
  public class Mod : BaseUnityPlugin
  {
    //***CONFIG ENTRIES***//
    //***GENERAL***//
    public static ConfigEntry<int> nexusID;
    public static ConfigEntry<bool> modEnabled;
    public static ConfigEntry<bool> diagnosticsEnabled;
    public static ConfigEntry<float> pinOverlapDistance;


    public static ConfigEntry<float> pinRange;
    public static ConfigEntry<bool> pinRangeExpRadiusMatching;
    public static ConfigEntry<float> minimapSizeMult;
    public static ConfigEntry<bool> hideAllLabels;
    public static ConfigEntry<string> hidePinLabels;
    public static ConfigEntry<string> hidePinTypes;
    public static ConfigEntry<string> savePinTypes;
    public static ConfigEntry<string> customPinSizes;
    public static ConfigEntry<bool> loggingEnabled;
    public static ConfigEntry<bool> objectLogging;
    public static ConfigEntry<string> objectLogFilter;
    public static ConfigEntry<bool> onlyLogUnique;
    public static ConfigEntry<bool> logKnownPinObjects;

    //** Pin Category control **//
    public static ConfigEntry<bool> destructablesEnabled;
    public static ConfigEntry<bool> pickablesEnabled;
    public static ConfigEntry<bool> locsEnabled;
    public static ConfigEntry<bool> spwnsEnabled;
    public static ConfigEntry<bool> creaturesEnabled;

    //***PUBLIC VARIABLES***//
    public static bool hasMoved = false;
    public static bool checkingPins = false;
    public static string currEnv = "";
    public static string[] filterObjectIds;
    public static List<string> uniqueObjectIds = new List<string>();

    //*** PUBLIC PIN TRACKING VARIABLES ***//
    /** <Dictionary> mtypePins 
      * Quick PinType lookups for (int)Minimap.PinData.PinType
      * AMPED PinTypes.type defined in .json files
      */
    public static Dictionary<int, PinType> mtypePins = new Dictionary<int, PinType>();

    /** <Dictionary> objectPins
      * Quick PinType lookups using objectIds
      * AMPED PinTypes.object_ids defined in .json files
      */
    public static Dictionary<string, PinType> objectPins = new Dictionary<string, PinType>();

    /** Dictionary<Vector3, PinType> pinItems 
      * Collection of discovered pin types keyed by the object's position
      * If pinned the <Minimap.PinData>pin will be added to the pinItems as PinType.minimapPin
      */
    public static Dictionary<Vector3, PinType> pinItems = new Dictionary<Vector3, PinType>();

    /** Dictionary<Vector3, PinType> pinItems 
      * Collection of discovered pin types keyed by the object's position
      * If pinned the <Minimap.PinData>pin will be added to the pinItems as PinType.minimapPin
      */
    public static List<Minimap.PinData> autoPins = new List<Minimap.PinData>();

    /** <Dictionary> dupPinLocs 
      * Collection of duplicate pins, this is when two objects are too close and only one pin should be created
      * The distance is configurable using <ConfigEntry>pinRange. Data managed mainly by the similarPinExists function
      */
    public static Dictionary<Vector3, Minimap.PinData> dupPinLocs = new Dictionary<Vector3, Minimap.PinData>();

    private void Awake()
    {
      Log.ModLogger = Logger;

      /** General Config **/
      nexusID = Config.Bind("_General_", "nexusID", 2199,
        new ConfigDescription("Nexus mod ID for updates", null, new ConfigurationManagerAttributes { Order = 1, DispName = "NexusID" }));
      modEnabled = Config.Bind("_General_", "modEnabled", true,
        new ConfigDescription("Enable this mod", null, new ConfigurationManagerAttributes { Order = 2, DispName = "Enabled" }));

      /** Pin Config **/
      pinOverlapDistance = Config.Bind<float>(
        "1. Pins", "pinOverlapDistance", 10,
        new ConfigDescription("Distance around pins to prevent overlapping of similar pins. \nRecommended values are 5-15",
        new AcceptableValueRange<float>(1, 50),
        new ConfigurationManagerAttributes { Order = 8, DispName = "Pin Overlap Distance" }));
      pinRangeExpRadiusMatching = Config.Bind<bool>("1. Pins", "pinRangeExpRadiusMatching", true,
        new ConfigDescription("Match Pin Range to the Player's Map Discovery Radius.\nShould be compatible with mods that change explore radius.", null,
        new ConfigurationManagerAttributes { Order = 7, DispName = "Match Pin Range to Explore Radius" }));
      pinRange = Config.Bind<float>(
        "1. Pins", "pinRange", 50,
        new ConfigDescription("Sets the range that pins will appear on the mini-map. Lower value means you need to be closer to set pin.\nDISABLED if Matching Player Explore Radius is enabled.\nRecommended 50-75",
        new AcceptableValueRange<float>(5, 150),
        new ConfigurationManagerAttributes { Order = 6, DispName = "Pin Range" }));
      hideAllLabels = Config.Bind("1. Pins", "hideAllLabels", false,
        new ConfigDescription("Hide all pin labels.\n*THIS WILL OVERRIDE THE INDIVIDUAL SETTINGS*", null,
        new ConfigurationManagerAttributes { Order = 5, DispName = "Hide ALL Labels" }));
      hidePinLabels = Config.Bind("1. Pins", "hidePinLabels", "",
        new ConfigDescription("Hide individual pin type labels.\nValue should be a comma seperated list of pin labels.", null,
        new ConfigurationManagerAttributes { Order = 4, DispName = "Hide Individual Labels" }));
      hidePinTypes = Config.Bind("1. Pins", "hidePinTypes", "",
        new ConfigDescription("Disable individual pin types.\nValue should be a comma seperated list of pin labels.", null,
        new ConfigurationManagerAttributes { Order = 3, DispName = "Disable Pins" }));
      savePinTypes = Config.Bind("1. Pins", "savePinTypes", "Crypt,Troll Cave,Sunken Crypt,Frost Cave,Infested Mine",
        new ConfigDescription("These Pin Types will persist on the map after the player as left the area.\nValue should be a comma seperated list of pin types.", null,
        new ConfigurationManagerAttributes { Order = 2, DispName = "Save Pins" }));
      minimapSizeMult = Config.Bind<float>(
        "1. Pins", "minimapSizeMult", (float)1.25,
        new ConfigDescription(
          "Pin sizes are multiplied by this number in the minimap.\nNote: Pins can also have a custom minimap size set in the pin packs .json config.\nThis multiplie will also be used on the custom minimap size of these pins.\nIf some pins seem to large please check any amp_*.json config files you have in your plugins folder and adjust the minimapSize acccordingly.",
          new AcceptableValueRange<float>((float)0.25, 5),
          new ConfigurationManagerAttributes { Order = 1, DispName = "Minimap Size Multiplier" }));

      destructablesEnabled = Config.Bind("2. Pins Enable/Disable", "destructablesEnabled", true,
        new ConfigDescription("Enable/Disable pins for\nOres, Trees, and other destructable resource nodes", null,
        new ConfigurationManagerAttributes { Order = 5, DispName = "Resources" }));
      pickablesEnabled = Config.Bind("2. Pins Enable/Disable", "pickablesEnabled", true,
        new ConfigDescription("Enable/Disable pins for\nBerries, Mushrooms, and other pickable items", null,
        new ConfigurationManagerAttributes { Order = 4, DispName = "Pickables" }));
      locsEnabled = Config.Bind("2. Pins Enable/Disable", "locsEnabled", true,
        new ConfigDescription("Enable/Disable pins for\nCrypts, Troll Caves, and other discoverable locations", null,
        new ConfigurationManagerAttributes { Order = 3, DispName = "Locations" }));
      spwnsEnabled = Config.Bind("2. Pins Enable/Disable", "spwnsEnabled", true,
        new ConfigDescription("Enable/Disable pins for\nGreydwarf nests, Skeleton Bone Piles, and other creature spawners", null,
        new ConfigurationManagerAttributes { Order = 2, DispName = "Spawners" }));
      creaturesEnabled = Config.Bind("2. Pins Enable/Disable", "creaturesEnabled", true,
        new ConfigDescription("Enable/Disable pins for\nSerpents, and other creatures when they spawn with in range of the player", null,
        new ConfigurationManagerAttributes { Order = 1, DispName = "Creatures" }));

      /** Logging Config **/
      loggingEnabled = Config.Bind("3. Logging", "loggingEnabled", false,
        new ConfigDescription("Toggle all logs from AMPED on/off", null,
        new ConfigurationManagerAttributes { Order = 6, DispName = "Enable Logging" }));
      objectLogging = Config.Bind("3. Logging", "objectLogging", false,
        new ConfigDescription("Writes object ids to log.\nThese can be used to create AMPED PinTypes in json config files", null,
        new ConfigurationManagerAttributes { Order = 5, DispName = "Object Id Logging" }));
      onlyLogUnique = Config.Bind<bool>("3. Logging", "onlyLogUnique", true,
       new ConfigDescription("Sets AMPED to only log out an objectId once, instead of every time an object spawns in.\nIt will logout a full list upon game exit.", null,
       new ConfigurationManagerAttributes { Order = 4, DispName = "Unique Objects Only" }));
      logKnownPinObjects = Config.Bind<bool>("3. Logging", "logKnownPinObjects", false,
        new ConfigDescription("Allow logging of objects that currently have a configured Pin Type.", null,
        new ConfigurationManagerAttributes { Order = 3, DispName = "Log Known Pin Objects" }));
      objectLogFilter = Config.Bind("3. Logging", "objectLogFilter", "",
        new ConfigDescription("Comma seperated list of object ids to filter out during logging process. Only applies when Object Logging is enabled", null,
        new ConfigurationManagerAttributes { Order = 2, DispName = "Object Log Filter" }));
      diagnosticsEnabled = Config.Bind("3. Logging", "diagnosticsEnabled", false,
        new ConfigDescription("Enables log output with function timing diagnostics. Used for developer optimization purposes", null,
        new ConfigurationManagerAttributes { Order = 1, DispName = "Enable Debug Diagnostics" }));

      filterObjectIds = objectLogFilter.Value.Split(',');

      /** Load Pin Type Config from JSON files **/
      string defaultPinConfPath = ResourceUtils.GetDefaultPinConfig();
      string[] configFiles = ResourceUtils.GetPinConfigFiles();
      objectPins = new Dictionary<string, PinType>();
      mtypePins = new Dictionary<int, PinType>();
      autoPins = new List<Minimap.PinData>();
      if (configFiles != null)
      {
        LoadPinsFromConfig(defaultPinConfPath);
        foreach (string confPath in configFiles)
        {
          if (Path.GetFileName(confPath) == "amp_pin_types.json") continue;
          LoadPinsFromConfig(confPath);
        }
      }

      if (!modEnabled.Value)
      {
        enabled = false;
        return;
      }

      new Harmony("amped.mod.auto_map_pins").PatchAll();

      Harmony.CreateAndPatchAll(typeof(Minimap_Patch), "amped.mod.auto_map_pins");
      Harmony.CreateAndPatchAll(typeof(Pin_Registration_Patches), "amped.mod.auto_map_pins");
      Harmony.CreateAndPatchAll(typeof(Player_Patches), "amped.mod.auto_map_pins");

      pinItems = new Dictionary<Vector3, PinType>();
      dupPinLocs = new Dictionary<Vector3, Minimap.PinData>();
      autoPins = new List<Minimap.PinData>();

      Config.SettingChanged += UpdatePinsFromSettings;

      Assets.Init();
    }

    private void OnDestroy()
    {
      if (objectLogging.Value && uniqueObjectIds.Count > 0)
      {
        Mod.Log.LogInfo($"AMP Found {uniqueObjectIds.Count} Object Ids");
        foreach (string objectId in uniqueObjectIds)
          Mod.Log.LogInfo($"[AMP object_id] {objectId}");
      }
    }

    public static void UpdatePinsFromSettings(object sender, SettingChangedEventArgs arg)
    {
      Log.LogDebug($"Setting change detected on {arg.ChangedSetting.Definition.Key}");

      if (pinRangeExpRadiusMatching.Value)
        if (Minimap.instance != null)
        {
          pinRange.Value = Minimap.instance.m_exploreRadius;
        } else pinRange.Value = 50;

      filterObjectIds = objectLogFilter.Value.Split(',');
      forcePinRefresh();
    }

    private static void LoadPinsFromConfig(string confFilePath)
    {
      Mod.Log.LogInfo($"Loading pin config file: {Path.GetFileName(confFilePath)}");
      PinConfig.PinConfig pinTypes = null;

      pinTypes = ResourceUtils.LoadPinConfig(confFilePath);
      foreach (PinType pinType in pinTypes.pins)
      {
        // Load mtype and objectId referance dictionary
        mtypePins[pinType.type] = pinType;
        foreach (string objectId in pinType.object_ids)
        {
          Mod.Log.LogDebug($"Adding {objectId.ToLower().Trim()} to pin type dictionary");
          objectPins[objectId.ToLower().Trim()] = pinType;
        }
      }
    }

    /** Log Class
     * A middleware class for BepInEx logging
     * Easy checking of logging config values before
     * writing logs to the system.
     */
    public static class Log
    {
      public static ManualLogSource ModLogger;

      public static void LogInfo(string msg)
      {
        if (!Mod.loggingEnabled.Value) return;
        ModLogger.LogInfo(msg);
      }

      public static void LogDebug(string msg)
      {
        if (!Mod.loggingEnabled.Value && !Mod.diagnosticsEnabled.Value) return;
        ModLogger.LogDebug(msg);
      }

      public static void LogWarning(string msg)
      {
        ModLogger.LogWarning(msg);
      }

      public static void LogError(string msg)
      {
        ModLogger.LogError(msg);
      }

      public static void LogObject(string type, string name, Vector3 pos)
      {
        if (!Mod.loggingEnabled.Value || !Mod.objectLogging.Value || filterObjectIds.Contains(name)) return;
        if (!Mod.logKnownPinObjects.Value && Mod.objectPins.ContainsKey(name)) return;
        if (Mod.onlyLogUnique.Value && !Mod.uniqueObjectIds.Contains(name))
        {
          LogInfo($"[AMP - {type}] Found Object Id {name}");
          Mod.uniqueObjectIds.Add(name);
        }
        else if (!Mod.onlyLogUnique.Value)
        {
          LogInfo($"[AMP - {type}] Found {name} at {pos.ToString()} distance from player[{Player_Patches.currPos.ToString()}] {distanceFromPlayer(pos).ToString()}");
        }
      }
    }

    public static void pinObject(string objectType, string objectId, Vector3 position)
    {
      string cleanedId = objectId.Replace("(Clone)", "").ToLower();
      Mod.Log.LogObject(objectType, cleanedId, position);
      if (Mod.objectPins.TryGetValue(cleanedId, out PinType type))
      {
        Mod.Log.LogDebug($"Mod.pinObject Adding {cleanedId} [{position}] to pinItems");
        type.isPinned = false;
        type.pinCat = objectType;
        Mod.pinItems[position] = type;
        Mod.mtypePins[type.type].pinCat = objectType;
        Mod.objectPins[cleanedId].pinCat = objectType;
      }
    }

    public static bool SimilarPinExists(
        Vector3 pos,
        Minimap.PinType type,
        List<Minimap.PinData> pins,
        string aName,
        Sprite aIcon,
        out Minimap.PinData match)
    {
      Mod.Log.LogDebug("Mod.SimilarPinExists");
      var watch = System.Diagnostics.Stopwatch.StartNew();

      foreach (Minimap.PinData pin in pins)
      {
        if (pos == pin.m_pos)
        {
          match = pin;
          watch.Stop();
          Mod.Log.LogDebug($"Mod.SimilarPinExists: Found Similar Pin. took {watch.ElapsedMilliseconds}ms");
          return true;
        }
        else if ((double)Utils.DistanceXZ(pos, pin.m_pos) < pinOverlapDistance.Value
            && type == pin.m_type
            && (aName == pin.m_name || aIcon == pin.m_icon))
        {
          match = pin;
          watch.Stop();
          Mod.Log.LogDebug($"Mod.SimilarPinExists: Found Similar Pin. took {watch.ElapsedMilliseconds}ms");
          return true;
        }
      }
      match = null;
      watch.Stop();
      Mod.Log.LogDebug($"Mod.SimilarPinExists took {watch.ElapsedMilliseconds}ms");
      return false;
    }

    public static void forcePinRefresh()
    {
      Mod.Log.LogDebug("Mod.forcePinRefresh settings changed, refreshing pins");

      dupPinLocs.Clear();

      // Reassess each pinItem's details 
      List<Minimap.PinData> tempPinList = new List<Minimap.PinData>(autoPins);
      foreach (Minimap.PinData pin in tempPinList)
        PinnedObject.updatePin(pin);

      // Now recheck pins to see if any need to be added / removed
      checkPins();
    }

    public static float distanceFromPlayer(Vector3 pos)
    {
      return Vector3.Distance(Player_Patches.currPos, pos);
    }

    public static void checkPins(bool firstLoad = false)
    {
      if (checkingPins) return;
      checkingPins = true;
      Mod.Log.LogDebug($"Mod.checkPins Looking for items to pin. Checking {pinItems.Count()} pinnable items");
      var watch = System.Diagnostics.Stopwatch.StartNew();

      // Compatibility check for pinRange. Other mods can change the exploreRadius 
      if (pinRangeExpRadiusMatching.Value)
      {
        if (Minimap.instance != null)
        {
          pinRange.Value = Minimap.instance.m_exploreRadius;
        } else pinRange.Value = 50;
      }

      // Check if there are any pins to add
      Dictionary<Vector3, PinType> tempPinItems = new Dictionary<Vector3, PinType>(pinItems);
      foreach (KeyValuePair<Vector3, PinType> pinItem in tempPinItems)
      {
        if (dupPinLocs.TryGetValue(pinItem.Key, out Minimap.PinData dupPin)) continue;

        float distance = distanceFromPlayer(pinItem.Key);

        // Add pins within range
        if (distance <= pinRange.Value)
        {
          Mod.Log.LogDebug($"Mod.checkPins found pinnable item in range. Pinning {pinItem.Value.label}");
          PinnedObject.pinOb(pinItem.Value, pinItem.Key);
        }

        // Clean pinItems of items that are far enough away to get re-added by objects "Awake" state
        if (!firstLoad && distance >= 300)
        {
          Mod.Log.LogDebug($"Mod.checkPins Distance is more than 300 for {pinItem.Value.label} [{pinItem.Key.ToString()}]. Distance: {distance.ToString()}. Removing from pinItems");
          pinItems.Remove(pinItem.Key);
        }
      }

      Mod.Log.LogDebug($"Mod.checkPins checking for out of range items on {autoPins.Count()} registered auto pins");
      // Temp pin list is used to avoid enumeration errors when updating autoPins inside of loop
      List<Minimap.PinData> tempPinList = new List<Minimap.PinData>(autoPins);
      foreach (Minimap.PinData pin in tempPinList)
      {
        if (!pin.m_save && distanceFromPlayer(pin.m_pos) > pinRange.Value)
        {
          Mod.Log.LogDebug($"Mod.checkPins {pin.m_name}[{pin.m_type}] is out of range, removing pin.");
          Minimap.instance.RemovePin(pin);
        }
      }

      watch.Stop();
      Mod.Log.LogDebug($"Mod.checkPins took {watch.ElapsedMilliseconds}ms");
      checkingPins = false;
    }

    public static Minimap.PinData GetNearestPin(Vector3 pos, float radius, List<Minimap.PinData> pins)
    {
      Minimap.PinData pinData = null;
      float num1 = 999999f;
      foreach (Minimap.PinData pin in pins)
      {
        float num2 = Utils.DistanceXZ(pos, pin.m_pos);
        if (num2 < radius && (num2 < num1 || pinData == null))
        {
          pinData = pin;
          num1 = num2;
        }
      }
      return pinData;
    }

    public Mod()
    {
      return;
    }
  }

  internal class PinnedObject : MonoBehaviour
  {
    public static Minimap.PinData pin;
    public static PinType pinType;
    public static bool aSave = false;
    public static bool showName = false;
    public static float pinSize = 20;
    public static Sprite aIcon;
    public static string aName = "";
    public static int pType;
    public static bool hidePin = false;

    public static Minimap.PinData updatePin(Minimap.PinData pin)
    {
      Mod.Log.LogDebug($"PinnedObject.updatePin start");
      var watch = System.Diagnostics.Stopwatch.StartNew();

      loadData(null, (int)pin.m_type);

      // If the pin is set to be hidden remove it from the map
      if (hidePin)
      {
        Minimap.instance.RemovePin(pin);
        return null;
      }

      string pLabel = !Mod.hideAllLabels.Value && showName ? aName : string.Empty;
      pin.m_name = pLabel;
      pin.m_worldSize = pinSize;
      pin.m_save = aSave;
      if (aIcon) pin.m_icon = aIcon;

      // if (pin.m_pos != pos) // Saved in comment for future *creature tracking* feature
      //   pin.m_pos = Vector3.MoveTowards(pin.m_pos, pos, 200f * Time.deltaTime);

      if (!pin.m_save)
      {
        // Double check unsaved pin distance from player, and remove if needed
        if (Mod.distanceFromPlayer(pin.m_pos) > Mod.pinRange.Value)
          Minimap.instance.RemovePin(pin);
      }

      watch.Stop();
      Mod.Log.LogDebug($"PinnedObject.updatePin took {watch.ElapsedMilliseconds}ms");

      return pin;
    }

    public static Minimap.PinData pinOb(PinType pinItem, Vector3 aPos)
    {
      if (pinItem == null || Mod.currEnv == "Crypt" || Mod.currEnv == "SunkenCrypt" || Mod.currEnv == "FrostCaves" || Mod.currEnv == "InfectedMine")
        return null;

      Mod.Log.LogDebug($"PinnedObject.pinOb start");
      var watch = System.Diagnostics.Stopwatch.StartNew();

      loadData(pinItem);

      // Don't pin if it's set to be hidden
      if (hidePin) return null;

      if (Mod.SimilarPinExists(aPos, (Minimap.PinType)pType, Mod.autoPins, aName, aIcon, out Minimap.PinData similarPin))
      {
        Mod.dupPinLocs[aPos] = similarPin;
        watch.Stop();
        Mod.Log.LogDebug($"PinnedObject.pinOb took {watch.ElapsedMilliseconds}ms. Similar pin exists.");
        return similarPin;
      }

      string pLabel = !Mod.hideAllLabels.Value && showName ? aName : string.Empty;
      pin = Minimap.instance.AddPin(aPos, (Minimap.PinType)pType, pLabel, aSave, false);
      if (aIcon) pin.m_icon = aIcon;
      pin.m_worldSize = pinSize;
      pin.m_save = aSave;

      //Mod.autoPins.Add(pin);

      watch.Stop();
      Mod.Log.LogDebug($"PinnedObject.pinOb took {watch.ElapsedMilliseconds}ms. Added Pin");
      return pin;
    }

    private void Update() { }

    private void OnDestroy()
    {
      if (pin == null || Minimap.instance == null)
        return;

      Mod.Log.LogDebug($"PinnedObject.OnDestroy on type {pin.m_type.ToString()}");
      var watch = System.Diagnostics.Stopwatch.StartNew();

      loadData(null, (int)pin.m_type);

      if (aSave || pin.m_save)
      {
        return;
      }
      if (Mod.distanceFromPlayer(pin.m_pos) < Mod.pinRange.Value)
        return;

      watch.Stop();
      var elapsedMs = watch.ElapsedMilliseconds;
      Mod.Log.LogDebug($"PinnedObject.OnDestroy took {elapsedMs}ms");
    }

    public static void loadData(PinType pin = null, int m_type = 0)
    {
      string pinLabel = pin == null ? m_type.ToString() : pin.label;
      Mod.Log.LogDebug($"PinnedObject.loadData on type/label {pinLabel}");
      var watch = System.Diagnostics.Stopwatch.StartNew();

      /** loadData called with minimap pin type data **/
      if (pin == null && m_type > 0)
      {
        if (!Mod.mtypePins.ContainsKey(m_type))
        {
          Mod.Log.LogInfo($"[AMP] Failed to load pin data from minimap pin type {m_type}");
          aName = "";
          return;
        }

        pin = Mod.mtypePins[m_type];
        Mod.Log.LogDebug($"[AMP] Loading pin {pin.label} from minimap type {m_type}");
      }

      pinType = pin;
      aName = pin.label;
      pType = pin.type;
      aSave = Mod.savePinTypes.Value.Split(',').Contains(pin.label);
      aIcon = pin.sprite;
      showName = !Mod.hidePinLabels.Value.Split(',').Contains(pin.label);
      switch (Minimap_Patch.mapMode)
      {
        case Minimap.MapMode.Small:
          pinSize = (pinType.minimapSize != 0 ? pinType.minimapSize : pinType.size) * Mod.minimapSizeMult.Value;
          break;
        case Minimap.MapMode.Large:
          pinSize = pin.size;
          break;
        default:
          pinSize = pin.size;
          break;
      }

      hidePin = Mod.hidePinTypes.Value != "" && Mod.hidePinTypes.Value.Split(',').Contains(pin.label);

      if(!Mod.destructablesEnabled.Value && pin.pinCat == "Destructable Resource") hidePin = true;
      if(!Mod.pickablesEnabled.Value && pin.pinCat == "Pickable") hidePin = true;
      if(!Mod.locsEnabled.Value && pin.pinCat == "Location") hidePin = true;
      if(!Mod.spwnsEnabled.Value && pin.pinCat == "Spawner") hidePin = true;
      if(!Mod.creaturesEnabled.Value && pin.pinCat == "Creature") hidePin = true;

      watch.Stop();
      var elapsedMs = watch.ElapsedMilliseconds;
      Mod.Log.LogDebug($"PinnedObject.loadData took {elapsedMs}ms");
    }

    public PinnedObject()
    {
      return;
    }
  }

  public class Assets
  {
    public static void Init()
    {
      foreach (KeyValuePair<int, PinType> mtype in Mod.mtypePins)
        mtype.Value.sprite = LoadSprite(mtype.Value.icon);
    }

    internal static Texture2D LoadTexture(byte[] file)
    {
      if (((IEnumerable<byte>)file).Count<byte>() > 0)
      {
        Texture2D texture2D = new Texture2D(2, 2);
        if (ImageConversion.LoadImage(texture2D, file))
        {
          return texture2D;
        }
      }
      return (Texture2D)null;
    }

    public static Sprite LoadSprite(string iconPath, float PixelsPerUnit = 50f)
    {
      Texture2D SpriteTexture = LoadTexture(ResourceUtils.GetResource(iconPath));
      return SpriteTexture ? Sprite.Create(SpriteTexture, new Rect(0.0f, 0.0f, SpriteTexture.width, SpriteTexture.height), new Vector2(0.0f, 0.0f), PixelsPerUnit) : (Sprite)null;
    }
  }
}
