using System;
using UnityEngine;

namespace ChangeSkin.Progress;

/// <summary>
/// MonoBehaviour that renders a floating load-indicator bar on screen.
/// Attach to any persistent GameObject (e.g. Plugin root).
/// Subscribe to a <see cref="LoadProgress"/> instance via <see cref="Bind"/>.
/// </summary>
internal sealed class SkinLoadIndicator : MonoBehaviour
{
    // ── Tunables ───────────────────────────────────────────────────────────
    private const float BarWidth = 300f;
    private const float BarHeight = 20f;
    private const float Margin = 16f;
    private const float AutoHideSeconds = 2.5f;

    // ── State ──────────────────────────────────────────────────────────────
    private string _phase = "";
    private float _percent;
    private bool _visible;
    private float _hideAt;
    private string _error;
    private GUIStyle _labelStyle;
    private GUIStyle _errorStyle;
    private bool _stylesReady;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Binds this indicator to a <see cref="LoadProgress"/> instance.</summary>
    public void Bind(LoadProgress progress)
    {
        progress.OnReport += HandleReport;
        progress.OnComplete += HandleComplete;
        progress.OnFail += HandleFail;
    }

    /// <summary>Manually show with a custom phase text.</summary>
    public void Show(string phase)
    {
        _phase = phase;
        _percent = 0f;
        _error = null;
        _visible = true;
        _hideAt = float.MaxValue;
    }

    /// <summary>Immediately hide the indicator.</summary>
    public void Hide()
    {
        _visible = false;
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void HandleReport(string phase, float percent)
    {
        // Unity: marshal to main thread if needed.
        if (!gameObject.activeInHierarchy)
            return;

        _phase = phase ?? _phase;
        _percent = Mathf.Clamp01(percent);
        _visible = true;
        _hideAt = float.MaxValue;
    }

    private void HandleComplete()
    {
        _phase = "Done!";
        _percent = 1f;
        _error = null;
        _hideAt = Time.unscaledTime + AutoHideSeconds;
    }

    private void HandleFail(string error)
    {
        _phase = "Failed";
        _percent = 0f;
        _error = error;
        _hideAt = Time.unscaledTime + AutoHideSeconds * 2f; // show error longer
    }

    // ── Unity lifecycle ────────────────────────────────────────────────────

    private void Update()
    {
        if (_visible && _hideAt < Time.unscaledTime)
            _visible = false;
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        EnsureStyles();

        float x = Screen.width - BarWidth - Margin;
        float y = Margin;

        // Background box
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x - 8, y - 4, BarWidth + 16, BarHeight + 28), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Phase label
        GUI.Label(new Rect(x, y - 2, BarWidth, 18), _phase, _labelStyle);

        // Progress bar background
        Rect barBg = new Rect(x, y + 18, BarWidth, BarHeight);
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(barBg, Texture2D.whiteTexture);

        // Progress bar fill
        if (_percent > 0f)
        {
            Rect barFill = new Rect(barBg.x, barBg.y, barBg.width * _percent, barBg.height);
            GUI.color = _error != null
                ? new Color(0.8f, 0.2f, 0.2f, 1f) // red on error
                : new Color(0.2f, 0.7f, 0.3f, 1f); // green normally
            GUI.DrawTexture(barFill, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        // Percent text centered on bar
        string pctText = $"{Mathf.RoundToInt(_percent * 100f)}%";
        GUI.Label(barBg, pctText, _labelStyle);

        // Error line below bar
        if (!string.IsNullOrEmpty(_error))
        {
            Rect errorRect = new Rect(x, barBg.yMax + 2, BarWidth, 18);
            GUI.Label(errorRect, _error, _errorStyle);
        }
    }

    private void EnsureStyles()
    {
        if (_stylesReady)
            return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _errorStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 11,
            normal = { textColor = new Color(1f, 0.5f, 0.5f, 1f) },
            wordWrap = true
        };

        _stylesReady = true;
    }
}
