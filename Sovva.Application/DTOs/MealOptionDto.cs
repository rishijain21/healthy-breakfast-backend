namespace Sovva.Application.DTOs
{
    public class MealOptionDto
    {
        public int MealOptionId { get; set; }
        public int MealId { get; set; }
        public int CategoryId { get; set; }
        public bool IsRequired { get; set; }
        public int MaxSelectable { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
