using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using EkipmanBakimProjesi.Data;

namespace EkipmanBakimProjesi
{
    // ÇÖZÜLEN ARIZALARI TUTMAK İÇİN YENİ MODEL
    public class CozulenArizaModel
    {
        public string EkipmanNo { get; set; }
        public string EkipmanAdi { get; set; }
        public DateTime BildirimTarihi { get; set; }
        public DateTime CozumTarihi { get; set; }
        public string ToplamSure { get; set; }
        public string Aciklama { get; set; }
    }

    public class BakimTakipModel
    {
        public string EkipmanNo { get; set; }
        public string EkipmanAdi { get; set; }
        public string SonBakim { get; set; }
        public double KalanSaat { get; set; }
        public string KalanSureMetin { get; set; }
        public string Durum { get; set; }
        public System.Windows.Media.Brush DurumRengi { get; set; }
        public int SiralamaOnceligi { get; set; }
        public Visibility ArizaCozGoster { get; set; }
        public string ArizaAciklamasi { get; set; }
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

            string arizaJsonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AktifArizalar.json");
            List<ArizaModel> aktifArizalar = new List<ArizaModel>();
            if (File.Exists(arizaJsonYolu))
            {
                aktifArizalar = JsonSerializer.Deserialize<List<ArizaModel>>(File.ReadAllText(arizaJsonYolu)) ?? new List<ArizaModel>();
            }

            List<BakimLogModel> tumBakimlar = new List<BakimLogModel>();
            if (File.Exists(JsonDosyaYolu))
            {
                tumBakimlar = JsonSerializer.Deserialize<List<BakimLogModel>>(File.ReadAllText(JsonDosyaYolu)) ?? new List<BakimLogModel>();
            }

            foreach (var eq in ekipmanlar)
            {
                var makineninArizasi = aktifArizalar.FirstOrDefault(x => x.EkipmanNo == eq);

                var kayitlarDb = _dbErisimi.KayitlariFiltrele(eq, null, null);
                string isim = "Tanımsız Makine";
                if (kayitlarDb != null && kayitlarDb.Any())
                {
                    var makine = kayitlarDb.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name));
                    if (makine != null) isim = makine.Name;
                }
                string tamEkipmanAdi = $"{eq} - {isim}";

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

