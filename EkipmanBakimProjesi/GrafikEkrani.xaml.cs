using LiveCharts;
using LiveCharts.Defaults; // ObservablePoint için eklendi
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EkipmanBakimProjesi.Models;

namespace EkipmanBakimProjesi
{
    public partial class GrafikEkrani : Window
    {
        public SeriesCollection SeriesCollection { get; set; }
        public string[] Tarihler { get; set; }
        private List<BakimKaydi> _siraliKayitlar;

        public GrafikEkrani(string ekipmanNo, List<BakimKaydi> kayitlar)
        {
            InitializeComponent();
            TxtBaslik.Text = $"Ekipman No: {ekipmanNo} - Çalışma Grafiği";

            _siraliKayitlar = kayitlar.Where(k => k.Date.HasValue).OrderBy(k => k.Date.Value).ToList();

            // 1. KİLİT NOKTASI: Formatter sadece tam sayılarda yazı göstersin, ara değerlerde boş dönsün
            AxisX.LabelFormatter = value => {
                double rounded = Math.Round(value);

                // Eğer LiveCharts ara bir değer gönderirse (örn: 1.5), onu yoksay. 
                // Sadece tam sayılara (0, 1, 2) denk geldiğinde tarihi bas.
                if (Math.Abs(value - rounded) < 0.01)
                {
                    int index = (int)rounded;
                    if (index >= 0 && index < _siraliKayitlar.Count)
                    {
                        return _siraliKayitlar[index].Date.Value.ToString("dd.MM.yyyy");
                    }
                }
                return ""; // Ara boşluklarda kaymayı engellemek için boş dön
            };

            // 2. KİLİT NOKTASI: X değerlerini (gün indeksini) manuel olarak noktalara çiviliyoruz
            var points = new ChartValues<ObservablePoint>();
            for (int i = 0; i < _siraliKayitlar.Count; i++)
            {
                double saat = _siraliKayitlar[i].WorkingHours.HasValue ? Convert.ToDouble(_siraliKayitlar[i].WorkingHours.Value) : 0.0;
                points.Add(new ObservablePoint(i, saat)); // X = i (0, 1, 2...), Y = saat
            }

            SeriesCollection = new SeriesCollection {
                new LineSeries {
                    Title = "Çalışma Saati",
                    Values = points,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 12,
                    LineSmoothness = 0,
                    StrokeThickness = 3,
                    Stroke = System.Windows.Media.Brushes.DodgerBlue,
                    Fill = System.Windows.Media.Brushes.Transparent,

                    DataLabels = true,
                    
                    // --- 3. GÜNCELLEME: YIĞILMA ÖNLEYİCİ AKILLI ETİKET ---
                    LabelPoint = p => {
                        // Güvenlik: Başlangıçta Min/Max boşsa diye kontrol
                        double min = double.IsNaN(AxisX.MinValue) ? 0 : AxisX.MinValue;
                        double max = double.IsNaN(AxisX.MaxValue) ? int.MaxValue : AxisX.MaxValue;

                        // Nokta, ekrandaki günlerin içindeyse yazıyı göster
                        if (p.X >= min && p.X <= max)
                            return $"{p.Y} Saat";
                        
                        // Ekrandan çıkmışsa boş gönder (Böylece sağa/sola yığılmaz, silinir)
                        return "";
                    },

                    DataLabelsTemplate = (DataTemplate)FindResource("ModernEtiket")
                }
            };

            // ... Kaydırma ve Görünürlük ayarları ...
            int gorunenVeri = 7;
            if (_siraliKayitlar.Count > 0)
            {
                if (_siraliKayitlar.Count <= gorunenVeri)
                {
                    AxisX.MinValue = 0;
                    AxisX.MaxValue = _siraliKayitlar.Count - 1;
                    HScroll.Visibility = Visibility.Collapsed;
                }
                else
                {
                    HScroll.Visibility = Visibility.Visible;
                    HScroll.Minimum = 0;
                    HScroll.Maximum = _siraliKayitlar.Count - gorunenVeri;
                    HScroll.Value = HScroll.Maximum;
                    AxisX.MinValue = HScroll.Value;
                    AxisX.MaxValue = HScroll.Value + (gorunenVeri - 1);
                }
            }

            DataContext = this;
            Chart.Series = SeriesCollection;
        }

        private void HScroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int gorunenVeri = 7;

            // 3. KİLİT NOKTASI: Kaydırma çubuğunun ondalıklı sayı vermesini engelleyip tam sayıya zorluyoruz
            int val = (int)Math.Round(HScroll.Value);

            AxisX.MinValue = val;
            AxisX.MaxValue = val + (gorunenVeri - 1);
        }
    }
}