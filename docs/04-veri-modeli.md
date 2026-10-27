# Veri modeli

Şema adı `rapor`. Kimlik tabloları Identity’nin kendi tablolarıdır. İşlemsel satırlar uygulamadan `DELETE` ile silinmez. İptal, durum alanıdır.

Tarih-saat sütunları `timestamptz` olarak tutulur. PostgreSQL bu türde yalnızca UTC ofsetini kabul ettiği için kayıt anı UTC’ye çevrilir. Ekranda `Europe/Istanbul` saati gösterilir. Olay saati ile sisteme yazılma saati ayrı sütunlardır.

## Ortak rapor kaydı

`report_entry` bütün türlerin defter satırıdır.

| Sütun | Anlamı |
| --- | --- |
| `id` | `uuid`, sistem kimliği |
| `record_number` | Kullanıcıya görünen numara, `MK-000123` biçimi, tekil |
| `event_at` | Olayın gerçekleştiği an |
| `recorded_at` | Sunucunun kaydı yazdığı an |
| `entry_type` | Kayıt türü |
| `shift_id` | Satırın göründüğü vardiya |
| `created_by_user_id` | Kaydı açan hesap |
| `report_text` | Güncel üretilmiş metin |
| `original_report_text` | İlk metin, değişmez |
| `additional_note` | İsteğe bağlı ek açıklama |
| `status` | Geçerli, düzeltilmiş, iptal |
| `late_entry_reason` | Geç kayıt gerekçesi |
| `idempotency_key` | Yinelenen istek anahtarı, tekil |
| `xmin` | Eşzamanlılık belirteci |

Türe özel alanlar bu tabloda çoğaltılmaz.

## Kişi ve kalış

`inmate`: `file_number`, `first_name`, `last_name`, arama için `normalized_full_name` ve `normalized_file_number`. Ad soyad üzerinde tekil kısıt yoktur. Normalize sicil üzerinde tekil kısıt vardır.

`inmate_file_history`: değişen siciller. Arama hem güncel sicile hem geçmişe bakar.

`custody_period`: kişinin kurumdaki bir kalışıdır. `started_at`, `ended_at`, `ward`, `status` (aktif, tahliye, sevk). Yeniden kabul yeni satır açar, yeni `inmate` açmaz. Aktif kalıştan en fazla bir tane olması kısmi tekil indeksle korunur: `inmate_id` üzerinde `status = aktif`.

## Geçici kurum dışı hareket

`temporary_absence` kişi bazındadır.

- `kind`: hastane, adliye, diğer
- `destination`, `reason`, `escort_note`
- `departed_at`, isteğe bağlı `expected_return_at`, `returned_at`
- `departure_report_entry_id`, `return_report_entry_id`
- `departure_shift_id`, `return_shift_id`
- isteğe bağlı `departure_vehicle_visit_id`, `return_vehicle_visit_id`
- `status`: açık, döndü, iptal

Üç kişilik çıkışta üç satır ve tek `report_entry` vardır. İki kişi dönünce iki satırın `returned_at` ve `return_report_entry_id` alanı dolar; üçüncü satır açık kalır. Açık hareket için kısmi tekil indeks vardır: aynı `inmate_id` için `status = açık` en fazla birdir. Bu, çelişen ikinci açık çıkışı veritabanında da engeller.

Tahliye ve kalıcı sevk bu tabloda durmaz. Onlar `release_detail` ve `transfer_detail` satırıdır ve kalış dönemini kapatır. Dönüş alanı yoktur.

`admission_detail` kabulün hangi kişiye ve hangi kalış dönemine ait olduğunu tutar.

## Araç

`vehicle_type`: yönetilebilir tür listesi. `name`, `is_active`, `sort_order`. Başlangıç verisi yemek, ring/sevk, ambulans, malzeme, bakım, resmî araç ve diğerdir. Kullanılmış tür pasifleştirilir, silinmez.

