using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    // Parses the JSON returned by `arkcli usage plan --format json` into a
    // QuotaSnapshot. Pure logic: no CLI, no UI, no disk, no credentials kept.
    // Never fakes missing values as 0; distinguishes error / unknown / real 0.
    public static class QuotaParser
    {
        public static QuotaSnapshot Parse(string json)
        {
            QuotaSnapshot snap = new QuotaSnapshot();
            snap.FetchedAt = DateTime.Now;

            if (string.IsNullOrEmpty(json))
            {
                return Fail(snap, QuotaStatus.FormatError, "未收到任何返回数据。");
            }

            object rootRaw;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                rootRaw = ser.DeserializeObject(json);
            }
            catch (Exception)
            {
                return Fail(snap, QuotaStatus.FormatError, "返回数据不是有效的 JSON。");
            }

            Dictionary<string, object> root = rootRaw as Dictionary<string, object>;
            if (root == null)
            {
                return Fail(snap, QuotaStatus.FormatError, "返回数据顶层结构不正确。");
            }

            // Some deployments may echo logged_in; if explicitly false, treat as not logged in.
            bool loggedIn;
            if (TryGetBool(root, "logged_in", out loggedIn) && !loggedIn)
            {
                return Fail(snap, QuotaStatus.NotLoggedIn, "当前未登录方舟账号。");
            }

            object itemsRaw;
            if (!root.TryGetValue("items", out itemsRaw) || itemsRaw == null)
            {
                return Fail(snap, QuotaStatus.FormatError, "返回数据缺少 items 字段。");
            }

            object[] items = itemsRaw as object[];
            if (items == null)
            {
                return Fail(snap, QuotaStatus.FormatError, "items 字段类型不正确。");
            }

            bool anyError = false;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                {
                    // A null item is structurally broken, NOT "no subscription".
                    ProductQuota bad = new ProductQuota();
                    bad.Product = null;
                    bad.DisplayName = "未知产品";
                    bad.Malformed = true;
                    bad.Error = "该产品条目为空。";
                    snap.Products.Add(bad);
                    anyError = true;
                    continue;
                }

                Dictionary<string, object> item = items[i] as Dictionary<string, object>;
                if (item == null)
                {
                    // A structurally broken item: surface as error, never as "unsubscribed".
                    ProductQuota bad = new ProductQuota();
                    bad.Product = null;
                    bad.DisplayName = "未知产品";
                    bad.Malformed = true;
                    bad.Error = "该产品条目结构不正确。";
                    snap.Products.Add(bad);
                    anyError = true;
                    continue;
                }

                ProductQuota pq = ParseItem(item);
                if (pq == null) continue; // hidden: known not subscribed, no error

                if (pq.Error != null || pq.Malformed || pq.PeriodErrorPresent) anyError = true;
                snap.Products.Add(pq);
            }

            if (snap.Products.Count == 0)
            {
                snap.Status = QuotaStatus.NoSubscription;
                snap.Message = "当前身份下未发现已订阅的方舟套餐。";
                return snap;
            }

            if (anyError)
            {
                snap.Status = QuotaStatus.PartialError;
                snap.Message = "部分额度数据获取失败，已展示可用部分。";
            }
            else
            {
                snap.Status = QuotaStatus.Ok;
                snap.Message = "已获取当前订阅额度。";
            }

            return snap;
        }

        private static ProductQuota ParseItem(Dictionary<string, object> item)
        {
            ProductQuota pq = new ProductQuota();

            string product;
            pq.Product = TryGetString(item, "product", out product) && !string.IsNullOrEmpty(product)
                ? product : null;
            pq.DisplayName = DisplayNames.Product(pq.Product);

            string edition; if (TryGetString(item, "edition", out edition)) pq.Edition = edition;
            string tier; if (TryGetString(item, "tier", out tier)) pq.Tier = tier;

            string itemError;
            bool hasError = TryGetString(item, "error", out itemError)
                            && itemError.Trim().Length > 0;

            // Item-level server data update time (Coding Plan puts updated_at here).
            object updatedRaw;
            if (item.TryGetValue("updated_at", out updatedRaw) && updatedRaw != null)
            {
                DateTime dt;
                if (TryParseUpdated(updatedRaw, out dt)) { pq.HasUpdated = true; pq.UpdatedLocal = dt; }
            }

            bool subscribed;
            bool hasSubscribedBool = TryGetBool(item, "subscribed", out subscribed);

            // Malformed when `subscribed` is missing or not a boolean.
            pq.SubscribedKnown = hasSubscribedBool;
            pq.Subscribed = subscribed;

            if (hasError)
            {
                // Never surface raw upstream error text (could contain tokens).
                pq.Error = "该产品查询失败，请稍后重试。";
            }
            else if (!hasSubscribedBool)
            {
                pq.Malformed = true;
                pq.Error = "订阅状态字段缺失或格式不正确。";
            }

            // Parse periods (present even for error items when available).
            object periodsRaw;
            if (item.TryGetValue("periods", out periodsRaw) && periodsRaw != null)
            {
                object[] periods = periodsRaw as object[];
                if (periods == null)
                {
                    if (pq.Error == null) pq.Error = "周期数据格式不正确。";
                    pq.Malformed = true;
                }
                else
                {
                    for (int i = 0; i < periods.Length; i++)
                    {
                        if (periods[i] == null)
                        {
                            // A null bucket is an error, not a silently skipped entry.
                            PeriodQuota badP = new PeriodQuota();
                            badP.LabelDisplay = "未知周期";
                            badP.Error = "该周期条目为空。";
                            pq.Periods.Add(badP);
                            pq.PeriodErrorPresent = true;
                            continue;
                        }
                        Dictionary<string, object> pd = periods[i] as Dictionary<string, object>;
                        if (pd == null)
                        {
                            PeriodQuota badP = new PeriodQuota();
                            badP.LabelDisplay = "未知周期";
                            badP.Error = "该周期条目格式不正确。";
                            pq.Periods.Add(badP);
                            pq.PeriodErrorPresent = true;
                            continue;
                        }
                        PeriodQuota parsed = ParsePeriod(pd);
                        if (parsed.Error != null) pq.PeriodErrorPresent = true;
                        pq.Periods.Add(parsed);
                    }
                }
            }
            else if (pq.Error == null && hasSubscribedBool && subscribed)
            {
                pq.Error = "周期数据缺失。";
                pq.Malformed = true;
            }

            // Visibility decision. Errors are always shown; a usable (even partial)
            // subscription is shown; only a clean "not subscribed" is hidden.
            if (pq.Error != null || pq.Malformed) return pq;
            if (pq.SubscribedKnown && pq.Subscribed) return pq;
            return null;
        }

        private static PeriodQuota ParsePeriod(Dictionary<string, object> pd)
        {
            PeriodQuota p = new PeriodQuota();

            string label;
            if (TryGetString(pd, "label", out label)) p.Label = label;
            p.LabelDisplay = DisplayNames.Period(p.Label);

            string perr;
            if (TryGetString(pd, "error", out perr) && perr.Trim().Length > 0)
            {
                p.Error = "该周期查询失败。"; // fixed safe message, no raw upstream text
                return p; // failed bucket: never interpreted as "unsubscribed"
            }

            double percent = 0;
            bool hasPercent = TryGetNumber(pd, "percent", out percent) && IsFinite(percent);

            double used = 0, total = 0;
            bool usedPresent = TryGetNumber(pd, "used", out used);
            bool totalPresent = TryGetNumber(pd, "total", out total);

            p.UsedKnown = usedPresent && IsFinite(used) && used >= 0;
            if (p.UsedKnown) p.Used = used;

            p.TotalKnown = totalPresent && IsFinite(total) && total >= 0;
            if (p.TotalKnown) p.Total = total;

            // Percentage resolution: percent wins; used/total is fallback.
            if (hasPercent)
            {
                double rawRemaining = 100.0 - percent;
                p.RemainingPercent = Clamp(rawRemaining);
                p.Clamped = rawRemaining < 0.0 || rawRemaining > 100.0;
                p.PercentKnown = true;
                if (p.Clamped)
                {
                    p.UnknownNote = "原始百分比越界，已按 0-100 修正。";
                }
            }
            else if (p.UsedKnown && p.TotalKnown && p.Total > 0.0)
            {
                double rawRemaining = (p.Total - p.Used) / p.Total * 100.0;
                p.RemainingPercent = Clamp(rawRemaining);
                p.Clamped = rawRemaining < 0.0 || rawRemaining > 100.0;
                p.PercentKnown = true;
                if (p.Clamped)
                {
                    p.UnknownNote = "由用量换算的百分比越界，已按 0-100 修正。";
                }
            }
            else
            {
                p.PercentKnown = false;
                if (p.TotalKnown && p.Total == 0.0 && !hasPercent)
                {
                    p.UnknownNote = "总量为 0，额度未知。";
                }
                else
                {
                    p.UnknownNote = "缺少可用的百分比数据。";
                }
            }

            // Remaining absolute amount.
            if (p.UsedKnown && p.TotalKnown && p.Total > 0.0)
            {
                double amt = p.Total - p.Used;
                p.AmountKnown = true;
                p.RemainingAmount = amt > 0.0 ? amt : 0.0;
            }

            // reset_at (server provided)
            string resetAt;
            if (TryGetString(pd, "reset_at", out resetAt))
            {
                DateTime dt;
                if (TryParseTime(resetAt, out dt)) { p.HasReset = true; p.ResetLocal = dt; }
            }

            // updated_at (server data time; may be epoch ms number or string)
            object updatedRaw;
            if (pd.TryGetValue("updated_at", out updatedRaw) && updatedRaw != null)
            {
                DateTime dt;
                if (TryParseUpdated(updatedRaw, out dt)) { p.HasUpdated = true; p.UpdatedLocal = dt; }
            }

            return p;
        }

        // ---- helpers ----

        private static QuotaSnapshot Fail(QuotaSnapshot snap, QuotaStatus status, string message)
        {
            snap.Status = status;
            snap.Message = message;
            return snap;
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        private static double Clamp(double v)
        {
            if (v < 0.0) return 0.0;
            if (v > 100.0) return 100.0;
            return v;
        }

        private static bool TryGetBool(Dictionary<string, object> d, string key, out bool value)
        {
            value = false;
            object raw;
            if (!d.TryGetValue(key, out raw) || raw == null) return false;
            if (raw is bool) { value = (bool)raw; return true; }
            return false;
        }

        private static bool TryGetString(Dictionary<string, object> d, string key, out string value)
        {
            value = null;
            object raw;
            if (!d.TryGetValue(key, out raw) || raw == null) return false;
            string s = raw as string;
            if (s == null) return false;
            value = s;
            return true;
        }

        private static bool TryGetNumber(Dictionary<string, object> d, string key, out double value)
        {
            value = 0;
            object raw;
            if (!d.TryGetValue(key, out raw) || raw == null) return false;
            try
            {
                if (raw is double) { value = (double)raw; return true; }
                if (raw is int) { value = (int)raw; return true; }
                if (raw is long) { value = (long)raw; return true; }
                if (raw is decimal) { value = (double)(decimal)raw; return true; }
                if (raw is float) { value = (float)raw; return true; }
                if (raw is short) { value = (short)raw; return true; }
                if (raw is byte) { value = (byte)raw; return true; }
                if (raw is sbyte) { value = (sbyte)raw; return true; }
                if (raw is uint) { value = (uint)raw; return true; }
                if (raw is ulong) { value = (ulong)raw; return true; }
            }
            catch (Exception) { }
            return false;
        }

        private static bool TryParseTime(string s, out DateTime local)
        {
            local = DateTime.MinValue;
            if (string.IsNullOrEmpty(s)) return false;
            try
            {
                DateTimeOffset dto;
                if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out dto))
                {
                    local = dto.LocalDateTime;
                    return true;
                }
                DateTime dt;
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out dt))
                {
                    local = dt;
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        // Accepts numeric epoch milliseconds (any numeric type) or a time string.
        // Out-of-range epoch values return false rather than crashing the parser.
        private static bool TryParseUpdated(object raw, out DateTime local)
        {
            local = DateTime.MinValue;
            try
            {
                bool numeric = raw is double || raw is int || raw is long || raw is decimal
                               || raw is float || raw is short || raw is byte || raw is uint
                               || raw is ulong || raw is sbyte;
                if (numeric)
                {
                    double ms;
                    try { ms = Convert.ToDouble(raw, CultureInfo.InvariantCulture); }
                    catch (Exception) { return false; }
                    if (!IsFinite(ms) || ms <= 0) return false;
                    // Valid .NET DateTime range guard (avoid AddMilliseconds overflow).
                    if (ms > 253402300799000.0) return false; // year 9999
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    local = epoch.AddMilliseconds(ms).ToLocalTime();
                    return true;
                }
                string s = raw as string;
                if (s != null) return TryParseTime(s, out local);
            }
            catch (Exception) { }
            return false;
        }
    }
}
