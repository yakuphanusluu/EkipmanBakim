using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using EkipmanBakimProjesi.Models;
using System.Linq;

namespace EkipmanBakimProjesi.Data
{
    public class VeritabaniErisimi
    {
        public static string BaglantiMetni { get; set; }
        public static string TabloAdi { get; set; }

        public bool BaglantiyiTestEt(string connectionString)
        {
            try
            {
                using (IDbConnection db = new SqlConnection(connectionString))
                {
                    db.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public List<string> BenzersizEkipmanlariGetir()
        {
            // Bağlantı metni boşsa veya tablo seçilmemişse boş liste dön
            if (string.IsNullOrEmpty(BaglantiMetni) || string.IsNullOrEmpty(TabloAdi))
                return new List<string>();

            try
            {
                using (IDbConnection db = new SqlConnection(BaglantiMetni))
                {
                    string sql = $"SELECT DISTINCT [Equipment] FROM {TabloAdi} WHERE [Equipment] IS NOT NULL ORDER BY [Equipment]";
                    return db.Query<int>(sql).Select(e => e.ToString()).ToList();
                }
            }
            catch
            {
                // Bağlantı hatası veya yetkisiz erişim durumunda boş liste döner
                return new List<string>();
            }
        }

        public List<BakimKaydi> KayitlariFiltrele(string ekipmanNo, DateTime? baslangic, DateTime? bitis)
        {
            if (string.IsNullOrEmpty(BaglantiMetni) || string.IsNullOrEmpty(TabloAdi))
                return new List<BakimKaydi>();

            try
            {
                using (IDbConnection db = new SqlConnection(BaglantiMetni))
                {
                    string sql = $"SELECT * FROM {TabloAdi} WHERE [Equipment] = @EkipmanNo";

                    if (baslangic.HasValue && bitis.HasValue)
                    {
                        sql += " AND [Date] >= @Baslangic AND [Date] <= @Bitis";
                    }

                    sql += " ORDER BY [Date] ASC";

                    return db.Query<BakimKaydi>(sql, new
                    {
                        EkipmanNo = int.Parse(ekipmanNo),
                        Baslangic = baslangic,
                        Bitis = bitis
                    }).ToList();
                }
            }
            catch
            {
                // Veritabanı şifresi yanlışsa veya erişim engeli varsa uygulama çökmez, boş liste döner
                return new List<BakimKaydi>();
            }
        }

        // VeritabaniErisimi.cs dosyasındaki sınıfın içine ekle:
        public static string AktifVeritabaniAdi
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(BaglantiMetni)) return "VarsayilanDB";

                    // Bağlantı cümlesini parçalayıp InitialCatalog (Veritabanı Adı) değerini alıyoruz
                    var builder = new SqlConnectionStringBuilder(BaglantiMetni);
                    return builder.InitialCatalog;
                }
                catch
                {
                    return "VarsayilanDB";
                }
            }
        }
    }
}