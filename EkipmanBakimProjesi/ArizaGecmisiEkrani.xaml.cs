using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace EkipmanBakimProjesi
{
    public partial class ArizaGecmisiEkrani : Window
    {
        private string GecmisJsonYolu => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ArizaGecmisi.json");

        public ArizaGecmisiEkrani()
        {
            InitializeComponent();
            GecmisiYukle();
        }

        private void GecmisiYukle()
        {
            if (File.Exists(GecmisJsonYolu))
            {
                var liste = JsonSerializer.Deserialize<List<CozulenArizaModel>>(File.ReadAllText(GecmisJsonYolu));
                // En son çözülen arıza en üstte çıksın diye tarihe göre tersten sıralıyoruz
                DgArizaGecmisi.ItemsSource = liste.OrderByDescending(x => x.CozumTarihi).ToList();
            }
        }
    }
}