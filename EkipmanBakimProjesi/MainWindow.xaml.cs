using ClosedXML.Excel;
using EkipmanBakimProjesi.Data;
using EkipmanBakimProjesi.Models;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

using PDFColors = QuestPDF.Helpers.Colors;

namespace EkipmanBakimProjesi
{
    public partial class MainWindow : Window
    {
        private VeritabaniErisimi dbErisimi;

        public MainWindow()
        {
            InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community;
            dbErisimi = new VeritabaniErisimi();
            EkipmanlariYukle();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (CmbEkipmanlar.SelectedItem != null)
            {
                // DÜZELTME: Sadece "-" işaretinden önceki numarayı alıyoruz
                string secilenEkipman = CmbEkipmanlar.SelectedItem.ToString().Split('-')[0].Trim();
                BakimVerileriniGuncelle(secilenEkipman);
            }
        }

        private void EkipmanlariYukle()
        {
            try
            {
                // Önce sadece benzersiz numaraları alıyoruz
                var ekipmanNumaralari = dbErisimi.BenzersizEkipmanlariGetir();
                List<string> gosterimListesi = new List<string>();

                foreach (var no in ekipmanNumaralari)
                {
                    // ÇÖZÜM: Her makineyi kendi numarasıyla veritabanından canlı sorguluyoruz
                    var kayitlar = dbErisimi.KayitlariFiltrele(no, null, null);

                    string isim = "Tanımsız Makine";
                    if (kayitlar != null && kayitlar.Any())
                    {
                        // O makineye ait kayıtlardan adı boş olmayan ilkini alıyoruz
                        var makine = kayitlar.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name));
                        if (makine != null) isim = makine.Name;
                    }

                    // "200087 - Şişirme Makinesi" formatında listeye ekle
                    gosterimListesi.Add($"{no} - {isim}");
                }

