using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EkipmanBakimProjesi
{
    public partial class ArizaGecmisiEkrani : Window
    {
        private string GecmisJsonYolu => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ArizaGecmisi.json");

        public ArizaGecmisiEkrani()
        {
            InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community; // PDF Kütüphanesi lisans onayı
            GecmisiYukle();
        }

        private void GecmisiYukle()
        {
            if (File.Exists(GecmisJsonYolu))
            {
                var liste = JsonSerializer.Deserialize<List<CozulenArizaModel>>(File.ReadAllText(GecmisJsonYolu));
                DgArizaGecmisi.ItemsSource = liste.OrderByDescending(x => x.CozumTarihi).ToList();
            }
        }

        // --- YENİ: EXCEL'E AKTARMA İŞLEMİ ---
        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            var liste = DgArizaGecmisi.ItemsSource as List<CozulenArizaModel>;
            if (liste == null || !liste.Any())
            {
                MessageBox.Show("Dışa aktarılacak veri bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                FileName = $"ArizaGecmisi_Raporu_{DateTime.Now:yyyyMMdd}.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (sfd.ShowDialog(this) == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Arıza Geçmişi");

                    // Başlıkları Ayarla
                    ws.Cell(1, 1).Value = "Ekipman Adı";
                    ws.Cell(1, 2).Value = "Arıza Bildirim Tarihi";
                    ws.Cell(1, 3).Value = "Çözüm Tarihi";
                    ws.Cell(1, 4).Value = "Toplam Duruş Süresi";
                    ws.Cell(1, 5).Value = "Arıza Açıklaması / Detay";

                    var baslikRange = ws.Range("A1:E1");
                    baslikRange.Style.Font.Bold = true;
                    baslikRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                    baslikRange.Style.Font.FontColor = XLColor.White;

                    // Verileri Doldur
                    int row = 2;
                    foreach (var item in liste)
                    {
                        ws.Cell(row, 1).Value = item.EkipmanAdi;
                        ws.Cell(row, 2).Value = item.BildirimTarihi.ToString("dd.MM.yyyy HH:mm");
                        ws.Cell(row, 3).Value = item.CozumTarihi.ToString("dd.MM.yyyy HH:mm");
                        ws.Cell(row, 4).Value = item.ToplamSure;
                        ws.Cell(row, 5).Value = item.Aciklama;
                        row++;
                    }

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(sfd.FileName);

                    MessageBox.Show("Arıza geçmişi başarıyla Excel'e aktarıldı!", "İşlem Tamam", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // --- YENİ: PDF ÇIKARTMA İŞLEMİ ---
        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            var liste = DgArizaGecmisi.ItemsSource as List<CozulenArizaModel>;
            if (liste == null || !liste.Any())
            {
                MessageBox.Show("Dışa aktarılacak veri bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "PDF Dosyası|*.pdf",
                FileName = $"ArizaGecmisi_Raporu_{DateTime.Now:yyyyMMdd}.pdf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (sfd.ShowDialog(this) == true)
            {
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        // PDF Üst Başlık (Header)
                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("Arıza Geçmişi ve Duruş Raporu").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text($"Rapor Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}");
                                column.Item().PaddingBottom(10);
                            });
                        });

                        // PDF Tablo İçeriği
                        page.Content().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Ekipman Adı
                                columns.RelativeColumn(2); // Bildirim Tarihi
                                columns.RelativeColumn(2); // Çözüm Tarihi
                                columns.RelativeColumn(2); // Toplam Süre
                                columns.RelativeColumn(3); // Açıklama
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#1E293B").Padding(5).Text("Ekipman Adı").FontColor(Colors.White).SemiBold();
                                header.Cell().Background("#1E293B").Padding(5).Text("Arıza Tarihi").FontColor(Colors.White).SemiBold();
                                header.Cell().Background("#1E293B").Padding(5).Text("Çözüm Tarihi").FontColor(Colors.White).SemiBold();
                                header.Cell().Background("#1E293B").Padding(5).Text("Toplam Süre").FontColor(Colors.White).SemiBold();
                                header.Cell().Background("#1E293B").Padding(5).Text("Açıklama").FontColor(Colors.White).SemiBold();
                            });

                            foreach (var item in liste)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.EkipmanAdi);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.BildirimTarihi.ToString("dd.MM.yyyy HH:mm"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.CozumTarihi.ToString("dd.MM.yyyy HH:mm"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.ToplamSure).FontColor(Colors.Red.Medium);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.Aciklama ?? "-");
                            }
                        });

                        // PDF Alt Bilgi (Sayfa No)
                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("Sayfa ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                })
                .GeneratePdf(sfd.FileName);

                MessageBox.Show("Arıza geçmişi başarıyla PDF olarak kaydedildi!", "İşlem Tamam", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
