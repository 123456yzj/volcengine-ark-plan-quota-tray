using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed partial class TrayApp
    {
        // Offline smoke for the floating circle + settings dialog. Synthetic data
        // only: injected null/no-op preference callbacks mean no real state file
        // is read or written, and no query / network is ever triggered.
        private static int SmokeFloating()
        {
            int bad = 0;
            try
            {
                using (FloatingQuotaForm floating = new FloatingQuotaForm(
                    delegate { return (FloatingSettings)null; },
                    delegate(FloatingSettings s) { return true; },
                    delegate { return (FloatingPreferences)null; },
                    delegate(FloatingPreferences p) { return true; }))
                {
                    IntPtr h = floating.Handle; GC.KeepAlive(h);

                    PanelModel model = new PanelModel();
                    model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
                    PanelView view = model.OnUsageResult(
                        SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null);
                    floating.ApplyModelView(view);

                    Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                    floating.ShowCircleAtForTest(wa, 1.0);
                    Application.DoEvents();
                    if (!floating.CircleVisible) { bad++; Console.Error.WriteLine("floating: not visible"); }
                    FloatingCircleControl circle = floating.CircleForTest;
                    if (circle.PercentTextForTest != "10%")
                    {
                        bad++;
                        Console.Error.WriteLine("floating.percent: got " + circle.PercentTextForTest
                            + " want 10%");
                    }
                    if (!circle.PercentKnownForTest)
                    {
                        bad++;
                        Console.Error.WriteLine("floating.percentKnown mismatch");
                    }
                    if (!circle.WaveRunningForTest)
                    {
                        bad++;
                        Console.Error.WriteLine("floating.wave: expected running");
                    }
                    if (!circle.TooltipForTest.Contains("5 小时"))
                    {
                        bad++;
                        Console.Error.WriteLine("floating.tooltip: " + circle.TooltipForTest);
                    }
                    if (circle.AmountTextForTest != "100 AFP")
                    {
                        bad++;
                        Console.Error.WriteLine("floating.amount: " + circle.AmountTextForTest);
                    }
                    if (!CircleInside(floating.CircleForTest.Bounds, wa))
                    {
                        bad++;
                        Console.Error.WriteLine("floating: bounds outside work area");
                    }
                    TrySaveControlPreview(circle, "preview-floating.png");

                    // v0.9 UX017: reduce motion stops the wave timer and
                    // repaints a static surface through the production
                    // handler; display values stay; toggling back resumes.
                    // Injected no-op preference save, zero query.
                    floating.SetReduceMotion(true);
                    Application.DoEvents();
                    if (circle.WaveRunningForTest)
                    {
                        bad++;
                        Console.Error.WriteLine("floating.reduceMotion: wave still running");
                    }
                    if (circle.PercentTextForTest != "10%")
                    {
                        bad++;
                        Console.Error.WriteLine("floating.reduceMotion: percent changed");
                    }
                    using (Bitmap staticWave = SnapshotControl(circle))
                    {
                        TrySaveBitmap(staticWave, "preview-floating-static.png",
                            circle.Region);
                    }
                    floating.SetReduceMotion(false);
                    Application.DoEvents();
                    if (!circle.WaveRunningForTest)
                    {
                        bad++;
                        Console.Error.WriteLine("floating.reduceMotion: wave not resumed");
                    }

                    // 0 / 100 / unknown render endpoints. Verify non-text water /
                    // no-water pixels rather than trusting the text alone.
                    bad += SmokeCircleEndpoints(floating);
                    bad += SmokeCircleScale();
                    ContextMenuStrip menu = circle.ContextMenuStrip;
                    UiStyle.StyleMenu(menu);
                    menu.Show(new Point(wa.Left + 24, wa.Top + 24));
                    Application.DoEvents();
                    TrySaveControlPreview(menu, "preview-rightmenu.png");
                    ToolStripMenuItem settingsItem = (ToolStripMenuItem)menu.Items[0];
                    settingsItem.ShowDropDown();
                    Application.DoEvents();
                    TrySaveControlPreview(settingsItem.DropDown, "preview-menu-settings.png");
                    ToolStripMenuItem contentItem = (ToolStripMenuItem)settingsItem.DropDownItems[0];
                    contentItem.ShowDropDown();
                    Application.DoEvents();
                    TrySaveControlPreview(contentItem.DropDown, "preview-menu-content.png");
                    menu.Close();

                    // Settings dialog (production Drawing) with an injected
                    // no-op save; never touches the real preference file.
                    List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(
                        FloatingSelection.Build(SyntheticSample.BuildLarge()));
                    using (FloatingSettingsForm dlg = new FloatingSettingsForm(
                        candidates, null, delegate(FloatingSettings s) { return true; }))
                    {
                        if (dlg.Controls.Count == 0) { bad++; Console.Error.WriteLine("settings: no controls"); }
                        dlg.Show();
                        Application.DoEvents();
                        TrySaveControlPreview(dlg, "preview-settings.png");
                        dlg.Close();
                    }

                    // Lifecycle: hiding stops the wave and disposes cleanly.
                    floating.HideCircleForTest();
                    Application.DoEvents();
                    if (floating.CircleVisible) { bad++; Console.Error.WriteLine("floating: hide failed"); }
                    if (circle.WaveRunningForTest) { bad++; Console.Error.WriteLine("floating: wave not stopped on hide"); }
                }
            }
            catch (Exception ex)
            {
                bad++;
                Console.Error.WriteLine("floating smoke exception: " + ex.Message);
            }
            return bad;
        }

        // 0 empty / 100 full / unknown neutral: verify pixel regions that cannot
        // be satisfied by the text alone.
        private static int SmokeCircleEndpoints(FloatingQuotaForm floating)
        {
            int bad = 0;
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                FloatingCircleControl circle = floating.CircleForTest;
                circle.ShowAt(wa, 1.0);

                foreach (int percent in new int[] { 50, 97 })
                {
                    circle.SetDisplay(EndpointDisplay(percent, true, percent + "%"));
                    Application.DoEvents();
                    using (Bitmap middle = SnapshotControl(circle))
                    {
                        if (!IsWater(middle.GetPixel(middle.Width / 4, (int)(middle.Height * 0.85)))) bad++;
                        TrySaveBitmap(middle, "preview-floating-" + percent + ".png", circle.Region);
                    }
                }

                circle.SetDisplay(EndpointDisplay(100, true, "100%"));
                Application.DoEvents();
                using (Bitmap full = SnapshotControl(circle))
                {
                    // Near the top, well left of the centered text, the water must
                    // reach the fill: no dark-background "notch" remains.
                    if (!IsWater(full.GetPixel(full.Width / 4, (int)(full.Height * 0.14)))) bad++;
                    if (!IsWater(full.GetPixel(full.Width / 4, (int)(full.Height * 0.85)))) bad++;
                    TrySaveBitmap(full, "preview-floating-100.png", circle.Region);
                }

                circle.SetDisplay(EndpointDisplay(0, true, "0%"));
                Application.DoEvents();
                using (Bitmap empty = SnapshotControl(circle))
                {
                    // No water anywhere away from the text.
                    if (IsWater(empty.GetPixel(empty.Width / 4, (int)(empty.Height * 0.30)))) bad++;
                    if (IsWater(empty.GetPixel(empty.Width / 4, (int)(empty.Height * 0.80)))) bad++;
                    TrySaveBitmap(empty, "preview-floating-0.png", circle.Region);
                }

                circle.SetDisplay(EndpointDisplay(0, false, "剩余未知"));
                Application.DoEvents();
                using (Bitmap unknown = SnapshotControl(circle))
                {
                    if (IsWater(unknown.GetPixel(unknown.Width / 4, (int)(unknown.Height * 0.50)))) bad++;
                    TrySaveBitmap(unknown, "preview-floating-unknown.png", circle.Region);
                }
                if (bad > 0) Console.Error.WriteLine("floating endpoints: " + bad);
            }
            catch (Exception ex)
            {
                bad++;
                Console.Error.WriteLine("floating endpoints exception: " + ex.Message);
            }
            return bad;
        }

        // Scale == 2 must enlarge BOTH the bounds and the fonts (rendered text
        // must grow), and 1% must paint only a sliver of water.
        private static int SmokeCircleScale()
        {
            int bad = 0;
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                using (FloatingQuotaForm a = new FloatingQuotaForm(
                    delegate { return (FloatingSettings)null; },
                    delegate(FloatingSettings s) { return true; }))
                {
                    IntPtr h1 = a.Handle; GC.KeepAlive(h1);
                    a.ShowCircleAtForTest(new Rectangle(0, 0, 2000, 2000), 1.0);
                    a.CircleForTest.SetDisplay(EndpointDisplay(1, true, "1%"));
                    Application.DoEvents();
                    Rectangle b1 = a.CircleForTest.Bounds;
                    using (Bitmap at1 = SnapshotControl(a.CircleForTest))
                    {
                        // 0% reference: SAME control, size and focus state, so
                        // the ring stroke is identical in both snapshots and any
                        // bottom-band delta is water, never ring color coupling.
                        a.CircleForTest.SetDisplay(EndpointDisplay(0, true, "0%"));
                        Application.DoEvents();
                        using (Bitmap at0 = SnapshotControl(a.CircleForTest))
                        {
                            // Sample near the vertical center line so the pixel
                            // is inside the ellipse; x = width/4 is outside it
                            // near the bottom (navy background instead).
                            int cx1 = at1.Width / 2;
                            int hh = at1.Height;
                            Color mid1 = at1.GetPixel(cx1, (int)(hh * 0.55));
                            Color bot1 = at1.GetPixel(cx1, hh - 2);
                            Color bot0 = at0.GetPixel(cx1, hh - 2);
                            if (IsWater(mid1))
                            {
                                bad++;
                                Console.Error.WriteLine("floating scale 1pct middle is water: " + mid1);
                            }
                            // 1% water is a ~1px sliver that only exists under
                            // the ring stroke, so the old "bottom G > top G"
                            // check compared ring edge pixels and broke once the
                            // focused/hover ring (bright teal) covered both
                            // sample points. Compare the SAME bottom pixel
                            // against the 0% reference instead: the ring is
                            // identical in both snapshots, so a red-channel
                            // drop from white identifies the sky-blue water.
                            if (!(bot0.R > bot1.R + 20))
                            {
                                bad++;
                                Console.Error.WriteLine("floating scale 1pct no water vs 0pct reference: 1pct="
                                    + bot1 + " 0pct=" + bot0);
                            }
                        }
                    }

                    using (FloatingQuotaForm b = new FloatingQuotaForm(
                        delegate { return (FloatingSettings)null; },
                        delegate(FloatingSettings s) { return true; }))
                    {
                        IntPtr h2 = b.Handle; GC.KeepAlive(h2);
                        b.ShowCircleAtForTest(new Rectangle(0, 0, 2000, 2000), 2.0);
                        b.CircleForTest.SetDisplay(EndpointDisplay(75, true, "75%"));
                        Application.DoEvents();
                        Rectangle b2 = b.CircleForTest.Bounds;
                        if (b2.Width != b1.Width * 2)
                        {
                            bad++;
                            Console.Error.WriteLine("floating scale bounds: " + b1.Width + " -> " + b2.Width);
                        }
                        // A scale-2 filled circle must have water across the middle.
                        using (Bitmap at2 = SnapshotControl(b.CircleForTest))
                        {
                            if (!IsWater(at2.GetPixel(at2.Width / 4, (int)(at2.Height * 0.45)))) bad++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                bad++;
                Console.Error.WriteLine("floating scale exception: " + ex.Message);
            }
            return bad;
        }

        private static FloatingDisplay EndpointDisplay(double percent, bool known, string text)
        {
            FloatingDisplay d = new FloatingDisplay();
            d.HasData = true;
            d.PercentKnown = known;
            d.Percent = percent;
            d.PercentText = text;
            d.AmountKnown = known;
            d.RemainingAmount = percent * 100;
            d.Tooltip = "Agent Plan · 5 小时";
            return d;
        }

        private static Bitmap SnapshotControl(Control c)
        {
            Bitmap bmp = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height));
            try { c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height)); } catch (Exception) { }
            return bmp;
        }

        // A water pixel is clearly blue-cyan: much higher blue AND green than
        // red. This must NOT match the dark navy background (16,42,74), whose
        // green is only slightly above red.
        private static bool IsWater(Color p)
        {
            return p.B > p.R + 40 && p.G > p.R + 40 && p.G > 80;
        }

        private static void TrySaveBitmap(Bitmap bmp, string file)
        {
            TrySaveBitmap(bmp, file, null);
        }

        // Saves a snapshot. When a Region is supplied the saved PNG is clipped
        // to it (transparent corners for the circular window); pixel assertions
        // keep using the raw in-memory bitmap.
        private static void TrySaveBitmap(Bitmap bmp, string file, Region clip)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                if (clip == null)
                {
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    return;
                }
                using (Bitmap outBmp = new Bitmap(bmp.Width, bmp.Height))
                using (Graphics g = Graphics.FromImage(outBmp))
                {
                    g.Clear(Color.Transparent);
                    using (Region r = clip.Clone())
                    {
                        r.Intersect(g.VisibleClipBounds);
                        g.SetClip(r, System.Drawing.Drawing2D.CombineMode.Replace);
                        g.DrawImageUnscaled(bmp, 0, 0);
                    }
                    outBmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception) { }
        }

        private static bool CircleInside(Rectangle b, Rectangle wa)
        {
            return b.Left >= wa.Left && b.Top >= wa.Top
                && b.Right <= wa.Right && b.Bottom <= wa.Bottom;
        }

        private static void TrySaveControlPreview(Control c, string file)
        {
            try
            {
                if (c.Width < 1 || c.Height < 1) return;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                using (Bitmap bmp = new Bitmap(c.Width, c.Height))
                {
                    // DrawToBitmap ignores a window Region, so clip to the
                    // elliptical region here to produce a transparent-corner
                    // circular PNG; non-circular controls (dialog) render as-is.
                    if (c.Region != null)
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        using (Region clip = c.Region.Clone())
                        {
                            g.Clear(Color.Transparent);
                            clip.Intersect(g.VisibleClipBounds);
                            g.SetClip(clip, System.Drawing.Drawing2D.CombineMode.Replace);
                            using (Bitmap raw = new Bitmap(c.Width, c.Height))
                            {
                                c.DrawToBitmap(raw, new Rectangle(0, 0, c.Width, c.Height));
                                g.DrawImageUnscaled(raw, 0, 0);
                            }
                        }
                    }
                    else
                    {
                        c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                    }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception) { }
        }
    }
}
