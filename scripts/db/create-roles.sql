-- Parolalar bu dosyada durmaz.
-- psql -v migrator_password=... -v app_password=... -v backup_password=... -f scripts/db/create-roles.sql
-- Rol zaten varsa komut hata verir. Geliştirme kümesi için scripts/dev/init-postgres.ps1 tekrar çalıştırılabilir.

CREATE ROLE rapor_migrator LOGIN PASSWORD :'migrator_password';
CREATE ROLE rapor_app LOGIN PASSWORD :'app_password';
CREATE ROLE rapor_backup LOGIN PASSWORD :'backup_password';
CREATE DATABASE rapor_defteri OWNER rapor_migrator;
