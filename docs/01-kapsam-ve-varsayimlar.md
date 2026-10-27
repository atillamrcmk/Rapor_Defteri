# Kapsam ve varsayımlar

Bu belge, Merkez Kontrol Rapor Defteri’nin ilk sürümünde neyin yapılacağını, neyin yapılmayacağını ve hangi gözlemlere dayanarak ilerlendiğini sabitler.

## Amaç

Uygulama, elle tutulan günlük merkez kontrol rapor defterinin şu işlerini kurum içi kapalı ağda dijitalleştirir:

- rapor kaydı oluşturma
- kişi, sicil ve koğuş bilgisiyle arama
- kurum dışı geçici hareket ve dönüş takibi
- araç geçiş takibi
- vardiya açma, devir ve kapama
- günlük ve vardiya raporlarının yazdırılması

İlk sürüm bir kayıt ve raporlama aracıdır. Resmî defterin hukukî yerine geçtiği, nitelikli elektronik imza ürettiği veya başka kurumsal sistemlere bağlandığı kabul edilmez. Devir ekranındaki “devralan” bilgisi, işlemi yapan hesap ve sunucu saatidir; elektronik imza değildir.

## Bu sürümde yapılmayanlar

- Çevrimdışı istemci ve sonradan senkronizasyon
- CDN, Google Fonts, harici kimlik doğrulama, telemetri, çevrimiçi lisans kontrolü
- T.C. kimlik numarası, suç, teşhis veya benzeri hassas alanlar
- Kapsamlı ziyaretçi yönetimi ve stok sistemi
- Ayrı bir PDF kütüphanesi (yazdırma görünümü tarayıcıdan PDF olarak kaydedilir)
- Kayıtların uygulamadan fiziksel silinmesi
- İnternet kesilince yerel kuyruğa alıp “başarılı” gösterme

Veritabanına ulaşılamazsa işlem başarılı sayılmaz. Kullanıcıya bağlantı hatası açıkça gösterilir.

## Gözlenen çalışma ortamı

İnceleme 27 Ekim 2026’da bu geliştirme bilgisayarında yapılmıştır:

| Gözlem | Değer |
| --- | --- |
| İşletim sistemi | Windows 11 Pro, derleme 10.0.26200 |
| Önceden yüklü SDK | .NET SDK 9.0.203 |
| Önceden yüklü çalışma zamanları | .NET 8.0.15 ve 9.0.4 |
| PostgreSQL | 18.1, Windows hizmeti `postgresql-x64-18`, çalışıyor |
| Kimlik doğrulama | `pg_hba.conf` içinde `127.0.0.1/32` için `scram-sha-256` |
| Docker | Yüklü değil |
| Çalışma klasörü | Boştu; mevcut bir proje silinmedi |

PostgreSQL hizmetinin parolası bu ortamda bulunamadı (`PGPASSWORD` ve `pgpass.conf` yok). Mevcut kümenin parolası tahmin edilmedi ve `pg_hba.conf` değiştirilmedi. Geliştirme ve test için ayrı bir yerel küme kullanılacaksa bu, kurumun asıl sunucusunun kimlik doğrulamasını zayıflatmaz.

## Teknoloji kararları

### PostgreSQL

İlk teknik tercihte SQL Server yazıyordu. Bu projede veritabanı PostgreSQL olacaktır. SQL Server kurulumu, yedekleme görevi veya T-SQL prosedürü yazılmayacaktır. Yedekler `pg_dump` ile alınır.

Sürüm kararı: geliştirme makinesinde doğrulanan sürüm PostgreSQL 18.1’dir. Uygulama PostgreSQL 16 ve üzeri ile çalışacak şekilde sıradan SQL kullanır; PostgreSQL 18’e özel sanal üretilmiş kolon veya `uuidv7()` kullanılmaz. Kurum sunucusunda hangi küçük sürümün kurulacağı kurulum sırasında doğrulanır. PostgreSQL’in SQL Server’daki gibi Standard/Enterprise edition ayrımı yoktur; bu belge bir edition varsaymaz.

### .NET

27 Ekim 2026 destek durumu:

- .NET 10 LTS: aktif destek, bitiş 14 Kasım 2028
- .NET 8 LTS ve .NET 9 STS: ikisi de 10 Kasım 2026’da destekten çıkıyor

Hedef çerçeve `net10.0` seçilmiştir. Makinede yüklü olan 8 ve 9 çalışma zamanları iki hafta içinde desteksiz kalacağı için yeni kurumsal uygulamanın hedefi yapılmadı. Geliştirme sırasında .NET SDK 10.0.403 ve çalışma zamanı 10.0.14 kullanıcı klasörüne kurulmuştur; sistem klasöründeki SDK 9.0.203 yerinde bırakılmıştır. Kurum sunucusuna .NET 10 Hosting Bundle 10.0.14 veya aynı 10.0 yama çizgisinin internetsiz kurulum paketi olarak önceden indirilmesi gerekir. Geliştirme makinesine paket inmesi, kurum bilgisayarlarının internete çıkacağı anlamına gelmez.

