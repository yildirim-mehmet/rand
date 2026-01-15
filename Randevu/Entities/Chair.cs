namespace Randevu.Entities
{
    public class Chair
    {
        public int Id { get; set; }

        public int SalonId { get; set; }
        public Salon Salon { get; set; } = null!;

        public int SiraNo { get; set; } // 1,2,3...

        public bool Aktif { get; set; } = true;
    }
}
