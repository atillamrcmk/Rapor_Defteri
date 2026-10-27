# Mimari

## Biçim

Tek dağıtılan uygulama, modüler monolittir. Tarayıcı yalnızca MVC sayfalarını konuşur. PostgreSQL’e yalnız uygulama süreci bağlanır. Ayrı bir API sitesi, mesaj kuyruğu, olay kaynağı (event sourcing) veya mikroservis yoktur.

## Projeler

```
RaporDefteri.Domain
RaporDefteri.Application
RaporDefteri.Infrastructure
RaporDefteri.Web
RaporDefteri.Tests
```

Bağımlılık yönü:

```
Web → Application → Domain
Web → Infrastructure → Application → Domain
Tests → Domain, Application, Infrastructure, Web
```

Domain; Entity Framework, ASP.NET Core, Npgsql veya HTTP bilmez. Rapor cümlesi, saat eki, çıkış/dönüş uygunluğu, vardiya penceresi ve araç sefer kararı bu katmandadır.

Application; kullanım senaryoları, komut ve sorgu nesneleri, doğrulama ve politika adlarını taşır. Ef Core `DbContext` bu katmana konmaz.

Infrastructure; `AppDbContext`, Identity, PostgreSQL, kayıt numarası dizisi, denetim satırı ve yedek günlüğünün okunmasıdır. Genel amaçlı generic repository yoktur. Her depo, senaryonun ihtiyaç duyduğu sorguları açık metodlarla sunar.

Web; controller, Razor görünümü, çerez oturumu ve yerel statik dosyalardır. Controller, iş kuralını yeniden yazmaz. Komutu servise verir, hataları forma basar, başarıda ilgili kayda yönlendirir.

## Bilinçli olarak kurulmayanlar

- Generic repository ve her tablo için aynı CRUD tabanı
- CQRS çerçeve altyapısı, mediatR benzeri bir komut veri yolu
- Domain event dağıtıcısı, outbox, kuyruk
- Ayrı okuma modeli
- Otomatik PDF motoru
- İnternetten yüklenen ön yüz paketi

Komut ile sorgu aynı servis sınıfında yan yana durabilir. Ayrım, klasör karmaşası çıkarmak için değil, Domain’in saf kalması içindir.

## İstek akışı

1. Tarayıcı antiforgery belirteci taşıyan formu gönderir.
2. MVC modeli bağlar. Yetki filtresi rolü sunucuda denetler.
3. Uygulama servisi Domain kurallarını çalıştırır.
4. Uygunsa rapor metni üretilir. Metin, kaydın alanlarına yazılır; kullanıcı metni serbestçe değiştirip yapılandırılmış alanlarla çeliştiremez.
5. Kayıt, ilişkili hareket ve denetim satırı aynı `SaveChanges` çağrısındadır.
6. Başarı yalnızca bu çağrı tamamlanırsa ve tarayıcı yönlendirme yanıtını alırsa oluşur.
7. `NpgsqlException` veya bağlantı kopması 503 sayfasına düşer. Bu sayfa kayıt numarasını “alındı” diye göstermez.

## Eşzamanlılık

- Kayıt numarası PostgreSQL `report_entry_number_seq` dizisinden gelir. `MAX(numara)+1` kullanılmaz. İşlem geri alınırsa numara atlanabilir. Kesintisiz numara vaadi yoktur.
- Yinelenen POST, formda üretilen `IdempotencyKey` ile yakalanır. Veritabanında tekil indekstir. İkinci istek yeni hareket açmaz.
- Düzeltmede `xmin` sistem sütunu eşzamanlılık belirtecidir. İkinci kullanıcı eskimiş belirteçle kaydederse yazma olmaz.
- Grup çıkışı ve kısmi dönüş tek transaction’dır.

## Zaman

Olay saati (`EventAt`) ile sisteme giriş saati (`RecordedAt`) ayrı sütunlardır. `RecordedAt` sunucudan gelir, formdan alınmaz. Geç kayıtta ikisi de defterde görünür.

## Metin sürümü

`OriginalReportText` ilk kayıtta yazılır ve güncellenmez. Düzeltme `ReportText` alanını değiştirir, önceki metni `ReportRevision` satırına koyar. Vardiya kapanınca `ReportSnapshot` o andaki satırların kopyasını saklar. Şablon veya kişi kartı sonradan değişirse eski snapshot değişmez.

## Kimlik

Identity tabloları aynı veritabanındadır. Parola ASP.NET Core Identity’nin PBKDF2 özetiyle durur. Uygulama parolayı loglamaz. Bağlantı dizesi loga yazılmaz.

## Çalışma biçimleri

- Kurum sunucusu: IIS, ASP.NET Core Module, .NET 10 Hosting Bundle, PostgreSQL aynı makinede veya iç ağdaki ayrı veritabanı makinesinde.
- Tek bilgisayar: aynı yayınlanmış uygulama, Kestrel veya IIS Express değil, dokümante edilen `localhost` bağlaması.

İkisi de aynı derlemedir.

## Testlerin yeri

Kural testleri Domain fonksiyonlarını çağırır; veritabanı gerektirmez. Model testi, `xmin` belirtecinin ve kayıt numarası dizisinin EF modelinde durduğunu doğrular. HTTP yetki testi, uygulama ayağa kalkabiliyorsa WebApplicationFactory ile yapılır. Veritabanı kimlik bilgisi olmadığı için çalıştırılamayan test, belgede “geçti” diye işaretlenmez.
