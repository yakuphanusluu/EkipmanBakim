using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using EkipmanBakimProjesi.Data;
using EkipmanBakimProjesi.Models;
using ClosedXML.Excel;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using PDFColors = QuestPDF.Helpers.Colors;

namespace EkipmanBakimProjesi
{
    public partial class MakineKullanimRaporuEkrani : Window
    {
        private VeritabaniErisimi _dbErisimi;
        private List<MakineKullanimModel> _analizListesi;

        public MakineKullanimRaporuEkrani()
        {
            InitializeComponent();
            _dbErisimi = new VeritabaniErisimi();

            // Varsayılan olarak bu ayın başından bugüne getir
            DpBaslangic.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DpBitis.SelectedDate = DateTime.Now;

            VerileriHesapla();
        }

        private void Filtre_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded) VerileriHesapla();
        }

        private void BtnBuAy_Click(object sender, RoutedEventArgs e)
        {
            DpBaslangic.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DpBitis.SelectedDate = DateTime.Now;
            VerileriHesapla();
        }

        private void BtnTumZamanlar_Click(object sender, RoutedEventArgs e)
        {
            DpBaslangic.SelectedDate = null;
            DpBitis.SelectedDate = null;
            VerileriHesapla();
        }

        private void VerileriHesapla()
        {
            // 1. Veritabanındaki tüm benzersiz ekipman kodlarını alıyoruz (ÇÖZÜM BURADA)
            var ekipmanlar = _dbErisimi.BenzersizEkipmanlariGetir();
            if (ekipmanlar == null || !ekipmanlar.Any()) return;

            _analizListesi = new List<MakineKullanimModel>();
            List<DateTime> tumTarihler = new List<DateTime>();

            // Önce tüm ekipmanları tek tek dönüp çalışma saatlerini çekelim
            foreach (var eq in ekipmanlar)
            {
                var kayitlar = _dbErisimi.KayitlariFiltrele(eq, DpBaslangic.SelectedDate, DpBitis.SelectedDate);

                double toplamSaat = 0;
                string makineAdi = $"Makine {eq}";

                if (kayitlar != null && kayitlar.Any())
                {
                    toplamSaat = kayitlar.Sum(k => k.WorkingHours ?? 0);

                    // Makine adını kayıttan al
                    var ilkKayit = kayitlar.FirstOrDefault(k => !string.IsNullOrWhiteSpace(k.Name));
                    if (ilkKayit != null) makineAdi = ilkKayit.Name;

                    // Tüm zamanlar hesabı için tarihleri toplayalım
                    foreach (var k in kayitlar.Where(x => x.Date.HasValue))
                    {
                        tumTarihler.Add(k.Date.Value.Date);
                    }
                }
                else
                {
                    // Seçili aralıkta kayıt yoksa bile makine adını bulabilmek için genel arama yapalım
                    var genelKayitlar = _dbErisimi.KayitlariFiltrele(eq, null, null);
                    var ilkKayit = genelKayitlar?.FirstOrDefault(k => !string.IsNullOrWhiteSpace(k.Name));
                    if (ilkKayit != null) makineAdi = ilkKayit.Name;
                }

                _analizListesi.Add(new MakineKullanimModel
                {
                    EkipmanNo = eq,
                    EkipmanAdi = makineAdi,
                    ToplamCalismaSaati = toplamSaat
                });
            }

            // 2. Gün sayısını belirle (Teorik kapasite hesabı için)
            double gunSayisi = 1;
            if (DpBaslangic.SelectedDate.HasValue && DpBitis.SelectedDate.HasValue)
            {
                gunSayisi = (DpBitis.SelectedDate.Value.Date - DpBaslangic.SelectedDate.Value.Date).TotalDays + 1;
            }
            else if (tumTarihler.Any())
            {
                // "Tüm Zamanlar" seçildiğinde veritabanındaki en eski ve en yeni tarih arası gün sayısı
                DateTime minDate = tumTarihler.Min();
                DateTime maxDate = tumTarihler.Max();
                gunSayisi = (maxDate - minDate).TotalDays + 1;
            }

            if (gunSayisi < 1) gunSayisi = 1;
            double teorikMaksimumSaat = gunSayisi * 24.0;

            // 3. Ortalama, Utilization ve Bar Renklerini Hesapla
            foreach (var model in _analizListesi)
            {
                model.GunlukOrtalamaSaat = model.ToplamCalismaSaati / gunSayisi;

                double utilization = (model.ToplamCalismaSaati / teorikMaksimumSaat) * 100.0;
                if (utilization > 100) utilization = 100; // Görsel taşmayı engelle

                model.UtilizationYuzdesi = utilization;

                Brush barRengi = Brushes.DodgerBlue;
                if (utilization >= 70)
                    barRengi = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981")); // Yeşil
                else if (utilization <= 30)
                    barRengi = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444")); // Kırmızı

                model.BarRengi = barRengi;
            }

            // 4. EKRANLARI DOLDUR
            IcGrafik.ItemsSource = _analizListesi.OrderByDescending(x => x.ToplamCalismaSaati).ToList();
            DgEnCokCalisanlar.ItemsSource = _analizListesi.OrderByDescending(x => x.ToplamCalismaSaati).ToList();
            DgEnAzCalisanlar.ItemsSource = _analizListesi.OrderBy(x => x.ToplamCalismaSaati).ToList();
        }

        // --- EXCEL ÇIKTISI ---
        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_analizListesi == null || !_analizListesi.Any()) return;

            SaveFileDialog sfd = new SaveFileDialog { Filter = "Excel Dosyası|*.xlsx", FileName = $"MakineKullanimAnalizi_{DateTime.Now:yyyyMMdd}.xlsx" };
            if (sfd.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Makine Kullanım Raporu");

                    ws.Cell(1, 1).Value = "Ekipman No";
                    ws.Cell(1, 2).Value = "Makine Adı";
                    ws.Cell(1, 3).Value = "Toplam Çalışma (Saat)";
                    ws.Cell(1, 4).Value = "Günlük Ortalama (Saat)";
                    ws.Cell(1, 5).Value = "Utilization (Kullanım %)";

                    var baslik = ws.Range("A1:E1");
                    baslik.Style.Font.Bold = true;
                    baslik.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                    baslik.Style.Font.FontColor = XLColor.White;

                    int row = 2;
                    foreach (var m in _analizListesi.OrderByDescending(x => x.ToplamCalismaSaati))
                    {
                        ws.Cell(row, 1).Value = m.EkipmanNo;
                        ws.Cell(row, 2).Value = m.EkipmanAdi;
                        ws.Cell(row, 3).Value = Math.Round(m.ToplamCalismaSaati, 1);
                        ws.Cell(row, 4).Value = Math.Round(m.GunlukOrtalamaSaat, 1);
                        ws.Cell(row, 5).Value = Math.Round(m.UtilizationYuzdesi, 1) + "%";
                        row++;
                    }

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(sfd.FileName);
                    MessageBox.Show("Excel raporu başarıyla oluşturuldu!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // --- PDF ÇIKTISI ---
        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_analizListesi == null || !_analizListesi.Any()) return;

            SaveFileDialog sfd = new SaveFileDialog { Filter = "PDF Dosyası|*.pdf", FileName = $"MakineKullanimAnalizi_{DateTime.Now:yyyyMMdd}.pdf" };
            if (sfd.ShowDialog(this) == true)
            {
                Document.Create(container => {
                    container.Page(page => {
                        page.Size(PageSizes.A4); page.Margin(2, Unit.Centimetre);

                        page.Header().Column(col => {
                            col.Item().AlignCenter().Text("MAKİNE PERFORMANS VE UTILIZATION RAPORU").SemiBold().FontSize(18);
                            string tarihAraligi = DpBaslangic.SelectedDate.HasValue ? $"{DpBaslangic.SelectedDate:dd.MM.yyyy} - {DpBitis.SelectedDate:dd.MM.yyyy}" : "Tüm Zamanlar";
                            col.Item().AlignCenter().Text($"Analiz Aralığı: {tarihAraligi}").FontSize(12).FontColor(PDFColors.Grey.Darken2);
                        });

                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(col => {
                            col.Spacing(15);

                            var enCok = _analizListesi.OrderByDescending(x => x.ToplamCalismaSaati).FirstOrDefault();
                            var enAz = _analizListesi.OrderBy(x => x.ToplamCalismaSaati).FirstOrDefault();

                            col.Item().Table(table => {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                                table.Cell().Border(0.5f).Padding(8).Text($"En Çok Çalışan: {enCok?.EkipmanAdi} ({Math.Round(enCok?.ToplamCalismaSaati ?? 0, 1)} saat)").Bold().FontColor(PDFColors.Black);
                                table.Cell().Border(0.5f).Padding(8).Text($"En Az Çalışan: {enAz?.EkipmanAdi} ({Math.Round(enAz?.ToplamCalismaSaati ?? 0, 1)} saat)").Bold().FontColor(PDFColors.Black);
                            });

                            col.Item().Table(table => {
                                table.ColumnsDefinition(c => { c.ConstantColumn(80); c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Header(h => {
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Ekipman No").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Makine Adı").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Toplam Saat").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Padding(5).Text("Utilization").Bold();
                                });
                                foreach (var m in _analizListesi.OrderByDescending(x => x.ToplamCalismaSaati))
                                {
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(m.EkipmanNo);
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(m.EkipmanAdi);
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text($"{Math.Round(m.ToplamCalismaSaati, 1)} sa");
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(m.UtilizationMetin);
                                }
                            });
                        });

                        page.Footer().AlignCenter().Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); });
                    });
                }).GeneratePdf(sfd.FileName);

                MessageBox.Show("PDF Raporu başarıyla oluşturuldu!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Kapanan pencerenin sahibi (ana ekran) varsa ona odaklanmayı zorla
            if (this.Owner != null)
            {
                this.Owner.Activate();
            }
        }
    }
}