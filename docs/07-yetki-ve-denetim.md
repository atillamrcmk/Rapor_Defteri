# Yetki ve denetim

## Roller

| Rol | Yapabildiği | Yapamadığı |
| --- | --- | --- |
| Operatör | Açık veya devre hazır vardiyada kayıt oluşturur, kendi yetkisindeki sorguları görür, açık vardiyada gerekçeli düzeltme ister | Vardiya açamaz, kapalı vardiyayı değiştiremez, kullanıcı yönetemez |
| Vardiya sorumlusu | Operatörün yapabildiklerine ek olarak vardiya açar, devre hazırlar, devralır, kapatır, kapalı vardiyada gerekçeli düzeltme ve ek kayıt yapar | Kullanıcı ve tür tanımı yönetemez, denetim satırını silemez |
| Yönetici | Kullanıcı, rol, araç türü ve kurum yapılandırmasının ekranlarını yönetir, denetim kayıtlarını görür | Bu rol tek başına geçmiş rapor metnini değiştirmez |
| Salt okunur | İzinli rapor ve defter ekranlarını görür | POST ile kayıt, düzeltme, vardiya veya kullanıcı işlemi yapamaz |

Bir hesaba birden fazla rol verilebilir. Örneğin küçük birimde vardiya sorumlusu aynı zamanda operatör olabilir. Yöneticiye ayrıca vardiya sorumlusu rolü verilmedikçe düzeltme uçları 403 döner.

İlk kullanıcı kurulum ekranından oluşur ve yalnızca Yönetici rolünü alır. Parola kaynak kodunda yoktur.

## Sunucu denetimi

Her eylem için politika vardır: `KayitGoruntule`, `KayitOlustur`, `VardiyaYonet`, `KayitDuzelt`, `KullaniciYonet`, `TanimYonet`. `[Authorize]` controller ve eylem üzerindedir. Menüden düğme gizlemek ek kolaylıktır; karar filtrede verilir.

Doğrudan URL veya elle hazırlanmış POST, aynı politikaya çarpar. Antiforgery belirteci olmayan POST reddedilir.

Kapalı vardiya düzeltmesinde politika yetmez. Servis, vardiya durumuna ve gerekçenin dolu olduğuna ayrıca bakar. Yönetici rolü bu kontrolü atlamaz.

## Parola ve oturum

- Identity PBKDF2 parola özeti
- En az 10 karakter, büyük harf, küçük harf, rakam ve simge
- 5 hatalı denemede 15 dakika kilit
- Çerez `HttpOnly`, `SameSite=Lax`
- Üretimde `Secure` çerez; HTTPS olmadan üretim oturumu açılmaz
- Kayan süre: 30 dakika
- Hesap pasifleştirilince güvenlik damgası değişir, açık oturum düşer

Parola sıfırlama e-postası yoktur. Kapalı ağda yönetici yeni parola belirler. Bu işlem denetim kaydına “parola sıfırlandı” diye geçer; parolanın kendisi geçmez.

## Veri bütünlüğü

- Operasyonel tablolarda uygulama `DELETE` çalıştırmaz.
- İptal: gerekçe, kullanıcı, saat, durum = iptal.
- Düzeltme geçmişi ayrı tablodadır. Web’de düzenleme eylemi yoktur.
- Bu tasarım, veritabanı yöneticisinin dosyayı veya SQL’i değiştiremeyeceği iddiası taşımaz. Uygulama hesapları denetim tablosuna `UPDATE` ve `DELETE` yetkisi almamalıdır. Ayrıntı kurulum belgesindedir.

## Eşzamanlı düzenleme

Düzeltme formu `xmin` değerini gizli alan olarak taşır. Servis, okuduğu satırın belirteci formdakiyle uyuşmuyorsa kaydetmez. Uyuşuyorsa EF `WHERE xmin = @original` şartını kullanır. İki sorumlu aynı anda kaydederse biri başarı görür, diğeri “kayıt başka bir işlemle değişti, yeniden açın” uyarısı görür. Sessiz üzerine yazma yoktur.

## Denetim kaydı

Şunlar `audit_event` tablosuna yazılır:

- kullanıcı oluşturma, rol değişikliği, pasifleştirme, parola sıfırlama
- vardiya açma, devre hazırlama, devralma, kapama
- gerekçeli düzeltme ve iptal (ayrıca `report_revision`)

Yazılmayanlar: parola, parola özeti, bağlantı dizesi, çerez.

## Log

Uygulama log seviyesi bilgi ve uyarıdır. İstek gövdesi loglanmaz. Hata ayrıntısı üretimde kullanıcıya dökülmez; bağlantı hatası genel metinle gösterilir. Ayrıntı sunucu logunda kalır ve bağlantı dizesini içermemelidir. `appsettings` içinde bağlantı dizesinin log filtresine düşmesi için özel bir sağlayıcı eklenmez.

## En az yetki

İki PostgreSQL rolü vardır:

- `rapor_migrator`: şema kurar, migration çalıştırır. Uygulama havuzu bu rolle çalışmaz.
- `rapor_app`: şema üzerinde `SELECT`, `INSERT`, `UPDATE`. `report_revision`, `report_snapshot` ve `audit_event` üzerinde `UPDATE` ve `DELETE` yoktur. Hiçbir iş tablosunda `DELETE` yoktur. Sequence için `USAGE` ve `UPDATE` vardır, çünkü `nextval` bunu ister.

Yönetici rolündeki insan kullanıcının veritabanı süper kullanıcı olması gerekmez. Veritabanı süper kullanıcısı uygulama hesabı değildir.
