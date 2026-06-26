using System;

namespace EkipmanBakimProjesi.Models
{
    public class BakimKaydi
    {
        // SQL'deki kolon isimleriyle birebir aynı olmalı ki Dapper eşleştirebilsin.
        public int ID { get; set; }
        public int? Equipment { get; set; }
        public double? WorkingHours { get; set; }
        public DateTime? Date { get; set; }
        public int? Location { get; set; }
        public string Name { get; set; }
    }
}