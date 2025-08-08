using MelonLoader;
using UnityEngine;
using Il2CppVampireSurvivors.UI;
using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using System.Collections;
using Il2CppSystem.Runtime.Remoting.Messaging;

[assembly: MelonInfo(typeof(CoinsCapRemover.CoinsCapRemover), "Coins Cap Remover", "0.0.1", "ZaPasta and Black0wl")]
[assembly: MelonGame("poncle", "Vampire Survivors")]

namespace CoinsCapRemover
{
    public class CoinsCapRemover : MelonMod
    {
        private static CoinsUI coinsUI;
        
        // PlayerOptionsData access variables
        private Type playerOptionsDataType = null;
        private MethodInfo getCoinsMethod = null;
        private object playerOptionsDataInstance = null;
        private bool isFullyInitialized = false;
        private float currentCoins = 0f;
        private static Il2CppTMPro.TextMeshProUGUI wwwComponent = null;
        
        // Display variables
        private float lastCoinAmount = -1f;
        private bool showCoins = false;
        private const float INITIALIZATION_RETRY_INTERVAL = 0.1f;
        
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Coins Cap Remover initialized!");
            MelonLogger.Msg("F1 = Toggle display GUI (debugging)");

            // Start immediate initialization attempt
            MelonCoroutines.Start(InitializationCoroutine());
        }

        private IEnumerator InitializationCoroutine()
        {
            while (!isFullyInitialized)
            {
                try
                {
                    if (InitializePlayerOptionsDataMethods())
                    {
                        isFullyInitialized = true;
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Stack trace: {ex.StackTrace}");
                }
                
                yield return new WaitForSeconds(INITIALIZATION_RETRY_INTERVAL);
            }
        }

        public override void OnUpdate()
        {
            if (wwwComponent == null || wwwComponent.WasCollected)
            {
                FindWWWComponent();
            }

            if (!isFullyInitialized)
                return;

            // Toggle display with F1
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1)
            {
                showCoins = !showCoins;
                MelonLogger.Msg($"Coin display: {(showCoins ? "ON" : "OFF")}");
            }

            try
            {
                // Display current coins if we have everything set up
                if (playerOptionsDataInstance != null && getCoinsMethod != null)
                {
                    currentCoins = (float)getCoinsMethod.Invoke(playerOptionsDataInstance, null);

                    // Only log when coins change to avoid spam
                    if (Math.Abs(currentCoins - lastCoinAmount) > 0.01f)
                    {
                        lastCoinAmount = currentCoins;
                        if (wwwComponent != null)
                        {
                            wwwComponent.text = FormatAsKMB(currentCoins);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnUpdate: {ex.Message}");

                // Reset initialization to retry
                isFullyInitialized = false;
                playerOptionsDataInstance = null;
                MelonCoroutines.Start(InitializationCoroutine());
            }
        }

        private void FindWWWComponent()
        {
            var TMPText = Resources.FindObjectsOfTypeAll<Il2CppTMPro.TextMeshProUGUI>().FirstOrDefault(t => t != null && t.gameObject != null && t.gameObject.name == "www");

            if (TMPText != null)
            {
                wwwComponent = TMPText;
                return;
            }
        }

        // TODO Implement feature to show the full coins number in the UI with delimeters
        // TODO Fix the format of coins under 10M

        static string FormatAsKMB(float value)
        {
            if (value >= 1_000_000_000_000)
                return (value / 1_000_000_000_000f).ToString("0.##") + "T";
            if (value >= 1_000_000_000)
                return (value / 1_000_000_000f).ToString("0.##") + "B";
            if (value >= 10_000_000)
                return (value / 1_000_000f).ToString("0.##") + "M";

            return value.ToString();
        }

        public override void OnGUI()
        {
            if (!showCoins)
                return;

            try
            {
                if (isFullyInitialized && playerOptionsDataInstance != null && getCoinsMethod != null)
                {
                    float currentCoins = (float)getCoinsMethod.Invoke(playerOptionsDataInstance, null);
                    GUI.color = Color.yellow;

                    if(wwwComponent != null)
                    {
                        wwwComponent.text = FormatAsKMB((float)currentCoins);
                    }
                    
                    // TODO Add delimeters to debug coins

                    GUI.Label(new Rect(10, 10, 300, 30), $"Real Currency Value: {currentCoins:F3}");
                    GUI.color = Color.white;
                }
            }
            catch (Exception ex)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 10, 300, 30), $"Error: {ex.Message}");
                GUI.color = Color.white;
            }
        }

