﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WheelbarrowOfCash;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    
    private static GameObject wheelbarrowPrefab;
    private static GameObject cashPrefab;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int NormalMap = Shader.PropertyToID("_NormalMap");
    private static readonly int Metallic = Shader.PropertyToID("_Metallic");
    private static readonly int HeightMap = Shader.PropertyToID("_HeightMap");
    private static readonly int OcclusionMap = Shader.PropertyToID("_OcclusionMap");
    private Harmony m_harmony = new(MyPluginInfo.PLUGIN_GUID);
    
    private static readonly Dictionary<Customer, GameObject> wheelbarrowInstances = new();
    
    private void Awake()
    {
        Logger = base.Logger;
        try
        {
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            PluginConfig.Init(Config);
            m_harmony.PatchAll(typeof(WheelbarrowAdderInjector));
            m_harmony.PatchAll(typeof(WheelbarrowRemoverInjector));
            LoadWheelbarrow();
            LoadCash();
        }
        catch (Exception ex)
        {
            Logger.LogError("Wheelbarrow of Cash Failed to Awake - " + ex);
        }
    }
    
    private static void LoadWheelbarrow()
    {
        if (LoadAssetBundle(out var wheelbarrowBundle, "wheelbarrow")) return;

        wheelbarrowPrefab = wheelbarrowBundle.LoadAsset<GameObject>("wheelbarrow_OBJ");
        if (wheelbarrowPrefab == null)
        {
            Logger.LogError("Failed to load wheelbarrow prefab from AssetBundle!");
            return;
        }
        
        var materials = wheelbarrowBundle.LoadAllAssets<Material>();
        var textures = wheelbarrowBundle.LoadAllAssets<Texture2D>();

        foreach (var renderer in wheelbarrowPrefab.GetComponentsInChildren<Renderer>())
        {
            foreach (var material in materials)
            {
                switch (material.name)
                {
                    case "Metal" or "PMetal":
                        material.SetTexture(MainTex, textures.First(t => t.name == "pmetal_mettalic"));
                        material.SetTexture(NormalMap, textures.First(t => t.name == "pmetal_normal"));
                        material.SetTexture(Metallic, textures.First(t => t.name == "pmetal_roughness"));
                          break;
                    case "Tire" or "Rubber":
                        material.SetTexture(MainTex, textures.First(t => t.name == "tire_albedo"));
                        material.SetTexture(NormalMap, textures.First(t => t.name == "tire_normal"));
                        material.SetTexture(HeightMap, textures.First(t => t.name == "tire_height"));
                        material.SetTexture(OcclusionMap, textures.First(t => t.name == "tire_ao"));
                        break;
                }
            }
            renderer.material = materials.First(m => renderer.material.name.Contains(m.name));
        }
    }

    private static void LoadCash()
    {
        if (LoadAssetBundle(out var cashBundle, "cash")) return;
        
        cashPrefab = cashBundle.LoadAsset<GameObject>("cashpile 4");
        if (cashPrefab == null)
        {
            Logger.LogError("Failed to load cash prefab from AssetBundle!");
            return;
        }
        
        var materials = cashBundle.LoadAllAssets<Material>();
        var textures = cashBundle.LoadAllAssets<Texture2D>();
        
        foreach (var renderer in cashPrefab.GetComponentsInChildren<Renderer>())
        {
            foreach (var material in materials)
            {
                switch (material.name)
                {
                    case "100_Dollar":
                        material.SetTexture(MainTex, textures.First(t => t.name == "100_dollar"));
                        break;
                    case "Cash_Pile":
                        material.SetTexture(MainTex, textures.First(t => t.name == "cashpile"));
                        material.SetTexture(NormalMap, textures.First(t => t.name == "cashpile_n"));
                        break;
                }
            }
            renderer.material = materials.First(m => renderer.material.name.Contains(m.name));
        }
    }

    private static bool LoadAssetBundle(out AssetBundle assetBundle, string name)
    {
        var assetBundlePath = Path.Combine(Paths.PluginPath, "WheelbarrowOfCash", name);
        if (!File.Exists(assetBundlePath))
        {
            Logger.LogError("No asset bundle found at " + assetBundlePath);
            assetBundle = null;
            return true;
        }
        assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
        if (assetBundle != null) return false;
        Logger.LogError("Failed to load AssetBundle!");
        return true;

    }

    internal static bool IsHighRoll(float money)
    {
        return money >= PluginConfig.Threshold.Value;
    }
    
    internal static void AddWheelbarrowToNpc(Customer customer)
    {
        if (wheelbarrowPrefab == null) return;
        if (!IsHighRoll(customer.m_MaxMoney)) return;

        var wheelbarrowInstance = Instantiate(wheelbarrowPrefab, customer.transform);
        const float wheelbarrowScale = 1.3f;
        wheelbarrowInstance.transform.localScale = new Vector3(wheelbarrowScale, wheelbarrowScale, wheelbarrowScale);
        wheelbarrowInstance.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
        wheelbarrowInstance.transform.localPosition = new Vector3(0, 0.08f, 1);
        
        var cashInstance = Instantiate(cashPrefab, wheelbarrowInstance.transform);
        const float cashScale = 60f;
        cashInstance.transform.localScale = new Vector3(cashScale, cashScale, cashScale);
        cashInstance.transform.localPosition = new Vector3(0, 0.43f, 0.02f);
        wheelbarrowInstances[customer] = wheelbarrowInstance;
    }

    internal static void RemoveWheelbarrowFromNpc(Customer customer)
    {
        if (wheelbarrowPrefab == null) return;
        if (!wheelbarrowInstances.TryGetValue(customer, out var wheelbarrowInstance)) return;
        
        Logger.LogInfo("Destroying Wheelbarrow");
        Destroy(wheelbarrowInstance);
        wheelbarrowInstances.Remove(customer);
    }

    private class WheelbarrowAdderInjector
    {
        [HarmonyPatch(typeof(Customer), "ActivateCustomer")]
        private static void Postfix(Customer __instance)
        {
            AddWheelbarrowToNpc(__instance);
        }
    }

    private class WheelbarrowRemoverInjector
    {
        [HarmonyPatch(typeof(Customer), "DeactivateCustomer")]
        private static void Postfix(Customer __instance)
        {
            RemoveWheelbarrowFromNpc(__instance);
        }
    }
}