                CmbEkipmanlar.ItemsSource = gosterimListesi;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veritabanına bağlanırken bir hata oluştu:\n" + ex.Message, "Bağlantı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFiltrele_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEkipmanlar.SelectedItem == null)
            {
                MessageBox.Show("Lütfen önce bir ekipman seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // DÜZELTME: Sadece numarayı ayıkla
            string secilenEkipman = CmbEkipmanlar.SelectedItem.ToString().Split('-')[0].Trim();
            DateTime? baslangic = DpBaslangic.SelectedDate;
            DateTime? bitis = DpBitis.SelectedDate;

            try
            {
                var kayitlar = dbErisimi.KayitlariFiltrele(secilenEkipman, baslangic, bitis);
                DgKayitlar.ItemsSource = kayitlar;

                double toplamSaat = kayitlar.Sum(k => k.WorkingHours ?? 0);
                TxtToplamSaat.Text = $"Toplam Çalışma Saati: {Math.Round(toplamSaat, 2)} saat";

                BakimVerileriniGuncelle(secilenEkipman);

                var calisilmayanGunler = kayitlar
                    .Where(k => k.Date.HasValue && (k.WorkingHours == null || k.WorkingHours == 0))
                    .Select(k => k.Date.Value.Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                if (calisilmayanGunler.Count > 0)
                {
                    DateTime ilkTarih = baslangic ?? kayitlar.Min(k => k.Date.Value);
                    DateTime sonTarih = bitis ?? kayitlar.Max(k => k.Date.Value);

                    TxtCalisilmayanGunlerOzet.Text = $"⚠️ Uyarı: {ilkTarih:dd.MM.yyyy} ile {sonTarih:dd.MM.yyyy} arasında {calisilmayanGunler.Count} gün çalışılmadı.";
                    TxtCalisilmayanGunlerOzet.Visibility = Visibility.Visible;

                    DgCalisilmayanGunler.ItemsSource = calisilmayanGunler;
                    DgCalisilmayanGunler.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtCalisilmayanGunlerOzet.Visibility = Visibility.Collapsed;
                    DgCalisilmayanGunler.ItemsSource = null;
                    DgCalisilmayanGunler.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BakimVerileriniGuncelle(string ekipmanNo)
        {
            string dbName = VeritabaniErisimi.AktifVeritabaniAdi;

            string jsonDosyaYolu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"BakimLoglari_{dbName}.json");
            string ayarDosyasi = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"PeriyotAyari_{ekipmanNo}_{dbName}.json");

            double sabitPeriyot = 0;

            if (File.Exists(ayarDosyasi))
            {
                try
                {
                    var jsonAyar = File.ReadAllText(ayarDosyasi);
                    using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(jsonAyar))
                    {
                        if (doc.RootElement.TryGetProperty("SabitPeriyot", out System.Text.Json.JsonElement val))
                            sabitPeriyot = double.Parse(val.ToString());
                    }
                }
                catch { }
            }

            List<BakimLogModel> tumKayitlar = new List<BakimLogModel>();
            if (File.Exists(jsonDosyaYolu))
            {
                var json = File.ReadAllText(jsonDosyaYolu);
                tumKayitlar = System.Text.Json.JsonSerializer.Deserialize<List<BakimLogModel>>(json) ?? new List<BakimLogModel>();
            }

            var sonBakim = tumKayitlar.Where(x => x.EkipmanNo == ekipmanNo).OrderByDescending(x => x.KayitZamani).FirstOrDefault();

            if (sabitPeriyot == 0 && sonBakim != null)
            {
                sabitPeriyot = sonBakim.BakimPeriyodu;
            }

            double calisilanSaat = 0;
            if (sonBakim != null)
            {
                TxtSonBakimTarihi.Text = sonBakim.BakimTarihi.ToString("dd.MM.yyyy");
                var kayitlar = dbErisimi.KayitlariFiltrele(ekipmanNo, sonBakim.BakimTarihi, DateTime.Now.Date);
                calisilanSaat = kayitlar.Sum(k => k.WorkingHours ?? 0);
            }
            else
            {
                TxtSonBakimTarihi.Text = "-";
                var tumCalisma = dbErisimi.KayitlariFiltrele(ekipmanNo, null, null);
                calisilanSaat = tumCalisma.Sum(k => k.WorkingHours ?? 0);
            }

            string tahminiAyYil = "-";

            if (sabitPeriyot > 0)
            {
                double kalan = sabitPeriyot - calisilanSaat;

                if (kalan > 0)
                {
                    TxtBakimKalanSure.Text = $"{Math.Round(kalan, 2)} Saat Kaldı";
                    TxtBakimKalanSure.Foreground = System.Windows.Media.Brushes.DodgerBlue;
                }
                else
                {
                    TxtBakimKalanSure.Text = $"Bakım {Math.Round(Math.Abs(kalan), 2)} Saat Gecikti!";
                    TxtBakimKalanSure.Foreground = System.Windows.Media.Brushes.Red;
                }

                if (kalan > 0)
                {
                    var tumGecmis = dbErisimi.KayitlariFiltrele(ekipmanNo, null, null);
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
                else
                {
                    tahminiAyYil = "Zamanı Geldi!";
                }
            }
            else
            {
                TxtBakimKalanSure.Text = "Periyot Ayarlanmadı!";
                TxtBakimKalanSure.Foreground = System.Windows.Media.Brushes.Gray;
            }

            if (TxtTahminiTarihAna != null)
            {
                TxtTahminiTarihAna.Text = $"Tahmini: {tahminiAyYil}";
            }
        }

        private void BtnUtilization_Click(object sender, RoutedEventArgs e)
        {
            MakineKullanimRaporuEkrani raporEkrani = new MakineKullanimRaporuEkrani();
            raporEkrani.Owner = this;
            raporEkrani.ShowDialog();
        }

        private void BtnTakip_Click(object sender, RoutedEventArgs e)
        {
            BakimTakipEkrani takipEkrani = new BakimTakipEkrani();
            takipEkrani.Owner = this;
            takipEkrani.Show();
        }

        private void BtnGecmis_Click(object sender, RoutedEventArgs e)
        {
            GecmisBakimlarEkrani gecmisEkrani = new GecmisBakimlarEkrani();
            gecmisEkrani.Owner = this;
            gecmisEkrani.ShowDialog();
        }

        private void BtnTarihiTemizle_Click(object sender, RoutedEventArgs e) { DpBaslangic.SelectedDate = null; DpBitis.SelectedDate = null; }
        private void BtnGeri_Click(object sender, RoutedEventArgs e) { GirisEkrani g = new GirisEkrani(); g.Show(); this.Close(); }

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            var kayitlar = DgKayitlar.ItemsSource as List<BakimKaydi>;
            if (kayitlar == null || !kayitlar.Any()) return;

            SaveFileDialog sfd = new SaveFileDialog { Filter = "Excel Dosyası|*.xlsx", FileName = $"EkipmanRaporu_{DateTime.Now:yyyyMMdd}.xlsx", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };

            if (sfd.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Bakım Kayıtları");

                    worksheet.Cell(1, 1).Value = "Ekipman Numarası";
                    worksheet.Cell(1, 2).Value = "Tarih";
                    worksheet.Cell(1, 3).Value = "Çalışma Saati";
                    worksheet.Cell(1, 4).Value = "Ekipman Adı";

                    var baslikRange = worksheet.Range("A1:D1");
                    baslikRange.Style.Font.Bold = true;
                    baslikRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    baslikRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    var koyuKirmizi = XLColor.FromHtml("#E74C3C");

                    int row = 2;
                    foreach (var k in kayitlar)
                    {
                        worksheet.Cell(row, 1).Value = k.Equipment;
                        worksheet.Cell(row, 2).Value = k.Date?.ToString("dd.MM.yyyy");
                        worksheet.Cell(row, 3).Value = k.WorkingHours ?? 0;
                        worksheet.Cell(row, 4).Value = k.Name;

                        var range = worksheet.Range(row, 1, row, 4);
                        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                        if ((k.WorkingHours ?? 0) == 0)
                        {
                            range.Style.Fill.BackgroundColor = koyuKirmizi;
                            range.Style.Font.FontColor = XLColor.White;
                        }
                        row++;
                    }

                    int toplamSatir = row;
                    worksheet.Cell(toplamSatir, 2).Value = "TOPLAM ÇALIŞMA SAATİ:";
                    worksheet.Cell(toplamSatir, 2).Style.Font.Bold = true;
                    worksheet.Cell(toplamSatir, 3).FormulaA1 = $"SUM(C2:C{toplamSatir - 1})";
                    worksheet.Cell(toplamSatir, 3).Style.Font.Bold = true;

                    var toplamRange = worksheet.Range(toplamSatir, 1, toplamSatir, 4);
                    toplamRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    toplamRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(sfd.FileName);
                    MessageBox.Show("Excel raporu başarıyla oluşturuldu!");
                }
            }
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            var kayitlar = DgKayitlar.ItemsSource as List<BakimKaydi>;
            if (kayitlar == null || !kayitlar.Any()) return;

            SaveFileDialog sfd = new SaveFileDialog { Filter = "PDF Dosyası|*.pdf", FileName = $"EkipmanRaporu_{DateTime.Now:yyyyMMdd}.pdf", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };

            if (sfd.ShowDialog() == true)
            {
                Document.Create(container => {
                    container.Page(page => {
                        page.Size(PageSizes.A4); page.Margin(2, Unit.Centimetre);
                        page.Header().AlignCenter().Text("EKİPMAN ÇALIŞMA RAPORU").SemiBold().FontSize(20).FontColor(Colors.Black);

                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(col => {
                            col.Spacing(10);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(120);
                                    c.RelativeColumn();
                                    c.ConstantColumn(120);
                                    c.RelativeColumn();
                                });

                                table.Cell().Border(0.5f).Padding(5).Text("Ekipman Adı").SemiBold();
                                table.Cell().ColumnSpan(3).Border(0.5f).Padding(5).Text(kayitlar.First().Name);

                                table.Cell().Border(0.5f).Padding(5).Text("Ekipman No").SemiBold();
                                table.Cell().Border(0.5f).Padding(5).Text(kayitlar.First().Equipment.ToString());

                                table.Cell().Border(0.5f).Padding(5).Text("Toplam Saat").SemiBold();
                                table.Cell().Border(0.5f).Padding(5).Text(TxtToplamSaat.Text.Replace("Toplam Çalışma Saati: ", ""));
                            });

                            if (DgCalisilmayanGunler.Visibility == Visibility.Visible)
                            {
                                col.Item().PaddingTop(15).Text(TxtCalisilmayanGunlerOzet.Text).FontSize(13).FontColor(Colors.Red.Medium).Bold().Italic();
                                col.Item().PaddingTop(5).Text("Çalışılmayan Tarihlerin Listesi:").FontSize(12).Bold().FontColor(Colors.Red.Medium);

                                col.Item().Table(t => {
                                    t.ColumnsDefinition(c => c.RelativeColumn());
                                    t.Header(h => h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Tarih").Bold());
                                    foreach (var gun in (List<DateTime>)DgCalisilmayanGunler.ItemsSource)
                                        t.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(gun.ToString("dd.MM.yyyy"));
                                });
                            }

                            col.Item().PaddingTop(20);

                            col.Item().Table(table => {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Header(h => {
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Ekipman No").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Tarih").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Saat").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Ekipman Adı").Bold();
                                });
                                foreach (var k in kayitlar)
                                {
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.Equipment.ToString());
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.Date?.ToString("dd.MM.yyyy") ?? "-");
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.WorkingHours?.ToString() ?? "0");
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.Name ?? "-");
                                }
                            });

