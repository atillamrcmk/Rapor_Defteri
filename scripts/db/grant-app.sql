-- Migration uygulandıktan sonra rapor_migrator ile çalıştırılır.
-- rapor_app operasyon satırlarını silemez. Düzeltme, anlık görüntü ve denetim satırlarını güncelleyemez.
-- rapor_backup yalnızca okur ve doğrulanmış yedek satırı ekler.

GRANT USAGE ON SCHEMA rapor TO rapor_app;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA rapor TO rapor_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA rapor TO rapor_app;
REVOKE DELETE ON ALL TABLES IN SCHEMA rapor FROM rapor_app;

DO $$
DECLARE role_table text;
BEGIN
    FOREACH role_table IN ARRAY ARRAY['AspNetUserRoles', 'AspNetUserClaims', 'AspNetUserLogins', 'AspNetUserTokens', 'AspNetRoleClaims']
    LOOP
        EXECUTE format('GRANT DELETE ON TABLE rapor.%I TO rapor_app', role_table);
    END LOOP;
END $$;

REVOKE UPDATE, DELETE ON TABLE rapor.report_revision, rapor.report_snapshot, rapor.audit_event FROM rapor_app;
REVOKE INSERT, UPDATE, DELETE ON TABLE rapor.backup_log FROM rapor_app;

GRANT USAGE ON SCHEMA rapor TO rapor_backup;
GRANT SELECT ON ALL TABLES IN SCHEMA rapor TO rapor_backup;
GRANT INSERT ON TABLE rapor.backup_log TO rapor_backup;

ALTER DEFAULT PRIVILEGES FOR ROLE rapor_migrator IN SCHEMA rapor
    GRANT SELECT, INSERT, UPDATE ON TABLES TO rapor_app;
ALTER DEFAULT PRIVILEGES FOR ROLE rapor_migrator IN SCHEMA rapor
    GRANT USAGE, SELECT ON SEQUENCES TO rapor_app;
