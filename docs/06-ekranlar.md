# Ekranlar

Arayüz Türkçedir. Koyu lacivert (`#0B1F3A`), gri zemin (`#F4F6F8`) ve beyaz kartlar kullanılır. Yazı rengi koyu, punto masaüstünde okunacak büyüklüktedir. Karşılama görseli, animasyon ve tanıtım alanı yoktur. Kişi etiketlerinde “hükümlü/tutuklu” kullanılır.

Stil dosyası `wwwroot/css/site.css`, davranış `wwwroot/js/site.js` içindedir. İkisi de uygulama ile dağıtılır.

## Ana sayfa

- Aktif vardiyanın başlangıcı, bitişi, durumu ve görevlileri
- Son kayıtlar
- Dönüş beklenen hükümlü/tutuklular (sicil, ad, koğuş, çıkış saati, gidilen yer)
- Açık araç seferleri (yalnızca çıkış veya dönüş bekleyenler)
- Hızlı düğmeler: hastane çıkışı, dönüş, araç geçişi, genel olay, rapor defteri
- Son doğrulanmış yedek satırı veya “doğrulanmış yedek yok”
- Veritabanı sağlık kontrolü başarısızsa kırmızı bağlantı uyarısı

## Giriş ve ilk kurulum

`/giris` kullanıcı adı ve parola ister. `/kurulum` yalnızca kullanıcı tablosu boşken açılır. Yetkisiz adres `/erisim-yok` sayfasına düşer.

## Rapor defteri

Kronolojik tabloda kayıt numarası, olay saati, kayıt saati, tür, vardiya, durum ve metnin kısa hali vardır. Sayfalama 25 satırdır. Süzgeçler: tarih aralığı, tür, vardiya, durum, kayıt numarası. Satır detayında tam metin, ek açıklama, ilişkili kişiler, araç ve varsa düzeltme geçmişi görünür. Geç kayıtta her iki saat ve gerekçe birlikte durur.

## Yeni kayıt

Tür seçilir, forma yalnızca o türün alanları gelir. Kayıt iki adımdır: bilgiler, sonra önizleme. Önizlemede üretilen cümle salt okunurdur. Ek açıklama ayrıdır. Kaydet, idempotency anahtarını taşır. Düğme ikinci tıklamada istemcide de pasifleşir; asıl koruma sunucudadır.

Kişi alanı yazıldıkça sicil, ad soyad ve koğuş döner. Klavye okları ve Enter ile seçilir. Eski kayıttan “bu kişiyi kullan” yalnız kişiyi taşır; saat ve hareket durumu taşınmaz.

## Hükümlü/tutuklu

Arama: ad soyad, sicil, koğuş. Aynı ad birden fazla satır döndürür. Kartta kimlik, güncel koğuş, kalış dönemleri ve kronolojik hareket vardır. Hareket süzgeci: tarih aralığı, tür, gidilen yer.

Yeni kart ve yeniden kabul aynı ekran ailesindedir. Yeniden kabulde mevcut kart seçilir.

## Açık hareketler

Kişi listesi ve araç listesi ayrıdır. Kişi satırında dönüş seçimi vardır. Üç kişilik çıkış üç satırdır.

## Araç

Plaka geçmişi, seferler ve yeni geçiş formu. Tür yönetimi yönetici ekranındadır. Belirsiz konumlu plaka “konum bilinmiyor” olarak kalır; içeride/dışarıda rozeti almaz.

## Vardiya

Açma formu tarih-saat seçicileri kullanır. Devir ekranı açık kişiler, açık seferler, süren genel olaylar ve notları listeler. “Devre hazırla” ve “Devral ve kapat” ayrı düğmelerdir. Devralma onayında kullanıcı adı ve sunucunun kaydedeceği saat metin olarak belirtilir. Kapama sonrası kapanış sürümü bağlantısı çıkar.

## Rapor önizleme ve yazdırma

Günlük, vardiya, kişi, plaka ve tür/tarih listesi aynı yazdırma düzenini kullanır. A4 sayfasında kurum adı, başlık, dönem, sütunlar ve altta görevli/devir için imza çizgileri vardır. Sayfa numarası yazdırma CSS sayaçıdır. Tarayıcının “PDF olarak kaydet” komutu yeterlidir. Kapanmış vardiyada sürüm numarası başlıktadır.

## Yönetim

- Kullanıcı listesi, rol atama, pasifleştirme
- Araç türleri
- Denetim ve düzeltme geçmişi: vardiya sorumlusu kendi düzeltmelerini, yönetici kullanıcı işlemlerinin kaydını görür. Salt okunur kullanıcı bu geçmişi açamaz. Geçmiş satırının düzenleme formu yoktur.

## Bağlantı hatası sayfası

`/hata/veritabani` 503 döner. Metin, kaydın yazılmadığını söyler. Başarı yönlendirmesi yapılmaz.

## Klavye

Form alanları belgede yukarıdan aşağıya doğal sekme sırasındadır. Arama kutusunda ok tuşları çalışır. Kaydet düğmesi formun sonundadır.