        private bool InitializePlayerOptionsDataMethods()
        { 
            try
            {
                // Find PlayerOptionsData type in all assemblies
                if (playerOptionsDataType == null)
                {
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            var types = assembly.GetTypes();
                            playerOptionsDataType = types.FirstOrDefault(t => t.Name == "PlayerOptionsData");
                            if (playerOptionsDataType != null)
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"Error searching assembly {assembly.GetName().Name}: {ex.Message}");
                        }
                    }
                }

                if (playerOptionsDataType == null)
                {
                    return false;
                }

                // Find the Coins property getter method
                if (getCoinsMethod == null)
                {
                    var methods = playerOptionsDataType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    // Look for get_Coins method (property getter)
                    getCoinsMethod = methods.FirstOrDefault(m => 
                        m.Name == "get_Coins" && m.GetParameters().Length == 0);
                    
                    // Also try looking at properties directly
                    if (getCoinsMethod == null)
                    {
                        var properties = playerOptionsDataType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        var coinsProperty = properties.FirstOrDefault(p => p.Name == "Coins" && p.CanRead);
                        if (coinsProperty != null)
                        {
                            getCoinsMethod = coinsProperty.GetGetMethod(true);
                        }
                    }

                    if (getCoinsMethod == null)
                    {
                        foreach (var method in methods)
                        {
                            if (method.Name.ToLower().Contains("coin"))
                            {
                                var paramTypes = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                                MelonLogger.Msg($"  {method.Name}({paramTypes}) - Returns: {method.ReturnType.Name}");
                            }
                        }
                        return false;
                    }
                }

                // Try to find an instance
                playerOptionsDataInstance = FindPlayerOptionsDataInstance();

                return playerOptionsDataInstance != null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error initializing methods: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private object FindPlayerOptionsDataInstance()
        {
            if (playerOptionsDataType == null)
                return null;
                
            try
            {
                if (coinsUI != null)
                {
                    if (coinsUI._playerOptions != null)
                    {
                        var playerOptions = coinsUI._playerOptions;
                        
                        // Search all fields in PlayerOptions for PlayerOptionsData
                        var playerOptionsType = playerOptions.GetType();
                        var fields = playerOptionsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        foreach (var field in fields)
                        {
                            if (field.FieldType == playerOptionsDataType)
                            {
                                try
                                {
                                    var instance = field.GetValue(playerOptions);
                                    if (instance != null)
                                    {
                                        return instance;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Error($"Error accessing field {field.Name}: {ex.Message}");
                                }
                            }
                            else if (field.FieldType.Name.Contains("PlayerOptionsData"))
                            {
                                try
                                {
                                    var instance = field.GetValue(playerOptions);
                                    if (instance != null)
                                    {
                                        return instance;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Error($"Error accessing similar field {field.Name}: {ex.Message}");
                                }
                            }
                        }
                        
                        // Also check properties in PlayerOptions
                        var properties = playerOptionsType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var prop in properties)
                        {     
                            if (prop.PropertyType == playerOptionsDataType && prop.CanRead)
                            {
                                try
                                {
                                    var instance = prop.GetValue(playerOptions);
                                    if (instance != null)
                                    {
                                        return instance;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Error($"Error accessing property {prop.Name}: {ex.Message}");
                                }
                            }
                            else if (prop.PropertyType.Name.Contains("PlayerOptionsData"))
                            {
                                try
                                {
                                    var instance = prop.GetValue(playerOptions);
                                    if (instance != null)
                                    {
                                        return instance;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Error($"Error accessing similar property {prop.Name}: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding PlayerOptionsData instance: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // Patch CoinsUI.Start to get the CoinsUI instance
        [HarmonyPatch(typeof(CoinsUI), nameof(CoinsUI.Start))]
        static class PatchCoinsUI 
        { 
            static void Postfix(CoinsUI __instance) 
            {
                coinsUI = __instance;
            } 
        }
    }
}
