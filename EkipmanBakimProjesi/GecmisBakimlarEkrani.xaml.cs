using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using EkipmanBakimProjesi.Data;
using ClosedXML.Excel;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// YENİ EKLENEN TAKMA AD: WPF ve QuestPDF renk çakışmasını engellemek için
using PDFColors = QuestPDF.Helpers.Colors;

namespace EkipmanBakimProjesi
{
    public partial class GecmisBakimlarEkrani : Window
    {
        // Artık sabit (readonly) değil, aktif veritabanı ismine göre dinamik dosya yolunu alıyor.
        private string JsonDosyaYolu => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"BakimLoglari_{VeritabaniErisimi.AktifVeritabaniAdi}.json");

        private List<BakimLogModel> _tumKayitlar;
        private VeritabaniErisimi _dbErisimi;

        public GecmisBakimlarEkrani()
        {
            InitializeComponent();
            _dbErisimi = new VeritabaniErisimi();
            VerileriYukle();
        }

        private void VerileriYukle()
        {
            // Verileri JSON'dan oku
            if (File.Exists(JsonDosyaYolu))
            {
                var json = File.ReadAllText(JsonDosyaYolu);
                _tumKayitlar = JsonSerializer.Deserialize<List<BakimLogModel>>(json) ?? new List<BakimLogModel>();
            }
            else
            {
                _tumKayitlar = new List<BakimLogModel>();
            }

            // Benzersiz numaraları çek
            var numaralar = _dbErisimi.BenzersizEkipmanlariGetir();
            List<string> ekipmanListesi = new List<string> { "Tüm Ekipmanlar" };

            foreach (var no in numaralar)
            {
                // ÇÖZÜM: Her makineyi kendi numarasıyla veritabanından canlı sorguluyoruz
                var kayitlar = _dbErisimi.KayitlariFiltrele(no, null, null);

                string isim = "Tanımsız Makine";
                if (kayitlar != null && kayitlar.Any())
                {
                    var makine = kayitlar.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name));
                    if (makine != null) isim = makine.Name;
                }

