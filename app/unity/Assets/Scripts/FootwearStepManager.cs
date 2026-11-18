using System;
using System.Collections;
using TMPro;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class FootwearStepManager : MonoBehaviour
{
    [Header("References")]
    public EnergySystem energySystem;
    public TMP_Text connectionStatusText;

    [Header("Editor / BlueStacks Testing")]
    public bool simulateStepsInEditor = true;
    public KeyCode simulatedStepKey = KeyCode.Space;

    private bool huntActive;
    private int fallbackHuntSteps;
    private int huntStartSessionSteps;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaClass footwearBridge;
#endif

    public bool HuntActive => huntActive;

    public int HuntSteps =>
        energySystem != null
            ? energySystem.HuntSteps
            : fallbackHuntSteps;

    public int SessionSteps => HuntSteps;
    public int HuntStartSessionSteps => huntStartSessionSteps;

    void Awake()
    {
        if (energySystem == null)
        {
            energySystem = FindFirstObjectByType<EnergySystem>();
        }

        SetStatus("Footwear idle");
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (footwearBridge != null)
        {
            // Drain a bounded number of queued BLE messages each frame.
            for (int index = 0; index < 32; index++)
            {
                string message =
                    footwearBridge.CallStatic<string>("pollMessage");

                if (string.IsNullOrEmpty(message))
                {
                    break;
                }

                OnFootwearBleMessage(message);
            }
        }
#endif

#if UNITY_EDITOR
        if (huntActive &&
            simulateStepsInEditor &&
            Input.GetKeyDown(simulatedStepKey))
        {
            RegisterStep();
        }

        if (huntActive &&
            Input.GetKeyDown(KeyCode.R))
        {
            OnFootwearBleMessage("Power");
        }
#endif
    }

    // Begin a new hunt without resetting the AR-session counter.
    public void BeginHunt()
    {
        huntStartSessionSteps = SessionSteps;
        bool wasAlreadyCounting = huntActive;
        huntActive = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Do not reconnect BLE for every monster.
        if (!wasAlreadyCounting)
            StartCoroutine(ConnectOnAndroid());
#else
        SetStatus("AR session counting; hunt baseline=" + huntStartSessionSteps);
#endif
    }

    // Returns only this hunt's steps; the session keeps counting.
    public int CompleteHunt()
    {
        int completedSteps = Mathf.Max(0, SessionSteps - huntStartSessionSteps);
        Debug.Log("FINAL HUNT STEPS: " + completedSteps +
                  " | SESSION STEPS: " + SessionSteps);
        return completedSteps;
    }

    // Called when leaving the AR experience, never between hunts.
    public void EndSession()
    {
        huntActive = false;
        DisconnectFootwear();
        SetStatus("AR session ended");
    }

    public void CancelHunt()
    {
        huntActive = false;
        SetStatus("Hunt cancelled; AR session continues");
    }

    // Called by Android BLE plug-in through UnitySendMessage.
    public void OnFootwearBleMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string cleanMessage = message.Trim();
        Debug.Log("ESP32 RAW MESSAGE: [" + cleanMessage + "]");

        if (cleanMessage.StartsWith(
            "STATUS:",
            StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(
                cleanMessage.Substring(
                    "STATUS:".Length
                )
            );

            return;
        }

        if (cleanMessage.Equals(
                "Pressed",
                StringComparison.OrdinalIgnoreCase))
        {
            RegisterStep();
            return;
        }

        if (huntActive &&
            cleanMessage.Equals(
                "Power",
                StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("POWER signal received from footwear.");
            return;
        }
    }

    public void SimulateOneStep()
    {
        
            RegisterStep();
        
    }

    void RegisterStep()
    {
        fallbackHuntSteps++;

        if (energySystem != null)
        {
            energySystem.RegisterFootwearStep();
        }

        Debug.Log(
            "FOOTWEAR STEP: " +
            HuntSteps
        );
    }

    void SetStatus(string status)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = status;
        }

        Debug.Log(
            "FOOTWEAR: " +
            status
        );
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    IEnumerator ConnectOnAndroid()
    {
        SetStatus(
            "Requesting Bluetooth permission..."
        );

        int sdkVersion;

        using (
            AndroidJavaClass versionClass =
                new AndroidJavaClass(
                    "android.os.Build$VERSION"
                )
        )
        {
            sdkVersion =
                versionClass.GetStatic<int>(
                    "SDK_INT"
                );
        }

        if (sdkVersion >= 31)
        {
            string[] permissions =
            {
                "android.permission.BLUETOOTH_SCAN",
                "android.permission.BLUETOOTH_CONNECT"
            };

            bool missingPermission = false;

            foreach (string permission in permissions)
            {
                if (!Permission
                    .HasUserAuthorizedPermission(
                        permission
                    ))
                {
                    missingPermission = true;
                    break;
                }
            }

            if (missingPermission)
            {
                Permission.RequestUserPermissions(
                    permissions
                );

                float timeout = 15f;

                while (timeout > 0f)
                {
                    bool allGranted = true;

                    foreach (
                        string permission
                        in permissions
                    )
                    {
                        if (!Permission
                            .HasUserAuthorizedPermission(
                                permission
                            ))
                        {
                            allGranted = false;
                            break;
                        }
                    }

                    if (allGranted)
                    {
                        break;
                    }

                    timeout -=
                        Time.unscaledDeltaTime;

                    yield return null;
                }

                foreach (
                    string permission
                    in permissions
                )
                {
                    if (!Permission
                        .HasUserAuthorizedPermission(
                            permission
                        ))
                    {
                        SetStatus(
                            "Bluetooth permission denied"
                        );

                        yield break;
                    }
                }
            }
        }
        else
        {
            string locationPermission =
                "android.permission.ACCESS_FINE_LOCATION";

            if (!Permission
                .HasUserAuthorizedPermission(
                    locationPermission
                ))
            {
                Permission.RequestUserPermission(
                    locationPermission
                );

                float timeout = 15f;

                while (
                    timeout > 0f &&
                    !Permission
                        .HasUserAuthorizedPermission(
                            locationPermission
                        )
                )
                {
                    timeout -=
                        Time.unscaledDeltaTime;

                    yield return null;
                }

                if (!Permission
                    .HasUserAuthorizedPermission(
                        locationPermission
                    ))
                {
                    SetStatus(
                        "Location permission denied"
                    );

                    yield break;
                }
            }
        }

        if (!huntActive)
        {
            yield break;
        }

        SetStatus(
            "Searching for ESP32_Shoe..."
        );

        try
        {
            footwearBridge =
                new AndroidJavaClass(
                    "com.masar.footwear.FootwearBleBridge"
                );

            using (
                AndroidJavaClass unityPlayer =
                    new AndroidJavaClass(
                        "com.unity3d.player.UnityPlayer"
                    )
            )
            using (
                AndroidJavaObject activity =
                    unityPlayer.GetStatic<
                        AndroidJavaObject
                    >(
                        "currentActivity"
                    )
            )
            {
                footwearBridge.CallStatic(
                    "connect",
                    activity
                );
            }
        }
        catch (Exception exception)
        {
            SetStatus(
                "BLE plug-in unavailable"
            );

            Debug.LogException(
                exception
            );
        }
    }
#endif

    public void ReleaseForReturn()
    {
        DisconnectFootwear();
    }

    void DisconnectFootwear()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (footwearBridge == null)
            {
                footwearBridge =
                    new AndroidJavaClass(
                        "com.masar.footwear.FootwearBleBridge"
                    );
            }

            footwearBridge.CallStatic(
                "disconnect"
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Could not disconnect footwear BLE: " +
                exception.Message
            );
        }
#endif
    }

    void OnDestroy()
    {
        DisconnectFootwear();

#if UNITY_ANDROID && !UNITY_EDITOR
        footwearBridge?.Dispose();
        footwearBridge = null;
#endif
    }
}
