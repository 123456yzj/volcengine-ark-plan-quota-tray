using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ArkLeft;

namespace ArkLeft.Tests
{
    internal static partial class QuotaTests
    {
        // ---- v0.24 UX029: theme (accent presets + dark mode) ----

        private static void ThemeUX029Cases()
        {
            // ---- catalog: default accent 0 is the historical blue; names are
            // stable and normalization clamps out-of-range indices ----
            Check("ux029.accentCount", ThemeCatalog.Count, 6);
            Check("ux029.accent0", ThemeCatalog.Accent(0).ToArgb(),
                Color.FromArgb(58, 131, 247).ToArgb());
            Check("ux029.accentName0", ThemeCatalog.Name(0), "默认蓝");
            Check("ux029.normalizeNeg", ThemeCatalog.Normalize(-1), 0);
            Check("ux029.normalizeBig", ThemeCatalog.Normalize(99), 0);
            Check("ux029.normalizeValid", ThemeCatalog.Normalize(3), 3);

            // ---- default (accent 0, light) palette is the frozen historical
            // set, so every existing visual constant is unchanged ----
            ThemePalette def = ThemePaletteFactory.Build(0, false);
            Check("ux029.defNavy", def.Navy.ToArgb(), Color.FromArgb(36, 65, 92).ToArgb());
            Check("ux029.defPrimary", def.Primary.ToArgb(), Color.FromArgb(58, 131, 247).ToArgb());
            Check("ux029.defCanvas", def.Canvas.ToArgb(), Color.FromArgb(248, 251, 255).ToArgb());
            Check("ux029.defSurface", def.Surface.ToArgb(), Color.White.ToArgb());
            Check("ux029.defWater", def.Water.ToArgb(), Color.FromArgb(90, 154, 248).ToArgb());
            Check("ux029.defCircleNumber", def.CircleNumber.ToArgb(),
                Color.FromArgb(24, 62, 99).ToArgb());

            // ---- a non-default accent differs in the derived accent colors but
            // keeps the neutral surfaces; dark mode flips both surface and ink ----
            ThemePalette alt = ThemePaletteFactory.Build(2, false);
            Check("ux029.altPrimaryDiffers", alt.Primary.ToArgb() != def.Primary.ToArgb(), true);
            Check("ux029.altSurfaceSame", alt.Surface.ToArgb(), def.Surface.ToArgb());
            Check("ux029.altWaterDiffers", alt.Water.ToArgb() != def.Water.ToArgb(), true);
            ThemePalette dark = ThemePaletteFactory.Build(0, true);
            Check("ux029.darkSurface", dark.Surface.ToArgb(), Color.FromArgb(40, 44, 52).ToArgb());
            Check("ux029.darkCanvas", dark.Canvas.ToArgb(), Color.FromArgb(28, 31, 36).ToArgb());
            Check("ux029.darkInkLight", dark.Navy.R > 200 && dark.Navy.G > 200, true);
            Check("ux029.darkOnAccent", dark.OnAccent.ToArgb(), Color.White.ToArgb());

            // ---- UiStyle.Apply swaps the live palette; same value is a no-op ----
            Color basePrimary = UiStyle.Primary;
            Check("ux029.applyChange", UiStyle.Apply(5, true), true);
            Check("ux029.livePrimary", UiStyle.Primary.ToArgb(),
                ThemeCatalog.Accent(5).ToArgb());
            Check("ux029.liveSurface", UiStyle.Surface.ToArgb(),
                Color.FromArgb(40, 44, 52).ToArgb());
            Check("ux029.applySame", UiStyle.Apply(5, true), false);
            Check("ux029.applyBack", UiStyle.Apply(0, false), true);
            Check("ux029.restoredPrimary", UiStyle.Primary.ToArgb(), basePrimary.ToArgb());
            Check("ux029.restoredSurface", UiStyle.Surface.ToArgb(), Color.White.ToArgb());
            Check("ux029.applyClamps", UiStyle.Apply(99, false), false); // already default

            // ---- FloatingQuotaForm: restore persisted theme, no write on start
            // and no query ----
            FloatingPreferences themed = new FloatingPreferences();
            themed.Version = FloatingPreferencesStore.FormatVersion;
            themed.AccentIndex = 3;
            themed.DarkMode = true;
            int saves = 0;
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return themed; },
                delegate(FloatingPreferences p) { saves++; return true; }))
            {
                Check("ux029.formAccent", f.AccentIndex, 3);
                Check("ux029.formDark", f.DarkMode, true);
                Check("ux029.formNoWriteOnStart", saves, 0);
                Check("ux029.formLiveApplied", UiStyle.Primary.ToArgb(),
                    ThemeCatalog.Accent(3).ToArgb());
            }

            // Default (null prefs) -> accent 0 / light.
            using (FloatingQuotaForm f = new FloatingQuotaForm(
                delegate { return (FloatingSettings)null; },
                delegate(FloatingSettings s) { return true; },
                delegate { return (FloatingPreferences)null; },
                delegate(FloatingPreferences p) { return true; }))
            {
                Check("ux029.formDefaultAccent", f.AccentIndex, 0);
                Check("ux029.formDefaultDark", f.DarkMode, false);
            }
            UiStyle.Apply(0, false);

            // ---- settings dialog: preview commits on 保存, reverts on cancel;
            // both carry the accent + dark through the injected callbacks ----
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(
                FloatingSelection.Build(SyntheticSample.BuildLarge()));
            List<string> previews = new List<string>();
            int themeSaves = 0;
            int savedAccent = -1; bool savedDark = false;
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates, null,
                delegate(FloatingSettings s) { return true; },
                1.0, new Rectangle(0, 0, 1920, 1040),
                delegate(int a, bool d) { previews.Add(a + ":" + d); },
                delegate(int a, bool d) { themeSaves++; savedAccent = a; savedDark = d; return true; },
                1, false))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                Check("ux029.dlgAccentPreselect", dlg.AccentComboForTest.SelectedIndex, 1);
                Check("ux029.dlgDarkPreselect", dlg.DarkCheckForTest.Checked, false);
                // Live preview on selection / toggle.
                dlg.SelectAccentForTest(4);
                Check("ux029.dlgPreviewAccent", previews.Count >= 1 && previews[previews.Count - 1] == "4:False", true);
                dlg.SetDarkForTest(true);
                Check("ux029.dlgPreviewDark", previews[previews.Count - 1], "4:True");
                Check("ux029.dlgStateAccent", dlg.ThemeAccentForTest, 4);
                Check("ux029.dlgStateDark", dlg.ThemeDarkForTest, true);
                // Save persists both theme and target.
                dlg.SelectForTest(0);
                Check("ux029.dlgSave", dlg.SaveForTest(), true);
                Check("ux029.dlgThemeSaved", themeSaves, 1);
                Check("ux029.dlgSavedAccent", savedAccent, 4);
                Check("ux029.dlgSavedDark", savedDark, true);
            }

            // Cancel: a live preview must be reverted to the initial values and
            // nothing persisted.
            List<string> previews2 = new List<string>();
            int themeSaves2 = 0;
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates, null,
                delegate(FloatingSettings s) { return true; },
                1.0, new Rectangle(0, 0, 1920, 1040),
                delegate(int a, bool d) { previews2.Add(a + ":" + d); },
                delegate(int a, bool d) { themeSaves2++; return true; },
                2, true))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                dlg.SelectAccentForTest(0);
                dlg.SetDarkForTest(false);
                dlg.DialogResult = DialogResult.Cancel;
                dlg.Close();
                Check("ux029.cancelNoSave", themeSaves2, 0);
                Check("ux029.cancelReverts", previews2.Count >= 1
                    && previews2[previews2.Count - 1] == "2:True", true);
            }

            // Save failure keeps the dialog open with an inline error and
            // reverts the preview; the target save is not the failure.
            List<string> previews3 = new List<string>();
            using (FloatingSettingsForm dlg = new FloatingSettingsForm(candidates, null,
                delegate(FloatingSettings s) { return true; },
                1.0, new Rectangle(0, 0, 1920, 1040),
                delegate(int a, bool d) { previews3.Add(a + ":" + d); },
                delegate(int a, bool d) { return false; },
                0, false))
            {
                IntPtr h = dlg.Handle; GC.KeepAlive(h);
                dlg.SelectAccentForTest(3);
                dlg.SelectForTest(0);
                Check("ux029.failSaveRefused", dlg.SaveForTest(), false);
                Check("ux029.failStaysOpen", dlg.DialogResult != DialogResult.OK, true);
                Check("ux029.failInline", dlg.ErrorForTest.Contains("主题设置保存失败"), true);
                Check("ux029.failReverts", previews3.Count >= 1
                    && previews3[previews3.Count - 1] == "0:False", true);
            }

            // ---- production path: tray menu -> settings modal changes the
            // theme; zero query, persisted, and the circle/UI repaints ----
            int queries = 0;
            int prefsSaved = 0;
            int lastAccent = -1; bool lastDark = false;
            using (TrayApp app = new TrayApp(delegate(IProgress<QueryProgress> progress,
                System.Threading.CancellationToken token)
                { queries++; return System.Threading.Tasks.Task.FromResult(new QueryOutcome()); },
                delegate(FloatingPreferences p)
                { prefsSaved++; lastAccent = p.AccentIndex; lastDark = p.DarkMode; return true; }))
            {
                app.HideDetailsForTest();
                app.ApplyViewForTest(SyntheticSample.BuildLarge());
                Check("ux029.menuZeroBefore", queries, 0);
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 200;
                t.Tick += delegate
                {
                    t.Stop();
                    FloatingSettingsForm dlg = FindOpenSettings();
                    if (dlg == null) return;
                    dlg.SelectAccentForTest(2);
                    dlg.SetDarkForTest(true);
                    dlg.SelectForTest(0);
                    dlg.SaveForTest();
                };
                t.Start();
                app.OpenSettingsForTest();
                t.Dispose();
                System.Windows.Forms.Application.DoEvents();
                Check("ux029.prodThemePersisted", prefsSaved >= 1 && lastAccent == 2 && lastDark, true);
                Check("ux029.prodLiveApplied", UiStyle.Primary.ToArgb(),
                    ThemeCatalog.Accent(2).ToArgb());
                Check("ux029.prodZeroQuery", queries, 0);
                Check("ux029.prodDialogClosed", app.SettingsOpenForTest, false);
            }
            UiStyle.Apply(0, false);
        }
    }
}
