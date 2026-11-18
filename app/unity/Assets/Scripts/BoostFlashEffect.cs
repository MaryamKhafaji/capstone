using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BoostFlashEffect : MonoBehaviour
{
    [Header("References")]
    public Image flashImage;

    [Header("Settings")]
    public float flashInTime = 0.08f;
    public float holdTime = 0.08f;
    public float flashOutTime = 0.65f;

    private Coroutine flashRoutine;

    public void PlayBoostFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        if (flashImage == null)
            yield break;

        flashImage.gameObject.SetActive(true);

        Color color = flashImage.color;

        // Start invisible
        color.a = 0f;
        flashImage.color = color;

        // Flash in
        float elapsed = 0f;

        while (elapsed < flashInTime)
        {
            elapsed += Time.deltaTime;

            color.a = Mathf.Lerp(
                0f,
                0.75f,
                elapsed / flashInTime
            );

            flashImage.color = color;

            yield return null;
        }

        yield return new WaitForSeconds(holdTime);

        // Fade out
        elapsed = 0f;

        while (elapsed < flashOutTime)
        {
            elapsed += Time.deltaTime;

            color.a = Mathf.Lerp(
                0.75f,
                0f,
                elapsed / flashOutTime
            );

            flashImage.color = color;

            yield return null;
        }

        color.a = 0f;
        flashImage.color = color;

        flashImage.gameObject.SetActive(false);

        flashRoutine = null;
    }
}