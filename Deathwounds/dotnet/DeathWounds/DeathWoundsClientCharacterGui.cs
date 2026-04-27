using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace DeathWounds;

/// <summary>Character dialog panel for death-touched wounds only. Loads before Trait Buffs (mod id order) so the buff panel can stack beneath.</summary>
public class DeathWoundsClientCharacterGui : ModSystem
{
    public const string ComposerKey = "deathwounds-wounds-panel";

    private const int WoundsClipHeight = 220;
    private const int WoundsVisibleLineCount = 10;
    private const int ApproxCharsPerRow = 48;
    private ICoreClientAPI? capi;
    private GuiDialogCharacterBase? charDlg;
    private long tickListener;
    private int woundsLineOffset;
    private List<string> woundsLines = [];
    private float woundsLastScrollbarValue = -1f;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        tickListener = api.Event.RegisterGameTickListener(OnClientTick, 200);
    }

    public override void Dispose()
    {
        if (capi is not null)
        {
            capi.Event.UnregisterGameTickListener(tickListener);
        }

        DetachFromDialog();
    }

    private void OnClientTick(float dt)
    {
        if (capi is null)
        {
            return;
        }

        if (charDlg is not null)
        {
            if (!IsDialogStillLoaded(charDlg))
            {
                DetachFromDialog();
            }
            else
            {
                SyncWoundsFromScrollbar();
                return;
            }
        }

        for (int i = 0; i < capi.Gui.LoadedGuis.Count; i++)
        {
            if (capi.Gui.LoadedGuis[i] is not GuiDialogCharacterBase found)
            {
                continue;
            }

            charDlg = found;
            charDlg.ComposeExtraGuis += OnComposeExtraGuis;
            charDlg.OnClosed += OnCharDialogClosed;
            return;
        }
    }

    private bool IsDialogStillLoaded(GuiDialogCharacterBase dlg)
    {
        if (capi is null)
        {
            return false;
        }

        for (int i = 0; i < capi.Gui.LoadedGuis.Count; i++)
        {
            if (ReferenceEquals(capi.Gui.LoadedGuis[i], dlg))
            {
                return true;
            }
        }

        return false;
    }

    private void OnCharDialogClosed()
    {
        DetachFromDialog();
    }

    private void DetachFromDialog()
    {
        if (charDlg is not null)
        {
            charDlg.ComposeExtraGuis -= OnComposeExtraGuis;
            charDlg.OnClosed -= OnCharDialogClosed;
        }

        charDlg = null;
    }

    private void OnComposeExtraGuis()
    {
        if (capi is null || charDlg is null)
        {
            return;
        }

        var composers = charDlg.Composers;
        if (composers["playercharacter"] is null)
        {
            return;
        }

        ElementBounds left = composers["playercharacter"]!.Bounds;
        ElementBounds? env = composers["environment"]?.Bounds;

        CairoFont bodyFont = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);
        woundsLines = BuildWoundsLines(capi);
        woundsLineOffset = 0;

        const int clipW = 430;
        const int clipH = WoundsClipHeight;
        const int yUnderTitle = 28;

        ElementBounds textBounds = ElementBounds.Fixed(0, yUnderTitle, clipW, clipH);
        ElementBounds scrollbarBounds = ElementStdBounds.VerticalScrollbar(textBounds);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        textBounds = textBounds.WithParent(bgBounds);
        scrollbarBounds = scrollbarBounds.WithParent(bgBounds);
        _ = bgBounds.WithChildren(textBounds, scrollbarBounds);

        double offsetY = env is not null
            ? (env.renderY - left.renderY + env.OuterHeight) / RuntimeEnv.GUIScale + 12
            : (left.OuterHeight / RuntimeEnv.GUIScale) + 8;

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.None)
            .WithFixedPosition(left.renderX / RuntimeEnv.GUIScale, left.renderY / RuntimeEnv.GUIScale + offsetY);

        const string dynamicTextKey = "deathwoundstext";
        const string scrollbarKey = "deathwounds-sb";
        var compo = capi.Gui
            .CreateCompo(ComposerKey, dialogBounds)
            .AddShadedDialogBG(bgBounds, true, 0, 0.5f)
            .AddDialogTitleBar(Lang.Get("deathwounds:gui-title-wounds"), () => charDlg?.TryClose())
            .BeginChildElements(bgBounds)
            .AddDynamicText(ComposeVisibleWoundsLines(), bodyFont, textBounds, dynamicTextKey)
            .AddVerticalScrollbar(OnWoundsScroll, scrollbarBounds, scrollbarKey)
            .EndChildElements()
            .Compose();

        if (compo.GetScrollbar(scrollbarKey) is { } sc)
        {
            sc.SetHeights(WoundsVisibleLineCount, Math.Max(woundsLines.Count, WoundsVisibleLineCount));
            woundsLastScrollbarValue = sc.CurrentYPosition * sc.ScrollConversionFactor;
        }

        composers[ComposerKey] = compo;
    }

    private void OnWoundsScroll(float newValue)
    {
        GuiComposer? composer = charDlg?.Composers?[ComposerKey];
        if (composer is null)
        {
            return;
        }

        int maxOffset = Math.Max(0, woundsLines.Count - WoundsVisibleLineCount);
        float raw = newValue;
        if (raw <= 1.001f && maxOffset > 1)
        {
            raw *= maxOffset;
        }

        woundsLineOffset = (int)Math.Clamp(MathF.Round(raw), 0, maxOffset);
        UpdateWoundsText(composer);
    }

    private void SyncWoundsFromScrollbar()
    {
        GuiComposer? composer = charDlg?.Composers?[ComposerKey];
        if (composer is null)
        {
            return;
        }

        if (composer.GetScrollbar("deathwounds-sb") is not { } sc)
        {
            return;
        }

        float raw = sc.CurrentYPosition * sc.ScrollConversionFactor;
        if (MathF.Abs(raw - woundsLastScrollbarValue) < 0.001f)
        {
            return;
        }

        woundsLastScrollbarValue = raw;
        int maxOffset = Math.Max(0, woundsLines.Count - WoundsVisibleLineCount);
        if (raw <= 1.001f && maxOffset > 1)
        {
            raw *= maxOffset;
        }

        int newOffset = (int)Math.Clamp(MathF.Round(raw), 0, maxOffset);
        if (newOffset == woundsLineOffset)
        {
            return;
        }

        woundsLineOffset = newOffset;
        UpdateWoundsText(composer);
    }

    private void UpdateWoundsText(GuiComposer composer)
    {
        if (composer.GetDynamicText("deathwoundstext") is { } txt)
        {
            txt.SetNewText(ComposeVisibleWoundsLines(), false, true, false);
        }
    }

    private string ComposeVisibleWoundsLines()
    {
        if (woundsLines.Count == 0)
        {
            return Lang.Get("deathwounds:gui-wounds-empty");
        }

        int start = Math.Clamp(woundsLineOffset, 0, Math.Max(0, woundsLines.Count - 1));
        int count = Math.Min(WoundsVisibleLineCount, woundsLines.Count - start);
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }

            sb.Append(woundsLines[start + i]);
        }

        return sb.ToString();
    }

    private static List<string> BuildWoundsLines(ICoreClientAPI capi)
    {
        SyncedTreeAttribute? attrs = capi.World.Player?.Entity.WatchedAttributes;
        var lines = new List<string>(24);

        int moveT = attrs?.GetInt(DeathWoundDebuffSystem.AttrMoveTiers, 0) ?? 0;
        int torsoHungerT = attrs?.GetInt(DeathWoundDebuffSystem.AttrTorsoHungerTiers, 0) ?? 0;

        if (moveT == 0 && torsoHungerT == 0)
        {
            lines.Add(Lang.Get("deathwounds:gui-wounds-empty"));
            return lines;
        }

        AddWrappedLine(lines, Lang.Get("deathwounds:gui-subtitle-wounds"));
        lines.Add("");

        if (moveT > 0)
        {
            int pct = 25 * moveT;
            string tiers = WoundTierRangeText(moveT);
            AddWrappedLine(lines, "• " + Lang.Get("deathwounds:debuff-wound-move-title") + Lang.Get("deathwounds:debuff-wound-part-suffix-leg"));
            AddWrappedLine(lines, Lang.Get("deathwounds:debuff-wound-move-desc"));
            AddWrappedLine(lines, Lang.Get("deathwounds:debuff-wound-statline-move", pct, tiers));
            lines.Add("");
        }

        if (torsoHungerT > 0)
        {
            int pct = 25 * torsoHungerT;
            string tiers = WoundTierRangeText(torsoHungerT);
            AddWrappedLine(lines, "• " + Lang.Get("deathwounds:debuff-wound-hunger-title") + Lang.Get("deathwounds:debuff-wound-part-suffix-torso"));
            AddWrappedLine(lines, Lang.Get("deathwounds:debuff-wound-hunger-desc"));
            AddWrappedLine(lines, Lang.Get("deathwounds:debuff-wound-statline-hunger", pct, tiers));
        }

        return lines;
    }

    private static void AddWrappedLine(List<string> rows, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            rows.Add("");
            return;
        }

        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length > ApproxCharsPerRow)
            {
                rows.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
            else
            {
                current.Append(' ');
                current.Append(word);
            }
        }

        if (current.Length > 0)
        {
            rows.Add(current.ToString());
        }
    }

    private static string WoundTierRangeText(int tierCount)
    {
        return tierCount switch
        {
            1 => "I",
            2 => "I–II",
            3 => "I–II–III",
            _ => ""
        };
    }
}
