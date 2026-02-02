// HealthyBreakfastApp.Application/DTOs/WeeklyScheduleDto.cs

namespace HealthyBreakfastApp.Application.DTOs
{
    public class WeeklyScheduleDto
    {
        public int DayOfWeek { get; set; }  // 0=Sunday, 1=Monday, etc.
        public int Quantity { get; set; }   // Number of items for this day
        
        // Helper property for display
        public string DayName => ((DayOfWeek)DayOfWeek).ToString();
    }
}