### Arayüz

ASP.NET Core MVC ve Razor. Bootstrap veya başka bir ön yüz kütüphanesi CDN’den yüklenmez. Stil ve davranış `wwwroot` altındaki yerel CSS ve JavaScript dosyalarındadır.

Kimlik doğrulama ASP.NET Core Identity ve uygulama çerezidir. Kaynak kodda veya sabit bir yapılandırmada yönetici parolası yoktur.

## Dağıtım varsayımı

Kurum sunucusunun işletim sistemi bu geliştirme oturumunda görülmedi. Güvenli varsayım:

- Uygulama sunucusu Windows Server 2022 veya Windows 11’dir.
- İstemciler aynı yerel ağdaki masaüstü tarayıcılardır.
- Tek bilgisayar kurulumu, aynı uygulamanın `localhost` üzerinden açılmasıdır; ayrı bir kod dalı yoktur.
- PostgreSQL yalnızca uygulama sunucusundan erişilir. Veritabanı portu istemci bilgisayarlara açılmaz.
- İstemciler uygulama sunucusunun HTTPS portuna bağlanır. Geliştirmede HTTP yalnızca `localhost` içindir.

Bu varsayım yanlışsa (örneğin Linux uygulama sunucusu) yayınlama komutu ve hizmet tanımı değişir; veri modeli ve iş kuralları değişmez.

## İş kuralı varsayımları

Aşağıdakiler belirtimde açık değildi. Geri alınabilir biçimde şöyle kabul edildi:

1. Aynı anda yalnızca bir vardiya `Açık` veya `Devre hazır` olabilir. Kapalı vardiyalar arşivde kalır.
2. Kurum sicil veya dosya numarası kişi kimliğidir ve büyük/küçük harf ile boşluk farkı yok sayılarak benzersizdir. Ad soyad benzersiz değildir.
3. Aynı kişi yeniden kabul edilebilir. Bunun için ikinci kişi kartı açılmaz; aynı kişiye yeni bir kalış dönemi açılır. Aktif kalış varken ikinci kabul reddedilir.
4. Sicil numarası değişirse eski numara geçmişte tutulur ve aramada bulunmaya devam eder.
5. Tahliye veya kalıcı sevk, kişinin açık geçici çıkışı varken reddedilir. Önce dönüş kaydı gerekir.
6. Bir dönüş kaydı aynı hareket türündeki ve aynı gidilen yerdeki kişileri kapsar. Farklı yerlerden dönenler ayrı dönüş kaydı olur.
7. Vardiya `Devre hazır` iken olay kaydı girilebilir. Açık hareketler vardiya kapandı diye kapanmaz.
8. Devralma, vardiyayı kapatır. Yeni vardiya ancak görevli ayrıca açarsa başlar; saatler önceki kayıttan kontrolsüz kopyalanmaz.
9. Açık vardiyada da düzeltme gerekçe ister ve eski metni saklar. Kapanmış vardiyada bunu yalnızca vardiya sorumlusu yapabilir.
10. Kurum adı yapılandırmadadır. Kod içine gerçek bir kurum adı yazılmaz.
11. Saatler `timestamptz` olarak saklanır, ekranda `Europe/Istanbul` kuşağında gösterilir. Türkiye 2016’dan beri yaz saati uygulamamaktadır.
12. Geç kayıt eşiği: olay saati, kayıt anından 10 dakikadan daha eskiyse veya vardiya başlangıcından önceyse gerekçe zorunludur.
13. Olay saati sunucu saatinden en fazla 2 dakika ileride olabilir.
14. Araç kartındaki konum sınıfı `Belirsiz` ise araç ne içeride ne dışarıda gösterilir. Her geçiş bağımsız kayıttır.
15. Dışarıdan gelen araçta açık bir giriş varken ikinci giriş, kurum aracında açık bir çıkış varken ikinci çıkış reddedilir. Gün içinde birden fazla tamamlanmış ziyaret serbesttir.

## Belirsiz kalanlar

- Kurum sunucusunun gerçek işletim sistemi, disk düzeni ve sertifika otoritesi görülmedi.
- PostgreSQL’in kurumda uygulama sunucusuyla aynı makinede mi yoksa ayrı bir iç sunucuda mı duracağı kurulumda seçilecektir. Her iki durumda da istemciler veritabanına bağlanmaz.
- Yedeklerin ikinci kopyasının yolu (`D:\Yedek\RaporDefteri` gibi) kurum tarafından verilecektir. Uygulama bu yolu kendiliğinden uydurmaz.
- Vardiya sorumlusu ile operatörün aynı kişiye birden fazla rol olarak verilip verilmeyeceği kuruma bırakılmıştır. Uygulama bir hesaba birden fazla rol bağlayabilir.
