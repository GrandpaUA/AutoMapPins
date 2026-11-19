using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AutoMapPins.Icons;

// Changes only the visible button image, never the selected overlay, m_icons, filter tint or scale.
internal static class PickerIconOverrides
{
    private sealed class ButtonState
    {
        internal Image Image = null!;
        internal Sprite Original = null!;
        internal Sprite Applied = null!;
    }

    private static readonly Dictionary<Minimap.PinType, ButtonState> States = new();
    private static Minimap? _owner;

    internal static void Refresh(Minimap? minimap)
    {
        if (!ReferenceEquals(_owner, minimap))
        {
            Reset();
            _owner = minimap;
        }
        if (minimap == null) return;

        RefreshButton(minimap, Minimap.PinType.Icon0, minimap.m_selectedIcon0);
        RefreshButton(minimap, Minimap.PinType.Icon1, minimap.m_selectedIcon1);
        RefreshButton(minimap, Minimap.PinType.Icon2, minimap.m_selectedIcon2);
        RefreshButton(minimap, Minimap.PinType.Icon3, minimap.m_selectedIcon3);
        RefreshButton(minimap, Minimap.PinType.Icon4, minimap.m_selectedIcon4);
        RefreshButton(minimap, Minimap.PinType.Death, minimap.m_selectedIconDeath);
        RefreshButton(minimap, Minimap.PinType.Boss, minimap.m_selectedIconBoss);
    }

    private static void RefreshButton(Minimap minimap, Minimap.PinType type, Image? selectedOverlay)
    {
        // Valheim's selection Image is an overlay; the parent Image is the picker button.
        // An unknown prefab hierarchy or another mod's sprite must be left untouched.
        Image? button = selectedOverlay && selectedOverlay.transform.parent
            ? selectedOverlay.transform.parent.GetComponent<Image>() : null;
        if (States.TryGetValue(type, out ButtonState previous) &&
            (!button || !ReferenceEquals(button, previous.Image)))
        {
            Restore(previous);
            States.Remove(type);
        }
        if (!button) return;

        bool hasOverride = Assets.TryGetVanillaMapOverride(type, out Sprite sprite, out _);
        if (States.TryGetValue(type, out ButtonState state))
        {
            if (!ReferenceEquals(button.sprite, state.Applied))
            {
                // Another UI mod owns this image now. Never overwrite or restore its work.
                States.Remove(type);
                return;
            }
            if (hasOverride)
            {
                button.sprite = sprite;
                state.Applied = sprite;
            }
            else
            {
                Restore(state);
                States.Remove(type);
            }
            return;
        }
        if (!hasOverride) return;
        Sprite? vanilla = null;
        foreach (Minimap.SpriteData data in minimap.m_icons)
            if (data.m_name == type) { vanilla = data.m_icon; break; }
        if (vanilla == null || !ReferenceEquals(button.sprite, vanilla)) return;
        States[type] = new ButtonState { Image = button, Original = vanilla, Applied = sprite };
        button.sprite = sprite;
    }

    private static void Restore(ButtonState state)
    {
        if (state.Image && ReferenceEquals(state.Image.sprite, state.Applied))
            state.Image.sprite = state.Original;
    }

    internal static void Reset()
    {
        foreach (ButtonState state in States.Values) Restore(state);
        States.Clear();
        _owner = null;
    }
}
