using HarmonyLib;
using Il2CppVampireSurvivors.UI;
using MelonLoader;
using System.Collections;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(CoinsCapRemover.CoinsCapRemover), "Coins Cap Remover", "0.0.1", "ZaPasta and Black0wl")]
[assembly: MelonGame("poncle", "Vampire Survivors")]

namespace CoinsCapRemover

{
    public class CoinsCapRemover : MelonMod
    {
        private static CoinsUI coinsUI;
        private Il2CppTMPro.TextMeshProUGUI wwwComponent = null;
        private object playerOptionsDataInstance = null;
        private Type playerOptionsDataType = null;
        private MethodInfo getCoinsMethod = null;

        private bool showCoins = false;
        private bool formatCurrency = false;
        private bool isFullyInitialized = false;
        private bool isCoinsPanelSizeSet = false;
        private bool watchingForUI = false;

        private float currentCoins = 0f;
        private float lastCoinAmount = -1f;
        private const float INITIALIZATION_RETRY_INTERVAL = 0.1f;


        public static CoinsCapRemover Instance;

        private GUIStyle style = null;

        public override void OnInitializeMelon()
        {
            Instance = this;

            MelonLogger.Msg("Coins Cap Remover initialized!");
            MelonLogger.Msg("F1 = Toggle display GUI (debugging)");
            MelonLogger.Msg("F2 = Toggle currency formatting");

            // Start immediate initialization attempt
            MelonCoroutines.Start(InitializationCoroutine());

            PatchGetCurrency();
        }

        private void PatchGetCurrency()
        {
            var harmony = new HarmonyLib.Harmony("com.company.coinscapremover");

            var methods = typeof(MerchantUIPage).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            var target = methods.FirstOrDefault(m => m.Name.Contains("Populate") && m.GetParameters().Length == 0);

            if (target == null)
            {
                return;
            }

            var postfix = typeof(CoinsCapRemover).GetMethod(nameof(GetCurrencyPostfix),
                BindingFlags.NonPublic | BindingFlags.Static);

            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
        }

        private static void GetCurrencyPostfix()
        {
            MelonCoroutines.Start(Instance.DelayedMethod());
        }

        private IEnumerator DelayedMethod()
        {
            yield return new WaitForSeconds(0.1f);
            Instance.isCoinsPanelSizeSet = false;
            Instance.UpdateWWWComponentText();
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

            // Toggle format currency with F2
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F2)
            {
                formatCurrency = !formatCurrency;
                MelonLogger.Msg($"Format Currency: {(formatCurrency ? "ON" : "OFF")}");

                UpdateWWWComponentText();
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
                        UpdateWWWComponentText();
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

            if (wwwComponent != null && wwwComponent.gameObject.activeInHierarchy && watchingForUI)
            {
                UpdateWWWComponentText();

                watchingForUI = false; // Stop checking after found
            }
        }

        private static void FindWWWComponent()
        {
            var TMPText = Resources.FindObjectsOfTypeAll<Il2CppTMPro.TextMeshProUGUI>().FirstOrDefault(t => t != null && t.gameObject != null && t.gameObject.name == "www");

            if (TMPText != null)
            {
                Instance.wwwComponent = TMPText;
                return;
            }
        }

        private void UpdateWWWComponentText()
        {
            if (wwwComponent != null)
            {
                if (currentCoins != 0)
                {
                    wwwComponent.text = formatCurrency ? FormatAsKMB(currentCoins) : $"{currentCoins:N0}";

                    if (!isCoinsPanelSizeSet)
                    {
                        isCoinsPanelSizeSet = true;
                        SetCoinsPanelSize();
                    }
                }
            }
        }

        private void SetCoinsPanelSize()
        {
            var parent = wwwComponent.transform.parent.GetComponent<RectTransform>();
            if (parent == null) return;
            parent.sizeDelta = new Vector2(400f, parent.sizeDelta.y);

            var rect = parent.GetComponent<RectTransform>();
            Vector2 pos = rect.anchoredPosition;
            pos.x += 30f; 
            rect.anchoredPosition = pos;

            var cashImage = parent.Find("CashImage");
            RectTransform imageRect = cashImage.GetComponent<RectTransform>();
            if (imageRect != null)
            {
                Vector2 imagePos = imageRect.anchoredPosition;
                imagePos.x -= 30f;
                imageRect.anchoredPosition = imagePos;
            }

            var coinsRect = wwwComponent.GetComponent<RectTransform>();

            Vector2 offsetMin = coinsRect.offsetMin;
            offsetMin.x -= 20;
            coinsRect.offsetMin = offsetMin;
        }

        static string FormatAsKMB(float value)
        {
            if (value >= 1_000_000_000_000)
                return (value / 1_000_000_000_000f).ToString("0.##") + "T";
            if (value >= 1_000_000_000)
                return (value / 1_000_000_000f).ToString("0.##") + "B";
            if (value >= 10_000_000)
                return (value / 1_000_000f).ToString("0.##") + "M";

            return value.ToString("N0");
        }

        public override void OnGUI()
        {
            if (!showCoins)
                return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20, // bigger text
                    alignment = TextAnchor.MiddleCenter, // center alignment
                    fontStyle = FontStyle.Bold // optional bold style
                };

                style.normal.textColor = Color.white; // default text color
            }

            try
            {
                if (isFullyInitialized && playerOptionsDataInstance != null && getCoinsMethod != null)
                {
                    float currentCoins = (float)getCoinsMethod.Invoke(playerOptionsDataInstance, null);
                    GUI.color = Color.yellow;

                    UpdateWWWComponentText();

                    GUI.Label(new Rect(200, 4, 500, 30), $"Real Currency Value: {currentCoins:N0}", style);

                }
            }
            catch (Exception ex)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(300, 10, 300, 30), $"Error: {ex.Message}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // MelonLogger.Msg($"Scene loaded: {sceneName}");
            if (sceneName == "MainMenu")
            {
                watchingForUI = true;
                isCoinsPanelSizeSet = false;
                MelonCoroutines.Start(InitializationCoroutine());
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
