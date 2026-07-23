namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Catalogue des outils C# autorisés. Le LLM ne les invoque jamais —
    /// seuls StoreChat / engines / WorkflowGuard décident.
    /// </summary>
    public static class SalesAssistantTools
    {
        public const string SearchProducts = "SearchProducts";
        public const string AddToCart = "AddToCart";
        public const string RemoveFromCart = "RemoveFromCart";
        public const string SuggestComplements = "SuggestComplements";
        public const string BuildPack = "BuildPack";
        public const string CompareProducts = "CompareProducts";
        public const string CreateQuote = "CreateQuote";
        public const string CreateOrder = "CreateOrder";
        public const string StartPayment = "StartPayment";
        public const string ResetProject = "ResetProject";

        /// <summary>Outils commerce sensibles — soumis au WorkflowGuard.</summary>
        public static readonly string[] Guarded =
        {
            AddToCart,
            CreateQuote,
            CreateOrder,
            StartPayment
        };

        public static readonly string[] All =
        {
            SearchProducts,
            AddToCart,
            RemoveFromCart,
            SuggestComplements,
            BuildPack,
            CompareProducts,
            CreateQuote,
            CreateOrder,
            StartPayment,
            ResetProject
        };
    }
}
