namespace HealthyBreakfastApp.Application.DTOs
{
    public class IngredientBreakdownDto
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int Calories { get; set; }
        public decimal Protein { get; set; }
        
        // ✅ REMOVED: CategoryName (was always empty - waste eliminated)
    }
}
