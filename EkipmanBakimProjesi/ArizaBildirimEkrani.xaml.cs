using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using EkipmanBakimProjesi.Data;

namespace EkipmanBakimProjesi
{
    public class ArizaModel
    {
        public string EkipmanNo { get; set; }
        public string Aciklama { get; set; }
        public DateTime BildirimTarihi { get; set; }
    }

    public partial class ArizaBildirimEkrani : Window
    {
        private VeritabaniErisimi _dbErisimi;
        private string JsonDosyaYolu => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AktifArizalar.json");

        public ArizaBildirimEkrani()
        {
            InitializeComponent();
            _dbErisimi = new VeritabaniErisimi();
            EkipmanlariYukle();
        }

        private void EkipmanlariYukle()
        {
            var ekipmanlar = _dbErisimi.BenzersizEkipmanlariGetir();
            List<string> liste = new List<string>();

            foreach (var eq in ekipmanlar)
            {
                // ÇÖZÜM: Tıpkı Bakım Takip ekranında yaptığımız gibi her makineyi kendi ID'siyle soruyoruz
                var kayitlarDb = _dbErisimi.KayitlariFiltrele(eq, null, null);

                string isim = "Tanımsız Makine";
                if (kayitlarDb != null && kayitlarDb.Any())
                {
                    var makine = kayitlarDb.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name));
                    if (makine != null) isim = makine.Name;
                }

                liste.Add($"{eq} - {isim}");
            }

            CmbEkipmanlar.ItemsSource = liste;
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEkipmanlar.SelectedItem == null || string.IsNullOrWhiteSpace(TxtArizaAciklamasi.Text))
            {
                MessageBox.Show("Lütfen ekipman seçin ve arıza açıklamasını girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string secim = CmbEkipmanlar.SelectedItem.ToString();
            string ekipmanNo = secim.Split('-')[0].Trim();

            List<ArizaModel> aktifArizalar = new List<ArizaModel>();
            if (File.Exists(JsonDosyaYolu))
            {
                aktifArizalar = JsonSerializer.Deserialize<List<ArizaModel>>(File.ReadAllText(JsonDosyaYolu)) ?? new List<ArizaModel>();
            }

            if (aktifArizalar.Any(x => x.EkipmanNo == ekipmanNo))
            {
                MessageBox.Show("Bu makine için zaten açık bir arıza kaydı var!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            aktifArizalar.Add(new ArizaModel
            {
                EkipmanNo = ekipmanNo,
                Aciklama = TxtArizaAciklamasi.Text.Trim(),
                BildirimTarihi = DateTime.Now
            });

            File.WriteAllText(JsonDosyaYolu, JsonSerializer.Serialize(aktifArizalar, new JsonSerializerOptions { WriteIndented = true }));

            MessageBox.Show("Arıza sisteme işlendi. Bakım takip ekranında en üstte görünecektir.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}