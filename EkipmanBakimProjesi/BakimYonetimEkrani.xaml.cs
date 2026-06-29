using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using EkipmanBakimProjesi.Data;

namespace EkipmanBakimProjesi
{
    public class BakimLogModel
    {
        public string EkipmanNo { get; set; }
        public DateTime BakimTarihi { get; set; }
        public string BakimYapanKisi { get; set; }
        public double BakimPeriyodu { get; set; }
        public DateTime KayitZamani { get; set; }
        public string Aciklama { get; set; }
    }

    public partial class BakimYonetimEkrani : Window
    {
        public string EkipmanNo { get; set; }
        private VeritabaniErisimi _dbErisimi;

        // YENİ EKLENEN: JsonDosyaYolu artık sabit (readonly) değil, bağlanan veritabanına göre ismini otomatik alıyor.
        private string JsonDosyaYolu => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"BakimLoglari_{VeritabaniErisimi.AktifVeritabaniAdi}.json");

        private ObservableCollection<BakimLogModel> _bakimListesi;

        public BakimYonetimEkrani(string ekipmanNo)
        {
            EkipmanNo = ekipmanNo;
            _dbErisimi = new VeritabaniErisimi();
            InitializeComponent();
            DpBaslangic.DisplayDateEnd = DateTime.Now; // Takvimi bugüne kilitle
            LoglariYukle();
        }

        private void LoglariYukle()
        {
            // 1. Ayarları yükle - YENİ EKLENEN: Ayar dosyası da veritabanına özel oluşturuluyor
            string ayarDosyasi = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"PeriyotAyari_{EkipmanNo}_{VeritabaniErisimi.AktifVeritabaniAdi}.json");

