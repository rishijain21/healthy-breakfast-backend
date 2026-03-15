namespace Sovva.Application.DTOs
{
    public class CreateMealOptionDto
    {
        public int MealId { get; set; }
        public int CategoryId { get; set; }
        public bool IsRequired { get; set; }
        public int MaxSelectable { get; set; }
    }
}
