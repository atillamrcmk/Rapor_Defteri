# Kurulum ve yedekleme

Bu belge kurum sunucusu içindir. Geliştirme makinesinde paket indirmek serbesttir. Kurum bilgisayarları kurulumu çevrimdışı yapar; kurulacak dosyalar önceden indirilip içeri taşınır.

## Hedef

Varsayılan hedef Windows Server 2022 veya Windows 11’dir. PostgreSQL 18.1 geliştirme makinesinde görüldü. Kurumda PostgreSQL 16 veya daha yeni bir sürüm kullanılabilir. Uygulama SQL Server beklemez.

Geliştirme bilgisayarının derlemesi `net10.0` içindir. .NET 10 LTS desteği 14 Kasım 2028’de biter. .NET 8 ve 9 bu belge yazılırken 10 Kasım 2026’da destekten çıkmak üzeredir; kurum sunucusuna onlar kurulmaz.

## Önceden indirilecekler

İnternetli bir hazırlık bilgisayarında:

1. [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0), 10.0.14 veya daha yeni 10.0 yaması. Windows için `dotnet-hosting-10.0.x-win.exe`.
2. PostgreSQL 18 Windows kurulum paketi, kurum sürümüyle aynı. EDN veya EnterpriseDB paketinin SHA256 özeti indirilen dosyayla karşılaştırılır.
3. Uygulamanın `dotnet publish` çıktısı.
4. Bu depodaki `scripts` klasörü.

Bunlar çıkarılabilir ortama kopyalanır. Kurum sunucusu bu dosyaları internetten çekmez.

## Sunucu önkoşulları

- Windows güncellemeleri kurum politikasına göre uygulanmış olur.
- IIS rolü ve WebSockets / statik içerik özellikleri açıktır.
- Uygulama havuzu kimliği ayrı bir yerel Windows hesabıdır. Bu hesap PostgreSQL süper kullanıcısı değildir.
- Diskte uygulama, veritabanı ve yedek için ayrı dizinler vardır. Yedek dizini uygulama kimliğinin yazabildiği tek yer olmamalıdır; ikinci kopya başka bir yerel disk veya yalnızca yedek hesabının gördüğü paylaşımdır.

## PostgreSQL

1. PostgreSQL’i kurun. `listen_addresses` yalnızca `localhost` veya uygulama sunucusunun iç IP’si olsun. İstemci bilgisayarların bulunduğu ağa `5432` açmayın.
2. Güvenlik duvarında `5432` yalnızca uygulama sunucusundaysa dışarı kapalı kalır. Veritabanı ayrı makinedeyse yalnız uygulama sunucusunun IP’sine izin verilir.
3. `scram-sha-256` kalır. `trust` kuruma konulmaz.

`scripts/db/create-roles.sql` iki rol ve `rapor_defteri` veritabanını anlatır. Parolalar betiğe yazılmaz; kurulum sırasında değiştirilir.

```sql
-- Örnek isimler. Parolayı betiğe kaydetmeyin.
CREATE ROLE rapor_migrator LOGIN PASSWORD 'kurulumda-verilir';
CREATE ROLE rapor_app LOGIN PASSWORD 'kurulumda-verilir';
CREATE DATABASE rapor_defteri OWNER rapor_migrator;
```

Migrator şemayı kurar. Sonra tablo yetkileri `scripts/db/grant-app.sql` ile `rapor_app` hesabına verilir. Uygulama havuzu yalnızca `rapor_app` bağlantı dizesini kullanır.

## Migration

Geliştirme veya dağıtım makinesinde, migrator bağlantı dizesi ortam değişkeninde:

```powershell
$env:ConnectionStrings__Migrator = "Host=127.0.0.1;Port=5432;Database=rapor_defteri;Username=rapor_migrator;Password=..."
dotnet ef database update --project src/RaporDefteri.Infrastructure --startup-project src/RaporDefteri.Web
```

Uygulama açılışında kendiliğinden migration çalıştırmaz. Eksik şema varsa sayfalar kayıt başarılı göstermez; veritabanı hatası gösterilir.

