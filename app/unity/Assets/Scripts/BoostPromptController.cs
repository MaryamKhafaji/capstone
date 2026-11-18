using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BoostPromptController : MonoBehaviour
{
    [Header("References")]
    public EnergySystem energySystem;
    public GameObject boostPromptPanel;
    public TMP_Text promptText;
    public Image flashImage;

    [Header("Normal Boost")]
    public string boostName = "EXPOSE";
    public float requiredEnergy = 100f;

    [Header("Power Flash")]
    public float flashInTime = 0.08f;
    public float holdTime = 0.08f;
    public float flashOutTime = 0.65f;

    [Range(0f, 1f)]
    public float maxFlashAlpha = 0.75f;

    [Header("Normal Boost Action")]
    public UnityEvent onBoostActivated;

    [Header("Physical Power Action")]
    public UnityEvent onPowerBoostActivated;

    [Header("Editor Testing")]
    public bool enablePowerKeyTest = true;
    public KeyCode powerTestKey = KeyCode.R;

    private bool offeredAtCurrentCharge;
    private Coroutine flashRoutine;

    void OnEnable()
    {
        offeredAtCurrentCharge = false;

        if (boostPromptPanel != null)
            boostPromptPanel.SetActive(false);

        if (flashImage != null)
        {
            Color color = flashImage.color;
            color.a = 0f;
            flashImage.color = color;

            flashImage.gameObject.SetActive(false);
        }

        UpdatePromptText();
    }

    void Update()
    {
        // =========================================
        // EDITOR POWER TEST
        // R acts like the ESP32 sent "Power"
        // Completely independent from steps/energy
        // =========================================

#if UNITY_EDITOR
        if (enablePowerKeyTest &&
            Input.GetKeyDown(powerTestKey))
        {
            TriggerPowerDetected();
        }
#endif

        // =========================================
        // NORMAL ENERGY BOOST PROMPT
        // =========================================

        if (energySystem == null ||
            boostPromptPanel == null)
        {
            return;
        }

        if (energySystem.currentEnergy < requiredEnergy)
        {
            offeredAtCurrentCharge = false;
        }

        if (!offeredAtCurrentCharge &&
            energySystem.currentEnergy >= requiredEnergy)
        {
            offeredAtCurrentCharge = true;

            UpdatePromptText();

            boostPromptPanel.SetActive(true);
        }
    }

    // =========================================
    // NORMAL BOOST
    // This IS the energy-based boost.
    // =========================================

    public void ActivateBoost()
    {
        if (energySystem == null)
            return;

        if (!energySystem.SpendEnergy(requiredEnergy))
            return;

        if (boostPromptPanel != null)
            boostPromptPanel.SetActive(false);

        PlayPowerFlash();

        onBoostActivated?.Invoke();

        Debug.Log(
            "NORMAL BOOST ACTIVATED: " + boostName
        );
    }

    public void DeclineBoost()
    {
        if (boostPromptPanel != null)
            boostPromptPanel.SetActive(false);
    }

    // =========================================
    // PHYSICAL POWER BOOST
    //
    // ESP32 "Power" OR keyboard R calls this.
    //
    // DOES NOT:
    // - check steps
    // - check energy
    // - spend energy
    // - reset energy
    // - change normal boost charge
    // =========================================

    public void TriggerPowerDetected()
    {
        Debug.Log(
            "POWER DETECTED -> PHYSICAL BOOST FIRED"
        );

        PlayPowerFlash();

        onPowerBoostActivated?.Invoke();
    }

    // =========================================
    // SCREEN EFFECT
    // =========================================

    public void PlayPowerFlash()
    {
        if (flashImage == null)
        {
            Debug.LogWarning(
                "Power flash image is not assigned."
            );

            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine =
            StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashImage.gameObject.SetActive(true);

        Color color = flashImage.color;

        color.a = 0f;
        flashImage.color = color;

        // Flash in
        float elapsed = 0f;

        while (elapsed < flashInTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / flashInTime
            );

            color.a = Mathf.Lerp(
                0f,
                maxFlashAlpha,
                t
            );

            flashImage.color = color;

            yield return null;
        }

        // Hold at full brightness
        color.a = maxFlashAlpha;
        flashImage.color = color;

        yield return new WaitForSeconds(
            holdTime
        );

        // Fade out
        elapsed = 0f;

        while (elapsed < flashOutTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / flashOutTime
            );

            color.a = Mathf.Lerp(
                maxFlashAlpha,
                0f,
                t
            );

            flashImage.color = color;

            yield return null;
        }

        color.a = 0f;
        flashImage.color = color;

        flashImage.gameObject.SetActive(false);

        flashRoutine = null;
    }

    private void UpdatePromptText()
    {
        if (promptText != null)
        {
            promptText.text =
                "ENERGY FULL\nActivate " +
                boostName +
                "?";
        }
    }
}