`vehicle_card`: plaka kartı. `plate_display`, `normalized_plate` tekil, `base_location` (dışarıdan gelen, kurumda bulunan, belirsiz), isteğe bağlı kurum/firma.

`vehicle_visit`: bir ziyaret veya sefer. Karttan ayrıdır.

- `status`: açık, tamamlandı, bağımsız
- `location_inference`: çıkış bekleniyor, dönüş bekleniyor, yok
- `started_at`, `start_direction`, `ended_at`
- `purpose`, `driver_name`, `organization_snapshot`, `vehicle_type_id`
- `start_report_entry_id`, `end_report_entry_id`

Belirsiz konum sınıfında `location_inference = yok` ve `status = bağımsız` olur. Bu satırlar “içeride” veya “dışarıda” listesine girmez. Aynı plakanın aynı gün içindeki ikinci ziyareti, öncekinin açık olmaması şartıyla yeni `vehicle_visit` satırıdır.

Kişi hareketindeki araç alanı yalnızca ilişkidir. Sefer kapanırken `temporary_absence` güncellenmez.

## Diğer türler

Her biri `report_entry` ile birebir:

- `visitor_detail`: ad, kurum/birim, amaç
- `material_detail`: tanım, yön (teslim alma / teslim etme), karşı taraf
- `general_event_detail`: başlık
- `handover_note_detail`: devir notu

Bu tablolar stok bakiyesi veya ziyaretçi kimlik dosyası tutmaz.

## Vardiya

`shift`: `starts_at`, `ends_at`, `status` (açık, devre hazır, kapalı), açan hesap ve `opened_at`, devir notu, devre hazırlayan hesap ve saat, devralan hesap ve `accepted_at`, kapatan hesap ve `closed_at`.

`shift_duty`: vardiyadaki görevli kullanıcılar.

Açık ve devre hazır için kısmi tekil kısıt, aynı anda ikinci aktif vardiyayı engeller. Kısıt sabit bir sayı satırı değil, `status in (açık, devre hazır)` üzerinde tekil bir işaret sütunudur.

## Sürüm ve denetim

`report_revision`: `report_entry_id`, eski ve yeni değerlerin JSON özeti, eski ve yeni metin, gerekçe, kullanıcı, sunucu saati. Uygulamanın güncelleme veya silme uç noktası yoktur.

`report_snapshot`: kapanan vardiya veya kilitlenen günlük rapor. `kind`, dönem, `version`, `body_json`, oluşturan kullanıcı, sunucu saati. Sonraki düzeltme yeni snapshot açabilir; eskisini güncellemez.

`audit_event`: kullanıcı açma, rol değişikliği, hesap pasifleştirme, vardiya açma, devralma ve kapama. Parola ve bağlantı dizesi yazılmaz.

`backup_log`: yedek betiği başarılı doğrulamadan sonra buraya satır ekler. Uygulama tarayıcı isteğiyle dosya kopyalamaz. Ana sayfa son satırı okur. Satır yoksa “doğrulanmış yedek bilgisi yok” denir; dosyanın diskte durması başarılı yedek sayılmaz.

## Numara dizisi

`report_entry_number_seq` PostgreSQL sequence nesnesidir. `nextval` transaction geri alınsa da harcanır. Çakışma olmaz, boşluk olabilir.

## Identity

`AspNetUsers` genişletmesi `display_name`, `is_active`, `created_at`. Roller: Operatör, Vardiya Sorumlusu, Yönetici, Salt Okunur. Kullanıcı tohum verisi yoktur.

## Aramalar

Kişi araması `normalized_full_name`, `normalized_file_number` ve kalıştaki koğuş üzerinde `LIKE` ile yapılır. Normalize etme uygulama tarafında `tr-TR` kültürüyle küçük harfe çevirmektir. Böylece `I/İ` farkı veritabanı collation’ına bırakılmaz. Benzerlik indeksi veya otomatik birleştirme yoktur.