## Uygulama

```powershell
dotnet publish src/RaporDefteri.Web -c Release -o C:\RaporDefteri\app
```

Yayın çıktısında `wwwroot` yerel CSS ve JS ile birlikte gelir. Sunucunun bu dosyaları CDN’den tamamlaması gerekmez.

`appsettings.Production.json` kurumda, yayın klasörünün dışında veya ACL ile kısıtlı bir dosyada durur:

```json
{
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Port=5432;Database=rapor_defteri;Username=rapor_app;Password=ORTAM-DEGISKENINDEN"
  },
  "Institution": {
    "Name": "Kurum adı buraya yazılır",
    "UnitName": "Merkez Kontrol"
  },
  "Security": { "RequireHttps": true },
  "DataProtection": { "KeyPath": "D:\\RaporDefteri\\keys" }
}
```

Parolayı dosyaya düz yazmak yerine `ConnectionStrings__Default` ortam değişkeni kullanın. Uygulama havuzu bu değişkeni görür. Dosyaya yazıldıysa NTFS izni yalnız havuz kimliği ve yöneticidedir.

## IIS

1. Hosting Bundle kurulduktan sonra IIS yeniden başlatılır (`net stop was /y` ardından `net start w3svc`).
2. Site bağlaması: iç DNS adı, port 443, kurum sertifikası.
3. HTTP 80 yalnızca HTTPS’e yönlendirir veya kapalıdır.
4. Uygulama havuzu: yönetilen kod yok, 64 bit, boşta kalma süresi vardiyayı düşünecek kadar uzun tutulabilir. Çerez süresi yine 30 dakikadır.
5. Site fiziksel yolu yayın klasörüdür.

