using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ChangeSkinMP;

[DisallowMultipleComponent]
public sealed class SpriteReplacer : MonoBehaviour
{
    public SpriteRenderer Renderer { get; private set; }
    public Sprite Original { get; private set; }
    public bool Active { get; private set; }

    // Весь словарь скина — ищем по имени входящего спрайта
    public IReadOnlyDictionary<string, Sprite> SkinSprites;

    public static SpriteReplacer Attach(
        SpriteRenderer renderer,
        IReadOnlyDictionary<string, Sprite> skinSprites // почему? потому что спрайт может смениться каким то другим.
    )
    {
        var replacer =
            renderer.GetComponent<SpriteReplacer>()
            ?? renderer.gameObject.AddComponent<SpriteReplacer>();

        replacer.Renderer = renderer;
        replacer.Original = renderer.sprite;
        replacer.SkinSprites = skinSprites;
        return replacer;
    }

    public void Apply()
    {
        Active = true;
        // Сразу подменяем текущий спрайт если он есть в словаре
        if (
            Renderer.sprite != null
            && SkinSprites.TryGetValue(Renderer.sprite.name, out var initial)
        )
            Renderer.sprite = initial;
    }

    public void Restore()
    {
        Active = false;
        if (Renderer != null)
            Renderer.sprite = Original;
    }

    private void OnDestroy() => Restore();
}

[HarmonyPatch(typeof(SpriteRenderer), nameof(SpriteRenderer.sprite), MethodType.Setter)]
static class SpriteRendererSpritePatch
{
    static void Prefix(SpriteRenderer __instance, ref Sprite value)
    {
        var replacer = __instance.GetComponent<SpriteReplacer>();
        if (replacer == null || !replacer.Active || value == null)
            return;

        // Игра ставит любой спрайт — ищем его скинованную замену по имени
        // EyeHappy → skinned EyeHappy, EyeSad → skinned EyeSad, нет в словаре → пропускаем
        if (replacer.SkinSprites.TryGetValue(value.name, out var skinned))
            value = skinned;
    }
}
