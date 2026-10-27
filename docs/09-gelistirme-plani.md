# Geliştirme planı

Sıra, çalışan dikey akış önce gelecek biçimde kuruludur. Belge, kod yazılmadan önceki plandır. Uygulama bittikten sonra bu dosyanın sonuna gerçekten çalıştırılan kontroller ve kalan eksikler eklenir. Plandaki bir madde, test edilmeden tamamlandı sayılmaz.

## Aşama 0 — Ortam

Gözlem `docs/01-kapsam-ve-varsayimlar.md` içindedir. Çözüm `net10.0` hedefler. Veritabanı PostgreSQL’dir. Mevcut PostgreSQL 18 hizmetinin parolası olmadığından o küme değiştirilmez.

## Aşama 1 — İskelet

- Solution ve beş proje
- Domain’in EF’ye referans vermemesi
- Identity, çerez, antiforgery, yerel CSS
- İlk migration
- Kurulum ekranı ve giriş

## Aşama 2 — Dikey akış

1. Kullanıcı girişi
2. Vardiya açma
3. Hükümlü/tutuklu ekleme ve arama
4. Hastaneye çıkış
5. Açık hareketten dönüş
6. Kişinin hareket geçmişi
7. Bu kayıtların rapor defterinde görünmesi

Bu aşama bitmeden araç, devir ve yazdırma “bitti” denmez. Dikey akış da kendi testleriyle birlikte biter.

## Aşama 3 — Genişletme

- Adliye ve diğer geçici çıkış
- Kabul, tahliye, kalıcı sevk
- Araç kartı, tür yönetimi, geçiş ve plaka geçmişi
- Ziyaretçi, malzeme, genel olay, devir notu
- Vardiya devri, devralma, kapama ve snapshot
- Günlük, vardiya, kişi, plaka ve aralık raporlarının yazdırma görünümü
- Kullanıcı, rol, denetim ekranları
- Kapalı vardiya düzeltmesi

## Aşama 4 — Kabul senaryoları

Aşağıdakiler test kodu veya elle yürütülen bir kontrol olarak ayrıca yazılır. Çalıştırılmayan satır geçti diye işaretlenmez.

| Senaryo | Nasıl doğrulanacak |
| --- | --- |
| Aynı isimli iki kişi karışmaz | Ayrı kimlik, arama iki satır döner |
| Üç çıkış, iki dönüş, biri açık | Domain dönüş fonksiyonu |
| Önceki vardiya çıkışı sonraki vardiyada kapanır | Dönüş komutu mevcut vardiyayı yazar, çıkış bağını korur |
| Gece yarısını aşan vardiya | Vardiya penceresi testi; günlük rapor gün böler, vardiya raporu bölmez |
| Çelişen ikinci açık çıkış | Kural testi ve kısmi tekil indeks |
| Yinelenen istek ikinci dönüş açmaz | Idempotency anahtarı |
| Araç dönüşü kişiyi kapatmaz | Araç planı kişi durumu döndürmez; servis kişi deposuna yazmaz |
| Aynı araç günde birden fazla ziyaret | Kapalı seferden sonra yeni sefer serbest, açık sefer varken değil |
| Tahliye dönüş beklemez | Tahliye geçici hareket açmaz |
| Kapalı vardiya gerekçesiz değişmez | Düzeltme kuralı |
| Düzeltme geçmişi durur | Revision nesnesi eski metni taşır |
| Eşzamanlı düzenleme | `xmin` modelde concurrency token; iki bağlam varsa entegrasyon testi |
| Yetkisiz URL | `[Authorize]` ve mümkünse HTTP testi |
| İnternetsiz arayüz | Razor/CSS/JS içinde `http://` veya `https://` varlık yok |
| Bağlantı kopunca başarı yok | Bağlantı istisnası 503 sayfasına gider |

## Bilinçli sadeleştirmeler

- PDF kütüphanesi yok.
- Generic repository yok.
- Uygulama açılışında migration yok.
- Demo veri yalnızca geliştirme ortamında ve açık komutla yüklenir. Üretim başlangıcına bağlı değildir. Kişiler kurmaca olduğunu adında taşır.

## Durum

27 Ekim 2026. Uygulama kodu, ilk migration ve geliştirme veritabanı vardır. Üretim kurulumu ve geri yükleme tatbikatı yapılmadı.

Çalıştırılanlar:

- `dotnet test`: 22 test geçti. Aynı isim, üç kişiden ikisinin dönüşü, sonraki vardiyada kapanış, gece yarısı, çelişen açık çıkış, dönüşten önce saat, ikinci dönüş, araç planı, araç bağının kişiyi kapatmaması, tahliyenin dönüş beklememesi, kapanmış vardiya, düzeltme metni, `xmin` bayatlığı, Türkçe saat eki, rapor cümlesi, bağlantı SQL durumu ve arayüz dosyalarında uzak adres yokluğu bu testlerdedir. Oturumsuz `GET /vardiya/ac` yönlendirmesi `WebApplicationFactory` ile denendi.
- PostgreSQL 18.1 üzerinde, mevcut 5432 hizmetine dokunmadan, `127.0.0.1:5433` geliştirme kümesi açıldı. `Initial` ve `UtcTimestamps` migration’ları uygulandı. `rapor_app` hesabının `report_entry` silmesi ve `audit_event` güncellemesi `psql` ile reddedildi.
- Tarayıcıda kurmaca hesapla şu akış yazıldı: ilk yönetici, vardiya sorumlusu, 27.10.2026 20:00–28.10.2026 08:00 vardiyası, `KURMACA-001` kabul (`MK-000001`), Şehir Hastanesi çıkışı (`MK-000002`), dönüş (`MK-000003`). Üç cümle kişi kartında ve defterde göründü. Açık hareket listesi çıkıştan sonra kişiyi gösterdi.

Çalıştırılmayanlar:

- İki veritabanı bağlamıyla eşzamanlı `xmin` çakışması.
- Aynı dönüş isteğinin veritabanında ikinci satır açmadığına dair entegrasyon testi. Kural ve tekil indeks var; yinelenen HTTP isteği bu oturumda tekrar gönderilmedi.
- Üç kişilik çıkışın tarayıcıda iki dönüşü. Bunu domain testi doğruladı.
- Sunucu bağlantısı kesildiğinde tarayıcının 503 sayfası. Sınıflandırıcı birim testi geçti; çalışan küme durdurulup ekran denenmedi.
- Yedek betiğinin iki yola döküm alması ve `pg_restore --list` sonrası `backup_log` yazması.
- Kapalı vardiya düzeltmesinin tarayıcıda eski metni koruması. Revision nesnesi birim testinde duruyor.
- IIS, sertifika ve üretim hesabıyla kurulum.
