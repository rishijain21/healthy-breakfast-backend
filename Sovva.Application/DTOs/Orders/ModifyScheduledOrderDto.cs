using System.Collections.Generic;

namespace Sovva.Application.DTOs
{
    public class ModifyScheduledOrderDto
    {
        public List<ScheduledOrderIngredientDto> SelectedIngredients { get; set; } = new();
        public string? DeliveryTimeSlot { get; set; }
        public NutritionalSummaryDto? NutritionalSummary { get; set; }
    }
}
