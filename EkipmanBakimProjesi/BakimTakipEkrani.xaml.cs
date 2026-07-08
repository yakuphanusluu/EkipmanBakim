using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using EkipmanBakimProjesi.Data;

namespace EkipmanBakimProjesi
{
    public class BakimTakipModel
    {
        public string EkipmanNo { get; set; }
        public string EkipmanAdi { get; set; }
        public string SonBakim { get; set; }
        public double KalanSaat { get; set; }
        public string KalanSureMetin { get; set; }
        public string Durum { get; set; }
        public System.Windows.Media.Brush DurumRengi { get; set; }
    }

    public partial class BakimTakipEkrani : Window
    {
        private VeritabaniErisimi _dbErisimi;

        private string JsonDosyaYolu => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"BakimLoglari_{VeritabaniErisimi.AktifVeritabaniAdi}.json");

        public BakimTakipEkrani()
        {
            InitializeComponent();
            _dbErisimi = new VeritabaniErisimi();
            ListeyiDoldur();
        }

        private void ListeyiDoldur()
        {
            var ekipmanlar = _dbErisimi.BenzersizEkipmanlariGetir();
            List<BakimTakipModel> takipListesi = new List<BakimTakipModel>();

            List<BakimLogModel> tumBakimlar = new List<BakimLogModel>();
            if (File.Exists(JsonDosyaYolu))
            {
                tumBakimlar = JsonSerializer.Deserialize<List<BakimLogModel>>(File.ReadAllText(JsonDosyaYolu)) ?? new List<BakimLogModel>();
            }

            foreach (var eq in ekipmanlar)
            {
                // ÇÖZÜM BURADA: Her makine için kendi numarasıyla veritabanına soruyoruz!
                var kayitlarDb = _dbErisimi.KayitlariFiltrele(eq, null, null);

                string isim = "Tanımsız Makine";
                if (kayitlarDb != null && kayitlarDb.Any())
                {
                    var makine = kayitlarDb.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name));
                    if (makine != null) isim = makine.Name;
                }
                string tamEkipmanAdi = $"{eq} - {isim}";

                // 1. Periyodu Bul
                double periyot = 0;
                string ayarDosyasi = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"PeriyotAyari_{eq}_{VeritabaniErisimi.AktifVeritabaniAdi}.json");

                if (File.Exists(ayarDosyasi))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(File.ReadAllText(ayarDosyasi));
                        if (doc.RootElement.TryGetProperty("SabitPeriyot", out JsonElement val)) periyot = double.Parse(val.ToString());
                    }
                    catch { }
                }

                var sonBakim = tumBakimlar.Where(x => x.EkipmanNo == eq).OrderByDescending(x => x.KayitZamani).FirstOrDefault();
                if (periyot == 0 && sonBakim != null) periyot = sonBakim.BakimPeriyodu;

                if (periyot == 0) continue;

                // 2. Çalışılan Saati Hesapla
                double calisilanSaat = 0;
                string sonBakimTarihiTxt = "-";

                if (sonBakim != null)
                {
                    sonBakimTarihiTxt = sonBakim.BakimTarihi.ToString("dd.MM.yyyy");
                    var kayitlar = _dbErisimi.KayitlariFiltrele(eq, sonBakim.BakimTarihi, DateTime.Now.Date);
                    calisilanSaat = kayitlar.Sum(k => k.WorkingHours ?? 0);
                }
                else
                {
                    // Yukarıda çektiğimiz db kayıtlarını kullanabiliriz (performans için)
                    calisilanSaat = kayitlarDb.Sum(k => k.WorkingHours ?? 0);
                }

                // 3. Durumu Belirle
                double kalan = periyot - calisilanSaat;
                string durum;
                System.Windows.Media.Brush renk;
                string kalanSureMetin;

                if (kalan <= 0)
                {
                    durum = "Bakım Geldi!";
                    renk = System.Windows.Media.Brushes.Red;
                    kalanSureMetin = $"{Math.Round(Math.Abs(kalan), 2)} Saat Gecikti";
                }
                else if (kalan <= 150)
                {
                    durum = "Yaklaşıyor";
                    renk = System.Windows.Media.Brushes.DarkOrange;
                    kalanSureMetin = $"{Math.Round(kalan, 2)} Saat";
                }
                else
                {
                    durum = "Normal";
                    renk = System.Windows.Media.Brushes.ForestGreen;
                    kalanSureMetin = $"{Math.Round(kalan, 2)} Saat";
                }

                takipListesi.Add(new BakimTakipModel
                {
                    EkipmanNo = eq,
                    EkipmanAdi = tamEkipmanAdi,
                    SonBakim = sonBakimTarihiTxt,
                    KalanSaat = kalan,
                    KalanSureMetin = kalanSureMetin,
                    Durum = durum,
                    DurumRengi = renk
                });
            }

            DgBakimTakip.ItemsSource = takipListesi.OrderBy(x => x.KalanSaat).ToList();
        }
    }
}