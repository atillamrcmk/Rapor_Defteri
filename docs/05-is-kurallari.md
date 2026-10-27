# İş kuralları

Kurallar Domain’de toplanır. Servisler bunları transaction içinde uygular. Veritabanı kısıtları ikinci emniyettir; tek emniyet değildir.

## Kayıt numarası

- Numara `nextval('rapor.report_entry_number_seq')` ile üretilir ve `MK-` artı altı hane olarak yazılır.
- `SELECT MAX(record_number)` kullanılmaz.
- Eşzamanlı iki kayıt aynı numarayı alamaz.
- Geri alınan işlem numara boşluğu bırakabilir. Raporlar boşluksuz seri vaat etmez.

## Olay saati ve kayıt saati

- `RecordedAt` uygulama sunucusunun saatidir.
- `EventAt` kullanıcının girdiği olay saatidir ve gelecekte, sunucudan 2 dakikayı aşan pay bırakılarak, olamaz.
- Olay saati kayıt saatinden 10 dakikadan eskiyse veya vardiya başlangıcından önceyse `LateEntryReason` boş olamaz.
- Defter her iki saati de gösterir.

## Vardiya

- `EndsAt > StartsAt`. Takvim günü aynı olmak zorunda değildir.
- Yeni vardiya, durumu açık veya devre hazır olan bir vardiya varken açılmaz.
- Kayıt girmek için durumun açık veya devre hazır olması gerekir. Kapalı vardiyaya normal kayıt eklenmez. Sorumlu, gerekçeyle ek kayıt isterse bu ayrı bir düzeltme/ekleme yoludur ve denetim satırı yazar.
- Devre hazır olmak açık hareketi kapatmaz.
- Devralma, hazır olmayan vardiyada yapılamaz. `AcceptedBy` ve `AcceptedAt` boş kalarak kapama yapılamaz.
- `AcceptedAt` sunucu saatidir, form saati değildir.

## Geçici çıkış

- Kişi seçilmeden çıkış olmaz. Serbest metin kişi yerine geçmez.
- Seçilen her kişinin aktif kalışı olmalıdır.
- Seçilen kişilerin hiçbirinde açık geçici hareket olmamalıdır. Biri uygunsuzsa grup kaydı yazılmaz.
- Gidilen yer ve gerekçe boş olamaz.
- Beklenen dönüş saati boş olabilir. Doluysa çıkış saatinden önce olamaz.
- Araç bağı isteğe bağlıdır. Bağlamak araç seferinin durumunu değiştirmez.

## Dönüş

- Yalnızca durumu açık olan satırlar kapatılır.
- Aynı satır ikinci kez kapatılamaz. Yinelenen istek anahtarı aynıysa ilk sonuç döner, yeni satır açılmaz.
- Dönüş saati, kapatılan her çıkış saatinden büyük veya ona eşit olmalıdır.
- Seçim boş olamaz.
- Tek dönüş kaydındaki kişiler aynı hareket türünde ve aynı gidilen yerde olmalıdır.
- Dönüş vardiyası, işlemin yapıldığı açık vardiyadır. Çıkış başka vardiyada olabilir.
- Dönüş aracı, çıkış aracından bağımsız seçilebilir ve boş bırakılabilir.
- Kısmi dönüşte seçilmeyen kişiler açık kalır.

## Tahliye, sevk, kabul

- Tahliye ve kalıcı sevk dönüş beklemez, `temporary_absence` açmaz.
- Bu işlemler aktif kalışı kapatır.
- Açık geçici hareketi olan kişide tahliye ve sevk reddedilir.
- Kabul, sicil mevcutsa aynı `inmate` kaydını kullanır.
- Aktif kalış varken kabul reddedilir.
- Sicil hiç yoksa yeni kişi ve ilk kalış birlikte açılır.

## Araç

