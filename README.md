# 🧭 LeadFinder.Api – Smart Business Discovery & Audit System

## 🚀 Genel Tanım
**LeadFinder**, belirli bir şehir veya ilçe içindeki potansiyel işletmeleri **OpenStreetMap (OSM)** verileri üzerinden keşfeden ve her işletme için web sitesi analizleri yapan bir sistemdir.  
Amaç, *“lokasyon tabanlı lead discovery + otomatik site denetimi”* mantığıyla çalışan, ölçeklenebilir bir servis altyapısı kurmaktır.

---

## 🧩 Sistem Bileşenleri

| Katman | Açıklama |
|--------|-----------|
| **API Layer (LeadFinder.Api)** | REST endpoint’leri barındırır. Scan isteklerini kuyruklar ve sonuçları veritabanına yazar. |
| **Background Worker (ScanWorker)** | Kuyruktaki istekleri asenkron işler. Discovery + Site Audit süreçlerini tetikler. |
| **DiscoveryService** | Şehir / ilçe ve anahtar kelimeye göre işletme keşfi yapar (ör. “Bağcılar / parke”). |
| **OsmpPlacesProvider** | OpenStreetMap + Overpass API üzerinden işletme verilerini çeker. |
| **SiteAuditService** | Keşfedilen işletmelerin web sitelerini tarar ve HTTPS, e-posta, telefon, iletişim sayfası vb. sinyalleri çıkarır. |
| **AppDbContext (EF Core)** | Tüm sonuçların MySQL veritabanına kaydını sağlar. |
| **BackgroundQueue** | API ile Worker arasında thread-safe görev kuyruğu sağlar. |

---

## ⚙️ Kullanılan Teknolojiler

| Teknoloji | Rolü |
|------------|------|
| **.NET 8 (ASP.NET Core)** | API ve Worker altyapısı |
| **Entity Framework Core** | ORM / Database erişimi |
| **MySQL (Docker)** | Kalıcı veri deposu |
| **OpenStreetMap + Overpass API** | İşletme keşfi verileri |
| **Nominatim API** | Şehir ve ilçe koordinat çözümleme |
| **HttpClientFactory** | Güvenli ve thread-safe HTTP istekleri |
| **Serilog / ILogger** | Loglama altyapısı |
| **C# 12 Record Types & Async Pattern** | Temiz ve modern kod yapısı |

---

## 🧠 Sistemin Çalışma Mantığı

```text
[HTTP Request]
   ↓
[ScanEndpoint]  --->  [BackgroundQueue.Enqueue()]
                           ↓
                   [ScanWorker.ExecuteAsync()]
                           ↓
       ┌──────────────────────────────────────────┐
       │ DiscoveryService → OsmpPlacesProvider    │
       │  ↓                                       │
       │  OpenStreetMap & Overpass API            │
       │  → işletme adayları (adı, adresi, site) │
       └──────────────────────────────────────────┘
                           ↓
            [SiteAuditService → HttpClient]
            ↓
     HTTPS kontrolü, iletişim sinyalleri,
     başlık, e-posta, telefon analizleri
                           ↓
        [AppDbContext → MySQL] (veritabanı kaydı)
```

---

## ✅ Tamamlanan Modüller

| Modül | Durum | Açıklama |
|--------|--------|-----------|
| **ScanEndpoint** | ✅ | İstek alıp kuyruklayan endpoint. |
| **BackgroundQueue** | ✅ | Thread-safe job kuyruğu. |
| **ScanWorker** | ✅ | Kuyruktaki işlemleri arka planda yürütüyor. |
| **SiteAuditService** | ✅ | Web sitesi analiz modülü (HTTPS, meta, iletişim). |
| **OsmpPlacesProvider** | ✅ | OSM ve Overpass API entegrasyonu, hata toleranslı sorgular. |
| **MySQL Docker DB** | ✅ | Bağlantı ve EF Core yapılandırması. |
| **Retry / Timeout mekanizması** | ✅ | Overpass API isteği yeniden deneme (backoff) mantığı. |

---

## 🧭 Yapılacaklar (Next Steps)

| Başlık | Açıklama |
|---------|-----------|
| 🗂 **Veritabanı Modeli Genişletme** | `Website`, `ScanRequest`, `PlaceResult` tabloları normalize edilecek. |
| 🌐 **Front-end Dashboard (SPA)** | React / Vue tabanlı arayüz: arama, sonuç listesi, skor gösterimi. |
| 📡 **Kategori Bazlı Arama Optimizasyonu** | `KeywordToOverpassFilters` dinamik hale getirilecek (ML veya JSON mapping). |
| 🧮 **Skorlama Sistemi Geliştirme** | SiteAudit puanlaması: hız, SEO, HTTPS sertifikası, içerik vb. |
| 🔄 **Cache & Rate Limit Handling** | OSM API çağrılarında caching ve limit koruması. |
| 📊 **Raporlama / Export Modülü** | Excel / CSV olarak lead verisi export edilebilecek. |
| 🧰 **CI/CD & Docker Compose** | Geliştirme ortamı ve production için Compose dosyası hazırlanacak. |

---

## 🧱 Kurulum (Development Environment)

```bash
# 1️⃣ Docker MySQL başlat
docker run -d --name leadfinder-mysql   -e MYSQL_ROOT_PASSWORD=1234   -e MYSQL_DATABASE=leadfinder   -p 3306:3306 mysql:8

# 2️⃣ .NET tarafı
cd LeadFinder.Api
dotnet restore
dotnet ef database update
dotnet run
```

API otomatik olarak şu adreste çalışır:  
👉 **http://localhost:5203**

---

## 🧾 Örnek Kullanım

```bash
POST /scan
{
  "city": "Bağcılar",
  "keyword": "parke"
}
```

**Sistem Akışı:**
1. `ScanWorker` isteği kuyruktan alır.
2. `OsmpPlacesProvider` → OSM / Overpass sorgusunu çalıştırır.
3. Bulunan işletmelerin adres ve web site bilgileri alınır.
4. `SiteAuditService` her web sitesini test eder.
5. Sonuçlar MySQL veritabanına kaydedilir.

---

## 🧩 Örnek Log Çıktısı

```
info: ScanEndpoint[0]
      Queued scan: Bağcılar / parke (0 urls)
info: OsmpPlacesProvider[0]
      OSM returned 12 elements for 'Bağcılar' / 'parke'
info: SiteAuditService[0]
      Audited site: https://abcparke.com  [Score: 29]
```

---

## 📘 Proje Yapısı

```
LeadFinder.Api/
│
├── Data/
│   └── AppDbContext.cs
│
├── Endpoints/
│   └── ScanEndpoint.cs
│
├── Services/
│   ├── Discovery/
│   │   ├── DiscoveryService.cs
│   │   └── Providers/Osmp/OsmpPlacesProvider.cs
│   ├── SiteAudit/
│   │   ├── SiteAuditService.cs
│   │   └── SiteAuditHelpers.cs
│   └── Queue/
│       └── BackgroundQueue.cs
│
└── Program.cs
```

---

## 👨‍💻 Yazar

**Cengizhan Karagöz**  
LeadFinder – 2025  
🛠 `.NET | Docker | MySQL | OSM API`
> "Discover smarter. Audit deeper."