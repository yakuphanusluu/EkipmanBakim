using System;
using System.Windows.Media;

namespace EkipmanBakimProjesi.Models
{
    public class MakineKullanimModel
    {
        public string EkipmanNo { get; set; }
        public string EkipmanAdi { get; set; }
        public double ToplamCalismaSaati { get; set; }
        public double GunlukOrtalamaSaat { get; set; }
        public double UtilizationYuzdesi { get; set; } // % Kullanım Oranı
        public string UtilizationMetin => $"%{Math.Round(UtilizationYuzdesi, 1)}";
        public Brush BarRengi { get; set; } // Grafikte az çalışan kırmızı, çok çalışan yeşil görünecek
    }
}