# Kullanım senaryoları

Aktörler: operatör, vardiya sorumlusu, yönetici, salt okunur kullanıcı. Herkes kendi hesabıyla girer. Ortak şifre yoktur.

## US-01 Giriş

Kullanıcı kullanıcı adı ve parolasını girer. Parola doğruysa çerez oturumu açılır ve ana sayfaya gider. Beş hatalı denemeden sonra hesap 15 dakika kilitlenir. Oturum 30 dakika işlem görülmezse düşer. Veritabanı yanıt vermezse “giriş başarılı” denmez; bağlantı hatası gösterilir.

İlk kurulumda hiç kullanıcı yoksa yalnızca kurulum ekranı açılır. Yönetici adını ve parolasını o ekranda belirler. Bundan sonra kurulum ekranı kapanır.

## US-02 Vardiya açma

Vardiya sorumlusu başlangıç ve bitiş tarih-saatini girer. Bitiş, başlangıçtan sonra olmalıdır; gece yarısını aşabilir. Örnek: 27 Ekim 2026 20:00 – 28 Ekim 2026 08:00. Görevli kullanıcıları seçer. Kayıt, işlemi yapan hesabı ve sunucu saatini tutar. Açık veya devre hazır başka vardiya varsa yenisi açılmaz.

## US-03 Kişi kartı

Operatör hükümlü/tutuklu ekler: sicil veya dosya numarası, ad, soyad, varsa koğuş/oda. Ad soyad aynı olan iki kişi iki karttır. Arama sonucunda ikisi de sicil ve koğuş ile ayrı satırlarda görünür. Uygulama isim benzerliğiyle bunları birleştirmez.

Yeniden kabulde operatör mevcut sicili seçer. Aktif kalış varsa işlem reddedilir. Aktif kalış yoksa aynı karta yeni kalış dönemi açılır.

## US-04 Hastaneye çıkış

Operatör açık vardiyada bir veya daha çok kişiyi seçer. Her kişi için sicil ve koğuş seçim listesinde görünür. Çıkışın olay saatini, gidilen yeri, gerekçeyi, varsa refakat bilgisini ve isteğe bağlı beklenen dönüş saatini girer. İsteğe bağlı olarak mevcut bir araç seferini bağlar.

Kaydetmeden önce üretilen cümleyi görür. Ek açıklama ayrı alandadır ve yapılandırılmış metnin yerine geçmez. Üç kişi birlikte çıkabilir. Üçü için de ayrı açık hareket oluşur; üçü de aynı çıkış kaydına bağlıdır.

Aynı kişinin hâlihazırda açık bir kurum dışı hareketi varsa ikinci çıkış hiç kaydedilmez. Grup çıkışında bir kişi bile uygun değilse grubun tamamı reddedilir.

## US-05 Kısmi dönüş

Açık hareketler ekranında üç kişiden ikisi seçilir. Dönüş saati her seçilen kişinin çıkış saatinden önce olamaz. Kayıt yalnızca seçilen iki kişiyi kapatır. Üçüncü kişi açık kalır ve ana sayfadaki “dönüş beklenen kişiler” listesinde durur. Dönüş, yapıldığı vardiyanın defterinde görünür; önceki çıkış kaydıyla bağı silinmez. Dönüş aracı, gidiş aracıyla aynı olmak zorunda değildir.

Aynı dönüş formu ikinci kez gönderilirse ikinci dönüş satırı açılmaz. İlk kayıtın sonucu gösterilir.

Beklenen dönüş saati boşsa kişi “gecikti” diye işaretlenmez. “Dönüş saati belirlenmedi” denir. Saat doluysa ve geçmişse yalnızca “beklenen saat geçti” bilgisi gösterilir.

## US-06 Sonraki vardiyada dönüş

Gece vardiyasında çıkan kişi sabah vardiyasında dönebilir. Sabah vardiyası açıkken dönüş kaydı sabah vardiyasına yazılır. Gece vardiyası kapandı diye bu hareket kapanmış sayılmaz.

## US-07 Kişi geçmişi

Kişi kartında kabul, hastane, adliye, diğer geçici çıkış, dönüş, sevk ve tahliye kronolojik durur. Tarih aralığı, hareket türü ve gidilen yere göre süzülür.

## US-08 Araç geçişi

Operatör plaka, araç türü, yön, amaç ve olay saatini girer. Tür listesi yönetici tarafından düzenlenir. Aynı plaka aynı gün içinde, önceki sefer kapandıktan sonra yeniden giriş yapabilir.

Dışarıdan gelen araçta giriş, çıkış beklenen bir sefer açar. Kurum aracında çıkış, dönüş beklenen bir sefer açar. Konum sınıfı belirsiz araçta her geçiş bağımsızdır; araç içeride veya dışarıda gösterilmez.

Kişi çıkışı bir sefere bağlansa bile seferin kapanması kişilerin dönüşünü kapatmaz. Kişinin dönmesi de aracı dışarı çıkmış saymaz.

## US-09 Diğer kayıtlar

Görevli veya ziyaretçi gelişi, malzeme teslimi, genel olay ve devir notu ayrı türlerdir. Serbest metin, kişi ve araç bağının yerine geçmez. Ziyaretçi için ad ve geliş amacı tutulur; ziyaretçi kimlik sistemi kurulmaz. Malzeme için tanım ve teslim yönü tutulur; stok bakiyesi tutulmaz.

## US-10 Devir

Vardiya sorumlusu devir ekranında henüz dönmeyen kişileri, açık araç seferlerini, takibi süren olayları ve aktarılacak notları görür. “Devre hazırla” açık hareketleri kapatmaz. Devralan sorumlu, devralma işlemini ayrıca onaylar. Ekranda işlemi yapan kullanıcı ve sunucu saati yazılır. Bu onay elektronik imza değildir. Onay, vardiyayı kapatır ve kapanış sürümünü dondurur.

## US-11 Kapanmış vardiyada düzeltme

Operatör kapanmış vardiyanın kaydını değiştiremez. Vardiya sorumlusu gerekçe yazarsa düzeltme kaydı oluşur. Eski metin ve alan değerleri durur. Yeni yazdırma “kapanış sürümü” ise eski çıktıyı değiştirmez. Güncel görünüm düzeltmeyi ve eski metni birlikte gösterir.

## US-12 Raporlar

- Günlük merkez kontrol raporu, olay saatine göre takvim günüdür. Gece vardiyasını ortadan bölüp “vardiya bu gündür” demez; gün değişince olaylar kendi günlerinin raporuna düşer.
- Vardiya raporu, vardiyanın başlangıç ve bitiş saatleri arasındadır. 20:00–08:00 vardiyasının 23:30 ve 01:15 olayları aynı vardiya raporunda kalır.
- Kişi dökümü ve plaka dökümü ayrı çıktılardır.
- Tarih aralığı ve kayıt türü listesi operatörün aramasıdır.

Yazdırma A4’tür. Kurum adı, başlık, aralık, kayıt numarası, olay saati, açıklama, sayfa numarası ve görevli alanları vardır.

## US-13 Yetkisiz istek

Salt okunur kullanıcı veya anonim oturum, kayıt oluşturma adresine doğrudan istek gönderirse işlem yapılmaz. Düğmenin gizlenmesi yeterli sayılmaz.

## US-14 Tanım ve kullanıcı yönetimi

Yönetici kullanıcı açar, rol verir, hesabı pasifleştirir ve araç türlerini düzenler. Bu yetki, eski rapor satırını iz bırakmadan değiştirme yetkisi vermez.