                ekipmanListesi.Add($"{no} - {isim}");
            }

            CmbEkipman.ItemsSource = ekipmanListesi;
            CmbEkipman.SelectedIndex = 0;

            // Bakım Yapan Kişi ComboBox'ını Doldur
            var kisiListesi = _tumKayitlar.Select(x => x.BakimYapanKisi).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            kisiListesi.Insert(0, "Tüm Kişiler");
            CmbKisi.ItemsSource = kisiListesi;
            CmbKisi.SelectedIndex = 0;

            Filtrele();
        }

        private void Filtre_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Eğer form yüklenirken tetiklenirse hata vermemesi için
            if (_tumKayitlar != null) Filtrele();
        }

        private void Filtrele()
        {
            var filtrelenmisListe = _tumKayitlar.AsEnumerable();

            // Ekipman Filtresi
            if (CmbEkipman.SelectedIndex > 0)
            {
                // --- DEĞİŞİKLİK BURADA: Sadece Numarayı çekip filtreliyoruz ---
                string seciliTamMetin = CmbEkipman.SelectedItem.ToString();
                string seciliNo = seciliTamMetin.Split('-')[0].Trim();
                filtrelenmisListe = filtrelenmisListe.Where(x => x.EkipmanNo == seciliNo);
            }

            // Kişi Filtresi
            if (CmbKisi.SelectedIndex > 0)
            {
                string seciliKisi = CmbKisi.SelectedItem.ToString();
                filtrelenmisListe = filtrelenmisListe.Where(x => x.BakimYapanKisi == seciliKisi);
            }

            // Tarih Filtresi
            if (DpBaslangic.SelectedDate.HasValue)
            {
                filtrelenmisListe = filtrelenmisListe.Where(x => x.BakimTarihi.Date >= DpBaslangic.SelectedDate.Value.Date);
            }
            if (DpBitis.SelectedDate.HasValue)
            {
                filtrelenmisListe = filtrelenmisListe.Where(x => x.BakimTarihi.Date <= DpBitis.SelectedDate.Value.Date);
            }

            // Sonuçları listeye al
            var sonListe = filtrelenmisListe.OrderByDescending(x => x.BakimTarihi).ToList();
            DgGecmisBakimlar.ItemsSource = sonListe;

            // Liste boşsa tabloyu gizle, uyarıyı göster
            if (sonListe.Count == 0)
            {
                DgGecmisBakimlar.Visibility = Visibility.Collapsed;

                if (TxtUyari != null)
                {
                    TxtUyari.Visibility = Visibility.Visible;
                }
            }
            else
            {
                DgGecmisBakimlar.Visibility = Visibility.Visible;

                if (TxtUyari != null)
                {
                    TxtUyari.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnTemizle_Click(object sender, RoutedEventArgs e)
        {
            CmbEkipman.SelectedIndex = 0;
            CmbKisi.SelectedIndex = 0;
            DpBaslangic.SelectedDate = null;
            DpBitis.SelectedDate = null;
            Filtrele();
        }

        private void BtnDetay_Click(object sender, RoutedEventArgs e)
        {
            // Tıklanan butonu bul
            Button tıklananButon = sender as Button;

            // Butonun ait olduğu satırdaki veriyi (BakimLogModel) al
            if (tıklananButon != null && tıklananButon.DataContext is BakimLogModel seciliKayit)
            {
                // Veriyi yeni oluşturduğumuz Detay Ekranına gönder ve ekranı aç
                BakimDetayiEkrani detayEkrani = new BakimDetayiEkrani(seciliKayit);
                detayEkrani.Owner = this;
                detayEkrani.ShowDialog();
            }
        }

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            // Tablodaki veriyi al
            var kayitlar = DgGecmisBakimlar.ItemsSource as IEnumerable<BakimLogModel>;
            if (kayitlar == null || !kayitlar.Any())
            {
                MessageBox.Show("Dışa aktarılacak veri bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Klasik Excel kaydetme penceresi
            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                FileName = $"GecmisBakimlar_Raporu_{DateTime.Now:yyyyMMdd}.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (sfd.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Geçmiş Bakımlar");

                    // 1. Başlıklar ve Kenarlıklar
                    worksheet.Cell(1, 1).Value = "Ekipman Numarası";
                    worksheet.Cell(1, 2).Value = "Son Bakım Tarihi";
                    worksheet.Cell(1, 3).Value = "Bakımı Yapan Kişi";
                    worksheet.Cell(1, 4).Value = "Periyot (Saat)";
                    worksheet.Cell(1, 5).Value = "Bakım Detayı";

                    var baslikRange = worksheet.Range("A1:E1");
                    baslikRange.Style.Font.Bold = true;
                    baslikRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    baslikRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    baslikRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D0D3D4"); // Şık bir gri arka plan

                    // 2. Verileri Yazdırma
                    int row = 2;
                    foreach (var k in kayitlar)
                    {
                        worksheet.Cell(row, 1).Value = k.EkipmanNo;
                        worksheet.Cell(row, 2).Value = k.BakimTarihi.ToString("dd.MM.yyyy");
                        worksheet.Cell(row, 3).Value = k.BakimYapanKisi ?? "-";
                        worksheet.Cell(row, 4).Value = k.BakimPeriyodu;
                        worksheet.Cell(row, 5).Value = k.Aciklama ?? "-";

                        // Satır için kenarlık
                        var range = worksheet.Range(row, 1, row, 5);
                        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                        row++;
                    }

                    // Sütun genişliklerini otomatik ayarla ve kaydet
                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(sfd.FileName);
                    MessageBox.Show("Excel raporu başarıyla oluşturuldu!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            var kayitlar = DgGecmisBakimlar.ItemsSource as IEnumerable<BakimLogModel>;
            if (kayitlar == null || !kayitlar.Any())
            {
                MessageBox.Show("PDF'e yazdırılacak veri bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- BAŞLIK TABLOSU İÇİN AKILLI VERİ HAZIRLIĞI ---
            // Eğer tüm listedeki ekipman numarası aynıysa o numarayı al, değilse "Karışık / Tümü" yaz
            string hEkipmanNo = kayitlar.Select(x => x.EkipmanNo).Distinct().Count() == 1 ? kayitlar.First().EkipmanNo : "Karışık / Tümü";
            string hEkipmanAdi = "Karışık / Tümü";

            if (hEkipmanNo != "Karışık / Tümü")
            {
                // Ekipmanın adını veritabanından çekiyoruz
                var dbKayit = _dbErisimi.KayitlariFiltrele(hEkipmanNo, null, null).FirstOrDefault();
                hEkipmanAdi = dbKayit?.Name ?? "Tanımsız";
            }

            string hKisi = kayitlar.Select(x => x.BakimYapanKisi).Distinct().Count() == 1 ? kayitlar.First().BakimYapanKisi : "Karışık / Tümü";
            string hPeriyot = kayitlar.Select(x => x.BakimPeriyodu).Distinct().Count() == 1 ? kayitlar.First().BakimPeriyodu.ToString() + " Saat" : "Karışık";
            // ------------------------------------------------

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "PDF Dosyası|*.pdf",
                FileName = $"GecmisBakimlar_Raporu_{DateTime.Now:yyyyMMdd}.pdf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (sfd.ShowDialog() == true)
            {
                Document.Create(container => {
                    container.Page(page => {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);

                        page.Header().AlignCenter().Text("GEÇMİŞ BAKIM KAYITLARI RAPORU").SemiBold().FontSize(18).FontColor(PDFColors.Black);

                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(col => {
                            col.Spacing(10);

                            // --- ÖZET KÜNYE TABLOSU ---
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(120);
                                    c.RelativeColumn();
                                    c.ConstantColumn(120);
                                    c.RelativeColumn();
                                });

                                // 1. Satır: Ekipman Adı
                                table.Cell().Border(0.5f).Padding(5).Text("Ekipman Adı").SemiBold();
                                table.Cell().ColumnSpan(3).Border(0.5f).Padding(5).Text(hEkipmanAdi);

                                // 2. Satır: Ekipman No & Bakım Periyodu
                                table.Cell().Border(0.5f).Padding(5).Text("Bakım Periyodu").SemiBold();
                                table.Cell().Border(0.5f).Padding(5).Text(hPeriyot);

                                // 3. Satır: Bakımı Yapan
                                table.Cell().Border(0.5f).Padding(5).Text("Bakımı Yapan").SemiBold();
                                table.Cell().Border(0.5f).Padding(5).Text(hKisi);
                            });

                            col.Item().PaddingTop(10);

                            // Özet Metni
                            col.Item().Text($"Listelenen Toplam Bakım Kaydı: {kayitlar.Count()} adet").FontSize(12).Italic().FontColor(PDFColors.Grey.Darken2);
                            col.Item().PaddingTop(5);

                            // Ana Veri Tablosu
                            col.Item().Table(table => {
                                table.ColumnsDefinition(c => {
                                    c.ConstantColumn(80); // Ekipman No
                                    c.ConstantColumn(90); // Tarih 
                                    c.RelativeColumn(2);  // Bakımı Yapan
                                    c.ConstantColumn(50); // Saat
                                    c.RelativeColumn(3);  // Açıklama
                                });

                                table.Header(h => {
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Border(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text("Ekipman Numarası").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Border(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text("Bakım Tarihi").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Border(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text("Bakımı Yapan").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Border(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text("Periyot (Saat)").Bold();
                                    h.Cell().Background(PDFColors.Grey.Lighten2).Border(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text("Bakım Detayı").Bold();
                                });

                                foreach (var k in kayitlar)
                                {
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.EkipmanNo);
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.BakimTarihi.ToString("dd.MM.yyyy"));
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.BakimYapanKisi ?? "-");
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.BakimPeriyodu.ToString());
                                    table.Cell().BorderBottom(0.5f).BorderColor(PDFColors.Grey.Lighten1).Padding(5).Text(k.Aciklama ?? "-");
                                }
                            });
                        });

                        // Alt Bilgi (Footer)
                        page.Footer().Table(table => {
                            table.ColumnsDefinition(columns => {
                                columns.ConstantColumn(100); columns.RelativeColumn(); columns.ConstantColumn(150);
                            });
                            table.Cell().Text("");
                            table.Cell().AlignCenter().Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); });
                            table.Cell().AlignRight().Text("Oluşturulma: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontSize(10);
                        });
                    });
                }).GeneratePdf(sfd.FileName);

                MessageBox.Show("PDF başarıyla oluşturuldu!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}