            if (File.Exists(ayarDosyasi))
            {
                try
                {
                    var jsonAyar = File.ReadAllText(ayarDosyasi);
                    using (JsonDocument doc = JsonDocument.Parse(jsonAyar))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("SabitPeriyot", out JsonElement val))
                            TxtHedefSaat.Text = val.ToString();
                    }
                }
                catch { }
            }

            // 2. Bakım geçmişini yükle
            if (File.Exists(JsonDosyaYolu))
            {
                var json = File.ReadAllText(JsonDosyaYolu);
                var tumKayitlar = JsonSerializer.Deserialize<List<BakimLogModel>>(json) ?? new List<BakimLogModel>();
                var siraliListe = tumKayitlar.Where(x => x.EkipmanNo == EkipmanNo).OrderByDescending(x => x.KayitZamani).ToList();
                _bakimListesi = new ObservableCollection<BakimLogModel>(siraliListe);
                DgBakimGecmisi.ItemsSource = _bakimListesi;
            }
            else
            {
                _bakimListesi = new ObservableCollection<BakimLogModel>();
            }

            HesaplaVeGuncelle();
        }

        public string SonHesaplananSure { get; private set; }
        public string SonBakimTarihi { get; private set; }
        public string KalanSure { get; private set; }
        public System.Windows.Media.Brush SureRengi { get; private set; }
        public string TahminiTarih { get; private set; }

        private void HesaplaVeGuncelle()
        {
            if (string.IsNullOrEmpty(TxtHedefSaat.Text)) return;
            if (!double.TryParse(TxtHedefSaat.Text, out double periyot)) periyot = 0;

            var sonGiris = _bakimListesi?.OrderByDescending(x => x.KayitZamani).FirstOrDefault();
            double calisilanSaat = 0;

            if (sonGiris == null)
            {
                var tumKayitlar = _dbErisimi.KayitlariFiltrele(EkipmanNo, (DateTime?)null, (DateTime?)null);
                calisilanSaat = tumKayitlar.Sum(k => k.WorkingHours ?? 0);
                TxtDetay.Text = $"Bakım kaydı yok. Toplam {Math.Round(calisilanSaat, 2)} saat çalışıldı.";
                SonBakimTarihi = "-";

                if (BrdSonAciklama != null) BrdSonAciklama.Visibility = Visibility.Collapsed;
            }
            else
            {
                var kayitlar = _dbErisimi.KayitlariFiltrele(EkipmanNo, sonGiris.BakimTarihi, DateTime.Now.Date);
                calisilanSaat = kayitlar.Sum(k => k.WorkingHours ?? 0);
                TxtDetay.Text = $"Son bakımdan beri {Math.Round(calisilanSaat, 2)} saat çalışıldı.";
                SonBakimTarihi = sonGiris.BakimTarihi.ToString("dd.MM.yyyy");

                if (BrdSonAciklama != null)
                {
                    if (!string.IsNullOrWhiteSpace(sonGiris.Aciklama))
                    {
                        BrdSonAciklama.Visibility = Visibility.Visible;
                        TxtSonAciklamaGoster.Text = sonGiris.Aciklama;
                    }
                    else
                    {
                        BrdSonAciklama.Visibility = Visibility.Collapsed;
                    }
                }
            }

            double kalan = periyot - calisilanSaat;

            if (kalan > 0)
            {
                TxtKalanSureBaslik.Visibility = Visibility.Visible;
                TxtKalanSure.Text = $"{Math.Round(kalan, 2)} Saat Kaldı";
                TxtKalanSure.Foreground = System.Windows.Media.Brushes.DarkGreen;
            }
            else
            {
                TxtKalanSureBaslik.Visibility = Visibility.Collapsed;
                TxtKalanSure.Text = $"Bakım {Math.Round(Math.Abs(kalan), 2)} Saat Gecikti!";
                TxtKalanSure.Foreground = System.Windows.Media.Brushes.Red;
            }

            TxtKalanSure.Foreground = kalan > 0 ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.Red;

            string tahminiAyYil = "-";
            if (kalan > 0 && periyot > 0)
            {
                var tumGecmis = _dbErisimi.KayitlariFiltrele(EkipmanNo, (DateTime?)null, (DateTime?)null);
                var gecerliKayitlar = tumGecmis.Where(x => x.Date.HasValue).ToList();

                if (gecerliKayitlar.Any())
                {
                    DateTime ilkGun = gecerliKayitlar.Min(x => x.Date.Value);
                    DateTime sonGun = gecerliKayitlar.Max(x => x.Date.Value);
                    double toplamSaat = gecerliKayitlar.Sum(x => x.WorkingHours ?? 0);

                    double gunFarki = (sonGun - ilkGun).TotalDays;
                    if (gunFarki < 1) gunFarki = 1;

                    double gunlukOrtalama = toplamSaat / gunFarki;

                    if (gunlukOrtalama > 0)
                    {
                        double kalanGun = kalan / gunlukOrtalama;
                        DateTime tahminiZaman = DateTime.Now.AddDays(kalanGun);
                        tahminiAyYil = tahminiZaman.ToString("MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
                    }
                }
            }
            else if (kalan <= 0 && periyot > 0)
            {
                tahminiAyYil = "Zamanı Geldi!";
            }

            TxtTahmini.Text = $"Tahmini Bakım: {tahminiAyYil}";
            TahminiTarih = tahminiAyYil;

            TxtDetay.Text += "\n" + OrtalamaBakimSuresiHesapla();

            KalanSure = TxtKalanSure.Text;
            SureRengi = TxtKalanSure.Foreground;
        }

        private void FormuTemizle()
        {
            TxtBakimYapan.Clear();
            if (TxtAciklama != null) TxtAciklama.Clear();
            DpBaslangic.SelectedDate = null;
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (!DpBaslangic.SelectedDate.HasValue || string.IsNullOrWhiteSpace(TxtHedefSaat.Text))
            {
                MessageBox.Show("Lütfen tarih ve periyot girin.");
                return;
            }

            if (DpBaslangic.SelectedDate.Value.Date > DateTime.Now.Date)
            {
                MessageBox.Show("Gelecek tarihli kayıt yapılamaz!");
                return;
            }

            double hedef = double.Parse(TxtHedefSaat.Text);

            var tumKayitlar = File.Exists(JsonDosyaYolu) ? JsonSerializer.Deserialize<List<BakimLogModel>>(File.ReadAllText(JsonDosyaYolu)) : new List<BakimLogModel>();

            var yeniLog = new BakimLogModel
            {
                EkipmanNo = EkipmanNo,
                BakimTarihi = DpBaslangic.SelectedDate.Value,
                BakimYapanKisi = TxtBakimYapan.Text,
                BakimPeriyodu = hedef,
                KayitZamani = DateTime.Now,
                Aciklama = TxtAciklama.Text
            };
            tumKayitlar.Add(yeniLog);

            File.WriteAllText(JsonDosyaYolu, JsonSerializer.Serialize(tumKayitlar.OrderByDescending(x => x.KayitZamani), new JsonSerializerOptions { WriteIndented = true }));

            _bakimListesi = new ObservableCollection<BakimLogModel>(tumKayitlar.Where(x => x.EkipmanNo == EkipmanNo).OrderByDescending(x => x.KayitZamani));
            DgBakimGecmisi.ItemsSource = _bakimListesi;

            FormuTemizle();
            HesaplaVeGuncelle();
            MessageBox.Show("Başarıyla kaydedildi.");
        }

        private void TxtHedefSaat_TextChanged(object sender, TextChangedEventArgs e) => HesaplaVeGuncelle();

        private void BtnPeriyotKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TxtHedefSaat.Text, out double hedef))
            {
                string ayarDosyasi = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"PeriyotAyari_{EkipmanNo}_{VeritabaniErisimi.AktifVeritabaniAdi}.json");
                File.WriteAllText(ayarDosyasi, JsonSerializer.Serialize(new { SabitPeriyot = hedef }));
                MessageBox.Show("Periyot sabitlendi!");
            }
        }

        private string OrtalamaBakimSuresiHesapla()
        {
            // Yeterli kayıt yoksa (en az 2 kayıt lazım ki 1 aralık bulalım)
            if (_bakimListesi == null || _bakimListesi.Count < 2)
            {
                return "Ortalama için en az 2 bakım kaydı gerekiyor.";
            }

            // Listeyi tarihe göre en yeniden en eskiye sıralayalım
            var siraliBakimlar = _bakimListesi.OrderByDescending(x => x.BakimTarihi).ToList();

            // En fazla SON 5 KAYDI alıyoruz
            int alinacakKayitSayisi = Math.Min(siraliBakimlar.Count, 5);
            var secilenBakimlar = siraliBakimlar.Take(alinacakKayitSayisi).ToList();

            List<double> aralikSaatleri = new List<double>();

            // Her bir bakım ile bir önceki (daha eski) bakım arasındaki ÇALIŞMA SAATİNİ bul
            for (int i = 0; i < secilenBakimlar.Count - 1; i++)
            {
                DateTime yeniTarih = secilenBakimlar[i].BakimTarihi;
                DateTime eskiTarih = secilenBakimlar[i + 1].BakimTarihi;

                // Veritabanından bu iki tarih arasındaki kayıtları çek (eskiTarih'ten yeniTarih'e kadar)
                var ikiBakimArasiKayitlar = _dbErisimi.KayitlariFiltrele(EkipmanNo, eskiTarih, yeniTarih);

                // O aralıktaki toplam çalışma saatini hesapla
                double aralikToplamSaati = ikiBakimArasiKayitlar.Sum(k => k.WorkingHours ?? 0);

                // Listeye ekle
                aralikSaatleri.Add(aralikToplamSaati);
            }

            // Çalışma saatlerinin ortalamasını alıyoruz
            double ortalamaSaat = aralikSaatleri.Average();

            return $"Son {secilenBakimlar.Count} bakıma göre ortalama {Math.Round(ortalamaSaat, 1)} saatte bir bakım yapılmış.";
        }

        private void DpBaslangic_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGuncelle();
    }
}