Tek bilgisayar kullanımında IIS şart değildir:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "https://127.0.0.1:8443"
dotnet RaporDefteri.Web.dll
```

Bu adres yalnız o bilgisayardır. Geliştirme sertifikası kurum dağıtımı için kullanılmaz.

## Sertifika

Kurum içi bir sertifika otoritesi varsa sunucu sertifikası oradan gelir. Yoksa kurumca üretilmiş bir iç kök ve sunucu sertifikası istemci bilgisayarların güvenilen köklerine elle kurulur. İnternetten genel bir CA’ya ulaşmak gerekmez. Self-signed sertifika, her istemciye tek tek güven verilmeden tarayıcı uyarısı çıkarır; üretimde tercih edilmez. Belge bir sertifika ürünü dayatmaz.

`Security:RequireHttps` üretimde `true` kalır. Çerez `Secure` olduğu için HTTPS olmadan oturum açılmaz.

## İlk kullanıcı

Şema kurulduktan sonra tarayıcıdan site açılır. Kullanıcı yokken `/kurulum` görünür. Yönetici kullanıcı adı, görünen ad ve parola girilir. Parola kuralları ekranda yazılır. İşlemden sonra bu sayfa 404 döner. Yönetici, kullanıcı ekranından operatör ve vardiya sorumlusu hesaplarını açar.

## Yerel ağ ve portlar

| Port | Kim görür |
| --- | --- |
| 443 | Kurum yerel ağındaki istemci bilgisayarlar |
| 5432 | Yalnızca uygulama sunucusunun kendisi veya ayrıysa yalnızca uygulama sunucusunun IP’si |

İstemci bilgisayarlara PostgreSQL kurulmaz. İstemcide yalnızca tarayıcı vardır.

## Yedekleme

Yedek, tarayıcı isteğiyle dosya kopyalama değildir. Görev Zamanlayıcı, veritabanı makinesinde `scripts/backup/backup.ps1` betiğini çalıştırır.

Betiğin yaptığı iş:

1. `pg_dump --format=custom` ile döküm alır.
2. Dosyayı birincil yedek klasörüne yazar.
3. Aynı dosyayı kurumun verdiği ikinci yerel yola kopyalar. İkinci yol yoksa betik başarısız olur ve `backup_log` yazmaz.
4. `pg_restore --list` ile dosyanın okunabildiğini denetler.
5. Ancak bu adım başarılıysa `backup_log` tablosuna dosya adı, boyut, UTC saat ve her iki yolu ekler.

Önerilen sıklık: günde en az bir tam döküm, vardiya kapanışına denk gelecek şekilde. PostgreSQL WAL arşivi bu ilk sürümde kurulmaz. Bu, iki döküm arasındaki kaybın döküm aralığı kadar olabileceği anlamına gelir. Kurum daha sık döküm isterse görev tekrarını artırır.

### Geri yükleme tatbikatı

Dosyanın oluşması ve `pg_restore --list` başarılı olması, geri yüklemenin denendiği anlamına gelmez. Ayrı prosedür:

1. Üretim veritabanına dokunmayan `rapor_defteri_tatbikat` veritabanı açılır.
2. `pg_restore --dbname=rapor_defteri_tatbikat` çalıştırılır.
3. `SELECT COUNT(*) FROM rapor.report_entry` ve son `backup_log` satırı karşılaştırılır.
4. Sonuç bir tutanağa yazılır. Uygulama bu tatbikatı kendiliğinden “başarılı yedek” saymaz.

Geri yükleme üretimde gerekirse uygulama havuzu durdurulur, mevcut veritabanı yeniden adlandırılır, döküm yeni veritabanına alınır, uygulama `rapor_app` ile açılır. Eski veritabanı, tatbikat tutanağı yazılana kadar silinmez.

### Son başarılı yedek nasıl izlenir

Ana sayfa `backup_log` tablosundaki son satırın saatini gösterir. Satır yoksa veya son satır kurumun kabul ettiği süreden eskiyse bu bir arıza işaretidir. Dosya gezgininde döküm görmek yeterli kabul edilmez. Betik başarısız olursa satır eklemez; görev zamanlayıcının son sonuç kodu da izlenir.

## Güncelleme ve geri dönüş

1. Yeni yedek alınır ve `pg_restore --list` geçer. Geçmezse güncelleme başlamaz.
2. Uygulama havuzu durdurulur.
3. Mevcut yayın klasörü `C:\RaporDefteri\app-onceki` olarak adlandırılır.
4. Yeni yayın kopyalanır.
5. Varsa migration, migrator hesabıyla `dotnet ef database update` ile uygulanır.
6. Havuz başlatılır. Giriş, bir kayıt okuma ve yazdırma görünümü denenir.
7. Sorun varsa havuz durur, klasör eski sürüme döner. Veritabanı migration’ı geri alınamıyorsa yedekten tatbikat prosedürü işletilir. Geri alınamayan migration üretilmemesi geliştirme kuralıdır: veri silen migration yazılmaz, iptal durum alanıyla yapılır.

## Geliştirme kümesi

Bu deponun geliştirildiği makinede PostgreSQL 18.1 hizmeti `5432` portunda duruyordu. Parolası bilinmediği için o hizmetin `pg_hba.conf` dosyasına ve verilerine dokunulmadı. Geliştirme için `scripts/dev/init-postgres.ps1` ayrı bir kümeyi `127.0.0.1:5433` üzerinde açar. Parolalar `%LOCALAPPDATA%\RaporDefteri\dev-passwords.json` içindedir ve depoya yazılmaz. Uygulama bağlantısı `dotnet user-secrets` ile `rapor_app` hesabına verilir. Kurum sunucusu bu betiği kullanmaz; kurum portu belgedeki `5432` düzenidir.

## Geliştirme bağımlılıkları

Geliştirme makinesinde NuGet paketleri çözüm derlenirken indirilir. Çevrimdışı derleme gerekiyorsa o paketler bir iç NuGet klasörüne konur:

```powershell
dotnet restore --packages .\nuget-cache
dotnet publish -c Release --source .\nuget-cache --no-restore
```

Kurum sunucusu `nuget.org` görmek zorunda değildir. Yayın çıktısı derlenmiş haldedir.
