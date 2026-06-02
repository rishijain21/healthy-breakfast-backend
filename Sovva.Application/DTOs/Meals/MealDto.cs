namespace Sovva.Application.DTOs
{
    public class MealDto
    {
        public int MealId { get; set; }
        public string MealName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal BasePrice { get; set; }
        public bool IsComplete { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
