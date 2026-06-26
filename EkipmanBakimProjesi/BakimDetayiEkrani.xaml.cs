using System;
using System.Windows;

namespace EkipmanBakimProjesi
{
    public partial class BakimDetayiEkrani : Window
    {
        // Sayfa açılırken tıklanan kaydın bilgilerini (BakimLogModel) parametre olarak alıyoruz
        public BakimDetayiEkrani(BakimLogModel seciliKayit)
        {
            InitializeComponent();

            if (seciliKayit != null)
            {
                TxtEkipmanNo.Text = seciliKayit.EkipmanNo;
                TxtTarih.Text = seciliKayit.BakimTarihi.ToString("dd.MM.yyyy");
                TxtKisi.Text = string.IsNullOrWhiteSpace(seciliKayit.BakimYapanKisi) ? "-" : seciliKayit.BakimYapanKisi;
                TxtPeriyot.Text = $"{seciliKayit.BakimPeriyodu} Saat";

                // Eğer açıklama boşsa "Not girilmemiş" yazsın
                TxtAciklama.Text = string.IsNullOrWhiteSpace(seciliKayit.Aciklama)
                    ? "Bu bakım için herhangi bir not girilmemiş."
                    : seciliKayit.Aciklama;
            }
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}