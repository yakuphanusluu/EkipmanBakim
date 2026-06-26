using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Microsoft.Data.SqlClient;
using EkipmanBakimProjesi.Data;

namespace EkipmanBakimProjesi
{
    public partial class GirisEkrani : Window
    {
        // Son girilen bilgileri uygulama açık kaldığı sürece hafızada tutacak statik değişkenler
        public static string KayitliSunucu { get; set; } = "";
        public static string KayitliKullanici { get; set; } = "";
        public static string KayitliSifre { get; set; } = "";

        public GirisEkrani()
        {
            InitializeComponent();

            // Eğer daha önce bir kayıt varsa doldur, yoksa boş kalsın
            TxtSunucu.Text = KayitliSunucu;
            TxtUser.Text = KayitliKullanici;
            TxtPass.Password = KayitliSifre;
        }

        private string GetConnString(string dbName = "master")
        {
            if (string.IsNullOrWhiteSpace(TxtUser.Text))
                return $"Server={TxtSunucu.Text};Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;";

            return $"Server={TxtSunucu.Text};Database={dbName};User Id={TxtUser.Text};Password={TxtPass.Password};TrustServerCertificate=True;";
        }

        // Metin kutularındaki veriyi statik değişkenlere kaydeden yardımcı metot
        private void BilgileriHafizayaAl()
        {
            KayitliSunucu = TxtSunucu.Text;
            KayitliKullanici = TxtUser.Text;
            KayitliSifre = TxtPass.Password;
        }

        private void BtnDbGetir_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSunucu.Text))
            {
                MessageBox.Show("Lütfen bir sunucu adı girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BilgileriHafizayaAl(); // İşlem yapıldığı an son girilen bilgileri kaydet

            try
            {
                using (IDbConnection db = new SqlConnection(GetConnString("master")))
                {
                    var dbs = db.Query<string>("SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name").ToList();
                    CmbVeritabanlari.ItemsSource = dbs;
                    MessageBox.Show("Veritabanları başarıyla listelendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Bağlantı başarısız:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbVeritabanlari_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbVeritabanlari.SelectedItem == null) return;

            try
            {
                string secilenDb = CmbVeritabanlari.SelectedItem.ToString();
                using (IDbConnection db = new SqlConnection(GetConnString(secilenDb)))
                {
                    var tablolar = db.Query<string>("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME").ToList();
                    CmbTablolar.ItemsSource = tablolar;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tablolar alınamadı:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBaslat_Click(object sender, RoutedEventArgs e)
        {
            if (CmbVeritabanlari.SelectedItem == null || CmbTablolar.SelectedItem == null)
            {
                MessageBox.Show("Lütfen Veritabanı ve Tablo seçimi yapın.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BilgileriHafizayaAl(); // Uygulamayı başlatırken de verileri kaydetmeyi unutma

            VeritabaniErisimi.BaglantiMetni = GetConnString(CmbVeritabanlari.SelectedItem.ToString());
            VeritabaniErisimi.TabloAdi = CmbTablolar.SelectedItem.ToString();

            new MainWindow().Show();
            this.Close();
        }
    }
}