using AMP_Configurable.PinConfig;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

namespace AMP_Configurable.Patches
{
  internal class Minimap_Patch : MonoBehaviour
  {
    public static int count = 0;
    public static bool checkedSavedPins = false;
    public static Minimap.MapMode mapMode = Minimap.MapMode.Small;

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(Minimap), "ScreenToWorldPoint", new System.Type[] { typeof(Vector3) })]
    public static Vector3 ScreenToWorldPoint(object instance, Vector3 screenPos) => throw new NotImplementedException();

    [HarmonyPatch(typeof(Minimap), "Awake")]
    [HarmonyPostfix]
    private static void Minimap_Awake()
    {
      checkedSavedPins = false;
      Mod.dupPinLocs.Clear();
    }

    [HarmonyPatch(typeof(Minimap), "Start")]
    [HarmonyPostfix]
    private static void Minimap_Start(
        Minimap __instance,
        ref bool[] ___m_visibleIconTypes)
    {
      ___m_visibleIconTypes = new bool[150];
      for (int index = 0; index < ___m_visibleIconTypes.Length; index++)
      {
        ___m_visibleIconTypes[index] = true;
      }
    }

    // [HarmonyPatch(typeof(Minimap), "AddPin")]
    // [HarmonyPrefix]
    // private static bool Minimap_AddPin(
    //   ref Minimap __instance,
    //   List<Minimap.PinData> ___m_pins,
    //   Vector3 pos,
    //   Minimap.PinType type,
    //   string name,
    //   bool save,
    //   bool isChecked)
    // {
    //   // bool shouldAddPin = ((type != Minimap.PinType.Death ? 0 : (Mod.SimilarPinExists(pos, type, ___m_pins, name, PinnedObject.aIcon, out Minimap.PinData _) ? 1 : 0)) & (save ? 1 : 0)) == 0;
    //   // if(shouldAddPin) Mod.Log.LogInfo($"Patches.Minimap.AddPin Attempting to pin {name} [{type}]");
    //   // return shouldAddPin;
    //   Mod.Log.LogInfo($"Patches.Minimap.AddPin Attempting to pin {name} [{type}]");
    //   return true;
    // }

    [HarmonyPatch(typeof(Minimap), "AddPin")]
    [HarmonyPostfix]
    private static Minimap.PinData Minimap_AddPin_PostFix(Minimap.PinData pin)
    {
      // Check if the pin is a part of AMPED pinItems and then set data accordingly
      if (Mod.pinItems.TryGetValue(pin.m_pos, out PinType pinTypeData))
      {
        Mod.pinItems[pin.m_pos].isPinned = true;
        Mod.pinItems[pin.m_pos].minimapPin = pin;
        Mod.autoPins.Add(pin);
      }
      return pin;
    }

    [HarmonyPatch(typeof(Minimap), "RemovePin", new Type[] { typeof(Minimap.PinData) })]
    [HarmonyPrefix]
    private static void Minimap_RemovePin(ref Minimap __instance, Minimap.PinData pin)
    {
      if (Mod.pinItems.ContainsKey(pin.m_pos))
      {
        Mod.pinItems[pin.m_pos].minimapPin = null;
        Mod.pinItems[pin.m_pos].isPinned = false;
      }

      if (Mod.dupPinLocs.ContainsKey(pin.m_pos)) Mod.dupPinLocs.Remove(pin.m_pos);
      if (Mod.autoPins.Contains(pin)) Mod.autoPins.Remove(pin);
    }

    [HarmonyPatch(typeof(Minimap), "UpdateProfilePins")]
    [HarmonyPrefix]
    private static void Minimap_UpdateProfilePins(
      ref List<Minimap.PinData> ___m_pins)
    {
      if (!checkedSavedPins)
      {
        Mod.Log.LogDebug("Minimap.UpdateProfilePins checking saved pins");
        DiagnosticUtils timer = new DiagnosticUtils();
        timer.startTimer();
        foreach (Minimap.PinData pin in ___m_pins)
        {
          if ((int)pin.m_type >= 100)
          {
            PinnedObject.loadData(null, (int)pin.m_type);
            if (PinnedObject.hidePin)
            {
              Minimap.instance.RemovePin(pin);
              continue;
            }

            if (pin.m_type == (Minimap.PinType)PinnedObject.pType)
            {
              pin.m_icon = PinnedObject.aIcon;
              pin.m_worldSize = PinnedObject.pinSize;
              if (!PinnedObject.showName) pin.m_name = string.Empty;
            }

            if (Mod.mtypePins.TryGetValue((int)pin.m_type, out PinType pinTypeData))
            {
              Mod.pinItems[pin.m_pos] = pinTypeData;
              Mod.pinItems[pin.m_pos].minimapPin = pin;
            }
            Mod.autoPins.Add(pin);
          }
        }
        checkedSavedPins = true;

        Mod.checkPins(true);
        Mod.Log.LogDebug($"Minimap.UpdateProfilePins checking saved pins took {timer.stopTimer()}ms");
      }
    }

    /** Keeping code in comment since UpdatePins will be used in creature tracking feature **/
    // [HarmonyPatch(typeof(Minimap), "UpdatePins")]
    // [HarmonyPrefix]
    // private static void Minimap_UpdatePins(
    //   Minimap __instance,
    //   ref List<Minimap.PinData> ___m_pins)
    // {
    //   if (Mod.filteredPins.Count() == 0)
    //     return;

    //   Mod.Log.LogDebug("Minimap: Update Pins - Filtering pins");
    //   foreach (Minimap.PinData p in ___m_pins)
    //   {
    //     if (Mod.filteredPins.Contains(p.m_type.ToString()))
    //     {
    //       Mod.FilterPins();
    //     }
    //   }
    // }

    [HarmonyPatch(typeof(Minimap), "SetMapMode")]
    [HarmonyPostfix]
    private static void Minimap_ChangeMapMode(
      Minimap __instance,
      ref Minimap.MapMode ___m_mode)
    {
      Mod.Log.LogDebug($"Minimap.SetMapMode - Mode changed to {___m_mode}");
      DiagnosticUtils timer = new DiagnosticUtils();
      timer.startTimer();

      mapMode = ___m_mode;
      foreach (Minimap.PinData pin in Mod.autoPins)
      {
        if (!Mod.mtypePins.TryGetValue((int)pin.m_type, out PinType typeConf)) continue;
        float new_size = typeConf.size;
        if (___m_mode == Minimap.MapMode.Small)
          new_size = (typeConf.minimapSize != 0 ? typeConf.minimapSize : typeConf.size) * Mod.minimapSizeMult.Value;

        pin.m_worldSize = new_size;
      }
      Mod.Log.LogDebug($"Minimap.SetMapMode timing {timer.stopTimer()}ms");
    }

    [HarmonyPatch(typeof(Minimap), "OnMapRightClick")]
    [HarmonyPrefix]
    private static bool Minimap_OMRC(
        Minimap __instance,
        ref List<Minimap.PinData> ___m_pins)
    {
      Mod.Log.LogDebug("Minimap.OnMapRightClick");
      ZLog.Log("[AMP] Right click");

      Vector3 worldPoint = ScreenToWorldPoint(__instance, Input.mousePosition);
      Minimap.PinData closestPin = Mod.GetNearestPin(worldPoint, 10, ___m_pins);

      if (closestPin == null || closestPin.m_icon.name == "mapicon_start" || closestPin.m_type == Minimap.PinType.Death)
        return true;

      __instance.RemovePin(closestPin);
      return false;
    }

    [HarmonyPatch(typeof(Minimap), "OnMapLeftClick")]
    [HarmonyPrefix]
    private static bool Minimap_OMLC(
        Minimap __instance,
        ref List<Minimap.PinData> ___m_pins)
    {
      Mod.Log.LogDebug("Minimap.OnMapLeftClick");
      ZLog.Log("[AMP] Left click");
      Vector3 worldPoint = ScreenToWorldPoint(__instance, Input.mousePosition);

      Minimap.PinData closestPin = Mod.GetNearestPin(worldPoint, 10, ___m_pins);

      if (closestPin == null) return true;

      closestPin.m_checked = !closestPin.m_checked;
      return false;
    }
  }

  internal class Pin_Registration_Patches : MonoBehaviour
  {
    [HarmonyPatch(typeof(Destructible), "Start")]
    [HarmonyPostfix]
    private static void DestructibleSpawnPatch(ref Destructible __instance)
    {
      if (!Mod.destructablesEnabled.Value) return;
      HoverText comp = __instance.GetComponent<HoverText>();
      if (!comp) return;

      string objectId = comp.m_text;
      Vector3 position = comp.transform.position;
      Mod.pinObject("Destructable Resource", objectId, position);
    }

    [HarmonyPatch(typeof(Pickable), "Awake")]
    [HarmonyPostfix]
    private static void PickableSpawnPatch(ref Pickable __instance)
    {
      if (!Mod.pickablesEnabled.Value) return;
      Pickable comp = __instance.GetComponent<Pickable>();
      if (!comp) return;

      string objectId = comp.name;
      Vector3 position = comp.transform.position;
      Mod.pinObject("Pickable", objectId, position);
    }

    [HarmonyPatch(typeof(Location), "Awake")]
    [HarmonyPostfix]
    private static void LocationSpawnPatch(ref Location __instance)
    {
      if (!Mod.locsEnabled.Value) return;
      Location comp = __instance.GetComponent<Location>();
      if (!comp) return;

      string objectId = comp.name;
      Vector3 position = comp.transform.position;
      Mod.pinObject("Location", objectId, position);
    }

    [HarmonyPatch(typeof(SpawnArea), "Awake")]
    [HarmonyPostfix]
    private static void SpawnAreaSpawnPatch(ref Location __instance)
    {
      if (!Mod.spwnsEnabled.Value) return;
      HoverText comp = __instance.GetComponent<HoverText>();
      if (!comp) return;

      string objectId = comp.m_text;
      Vector3 position = comp.transform.position;
      Mod.pinObject("Spawner", objectId, position);
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    [HarmonyPostfix]
    private static void CharacterSpawnPatch(ref CreatureSpawner __instance)
    {
      if (!Mod.creaturesEnabled.Value) return;
      Character comp = __instance.GetComponent<Character>();
      if (!comp) return;

      string objectId = comp.name;
      Vector3 position = comp.transform.position;
      Mod.pinObject("Creature", objectId, position);
    }

    [HarmonyPatch(typeof(MineRock), "Start")]
    [HarmonyPostfix]
    private static void MineRockSpawnPatch(ref MineRock __instance)
    {
      if (!Mod.destructablesEnabled.Value) return;
      MineRock comp = __instance.GetComponent<MineRock>();
      if (!comp) return;

      string objectId = comp.name;
      Vector3 position = comp.transform.position;
      Mod.pinObject("Destructable Resource", objectId, position);
    }

    [HarmonyPatch(typeof(Leviathan), "Awake")]
    [HarmonyPostfix]
    private static void LeviathanSpawnPatch(ref Leviathan __instance)
    {
      if (!Mod.creaturesEnabled.Value) return;
      Leviathan comp = __instance.GetComponent<Leviathan>();
      if (!comp) return;

      string objectId = comp.name;
      Vector3 position = comp.transform.position;
      Mod.pinObject("Destructable Resource", objectId, position);
    }

    [HarmonyPatch(typeof(TreeBase), "Awake")]
    [HarmonyPostfix]
    private static void TreeBaseSpawnPatch(ref TreeBase __instance)
    {
      if (!Mod.destructablesEnabled.Value) return;
      TreeBase comp = __instance.GetComponent<TreeBase>();
      if (!comp) return;

      string objectId = comp.name;
      Vector3 position = comp.transform.position;
      Mod.pinObject("Destructable Resource", objectId, position);
    }

    // [HarmonyPatch(typeof(Vagon), "Awake")]
    // [HarmonyPostfix]
    // private static void VagonSpawnPatch(ref Vagon __instance)
    // {
    //   Vagon cartComp = __instance.GetComponent<Vagon>();

    //   if (!cartComp) return;

    //   string cartText = cartComp.m_name;
    //   cartText = cartText.Replace("(Clone)", "").ToLower();

    //   Mod.Log.LogDebug($"Found wagon: {cartText} at {cartComp.transform.position.ToString()}");
    // }

    // [HarmonyPatch(typeof(Ship), "Awake")]
    // [HarmonyPostfix]
    // private static void ShipSpawnPatch(ref Ship __instance)
    // {
    //   Ship shipComp = __instance.GetComponent<Ship>();

    //   if (!shipComp) return;

    //   string shipText = shipComp.name;
    //   shipText = shipText.Replace("(Clone)", "").ToLower();

    //   Mod.Log.LogDebug($"Found Ship: {shipText} at {shipComp.transform.position.ToString()}");
    // }

    // [HarmonyPatch(typeof(TeleportWorld), "Awake")]
    // [HarmonyPostfix]
    // private static void TeleportWorldSpawnPatch(ref TeleportWorld __instance)
    // {
    //   TeleportWorld portalComp = __instance.GetComponent<TeleportWorld>();

    //   if (!portalComp) return;

    //   string portalText = portalComp.name;
    //   portalText = portalText.Replace("(Clone)", "").ToLower();

    //   HoverText hoverComp = __instance.GetComponent<HoverText>();
    //   string portalDestination = hoverComp.m_text;

    //   Mod.Log.LogDebug($"Found Portal: {portalText} ({portalDestination}) at {portalComp.transform.position.ToString()}");
    // }
  }

  internal class Player_Patches
  {
    public static Vector3 currPos;
    public static Vector3 prevPos;
    public const int interval = 120;
    public static Player player = null;
    public static bool hasWishbone = false;

    public static void checkForWishbone()
    {
      if(player == null) return;
      List<ItemDrop.ItemData> inventoryItems;
      bool origHasWishbone = hasWishbone;

      switch (Mod.wishboneMode.Value)
      {
        case Mod.WishboneModes.equipped:
          inventoryItems = player.GetInventory().GetEquipedtems();
          break;
        case Mod.WishboneModes.inventory:
          inventoryItems = player.GetInventory().GetAllItems();
          break;
        default:
          hasWishbone = true;
          if(origHasWishbone != hasWishbone) Mod.forcePinRefresh();
          return;
      }

      hasWishbone = false;
      foreach (ItemDrop.ItemData item in inventoryItems)
      {
        if (item.m_dropPrefab.name == "Wishbone")
        {
          hasWishbone = true;
          break;
        }
      }

      if(origHasWishbone != hasWishbone) Mod.forcePinRefresh();
    }

    [HarmonyPatch(typeof(Player), "Awake")]
    internal class PlayerAwakePatch
    {
      private static void Postfix(ref Player __instance)
      {
        if (!Player.m_localPlayer || !__instance.IsOwner() || Game.IsPaused() || Mod.checkingPins)
          return;

        player = __instance;
        currPos = player.transform.position;
        prevPos = player.transform.position;
        checkForWishbone();
      }
    }

    [HarmonyPatch(typeof(Player), "Update")]
    internal class PlayerUpdatePatch
    {
      private static void Postfix(ref Player __instance)
      {
        if (!Player.m_localPlayer || !__instance.IsOwner() || Game.IsPaused() || Mod.checkingPins || Mod.hasMoved) return;

        Mod.currEnv = EnvMan.instance.GetCurrentEnvironment().m_name;

        if (Time.frameCount % interval == 0)
        {
          player = __instance;
          currPos = __instance.transform.position;
          checkForWishbone();
          Mod.hasMoved = Vector3.Distance(currPos, prevPos) > 5;
        }

        if (Mod.hasMoved)
        {
          Mod.hasMoved = false;
          prevPos = currPos;
          Mod.Log.LogDebug("Patches.PlayerUpdatePatch player movement detected. Check Pins");
          Mod.checkPins();
        }
      }
    }
  }
}
