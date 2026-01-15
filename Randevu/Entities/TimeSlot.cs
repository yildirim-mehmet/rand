using System;
using Randevu.Entities;

namespace Randevu.Entities
{
    public class TimeSlot
    {
        public int Id { get; set; }

        public int SalonId { get; set; }
        public int KoltukId { get; set; }

        public DateOnly SlotDate { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public SlotStatus Status { get; set; } = SlotStatus.Bos;

        public string? Kullanici { get; set; } // randevuyu alan
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