- Plaka ve araç türü zorunludur. Amaç zorunludur. Sürücü ve firma isteğe bağlıdır.
- Konum sınıfı belirsizse sefer “bağımsız” kaydedilir. İçeride veya dışarıda listesine girmez.
- Dışarıdan gelen araçta giriş, açık sefer yoksa “çıkış bekleniyor” açar. Açık sefer varken yeni giriş reddedilir. Eşleşen çıkış seferi kapatır. Açık giriş yokken yapılan çıkış, aracın dışarıda veya içeride olduğu iddiası olmadan bağımsız geçiş olarak yazılır.
- Kurum aracında çıkış, “dönüş bekleniyor” açar. Eşleşen giriş seferi kapatır. Açık çıkış yokken yapılan giriş bağımsız geçiştir.
- Bütün araçlar “önce giriş, sonra çıkış” diye zorlanmaz.
- Tamamlanmış seferden sonra aynı gün yeni sefer açılabilir.
- Sefer kapanışı kişi hareketlerini güncellemez. Kişi dönüşü seferi güncellemez. İkisi ancak kullanıcı aynı formda ilişki seçerse birbirine bağlanır.

## Metin

- Metin şablonla, sunucuda, Türkçe saat ekiyle üretilir. Ağ çağrısı yoktur.
- Eksik zorunlu alan varsa cümle uydurulmaz; kayıt reddedilir.
- İsteğe bağlı alan boşsa cümleden çıkarılır.
- Kullanıcı kayıttan önce metni önizler. Metin kutusu serbest düzenlemeye açık değildir.
- Ek açıklama ayrı sütundadır.
- İlk metin `original_report_text` olarak kalır. Şablon kodu değişince eski satırlar yeniden üretilmez.

Saat eki, dakika sıfırsa saatin, değilse dakikanın Türkçe okunuşuna göre gelir: 09.15’te, 12.40’ta, 11.20’de.

## Düzeltme ve iptal

- Her düzeltme `report_revision` yazar: eski değer, yeni değer, eski metin, yeni metin, gerekçe, kullanıcı, saat.
- Gerekçesiz düzeltme yoktur.
- Kapanmış vardiyada düzeltmeyi yalnız vardiya sorumlusu yapar. Operatör ve yönetici bu yolla sessiz değişiklik yapamaz.
- Yönetici rolü tek başına düzeltme yetkisi vermez.
- İptal fiziksel silme değildir. Gerekçe, kullanıcı ve saat zorunludur.
- Dönüşü olan çıkış kaydı iptal edilemez.
- Dönüş kaydının iptali, ilgili kişilerin açık hareketi yoksa o kişileri yeniden açık hale getirir. Başka açık hareket varsa iptal reddedilir.
- Düzeltme formu eskimiş `xmin` taşırsa kaydetmez. Kullanıcıya kaydın başkası tarafından değiştiği söylenir.

## Rapor dönemleri

- Günlük rapor, `Europe/Istanbul` takvim gününde `event_at` değeri o güne düşen satırlardır. Vardiya gece yarısını aşıyorsa günlük rapor vardiyayı tek parça saymaz; her olayı kendi gününe koyar.
- Vardiya raporu `shift_id` ile seçilir. 20:00–08:00 vardiyasının her iki gündeki olayları bu raporda birlikte durur.
- Kapalı vardiya yazdırılırken varsayılan içerik kapanış snapshot’ıdır. Güncel düzeltmeli görünüm ayrıca istenir ve “güncel, kapanış sürümünden farklı olabilir” başlığı taşır.

## Yedek bilgisi

Ana sayfadaki son yedek saati yalnızca `backup_log` satırından okunur. Dosya oluşmuş olması bu satırı yazmaz. Satır, döküm alındıktan ve `pg_restore --list` o dosyayı okuduktan sonra eklenir. Geri yüklemenin kendisi ayrı bir tatbikattır; betik tatbikatı otomatik yapmaz, belge bunu açıkça ayırır.