                // 1. ÇÖZÜM BURASI: Periyot yoksa BİLE makine arızalıysa artık es geçmeyecek ve listeye alacak!
                if (periyot == 0 && makineninArizasi == null) continue;

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
                    calisilanSaat = kayitlarDb.Sum(k => k.WorkingHours ?? 0);
                }

                double kalan = periyot - calisilanSaat;
                string durum;
                System.Windows.Media.Brush renk;
                string kalanSureMetin;
                int oncelik;
                Visibility arizaButon = Visibility.Collapsed;
                string arizaDetayMetni = "";

                if (makineninArizasi != null)
                {
                    durum = "⚠️ ARIZALI!";
                    renk = System.Windows.Media.Brushes.DarkRed;
                    kalanSureMetin = "Beklenmeyen Arıza";
                    oncelik = 0;
                    arizaButon = Visibility.Visible;
                    arizaDetayMetni = makineninArizasi.Aciklama;
                }
                else if (kalan <= 0)
                {
                    durum = "Bakım Geldi!";
                    renk = System.Windows.Media.Brushes.Red;
                    kalanSureMetin = $"{Math.Round(Math.Abs(kalan), 2)} Saat Gecikti";
                    oncelik = 1;
                }
                else if (kalan <= 150)
                {
                    durum = "Yaklaşıyor";
                    renk = System.Windows.Media.Brushes.DarkOrange;
                    kalanSureMetin = $"{Math.Round(kalan, 2)} Saat";
                    oncelik = 2;
                }
                else
                {
                    durum = "Normal";
                    renk = System.Windows.Media.Brushes.ForestGreen;
                    kalanSureMetin = $"{Math.Round(kalan, 2)} Saat";
                    oncelik = 3;
                }

                takipListesi.Add(new BakimTakipModel
                {
                    EkipmanNo = eq,
                    EkipmanAdi = tamEkipmanAdi,
                    SonBakim = sonBakimTarihiTxt,
                    KalanSaat = kalan,
                    KalanSureMetin = kalanSureMetin,
                    Durum = durum,
                    DurumRengi = renk,
                    SiralamaOnceligi = oncelik,
                    ArizaCozGoster = arizaButon,
                    ArizaAciklamasi = arizaDetayMetni
                });
            }

            DgBakimTakip.ItemsSource = takipListesi.OrderBy(x => x.SiralamaOnceligi).ThenBy(x => x.KalanSaat).ToList();
        }

        private void BtnArizaDetay_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.DataContext is BakimTakipModel seciliMakine)
            {
                Window detayPenceresi = new Window
                {
                    Title = "🔍 Arıza Detayı",
                    Width = 450,
                    SizeToContent = SizeToContent.Height,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#F8FAFC")
                };

                StackPanel panel = new StackPanel { Margin = new Thickness(20, 20, 20, 30) };

                TextBlock txtBaslik = new TextBlock
                {
                    Text = $"{seciliMakine.EkipmanAdi}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1E293B"),
                    Margin = new Thickness(0, 0, 0, 5),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                TextBlock txtAltBaslik = new TextBlock
                {
                    Text = "Arıza Bildirim Raporu",
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#64748B"),
                    Margin = new Thickness(0, 0, 0, 15),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                TextBox txtDetay = new TextBox
                {
                    Text = seciliMakine.ArizaAciklamasi,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Height = 130,
                    Padding = new Thickness(10),
                    FontSize = 14,
                    Background = System.Windows.Media.Brushes.White,
                    BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#CBD5E1"),
                    BorderThickness = new Thickness(1)
                };

                Button btnKapat = new Button
                {
                    Content = "Kapat",
                    Width = 120,
                    Height = 40,
                    Margin = new Thickness(0, 20, 0, 0),
                    Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#3B82F6"),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    BorderThickness = new Thickness(0)
                };

                btnKapat.Click += (s, args) => detayPenceresi.Close();

                panel.Children.Add(txtBaslik);
                panel.Children.Add(txtAltBaslik);
                panel.Children.Add(txtDetay);
                panel.Children.Add(btnKapat);

                detayPenceresi.Content = panel;
                detayPenceresi.ShowDialog();
            }
        }

        // 2. ÇÖZÜM BURASI: Arıza Çözülünce Silmeden Önce Geçmişe Kaydediyoruz!
        private void BtnArizaCoz_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.DataContext is BakimTakipModel seciliMakine)
            {
                var cevap = MessageBox.Show($"{seciliMakine.EkipmanAdi} arızası giderildi olarak işaretlensin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (cevap == MessageBoxResult.Yes)
                {
                    string arizaJsonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AktifArizalar.json");
                    string gecmisJsonYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ArizaGecmisi.json");

                    if (File.Exists(arizaJsonYolu))
                    {
                        var arizalar = JsonSerializer.Deserialize<List<ArizaModel>>(File.ReadAllText(arizaJsonYolu));
                        var silinecek = arizalar.FirstOrDefault(x => x.EkipmanNo == seciliMakine.EkipmanNo);

                        if (silinecek != null)
                        {
                            // 1. GEÇMİŞE KAYDET
                            List<CozulenArizaModel> gecmis = new List<CozulenArizaModel>();
                            if (File.Exists(gecmisJsonYolu))
                            {
                                gecmis = JsonSerializer.Deserialize<List<CozulenArizaModel>>(File.ReadAllText(gecmisJsonYolu)) ?? new List<CozulenArizaModel>();
                            }

                            // Süre hesaplama (Gün, Saat, Dakika)
                            TimeSpan gecenSure = DateTime.Now - silinecek.BildirimTarihi;
                            string sureMetni = $"{(int)gecenSure.TotalDays} Gün, {gecenSure.Hours} Saat, {gecenSure.Minutes} Dk";

                            gecmis.Add(new CozulenArizaModel
                            {
                                EkipmanNo = silinecek.EkipmanNo,
                                EkipmanAdi = seciliMakine.EkipmanAdi,
                                BildirimTarihi = silinecek.BildirimTarihi,
                                CozumTarihi = DateTime.Now,
                                ToplamSure = sureMetni,
                                Aciklama = silinecek.Aciklama
                            });

                            File.WriteAllText(gecmisJsonYolu, JsonSerializer.Serialize(gecmis, new JsonSerializerOptions { WriteIndented = true }));

                            // 2. AKTİFLERDEN SİL
                            arizalar.Remove(silinecek);
                            File.WriteAllText(arizaJsonYolu, JsonSerializer.Serialize(arizalar, new JsonSerializerOptions { WriteIndented = true }));

                            ListeyiDoldur();
                            MessageBox.Show("Arıza başarıyla kapatıldı ve geçmişe kaydedildi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
        }
    }
}