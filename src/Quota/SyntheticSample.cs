namespace ArkLeft
{
    internal static class SyntheticSample
    {
        // Anonymous synthetic data. No real account identifiers are ever stored.
        public const string Json =
            "{\"items\":[" +
            "{\"product\":\"agent-plan\",\"edition\":\"personal\",\"tier\":\"medium\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"5h\",\"used\":250,\"total\":1000,\"percent\":25,\"reset_at\":\"2026-10-03T14:43:59+08:00\"}," +
            "{\"label\":\"weekly\",\"used\":700,\"total\":1000,\"percent\":70,\"reset_at\":\"2026-10-05T00:00:00+08:00\"}]}," +
            "{\"product\":\"coding-plan\",\"edition\":\"personal\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"monthly\",\"percent\":40,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}]}]}";

        public const string JsonLarge =
            "{\"items\":[" +
            "{\"product\":\"agent-plan\",\"edition\":\"personal\",\"tier\":\"medium\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"5h\",\"used\":250,\"total\":1000,\"percent\":25,\"reset_at\":\"2026-10-03T14:43:59+08:00\"}," +
            "{\"label\":\"weekly\",\"used\":700,\"total\":1000,\"percent\":70,\"reset_at\":\"2026-10-05T00:00:00+08:00\"}," +
            "{\"label\":\"monthly\",\"used\":900,\"total\":1000,\"percent\":90,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}]}," +
            "{\"product\":\"agent-plan-team\",\"edition\":\"team\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"5h\",\"used\":10,\"total\":100,\"percent\":10,\"reset_at\":\"2026-10-03T14:43:59+08:00\"}," +
            "{\"label\":\"weekly\",\"percent\":55,\"reset_at\":\"2026-10-05T00:00:00+08:00\"}," +
            "{\"label\":\"monthly\",\"percent\":12,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}]," +
            "\"updated_at\":\"2026-10-03T10:00:00+08:00\"}," +
            "{\"product\":\"coding-plan\",\"edition\":\"personal\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"monthly\",\"percent\":40,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}," +
            "{\"label\":\"session\",\"percent\":5,\"reset_at\":\"2026-10-03T18:00:00+08:00\"}]}]}";

        public static QuotaSnapshot Build()
        {
            return QuotaParser.Parse(Json);
        }

        public static QuotaSnapshot BuildLarge()
        {
            return QuotaParser.Parse(JsonLarge);
        }
    }
}