                            col.Item().PaddingTop(10).AlignRight().Text(TxtToplamSaat.Text)
                                .FontSize(14)
                                .Bold()
                                .FontColor(PDFColors.Green.Darken2);
                        });
                        page.Footer().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(100);
                                columns.RelativeColumn();
                                columns.ConstantColumn(150);
                            });

                            table.Cell().Text("");
                            table.Cell().AlignCenter().Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); });
                            table.Cell().AlignRight().Text("Oluşturulma: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontSize(10);
                        });
                    });
                }).GeneratePdf(sfd.FileName);
                MessageBox.Show("PDF başarıyla oluşturuldu!");
            }
        }

        private void BtnGrafik_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEkipmanlar.SelectedItem == null)
            {
                MessageBox.Show("Lütfen önce bir ekipman seçin.");
                return;
            }

            // DÜZELTME: Sadece numarayı ayıkla
            string secilenEkipman = CmbEkipmanlar.SelectedItem.ToString().Split('-')[0].Trim();
            var kayitlar = dbErisimi.KayitlariFiltrele(secilenEkipman, null, null);

            GrafikEkrani grafikEkrani = new GrafikEkrani(secilenEkipman, kayitlar.ToList());
            grafikEkrani.Owner = this;
            grafikEkrani.Show();
        }

        private void BtnBakim_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEkipmanlar.SelectedItem == null) return;

            // DÜZELTME: Sadece numarayı ayıkla
            string secilenEkipman = CmbEkipmanlar.SelectedItem.ToString().Split('-')[0].Trim();

            BakimYonetimEkrani bakimEkrani = new BakimYonetimEkrani(secilenEkipman);
            bakimEkrani.Owner = this;
            bakimEkrani.Show();
        }

        private void BtnUrunTalep_Click(object sender, RoutedEventArgs e)
        {
            UrunTalepEkrani talepEkrani = new UrunTalepEkrani();
            talepEkrani.Owner = this;
            talepEkrani.ShowDialog();
        }
    }
}