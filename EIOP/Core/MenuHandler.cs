using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EIOP.Tab_Handlers;
using EIOP.Tools;
using TMPro;
using UnityEngine;

namespace EIOP.Core;

public class MenuHandler : MonoBehaviour
{
    public static readonly Vector3 TargetMenuScale = Vector3.one * 15f;
    public static readonly Vector3 BaseMenuPosition = new(0.25f, 0f, 0.05f);
    public static readonly Quaternion BaseMenuRotation = Quaternion.Euler(300f, 0f, 180f);

    public bool IsMenuOpen;
    public GameObject Menu;
    private bool wasPressed;

    // --- COLORS ---
    // Bright Green for the bars
    private Color BarColor = Color.green; 
    // Dark Green for the background and buttons
    private Color BaseColor = new Color(0.0f, 0.2f, 0.0f, 1.0f); 

    private void Start()
    {
        GameObject menuPrefab = Plugin.EIOPBundle.LoadAsset<GameObject>("Menu");
        Menu = Instantiate(menuPrefab, EIOPUtils.RealLeftController);
        Destroy(menuPrefab);
        Menu.name = "Menu";

        Menu.transform.localPosition = BaseMenuPosition;
        Menu.transform.localRotation = BaseMenuRotation;
        Menu.transform.localScale    = Vector3.zero;

        // 1. Fix Shaders first
        PerformShaderManagement(Menu);

        // 2. Color the Main Menu Background
        Renderer mainRenderer = Menu.GetComponent<Renderer>();
        if (mainRenderer != null)
        {
            mainRenderer.material.color = BaseColor; // Dark Green
        }

        // 3. Color the Side/Bottom Bars (ModePanel)
        Transform panelTrans = Menu.transform.Find("ModePanel");
        if (panelTrans != null)
        {
            Renderer panelRenderer = panelTrans.GetComponent<Renderer>();
            if (panelRenderer != null)
            {
                panelRenderer.material.color = BarColor; // Bright Green
            }
        }

        // Set global colors for other scripts to use
        Plugin.MainColour      = BaseColor;
        Plugin.SecondaryColour = BarColor;

        Menu.SetActive(false);
        SetUpTabs(); // Buttons are colored inside here now

        gameObject.AddComponent<PCHandler>().MenuHandlerInstance = this;
    }

    private void Update()
    {
        bool isPressed = ControllerInputPoller.instance.leftControllerSecondaryButton;

        if (isPressed && !wasPressed)
        {
            IsMenuOpen = !IsMenuOpen;
            CoroutineManager.Instance.StartCoroutine(IsMenuOpen ? OpenMenu() : CloseMenu());
        }

        wasPressed = isPressed;
    }

    private void SetUpTabs()
    {
        List<Type> tabHandlerTypes = Assembly.GetExecutingAssembly().GetTypes()
                                             .Where(t => !t.IsAbstract && t.IsClass &&
                                                         typeof(TabHandlerBase).IsAssignableFrom(t)).ToList();

        List<Transform> tabViews = Menu.GetComponentsInChildren<Transform>(true)
                                       .Where(t => t.gameObject.name.EndsWith("View",
                                                      StringComparison.OrdinalIgnoreCase)).ToList();

        Transform modePanel = Menu.transform.Find("ModePanel");
        List<Transform> tabButtons = modePanel.GetComponentsInChildren<Transform>(true)
                                              .Where(t => t.gameObject.name.EndsWith("Button",
                                                             StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (Type tabHandlerType in tabHandlerTypes)
        {
            string tabName = tabHandlerType.Name.Replace("Handler", "");
            Transform tabView =
                    tabViews.FirstOrDefault(t => t.gameObject.name.Equals(tabName + "View",
                                                    StringComparison.OrdinalIgnoreCase));

            Transform tabButton =
                    tabButtons.FirstOrDefault(t => t.gameObject.name.Equals(tabName + "Button",
                                                      StringComparison.OrdinalIgnoreCase));

            if (tabView == null || tabButton == null)
            {
                Debug.LogWarning($"[MenuHandler] Could not find View or Button for tab: {tabName}");
                continue;
            }

            // --- FIX: COLORING THE BUTTONS ---
            // We use GetComponentInChildren because the Renderer might be inside the button object
            Renderer btnRenderer = tabButton.GetComponentInChildren<Renderer>();
            if (btnRenderer != null)
            {
                // Make buttons Dark Green so they stand out against the Bright Green bar
                btnRenderer.material.color = BaseColor; 
            }
            else 
            {
                // Backup: try to color the object itself if no children found
                if(tabButton.TryGetComponent(out Renderer directRenderer))
                {
                    directRenderer.material.color = BaseColor;
                }
            }
            // ---------------------------------

            EIOPButton button = tabButton.gameObject.AddComponent<EIOPButton>();
            button.OnPress = () =>
                             {
                                 foreach (Transform tab in tabViews)
                                     tab.gameObject.SetActive(false);

                                 tabView.gameObject.SetActive(true);
                             };

            tabView.gameObject.AddComponent(tabHandlerType);
            tabView.gameObject.SetActive(false);
        }
    }

    public static void PerformShaderManagement(GameObject obj)
    {
        foreach (Transform child in obj.transform)
            PerformShaderManagement(child.gameObject);

        if (obj.TryGetComponent(out Renderer renderer))
            if (renderer.material.shader.name.Contains("Universal"))
            {
                renderer.material.shader = Plugin.UberShader;
                if (renderer.material.mainTexture != null) renderer.material.EnableKeyword("_USE_TEXTURE");
            }

        if (obj.TryGetComponent(out TextMeshPro tmp))
            tmp.fontMaterial = new Material(tmp.fontMaterial)
            {
                    shader = Shader.Find("TextMeshPro/Mobile/Distance Field"),
            };

        if (obj.TryGetComponent(out TextMeshProUGUI tmpUGUI))
            tmpUGUI.fontMaterial = new Material(tmpUGUI.fontMaterial)
            {
                    shader = Shader.Find("TextMeshPro/Mobile/Distance Field"),
            };
    }

    public IEnumerator OpenMenu()
    {
        Menu.SetActive(true);
        Menu.transform.localScale = Vector3.zero;
        float startTime = Time.time;

        while (Time.time - startTime < 0.1f)
        {
            float t = (Time.time - startTime) / 0.1f;
            Menu.transform.localScale = Vector3.Lerp(Vector3.zero, TargetMenuScale, t);

            yield return null;
        }

        Menu.transform.localScale = TargetMenuScale;
    }

    public IEnumerator CloseMenu()
    {
        Menu.transform.localScale = TargetMenuScale;
        float startTime = Time.time;

        while (Time.time - startTime < 0.1f)
        {
            float t = (Time.time - startTime) / 0.1f;
            Menu.transform.localScale = Vector3.Lerp(TargetMenuScale, Vector3.zero, t);

            yield return null;
        }

        Menu.transform.localScale = Vector3.zero;
        Menu.SetActive(false);
    }
}
