using TMPro;
using UnityEngine;
using System;

public class TimeDisplay : MonoBehaviour
{
    public TMP_Text timeText;

    float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 1f)
        {
            timer = 0f;

            DateTime now = DateTime.Now;
            timeText.text = now.ToString("h:mm tt");
        }
    }
}