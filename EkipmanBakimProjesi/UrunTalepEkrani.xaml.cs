using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace EkipmanBakimProjesi
{
    public class UrunTalepModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UrunAdi { get; set; }
        public int Miktar { get; set; }
        public string KullanimYeri { get; set; }
        public string TalepEdenKisi { get; set; } // YENİ ÖZELLİK
        public DateTime TalepTarihi { get; set; }
    }

    public partial class UrunTalepEkrani : Window
    {
        private ObservableCollection<UrunTalepModel> _talepListesi;
        private string JsonDosyaYolu => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UrunTalepleri.json");

        public UrunTalepEkrani()
        {
            InitializeComponent();
            VerileriYukle();
        }

        private void VerileriYukle()
        {
            if (File.Exists(JsonDosyaYolu))
            {
                var json = File.ReadAllText(JsonDosyaYolu);
                var liste = JsonSerializer.Deserialize<List<UrunTalepModel>>(json) ?? new List<UrunTalepModel>();
                _talepListesi = new ObservableCollection<UrunTalepModel>(liste.OrderByDescending(x => x.TalepTarihi));
            }
            else
            {
                _talepListesi = new ObservableCollection<UrunTalepModel>();
            }

            DgTalepler.ItemsSource = _talepListesi;
        }

        private void VerileriKaydet()
        {
            var json = JsonSerializer.Serialize(_talepListesi, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(JsonDosyaYolu, json);
        }

        private void BtnEkle_Click(object sender, RoutedEventArgs e)
        {
            // YENİ: Kontrollere TxtTalepEden de eklendi
            if (string.IsNullOrWhiteSpace(TxtUrunAdi.Text) || string.IsNullOrWhiteSpace(TxtKullanimYeri.Text) || string.IsNullOrWhiteSpace(TxtTalepEden.Text))
            {
                MessageBox.Show("Lütfen tüm alanları doldurun.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtAdet.Text, out int miktar) || miktar <= 0)
            {
                MessageBox.Show("Lütfen geçerli bir adet girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var yeniTalep = new UrunTalepModel
            {
                UrunAdi = TxtUrunAdi.Text.Trim(),
                Miktar = miktar,
                KullanimYeri = TxtKullanimYeri.Text.Trim(),
                TalepEdenKisi = TxtTalepEden.Text.Trim(), // YENİ
                TalepTarihi = DateTime.Now
            };

            _talepListesi.Insert(0, yeniTalep);
            VerileriKaydet();

            TxtUrunAdi.Clear();
            TxtAdet.Clear();
            TxtKullanimYeri.Clear();
            TxtTalepEden.Clear(); // YENİ

            MessageBox.Show("Talep başarıyla eklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.DataContext is UrunTalepModel seciliTalep)
            {
                var cevap = MessageBox.Show($"'{seciliTalep.UrunAdi}' talebini silmek istediğinize emin misiniz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (cevap == MessageBoxResult.Yes)
                {
                    _talepListesi.Remove(seciliTalep);
                    VerileriKaydet();
                }
            }
        }

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_talepListesi == null || !_talepListesi.Any())
            {
                MessageBox.Show("Excel'e aktarılacak veri bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "Excel Dosyası|*.xlsx",
                FileName = $"UrunTalepListesi_{DateTime.Now:yyyyMMdd}.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            // Bir önceki odak probleminden dolayı 'this' parametresini eklemeyi unutmadım kanka
            if (sfd.ShowDialog(this) == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Ürün Talepleri");

                    // Başlıklar (E sütununa yeni başlık eklendi)
                    ws.Cell(1, 1).Value = "Talep Tarihi";
                    ws.Cell(1, 2).Value = "Ürün / Parça Adı";
                    ws.Cell(1, 3).Value = "Miktar";
                    ws.Cell(1, 4).Value = "Kullanım Yeri";
                    ws.Cell(1, 5).Value = "Talep Eden Kişi"; // YENİ

                    var baslik = ws.Range("A1:E1");
                    baslik.Style.Font.Bold = true;
                    baslik.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                    baslik.Style.Font.FontColor = XLColor.White;

                    int row = 2;
                    foreach (var t in _talepListesi)
                    {
                        ws.Cell(row, 1).Value = t.TalepTarihi.ToString("dd.MM.yyyy HH:mm");
                        ws.Cell(row, 2).Value = t.UrunAdi;
                        ws.Cell(row, 3).Value = t.Miktar;
                        ws.Cell(row, 4).Value = t.KullanimYeri;
                        ws.Cell(row, 5).Value = t.TalepEdenKisi; // YENİ
                        row++;
                    }

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(sfd.FileName);

                    // --- ÇÖZÜM BURADA: Kayıt başarılı olduktan sonra listeyi sıfırlıyoruz ---
                    _talepListesi.Clear(); // Ekranı temizler
                    VerileriKaydet();     // JSON dosyasını sıfırlar

                    MessageBox.Show("Talep listesi başarıyla Excel'e aktarıldı ve bekleyen talepler sıfırlandı!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}